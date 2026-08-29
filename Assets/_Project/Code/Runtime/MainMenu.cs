using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Startmenue. Platzhalter-IMGUI wie das HUD. Waehlt Teamgroesse,
    /// Bot-Schwierigkeit und Maus-Empfindlichkeit; startet die Runde oder
    /// beendet das Spiel.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        GUIStyle _title, _label, _button;

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Styles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 20 };
        }

        void OnGUI()
        {
            Styles();

            float w = 460f;
            float x = (Screen.width - w) / 2f;
            float y = Screen.height * 0.16f;

            GUI.Label(new Rect(0, y, Screen.width, 60), "INFRONT", _title);
            y += 90f;

            // Teamgroesse
            GUI.Label(new Rect(x, y, w, 26), $"Teamgroesse: {GameSettings.TeamSize} gegen {GameSettings.TeamSize}", _label);
            y += 28f;
            GameSettings.TeamSize = Mathf.RoundToInt(GUI.HorizontalSlider(new Rect(x, y, w, 24), GameSettings.TeamSize, 2f, 5f));
            y += 40f;

            // Schwierigkeit
            GUI.Label(new Rect(x, y, w, 26), "Bot-Schwierigkeit", _label);
            y += 28f;
            string[] names = { "Leicht", "Normal", "Schwer" };
            int diff = (int)GameSettings.Difficulty;
            diff = GUI.Toolbar(new Rect(x, y, w, 34), diff, names);
            GameSettings.Difficulty = (GameSettings.Level)diff;
            y += 50f;

            // Maus-Empfindlichkeit
            GUI.Label(new Rect(x, y, w, 26), $"Maus-Empfindlichkeit: {GameSettings.MouseSensitivity:0.00}", _label);
            y += 28f;
            GameSettings.MouseSensitivity = GUI.HorizontalSlider(new Rect(x, y, w, 24), GameSettings.MouseSensitivity, 0.02f, 0.3f);
            y += 50f;

            if (GUI.Button(new Rect(x, y, w, 48), "Runde starten", _button))
            {
                GameSettings.Save();
                if (GameFlow.Instance != null) GameFlow.Instance.ToArena();
            }
            y += 58f;

            if (GUI.Button(new Rect(x, y, w, 44), "Beenden", _button))
            {
                GameSettings.Save();
                Application.Quit();
            }
        }
    }
}
