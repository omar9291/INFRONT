using UnityEngine;

namespace Infront.Tests
{
    /// <summary>
    /// Gefaelschte Eingabequelle fuer PlayMode-Tests. Alle Werte frei setzbar.
    /// </summary>
    public sealed class FakePlayerInput : IPlayerInputSource
    {
        public Vector2 Move { get; set; }
        public float LookYaw { get; set; }
        public float LookPitch { get; set; }
        public bool Sprint { get; set; }
        public bool AimHeld { get; set; }
        public bool CrouchHeld { get; set; }
        public bool WalkHeld { get; set; }
        public bool FireHeld { get; set; }
        public bool UseHeld { get; set; }

        bool _jumpQueued;
        bool _reloadQueued;

        public bool JumpPressed
        {
            get { if (!_jumpQueued) return false; _jumpQueued = false; return true; }
        }

        public bool ReloadPressed
        {
            get { if (!_reloadQueued) return false; _reloadQueued = false; return true; }
        }

        public int SwitchToSlot { get; set; } = -1;
        public int UseAbilitySlot { get; set; } = -1;

        public void QueueJump() => _jumpQueued = true;
        public void QueueReload() => _reloadQueued = true;
    }
}
