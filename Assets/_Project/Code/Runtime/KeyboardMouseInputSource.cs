using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Echte Eingabequelle: liest Tastatur und Maus ueber das Input System.
    /// Phase 2 nutzt weiterhin direkte Geraeteabfragen, noch kein .inputactions-Asset.
    /// WASD = Bewegung, Umschalt = Sprint, Leertaste = Springen,
    /// Maus X/Y = Zielen, linke Maustaste = Feuern, R = Nachladen,
    /// rechte Maustaste = ueber Kimme/Korn zielen, Strg = ducken, Alt = schleichen.
    /// </summary>
    public sealed class KeyboardMouseInputSource : IPlayerInputSource
    {
        readonly float _sensitivity;
        readonly float _maxPitch;
        float _yaw;
        float _pitch;

        /// <summary>Wird beim Zielen heruntergesetzt (langsameres Umsehen ueber
        /// dem Visier / im Zielfernrohr). 1 = normal. Setzt der Charakter jeden Frame.</summary>
        public float SensitivityScale { get; set; } = 1f;

        public KeyboardMouseInputSource(float startYaw, float sensitivity = 0.1f, float maxPitch = 80f)
        {
            _yaw = startYaw;
            _sensitivity = sensitivity;
            _maxPitch = maxPitch;
        }

        public Vector2 Move
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return Vector2.zero;
                float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
                float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
                return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
            }
        }

        public float LookYaw
        {
            get
            {
                var m = Mouse.current;
                if (m != null)
                    _yaw += m.delta.ReadValue().x * _sensitivity * SensitivityScale;
                return _yaw;
            }
        }

        public float LookPitch
        {
            get
            {
                var m = Mouse.current;
                if (m != null)
                    _pitch = Mathf.Clamp(_pitch - m.delta.ReadValue().y * _sensitivity * SensitivityScale, -_maxPitch, _maxPitch);
                return _pitch;
            }
        }

        // ------------------------------------------------------------------
        //  Halten oder Umschalten
        //
        //  Zielen, Ducken und Sprinten dauerhaft gedrueckt zu halten ist fuer
        //  manche Haende schlicht schmerzhaft, und mit nur einer brauchbaren
        //  Hand geht es gar nicht. Wer will, schaltet stattdessen um. Die
        //  Tasten bleiben dieselben - nur die Bedeutung des Druckes aendert sich.
        // ------------------------------------------------------------------

        bool _zielUm;      // Zustand im Umschalt-Betrieb
        bool _duckUm;
        bool _sprintUm;

        /// <summary>
        /// Gemeinsame Regel: im Halte-Betrieb zaehlt der Druck, im
        /// Umschalt-Betrieb kippt jeder neue Druck den Zustand.
        /// </summary>
        static bool HaltenOderUmschalten(bool umschalten, bool gedrueckt, bool neuGedrueckt,
                                         ref bool zustand)
        {
            if (!umschalten)
            {
                zustand = false;      // sauber zuruecksetzen fuer den naechsten Wechsel
                return gedrueckt;
            }
            if (neuGedrueckt) zustand = !zustand;
            return zustand;
        }

        public bool Sprint
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return false;
                return HaltenOderUmschalten(GameSettings.ToggleSprint,
                    k.leftShiftKey.isPressed, k.leftShiftKey.wasPressedThisFrame, ref _sprintUm);
            }
        }

        public bool AimHeld
        {
            get
            {
                var m = Mouse.current;
                if (m == null) return false;
                return HaltenOderUmschalten(GameSettings.ToggleAim,
                    m.rightButton.isPressed, m.rightButton.wasPressedThisFrame, ref _zielUm);
            }
        }

        public bool CrouchHeld
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return false;
                bool gedrueckt = k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
                bool neu = k.leftCtrlKey.wasPressedThisFrame || k.rightCtrlKey.wasPressedThisFrame;
                return HaltenOderUmschalten(GameSettings.ToggleCrouch, gedrueckt, neu, ref _duckUm);
            }
        }

        /// <summary>Nur fuer Tests: die Umschalt-Zustaende zuruecksetzen.</summary>
        public void UmschaltZustandZuruecksetzenForTests()
        {
            _zielUm = _duckUm = _sprintUm = false;
        }

        public bool WalkHeld
        {
            get { var k = Keyboard.current; return k != null && (k.leftAltKey.isPressed || k.rightAltKey.isPressed); }
        }

        public bool JumpPressed
        {
            get { var k = Keyboard.current; return k != null && k.spaceKey.wasPressedThisFrame; }
        }

        public bool FireHeld
        {
            get { var m = Mouse.current; return m != null && m.leftButton.isPressed; }
        }

        public bool ReloadPressed
        {
            get { var k = Keyboard.current; return k != null && k.rKey.wasPressedThisFrame; }
        }

        public bool UseHeld
        {
            get { var k = Keyboard.current; return k != null && k.eKey.isPressed; }
        }

        public int SwitchToSlot
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return -1;
                if (k.digit1Key.wasPressedThisFrame) return 0;
                if (k.digit2Key.wasPressedThisFrame) return 1;
                return -1;
            }
        }

        public int UseAbilitySlot
        {
            get
            {
                var k = Keyboard.current;
                if (k == null) return -1;
                if (k.qKey.wasPressedThisFrame) return 0;
                if (k.fKey.wasPressedThisFrame) return 1;
                if (k.gKey.wasPressedThisFrame) return 2;
                return -1;
            }
        }
    }
}
