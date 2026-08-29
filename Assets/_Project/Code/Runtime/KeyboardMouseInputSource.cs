using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Echte Eingabequelle: liest Tastatur und Maus ueber das Input System.
    /// Phase 1 nutzt noch kein .inputactions-Asset, sondern direkte Geraeteabfragen.
    /// WASD = Bewegung, Umschalt = Sprint, Leertaste = Springen, Maus X = Drehen.
    /// </summary>
    public sealed class KeyboardMouseInputSource : IPlayerInputSource
    {
        readonly float _mouseSensitivity;
        float _yaw;

        public KeyboardMouseInputSource(float startYaw, float mouseSensitivity = 0.1f)
        {
            _yaw = startYaw;
            _mouseSensitivity = mouseSensitivity;
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
                    _yaw += m.delta.ReadValue().x * _mouseSensitivity;
                return _yaw;
            }
        }

        public bool Sprint
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k.leftShiftKey.isPressed;
            }
        }

        public bool JumpPressed
        {
            get
            {
                var k = Keyboard.current;
                return k != null && k.spaceKey.wasPressedThisFrame;
            }
        }
    }
}
