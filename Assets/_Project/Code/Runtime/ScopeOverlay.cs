using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Das schwarze Zielfernrohr-Bild ueber dem ganzen Schirm, wenn der Spieler
    /// mit einer Fernrohr-Waffe (Scharfschuetzengewehr) ueber die rechte
    /// Maustaste voll aufzieht. Nur beim Besitzer, reine Optik.
    ///
    /// Die runde Maske und das Fadenkreuz werden einmalig als Textur aus Code
    /// erzeugt - kein Bild-Asset noetig. Wie stark sie sichtbar ist, kommt aus
    /// <see cref="NetworkPlayerController.ScopeAmount01"/>.
    ///
    /// NICHT pruefbar: wie es aussieht.
    /// </summary>
    public sealed class ScopeOverlay : NetworkBehaviour
    {
        NetworkPlayerController _npc;
        Texture2D _scopeTex;
        Texture2D _black;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }
            _npc = GetComponent<NetworkPlayerController>();
        }

        public override void OnDestroy()
        {
            if (_scopeTex != null) Destroy(_scopeTex);
            if (_black != null) Destroy(_black);
            base.OnDestroy();
        }

        void EnsureTextures()
        {
            if (_scopeTex != null) return;

            const int n = 512;
            _scopeTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float c = (n - 1) * 0.5f;
            const float inner = 0.70f;   // klarer Blick
            const float edge = 0.76f;    // weicher Linsenrand

            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                float a;
                if (r >= edge) a = 1f;
                else if (r >= inner) a = Mathf.InverseLerp(inner, edge, r);
                else a = 0f;

                // Fadenkreuz im klaren Bereich
                if (a < 0.99f && r < inner)
                {
                    float ax = Mathf.Abs(x - c), ay = Mathf.Abs(y - c);
                    bool line = ax < 1.2f || ay < 1.2f;
                    bool gap = r < 0.045f;     // kleines Loch in der Mitte
                    bool dot = r < 0.012f;     // Zielpunkt
                    if ((line && !gap) || dot) a = Mathf.Max(a, 0.85f);
                }

                px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
            _scopeTex.SetPixels32(px);
            _scopeTex.Apply(false);

            _black = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _black.SetPixel(0, 0, Color.black);
            _black.Apply(false);
        }

        void OnGUI()
        {
            if (_npc == null || Event.current.type != EventType.Repaint) return;

            float amt = _npc.ScopeAmount01;
            if (amt <= 0.02f) return;

            EnsureTextures();

            float h = Screen.height;
            float w = Screen.width;
            float side = h;
            float x0 = (w - side) * 0.5f;

            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, amt);

            // Runde Maske + Fadenkreuz, quadratisch und so hoch wie der Schirm.
            GUI.DrawTexture(new Rect(x0, 0f, side, side), _scopeTex);

            // Reste links und rechts komplett schwarz.
            if (x0 > 0f)
            {
                GUI.DrawTexture(new Rect(0f, 0f, x0 + 1f, h), _black);
                GUI.DrawTexture(new Rect(w - x0 - 1f, 0f, x0 + 1f, h), _black);
            }

            GUI.color = prev;
        }
    }
}
