using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Infront.Tests
{
    public sealed class AudioContentTests
    {
        [Test]
        public void Jede_Klangkennung_hat_Dateien_mit_Credit_oder_einen_Synthesegrund()
        {
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                var entry = AudioCatalog.For(id);
                Assert.IsNotNull(entry, "Keine Herkunft für " + id);
                if (entry.kind == "synthetic")
                {
                    Assert.Greater(entry.reason?.Length ?? 0, 25, "Keine Begründung für " + id);
                    Assert.IsEmpty(entry.clips);
                    continue;
                }
                var credit = AssetCredits.All.SingleOrDefault(c => c.Id == entry.credit);
                Assert.IsNotNull(credit, "Credit fehlt für " + id);
                Assert.IsNotEmpty(entry.clips, "Leere Aufnahmeliste für " + id);
                foreach (string resource in entry.clips)
                {
                    var clip = Resources.Load<AudioClip>(resource);
                    Assert.IsNotNull(clip, "Datei fehlt statt Aufnahme: " + resource);
                    Assert.Greater(clip.samples, 100, resource);
                    Assert.Greater(clip.length, .02f, resource);
                    Assert.IsTrue(credit.Content.Any(f => f.Path.Contains("/Resources/" + resource + ".")),
                        "Audiodatei fehlt im Credit-Fingerabdruck: " + resource);
                    if (resource.StartsWith("InfrontAudio/") && entry.kind != "music")
                    {
                        Assert.AreEqual(1, clip.channels, "Raeumliche Effekte muessen Mono bleiben: " + resource);
                        var samples = new float[clip.samples];
                        Assert.IsTrue(clip.GetData(samples, 0), "Kurze Effekte muessen ohne Nachladen bereit sein: " + resource);
                        float peak = samples.Max(v => Mathf.Abs(v));
                        Assert.That(peak, Is.InRange(.05f, .85f),
                            "Import hat den vorbereiteten Pegel veraendert oder den Clip verstummen lassen: " + resource);
                    }
                }
            }
        }

        [Test]
        public void Lizenzpflichtige_Schritte_behalten_Lizenzlink_und_Bearbeitungsvermerk()
        {
            foreach (string id in new[] { "footsteps-boots", "footsteps-metal" })
            {
                var credit = AssetCredits.All.Single(c => c.Id == id);
                Assert.AreEqual("https://creativecommons.org/licenses/by/3.0/", credit.LicenseUrl);
                Assert.IsNotEmpty(credit.AttributionNote);
                Assert.IsTrue(credit.Evidence.Any(e => e.Kind == "package-license"));
            }
        }

        [Test]
        public void Raue_Kernklaenge_stammen_aus_Aufnahmen()
        {
            foreach (SoundId id in new[] { SoundId.WaffeWechsel, SoundId.BombeExplosion, SoundId.MetallKnarzen })
            {
                var entry = AudioCatalog.For(id);
                Assert.AreEqual("recording", entry.kind, id.ToString());
                Assert.IsNotEmpty(entry.clips, id.ToString());
                Assert.IsNotEmpty(entry.credit, id.ToString());
            }
        }
    }
}
