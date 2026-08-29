using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// PLATZHALTER-HUD. Reiner IMGUI-Text, keine Grafik, keine Schriftart-Assets.
    /// Zeigt Leben, Munition, Punktestand und Restzeit, damit man spielen und
    /// testen kann. Das richtige HUD kommt spaeter (Spaeter-Stufe 4).
    /// </summary>
    public sealed class MatchHud : MonoBehaviour
    {
        GUIStyle _big;
        GUIStyle _mid;

        void EnsureStyles()
        {
            if (_big != null) return;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _mid = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        }

        void OnGUI()
        {
            // Kein Repaint-Guard mehr: die Rundenende-Knoepfe brauchen alle
            // Ereignisse (Layout/MouseUp), sonst reagieren sie nicht.
            EnsureStyles();

            var match = MatchManager.Instance;
            if (match != null)
            {
                int a = match.GetScore(Team.Alpha);
                int b = match.GetScore(Team.Bravo);
                int seconds = Mathf.CeilToInt((float)match.SecondsRemaining);
                GUI.Label(new Rect(0, 8, Screen.width, 34),
                    $"ALPHA  {a}   -   {b}  BRAVO      {seconds / 60}:{seconds % 60:00}",
                    Center(_big));

                if (match.CurrentPhase == MatchManager.Phase.RoundOver)
                {
                    string text = match.Winner == Team.None ? "Unentschieden" : Team.Name(match.Winner) + " gewinnt!";
                    GUI.Label(new Rect(0, Screen.height / 2 - 90, Screen.width, 60), text, Center(_big));

                    float bw = 260f;
                    float bx = (Screen.width - bw) / 2f;
                    if (GUI.Button(new Rect(bx, Screen.height / 2f, bw, 42), "Sofort weiter")
                        && match.IsServer)
                        match.ServerStartNextRoundNow();
                    if (GUI.Button(new Rect(bx, Screen.height / 2f + 52f, bw, 42), "Zurueck zum Menue")
                        && GameFlow.Instance != null)
                        GameFlow.Instance.ToMenu();
                }
            }

            // Fadenkreuz (nicht im Pausemenue)
            if (!PauseMenu.IsPaused)
            {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            var cross = new Color(1f, 1f, 1f, 0.75f);
            GUI.color = cross;
            GUI.DrawTexture(new Rect(cx - 1f, cy - 8f, 2f, 16f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 8f, cy - 1f, 16f, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            }

            var local = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject
                : null;
            if (local != null)
            {
                var health = local.GetComponent<Health>();
                var weapon = local.GetComponent<NetworkWeapon>();
                string line = "";
                if (health != null) line += $"Leben {health.Current}/{health.Max}";
                if (weapon != null) line += $"    Munition {weapon.Ammo}/{weapon.MagazineSize}" + (weapon.IsReloading ? " (nachladen...)" : "");
                GUI.Label(new Rect(16, Screen.height - 40, Screen.width, 30), line, _mid);
            }
        }

        static GUIStyle Center(GUIStyle s)
        {
            s.alignment = TextAnchor.UpperCenter;
            return s;
        }
    }
}
