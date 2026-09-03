using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Server-autoritativer First-Person-Charakter mit Zielen hoch/runter.
    ///
    /// Ablauf:
    ///  - Der besitzende Client liest jeden Frame seine Eingaben und schickt sie
    ///    als <see cref="PlayerInputCommand"/> an den Server (SubmitCommandRpc).
    ///  - Nur der Server bewegt den CharacterController, dreht den Koerper (Yaw)
    ///    und neigt den Ziel-Drehpunkt (Pitch).
    ///  - NetworkTransform (server-autoritativ) verteilt Position und Yaw.
    ///  - Der Pitch wird ueber eine eigene NetworkVariable verteilt, damit auch
    ///    andere Clients sehen, wohin gezielt wird.
    ///
    /// "Der Koerper" (Etappe 1): rechte Maustaste = ueber Kimme/Korn zielen
    /// (langsameres Umsehen, engeres Blickfeld, weniger Streuung; beim
    /// Scharfschuetzengewehr ein echtes Zielfernrohr mit Atem-Schwanken),
    /// Strg = ducken (kleiner, langsamer, leiser, tiefere Trefferzonen),
    /// Alt = schleichen (sehr langsam, dafuer unhoerbar). Die Bewegung hat
    /// jetzt Traegheit - man laeuft an und bremst ab statt sofort auf Tempo.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerController : NetworkBehaviour, IAimSource
    {
        // Realismus-Etappe Schritt 2: die Werte sind bewusst niedriger als
        // vorher. Vorher war 6 m/s Gehen und 10 m/s Sprinten - 10 m/s ist
        // Weltrekord-Tempo, und das ohne Ausruestung. Jetzt: zuegiges Gehen
        // mit Weste und Gewehr, und ein Sprint, den ein Mensch schafft.
        [Header("Bewegung")]
        [SerializeField] float _walkSpeed = 4.6f;
        [SerializeField] float _sprintSpeed = 7.2f;
        // Muessen mitgezogen werden: bei 4,6 m/s Gehen waere Ducken mit 2,6
        // fast so schnell wie Gehen. Geduckt kommt man wirklich kaum voran.
        [SerializeField] float _crouchSpeed = 1.9f;
        [SerializeField] float _sneakSpeed = 1.4f;
        // Im Anschlag geht man, man laeuft nicht.
        [SerializeField] float _adsSpeed = 2.6f;
        // 1,5 m waere Hochsprung aus dem Stand. 0,85 m ist ein Hindernis
        // uebersteigen - mehr kann niemand mit Ausruestung.
        [SerializeField] float _jumpHeight = 0.85f;
        [SerializeField] float _gravity = 20f;
        [SerializeField] float _turnLerp = 20f;

        // Bei _groundAccel 55 war die Zielgeschwindigkeit nach 0,11 s erreicht -
        // also praktisch sofort. Mit 14 dauert es rund 0,43 s: der Koerper muss
        // erst anschieben. _groundDecel 18 laesst einen beim Stehenbleiben noch
        // ein Stueck weiterrutschen, _airAccel 4 beendet das Umsteuern im Sprung.
        [Header("Traegheit (Gewicht der Bewegung)")]
        [SerializeField] float _groundAccel = 14f;
        [SerializeField] float _groundDecel = 18f;
        [SerializeField] float _airAccel = 4f;

        // Sprinten setzt nicht sofort ein und endet nicht abrupt. _sprintRamp
        // laeuft von 0 (Gehen) bis 1 (voller Sprint).
        [Header("Gewicht")]
        [SerializeField] float _sprintRampUp = 1.1f;
        [SerializeField] float _sprintRampDown = 0.5f;
        // Hartes Aufkommen kostet kurz Kontrolle: in dieser Zeit faellt die
        // Beschleunigung auf _landStunAccelMul. Danach geht es normal weiter.
        [SerializeField] float _landStunTime = 0.35f;
        [SerializeField, Range(0f, 1f)] float _landStunAccelMul = 0.3f;

        [Header("Zielen")]
        [SerializeField] Transform _aimPivot;
        [SerializeField] float _maxPitch = 80f;

        [Header("First Person")]
        [SerializeField] GameObject[] _hideForOwner;

        // Masze im Stehen bzw. geduckt (Boden bleibt jeweils bei y = 0).
        const float StandHeight = 1.8f;
        const float CrouchHeight = 1.15f;
        const float StandEye = 1.6f;
        const float CrouchEye = 1.02f;
        const float StandHeadHb = 1.75f;
        const float CrouchHeadHb = 1.12f;
        const float StandBodyHb = 0.95f;
        const float CrouchBodyHb = 0.62f;
        const float StandBodyHbHeight = 1.3f;
        const float CrouchBodyHbHeight = 0.9f;

        CharacterController _controller;
        IPlayerInputSource _input;
        KeyboardMouseInputSource _kbInput;   // nur wenn echte Maus/Tastatur
        NetworkWeapon _weapon;
        FirstPersonCamera _camera;
        Health _health;
        TeamMember _teamMember;
        float _viewYaw;
        float _viewPitch;

        Transform _hbHead;
        Transform _hbBody;
        CapsuleCollider _hbBodyCol;

        readonly List<TeamMember> _specList = new();
        int _specIndex;
        bool _wasSpectating;

        [Header("Rueckstoss")]
        [SerializeField] float _recoilRecovery = 16f;
        float _recoilPitch;   // negativ = nach oben
        float _recoilYaw;
        float _recoilHold;    // solange > 0: kein Rueckgang (man feuert gerade)
        float _fireBloom;     // 0..1, fuers Fadenkreuz

        // Zielen (nur Optik/Gefuehl beim Besitzer; die Streuung rechnet der Server
        // aus dem Kommando).
        float _aimT;          // 0 = Huefte, 1 = voll ueber Kimme/Korn
        float _breath = 1f;   // Luft zum Atem-Anhalten im Zielfernrohr
        float _landKick;      // kurze Blick-Senkung nach hartem Aufkommen
        bool _prevGrounded = true;
        float _lastFallSpeed;
        Breathing _breathing;     // Schritt 3: Atmung (nur oertlich, nur beim Besitzer)
        float _sprintRamp;        // 0 = Gehen, 1 = voller Sprint
        float _landStunLeft;      // Restzeit des Kontrollverlusts nach der Landung
        bool _serverPrevGrounded = true;
        float _serverFallSpeed;

        // Nur Client-Besitzer
        PlayerInputCommand _pending;
        bool _jumpLatched;

        // Nur Server
        PlayerInputCommand _serverCommand;
        float _verticalVelocity;
        Vector3 _horizVel;       // waagrechte Geschwindigkeit mit Traegheit
        bool _movementEnabled = true;
        float _stepNoiseTimer;   // Server: Abstand bis zum naechsten hoerbaren Schritt

        readonly NetworkVariable<float> _aimPitchNet = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // Duck-Grad 0..1 - andere Clients ducken die Figur und die Trefferzonen mit.
        readonly NetworkVariable<float> _crouchNet = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float VerticalVelocity => _verticalVelocity;

        /// <summary>Die Atmung des Spielers. Nur fuer Tests.</summary>
        public Breathing BreathingForTests => _breathing;

        /// <summary>Sprint-Anlauf 0..1. Nur fuer Tests.</summary>
        public float SprintRampForTests => _sprintRamp;

        /// <summary>Restzeit des Landungs-Kontrollverlusts. Nur fuer Tests.</summary>
        public float LandStunLeftForTests => _landStunLeft;

        /// <summary>Waagrechte Geschwindigkeit des Servers. Nur fuer Tests.</summary>
        public Vector3 HorizontalVelocityForTests => _horizVel;
        public float AimPitch => _aimPitchNet.Value;
        public float RecoilPitch => _recoilPitch;

        /// <summary>Duck-Grad 0..1 (0 = aufrecht). Fuer Figur und Trefferzonen.</summary>
        public float Crouch01 => _crouchNet.Value;

        /// <summary>Zielt der Besitzer gerade ueber Kimme/Korn? 0..1 (weiche Blende).</summary>
        public float Aim01 => _aimT;

        /// <summary>Wie stark das Zielfernrohr-Bild sichtbar ist (0 = keins / nicht
        /// gezielt, 1 = voll). Nur bei Waffen mit <see cref="WeaponStats.ScopeZoom"/> &gt; 1.</summary>
        public float ScopeAmount01
        {
            get
            {
                var s = _weapon != null ? _weapon.Stats : null;
                if (s == null || s.ScopeZoom <= 1f) return 0f;
                return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, _aimT));
            }
        }

        /// <summary>Nur Server: haelt der Spieler die Ziel-Taste (fuer die Streuung)?
        /// Beim Sprinten zaehlt das Zielen nicht.</summary>
        public bool ServerAimHeld => _serverCommand.Aim && !_serverCommand.Sprint;

        /// <summary>Die Eingabequelle dieses Spielers. Auch die Waffe liest hier.</summary>
        public IPlayerInputSource Input => _input;

        /// <summary>Ursprung und Richtung fuer Schuesse. Vom Server geneigt.</summary>
        public Transform AimPivot => _aimPivot;

        // IAimSource: die Waffe holt sich hier Ursprung und Richtung
        public Vector3 AimOrigin => _aimPivot != null ? _aimPivot.position : transform.position + Vector3.up * 1.6f;
        public Vector3 AimDirection => _aimPivot != null ? _aimPivot.forward : transform.forward;

        void Awake()
        {
            _controller = GetComponent<CharacterController>();
            // Atmung: rein oertlich, deshalb einfach hier dazu statt ueber den
            // SceneBuilder. Fehlt sie, laeuft alles weiter wie vorher.
            _breathing = GetComponent<Breathing>();
            if (_breathing == null) _breathing = gameObject.AddComponent<Breathing>();
            _health = GetComponent<Health>();
            _teamMember = GetComponent<TeamMember>();
            _weapon = GetComponent<NetworkWeapon>();

            _hbHead = transform.Find("Hitbox_Head");
            _hbBody = transform.Find("Hitbox_Body");
            if (_hbBody != null) _hbBodyCol = _hbBody.GetComponent<CapsuleCollider>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                _input ??= new KeyboardMouseInputSource(transform.eulerAngles.y, GameSettings.MouseSensitivity);
                _kbInput = _input as KeyboardMouseInputSource;

                _viewYaw = transform.eulerAngles.y;
                _viewPitch = 0f;

                var cam = Camera.main;
                if (cam != null && cam.TryGetComponent(out _camera))
                    _camera.SetTarget(_aimPivot != null ? _aimPivot : transform);

                // Eigenen Koerper ausblenden - sonst schaut man in die eigene Kapsel
                foreach (var part in _hideForOwner)
                    if (part != null)
                        foreach (var r in part.GetComponentsInChildren<Renderer>())
                            r.enabled = false;
            }

            if (IsServer)
            {
                _verticalVelocity = 0f;
                ApplyBodyShape(0f);
            }
            else
            {
                _controller.enabled = false; // nur der Server simuliert Bewegung
            }
        }

        /// <summary>Setzt eine andere Eingabequelle. Wird von PlayMode-Tests genutzt.</summary>
        public void SetInputSource(IPlayerInputSource source)
        {
            _input = source;
            _kbInput = source as KeyboardMouseInputSource;
        }

        /// <summary>Nur Server: Bewegung an/aus (z.B. waehrend Tod).</summary>
        public void SetMovementEnabled(bool enabled)
        {
            if (IsServer)
                _movementEnabled = enabled;
        }

        void Update()
        {
            if (IsOwner && _input != null)
            {
                // Tot und Runde laeuft noch -> zuschauen
                bool dead = _health != null && !_health.IsAlive;
                bool roundPlaying = MatchManager.Instance != null
                    && MatchManager.Instance.CurrentPhase == MatchManager.Phase.Playing;

                if (dead && roundPlaying)
                {
                    if (_camera != null) _camera.SetAimZoom(0f, 12f);
                    if (_kbInput != null) _kbInput.SensitivityScale = 1f;
                    UpdateSpectator();
                    _wasSpectating = true;
                    return;
                }
                if (_wasSpectating)
                {
                    _wasSpectating = false;
                    _camera?.StopSpectate();
                }

                // Bei Pause und offenem Kaufmenue ist die Maus frei - dann die
                // Sicht nicht mitdrehen lassen.
                bool paused = PauseMenu.IsPaused || BuyMenuHud.IsOpen;

                UpdateAiming(paused, dead);

                if (!paused)
                {
                    _viewYaw = _input.LookYaw;
                    _viewPitch = Mathf.Clamp(_input.LookPitch, -_maxPitch, _maxPitch);
                }

                // Rueckstoss geht erst zurueck, wenn man aufhoert zu feuern
                _recoilHold = Mathf.Max(0f, _recoilHold - Time.deltaTime);
                if (_recoilHold <= 0f)
                {
                    _recoilPitch = Mathf.MoveTowards(_recoilPitch, 0f, _recoilRecovery * Time.deltaTime);
                    _recoilYaw = Mathf.MoveTowards(_recoilYaw, 0f, _recoilRecovery * Time.deltaTime);
                }
                _fireBloom = Mathf.MoveTowards(_fireBloom, 0f, 2.5f * Time.deltaTime);
                _landKick = Mathf.MoveTowards(_landKick, 0f, 3f * Time.deltaTime);

                Vector2 sway = BreathSway();

                float finalYaw = _viewYaw + _recoilYaw + sway.x;
                float finalPitch = Mathf.Clamp(_viewPitch + _recoilPitch + sway.y - _landKick * 3f,
                    -_maxPitch, _maxPitch);

                _pending.Move = paused ? Vector2.zero : _input.Move;
                _pending.Yaw = finalYaw;
                _pending.Pitch = finalPitch;
                _pending.Sprint = !paused && _input.Sprint;
                _pending.Aim = !paused && _input.AimHeld;
                _pending.Crouch = !paused && _input.CrouchHeld;
                _pending.Walk = !paused && _input.WalkHeld;
                if (!paused && _input.JumpPressed)
                    _jumpLatched = true;

                // --- Schritt 3: Atmung ---------------------------------------
                // Die Atmung bekommt ihren Zustand von hier und liefert einen
                // kleinen Versatz zurueck, der auf den Blick gelegt wird.
                Vector2 atem = Vector2.zero;
                if (_breathing != null)
                {
                    _breathing.Sprinting = _pending.Sprint && _sprintRamp > 0.3f;
                    _breathing.Aiming = _aimT > 0.5f;
                    // Luft anhalten liegt auf derselben Taste wie im Fernrohr,
                    // damit man sich nichts Neues merken muss.
                    _breathing.WantHold = _input != null && _input.Sprint;
                    _breathing.Health01 = _health != null && _health.Max > 0
                        ? Mathf.Clamp01(_health.Current / (float)_health.Max)
                        : 1f;
                    _breathing.Suspended = paused || dead;
                    atem = _breathing.Offset;
                }

                // Kamera SOFORT lokal fuehren - kein Netzwerk, keine Verzoegerung
                if (_camera != null)
                    _camera.SetView(finalYaw + atem.x, finalPitch + atem.y);
            }

            // Nicht-Server-Instanzen: Ziel-Drehpunkt aus der NetworkVariable neigen
            // und die geduckte Haltung nachziehen.
            if (!IsServer)
            {
                ApplyBodyShape(_crouchNet.Value);
                if (_aimPivot != null)
                    _aimPivot.localRotation = Quaternion.Euler(_aimPitchNet.Value, 0f, 0f);
            }
        }

        /// <summary>Owner: Ziel-Blende, Kamera-Zoom, Maus-Empfindlichkeit.</summary>
        void UpdateAiming(bool paused, bool dead)
        {
            var s = _weapon != null ? _weapon.Stats : null;
            float scopeZoom = s != null ? s.ScopeZoom : 0f;

            bool wantAim = !paused && !dead && _input.AimHeld && !_input.Sprint;
            _aimT = Mathf.MoveTowards(_aimT, wantAim ? 1f : 0f, Time.deltaTime * 9f);

            if (_camera == null) return;

            float baseFov = _camera.BaseFov;
            float zoomedFov = scopeZoom > 1f ? baseFov / scopeZoom : baseFov * 0.82f;
            float targetFov = Mathf.Lerp(baseFov, zoomedFov, _aimT);
            // Nur setzen, wenn wir wirklich zielen - sonst 0 (Kamera bleibt normal).
            _camera.SetAimZoom(_aimT > 0.02f ? targetFov : 0f, scopeZoom > 1f ? 16f : 12f);

            if (_kbInput != null)
            {
                float aimScale = scopeZoom > 1f ? 0.32f : 0.72f;
                _kbInput.SensitivityScale = Mathf.Lerp(1f, aimScale, _aimT);
            }
        }

        /// <summary>Atem-Schwanken im Zielfernrohr. Umschalt (Sprint-Taste) haelt
        /// die Luft an - ruhiger, aber begrenzt.</summary>
        Vector2 BreathSway()
        {
            float scopeAmt = ScopeAmount01;
            if (scopeAmt < 0.5f)
            {
                _breath = Mathf.Clamp01(_breath + Time.deltaTime / 3f);
                return Vector2.zero;
            }

            bool holding = _input.Sprint && _breath > 0.02f;
            _breath = Mathf.Clamp01(_breath + (holding ? -1f / 2.2f : 1f / 3f) * Time.deltaTime);

            float amp = (holding ? 0.09f : 0.5f) * scopeAmt;
            float t = Time.time;
            float x = (Mathf.PerlinNoise(7.7f, t * 0.5f) - 0.5f) * 2f * amp * 0.8f;
            float y = (Mathf.PerlinNoise(t * 0.6f, 3.1f) - 0.5f) * 2f * amp;
            return new Vector2(x, y);
        }

        void UpdateSpectator()
        {
            if (_teamMember == null) return;

            BuildSpectateList();

            if (_specList.Count == 0)
            {
                // Wirklich niemand mehr am Leben - die Runde endet gerade eben.
                // Die Kamera NICHT einfrieren, sondern frei umsehen lassen.
                _camera?.StopSpectate();
                if (_input != null)
                    _camera?.SetView(_input.LookYaw,
                        Mathf.Clamp(_input.LookPitch, -_maxPitch, _maxPitch));
                return;
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) _specIndex++;
                if (mouse.rightButton.wasPressedThisFrame) _specIndex--;
            }
            _specIndex = ((_specIndex % _specList.Count) + _specList.Count) % _specList.Count;

            var target = _specList[_specIndex];
            var aim = target.GetComponent<IAimSource>();
            if (aim != null)
                _camera?.SetSpectate(aim.AimOrigin, aim.AimDirection);
        }

        /// <summary>
        /// Fuellt <see cref="_specList"/>: zuerst lebende Verbuendete. Ist keiner
        /// mehr uebrig (z.B. die Bombe tickt noch, waehrend das ganze Team tot
        /// ist), darf man auch lebenden Gegnern zuschauen - sonst friert die
        /// Kamera an der eigenen Leiche fest.
        /// </summary>
        void BuildSpectateList()
        {
            _specList.Clear();

            foreach (var m in Combatants.Everyone)
                if (m != null && m != _teamMember && m.TeamId == _teamMember.TeamId
                    && m.Health != null && m.Health.IsAlive)
                    _specList.Add(m);

            if (_specList.Count > 0) return;

            foreach (var m in Combatants.Everyone)
                if (m != null && m != _teamMember
                    && m.Health != null && m.Health.IsAlive)
                    _specList.Add(m);
        }

        /// <summary>Vom Waffen-Code aufgerufen: Rueckstoss auf die Sicht geben.</summary>
        public void AddRecoil(float up, float side)
        {
            // Ueber Kimme/Korn steht die Waffe ruhiger.
            float ads = Mathf.Lerp(1f, 0.7f, _aimT);
            _recoilPitch -= up * ads;
            _recoilYaw += side * ads;
            _recoilHold = 0.16f;
            _fireBloom = Mathf.Min(1f, _fireBloom + 0.18f);

            // Kraeftigerer Kick: Kamera-Zucken und ein kurzer Blickfeld-Stoss,
            // beides nach der Wucht der Waffe. Beim Zielen deutlich gedaempft.
            float kick = Mathf.Clamp(up, 0.4f, 4f);
            _camera?.Shake(Mathf.Clamp(0.04f + kick * 0.03f, 0.04f, 0.2f), 0.13f);
            _camera?.AddFovKick(Mathf.Min(0.5f + kick * 0.35f, 2.6f) * Mathf.Lerp(1f, 0.35f, _aimT), 26f);
        }

        /// <summary>Wie weit das Fadenkreuz aufgehen soll (0..1). Nur Anzeige.</summary>
        public float CrosshairSpread01
        {
            get
            {
                Vector3 v = _controller != null && _controller.enabled ? _controller.velocity : Vector3.zero;
                float sp = new Vector2(v.x, v.z).magnitude;
                float m = Mathf.Clamp01(sp / 10f);
                if (_controller != null && _controller.enabled && !_controller.isGrounded) m = 1f;
                float aimMul = (_pending.Aim && !_pending.Sprint) ? 0.35f : 1f;
                return Mathf.Clamp01((m * 0.7f + _fireBloom) * aimMul);
            }
        }

        /// <summary>Name des gerade beobachteten Kaempfers (fuer die Anzeige).
        /// Bei einem Gegner wird "Gegner" vorangestellt.</summary>
        public string SpectatingName
        {
            get
            {
                if (!_wasSpectating || _specList.Count == 0
                    || _specIndex >= _specList.Count || _specList[_specIndex] == null)
                    return null;

                var m = _specList[_specIndex];
                bool enemy = _teamMember != null && m.TeamId != _teamMember.TeamId;
                return enemy ? $"Gegner {m.DisplayName}" : m.DisplayName;
            }
        }

        void FixedUpdate()
        {
            if (IsOwner)
            {
                _pending.Jump = _jumpLatched;
                _jumpLatched = false;
                SubmitCommandRpc(_pending);
            }

            if (IsServer)
            {
                Simulate(_serverCommand, Time.fixedDeltaTime);
                _serverCommand.Jump = false;
            }
        }

        [Rpc(SendTo.Server)]
        void SubmitCommandRpc(PlayerInputCommand command)
        {
            bool keepJump = _serverCommand.Jump || command.Jump;
            _serverCommand = command;
            _serverCommand.Jump = keepJump;
        }

        void Simulate(PlayerInputCommand command, float dt)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, command.Yaw, 0f);

            // Ziel-Drehpunkt neigen (Server ist die Wahrheit)
            float pitch = Mathf.Clamp(command.Pitch, -_maxPitch, _maxPitch);
            _aimPitchNet.Value = pitch;

            // Ducken: Ziel-Grad aus der Taste, aber nur aufstehen, wenn oben Platz ist.
            float target = command.Crouch ? 1f : 0f;
            if (target < _crouchNet.Value && !HasHeadroom())
                target = _crouchNet.Value;
            _crouchNet.Value = Mathf.MoveTowards(_crouchNet.Value, target, dt * 6f);
            ApplyBodyShape(_crouchNet.Value);

            if (_aimPivot != null)
                _aimPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            bool frozen = MatchManager.Instance != null && MatchManager.Instance.IsFrozen;
            if (!_movementEnabled || frozen)
            {
                // Startsperre / Tod: sofort stehen, nur Schwerkraft.
                _horizVel = Vector3.zero;
                _sprintRamp = 0f;
                _landStunLeft = 0f;
                _verticalVelocity = _controller.isGrounded ? -2f : _verticalVelocity - _gravity * dt;
                _controller.Move(Vector3.up * _verticalVelocity * dt);
                return;
            }

            Vector3 wishDir = yawRotation * new Vector3(command.Move.x, 0f, command.Move.y);
            wishDir = Vector3.ClampMagnitude(wishDir, 1f);

            float c = _crouchNet.Value;
            bool aiming = command.Aim && !command.Sprint;

            // Sprint-Anlauf: die Rampe faehrt hoch, solange gesprintet werden
            // darf, und wieder herunter, sobald nicht. Dadurch setzt der Sprint
            // nicht schlagartig ein und endet nicht abrupt.
            bool willSprint = command.Sprint && c < 0.2f && !aiming;
            float rampRate = willSprint
                ? (_sprintRampUp > 0f ? dt / _sprintRampUp : 1f)
                : (_sprintRampDown > 0f ? dt / _sprintRampDown : 1f);
            _sprintRamp = Mathf.MoveTowards(_sprintRamp, willSprint ? 1f : 0f, rampRate);

            float speed = _walkSpeed;
            if (_sprintRamp > 0.001f && c < 0.2f && !aiming)
                speed = Mathf.Lerp(_walkSpeed, _sprintSpeed, _sprintRamp);
            else if (c > 0.05f)
                speed = Mathf.Lerp(_walkSpeed, _crouchSpeed, c);
            else if (command.Walk)
                speed = _sneakSpeed;
            if (aiming)
                speed = Mathf.Min(speed, _adsSpeed);

            // Landungs-Kontrollverlust (Server, damit er wirklich die Bewegung
            // betrifft und nicht nur den Blick). Der Blick-Ruck in LateUpdate
            // bleibt daneben bestehen.
            bool groundedNow = _controller.isGrounded;
            if (groundedNow && !_serverPrevGrounded && _serverFallSpeed < -6f)
                _landStunLeft = _landStunTime;
            if (!groundedNow) _serverFallSpeed = _verticalVelocity;
            _serverPrevGrounded = groundedNow;
            if (_landStunLeft > 0f) _landStunLeft = Mathf.Max(0f, _landStunLeft - dt);

            if (groundedNow)
            {
                _verticalVelocity = -2f;
                if (command.Jump && c < 0.2f)
                    _verticalVelocity = Mathf.Sqrt(2f * _gravity * _jumpHeight);
            }
            else
            {
                _verticalVelocity -= _gravity * dt;
            }

            // Traegheit: die waagrechte Geschwindigkeit faehrt zum Wunsch, statt
            // sofort dort zu sein. Das gibt der Bewegung Gewicht.
            Vector3 wishVel = wishDir * speed;
            float rate = !groundedNow
                ? _airAccel
                : (wishVel.sqrMagnitude > 0.04f ? _groundAccel : _groundDecel);
            if (_landStunLeft > 0f) rate *= _landStunAccelMul;
            _horizVel = Vector3.MoveTowards(_horizVel, wishVel, rate * dt);

            _controller.Move((_horizVel + Vector3.up * _verticalVelocity) * dt);

            // Hoerbare Schritte fuer die Bots. Schleichen (Alt) = komplett lautlos,
            // ducken = seltener, sprinten = weithin hoerbar.
            float planarSpeed = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
            if (!command.Walk && _controller.isGrounded && planarSpeed > 1f)
            {
                _stepNoiseTimer -= dt;
                if (_stepNoiseTimer <= 0f)
                {
                    bool sprinting = command.Sprint && c < 0.2f && planarSpeed > _walkSpeed + 0.5f;
                    _stepNoiseTimer = sprinting ? 0.30f : (c > 0.5f ? 0.75f : 0.50f);
                    SoundEvents.ServerReport(transform.position,
                        sprinting ? SoundEvents.SprintLoud : SoundEvents.WalkLoud,
                        _teamMember != null ? _teamMember.TeamId : Team.None);
                }
            }

            // Koerper schaut dorthin, wo gezielt wird (Kamera-Yaw), nicht in die
            // Laufrichtung. Schnelle, aber weiche Drehung.
            transform.rotation = Quaternion.Slerp(transform.rotation, yawRotation, _turnLerp * dt);
        }

        /// <summary>Ist ueber dem Kopf genug Platz zum Aufstehen?</summary>
        bool HasHeadroom()
        {
            if (_controller == null) return true;
            Vector3 origin = transform.position + Vector3.up * (CrouchHeight - _controller.radius + 0.05f);
            float dist = StandHeight - CrouchHeight;
            return !Physics.SphereCast(origin, _controller.radius * 0.95f, Vector3.up,
                out _, dist, 1 << 0, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Kapsel, Augenhoehe und Trefferzonen an den Duck-Grad anpassen.
        /// c = 0 (aufrecht) .. 1 (voll geduckt). Der Fuszpunkt bleibt bei y = 0.</summary>
        void ApplyBodyShape(float c)
        {
            if (_controller != null && _controller.enabled)
            {
                float h = Mathf.Lerp(StandHeight, CrouchHeight, c);
                _controller.height = h;
                _controller.center = new Vector3(0f, h * 0.5f, 0f);
            }

            if (_aimPivot != null)
            {
                Vector3 p = _aimPivot.localPosition;
                p.y = Mathf.Lerp(StandEye, CrouchEye, c);
                _aimPivot.localPosition = p;
            }

            if (_hbHead != null)
                _hbHead.localPosition = new Vector3(0f, Mathf.Lerp(StandHeadHb, CrouchHeadHb, c), 0f);

            if (_hbBody != null)
            {
                _hbBody.localPosition = new Vector3(0f, Mathf.Lerp(StandBodyHb, CrouchBodyHb, c), 0f);
                if (_hbBodyCol != null)
                    _hbBodyCol.height = Mathf.Lerp(StandBodyHbHeight, CrouchBodyHbHeight, c);
            }
        }

        void LateUpdate()
        {
            if (!IsOwner || _controller == null || !_controller.enabled) return;

            // Hartes Aufkommen -> kurzer Blick-Ruck nach unten + Blickfeld-Stoss.
            bool grounded = _controller.isGrounded;
            if (grounded && !_prevGrounded && _lastFallSpeed < -6f)
            {
                _landKick = Mathf.Clamp01(-_lastFallSpeed / 16f);
                _camera?.AddFovKick(3.5f, 22f);
            }
            if (!grounded) _lastFallSpeed = _verticalVelocity;
            _prevGrounded = grounded;
        }
    }
}
