using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Infront.Tests
{
    /// <summary>
    /// Sprachvertrag fuer den echten UI-Baum und die serialisierten Anzeigenamen.
    /// Die Wortliste faengt bekannte deutsche UI-Begriffe ab, keine beliebige
    /// Sprache automatisch. Namen, Datei-IDs und Inspector-Texte sind kein UI.
    /// Darum werden nur TextElement.text und echte DisplayName-Felder geprueft.
    /// </summary>
    public sealed class LocalizationTests
    {
        static readonly Regex GermanUiWords = new Regex(
            @"\b(spielen|einstellungen|steuerung|steuern|zug[aä]nglichkeit|zugaenglichkeit|"
            + @"laufbahn|quellen|beenden|zur[uü]ck|zurueck|weiter|bereit|kaufen|gekauft|"
            + @"kaufzeit|kaufmen[uü]|kaufmenue|nachladen|laden|ladebildschirm|"
            + @"schwierigkeit|botst[aä]rke|botstaerke|teamgr[oö][sß]se|teamgroesse|"
            + @"leben|weste|waffen|ausr[uü]stung|ausruestung|f[aä]higkeiten|faehigkeiten|"
            + @"sturmgewehr|maschinenpistole|scharfsch[uü]tzengewehr|scharfschuetzengewehr|"
            + @"rauchwand|blendgranate|splittergranate|brandwand|stolperdraht|verbandspaket|"
            + @"angriff|verteidigung|bombe|gelegt|entsch[aä]rft|entschaerft|"
            + @"get[oö]tet|getoetet|abschuss|kopftreffer|gewonnen|verloren|rundensieg|"
            + @"runde|runden|halbzeit|zeit|sekunden|feind|gegner|verb[uü]ndete|verbuendete|"
            + @"lautst[aä]rke|lautstaerke|empfindlichkeit|vollbild|fenster|schlicht|"
            + @"farbblindheit|bewegung|fadenkreuz|halten|umschalten|abbrechen|best[aä]tigen|"
            + @"bestaetigen|l[oö]schen|loeschen|absturzbericht|erstlauf|benutzername)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        GameSettings.Mode _mode;
        int _teamSize;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            _mode = GameSettings.GameMode;
            _teamSize = GameSettings.TeamSize;
            PauseMenu.ForceResume();
            Meldungen.ForgetForTests();
            yield return MatchTestHarness.Teardown();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PauseMenu.ForceResume();
            if (LoadingOverlay.Instance != null) LoadingOverlay.Instance.ForceHideForTests();
            Meldungen.ForgetForTests();
            yield return MatchTestHarness.Teardown();
            GameSettings.GameMode = _mode;
            GameSettings.TeamSize = _teamSize;
        }

        static IEnumerable<TextElement> Texts(VisualElement root)
        {
            if (root is TextElement text) yield return text;
            foreach (var child in root.Children())
                foreach (var nested in Texts(child)) yield return nested;
        }

        static void AssertEnglish(string text, string context)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            var german = GermanUiWords.Match(text);
            Assert.IsFalse(german.Success,
                $"Deutscher Anzeigetext in {context}: '{text}' (Begriff '{german.Value}').");
        }

        static void AssertEnglishTree(VisualElement root, string context)
        {
            Assert.IsNotNull(root, $"Kein UI-Baum fuer {context}.");
            var texts = Texts(root).Where(t => !string.IsNullOrWhiteSpace(t.text)).ToList();
            Assert.IsNotEmpty(texts, $"Leerer UI-Baum ist kein Sprachnachweis: {context}.");
            foreach (var text in texts) AssertEnglish(text.text, context + "/" + text.name);
        }

        static void AssertContains(VisualElement root, string expected)
        {
            Assert.IsTrue(Texts(root).Any(t => t.text != null && t.text.Contains(expected)),
                $"Erwarteter englischer Text '{expected}' fehlt im aufgebauten UI.");
        }

        [Test]
        public void Sprachpruefung_findet_deutsche_Labels_auch_tief_im_Baum()
        {
            var root = new VisualElement();
            var nested = new VisualElement { name = "zugaenglichkeit" };
            root.Add(nested);
            nested.Add(new Label("If you die, you stay dead for the round."));
            Assert.DoesNotThrow(() => AssertEnglishTree(root, "Probe"),
                "Das englische Verb 'die' und deutsche Element-IDs sind erlaubt.");

            var bad = new Button { text = "ZURÜCK" };
            nested.Add(bad);
            Assert.Throws<AssertionException>(() => AssertEnglishTree(root, "Probe"));
            bad.text = "KAUFZEIT 10";
            Assert.Throws<AssertionException>(() => AssertEnglishTree(root, "Probe"));
        }

        [Test]
        public void Sprachkatalog_und_Erstlauftexte_bleiben_englisch()
        {
            Assert.AreEqual("en", GameText.LanguageCode);
            var entries = typeof(GameText).GetNestedTypes(BindingFlags.Public)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)).ToArray();
            Assert.Greater(entries.Length, 200, "Der zentrale Sprachkatalog ist leer oder unvollstaendig.");
            foreach (var entry in entries)
            {
                string text = (string)entry.GetRawConstantValue();
                AssertEnglish(text, entry.DeclaringType.Name + "/" + entry.Name);
                if (Regex.IsMatch(text, @"\{\d"))
                    Assert.DoesNotThrow(() => GameText.Format(text, 123, 456, 789),
                        $"Ungueltige Format-Platzhalter in {entry.Name}.");
            }
            foreach (var card in FirstRunFlow.Karten)
            {
                AssertEnglish(card.Titel, "Erstlauf/Titel");
                AssertEnglish(card.Text, "Erstlauf/Text");
            }
        }

        [UnityTest]
        public IEnumerator Alle_Menueseiten_und_der_Ladebildschirm_bleiben_englisch()
        {
            yield return MenuUiHarness.LadeMenue();
            var menu = MenuUiHarness.Ui();
            AssertContains(menu.RootForTests, "PLAY");

            // Ueber stabile IDs navigieren, damit ein deutscher Knopftext den
            // Test nicht schon vor der eigentlichen Sprachpruefung unterbricht.
            foreach (string page in new[] { "spielen", "einstellungen", "zugaenglichkeit",
                         "steuerung", "daten", "quellen", "beenden" })
            {
                Assert.IsTrue(menu.ClickForTests("nav-" + page), $"Menue-Seite {page} fehlt.");
                for (int i = 0; i < 20; i++) yield return null;
                AssertEnglishTree(menu.RootForTests, "Menue/" + page);
            }

            var loading = LoadingOverlay.Instance;
            Assert.IsNotNull(loading);
            for (int i = 0; i < 120 && !loading.ReadyForTests; i++) yield return null;
            Assert.IsTrue(loading.ReadyForTests);
            loading.Begin(GameText.Menu.Bomb);
            loading.SetProgress(0.5f, GameText.Loading.LoadingMap);
            yield return null;
            var loadingRoot = loading.GetComponent<UIDocument>().rootVisualElement;
            AssertEnglishTree(loadingRoot, "Laden");
            AssertContains(loadingRoot, "LOADING MAP");
        }

        [UnityTest]
        public IEnumerator HUD_Kaufzeit_MedKit_Pause_und_Rundenende_bleiben_englisch()
        {
            NetworkPlayerController player = null;
            MatchManager match = null;
            GameSettings.TeamSize = 3;
            yield return MatchTestHarness.LoadReady((p, m) => { player = p; match = m; });
            var hud = Object.FindAnyObjectByType<HudController>();
            Assert.IsNotNull(hud);
            for (int i = 0; i < 120 && !hud.IsBuiltForTests; i++) yield return null;
            Assert.IsTrue(hud.IsBuiltForTests);

            match.ServerApplyTestConfig(15, 999f, 20f);
            match.ServerForceBombMode(player.GetComponent<TeamMember>().TeamId);
            match.ForceBuyTimeForTests = true;
            match.SuspendedForTests = false;
            Assert.IsTrue(player.GetComponent<AbilityHolder>().ServerGrant(AbilityKind.Verbandspaket));
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(BuyMenuHud.Local.ShouldShowMenu, "Das Kaufmenue wurde nicht wirklich geoeffnet.");
            AssertContains(hud.RootForTests, "BUY MENU");
            AssertContains(hud.RootForTests, "Med Kit");
            AssertContains(hud.RootForTests, "MED");
            AssertEnglishTree(hud.RootForTests, "HUD/Kaufzeit");

            match.ForceBuyTimeForTests = false;
            match.SuspendedForTests = true;
            hud.ForceScoreboardForTests = true;
            PauseMenu.SetPausedExternally(true);
            for (int i = 0; i < 5; i++) yield return null;
            AssertContains(hud.RootForTests, "RESUME");
            AssertEnglishTree(hud.RootForTests, "HUD/Pause/Punktetabelle");
            PauseMenu.ForceResume();
            BotBrain.GloballyFrozen = true;
            hud.ForceScoreboardForTests = false;

            match.ServerForceRoundEndForTests(Team.Alpha);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(hud.RoundOverShownForTests);
            AssertContains(hud.RootForTests, "WINS THE ROUND");
            AssertEnglishTree(hud.RootForTests, "HUD/Rundenende");
        }

        [Test]
        public void Uebersetzbare_Anzeigenamen_lassen_Waffen_und_Faehigkeits_IDs_unveraendert()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponStats>();
            var ability = ScriptableObject.CreateInstance<AbilityStats>();
            try
            {
                weapon.name = "Sturmgewehr";
                weapon.DisplayName = "Ein anderer Anzeigetext";
                Assert.AreEqual("Assault Rifle", weapon.LocalizedName);
                Assert.AreEqual("Sturmgewehr", weapon.name);
                ability.Kind = AbilityKind.Verbandspaket;
                ability.DisplayName = "Ein anderer Anzeigetext";
                Assert.AreEqual("Med Kit", ability.LocalizedName);
                Assert.AreEqual(AbilityKind.Verbandspaket, ability.Kind);
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(ability);
            }
        }

#if UNITY_EDITOR
        [Test]
        public void Serialisierte_Anzeigenamen_stimmen_mit_dem_englischen_Katalog_ueberein()
        {
            const string folder = "Assets/_Project/Settings";
            var weapons = UnityEditor.AssetDatabase.FindAssets("t:WeaponStats", new[] { folder });
            var abilities = UnityEditor.AssetDatabase.FindAssets("t:AbilityStats", new[] { folder });
            Assert.IsNotEmpty(weapons, "Keine serialisierten Waffen gefunden.");
            Assert.IsNotEmpty(abilities, "Keine serialisierten Faehigkeiten gefunden.");
            foreach (string guid in weapons)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var weapon = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponStats>(path);
                AssertEnglish(weapon.DisplayName, path);
                Assert.AreEqual(weapon.LocalizedName, weapon.DisplayName, path);
            }
            foreach (string guid in abilities)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var ability = UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityStats>(path);
                AssertEnglish(ability.DisplayName, path);
                Assert.AreEqual(ability.LocalizedName, ability.DisplayName, path);
            }
            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:WeaponCatalog", new[] { folder }))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<WeaponCatalog>(path);
                foreach (var entry in catalog.BuyEntries)
                {
                    AssertEnglish(entry.DisplayName, path);
                    Assert.AreEqual(GameText.Equipment.BuyEntryName(catalog, entry), entry.DisplayName, path);
                }
            }
        }
#endif
    }
}
