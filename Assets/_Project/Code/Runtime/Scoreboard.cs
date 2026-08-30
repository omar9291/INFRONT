using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Infront
{
    /// <summary>
    /// Punktetabelle auf Tab: beide Teams, Name, Abschuesse, Tode, lebt/tot.
    /// Zaehlt ueber das ganze Match.
    /// </summary>
    public sealed class Scoreboard : MonoBehaviour
    {
        readonly List<TeamMember> _buf = new();
        GUIStyle _head, _row;

        void Styles()
        {
            if (_head != null) return;
            _head = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _row = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleLeft };
        }

        void OnGUI()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.tabKey.isPressed) return;

            Styles();
            float w = 640f, h = 380f;
            var box = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var mm = MatchManager.Instance;
            string title = mm != null
                ? $"ALPHA {mm.GetScore(Team.Alpha)}  :  {mm.GetScore(Team.Bravo)} BRAVO   (Runde bis {mm.RoundsToWin})"
                : "Punktetabelle";
            GUI.Label(new Rect(box.x + 20, box.y + 10, w - 40, 30), title, _head);

            DrawTeam(Team.Alpha, new Rect(box.x + 20, box.y + 50, w / 2 - 30, h - 70));
            DrawTeam(Team.Bravo, new Rect(box.x + w / 2 + 10, box.y + 50, w / 2 - 30, h - 70));
        }

        void DrawTeam(int team, Rect area)
        {
            GUI.color = team == Team.Alpha ? new Color(0.5f, 0.7f, 1f) : new Color(1f, 0.55f, 0.5f);
            GUI.Label(new Rect(area.x, area.y, area.width, 24), $"{(team == Team.Alpha ? "ALPHA" : "BRAVO")}      K   T", _head);
            GUI.color = Color.white;

            _buf.Clear();
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == team) _buf.Add(m);
            _buf.Sort((a, b) => b.Kills.CompareTo(a.Kills));

            float y = area.y + 30;
            foreach (var m in _buf)
            {
                bool alive = m.Health != null && m.Health.IsAlive;
                GUI.color = alive ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                string tag = alive ? "" : "  (tot)";
                GUI.Label(new Rect(area.x, y, area.width, 22),
                    $"{m.DisplayName}{tag}".PadRight(18) + $"   {m.Kills}   {m.Deaths}", _row);
                y += 24;
            }
            GUI.color = Color.white;
        }
    }
}
