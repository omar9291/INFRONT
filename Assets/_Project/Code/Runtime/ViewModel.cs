using UnityEngine;
using Unity.Netcode;

namespace Infront
{
    /// <summary>
    /// Die Waffe in der Hand des lokalen Spielers ("View Model").
    ///
    /// Vorher sah man gar keine Waffe - ein grosser Teil des fehlenden
    /// "Spiel"-Gefuehls. Dieses Bauteil baut aus Code ein stilisiertes Gewehr
    /// (bzw. eine kurze Pistole) direkt vor der Kamera und bewegt es lebendig:
    ///
    ///  - Wippen beim Laufen (Bob), staerker beim Sprinten
    ///  - Nachschwingen beim Umsehen (Sway)
    ///  - Rueckstoss-Ruck bei jedem Schuss
    ///  - Nachlade-Bewegung (Waffe kippt weg, Magazin faellt und kommt zurueck)
    ///  - Ziehen beim Waffenwechsel
    ///  - unsichtbar, wenn man tot ist oder jemandem zuschaut
    ///
    /// Haengt NUR am Spieler-Prefab (Bots werden aus der dritten Person gesehen
    /// und brauchen kein View Model). Wie bei <see cref="DamageFeedback"/> laeuft
    /// es ausschliesslich beim Besitzer.
    ///
    /// NICHT pruefbar: wie es aussieht und sich anfuehlt. Die Tests pruefen nur,
    /// dass das Modell gebaut wird, dass ein Schuss es messbar zurueckstoesst und
    /// dass es beim Tod verschwindet.
    /// </summary>
    [RequireComponent(typeof(NetworkWeapon))]
    public sealed class ViewModel : NetworkBehaviour
    {
        // Ruhelage vor der Kamera (rechte Hand, leicht unten).
        static readonly Vector3 BasePos = new Vector3(0.20f, -0.19f, 0.42f);
        static readonly Vector3 BaseEuler = new Vector3(1.5f, -4f, 0.5f);

        // ----------------------------------------------------------------
        //  Haltung echter Waffen-Modelle im Sichtfeld (P5).
        //  DAS ist die Stelle zum Nachjustieren, wenn eine Waffe verdreht
        //  oder verschoben in der Hand haengt - ich kann das nicht sehen.
        //  Jeweils: lokaler Versatz, Euler-Drehung, gleichmaesziger Maszstab.
        // ----------------------------------------------------------------
        struct HeldPose { public Vector3 Pos; public Vector3 Euler; public float Scale; }

        static HeldPose PoseFor(string key) => key switch
        {
            "waffe_pistole" => new HeldPose {
                Pos = new Vector3(0f, -0.02f, 0.10f), Euler = new Vector3(0f, 90f, 0f), Scale = 1f },
            "waffe_sniper" => new HeldPose {
                Pos = new Vector3(0f, -0.03f, 0.12f), Euler = new Vector3(0f, 90f, 0f), Scale = 1f },
            _ => new HeldPose { Pos = Vector3.zero, Euler = Vector3.zero, Scale = 1f },
        };

        NetworkWeapon _weapon;
        Health _health;
        NetworkPlayerController _npc;
        FirstPersonCamera _fpc;
        Transform _cam;

        Transform _rig;        // wird vor die Kamera gehaengt, traegt alle Teile
        Transform _magazine;   // eigenes Kind fuer die Nachlade-Bewegung
        Material _metalMat;
        Material _accentMat;

        // Laufwippen
        float _bobPhase;
        float _testBobSpeed = -1f;   // >= 0: Test speist die Geschwindigkeit ein

        // Umsehen-Nachschwingen
        Vector3 _prevCamEuler;
        Vector2 _sway;

        // Rueckstoss (klingt weich ab)
        Vector3 _recoilPos;
        Vector3 _recoilRot;

        // Nachladen 0..1 (0 = fertig)
        float _reloadT;
        bool _reloading;

        // Ziehen nach Waffenwechsel: 0 = ganz unten, 1 = oben
        float _draw = 1f;

        // Sprinthaltung 0..1 (Waffe schraeg nach unten) + Landungs-Stauchung
        float _sprintPose;
        float _landDip;
        float _prevVertVel;

        bool _hidden;
        bool _built;

        public bool HasModelForTests => _built && _rig != null && _rig.childCount > 0;
        public int PartCountForTests => _rig != null ? _rig.childCount : 0;
        public Vector3 ModelLocalPosForTests => _rig != null ? _rig.localPosition : Vector3.zero;
        public bool HiddenForTests => _hidden;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            _weapon = GetComponent<NetworkWeapon>();
            _health = GetComponent<Health>();
            _npc = GetComponent<NetworkPlayerController>();

            var main = Camera.main;
            if (main != null)
            {
                _cam = main.transform;
                main.TryGetComponent(out _fpc);
            }

            if (_cam == null)
            {
                // Ohne Kamera kein View Model - nicht abstuerzen.
                enabled = false;
                return;
            }

            // Der Kamera sagen, dass sie ihren Platzhalter-Wuerfel nicht bauen soll.
            _fpc?.HandOffViewModel();

            BuildRig();
            RefreshShape();

            if (_weapon != null)
            {
                _weapon.LocalFired += OnLocalFired;
                _weapon.ReloadingChanged += OnReloadingChanged;
                _weapon.WeaponSwitched += OnWeaponSwitched;
            }
            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Revived += OnRevived;
            }

            _prevCamEuler = _cam.eulerAngles;
        }

        public override void OnNetworkDespawn() => Cleanup();

        public override void OnDestroy()
        {
            Cleanup();
            base.OnDestroy();
        }

        void Cleanup()
        {
            if (_weapon != null)
            {
                _weapon.LocalFired -= OnLocalFired;
                _weapon.ReloadingChanged -= OnReloadingChanged;
                _weapon.WeaponSwitched -= OnWeaponSwitched;
            }
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }
            if (_rig != null) Destroy(_rig.gameObject);
            if (_metalMat != null) Destroy(_metalMat);
            if (_accentMat != null) Destroy(_accentMat);
            _rig = null;
            _built = false;
        }

        // ------------------------------------------------------------------
        //  Aufbau der Geometrie
        // ------------------------------------------------------------------

        void BuildRig()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Standard");

            _metalMat = new Material(lit) { name = "ViewModelMetal" };
            SetColor(_metalMat, new Color(0.13f, 0.14f, 0.16f));   // Gunmetal
            _accentMat = new Material(lit) { name = "ViewModelAccent" };
            SetColor(_accentMat, UiTheme.Accent);                  // Dark-Tactical-Orange

            _rig = new GameObject("ViewModel").transform;
            _rig.SetParent(_cam, false);
            _rig.localPosition = BasePos;
            _rig.localRotation = Quaternion.Euler(BaseEuler);

            // Teile werden in RefreshShape() nach Waffentyp (neu) aufgebaut.
        }

        /// <summary>Baut die Waffenteile passend zur aktiven Waffe neu.</summary>
        void RefreshShape()
        {
            if (_rig == null) return;

            // alte Teile weg
            for (int i = _rig.childCount - 1; i >= 0; i--)
                Destroy(_rig.GetChild(i).gameObject);
            _magazine = null;

            // P5: echtes Waffen-Modell, wenn es eins gibt (Pistole, Sniper).
            string modelKey = WeaponModelKey();
            GameObject real = modelKey != null ? AssetLibrary.Model(modelKey) : null;
            if (real != null)
            {
                var pose = PoseFor(modelKey);
                var m = Instantiate(real, _rig);
                m.name = "Modell";
                m.transform.localPosition = pose.Pos;
                m.transform.localRotation = Quaternion.Euler(pose.Euler);
                m.transform.localScale = Vector3.one * pose.Scale;
                foreach (var rr in m.GetComponentsInChildren<Renderer>())
                {
                    rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rr.receiveShadows = false;
                    rr.gameObject.layer = _rig.gameObject.layer;
                }
                _built = true;
                SetVisible(!_hidden);
                return;
            }

            var slot = _weapon != null && _weapon.Stats != null
                ? _weapon.Stats.SlotKind : WeaponStats.Slot.Primaer;

            if (slot == WeaponStats.Slot.Pistole)
            {
                BuildPistol();
            }
            else if (IsSubmachineGun())
            {
                BuildSubmachineGun();
            }
            else
            {
                BuildRifle();
            }

            _built = true;
            SetVisible(!_hidden);
        }

        /// <summary>Ist die aktive Waffe die Maschinenpistole? (Das Sturmgewehr
        /// baut sonst denselben Zweig - die MP soll kompakter aussehen.)</summary>
        bool IsSubmachineGun()
        {
            var s = _weapon != null ? _weapon.Stats : null;
            return s != null && !string.IsNullOrEmpty(s.DisplayName)
                   && s.DisplayName.IndexOf("aschinenpistole", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ----- Pistole -----------------------------------------------------
        void BuildPistol()
        {
            Part("Schlitten",   new Vector3(0f, 0f, 0.02f),      new Vector3(0.05f, 0.06f, 0.20f), _metalMat);
            Part("Lauf",        new Vector3(0f, 0.005f, 0.14f),  new Vector3(0.022f, 0.022f, 0.10f), _metalMat);
            Part("Griff",       new Vector3(0f, -0.09f, -0.05f), new Vector3(0.045f, 0.13f, 0.06f), _metalMat,
                                Quaternion.Euler(18f, 0f, 0f));
            Part("Abzugsbuegel", new Vector3(0f, -0.04f, -0.01f), new Vector3(0.03f, 0.035f, 0.05f), _metalMat);
            _magazine = Part("Magazin", new Vector3(0f, -0.13f, -0.05f), new Vector3(0.038f, 0.06f, 0.045f), _metalMat,
                             Quaternion.Euler(18f, 0f, 0f));
            Part("Korn",        new Vector3(0f, 0.05f, 0.12f),   new Vector3(0.006f, 0.014f, 0.01f), _metalMat);
            Part("Kimme",       new Vector3(0f, 0.048f, -0.06f), new Vector3(0.05f, 0.012f, 0.02f), _metalMat);
            Part("Streifen",    new Vector3(0f, 0.03f, 0.02f),   new Vector3(0.052f, 0.005f, 0.12f), _accentMat);
        }

        // ----- Sturmgewehr -----------------------------------------------
        void BuildRifle()
        {
            Part("Gehaeuse",    new Vector3(0f, 0f, 0f),         new Vector3(0.06f, 0.085f, 0.30f), _metalMat);
            Part("Oberschiene", new Vector3(0f, 0.052f, 0.0f),   new Vector3(0.03f, 0.014f, 0.32f), _metalMat);
            // Picatinny-Zaehne
            for (int i = 0; i < 6; i++)
                Part($"Zahn{i}", new Vector3(0f, 0.062f, -0.11f + i * 0.045f), new Vector3(0.032f, 0.01f, 0.014f), _metalMat);

            Part("Handschutz",  new Vector3(0f, 0.004f, 0.21f),  new Vector3(0.05f, 0.055f, 0.22f), _metalMat);
            Part("Schlitz_L",   new Vector3(-0.026f, 0.004f, 0.21f), new Vector3(0.006f, 0.03f, 0.16f), _accentMat);
            Part("Schlitz_R",   new Vector3(0.026f, 0.004f, 0.21f),  new Vector3(0.006f, 0.03f, 0.16f), _accentMat);
            Part("Lauf",        new Vector3(0f, 0.008f, 0.36f),  new Vector3(0.022f, 0.022f, 0.20f), _metalMat);
            // Muendungsbremse: drei Ringe
            for (int i = 0; i < 3; i++)
                Part($"Bremse{i}", new Vector3(0f, 0.008f, 0.45f + i * 0.03f), new Vector3(0.036f, 0.036f, 0.014f), _metalMat);

            Part("Ladehebel",   new Vector3(0.035f, 0.02f, -0.05f), new Vector3(0.02f, 0.02f, 0.06f), _metalMat);
            Part("Schaft_Rohr", new Vector3(0f, 0.0f, -0.20f),   new Vector3(0.02f, 0.02f, 0.12f), _metalMat);
            Part("Schaft",      new Vector3(0f, -0.008f, -0.30f), new Vector3(0.045f, 0.075f, 0.14f), _metalMat);
            Part("Wange",       new Vector3(0f, 0.03f, -0.28f),  new Vector3(0.03f, 0.02f, 0.12f), _metalMat);
            Part("Griff",       new Vector3(0f, -0.10f, -0.09f), new Vector3(0.045f, 0.12f, 0.055f), _metalMat,
                                Quaternion.Euler(15f, 0f, 0f));
            Part("Abzugsbuegel", new Vector3(0f, -0.045f, -0.03f), new Vector3(0.03f, 0.04f, 0.06f), _metalMat);
            _magazine = Part("Magazin", new Vector3(0f, -0.135f, 0.02f), new Vector3(0.045f, 0.14f, 0.07f), _metalMat,
                             Quaternion.Euler(-10f, 0f, 0f));
            Part("Magazin_Bogen", new Vector3(0f, -0.20f, 0.055f), new Vector3(0.043f, 0.06f, 0.06f), _metalMat,
                             Quaternion.Euler(-24f, 0f, 0f));
            Part("Korn",        new Vector3(0f, 0.085f, 0.30f),  new Vector3(0.008f, 0.03f, 0.012f), _metalMat);
            Part("Kimme",       new Vector3(0f, 0.08f, -0.08f),  new Vector3(0.03f, 0.022f, 0.02f), _metalMat);
            Part("Streifen",    new Vector3(0f, 0.045f, 0.03f),  new Vector3(0.055f, 0.006f, 0.22f), _accentMat);
        }

        // ----- Maschinenpistole ----------------------------------------
        void BuildSubmachineGun()
        {
            Part("Gehaeuse",    new Vector3(0f, 0f, 0f),         new Vector3(0.055f, 0.08f, 0.22f), _metalMat);
            Part("Oberschiene", new Vector3(0f, 0.048f, 0.02f),  new Vector3(0.028f, 0.012f, 0.18f), _metalMat);
            Part("Lauf",        new Vector3(0f, 0.006f, 0.18f),  new Vector3(0.02f, 0.02f, 0.12f), _metalMat);
            Part("Muendung",    new Vector3(0f, 0.006f, 0.25f),  new Vector3(0.03f, 0.03f, 0.03f), _metalMat);
            Part("Vordergriff", new Vector3(0f, -0.075f, 0.12f), new Vector3(0.03f, 0.09f, 0.035f), _metalMat,
                                Quaternion.Euler(-6f, 0f, 0f));
            Part("Griff",       new Vector3(0f, -0.095f, -0.05f), new Vector3(0.042f, 0.11f, 0.05f), _metalMat,
                                Quaternion.Euler(14f, 0f, 0f));
            Part("Abzugsbuegel", new Vector3(0f, -0.04f, -0.0f),  new Vector3(0.028f, 0.038f, 0.055f), _metalMat);
            _magazine = Part("Magazin", new Vector3(0f, -0.16f, -0.03f), new Vector3(0.04f, 0.18f, 0.055f), _metalMat,
                             Quaternion.Euler(6f, 0f, 0f));
            // Klappschaft: zwei duenne Buegel-Streben nach hinten
            Part("Schaft_L",    new Vector3(-0.02f, 0.0f, -0.20f), new Vector3(0.012f, 0.012f, 0.20f), _metalMat);
            Part("Schaft_R",    new Vector3(0.02f, 0.0f, -0.20f),  new Vector3(0.012f, 0.012f, 0.20f), _metalMat);
            Part("Schaft_Platte", new Vector3(0f, 0.0f, -0.30f),  new Vector3(0.05f, 0.06f, 0.02f), _metalMat);
            Part("Korn",        new Vector3(0f, 0.07f, 0.16f),   new Vector3(0.007f, 0.024f, 0.01f), _metalMat);
            Part("Kimme",       new Vector3(0f, 0.062f, -0.06f), new Vector3(0.026f, 0.018f, 0.018f), _metalMat);
            Part("Streifen",    new Vector3(0f, 0.04f, 0.03f),   new Vector3(0.05f, 0.005f, 0.16f), _accentMat);
        }

        /// <summary>
        /// Welches echte Modell passt zur aktiven Waffe? null = keins, dann
        /// baut <see cref="RefreshShape"/> die Wuerfel (Sturmgewehr, MP).
        /// </summary>
        string WeaponModelKey()
        {
            var s = _weapon != null ? _weapon.Stats : null;
            if (s == null) return null;
            if (s.SlotKind == WeaponStats.Slot.Pistole) return "waffe_pistole";
            if (!string.IsNullOrEmpty(s.DisplayName)
                && s.DisplayName.IndexOf("scharfsch", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "waffe_sniper";
            return null;
        }

        Transform Part(string name, Vector3 pos, Vector3 scale, Material mat)
            => Part(name, pos, scale, mat, Quaternion.identity);

        Transform Part(string name, Vector3 pos, Vector3 scale, Material mat, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var t = go.transform;
            t.SetParent(_rig, false);
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = scale;

            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            // In der Ebene der Kamera zeichnen, damit die Waffe nicht in Waenden steckt.
            go.layer = _rig.gameObject.layer;
            return t;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.6f);
        }

        // ------------------------------------------------------------------
        //  Ereignisse
        // ------------------------------------------------------------------

        void OnLocalFired()
        {
            _recoilPos += new Vector3(Random.Range(-0.006f, 0.006f), 0.010f, -0.055f);
            _recoilRot += new Vector3(-7f, Random.Range(-2.5f, 2.5f), Random.Range(-3f, 3f));
        }

        void OnReloadingChanged(bool reloading)
        {
            _reloading = reloading;
            if (reloading) _reloadT = 0.0001f;   // startet die Animation
        }

        void OnWeaponSwitched()
        {
            _draw = 0f;          // Waffe ist unten, wird hochgezogen
            RefreshShape();      // evtl. andere Waffenform
        }

        void OnDied() => SetHidden(true);
        void OnRevived() => SetHidden(false);

        void SetHidden(bool hidden)
        {
            _hidden = hidden;
            SetVisible(!hidden);
        }

        void SetVisible(bool visible)
        {
            if (_rig != null && _rig.gameObject.activeSelf != visible)
                _rig.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------
        //  Bewegung pro Frame
        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (_rig == null || _cam == null) return;

            // Zuschauen blendet die Waffe aus (Tod ueber das Health-Ereignis).
            // Im voll aufgezogenen Zielfernrohr ebenfalls - man schaut durchs Rohr.
            bool spectating = _fpc != null && _fpc.IsSpectating;
            bool scoped = _npc != null && _npc.ScopeAmount01 > 0.85f;
            bool wantHidden = spectating || scoped || (_health != null && !_health.IsAlive);
            if (wantHidden != _hidden) SetHidden(wantHidden);
            if (_hidden) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // --- Umsehen-Nachschwingen -----------------------------------
            Vector3 camEuler = _cam.eulerAngles;
            float dYaw = Mathf.DeltaAngle(_prevCamEuler.y, camEuler.y);
            float dPitch = Mathf.DeltaAngle(_prevCamEuler.x, camEuler.x);
            _prevCamEuler = camEuler;

            Vector2 swayTarget = new Vector2(
                Mathf.Clamp(-dYaw * 0.02f, -0.03f, 0.03f),
                Mathf.Clamp(dPitch * 0.02f, -0.03f, 0.03f));
            _sway = Vector2.Lerp(_sway, swayTarget, 1f - Mathf.Exp(-10f * dt));

            // --- Laufwippen ---------------------------------------------
            float speed = _testBobSpeed >= 0f ? _testBobSpeed : HorizontalCameraSpeed();
            float speed01 = Mathf.Clamp01(speed / 10f);
            _bobPhase += dt * (6f + speed01 * 8f);
            float bobAmt = Mathf.Lerp(0.0015f, 0.02f, speed01);
            Vector3 bob = new Vector3(
                Mathf.Cos(_bobPhase) * bobAmt,
                Mathf.Abs(Mathf.Sin(_bobPhase)) * bobAmt * -1f,
                0f);

            // --- Rueckstoss weich abklingen ----------------------------
            _recoilPos = Vector3.Lerp(_recoilPos, Vector3.zero, 1f - Mathf.Exp(-12f * dt));
            _recoilRot = Vector3.Lerp(_recoilRot, Vector3.zero, 1f - Mathf.Exp(-11f * dt));

            // --- Nachladen ---------------------------------------------
            Vector3 reloadPos = Vector3.zero;
            Vector3 reloadRot = Vector3.zero;
            if (_reloading || _reloadT > 0f)
            {
                float total = _weapon != null && _weapon.Stats != null
                    ? Mathf.Max(0.3f, _weapon.Stats.ReloadTime) : 2f;
                _reloadT += dt / total;
                float k = Mathf.Clamp01(_reloadT);
                // Waffe kippt weg und kommt zurueck (halbe Sinuswelle).
                float dip = Mathf.Sin(k * Mathf.PI);
                reloadPos = new Vector3(-0.05f * dip, -0.10f * dip, -0.03f * dip);
                reloadRot = new Vector3(25f * dip, -12f * dip, 8f * dip);

                if (_magazine != null)
                {
                    // Magazin faellt in der ersten Haelfte raus, in der zweiten rein.
                    float mag = k < 0.5f ? k * 2f : (1f - k) * 2f;
                    _magazine.localPosition += Vector3.down * 0.12f * mag;
                }

                if (!_reloading && _reloadT >= 1f) _reloadT = 0f;
                if (_reloadT >= 1f) _reloadT = _reloading ? 0.999f : 0f;
            }

            // --- Ziehen nach Wechsel ----------------------------------
            _draw = Mathf.MoveTowards(_draw, 1f, dt * 3.5f);
            float drawDown = (1f - Mathf.SmoothStep(0f, 1f, _draw)) * 0.28f;
            float drawRot = (1f - _draw) * 35f;

            // --- Sprinthaltung: Waffe schraeg nach unten, kein Nachladen -----
            bool sprinting = _npc != null && _npc.Input != null && _npc.Input.Sprint
                             && speed > 4f && !(_reloading || _reloadT > 0f) && _draw > 0.9f;
            _sprintPose = Mathf.MoveTowards(_sprintPose, sprinting ? 1f : 0f, dt * 5f);
            Vector3 sprintPos = _sprintPose * new Vector3(0.06f, -0.09f, -0.05f);
            Vector3 sprintRot = _sprintPose * new Vector3(-8f, 34f, -18f);

            // --- Landungs-Stauchung ---------------------------------------
            if (_npc != null)
            {
                float vv = _npc.VerticalVelocity;
                if (_prevVertVel < -6f && vv > -3f)
                    _landDip = Mathf.Clamp01(-_prevVertVel / 16f);
                _prevVertVel = vv;
            }
            _landDip = Mathf.MoveTowards(_landDip, 0f, dt * 4f);
            float landOffset = Mathf.Sin(Mathf.Clamp01(_landDip) * Mathf.PI) * 0.05f;

            // --- alles zusammensetzen --------------------------------
            Vector3 pos = BasePos
                          + new Vector3(_sway.x, _sway.y, 0f)
                          + bob
                          + _recoilPos
                          + reloadPos
                          + sprintPos
                          + Vector3.down * (drawDown + landOffset);

            Quaternion rot = Quaternion.Euler(BaseEuler
                          + new Vector3(_recoilRot.x + reloadRot.x - drawRot + sprintRot.x,
                                        _recoilRot.y + reloadRot.y + sprintRot.y,
                                        _recoilRot.z + reloadRot.z + sprintRot.z));

            // --- Zielen ueber Kimme/Korn: Waffe vor die Blickmitte ziehen ---
            float adsT = _npc != null ? _npc.Aim01 : 0f;
            if (adsT > 0.001f)
            {
                Vector3 adsPos = BasePos + new Vector3(-BasePos.x + 0.005f, 0.02f, -0.05f);
                Quaternion adsRot = Quaternion.Euler(BaseEuler + new Vector3(0f, 4f, 0f));
                float k = Mathf.SmoothStep(0f, 1f, adsT) * 0.9f;
                pos = Vector3.Lerp(pos, adsPos, k);
                rot = Quaternion.Slerp(rot, adsRot, k);
            }

            _rig.localPosition = Vector3.Lerp(_rig.localPosition, pos, 1f - Mathf.Exp(-25f * dt));
            _rig.localRotation = Quaternion.Slerp(_rig.localRotation, rot, 1f - Mathf.Exp(-25f * dt));
        }

        Vector3 _lastCamPos;
        bool _hasLastCamPos;

        float HorizontalCameraSpeed()
        {
            if (!_hasLastCamPos)
            {
                _lastCamPos = _cam.position;
                _hasLastCamPos = true;
                return 0f;
            }
            Vector3 d = _cam.position - _lastCamPos;
            _lastCamPos = _cam.position;
            d.y = 0f;
            return d.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        }

        // ------------------------------------------------------------------
        //  Test-Haken
        // ------------------------------------------------------------------

        /// <summary>Nur Tests: einen Schuss-Rueckstoss auf die Waffe geben.</summary>
        public void PokeRecoilForTests() => OnLocalFired();

        /// <summary>Nur Tests: die Nachlade-Bewegung starten.</summary>
        public void PokeReloadForTests() => OnReloadingChanged(true);

        /// <summary>Nur Tests: Laufgeschwindigkeit fuer die Wippen-Pruefung vorgeben
        /// (negativ = wieder normal aus der Kamera lesen).</summary>
        public void SetTestBobSpeed(float speed) => _testBobSpeed = speed;
    }
}
