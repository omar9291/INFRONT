using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Infront.Tests
{
    /// <summary>
    /// Die Zusatzstufen des Renderers.
    ///
    /// Der Zustand davor: <c>m_RendererFeatures</c> im Renderer-Asset war eine
    /// LEERE Liste - es gab ueberhaupt keine Umgebungsverdeckung. Deshalb fehlte
    /// die Abdunklung in Ecken und dort, wo etwas den Boden beruehrt, und Kisten
    /// sahen aus, als schwebten sie ueber dem Beton.
    ///
    /// NICHT pruefbar: ob die Verdeckung gut aussieht. Pruefbar: dass sie
    /// ueberhaupt eingehaengt ist - und zwar richtig. Unity nimmt eine Stufe
    /// stillschweigend nicht an, wenn Liste und Id-Tabelle unterschiedlich lang
    /// sind; das ist der uebliche Weg, wie so ein Skript scheitert.
    /// </summary>
    public sealed class GrafikstufenTests
    {
        [Test]
        public void Umgebungsverdeckung_ist_eingehaengt()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                      as UniversalRenderPipelineAsset;
            Assert.IsNotNull(urp, "Es laeuft keine URP-Pipeline.");

            // rendererDataList ist intern - ueber Reflexion herankommen, damit
            // der Test nicht an einer Unity-Version haengt.
            var feld = typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList",
                          System.Reflection.BindingFlags.NonPublic
                          | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(feld, "m_RendererDataList nicht gefunden - Unity-Version geaendert?");

            var datenListe = feld.GetValue(urp) as ScriptableRendererData[];
            Assert.IsNotNull(datenListe);
            Assert.Greater(datenListe.Length, 0, "Kein Renderer im URP-Asset.");

            var alle = datenListe.Where(d => d != null)
                                 .SelectMany(d => d.rendererFeatures)
                                 .Where(f => f != null)
                                 .ToArray();

            Assert.IsTrue(alle.Any(f => f is ScreenSpaceAmbientOcclusion),
                "Keine Umgebungsverdeckung (SSAO) im Renderer. Ohne sie fehlt die "
                + "Abdunklung in Ecken und am Boden - Gegenstaende sehen aus, als "
                + "schwebten sie. Gefundene Stufen: "
                + (alle.Length == 0 ? "keine"
                                    : string.Join(", ", alle.Select(f => f.GetType().Name))));
        }

        [Test]
        public void Die_Verdeckung_ist_auch_wirklich_aktiv()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                      as UniversalRenderPipelineAsset;
            Assert.IsNotNull(urp);

            var feld = typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList",
                          System.Reflection.BindingFlags.NonPublic
                          | System.Reflection.BindingFlags.Instance);
            var datenListe = feld.GetValue(urp) as ScriptableRendererData[];

            var ssao = datenListe.Where(d => d != null)
                                 .SelectMany(d => d.rendererFeatures)
                                 .OfType<ScreenSpaceAmbientOcclusion>()
                                 .FirstOrDefault();
            Assert.IsNotNull(ssao, "Keine SSAO-Stufe gefunden.");
            Assert.IsTrue(ssao.isActive,
                "Die SSAO-Stufe haengt drin, ist aber abgeschaltet - sie tut dann nichts.");
        }
    }
}
