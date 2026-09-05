using UnityEngine;
using Unity.Netcode;

namespace Infront
{
    /// <summary>
    /// Baut aus Code eine stilisierte Figur (Rumpf, Kopf, zwei Arme, zwei Beine)
    /// statt der nackten Kapsel. Laeuft auf Spieler UND Bot - man sieht Gegner
    /// und Verbuendete jetzt als Figuren, nicht als Pillen.
    ///
    ///  - Beine und Arme pendeln aus der Laufgeschwindigkeit
    ///  - der Kopf neigt sich in die Zielrichtung (hoch / runter)
    ///  - beim Tod kippt die Figur nach vorne
    ///  - fuer den eigenen Spieler ist die Figur unsichtbar (First Person)
    ///
    /// Rueckfallebene: die Kapsel ("Body") bleibt als GameObject erhalten, nur
    /// ihr Renderer wird abgeschaltet. Schlaegt der Aufbau fehl, kann man ihn
    /// mit einem Schalter wieder anmachen. Spaeter ersetzt eine echte
    /// Mixamo-Figur (Datei unter Assets/_Project/Art/Resources/) diese hier -
    /// die Anbindung steht dann schon.
    ///
    /// NICHT pruefbar: wie die Figur aussieht.
    /// </summary>
    public sealed class CharacterVisual : NetworkBehaviour
    {
        static readonly Color Cloth = new Color(0.22f, 0.24f, 0.27f);

        /// <summary>Uniform der echten Figur. Dunkel und matt, damit ein
        /// einziges Material ueber die ganze Figur nicht wie eingefaerbte Haut
        /// aussieht.</summary>
        static readonly Color Uniform = new Color(0.20f, 0.21f, 0.18f);
        static readonly Color Ausruestung = new Color(0.13f, 0.13f, 0.12f);
        static readonly Color Skin = new Color(0.55f, 0.42f, 0.34f);

        Health _health;
        IAimSource _aim;
        NetworkPlayerController _npc;   // nur am Spieler - fuer die geduckte Haltung
        Renderer _capsuleRenderer;

        Transform _figure;
        Transform _head;
        Transform _legL, _legR, _armL, _armR;
        Material _clothMat, _skinMat, _markenMat;

        // P7: echtes Figuren-Modell (Mixamo o.ae.), wenn vorhanden.
        Animator _animator;
        bool _usingRealModel;
        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int DeadParam = Animator.StringToHash("Dead");

        /// <summary>
        /// Um so viel sinkt die Figur waehrend des Sterbens.
        ///
        /// Die Sterbe-Animation legt den Koerper um die HUEFTE flach, und die
        /// Huefte bleibt dabei auf der Hoehe, auf der sie im Stehen war. Der
        /// Wurzel-Transform bleibt derweil am Boden - den bewegt der
        /// CharacterController, nicht der Animator, denn die Wurzelbewegung der
        /// Animation wird nicht uebernommen. Ergebnis: die Leiche liegt
        /// waagerecht, aber in der Luft. Genau so wurde es gemeldet.
        ///
        /// Der Zweig ohne Animator kippt die Figur schon immer selbst um und
        /// senkt sie dabei (Vector3.down * 0,55). Fuer das echte Modell fehlte
        /// das Senken - es gab ja eine Animation, also schien nichts noetig.
        ///
        /// Gemessen mit LeichenTests: der tiefste Knochen lag bei y = 0,90, der
        /// Boden bei 0,02 - also 0,88 m Luft. Um 0,78 gesenkt bleibt der
        /// tiefste Knochen bei rund 0,10 m; der Koerper hat um ihn herum noch
        /// Dicke, damit liegt er auf und steckt nicht im Boden.
        /// </summary>
        const float TotAbsenken = 0.78f;

        float _stride;
        Vector3 _lastPos;
        bool _hasLastPos;
        float _deathLean;   // 0 = aufrecht, 1 = umgekippt
        Vector3 _deathAxis = Vector3.right;   // Kippachse (aus der Schussrichtung)
        bool _dead;
        bool _hiddenForOwner;
        bool _built;

        public bool HasFigureForTests => _built && _figure != null
            && (_usingRealModel || _figure.childCount >= 5);
        public bool UsingRealModelForTests => _usingRealModel;
        public bool HiddenForOwnerForTests => _hiddenForOwner;
        public bool LeaningForTests => _deathLean > 0.5f;
        public bool CapsuleHiddenForTests => _capsuleRenderer != null && !_capsuleRenderer.enabled;

        public override void OnNetworkSpawn()
        {
            _health = GetComponent<Health>();
            _aim = GetComponent<IAimSource>();
            _npc = GetComponent<NetworkPlayerController>();

            var body = transform.Find("Body");
            if (body != null)
            {
                _capsuleRenderer = body.GetComponent<Renderer>();
                if (_capsuleRenderer != null) _capsuleRenderer.enabled = false;
            }

            BuildFigure();

            // Nur der eigene Spieler sieht seine Figur nicht (First Person).
            // IsLocalPlayer trifft NUR das eigene Spielerobjekt - Bots gehoeren
            // dem Server, waeren mit IsOwner faelschlich auch "eigen".
            if (IsLocalPlayer)
            {
                _hiddenForOwner = true;
                if (_figure != null) _figure.gameObject.SetActive(false);
            }

            GetComponent<TeamTint>()?.RefreshRenderers();

            if (_health != null)
            {
                _health.Died += OnDied;
                _health.Revived += OnRevived;
                _health.DiedWithInstigator += OnDiedFrom;
                if (!_health.IsAlive) _dead = true;
            }

            _lastPos = transform.position;
        }

        public override void OnDestroy()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
                _health.DiedWithInstigator -= OnDiedFrom;
            }
            if (_clothMat != null) Destroy(_clothMat);
            if (_skinMat != null) Destroy(_skinMat);
            base.OnDestroy();
        }

        void OnDied() => _dead = true;

        /// <summary>Kippachse aus der Schussrichtung waehlen: die Figur faellt
        /// von der Kugel weggeschoben.</summary>
        void OnDiedFrom(GameObject instigator)
        {
            _dead = true;
            if (instigator == null) { _deathAxis = Vector3.right; return; }
            Vector3 fromShooter = transform.position - instigator.transform.position;
            Vector3 local = transform.InverseTransformDirection(fromShooter);
            local.y = 0f;
            if (local.sqrMagnitude < 0.0001f) { _deathAxis = Vector3.right; return; }
            local.Normalize();
            // um diese Achse gedreht kippt die Figur in Richtung "local"
            _deathAxis = new Vector3(local.z, 0f, -local.x);
        }

        void OnRevived()
        {
            _dead = false;
            _deathLean = 0f;
            _deathAxis = Vector3.right;
            if (_figure != null)
            {
                _figure.localRotation = Quaternion.identity;
                _figure.localPosition = Vector3.zero;
                _figure.localScale = Vector3.one;
                if (!_hiddenForOwner) _figure.gameObject.SetActive(true);
            }
        }

        // ------------------------------------------------------------------

        void BuildFigure()
        {
            // P7: liegt ein echtes Figuren-Modell unter Resources/Models/figur,
            // wird das benutzt (mit Animator). Sonst die Wuerfel-Figur wie bisher.
            var real = AssetLibrary.Model("figur");
            if (real != null)
            {
                var go = Instantiate(real, transform);
                go.name = "Figur";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                _figure = go.transform;
                _animator = go.GetComponentInChildren<Animator>();
                _usingRealModel = true;
                ZieheFigurAn(go);
                _built = true;
                return;
            }

            _clothMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FigureCloth" };
            Paint(_clothMat, Cloth);
            _skinMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FigureSkin" };
            Paint(_skinMat, Skin);

            _figure = new GameObject("Figur").transform;
            _figure.SetParent(transform, false);
            _figure.localPosition = Vector3.zero;

            // Rumpf + Huefte
            Cube("Huefte", _figure, new Vector3(0f, 0.92f, 0f), new Vector3(0.42f, 0.28f, 0.26f), _clothMat);
            Cube("Rumpf", _figure, new Vector3(0f, 1.28f, 0f), new Vector3(0.5f, 0.6f, 0.3f), _clothMat);
            Cube("Rucksack", _figure, new Vector3(0f, 1.28f, -0.2f), new Vector3(0.34f, 0.44f, 0.16f), _clothMat);

            _head = Cube("Kopf", _figure, new Vector3(0f, 1.72f, 0f), new Vector3(0.28f, 0.28f, 0.3f), _skinMat);
            Cube("Helm", _head, new Vector3(0f, 0.12f, -0.02f), new Vector3(1.15f, 0.6f, 1.15f), _clothMat);

            // Arme (Drehpunkt an der Schulter)
            _armL = Limb("ArmL", _figure, new Vector3(-0.34f, 1.5f, 0f), 0.55f, _clothMat);
            _armR = Limb("ArmR", _figure, new Vector3(0.34f, 1.5f, 0f), 0.55f, _clothMat);

            // Beine (Drehpunkt an der Huefte)
            _legL = Limb("BeinL", _figure, new Vector3(-0.13f, 0.86f, 0f), 0.86f, _clothMat);
            _legR = Limb("BeinR", _figure, new Vector3(0.13f, 0.86f, 0f), 0.86f, _clothMat);

            _built = true;
        }

        /// <summary>Gibt dem echten Modell eine Uniform und ein Teamkennzeichen.
        ///
        /// Warum ueberhaupt: die Mixamo-Figur ist ohne Texturen heruntergeladen
        /// worden - im FBX steckt keine einzige Bilddatei, nur eine
        /// Diffuse-Farbe. Zusammen mit der alten Faerbung durch TeamTint, die
        /// JEDEN Renderer flaechig uebermalt hat, ergab das die lachsfarbenen
        /// Plastikpuppen der Rundgangsbilder.
        ///
        /// Ein einziges Material deckt die ganze Figur ab, Gesicht und Haende
        /// eingeschlossen. Deshalb ist die Uniform bewusst eine dunkle,
        /// matte Einsatzfarbe - das liest sich als Sturmhaube und Handschuhe
        /// und nicht als nackte Haut in Falschfarbe.</summary>
        void ZieheFigurAn(GameObject go)
        {
            _clothMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = "FigurUniform" };
            Paint(_clothMat, Uniform);
            Mattieren(_clothMat, 0.16f);

            var gurt = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { name = "FigurAusruestung" };
            Paint(gurt, Ausruestung);
            Mattieren(gurt, 0.24f);

            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = i == 0 ? _clothMat : gurt;
                r.sharedMaterials = mats;
            }

            // Armbinden an beiden Oberarmen und ein Rueckenpanel. Nur diese
            // Teile faerbt TeamTint spaeter in die Mannschaftsfarbe.
            Armbinde(go, "LeftArm");
            Armbinde(go, "RightArm");
            Rueckenpanel(go);
        }

        static void Mattieren(Material m, float glanz)
        {
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", glanz);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        }

        /// <summary>Sucht den Oberarm-Knochen und legt eine Binde darum. Die
        /// Mixamo-Knochen heissen "mixamorig:LeftArm" - deshalb Endung
        /// vergleichen und nicht den ganzen Namen.</summary>
        void Armbinde(GameObject go, string knochenEnde)
        {
            var knochen = FindeKnochen(go.transform, knochenEnde);
            if (knochen == null) return;

            // Mitte zwischen Schulter und Ellenbogen. Direkt am Gelenk saesse
            // die Binde im Rumpf.
            Vector3 pos = knochen.childCount > 0
                ? Vector3.Lerp(knochen.position, knochen.GetChild(0).position, 0.45f)
                : knochen.position;

            var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            band.name = "Teamband_" + knochenEnde;
            var c = band.GetComponent<Collider>();
            if (c != null) Destroy(c);
            band.transform.SetParent(knochen, true);
            band.transform.position = pos;
            band.transform.rotation = knochen.rotation;
            band.transform.localScale = new Vector3(0.14f, 0.09f, 0.14f);
            band.AddComponent<TeamMarker>();
            band.GetComponent<Renderer>().sharedMaterial = Kennzeichenmaterial();
        }

        void Rueckenpanel(GameObject go)
        {
            var spine = FindeKnochen(go.transform, "Spine2") ?? FindeKnochen(go.transform, "Spine1")
                        ?? FindeKnochen(go.transform, "Spine");
            if (spine == null) return;

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "Teamband_Ruecken";
            var c = panel.GetComponent<Collider>();
            if (c != null) Destroy(c);
            panel.transform.SetParent(spine, true);
            panel.transform.position = spine.position - transform.forward * 0.17f
                                       + transform.up * 0.08f;
            panel.transform.rotation = transform.rotation;
            panel.transform.localScale = new Vector3(0.22f, 0.16f, 0.03f);
            panel.AddComponent<TeamMarker>();
            panel.GetComponent<Renderer>().sharedMaterial = Kennzeichenmaterial();
        }

        /// <summary>Weisse Grundfarbe mit Absicht: TeamTint setzt sie ueber
        /// einen MaterialPropertyBlock auf die Mannschaftsfarbe. Waere sie
        /// schon eingefaerbt, saehe ein Kennzeichen ohne Team falsch aus.</summary>
        Material Kennzeichenmaterial()
        {
            if (_markenMat == null)
            {
                _markenMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { name = "FigurTeamband" };
                Paint(_markenMat, Color.white);
                Mattieren(_markenMat, 0.10f);
            }
            return _markenMat;
        }

        static Transform FindeKnochen(Transform wurzel, string ende)
        {
            foreach (var t in wurzel.GetComponentsInChildren<Transform>(true))
                if (t.name.EndsWith(ende, System.StringComparison.Ordinal)) return t;
            return null;
        }

        static Transform Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return t;
        }

        /// <summary>Ein Glied: leerer Drehpunkt oben, darunter ein laenglicher Cube.</summary>
        static Transform Limb(string name, Transform parent, Vector3 jointPos, float length, Material mat)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent, false);
            joint.localPosition = jointPos;

            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seg.name = name + "_Seg";
            var col = seg.GetComponent<Collider>();
            if (col != null) Destroy(col);
            seg.transform.SetParent(joint, false);
            seg.transform.localPosition = new Vector3(0f, -length * 0.5f, 0f);
            seg.transform.localScale = new Vector3(0.16f, length, 0.16f);
            var r = seg.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return joint;
        }

        static void Paint(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
        }

        // ------------------------------------------------------------------

        void LateUpdate()
        {
            if (_figure == null || _hiddenForOwner) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            // Tempo aus der Positionsaenderung (funktioniert bei Spieler und Bot,
            // Server und Client).
            Vector3 now = transform.position;
            Vector3 delta = now - _lastPos;
            _lastPos = now;
            delta.y = 0f;
            float speed = delta.magnitude / dt;
            float speed01 = Mathf.Clamp01(speed / 9f);

            // P7: echtes Modell -> nur den Animator fuettern, den Rest macht der
            // Animator-Controller (Idle/Lauf-Blend, Sterben).
            if (_usingRealModel)
            {
                if (_animator != null)
                {
                    _animator.SetFloat(SpeedParam, speed);
                    _animator.SetBool(DeadParam, _dead);
                }
                if (_dead)
                {
                    _deathLean = Mathf.MoveTowards(_deathLean, 1f, dt * 3f);
                    // Falls der Controller keine Sterbe-Animation hat: sichtbar wegkippen.
                    if (_animator == null)
                        _figure.localRotation = Quaternion.AngleAxis(_deathLean * 86f, _deathAxis);
                    else
                        _figure.localPosition = Vector3.down * (_deathLean * TotAbsenken);
                }
                return;
            }

            if (_dead)
            {
                _deathLean = Mathf.MoveTowards(_deathLean, 1f, dt * 3f);
                // um die aus der Schussrichtung gewaehlte Achse umkippen
                _figure.localRotation = Quaternion.AngleAxis(_deathLean * 86f, _deathAxis);
                _figure.localPosition = Vector3.down * _deathLean * 0.55f;
                return;
            }

            // Schrittzyklus
            _stride += dt * (2f + speed01 * 12f);
            float swing = Mathf.Sin(_stride) * Mathf.Lerp(4f, 42f, speed01);

            SetPitch(_legL, swing);
            SetPitch(_legR, -swing);
            SetPitch(_armL, -swing * 0.7f);
            SetPitch(_armR, swing * 0.7f);

            // leichtes Auf-und-Ab beim Laufen + ruhiges Atmen im Stand
            float bob = Mathf.Abs(Mathf.Sin(_stride)) * 0.04f * speed01
                        + Mathf.Sin(Time.time * 1.5f) * 0.01f;
            _figure.localPosition = new Vector3(0f, bob, 0f);
            _figure.localRotation = Quaternion.Euler(0f, 0f, 0f);

            // Ducken: die Figur staucht sich zusammen (echtes Modell spaeter mit Animation).
            float crouch = _npc != null ? _npc.Crouch01 : 0f;
            _figure.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.72f, crouch), 1f);

            // Kopf neigt sich in die Zielrichtung
            if (_head != null && _aim != null)
            {
                Vector3 d = transform.InverseTransformDirection(_aim.AimDirection);
                float pitch = -Mathf.Atan2(d.y, Mathf.Max(0.01f, d.z)) * Mathf.Rad2Deg;
                _head.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -50f, 50f), 0f, 0f);
            }
        }

        static void SetPitch(Transform limb, float degrees)
        {
            if (limb != null) limb.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }
}
