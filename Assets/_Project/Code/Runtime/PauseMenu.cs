using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Einfaches Pause-Overlay: Esc gibt die Maus frei und zeigt "Weiter" /
    /// "Spiel beenden". Die Simulation laeuft weiter (Netzwerkspiel -
    /// Time.timeScale = 0 wuerde den Host mit einfrieren), aber die lokale
    /// Eingabe des Spielers pausiert (siehe NetworkPlayerController).
    ///
    /// Platzhalter-IMGUI, wie das HUD. Ein richtiges Menue kommt in Phase 5.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public static bool IsPaused { get; private set; }

        GUIStyle _title;
        GUIStyle _button;

        void OnEnable() => SetPaused(false);
        void OnDisable() => IsPaused = false;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
                SetPaused(!IsPaused);
        }

        static void SetPaused(bool paused)
        {
            IsPaused = paused;
            Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused;
        }

        void OnGUI()
        {
            if (!IsPaused)
                return;

            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            }

            float w = 320f, h = 220f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.Box(box, GUIContent.none);
            GUI.Label(new Rect(box.x, box.y + 16, w, 44), "Pause", _title);

            if (GUI.Button(new Rect(box.x + 40, box.y + 80, w - 80, 44), "Weiter", _button))
                SetPaused(false);

            if (GUI.Button(new Rect(box.x + 40, box.y + 140, w - 80, 44), "Spiel beenden", _button))
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
                Application.Quit();
            }
        }
    }
}
