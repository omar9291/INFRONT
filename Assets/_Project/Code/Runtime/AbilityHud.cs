using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Zwei Bild-Effekte rund um Faehigkeiten, die ueber allem liegen muessen
    /// und darum als IMGUI direkt gezeichnet werden:
    ///  - der weisse Blitz-Bildschirm bei einer Blendgranate
    ///  - die gelben Kaesten um vom Scan-Puls aufgeklaerte Gegner (durch Waende)
    ///
    /// Die Faehigkeiten-Leiste unten (Q/F/G) zeichnet der
    /// <see cref="HudController"/>. Nur beim Besitzer.
    /// </summary>
    public sealed class AbilityHud : NetworkBehaviour
    {
        AbilityHolder _holder;
        TeamMember _team;
        float _blindT;          // Restdauer der Blendung
        float _blindTotal;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _holder = GetComponent<AbilityHolder>();
            _team = GetComponent<TeamMember>();
            if (_holder != null) _holder.OwnerBlinded += OnBlinded;
        }

        public override void OnNetworkDespawn()
        {
            if (_holder != null) _holder.OwnerBlinded -= OnBlinded;
        }

        void OnBlinded(float seconds)
        {
            _blindT = Mathf.Max(_blindT, seconds);
            _blindTotal = Mathf.Max(_blindTotal, seconds);
        }

        void Update()
        {
            if (_blindT > 0f) _blindT = Mathf.Max(0f, _blindT - Time.deltaTime);
        }

        void OnGUI()
        {
            // --- Blitz-Bildschirm ---
            if (_blindT > 0f && _blindTotal > 0f)
            {
                float a = Mathf.Clamp01(_blindT / _blindTotal);
                float alpha = a > 0.6f ? 1f : a / 0.6f;
                Color prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            if (_holder == null) return;

            var mm = MatchManager.Instance;
            if (mm == null || mm.CurrentPhase != MatchManager.Phase.Playing) return;

            DrawScanMarks();
        }

        /// <summary>Vom Scan-Puls aufgeklaerte Gegner als Kasten einzeichnen -
        /// auch durch Waende.</summary>
        void DrawScanMarks()
        {
            var cam = Camera.main;
            if (cam == null || _team == null) return;
            int myTeam = _team.TeamId;
            Color prev = GUI.color;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                if (!ScanRegistry.IsRevealedTo(m, myTeam)) continue;

                Vector3 sp = cam.WorldToScreenPoint(m.transform.position + Vector3.up * 1f);
                if (sp.z <= 0f) continue;
                float x = sp.x, y = Screen.height - sp.y;
                var r = new Rect(x - 16f, y - 30f, 32f, 60f);

                GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x, r.yMax - 2f, r.width, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(r.xMax - 2f, r.y, 2f, r.height), Texture2D.whiteTexture);
            }
            GUI.color = prev;
        }
    }
}
