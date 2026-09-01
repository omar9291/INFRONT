using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Einfaches Pause-Overlay: Esc gibt die Maus frei und zeigt "Weiter" /
    /// "Spiel beenden".
    ///
    /// Spielt man allein (nur Host, keine weiteren Spieler), ist es eine ECHTE
    /// Pause: Time.timeScale = 0 haelt Bewegung und Bots an, die Bots werden
    /// zusaetzlich stillgelegt, und der MatchManager schiebt beim Fortsetzen
    /// alle Rundenuhren um die Pausenzeit nach hinten - es dreht sich also
    /// nichts weg, waehrend man im Menue steht.
    ///
    /// Sobald ein zweiter Spieler verbunden ist, wird NICHT die Zeit angehalten
    /// (das wuerde den anderen mit einfrieren) - dann pausiert nur die eigene
    /// Eingabe (siehe NetworkPlayerController).
    ///
    /// Das Pause-Overlay selbst zeichnet der <see cref="HudController"/>
    /// (UI Toolkit). Diese Klasse haelt nur den Zustand und die Solo-Pause-Logik.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        // Laeuft gerade die echte Solo-Pause (Zeitlupe/Bots/Uhren angehalten)?
        // Getrennt von IsPaused, damit der Start-Reset (OnEnable) nichts anfasst.
        static bool _soloEffectsActive;

        void OnEnable() => SetPaused(false);

        void OnDisable()
        {
            // Beim Abbau nie in Zeitlupe stehen bleiben.
            if (IsPaused) SetPaused(false);
            IsPaused = false;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetPaused(!IsPaused);
        }

        /// <summary>Vor jedem Szenenwechsel: sicher aus der Pause zurueck, damit
        /// nichts in Zeitlupe in den Ladebildschirm laeuft.</summary>
        public static void ForceResume()
        {
            SetPaused(false);
            IsPaused = false;
        }

        /// <summary>Vom HUD-Knopf "Weiter": Pause von aussen umschalten.</summary>
        public static void SetPausedExternally(bool paused) => SetPaused(paused);

        static void SetPaused(bool paused)
        {
            IsPaused = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;

            // Echte Pause nur solo: Host laeuft, kein zweiter Spieler verbunden.
            var nm = NetworkManager.Singleton;
            bool solo = nm != null && nm.IsListening && nm.ConnectedClients.Count <= 1;

            if (paused && solo && !_soloEffectsActive)
            {
                _soloEffectsActive = true;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                BotBrain.GloballyFrozen = true;
                MatchManager.Instance?.ServerBeginSoloPause();
            }
            else if (!paused && _soloEffectsActive)
            {
                _soloEffectsActive = false;
                Time.timeScale = 1f;
                AudioListener.pause = false;
                BotBrain.GloballyFrozen = false;
                MatchManager.Instance?.ServerEndSoloPause();
            }
        }

    }
}
