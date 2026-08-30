using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Bestimmt in der Arena den Mauszeiger-Zustand: gefangen nur im laufenden
    /// Spiel, frei in Pause und beim Rundenende. Eine Stelle statt vieler.
    /// </summary>
    public sealed class CursorController : MonoBehaviour
    {
        void LateUpdate()
        {
            bool playing =
                MatchManager.Instance != null
                && MatchManager.Instance.CurrentPhase == MatchManager.Phase.Playing
                && !PauseMenu.IsPaused
                && !BuyMenuHud.IsOpen;

            var wanted = playing ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != wanted)
            {
                Cursor.lockState = wanted;
                Cursor.visible = !playing;
            }
        }
    }
}
