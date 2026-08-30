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
                    $"ALPHA  {a}  :  {b}  BRAVO      Runde bis {match.RoundsToWin}      {seconds / 60}:{seconds % 60:00}",
                    Center(_big));

                if (match.CurrentPhase == MatchManager.Phase.RoundOver)
                {
                    string text;
                    if (match.MatchWinner != Team.None)
                        text = Team.Name(match.MatchWinner) + " GEWINNT DAS MATCH!";
                    else if (match.RoundWinner == Team.None)
                        text = "Runde unentschieden";
                    else
                        text = Team.Name(match.RoundWinner) + " gewinnt die Runde";
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

            var local = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null
                ? NetworkManager.Singleton.LocalClient.PlayerObject
                : null;
            if (local != null)
            {
                var health = local.GetComponent<Health>();
                var weapon = local.GetComponent<NetworkWeapon>();

                if (health != null)
                {
                    float f = health.Max > 0 ? (float)health.Current / health.Max : 0f;
                    var bar = new Rect(16, Screen.height - 44, 240, 20);

                    GUI.color = new Color(0f, 0f, 0f, 0.5f);
                    GUI.DrawTexture(bar, Texture2D.whiteTexture);

                    // gruen -> gelb -> rot
                    Color fill = f > 0.5f
                        ? Color.Lerp(new Color(1f, 0.85f, 0.1f), new Color(0.3f, 0.85f, 0.2f), (f - 0.5f) * 2f)
                        : Color.Lerp(new Color(0.85f, 0.15f, 0.1f), new Color(1f, 0.85f, 0.1f), f * 2f);
                    GUI.color = fill;
                    GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, (bar.width - 4) * f, bar.height - 4), Texture2D.whiteTexture);

                    GUI.color = Color.white;
                    GUI.Label(new Rect(bar.xMax + 10, bar.y - 2, 200, 24), $"{health.Current}/{health.Max}", _mid);
                }

                if (weapon != null)
                {
                    string ammo = $"Munition {weapon.Ammo}/{weapon.MagazineSize}" + (weapon.IsReloading ? "  (nachladen...)" : "");
                    GUI.Label(new Rect(16, Screen.height - 22, Screen.width, 22), ammo, _mid);
                }
            }
        }

        static GUIStyle Center(GUIStyle s)
        {
            s.alignment = TextAnchor.UpperCenter;
            return s;
        }
    }
}
