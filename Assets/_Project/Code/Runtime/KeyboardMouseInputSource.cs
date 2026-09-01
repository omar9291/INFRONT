using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Echte Eingabequelle: liest Tastatur und Maus ueber das Input System.
    /// Phase 2 nutzt weiterhin direkte Geraeteabfragen, noch kein .inputactions-Asset.
    /// WASD = Bewegung, Umschalt = Sprint, Leertaste = Springen,
    /// Maus X/Y = Zielen, linke Maustaste = Feuern, R = Nachladen.
    /// </summary>
    public sealed class KeyboardMouseInputSource : IPlayerInputSource
    {
        readonly float _sensitivity;
        readonly float _maxPitch;
        float _yaw;
        float _pitch;

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
                    _yaw += m.delta.ReadValue().x * _sensitivity;
                return _yaw;
            }
        }

        public float LookPitch
        {
            get
            {
                var m = Mouse.current;
                if (m != null)
                    _pitch = Mathf.Clamp(_pitch - m.delta.ReadValue().y * _sensitivity, -_maxPitch, _maxPitch);
                return _pitch;
            }
        }

        public bool Sprint
        {
            get { var k = Keyboard.current; return k != null && k.leftShiftKey.isPressed; }
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
