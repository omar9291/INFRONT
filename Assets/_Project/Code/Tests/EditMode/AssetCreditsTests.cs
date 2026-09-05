using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Infront.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace Infront.Tests
{
    public sealed class AssetCreditsTests
    {
        [Test]
        public void Jedes_Asset_und_jeder_Nachweis_ist_im_Katalog()
        {
            var manifest = Resources.Load<TextAsset>(AssetCredits.ResourceName);
            Assert.IsNotNull(manifest, "Der Katalog muss im fertigen Spiel enthalten sein.");
            var entries = AssetCredits.Parse(manifest.text);
            Assert.Greater(entries.Count, 20, "Ein Anbietername ersetzt keine einzelnen Asset-Eintraege.");
            var errors = AssetCreditsAudit.Validate(Directory.GetCurrentDirectory(), entries);
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Credits_Dokument_spiegelt_die_Attribution_im_Spiel()
        {
            string document = File.ReadAllText("CREDITS.md");
            foreach (var entry in AssetCredits.All)
            {
                Assert.That(document, Does.Contain("<!-- credit:" + entry.Id + " -->"));
                // Den jeweiligen Abschnitt pruefen, damit ein anderer Eintrag
                // mit demselben Anbieternamen einen fehlenden nicht verdeckt.
                string section = document.Split(new[] { "<!-- credit:" + entry.Id + " -->" },
                    StringSplitOptions.None)[1].Split(new[] { "<!-- credit:" }, StringSplitOptions.None)[0];
                Assert.That(section, Does.Contain(entry.Name));
                Assert.That(section, Does.Contain(entry.Author));
                Assert.That(section, Does.Contain(entry.License));
                Assert.That(section, Does.Contain(entry.SourceUrl));
                Assert.That(section, Does.Contain(entry.ProvenanceStatus));
            }
        }

        [Test]
        public void Neues_Paket_und_unbemerkt_ersetzte_Datei_werden_erkannt()
        {
            string root = Path.Combine(Path.GetTempPath(), "infront-credits-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "Assets/Imported/known"));
            try
            {
                string file = "Assets/Imported/known/model.bin";
                File.WriteAllText(Path.Combine(root, file), "original asset");
                File.WriteAllText(Path.Combine(root, "LICENSE.txt"), "retained test license");
                string json = "{\"schemaVersion\":1,\"entries\":[{\"id\":\"test\",\"name\":\"Test\","
                    + "\"author\":\"Test author\",\"license\":\"Test license\",\"sourceUrl\":\"https://example.com/asset\","
                    + "\"provenanceStatus\":\"test-fixture\",\"paths\":[\"Assets/Imported/known\"],"
                    + "\"evidence\":[{\"path\":\"LICENSE.txt\",\"kind\":\"package-license\","
                    + "\"sourceUrl\":\"https://example.com/asset\",\"sha256\":\"" + Hash(Path.Combine(root, "LICENSE.txt")) + "\"}],"
                    + "\"content\":[{\"path\":\"" + file + "\",\"sha256\":\"" + Hash(Path.Combine(root, file)) + "\"}]}]}";
                var entries = AssetCredits.Parse(json);
                string[] roots = { "Assets/Imported" };
                Assert.IsEmpty(AssetCreditsAudit.Validate(root, entries, roots));

                Directory.CreateDirectory(Path.Combine(root, "Assets/Imported/uncredited"));
                File.WriteAllText(Path.Combine(root, "Assets/Imported/known/extra.bin"), "new asset");
                File.WriteAllText(Path.Combine(root, file), "different asset with same filename");
                var errors = AssetCreditsAudit.Validate(root, entries, roots);
                Assert.IsTrue(errors.Any(e => e.Contains("Uncredited asset folder") && e.Contains("uncredited")));
                Assert.IsTrue(errors.Any(e => e.Contains("Uncredited asset file") && e.Contains("extra.bin")));
                Assert.IsTrue(errors.Any(e => e.Contains("Changed credited content") && e.Contains("model.bin")));
            }
            finally { Directory.Delete(root, true); }
        }

        [Test]
        public void Doppelte_Ids_werden_nicht_stillschweigend_akzeptiert()
        {
            Assert.Throws<FormatException>(() => AssetCredits.Parse(
                "{\"schemaVersion\":1,\"entries\":[{\"id\":\"same\"},{\"id\":\"same\"}]}"));
        }

        static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
    }
}
