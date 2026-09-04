using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Eine weiche runde Wolke als Textur, im Code erzeugt.
    ///
    /// Warum es das gibt: Nebel und Staub liefen mit einem Material ganz ohne
    /// Textur. Ein Partikel ist dann ein blankes Viereck aus zwei Dreiecken -
    /// und weil die Deckkraft ueber die Flaeche verlaeuft, sieht man beide
    /// Kanten und die Diagonale dazwischen. Auf den Bildern aus dem Werk lagen
    /// deshalb ueberall grosse durchscheinende Dreiecke. Das war der
    /// "Pappkarton-Nebel".
    ///
    /// Mit einer runden Alpha-Verteilung verschwindet die Kante: aussen 0,
    /// innen 1, dazwischen weich. Eine einzige Textur reicht fuer alle
    /// Partikelsysteme, deshalb wird sie gemerkt.
    /// </summary>
    public static class SoftParticleTexture
    {
        static Texture2D _weich;

        /// <summary>Weiche runde Wolke. Wird einmal erzeugt und wiederverwendet.</summary>
        public static Texture2D Weich(int groesse = 64)
        {
            if (_weich != null) return _weich;

            var t = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false)
            {
                name = "SoftParticle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var px = new Color[groesse * groesse];
            float mitte = (groesse - 1) * 0.5f;
            for (int y = 0; y < groesse; y++)
            {
                for (int x = 0; x < groesse; x++)
                {
                    float dx = (x - mitte) / mitte;
                    float dy = (y - mitte) / mitte;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    // 1 in der Mitte, 0 am Rand des eingeschriebenen Kreises.
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // weicher Verlauf statt Kegel
                    a *= a;                      // aussen noch weiter ausduennen

                    px[y * groesse + x] = new Color(1f, 1f, 1f, a);
                }
            }

            t.SetPixels(px);
            t.Apply(false, false);
            _weich = t;
            return _weich;
        }

        /// <summary>
        /// Die weiche Wolke in ein Partikel-Material haengen. Deckt beide
        /// Namen ab: URP nennt sie _BaseMap, aeltere Shader mainTexture.
        /// </summary>
        public static void Anwenden(Material mat)
        {
            if (mat == null) return;
            var tex = Weich();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
        }

        /// <summary>Nur fuer Tests: die gemerkte Textur vergessen.</summary>
        public static void ForgetForTests()
        {
            if (_weich != null) Object.DestroyImmediate(_weich);
            _weich = null;
        }
    }
}
