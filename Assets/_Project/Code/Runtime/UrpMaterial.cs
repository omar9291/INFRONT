using UnityEngine;
using UnityEngine.Rendering;

namespace Infront
{
    /// <summary>
    /// Stellt ein zur Laufzeit gebautes URP-Material auf Durchsichtigkeit um.
    ///
    /// Die Falle dahinter (gefunden 2026-09-04): Bei den URP-Shadern reicht es
    /// NICHT, die Eigenschaften _Surface, _Blend, _SrcBlend, _DstBlend und
    /// _ZWrite zu setzen. Der Fragment-Teil des Shaders fragt ein Schluesselwort
    /// ab - ist _SURFACE_TYPE_TRANSPARENT nicht gesetzt, schreibt er die
    /// Deckkraft fest auf 1. Das Material mischt dann zwar hardwareseitig,
    /// bekommt aber ueberall Deckkraft 1 geliefert und ist damit undurchsichtig.
    ///
    /// Sichtbar wurde das erst, als die URP-Shader wirklich im Build lagen
    /// (siehe GraphicsTune.EnsureShaders). Vorher fiel alles still auf
    /// "Sprites/Default" zurueck, und der ist von Haus aus durchsichtig - der
    /// Fehler war also die ganze Zeit da und von einem zweiten Fehler verdeckt.
    /// Im Bild sah man dann statt einer Rauchwolke grosse harte Vielecke.
    ///
    /// Deshalb steht das hier an EINER Stelle und nicht acht Mal einzeln.
    /// NICHT pruefbar: wie es aussieht. Pruefbar: die Schluesselwoerter.
    /// </summary>
    public static class UrpMaterial
    {
        /// <summary>Alpha-Mischung: die Flaeche deckt ab (Rauch, Nebel, Staub).</summary>
        public static void Durchsichtig(Material m) => Stelle(m, additiv: false);

        /// <summary>Additiv: die Flaeche ist Licht (Leuchtspur, Muendungsfeuer,
        /// Funken). Vor heller Wand kaum sichtbar, im Dunkeln hell.</summary>
        public static void Leuchtend(Material m) => Stelle(m, additiv: true);

        /// <summary>
        /// Baut ein Effekt-Material aus der gespeicherten Vorlage.
        ///
        /// Nur so ist die durchsichtige Spielart des Shaders wirklich im Build:
        /// Unity uebersetzt eine Spielart nur, wenn ein gespeichertes Material
        /// sie benutzt. Ein zur Laufzeit zusammengesetztes Material zaehlt dabei
        /// nicht - deshalb sah man vorher harte Vielecke statt Rauch.
        /// Die Vorlagen legt GraphicsTune.EnsureFxMaterials an.
        /// </summary>
        public static Material NeuFx(bool additiv, string name)
        {
            var vorlage = Resources.Load<Material>(additiv ? "Materials/fx_additiv"
                                                           : "Materials/fx_alpha");
            Material m;
            if (vorlage != null)
            {
                m = new Material(vorlage) { name = name };
            }
            else
            {
                // Rueckfallebene wie bisher, falls die Vorlage fehlt.
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                m = new Material(sh) { name = name };
            }
            Stelle(m, additiv);
            return m;
        }

        /// <summary>Nur zum Pruefen: gibt es die gespeicherten Vorlagen?</summary>
        public static bool VorlagenDaForTests
            => Resources.Load<Material>("Materials/fx_alpha") != null
               && Resources.Load<Material>("Materials/fx_additiv") != null;

        static void Stelle(Material m, bool additiv)
        {
            if (m == null) return;

            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);        // 1 = transparent
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additiv ? 1f : 0f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", (float)CullMode.Off);

            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend"))
                m.SetFloat("_DstBlend", additiv ? (float)BlendMode.One
                                                : (float)BlendMode.OneMinusSrcAlpha);

            // Ohne dieses Schluesselwort bleibt alles undurchsichtig.
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            // Neuere URP-Fassungen kennen zusaetzlich diese Mischart-Schalter.
            m.DisableKeyword("_BLENDMODE_ALPHA");
            m.DisableKeyword("_BLENDMODE_ADD");
            m.DisableKeyword("_BLENDMODE_PREMULTIPLY");
            m.DisableKeyword("_BLENDMODE_MULTIPLY");
            m.EnableKeyword(additiv ? "_BLENDMODE_ADD" : "_BLENDMODE_ALPHA");

            m.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Nur zum Pruefen: ist das Material wirklich auf durchsichtig
        /// gestellt? Genau dieser Schalter hat gefehlt.</summary>
        public static bool IstDurchsichtigForTests(Material m)
            => m != null && m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
               && m.renderQueue >= (int)RenderQueue.Transparent;
    }
}
