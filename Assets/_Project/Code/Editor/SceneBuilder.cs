using System.IO;
using Infront;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Infront.EditorTools
{
    /// <summary>
    /// Erzeugt per Code die Prefabs (Spieler, Dummy), die Waffen-Kennwerte und
    /// die Test-Arena. Nichts davon wird von Hand in der Unity-Oberflaeche gebaut.
    ///
    /// Menue: "Infront/Setup/2 - Arena und Spieler bauen"
    /// Headless: Unity -batchmode -quit -executeMethod Infront.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        const string PrefabDir = "Assets/_Project/Prefabs";
        const string SceneDir = "Assets/_Project/Scenes";
        const string SettingsDir = "Assets/_Project/Settings";
        const string PlayerPrefabPath = PrefabDir + "/Player.prefab";
        const string DummyPrefabPath = PrefabDir + "/TargetDummy.prefab";
        const string CatalogPath = SettingsDir + "/WeaponCatalog.asset";
        const string AbilityCatalogPath = SettingsDir + "/AbilityCatalog.asset";
        const string BotStatsPath = SettingsDir + "/Bot_Normal.asset";
        const string BotStatsEasyPath = SettingsDir + "/Bot_Leicht.asset";
        const string BotStatsHardPath = SettingsDir + "/Bot_Schwer.asset";
        const string MenuScenePath = SceneDir + "/Menu.unity";
        const string BotPrefabPath = PrefabDir + "/Bot.prefab";
        const string MatchManagerPrefabPath = PrefabDir + "/MatchManager.prefab";
        const string BombPrefabPath = PrefabDir + "/Bomb.prefab";
        const string ScenePath = SceneDir + "/Arena.unity";
        const string UiResourcesDir = "Assets/_Project/UI/Resources";
        const string UiThemePath = UiResourcesDir + "/InfrontRuntimeTheme.tss";
        const string UiPanelPath = UiResourcesDir + "/InfrontPanel.asset";
        const string AudioResourcesDir = "Assets/_Project/Audio/Resources";

        [MenuItem("Infront/Setup/2 - Arena und Spieler bauen")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(SettingsDir);
            EnsureAudioFolder();
            AssetDatabase.Refresh();

            // Die Umgebungsverdeckung sitzt im Renderer-Asset, nicht in der
            // Szene. Sie wird hier mitgezogen, damit auch ein reiner
            // Szenen-Build sie hat - der Aufruf tut nichts, wenn sie schon da ist.
            GraphicsTune.EnsureSsao();
            GraphicsTune.EnsureShaders();
            GraphicsTune.EnsureFxMaterials();

            // P2-P4: heruntergeladene CC0-Pakete einbauen (falls da). Ohne die
            // Ordner passiert nichts - dann bleibt alles Code-Geometrie wie bisher.
            AssetImporterTools.BuildAllSurfaceMaterials();   // Flaechen-Texturen
            AssetImporterTools.BuildHdriSkybox();            // HDRI-Himmel
            AssetImporterTools.BuildAllDecoModels();         // Deko-FBX -> Prefabs
            AssetImporterTools.BuildAllWeaponModels();       // Waffen-FBX -> Prefabs
            AssetImporterTools.BuildFigureModel();           // Mixamo-Figur (falls da)

            WeaponCatalog catalog = CreateWeaponCatalog();
            AbilityCatalog abilityCatalog = CreateAbilityCatalog();
            BotStats botStats = CreateBotStats();
            GameObject playerPrefab = BuildPlayerPrefab(catalog, abilityCatalog);
            GameObject dummyPrefab = BuildDummyPrefab();
            GameObject botPrefab = BuildBotPrefab(catalog, abilityCatalog, botStats);
            GameObject matchManagerPrefab = BuildMatchManagerPrefab();
            GameObject bombPrefab = BuildBombPrefab();
            BuildArenaScene(playerPrefab, dummyPrefab, botPrefab, matchManagerPrefab, bombPrefab);
            BuildMenuScene();

            Debug.Log("SCENE_BUILD_OK");
        }

        [MenuItem("Infront/Setup/0 - Alles aufsetzen (URP + Arena)")]
        public static void SetupEverything()
        {
            UrpSetup.Run();
            GraphicsTune.Apply();   // HDR + Adaptive-Performance-Fix
            Build();
            Debug.Log("FULL_SETUP_OK");
        }

        /// <summary>
        /// Legt den Ordner an, in den echte Sounddateien kommen. Solange er
        /// leer ist, benutzt der <see cref="Infront.AudioService"/> die
        /// Platzhalter-Töne aus <see cref="Infront.ProceduralSfx"/>.
        /// </summary>
        static void EnsureAudioFolder()
        {
            Directory.CreateDirectory(AudioResourcesDir);
            string readme = AudioResourcesDir + "/LIESMICH.txt";
            if (!File.Exists(readme))
            {
                File.WriteAllText(readme,
                    "Echte Sounddateien hier ablegen (.wav oder .ogg).\n\n" +
                    "Der Dateiname MUSS zum Ton passen - Kleinschreibung mit\n" +
                    "Unterstrichen, so wie die Einträge in SoundId.cs:\n\n" +
                    "  schuss_gewehr.wav      schuss_mp.wav      schuss_sniper.wav\n" +
                    "  schuss_pistole.wav     nachladen.wav      waffe_wechsel.wav\n" +
                    "  treffer_marke.wav      treffer_kopf.wav   abschuss.wav\n" +
                    "  eigener_tod.wav        einschlag_wand.wav einschlag_koerper.wav\n" +
                    "  schritt_leise.wav      schritt_normal.wav schritt_laut.wav\n" +
                    "  runde_start.wav        runde_sieg.wav     runde_niederlage.wav\n" +
                    "  kaufzeit_vorbei.wav    bombe_piep.wav     bombe_gelegt.wav\n" +
                    "  bombe_entschaerft.wav  bombe_explosion.wav\n\n" +
                    "Liegt eine Datei da, wird sie automatisch statt des\n" +
                    "Platzhalter-Tons benutzt. Du kannst einzeln austauschen.\n" +
                    "Gute Gratis-Quellen: freesound.org, kenney.nl/assets (Audio).\n");
            }
        }

        static WeaponStats MakeWeapon(string file, System.Action<WeaponStats> setup)
        {
            string path = SettingsDir + "/" + file + ".asset";
            var s = AssetDatabase.LoadAssetAtPath<WeaponStats>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<WeaponStats>();
                AssetDatabase.CreateAsset(s, path);
            }
            setup(s);
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            return s;
        }

        static WeaponCatalog CreateWeaponCatalog()
        {
            // Reihenfolge = Netz-Index. Nicht umsortieren!
            var sturmgewehr = MakeWeapon("Sturmgewehr", w =>
            {
                w.DisplayName = "Assault Rifle"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.ShotSound = SoundId.SchussGewehr;
                w.Damage = 18; w.FireRate = 9f; w.MagazineSize = 30; w.ReloadTime = 2f; w.Range = 200f;
                w.RecoilUp = 0.85f; w.RecoilSide = 0.3f; w.SwitchTime = 0.5f;
                w.SpreadStand = 0.15f; w.SpreadWalk = 1.4f; w.SpreadSprint = 3.2f;
                w.AdsSpreadMul = 0.4f; w.ScopeZoom = 0f;
                // Mittleres Gewicht: zuegig im Anschlag, sichtbares Nachschwingen.
                w.AdsTime = 0.32f; w.SwayScale = 1f;
                w.RecoilRandomUp = 0.28f; w.RecoilRandomSide = 0.6f;
            });
            var mp = MakeWeapon("Maschinenpistole", w =>
            {
                w.DisplayName = "SMG"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.ShotSound = SoundId.SchussMp;
                w.Damage = 12; w.FireRate = 14f; w.MagazineSize = 30; w.ReloadTime = 1.8f; w.Range = 120f;
                w.RecoilUp = 0.5f; w.RecoilSide = 0.25f; w.SwitchTime = 0.4f;
                w.SpreadStand = 0.4f; w.SpreadWalk = 1.2f; w.SpreadSprint = 2.5f;
                w.AdsSpreadMul = 0.55f; w.ScopeZoom = 0f;
                // Leicht und kurz: schnell im Anschlag, wenig Nachschwingen,
                // dafuer der unruhigste Rueckstoss von allen.
                w.AdsTime = 0.22f; w.SwayScale = 0.7f;
                w.RecoilRandomUp = 0.35f; w.RecoilRandomSide = 0.8f;
            });
            var sniper = MakeWeapon("Scharfschuetzengewehr", w =>
            {
                w.DisplayName = "Sniper Rifle"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.ShotSound = SoundId.SchussSniper;
                w.Damage = 120; w.FireRate = 1.1f; w.MagazineSize = 5; w.ReloadTime = 3.2f; w.Range = 300f;
                w.RecoilUp = 4f; w.RecoilSide = 0.2f; w.SwitchTime = 0.9f;
                // Ohne Fernrohr streut die Waffe stark, im Fernrohr trifft sie punktgenau.
                w.SpreadStand = 1.6f; w.SpreadWalk = 5f; w.SpreadSprint = 10f; w.SpreadAir = 14f;
                w.AdsSpreadMul = 0.05f; w.ScopeZoom = 4f;
                w.HeadshotMultiplier = 2f;
                // Schwer und lang: braucht am laengsten in den Anschlag und
                // schwingt am staerksten nach. Der Rueckstoss ist heftig,
                // aber berechenbar - man schiesst ja nur einmal.
                w.AdsTime = 0.62f; w.SwayScale = 1.6f;
                w.RecoilRandomUp = 0.12f; w.RecoilRandomSide = 0.2f;
            });
            var pistole = MakeWeapon("Pistole", w =>
            {
                w.DisplayName = "Pistol"; w.SlotKind = WeaponStats.Slot.Pistole;
                w.ShotSound = SoundId.SchussPistole;
                w.Damage = 14; w.FireRate = 5f; w.MagazineSize = 14; w.ReloadTime = 1.5f; w.Range = 90f;
                w.RecoilUp = 1.2f; w.RecoilSide = 0.4f; w.SwitchTime = 0.3f;
                w.SpreadStand = 0.4f; w.SpreadWalk = 1.5f; w.SpreadSprint = 3f;
                w.AdsSpreadMul = 0.5f; w.ScopeZoom = 0f;
                // Am leichtesten: sofort einsatzbereit, kaum Nachschwingen.
                w.AdsTime = 0.18f; w.SwayScale = 0.5f;
                w.RecoilRandomUp = 0.4f; w.RecoilRandomSide = 0.7f;
            });
            var botRifle = MakeWeapon("Bot_Sturmgewehr", w =>
            {
                w.DisplayName = "Assault Rifle"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.ShotSound = SoundId.SchussGewehr;
                w.Damage = 12; w.FireRate = 9f; w.MagazineSize = 30; w.ReloadTime = 2f; w.Range = 200f;
                w.RecoilUp = 0.4f; w.RecoilSide = 0.2f; w.SwitchTime = 0.5f;
                w.SpreadStand = 0.3f; w.SpreadWalk = 1.6f; w.SpreadSprint = 3.5f;
            });
            // Bot-Versionen der Kaufwaffen: etwas weniger Schaden, mehr Streuung,
            // damit die Schwierigkeit stimmt (wie beim Bot-Sturmgewehr).
            var botMp = MakeWeapon("Bot_Maschinenpistole", w =>
            {
                w.DisplayName = "SMG"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.ShotSound = SoundId.SchussMp;
                w.Damage = 9; w.FireRate = 14f; w.MagazineSize = 30; w.ReloadTime = 1.8f; w.Range = 120f;
                w.RecoilUp = 0.3f; w.RecoilSide = 0.15f; w.SwitchTime = 0.4f;
                w.SpreadStand = 0.6f; w.SpreadWalk = 1.6f; w.SpreadSprint = 3f;
            });
            var botSniper = MakeWeapon("Bot_Scharfschuetzengewehr", w =>
            {
                w.DisplayName = "Sniper Rifle"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.ShotSound = SoundId.SchussSniper;
                w.Damage = 90; w.FireRate = 1.0f; w.MagazineSize = 5; w.ReloadTime = 3.2f; w.Range = 300f;
                w.RecoilUp = 3f; w.RecoilSide = 0.2f; w.SwitchTime = 0.9f;
                w.SpreadStand = 0.4f; w.SpreadWalk = 4f; w.SpreadSprint = 9f; w.SpreadAir = 12f;
                w.HeadshotMultiplier = 2f;
            });

            var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }
            // Reihenfolge = Netz-Index. 0..4 nie umsortieren (steht in Speicherdaten
            // fuer spaeter). Neue Bot-Waffen hinten anhaengen.
            cat.Weapons = new[] { sturmgewehr, mp, sniper, pistole, botRifle, botMp, botSniper };
            //                    0            1   2      3        4         5      6

            cat.BuyEntries = new[]
            {
                new WeaponCatalog.BuyEntry { DisplayName = "SMG",       Price = 1500, PlayerWeaponIndex = 1, BotWeaponIndex = 5 },
                new WeaponCatalog.BuyEntry { DisplayName = "Assault Rifle",             Price = 2700, PlayerWeaponIndex = 0, BotWeaponIndex = 4 },
                new WeaponCatalog.BuyEntry { DisplayName = "Sniper Rifle",   Price = 4750, PlayerWeaponIndex = 2, BotWeaponIndex = 6 },
            };

            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            return cat;
        }

        static AbilityStats MakeAbility(string file, System.Action<AbilityStats> setup)
        {
            string path = SettingsDir + "/" + file + ".asset";
            var s = AssetDatabase.LoadAssetAtPath<AbilityStats>(path);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<AbilityStats>();
                AssetDatabase.CreateAsset(s, path);
            }
            setup(s);
            EditorUtility.SetDirty(s);
            AssetDatabase.SaveAssets();
            return s;
        }

        static AbilityCatalog CreateAbilityCatalog()
        {
            var rauch = MakeAbility("Faehigkeit_Rauchwand", a =>
            {
                a.Kind = AbilityKind.Rauchwand; a.DisplayName = "Smoke Wall"; a.Slot = AbilitySlot.Q;
                a.Price = 300; a.Charges = 1; a.Cooldown = 0f;
                a.Duration = 15f; a.Radius = 4.5f; a.ThrowRange = 16f;
            });
            var blend = MakeAbility("Faehigkeit_Blendgranate", a =>
            {
                a.Kind = AbilityKind.Blendgranate; a.DisplayName = "Flashbang"; a.Slot = AbilitySlot.G;
                a.Price = 250; a.Charges = 2; a.Cooldown = 0f;
                a.Duration = 2f; a.Radius = 10f; a.ThrowRange = 14f;
            });
            var splitter = MakeAbility("Faehigkeit_Splittergranate", a =>
            {
                a.Kind = AbilityKind.Splittergranate; a.DisplayName = "Frag Grenade"; a.Slot = AbilitySlot.G;
                a.Price = 300; a.Charges = 1; a.Cooldown = 0f;
                a.Duration = 0f; a.Radius = 5.5f; a.ThrowRange = 16f;
            });
            var scan = MakeAbility("Faehigkeit_ScanPuls", a =>
            {
                a.Kind = AbilityKind.ScanPuls; a.DisplayName = "Scan Pulse"; a.Slot = AbilitySlot.F;
                a.Price = 250; a.Charges = 1; a.Cooldown = 0f;
                a.Duration = 3f; a.Radius = 16f; a.ThrowRange = 2f;
                // Schritt 6: bleibt vollstaendig im Spiel, wird aber nicht mehr
                // angeboten - Gegner durch Waende zu sehen passt nicht zum
                // Realismus. Auf true stellen und er ist sofort wieder da.
                a.Angeboten = false;
            });
            var brand = MakeAbility("Faehigkeit_Brandwand", a =>
            {
                a.Kind = AbilityKind.Brandwand; a.DisplayName = "Incendiary Wall"; a.Slot = AbilitySlot.Q;
                a.Price = 300; a.Charges = 1; a.Cooldown = 0f;
                a.Duration = 8f; a.Radius = 4f; a.ThrowRange = 14f;
            });
            var verband = MakeAbility("Faehigkeit_Verbandspaket", a =>
            {
                a.Kind = AbilityKind.Verbandspaket; a.DisplayName = "Med Kit";
                a.Slot = AbilitySlot.F;
                a.Price = 200; a.Charges = 1; a.Cooldown = 0f;
                a.Duration = 0f;
                // Radius wird hier als Heilmenge benutzt: 25 Leben zurueck.
                // Bewusst wenig - der Verband stoppt vor allem das Bluten.
                a.Radius = 25f; a.ThrowRange = 0f;
                a.AmBenutzer = true;
            });
            var draht = MakeAbility("Faehigkeit_Stolperdraht", a =>
            {
                a.Kind = AbilityKind.Stolperdraht; a.DisplayName = "Tripwire"; a.Slot = AbilitySlot.F;
                a.Price = 200; a.Charges = 1; a.Cooldown = 0f;
                a.Duration = 30f; a.Radius = 3f; a.ThrowRange = 6f;
            });

            var cat = AssetDatabase.LoadAssetAtPath<AbilityCatalog>(AbilityCatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<AbilityCatalog>();
                AssetDatabase.CreateAsset(cat, AbilityCatalogPath);
            }
            // Reihenfolge = Netz-Index. Neue Faehigkeiten hinten anhaengen.
            // Reihenfolge = Netz-Index und steht in Kaufdaten. Der Scan-Puls
            // bleibt deshalb an seiner Stelle stehen, obwohl er nicht mehr
            // angeboten wird - wuerde man ihn herausnehmen, verschoeben sich
            // alle Eintraege dahinter.
            cat.Abilities = new[] { rauch, blend, splitter, scan, brand, draht, verband };

            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            return cat;
        }

        static BotStats CreateBotStats()
        {
            // Leicht/Normal/Schwer stellen jetzt Reaktion, Zielguete, Nachziehen,
            // Aggressivitaet, Hoervermoegen und Teamwork ein - nicht nur das Tempo.
            // Sichtweiten an die grosse Karte "Werk" angepasst (lange Bahnen).
            var normal = LoadOrCreateBotStats(BotStatsPath,
                spread: 5f, reaction: 0.35f, view: 34f,
                track: 220f, aggr: 0.5f, hearing: 1f, teamwork: 0.5f);
            LoadOrCreateBotStats(BotStatsEasyPath,
                spread: 9f, reaction: 0.75f, view: 25f,
                track: 120f, aggr: 0.3f, hearing: 0.6f, teamwork: 0.25f);
            LoadOrCreateBotStats(BotStatsHardPath,
                spread: 2.3f, reaction: 0.16f, view: 42f,
                track: 340f, aggr: 0.8f, hearing: 1.3f, teamwork: 0.8f);
            return normal;
        }

        static BotStats LoadOrCreateBotStats(string path, float spread, float reaction, float view,
                                             float track, float aggr, float hearing, float teamwork)
        {
            var stats = AssetDatabase.LoadAssetAtPath<BotStats>(path);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<BotStats>();
                AssetDatabase.CreateAsset(stats, path);
            }
            stats.AimSpread = spread;
            stats.ReactionTime = reaction;
            stats.ViewDistance = view;
            stats.AimTrackSpeed = track;
            stats.Aggression = aggr;
            stats.Hearing = hearing;
            stats.Teamwork = teamwork;
            EditorUtility.SetDirty(stats);
            AssetDatabase.SaveAssets();
            return stats;
        }

        static GameObject BuildPlayerPrefab(WeaponCatalog catalog, AbilityCatalog abilityCatalog)
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.3f;

            root.AddComponent<NetworkObject>();
            root.layer = 7; // Character - vom Trefferstrahl ausgenommen

            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            netTransform.Interpolate = true;

            var playerController = root.AddComponent<NetworkPlayerController>();
            var health = root.AddComponent<Health>();
            root.AddComponent<TeamMember>();
            root.AddComponent<TeamTint>();
            root.AddComponent<CharacterVisual>();   // stilisierte Figur statt Kapsel
            root.AddComponent<FriendlyNameplates>();
            // Schritt 5: Blutungen und Zonenfolgen liegen neben der Health,
            // die selbst unveraendert bleibt.
            root.AddComponent<Bleeding>();
            AddHitboxes(root, health);

            // Sichtbarer Koerper (nur Optik, keine Collider)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            // Ziel-Drehpunkt auf Augenhoehe: neigt sich beim Zielen hoch/runter
            var aimPivot = new GameObject("AimPivot");
            aimPivot.transform.SetParent(root.transform, false);
            aimPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            // Nase zeigt die Zielrichtung, haengt am Drehpunkt
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(aimPivot.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.4f);
            nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);

            // Muendungspunkt: Ursprung der Schussspur
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(aimPivot.transform, false);
            muzzle.transform.localPosition = new Vector3(0.2f, -0.1f, 0.5f);

            var weaponComponent = root.AddComponent<NetworkWeapon>();
            root.AddComponent<TracerEffect>();
            root.AddComponent<MuzzleFlash>();      // Muendungsfeuer pro Schuss
            root.AddComponent<ShellEjector>();     // fliegende Patronenhuelsen
            root.AddComponent<DamageFeedback>();
            root.AddComponent<ViewModel>();        // sichtbare Waffe in der Hand (nur Besitzer)
            root.AddComponent<ScopeOverlay>();     // schwarzes Zielfernrohr-Bild (nur Besitzer)
            root.AddComponent<BulletWhiz>();       // Zischen bei dicht vorbeifliegenden Kugeln
            root.AddComponent<CombatAudio>();      // Treffer-/Abschuss-/Tod-Ton (nur Besitzer)
            root.AddComponent<FootstepSounds>();   // Schritt-Geraeusche nach Tempo
            root.AddComponent<Wallet>();
            root.AddComponent<BombAction>();
            var abilityHolder = root.AddComponent<AbilityHolder>();
            var purchaseAgent = root.AddComponent<PurchaseAgent>();
            root.AddComponent<BuyMenuHud>();
            root.AddComponent<BombHud>();
            root.AddComponent<AbilityHud>();       // Q/F/G-Leiste + Blitz-Bildschirm
            var lifecycle = root.AddComponent<PlayerLifecycle>();

            // Referenzen per SerializedObject setzen (private [SerializeField])
            var so = new SerializedObject(playerController);
            so.FindProperty("_aimPivot").objectReferenceValue = aimPivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var soWeapon = new SerializedObject(weaponComponent);
            soWeapon.FindProperty("_catalog").objectReferenceValue = catalog;
            soWeapon.FindProperty("_defaultPrimary").intValue = 0;   // Sturmgewehr (nur fuer Tests)
            soWeapon.FindProperty("_defaultSecondary").intValue = 3; // Pistole
            soWeapon.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            soWeapon.FindProperty("_hitMask").intValue = (1 << 0) | (1 << 6);
            soWeapon.ApplyModifiedPropertiesWithoutUndo();

            var soPurchase = new SerializedObject(purchaseAgent);
            soPurchase.FindProperty("_catalog").objectReferenceValue = catalog;
            soPurchase.FindProperty("_abilityCatalog").objectReferenceValue = abilityCatalog;
            soPurchase.ApplyModifiedPropertiesWithoutUndo();

            var soAbility = new SerializedObject(abilityHolder);
            soAbility.FindProperty("_catalog").objectReferenceValue = abilityCatalog;
            soAbility.ApplyModifiedPropertiesWithoutUndo();

            var soLife = new SerializedObject(lifecycle);
            var hideArray = soLife.FindProperty("_hideOnDeath");
            hideArray.arraySize = 2;
            hideArray.GetArrayElementAtIndex(0).objectReferenceValue = body;
            hideArray.GetArrayElementAtIndex(1).objectReferenceValue = nose;
            soLife.ApplyModifiedPropertiesWithoutUndo();

            // First Person: eigener Koerper fuer den Besitzer unsichtbar
            var soHide = new SerializedObject(playerController);
            var hideOwner = soHide.FindProperty("_hideForOwner");
            hideOwner.arraySize = 2;
            hideOwner.GetArrayElementAtIndex(0).objectReferenceValue = body;
            hideOwner.GetArrayElementAtIndex(1).objectReferenceValue = nose;
            soHide.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || prefab == null)
            {
                Debug.LogError("[Infront] Spieler-Prefab konnte nicht gespeichert werden.");
                return null;
            }

            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Debug.Log($"[Infront] Spieler-Prefab bereit. NetworkObject={prefab.GetComponent<NetworkObject>() != null}");
            return prefab;
        }

        static GameObject BuildDummyPrefab()
        {
            var root = new GameObject("TargetDummy");
            root.AddComponent<NetworkObject>();
            root.layer = 7;
            var dummyHealth = root.AddComponent<Health>();
            var dummy = root.AddComponent<TargetDummy>();

            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.Interpolate = false;

            // Sichtbarer Koerper (nur Optik) - Trefferflaechen sind eigene Hitboxen
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);

            AddHitboxes(root, dummyHealth);

            var soDummy = new SerializedObject(dummy);
            var hideArray = soDummy.FindProperty("_hideOnDeath");
            hideArray.arraySize = 1;
            hideArray.GetArrayElementAtIndex(0).objectReferenceValue = body;
            soDummy.ApplyModifiedPropertiesWithoutUndo();

            var soHealth = new SerializedObject(root.GetComponent<Health>());
            soHealth.FindProperty("_maxHealth").intValue = 60;
            soHealth.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DummyPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || prefab == null)
            {
                Debug.LogError("[Infront] Dummy-Prefab konnte nicht gespeichert werden.");
                return null;
            }

            AssetDatabase.ImportAsset(DummyPrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPrefabPath);
            Debug.Log($"[Infront] Dummy-Prefab bereit.");
            return prefab;
        }

        // ---- Karte: spiegelsymmetrisch in Z (Alpha bei -Z, Bravo bei +Z) ----

        static Transform _mapRoot;

        static void Block(string name, float x, float y, float z, float sx, float sy, float sz)
        {
            // Ein Bauteil, drei Aufgaben - und bis hierher fuer alle dasselbe
            // Material. 26 Aufrufe, ein Beton, eine Kachelung: die Aussenwand
            // der Halle, die Trennwaende im Inneren und die Saeulen sahen
            // identisch aus. Das ist der Hauptgrund, warum die Karte trotz
            // Texturen wie eine Graukiste wirkt - nicht die Schattierung,
            // sondern die Gleichfoermigkeit.
            //
            // Jetzt entscheidet der NAME ueber den Stoff, so wie es in einem
            // echten Werk waere: die Huelle ist Beton, die Trennwaende sind
            // Blech, die Saeulen sind ein anderer, feiner gekoernter Beton.
            // Das ist Absicht nach Bauteil, nicht Zufall nach Hash - sonst
            // steht neben derselben Wand mal Blech, mal Beton.
            if (name.StartsWith("Screen_"))
            {
                // Trennwaende: Blech im Rahmen. Enger gekachelt, damit die
                // Struktur auf 10 m Breite nicht zu Brei wird.
                Surfaced(name, x, y, z, sx, sy, sz, "deckung_metall", new Vector2(0.8f, 0.8f),
                         new Color(0.30f, 0.31f, 0.34f));
                return;
            }

            // Saeulen. ACHTUNG bei den Namen: in der gebauten Karte heissen sie
            // "HallePil_A", nicht "...Pillar...". Eine erste Fassung pruefte auf
            // "Pillar" und traf deshalb nur die Menue-Kulisse - in der Arena
            // aenderte sich nichts, und die Messung zeigte folgerichtig eine
            // Null. Die Namen stammen aus der gebauten Szene, nicht aus dem
            // Gedaechtnis.
            if (name.StartsWith("HallePil") || name.Contains("Pillar"))
            {
                Surfaced(name, x, y, z, sx, sy, sz, "platte", new Vector2(1.1f, 1.1f),
                         new Color(0.34f, 0.34f, 0.36f));
                return;
            }

            // ZURUECKGENOMMEN: die grossen Hallenwaende (HalleWall_*, TunWall_*)
            // hatten hier kurzzeitig den zweiten Beton bekommen. Gemessen war
            // das ein klarer Rueckschritt - Median 85 -> 65, unlesbare Flaeche
            // 27,6 % -> 33,5 %. Concrete016 ist ein dunkler Zuschlagbeton; auf
            // der groessten Flaeche der Karte zieht er das ganze Bild herunter,
            // und genau die Lesbarkeit war vorher muehsam erkaempft worden.
            //
            // Merksatz: Abwechslung darf nicht ueber die groesste Flaeche
            // gehen. Wer Vielfalt will, nimmt die kleinen Bauteile - Saeulen,
            // Trennwaende, Deckung - und laesst die Huelle in Ruhe.

            // Huelle: grober Beton, weit gekachelt.
            Surfaced(name, x, y, z, sx, sy, sz, "wand_beton", new Vector2(0.5f, 0.5f),
                     new Color(0.12f, 0.14f, 0.22f));   // sonst fast schwarz
        }

        /// <summary>Deckungsklotz. Nicht alles gleich grau: nach Namens-Hash
        /// abwechselnd Metallplatte oder Beton, enger gekachelt (mehr Detail),
        /// leicht unterschiedlich getoent. Metall bekommt echten Metallglanz.</summary>
        static void Crate(string name, float x, float y, float z, float sx, float sy, float sz)
        {
            bool metal = (Mathf.Abs(name.GetHashCode()) % 5) < 3;   // ~60% Metall, ~40% Beton
            if (metal)
                // Metallwert von 0,75 auf 0,3. Bei 0,75 ist das blankes,
                // poliertes Metall: es hat fast keine eigene Farbe mehr und
                // zeigt nur noch die Spiegelung der Umgebung. In einer offenen
                // Karte war das der Himmel und sah gut aus - seit die Halle ein
                // Dach hat, ist da nichts mehr zu spiegeln, und die Kisten
                // kamen tiefschwarz heraus. Lackierter Stahl, wie er in einem
                // Werk wirklich steht, ist ohnehin ueberwiegend nicht metallisch.
                Surfaced(name, x, y, z, sx, sy, sz, "deckung_metall", new Vector2(1.5f, 1.5f),
                         new Color(0.34f, 0.36f, 0.40f), metallic: 0.3f, smoothness: 0.38f);
            else
                Surfaced(name, x, y, z, sx, sy, sz, "wand_beton", new Vector2(1.2f, 1.2f),
                         new Color(0.42f, 0.42f, 0.44f), metallic: 0f, smoothness: 0.15f);
        }

        static readonly System.Collections.Generic.Dictionary<Color, Material> _mats = new();

        /// <summary>
        /// Untergrenze fuer die Grundfarbe eines Materials.
        ///
        /// Warum es das gibt (Rundgang 2026-09-04): quer durch die Karte standen
        /// Grundfarben zwischen 0,05 und 0,12 Helligkeit. So dunkel ist in der
        /// Wirklichkeit fast nichts. Frischer Asphalt liegt bei etwa 0,08, alter
        /// Beton bei 0,35 bis 0,55, dunkel lackierter Stahl bei 0,15 bis 0,25.
        /// Unter 0,10 kommen nur Russ, Kohle und Gummi. Eine Halle aus solchen
        /// Farben kann gar nicht echt aussehen: es fehlt nicht die Textur,
        /// es fehlt das Licht, das die Flaeche zurueckwerfen wuerde.
        ///
        /// Gemessen wurde es an den Rundgang-Bildern: 27 % jedes Bildes war
        /// praktisch schwarz, 51 % unter der Erkennbarkeitsschwelle.
        ///
        /// Deshalb wird jede Grundfarbe auf diese Helligkeit angehoben - der
        /// Farbton bleibt, nur die Helligkeit steigt. 0,13 ist immer noch
        /// dunkel, aber es ist eine Helligkeit, die es wirklich gibt.
        /// </summary>
        const float MindestAlbedo = 0.13f;

        /// <summary>Hebt zu dunkle Grundfarben auf einen echten Wert an,
        /// ohne den Farbton zu verschieben.</summary>
        static Color Echt(Color c)
        {
            float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            if (l >= MindestAlbedo || l <= 0.0001f) return c;
            float k = MindestAlbedo / l;
            return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k),
                             Mathf.Clamp01(c.b * k), c.a);
        }

        static Material MapMat(Color c)
        {
            if (_mats.TryGetValue(c, out var m)) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader) { name = "MapMat" };
            var e = Echt(c);
            m.color = e;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", e);
            _mats[c] = m;
            return m;
        }

        // --- P2: echte Textur-Materialien mit Rueckfall auf die Farbtoene -----
        static readonly System.Collections.Generic.Dictionary<string, Material> _texMats = new();

        /// <summary>
        /// Material fuer eine Flaechen-Rolle: liegt unter Resources/Materials/&lt;key&gt;
        /// ein echtes Textur-Material (via <see cref="AssetImporterTools"/> gebaut),
        /// wird das benutzt - mit einer eigenen Instanz, damit die Kachelung pro
        /// Rolle stimmt und das Resources-Material selbst unangetastet bleibt.
        /// Sonst kommt das bisherige einfarbige <see cref="MapMat"/> zurueck.
        /// <paramref name="tilePerUnit"/> ist die Kachelung pro Weltmeter.
        /// </summary>
        static Material RoleMat(string key, Vector2 tilePerUnit, Color fallbackTint)
        {
            string cacheKey = key + tilePerUnit;
            if (_texMats.TryGetValue(cacheKey, out var m)) return m;

            var real = Infront.AssetLibrary.Surface(key);
            if (real != null)
            {
                m = new Material(real) { name = "Tex_" + key };
            }
            else
            {
                m = MapMat(fallbackTint);
            }
            _texMats[cacheKey] = m;
            return m;
        }

        static void Tinted(string name, float x, float y, float z, float sx, float sy, float sz, Color c)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(_mapRoot, true);
            b.transform.position = new Vector3(x, y, z);
            b.transform.localScale = new Vector3(sx, sy, sz);
            b.GetComponent<Renderer>().sharedMaterial = MapMat(c);
        }

        /// <summary>Wie <see cref="Tinted"/>, aber mit Textur-Material (Rolle
        /// <paramref name="key"/>) und pro-Objekt richtig eingestellter Kachelung.</summary>
        static void Surfaced(string name, float x, float y, float z, float sx, float sy, float sz,
                             string key, Vector2 tilePerUnit, Color fallbackTint,
                             float metallic = -1f, float smoothness = -1f)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(_mapRoot, true);
            b.transform.position = new Vector3(x, y, z);
            b.transform.localScale = new Vector3(sx, sy, sz);

            var baseMat = RoleMat(key, tilePerUnit, fallbackTint);
            var r = b.GetComponent<Renderer>();

            if (baseMat.HasProperty("_BaseMap") && baseMat.GetTexture("_BaseMap") != null)
            {
                // Kachelung an die groesste sichtbare Flaeche des Quaders anpassen,
                // damit die Textur nicht gestreckt wirkt.
                var inst = new Material(baseMat) { name = baseMat.name + "_" + name };

                // Die Kachelung muss zur GROESSTEN sichtbaren Flaeche passen,
                // nicht blind zur Hoehe.
                //
                // Vorher stand hier fest (max(sx,sz), sy). Bei einer Wand ist
                // das richtig. Bei einer flachen Platte ist die sichtbare
                // Flaeche aber die Oberseite (sx mal sz), und sy ist nur die
                // Dicke - bei MidDais 0.5 m. Ergebnis war eine Kachelung von
                // (4.90, 0.18): ein 18-Prozent-Ausschnitt der Betontextur wurde
                // ueber zehn Meter gezogen. Erwischte er eine dunkle Stelle,
                // war die ganze Platte gleichmaessig fast schwarz. Genau das
                // war der "schwarze Podeste"-Fehler.
                //
                // Jetzt: die zwei groessten Kanten bestimmen die Kachelung.
                float k1 = Mathf.Max(sx, Mathf.Max(sy, sz));                  // groesste
                float k3 = Mathf.Min(sx, Mathf.Min(sy, sz));                  // kleinste
                float k2 = sx + sy + sz - k1 - k3;                            // mittlere
                Vector2 scale = new Vector2(k1 * tilePerUnit.x, k2 * tilePerUnit.y);
                inst.SetTextureScale("_BaseMap", scale);
                if (inst.HasProperty("_BumpMap")) inst.SetTextureScale("_BumpMap", scale);
                // Regler nur setzen, solange KEINE Glanzkarte da ist.
                //
                // Seit die Materialien eine _MetallicGlossMap tragen, ist
                // _Metallic wirkungslos (der Wert kommt aus dem roten Kanal)
                // und _Smoothness nur noch ein MULTIPLIKATOR auf das Alpha.
                // Wuerde hier weiterhin 0,15 oder 0,38 gesetzt, waere die
                // frisch verkabelte Karte sofort wieder auf ein Sechstel
                // heruntergedreht - die Halle saehe stumpfer aus als vorher,
                // und zwar genau wegen der Verbesserung.
                bool hatGlanzkarte = inst.HasProperty("_MetallicGlossMap")
                                     && inst.GetTexture("_MetallicGlossMap") != null;
                if (!hatGlanzkarte)
                {
                    if (metallic >= 0f && inst.HasProperty("_Metallic")) inst.SetFloat("_Metallic", metallic);
                    if (smoothness >= 0f && inst.HasProperty("_Smoothness")) inst.SetFloat("_Smoothness", smoothness);
                }

                // ---- Abnutzung ohne einen einzigen zusaetzlichen Zeichenaufruf
                //
                // Warum ueberhaupt: die Halle sah fabrikneu aus. JEDE
                // texturierte Flaeche bekam denselben Farbton (0,72 / 0,72 /
                // 0,74) und denselben Ausschnitt derselben Betontextur - also
                // sah jede Wand exakt aus wie die daneben. Das ist der
                // eigentliche Grund fuer den Bauklotz-Eindruck, nicht die
                // fehlenden Flecken.
                //
                // Ein Versuch mit aufgelegten Schmutzflecken ist zuvor
                // gescheitert: durchscheinende Rechtecke ohne Textur wurden zu
                // harten schwarzen Kacheln auf dem Boden, und die Bildrate fiel
                // von 60 auf 55,5 (18,0 ms). Verworfen.
                //
                // Das hier kostet nichts: derselbe Baustein, nur ein anderer
                // Ausschnitt und ein anderer Hauch Farbe - abgeleitet aus dem
                // Namen, also bei jedem Build gleich.
                int hash = Mathf.Abs(name.GetHashCode());
                float a = (hash % 997) / 997f;
                float b2 = ((hash / 997) % 991) / 991f;

                // Anderer Ausschnitt der Textur. Nachbarwaende zeigen damit
                // nicht mehr denselben Fleck.
                var versatz = new Vector2(a * 3.7f, b2 * 3.7f);
                inst.SetTextureOffset("_BaseMap", versatz);
                if (inst.HasProperty("_BumpMap")) inst.SetTextureOffset("_BumpMap", versatz);

                if (fallbackTint.maxColorComponent < 0.8f)
                {
                    // Helligkeit plus/minus rund zehn Prozent, dazu ein Hauch
                    // warm oder kalt. Nie Richtung Schwarz - das war hier schon
                    // zweimal der Fehler.
                    float hell = Mathf.Lerp(0.88f, 1.06f, a);
                    float warm = Mathf.Lerp(-0.03f, 0.03f, b2);
                    var t = new Color(
                        Mathf.Clamp(0.72f * hell + warm, 0.45f, 1f),
                        Mathf.Clamp(0.72f * hell, 0.45f, 1f),
                        Mathf.Clamp(0.74f * hell - warm, 0.45f, 1f));
                    if (inst.HasProperty("_BaseColor")) inst.SetColor("_BaseColor", t);
                    inst.color = t;
                }
                r.sharedMaterial = inst;
            }
            else
            {
                r.sharedMaterial = baseMat;
            }
        }

        static readonly System.Collections.Generic.Dictionary<(Color, float), Material> _glowMats = new();

        static Material GlowMat(Color c) => GlowMat(c, 3.2f);

        /// <summary><paramref name="emission"/> steuert die Leuchtkraft - klein
        /// (~1.2) fuer dezente Akzente, gross (~3) fuer echte Lampen.</summary>
        static Material GlowMat(Color c, float emission)
        {
            var key = (c, emission);
            if (_glowMats.TryGetValue(key, out var m)) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GlowMat" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * emission);
            _glowMats[key] = m;
            return m;
        }

        /// <summary>Leuchtender Akzentstreifen - fuehrt das Auge an Kanten und
        /// Durchgaengen. Bewusst dezent (Screenshot-Test 2026-09-04: die
        /// Streifen waren viel zu grell und dick).</summary>
        static void Stripe(string name, float x, float y, float z, float sx, float sy, float sz)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(_mapRoot, true);
            b.transform.position = new Vector3(x, y, z);
            b.transform.localScale = new Vector3(sx, sy * 0.4f, sz);
            var col = b.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);   // Deko, nicht beschussrelevant
            // Screenshot-Test 4: die orangen "Landebahn"-Streifen weg. Jetzt eine
            // Metall-Kantenleiste, die in der Geometrie fast verschwindet.
            //
            // Von 0,11 auf 0,30 aufgehellt: bei 0,11 sass auf jeder Wandkrone
            // ein schwarzer Balken, und seit die Halle ein Dach hat (also kein
            // heller Himmel mehr dahinter) sah das aus wie ein Loch in der Wand
            // statt wie eine Leiste. 0,30 liest sich als Stahl, ohne zu leuchten.
            b.GetComponent<Renderer>().sharedMaterial = MapMat(new Color(0.30f, 0.30f, 0.32f));
        }

        static void StripeM(string name, float x, float y, float z, float sx, float sy, float sz)
        {
            Stripe(name + "_B", x, y, z, sx, sy, sz);
            Stripe(name + "_A", x, y, -z, sx, sy, sz);
        }

        /// <summary>Punktlicht an einer Engstelle - Gegner zeichnen sich als
        /// Silhouette ab. Mit <paramref name="shadows"/> wirft es echte weiche
        /// Schatten (nur fuer die wenigen grossen Ankerlichter - Schatten von
        /// Punktlichtern kosten).</summary>
        static void PointLightAt(string name, Vector3 pos, Color c, float range, float intensity,
                                 bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_mapRoot, true);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            if (shadows)
            {
                l.shadows = LightShadows.Soft;
                l.shadowStrength = 0.72f;
                l.shadowBias = 0.15f;
                l.shadowNormalBias = 0.6f;
                l.renderMode = LightRenderMode.ForcePixel;   // nie wegoptimieren
            }
            else
            {
                l.shadows = LightShadows.None;
            }
        }

        /// <summary>Treibender Staub in der Arena (mehrere Volumen). Dichte und
        /// Farbe steuert der <see cref="WeatherDirector"/> zur Laufzeit. Nicht an
        /// die Karte gehaengt, damit Sicht-Tests (ClearArena) ihn nicht mit
        /// ausblenden.</summary>
        static void BuildAtmosphereDust()
        {
            void Vol(string name, Vector3 pos, Vector3 box)
            {
                var go = new GameObject(name);
                go.transform.position = pos;
                go.AddComponent<ParticleSystem>();
                var d = go.AddComponent<AtmosphereDust>();
                var so = new SerializedObject(d);
                var b = so.FindProperty("_boxSize");
                if (b != null) b.vector3Value = box;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // grosses, flaches Bett ueber der ganzen Karte
            Vol("Dust_Arena", new Vector3(0f, 3.5f, 0f), new Vector3(90f, 6f, 90f));
            // dichter in der Halle und an den beiden Bombenplaetzen (dort stehen
            // in P3 die Lichtschaechte, in denen der Staub aufblitzt)
            Vol("Dust_Halle", new Vector3(0f, 4f, 24f), new Vector3(20f, 7f, 34f));
            Vol("Dust_SiteA", new Vector3(-20f, 3.5f, 0f), new Vector3(16f, 6f, 16f));
            Vol("Dust_SiteB", new Vector3(20f, 3.5f, 0f), new Vector3(16f, 6f, 16f));
        }

        /// <summary>Farbige Bodenmarkierung fuer einen Bombenplatz mit grossem
        /// Buchstaben (A / B), aus flachen leuchtenden Balken.</summary>
        static void SiteLetter(float x, float z, char letter, Color c)
        {
            // Frueher leuchteten die Buchstaben. Von oben sah man dann zwei
            // riesige Neon-Lettern auf dem Hallenboden - das war der
            // auffaelligste Arcade-Rest der Karte. In einem echten Werk ist so
            // eine Kennzeichnung aufgemalt: abgelaufene Farbe, matt, sie
            // reflektiert nur das Licht, das ohnehin da ist.
            var mat = MapMat(Color.Lerp(c, new Color(0.86f, 0.84f, 0.78f), 0.45f));
            void Bar(float bx, float bz, float bw, float bd)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = $"Site{letter}_Bar";
                b.transform.SetParent(_mapRoot, true);
                b.transform.position = new Vector3(x + bx, 1.30f, z + bz);
                b.transform.localScale = new Vector3(bw, 0.05f, bd);
                var col = b.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                b.GetComponent<Renderer>().sharedMaterial = mat;
            }

            // gemeinsame Grundform: zwei senkrechte Striche + Querbalken
            Bar(-0.9f, 0f, 0.35f, 3.6f);   // linker Strich
            Bar(0.9f, 0f, 0.35f, 3.6f);    // rechter Strich
            Bar(0f, 0f, 2.1f, 0.35f);      // Querbalken Mitte
            if (letter == 'A') Bar(0f, 1.6f, 2.1f, 0.35f);        // A: oben zu
            else { Bar(0f, 1.6f, 2.1f, 0.35f); Bar(0f, -1.6f, 2.1f, 0.35f); }  // B: oben + unten zu
        }

        // Block plus sein Spiegelbild an -z
        static void BlockM(string name, float x, float y, float z, float sx, float sy, float sz)
        {
            Block(name + "_B", x, y, z, sx, sy, sz);
            Block(name + "_A", x, y, -z, sx, sy, sz);
        }

        static void CrateM(string name, float x, float y, float z, float sx, float sy, float sz)
        {
            Crate(name + "_B", x, y, z, sx, sy, sz);
            Crate(name + "_A", x, y, -z, sx, sy, sz);
        }

        static void Ramp(string name, float x, float z, float lowZ, float highY, float width)
        {
            // schraege Flaeche als gedrehter, flacher Block
            var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.name = name;
            r.transform.SetParent(_mapRoot, true);
            float len = 6f;
            r.transform.position = new Vector3(x, highY * 0.5f, z);
            r.transform.localScale = new Vector3(width, 0.4f, len);
            float ang = Mathf.Atan2(highY, len) * Mathf.Rad2Deg * (lowZ < z ? 1f : -1f);
            r.transform.rotation = Quaternion.Euler(ang, 0f, 0f);
            r.GetComponent<Renderer>().sharedMaterial = MapMat(new Color(0.9f, 0.8f, 0.2f));
        }

        // ---- Deko (nur Optik, keine Collider - stoert NavMesh/Gameplay nicht) ----

        static Transform _decoRoot;

        static void Deco(string name, PrimitiveType shape, Vector3 pos, Vector3 scale, Color c,
                         Quaternion rot = default)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(_decoRoot, true);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = rot == default ? Quaternion.identity : rot;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.GetComponent<Renderer>().sharedMaterial = MapMat(c);
        }

        /// <summary>
        /// Setzt ein echtes Deko-Modell (Resources/Models/&lt;key&gt;), wenn es das
        /// gibt. Rueckgabe true = Modell gesetzt, false = der Aufrufer baut seine
        /// Grundkoerper. So bleibt die alte Geometrie als Rueckfall erhalten.
        /// </summary>
        static bool DecoModel(string key, Vector3 pos, Quaternion rot, float scale = 1f)
            => Infront.AssetLibrary.SpawnModel(key, _decoRoot, pos, rot, scale) != null;

        static readonly System.Random _decoRnd = new System.Random(4242);
        static Quaternion RandomYaw() => Quaternion.Euler(0f, (float)_decoRnd.NextDouble() * 360f, 0f);

        /// <summary>Rohr an der Aussenwand: echtes Rohr-Modell (mit
        /// <paramref name="modelRot"/>), sonst der lange Deko-Zylinder.</summary>
        static void Pipe(string name, Vector3 pos, Quaternion cylinderRot, Quaternion modelRot)
        {
            if (DecoModel("rohre", pos, modelRot, 2f)) return;
            Deco(name, PrimitiveType.Cylinder, pos,
                new Vector3(0.18f, 30f, 0.18f), new Color(0.2f, 0.21f, 0.23f), cylinderRot);
        }

        static void Barrel(float x, float z)
        {
            if (DecoModel("fass", new Vector3(x, 0f, z), RandomYaw())) return;

            Deco("Fass", PrimitiveType.Cylinder, new Vector3(x, 0.55f, z),
                new Vector3(0.62f, 0.55f, 0.62f), new Color(0.16f, 0.17f, 0.19f));
            Deco("Fass_Band", PrimitiveType.Cylinder, new Vector3(x, 0.7f, z),
                new Vector3(0.66f, 0.06f, 0.66f), new Color(0.85f, 0.42f, 0.12f));
        }

        static void Lamp(float x, float z, float y = 4.4f)
        {
            // Echte Haengelampe (Pivot oben an der Decke): am Modell haengt kein
            // Licht - das leuchtende Kaestchen darunter bleibt fuer den Bloom.
            //
            // Erste Wahl ist die Kaefiglampe (0,75 m hoch, Pivot oben, haengt
            // also von selbst nach unten). Pivot auf y+0.65 gesetzt, damit die
            // Unterkante bei rund y-0.10 sitzt - genau dort, wo das leuchtende
            // Kaestchen steht. Faellt sie aus, bleibt die alte Haengelampe,
            // und danach die beiden Wuerfel wie bisher.
            bool real = DecoModel("caged_hanging_light", new Vector3(x, y + 0.65f, z), RandomYaw())
                     || DecoModel("haengelampe", new Vector3(x, y + 0.5f, z), Quaternion.identity);

            if (!real)
            {
                Deco("Lampe_Buegel", PrimitiveType.Cube, new Vector3(x, y + 0.3f, z),
                    new Vector3(0.1f, 0.6f, 0.1f), new Color(0.1f, 0.11f, 0.12f));
                Deco("Lampe_Schirm", PrimitiveType.Cube, new Vector3(x, y, z),
                    new Vector3(1.1f, 0.18f, 0.5f), new Color(0.1f, 0.11f, 0.12f));
            }

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulb.name = "Lampe_Licht";
            bulb.transform.SetParent(_decoRoot, true);
            bulb.transform.position = new Vector3(x, y - 0.12f, z);
            bulb.transform.localScale = new Vector3(0.9f, 0.06f, 0.35f);
            var bc = bulb.GetComponent<Collider>();
            if (bc != null) Object.DestroyImmediate(bc);
            bulb.GetComponent<Renderer>().sharedMaterial = GlowMat(new Color(1f, 0.85f, 0.55f));
        }

        static void BuildDecoration()
        {
            _decoRoot = new GameObject("Deko").transform;
            _decoRoot.SetParent(_mapRoot, true);

            // Faesser in den Ecken und an Deckungen
            float[,] barrels =
            {
                { -26f, -24f }, { 26f, -24f }, { -26f, 24f }, { 26f, 24f },
                { -5f, 12f }, { 6f, 11f }, { -21f, 6f }, { 22f, 7f },
                { -13f, -6f }, { 14f, -5f },
            };
            for (int i = 0; i < barrels.GetLength(0); i++)
                Barrel(barrels[i, 0], barrels[i, 1]);

            // Haengelampen ueber den drei Bahnen
            for (int seg = -1; seg <= 1; seg++)
            {
                Lamp(-19f, seg * 12f);
                Lamp(0f, seg * 12f, 5.2f);
                Lamp(19f, seg * 12f);
            }

            // Rohrleitungen oben an den Aussenwaenden. Echtes Rohr-Segment,
            // sonst der lange Zylinder wie bisher.
            Pipe("Rohr_N", new Vector3(0f, 3.4f, 29f), Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 90f, 0f));
            Pipe("Rohr_S", new Vector3(0f, 3.4f, -29f), Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 90f, 0f));
            Pipe("Rohr_E", new Vector3(29f, 3.0f, 0f), Quaternion.Euler(90f, 0f, 0f), Quaternion.identity);
            Pipe("Rohr_W", new Vector3(-29f, 3.0f, 0f), Quaternion.Euler(90f, 0f, 0f), Quaternion.identity);

            // Sandsack-Reihen vor beiden Spawns (echter Zementsack, sonst Cube)
            for (int s = -1; s <= 1; s += 2)
            for (int i = -2; i <= 2; i++)
            {
                var p = new Vector3(i * 0.8f, 0f, s * 26f);
                if (DecoModel("sandsack", p, RandomYaw())) continue;
                Deco("Sandsack", PrimitiveType.Cube,
                    new Vector3(i * 0.8f, 0.3f, s * 26f), new Vector3(0.7f, 0.5f, 0.5f),
                    new Color(0.32f, 0.3f, 0.24f));
            }

            // Munitions- und Holzkisten als zusaetzliche Deckungs-Deko an den Bahnen
            // (nur wenn Modelle da sind - sonst nichts, kein Cube-Ersatz noetig).
            DecoModel("muni_kiste", new Vector3(-16f, 0f, -3f), RandomYaw());
            DecoModel("muni_kiste", new Vector3(16f, 0f, 3f), RandomYaw());
            DecoModel("holz_kiste", new Vector3(-7f, 0f, -14f), RandomYaw());
            DecoModel("holz_kiste", new Vector3(7f, 0f, 14f), RandomYaw());
            DecoModel("kanister", new Vector3(-25f, 0f, 2f), RandomYaw());
            DecoModel("kanister", new Vector3(25f, 0f, -2f), RandomYaw());

            // dunkle Boden-Flecken (Grunge)
            var rnd = new System.Random(1234);
            for (int i = 0; i < 14; i++)
            {
                float gx = (float)(rnd.NextDouble() * 48f - 24f);
                float gz = (float)(rnd.NextDouble() * 44f - 22f);
                Deco("Fleck", PrimitiveType.Quad, new Vector3(gx, 0.02f, gz),
                    new Vector3(3f + (float)rnd.NextDouble() * 4f, 3f + (float)rnd.NextDouble() * 4f, 1f),
                    new Color(0.05f, 0.055f, 0.065f), Quaternion.Euler(90f, 0f, 0f));
            }

            // Masten in zwei Ecken
            Deco("Mast_A", PrimitiveType.Cylinder, new Vector3(-27f, 4f, -27f),
                new Vector3(0.14f, 4f, 0.14f), new Color(0.12f, 0.13f, 0.14f));
            Deco("Mast_B", PrimitiveType.Cylinder, new Vector3(27f, 4f, 27f),
                new Vector3(0.14f, 4f, 0.14f), new Color(0.12f, 0.13f, 0.14f));
        }

        /// <summary>
        /// Alte kleine Karte (60x60 m, drei gerade Bahnen). Bleibt als
        /// Rückfallebene erhalten - <see cref="BuildMap"/> ruft jetzt die grosse
        /// Karte "Werk". Zum Zurückschalten in <see cref="BuildArenaScene"/>
        /// einfach wieder BuildMapKlein() aufrufen.
        /// </summary>
        static void BuildMapKlein()
        {
            _mats.Clear();
            _glowMats.Clear();
            _texMats.Clear();
            _mapRoot = new GameObject("Map").transform;

            // Aussenwaende (Box bei +/-30)
            Block("Wall_N", 0f, 2f, 30f, 62f, 4f, 2f);
            Block("Wall_S", 0f, 2f, -30f, 62f, 4f, 2f);
            Block("Wall_E", 30f, 2f, 0f, 2f, 4f, 62f);
            Block("Wall_W", -30f, 2f, 0f, 2f, 4f, 62f);

            // Bahn-Trenner: zwei lange Waende bei x = -9 und x = +9 mit Luecken
            for (int seg = 0; seg < 3; seg++)
            {
                float cz = -16f + seg * 16f;
                Block($"LaneWall_L{seg}", -9f, 2f, cz, 1.2f, 4f, 10f);
                Block($"LaneWall_R{seg}", 9f, 2f, cz, 1.2f, 4f, 10f);
                // leuchtende Kante oben auf den Trennwaenden
                Stripe($"LaneGlow_L{seg}", -9f, 4.05f, cz, 1.3f, 0.12f, 10f);
                Stripe($"LaneGlow_R{seg}", 9f, 4.05f, cz, 1.3f, 0.12f, 10f);
            }
            // Licht in den Luecken zwischen den Trennwand-Segmenten
            PointLightAt("LaneGapLight_L", new Vector3(-9f, 2.6f, -8f), new Color(1f, 0.55f, 0.25f), 14f, 6f);
            PointLightAt("LaneGapLight_R", new Vector3(9f, 2.6f, 8f), new Color(1f, 0.55f, 0.25f), 14f, 6f);

            // Sichtschutz direkt vor beiden Spawns
            BlockM("SpawnScreen_mid", 0f, 1.5f, 22f, 10f, 3f, 1f);
            BlockM("SpawnScreen_l", -19f, 1.5f, 22f, 8f, 3f, 1f);
            BlockM("SpawnScreen_r", 19f, 1.5f, 22f, 8f, 3f, 1f);
            // Team-Kante auf dem Sichtschutz: blau bei Alpha (-Z), rot bei Bravo (+Z)
            Stripe("SpawnEdge_midB", 0f, 3.05f, 22f, 10f, 0.14f, 1.1f);
            Stripe("SpawnEdge_midA", 0f, 3.05f, -22f, 10f, 0.14f, 1.1f);

            // Deckung in der Mitte: genug zum Ueberqueren, Sichtachse bleibt lang
            CrateM("MidCrate1", 0f, 0.8f, 14f, 2.5f, 1.6f, 2f);
            CrateM("MidCrate2", -4f, 0.7f, 9f, 2f, 1.4f, 1.8f);
            CrateM("MidCrate3", 4f, 0.7f, 9f, 2f, 1.4f, 1.8f);
            CrateM("MidLow1", -2f, 0.5f, 4f, 3f, 1f, 1.2f);
            CrateM("MidLow2", 5f, 0.5f, 2f, 1.2f, 1f, 3f);
            Block("MidPillar", 0f, 2f, 0f, 1.5f, 4f, 1.5f);  // Saeule genau in der Mitte
            Stripe("MidPillarGlow", 0f, 4.1f, 0f, 1.6f, 0.16f, 1.6f);
            PointLightAt("MidLight", new Vector3(0f, 5f, 0f), new Color(1f, 0.6f, 0.3f), 22f, 10f);

            // Seitenbahnen: mehr Deckung, engere Kaempfe
            CrateM("LeftCrateA", -20f, 1f, 14f, 3f, 2f, 3f);
            CrateM("LeftCrateB", -15f, 0.9f, 8f, 2f, 1.8f, 2f);
            CrateM("RightCrateA", 20f, 1f, 14f, 3f, 2f, 3f);
            CrateM("RightCrateB", 15f, 0.9f, 8f, 2f, 1.8f, 2f);

            // Zwei erhoehte Platz-Bereiche, auf Z=0, damit beide Teams gleich
            // weit haben. Ueber Rampen erreichbar.
            BuildSite("Site_Links", -19f);
            BuildSite("Site_Rechts", 19f);

            // Bomben-Zonen (Bomben-Modus): A links, B rechts.
            MakeBombSite("BombZone_A", 0, -19f);
            MakeBombSite("BombZone_B", 1, 19f);
            SiteLetter(-19f, 0f, 'A', new Color(1f, 0.75f, 0.15f));
            SiteLetter(19f, 0f, 'B', new Color(0.35f, 0.75f, 1f));
            PointLightAt("SiteLight_A", new Vector3(-19f, 4.5f, 0f), new Color(1f, 0.8f, 0.4f), 20f, 8f);
            PointLightAt("SiteLight_B", new Vector3(19f, 4.5f, 0f), new Color(0.6f, 0.8f, 1f), 20f, 8f);

            BuildDecoration();
        }

        // ==================================================================
        //  Karte "Werk" - grosse Karte (~90x90 m), fünf Wege je Seite,
        //  Balkone als Hochpunkte. Spiegelsymmetrisch in Z:
        //  Alpha-Spawn bei -Z, Bravo-Spawn bei +Z, beide Bombenplätze auf
        //  z=0 (A links bei x=-20, B rechts bei x=+20) - gleiche Logik wie
        //  die kleine Karte, damit Rundenablauf, Bots und Bomben
        //  unverändert funktionieren.
        //
        //  Wege je Seite:
        //    Halle  (x~0)      hohe Wände, Container, umkämpftes Mittelpodest
        //    Tunnel L/R (x~15) eng, dunkel, rotes Notlicht, viele Deckungen
        //    Lange  L/R (x~36) offener Aussenweg, endet als Balkon über dem Platz
        // ==================================================================

        const float WerkHalf = 45f;   // Aussenwand bei +/- 45

        // ---- Deckung in drei Klassen (fy = Fussboden-Höhe, meist 0) ----
        //   Hoch   (~1.9 m): komplett gedeckt, blockt Sicht
        //   Mittel (~1.2 m): im Hocken gedeckt, im Stehen rauslehnen
        //   Niedrig(~0.7 m): nur Beinschutz, drüber schiessen
        static void CoverHigh(string n, float x, float fy, float z, float sx, float sz)
            => Crate(n, x, fy + 0.95f, z, sx, 1.9f, sz);

        /// <summary>Mittlere Deckung. Wenn es die CC0-Betonbarrieren gibt, steht
        /// hier ein echtes Modell (verwitterte Jersey-Barriere) - der Kollider
        /// darunter bleibt in Deckungsgroesse, Balance und NavMesh unveraendert.
        /// Ohne die Modelle: der texturierte Wuerfel wie bisher.</summary>
        static void CoverMid(string n, float x, float fy, float z, float sx, float sz)
        {
            string key = (Mathf.Abs(n.GetHashCode()) % 2) == 0
                ? "concrete_road_barrier" : "concrete_road_barrier_02";
            var prefab = Infront.AssetLibrary.Model(key);
            if (prefab == null) { Crate(n, x, fy + 0.60f, z, sx, 1.2f, sz); return; }

            // Unsichtbarer Deckungs-Kollider (Spiel-Logik).
            var col = GameObject.CreatePrimitive(PrimitiveType.Cube);
            col.name = n;
            col.transform.SetParent(_mapRoot, true);
            col.transform.position = new Vector3(x, fy + 0.55f, z);
            col.transform.localScale = new Vector3(Mathf.Max(sx, 1.8f), 1.1f, Mathf.Max(sz, 0.9f));
            col.GetComponent<Renderer>().enabled = false;

            // Sichtbares Modell davor. Lange Achse an die groessere Kante drehen.
            bool alongX = sx >= sz;
            var yaw = alongX ? 0f : 90f;
            var m = Infront.AssetLibrary.SpawnModel(key, _mapRoot,
                new Vector3(x, fy, z), Quaternion.Euler(0f, yaw + RandomYawSmall(n), 0f), BarrierScale);
            if (m != null) m.name = n + "_modell";
        }

        static void CoverLow(string n, float x, float fy, float z, float sx, float sz)
            => Crate(n, x, fy + 0.35f, z, sx, 0.7f, sz);

        const float BarrierScale = 1.35f;   // Screenshot-Test: bei 1.0 wirkten die Barrieren zu klein
        static float RandomYawSmall(string seed)
            => (Mathf.Abs(seed.GetHashCode()) % 13 - 6);   // +/- 6 Grad, damit nicht alles exakt gleich steht

        static void CoverHighM(string n, float x, float z, float sx, float sz)
        { CoverHigh(n + "_B", x, 0f, z, sx, sz); CoverHigh(n + "_A", x, 0f, -z, sx, sz); }
        static void CoverMidM(string n, float x, float z, float sx, float sz)
        { CoverMid(n + "_B", x, 0f, z, sx, sz); CoverMid(n + "_A", x, 0f, -z, sx, sz); }
        static void CoverLowM(string n, float x, float z, float sx, float sz)
        { CoverLow(n + "_B", x, 0f, z, sx, sz); CoverLow(n + "_A", x, 0f, -z, sx, sz); }

        /// <summary>Begehbare erhöhte Fläche (Balkon / Podest). top = Oberkante.
        /// Matt und dunkel gehalten (Screenshot-Test: helle glaenzende Podeste
        /// unter dem Platzlicht sind komplett ausgebrannt).</summary>
        static void Platform(string n, float x, float top, float z, float sx, float sz)
            // Screenshot-Test 2026-09-04: die Podeste waren pechschwarz, obwohl
            // ringsum alles normal beleuchtet war. Zwei Ursachen zusammen:
            //  - die Abtoenung (fallbackTint dunkler als 0.8 -> 0.72 Grau) UND
            //  - smoothness 0.05, also praktisch kein Glanz.
            // Die Rampen daneben sehen richtig aus und benutzen genau das
            // nicht. Deshalb hier derselbe helle Aufbau wie bei den Rampen.
            => Surfaced(n, x, top - 0.25f, z, sx, 0.5f, sz, "platte",
                        new Vector2(0.35f, 0.35f), new Color(0.82f, 0.82f, 0.84f),
                        metallic: 0f, smoothness: 0.32f);

        /// <summary>Hüfthohe Brüstung MIT Collider - hält Spieler und Bots oben
        /// auf dem Balkon, man schiesst aber darüber hinweg nach unten.</summary>
        static void Rail(string n, float x, float y, float z, float sx, float sz)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = n;
            b.transform.SetParent(_mapRoot, true);
            b.transform.position = new Vector3(x, y + 0.55f, z);
            b.transform.localScale = new Vector3(sx, 1.1f, sz);
            b.GetComponent<Renderer>().sharedMaterial = MapMat(new Color(0.09f, 0.10f, 0.12f));
        }

        /// <summary>Rampe entlang Z. dir=+1 steigt Richtung +Z, dir=-1 Richtung -Z.
        /// Steigung bleibt flach (unter 15°) damit das NavMesh sie sicher mitnimmt.</summary>
        static void SlopeZ(string n, float x, float zLow, float fy, float run, float rise, float width, int dir)
        {
            var r = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.name = n;
            r.transform.SetParent(_mapRoot, true);
            float ang = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            r.transform.rotation = Quaternion.Euler(-ang * dir, 0f, 0f);
            r.transform.position = new Vector3(x, fy + rise * 0.5f, zLow + dir * run * 0.5f);
            r.transform.localScale = new Vector3(width, 0.3f, Mathf.Sqrt(run * run + rise * rise));
            r.GetComponent<Renderer>().sharedMaterial =
                RoleMat("platte", new Vector2(0.4f, 0.4f), new Color(0.16f, 0.18f, 0.24f));
        }

        /// <summary>Punktlicht das flackert (Notlicht-Stimmung).</summary>
        static void FlickerLight(string name, Vector3 pos, Color c, float range, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_mapRoot, true);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            var f = go.AddComponent<LampFlicker>();
            var so = new SerializedObject(f);
            so.FindProperty("_baseIntensity").floatValue = intensity;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Schraeg von oben einfallender Lichtkegel (Spot). Der
        /// treibende Staub (<see cref="AtmosphereDust"/>) faengt ihn ein - so
        /// entsteht der sichtbare Lichtschacht ganz ohne Zusatz-Geometrie.
        /// Wirft keine Schatten (Kosten).</summary>
        static void ShaftLight(string name, Vector3 pos, Vector3 dirEuler, float range,
                               float angle, Color c, float intensity, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_mapRoot, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(dirEuler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = c;
            l.range = range;
            l.spotAngle = angle;
            l.innerSpotAngle = angle * 0.55f;
            l.intensity = intensity;
            // Schatten kosten - nur die wenigen Kegel bekommen sie, bei denen
            // man den Lichtfleck auf dem Boden wirklich sehen soll.
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        /// <summary>Sechs Lichtschaechte: zwei quer durch die Halle (wie durch
        /// hohe Fenster), einer je Bombenplatz, einer je Aussenweg.</summary>
        static void BuildLightShafts()
        {
            var warm = new Color(1f, 0.86f, 0.62f);
            var cool = new Color(0.72f, 0.82f, 1f);
            // Halle: von schraeg oben-seitlich herein
            ShaftLight("Shaft_Halle_1", new Vector3(-7f, 8.5f, 14f), new Vector3(58f, 62f, 0f), 20f, 34f, warm, 5.5f);
            ShaftLight("Shaft_Halle_2", new Vector3(7f, 8.5f, 30f), new Vector3(58f, -62f, 0f), 20f, 34f, warm, 5.5f);
            // Bombenplaetze
            ShaftLight("Shaft_SiteA", new Vector3(-20f, 8f, -3f), new Vector3(64f, 20f, 0f), 18f, 40f, warm, 5f);
            ShaftLight("Shaft_SiteB", new Vector3(20f, 8f, 3f), new Vector3(64f, -20f, 0f), 18f, 40f, cool, 5f);
            // Aussenwege
            ShaftLight("Shaft_LaneL", new Vector3(-36f, 9f, 8f), new Vector3(60f, 30f, 0f), 22f, 38f, cool, 4.5f);
            ShaftLight("Shaft_LaneR", new Vector3(36f, 9f, -8f), new Vector3(60f, -30f, 0f), 22f, 38f, cool, 4.5f);
        }

        static void BuildMap()
        {
            _mats.Clear();
            _glowMats.Clear();
            _texMats.Clear();
            _mapRoot = new GameObject("Map").transform;
            const float H = WerkHalf;

            // ---- Aussenschale (hohe Wände, oben offen wie bei der kleinen Karte) ----
            Block("Wall_N", 0f, 3.5f, H, H * 2f, 7f, 2f);
            Block("Wall_S", 0f, 3.5f, -H, H * 2f, 7f, 2f);
            Block("Wall_E", H, 3.5f, 0f, 2f, 7f, H * 2f);
            Block("Wall_W", -H, 3.5f, 0f, 2f, 7f, H * 2f);

            BuildWerkSpawns();
            BuildWerkHalle();
            BuildWerkTunnels();
            BuildWerkLanes();
            BuildWerkSites();
            BuildWerkDach();
            BuildWerkConnectors();
            BuildWerkLights();
            BuildLightShafts();
            BuildDecorationWerk();
            BuildDetailWerk();
        }

        /// <summary>
        /// Reflexionssonden fuer das Hallen-Innere.
        ///
        /// Vorher gab es KEINE einzige Sonde. 870 Renderer standen auf
        /// "Reflexionssonde benutzen", es war aber keine da - also fiel alles
        /// auf die Umgebungsreflexion zurueck, und die ist der Himmel aus dem
        /// HDRI. Jede glaenzende Flaeche in einer geschlossenen Halle hat also
        /// den Aussenhimmel gespiegelt, den sie gar nicht sehen kann. Deshalb
        /// sahen die Metallkisten falsch aus, und deshalb hat die neu
        /// verkabelte Glanzkarte zunaechst so wenig gebracht: Glanz ohne etwas
        /// zum Spiegeln ist nur ein heller Fleck.
        ///
        /// Neun Sonden im Raster von 30 m, mit Kasten-Projektion, damit die
        /// Spiegelung zur Raumkante passt statt aus dem Unendlichen zu kommen.
        ///
        /// Echtzeit statt gebacken, einmal beim Start: die Karte steht still,
        /// ein Durchlauf reicht. Das spart den langen Bake-Schritt und es
        /// liegen keine Cubemaps im Projekt. Schicht 7 (Character) bleibt
        /// aussen vor - sonst brennen sich die Bots, die zufaellig beim Start
        /// dastehen, als Geister in jede Spiegelung ein.
        /// </summary>
        static void BuildReflexionssonden()
        {
            var wurzel = new GameObject("Reflexionssonden").transform;
            wurzel.SetParent(_mapRoot, true);

            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
                var go = new GameObject($"Sonde_{ix + 1}_{iz + 1}");
                go.transform.SetParent(wurzel, true);
                go.transform.position = new Vector3(ix * 30f, 4.5f, iz * 30f);

                var s = go.AddComponent<ReflectionProbe>();
                s.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                s.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
                s.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                s.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
                s.resolution = 128;
                s.size = new Vector3(34f, 13f, 34f);   // ueberlappt die Nachbarn leicht
                s.boxProjection = true;
                s.importance = 1;
                s.intensity = 1f;
                s.nearClipPlane = 0.3f;
                s.farClipPlane = 140f;
                s.cullingMask = ~(1 << 7);             // ohne Figuren
                s.hdr = true;
            }
        }

        static void BuildWerkSpawns()
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float z = 40f * s;
                // Rückwand-Riegel mit Lücken vor den fünf Wegen
                Block($"Screen_mid_{s}", 0f, 1.9f, z, 10f, 3.8f, 1.2f);
                Block($"Screen_tunL_{s}", -15f, 1.9f, z, 8f, 3.8f, 1.2f);
                Block($"Screen_tunR_{s}", 15f, 1.9f, z, 8f, 3.8f, 1.2f);
                Block($"Screen_lngL_{s}", -36f, 1.9f, z, 10f, 3.8f, 1.2f);
                Block($"Screen_lngR_{s}", 36f, 1.9f, z, 10f, 3.8f, 1.2f);
                // Team-Kante (leuchtet)
                Stripe($"SpawnEdge_{s}", 0f, 3.9f, z, 10f, 0.16f, 1.3f);
                // etwas Deckung direkt vor dem Spawn
                CoverMid($"SpawnCov_a_{s}", -8f, 0f, z - 5f * s, 2f, 2f);
                CoverMid($"SpawnCov_b_{s}", 8f, 0f, z - 5f * s, 2f, 2f);
                CoverHigh($"SpawnCov_c_{s}", 0f, 0f, z - 4f * s, 3f, 1.4f);
            }
        }

        static void BuildWerkHalle()
        {
            // Trennwände Halle <-> Tunnel bei x = +/- 9, mit Durchgängen
            foreach (float cz in new[] { 9f, 25f })
            {
                BlockM("HalleWall_L", -9f, 3.5f, cz, 1.4f, 7f, 12f);
                BlockM("HalleWall_R", 9f, 3.5f, cz, 1.4f, 7f, 12f);
                StripeM("HalleWallGlow_L", -9f, 7.1f, cz, 1.5f, 0.12f, 12f);
                StripeM("HalleWallGlow_R", 9f, 7.1f, cz, 1.5f, 0.12f, 12f);
            }

            // Container-Stapel als hohe Deckung
            CoverHighM("HalleCont_1", -4f, 20f, 3f, 6f);
            CoverHighM("HalleCont_2", 4f, 28f, 5f, 2.4f);
            CoverHighM("HalleCont_3", -3f, 34f, 2.4f, 2.4f);
            CoverMidM("HalleCrate_1", 5f, 15f, 2f, 2f);
            CoverMidM("HalleCrate_2", -6f, 11f, 2f, 2f);
            CoverLowM("HalleLow_1", 0f, 18f, 3f, 1.2f);

            // Säulen
            BlockM("HallePil_A", -5f, 4f, 33f, 1.6f, 8f, 1.6f);
            BlockM("HallePil_B", 5f, 4f, 33f, 1.6f, 8f, 1.6f);

            // Mittelpodest auf z=0 - hohes High Ground, von beiden Seiten per Rampe
            Platform("MidDais", 0f, 1.2f, 0f, 14f, 10f);
            SlopeZ("MidRamp_B", 0f, 13f, 0f, 8f, 1.2f, 6f, -1);
            SlopeZ("MidRamp_A", 0f, -13f, 0f, 8f, 1.2f, 6f, +1);
            CoverHigh("MidTop_1", -3.5f, 1.2f, 2f, 1.8f, 1.8f);
            CoverHigh("MidTop_2", 3.5f, 1.2f, -2f, 1.8f, 1.8f);
            CoverLow("MidTop_3", 0f, 1.2f, 0f, 2.4f, 1f);
            Stripe("MidEdge_B", 0f, 1.35f, 5f, 14f, 0.06f, 0.4f);
            Stripe("MidEdge_A", 0f, 1.35f, -5f, 14f, 0.06f, 0.4f);
            PointLightAt("MidGlow", new Vector3(0f, 6.5f, 0f), new Color(1f, 0.6f, 0.3f), 26f, 8f, shadows: true);
        }

        static void BuildWerkTunnels()
        {
            foreach (int sgn in new[] { -1, 1 })
            {
                string side = sgn < 0 ? "L" : "R";
                float cx = 15.5f * sgn;

                // Aussenwand des Tunnels (Richtung Lange), Loch am Platz (z~0)
                foreach (float cz in new[] { 9f, 25f })
                    BlockM($"TunWall_{side}", 22f * sgn, 3.5f, cz, 1.2f, 7f, 12f);

                // Deckungen dicht an dicht (spiegelt sich über die _b-Einträge in Z)
                CoverHigh($"TunH1_{side}", cx - 2f, 0f, 30f, 2f, 2f);
                CoverHigh($"TunH1b_{side}", cx - 2f, 0f, -30f, 2f, 2f);
                CoverMid($"TunM1_{side}", cx + 3f, 0f, 20f, 2f, 3f);
                CoverMid($"TunM1b_{side}", cx + 3f, 0f, -20f, 2f, 3f);
                CoverMid($"TunM2_{side}", cx - 3f, 0f, 12f, 2.4f, 2f);
                CoverMid($"TunM2b_{side}", cx - 3f, 0f, -12f, 2.4f, 2f);
                CoverLow($"TunL1_{side}", cx, 0f, 16f, 3f, 1f);
                CoverLow($"TunL1b_{side}", cx, 0f, -16f, 3f, 1f);

                // rotes Notlicht - flackert
                FlickerLight($"TunLight_a_{side}", new Vector3(cx, 3.2f, 18f), new Color(1f, 0.35f, 0.2f), 13f, 6f);
                FlickerLight($"TunLight_b_{side}", new Vector3(cx, 3.2f, -18f), new Color(1f, 0.35f, 0.2f), 13f, 6f);
                PointLightAt($"TunLight_c_{side}", new Vector3(cx, 3.2f, 0f), new Color(1f, 0.5f, 0.3f), 15f, 7f);
            }
        }

        static void BuildWerkLanes()
        {
            foreach (int sgn in new[] { -1, 1 })
            {
                string side = sgn < 0 ? "L" : "R";
                float cx = 36f * sgn;

                // offener Aussenweg, lange Sichtachse, lockere Deckung
                CoverHigh($"LngH1_{side}", cx, 0f, 30f, 2.4f, 2.4f);
                CoverHigh($"LngH1b_{side}", cx, 0f, -30f, 2.4f, 2.4f);
                CoverHigh($"LngH2_{side}", cx - 5f * sgn, 0f, 18f, 2f, 4f);
                CoverHigh($"LngH2b_{side}", cx - 5f * sgn, 0f, -18f, 2f, 4f);
                CoverMid($"LngM1_{side}", cx + 4f * sgn, 0f, 24f, 3f, 2f);
                CoverMid($"LngM1b_{side}", cx + 4f * sgn, 0f, -24f, 3f, 2f);
                CoverLow($"LngL1_{side}", cx, 0f, 12f, 2f, 3f);
                CoverLow($"LngL1b_{side}", cx, 0f, -12f, 2f, 3f);

                // kaltes Aussenlicht (Kontrast zum warmen Innenlicht)
                PointLightAt($"LngSun_a_{side}", new Vector3(cx, 6f, 20f), new Color(0.55f, 0.72f, 1f), 22f, 6f);
                PointLightAt($"LngSun_b_{side}", new Vector3(cx, 6f, -20f), new Color(0.55f, 0.72f, 1f), 22f, 6f);

                // Balkon über dem Platz: Plattform bei y=2.6, Rampe von beiden Seiten hoch
                float bx = 30f * sgn;
                Platform($"Balc_{side}", bx, 2.6f, 0f, 10f, 12f);
                // Brüstung nur an der Aussenkante (Richtung Zaun). Die Innenkante
                // bleibt offen - von dort schiesst man auf den Platz hinunter.
                // Die Stirnseiten bleiben frei, dort münden die Rampen.
                Rail($"BalcRail_out_{side}", bx + 5f * sgn, 2.6f, 0f, 0.4f, 12f);
                SlopeZ($"BalcRamp_B_{side}", bx, 14f, 0f, 12f, 2.6f, 5f, -1);
                SlopeZ($"BalcRamp_A_{side}", bx, -14f, 0f, 12f, 2.6f, 5f, +1);
                CoverMid($"BalcCov_{side}", bx, 2.6f, 0f, 2f, 2f);
                Stripe($"BalcEdge_{side}", bx - 5f * sgn, 2.7f, 0f, 0.3f, 0.1f, 12f);
            }
        }

        // ------------------------------------------------------------------
        //  Dach
        // ------------------------------------------------------------------

        /// <summary>
        /// Das Dach ueber der ganzen Karte.
        ///
        /// Vorher war die Halle eine oben offene Kiste: sieben Meter hohe
        /// Waende und darueber direkt der Himmel. Von oben sah man in einen
        /// Schacht, im Spiel sah man ueber jede Wand hinweg ins Leere. Genau
        /// das liess die Karte nach Bauklotz aussehen und nicht nach Werk.
        ///
        /// Jetzt: Waende bis zwoelf Meter hoch, darueber Fachwerktraeger,
        /// Pfetten und ein Blechdach mit vier durchgehenden Lichtbaendern. Das
        /// Tageslicht kommt durch die Lichtbaender herein - deshalb bleibt es
        /// drinnen hell, obwohl die Sonne nicht mehr direkt hereinscheint.
        ///
        /// Wichtig: alles haengt unter einem eigenen Wurzelobjekt mit einem
        /// NavMeshModifier (ignoreFromBuild). Ohne das wuerde die Oberseite des
        /// Daches als begehbare Flaeche mitgebacken, und Bots koennten dort
        /// oben landen.
        /// </summary>
        static void BuildWerkDach()
        {
            const float H = WerkHalf;       // 45
            const float wandOben = 12.6f;   // Unterkante Traeger
            const float deckUnten = 14.0f;  // Unterkante Blech
            const float traeger = 13.3f;    // Hoehe der Hauptbinder

            // ---- Waende hochziehen, sonst klafft zwischen 7 m und Dach eine Luecke
            Block("Dach_WallUp_N", 0f, 9.9f, H, H * 2f, 5.8f, 2f);
            Block("Dach_WallUp_S", 0f, 9.9f, -H, H * 2f, 5.8f, 2f);
            Block("Dach_WallUp_E", H, 9.9f, 0f, 2f, 5.8f, H * 2f);
            Block("Dach_WallUp_W", -H, 9.9f, 0f, 2f, 5.8f, H * 2f);

            // ---- Hauptbinder quer (Nord-Sued-Abstand 12 m)
            foreach (float z in new[] { -36f, -24f, -12f, 0f, 12f, 24f, 36f })
            {
                // Untergurt, Steg, Obergurt - zusammen liest sich das als I-Traeger
                Surfaced($"Dach_Binder_U_{z}", 0f, traeger - 0.45f, z, H * 2f, 0.22f, 0.9f,
                         "deckung_metall", new Vector2(1.2f, 1.2f),
                         new Color(0.27f, 0.28f, 0.30f), metallic: 0.35f, smoothness: 0.3f);
                Surfaced($"Dach_Binder_S_{z}", 0f, traeger, z, H * 2f, 0.9f, 0.28f,
                         "deckung_metall", new Vector2(1.2f, 1.2f),
                         new Color(0.24f, 0.25f, 0.27f), metallic: 0.35f, smoothness: 0.3f);
                Surfaced($"Dach_Binder_O_{z}", 0f, traeger + 0.45f, z, H * 2f, 0.22f, 0.9f,
                         "deckung_metall", new Vector2(1.2f, 1.2f),
                         new Color(0.27f, 0.28f, 0.30f), metallic: 0.35f, smoothness: 0.3f);

                // Diagonale Streben - ohne sie ist es ein Balken, kein Fachwerk
                for (float x = -H + 6f; x < H; x += 12f)
                {
                    var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    d.name = $"Dach_Strebe_{z}_{x}";
                    d.transform.SetParent(_mapRoot, true);
                    d.transform.position = new Vector3(x, traeger, z);
                    d.transform.rotation = Quaternion.Euler(0f, 0f, 38f);
                    d.transform.localScale = new Vector3(0.18f, 1.5f, 0.24f);
                    var dc = d.GetComponent<Collider>();
                    if (dc != null) Object.DestroyImmediate(dc);
                    d.GetComponent<Renderer>().sharedMaterial =
                        MapMat(new Color(0.26f, 0.27f, 0.29f));
                }
            }

            // ---- Pfetten laengs (tragen das Blech)
            for (float x = -40f; x <= 40f; x += 8f)
            {
                Surfaced($"Dach_Pfette_{x}", x, deckUnten - 0.35f, 0f, 0.22f, 0.5f, H * 2f,
                         "deckung_metall", new Vector2(1.2f, 1.2f),
                         new Color(0.30f, 0.31f, 0.33f), metallic: 0.35f, smoothness: 0.28f);
            }

            // ---- Blechfelder, zwischen den Lichtbaendern
            // Lichtbaender liegen bei x = -30, -10, 10, 30 und sind 5 m breit.
            void Feld(string n, float cx, float breite)
            {
                Surfaced($"Dach_Blech_{n}", cx, deckUnten + 0.2f, 0f, breite, 0.4f, H * 2f,
                         "deckung_metall", new Vector2(0.35f, 0.35f),
                         new Color(0.20f, 0.21f, 0.23f), metallic: 0.3f, smoothness: 0.22f);
            }
            Feld("A", -38.75f, 12.5f);
            Feld("B", -20f, 15f);
            Feld("C", 0f, 15f);
            Feld("D", 20f, 15f);
            Feld("E", 38.75f, 12.5f);

            // ---- Lichtbaender: milchige Scheiben, von aussen beschienen
            var glasMat = GlowMat(new Color(0.82f, 0.88f, 1f), 1.35f);
            foreach (float lx in new[] { -30f, -10f, 10f, 30f })
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = $"Dach_Lichtband_{lx}";
                g.transform.SetParent(_mapRoot, true);
                g.transform.position = new Vector3(lx, deckUnten + 0.1f, 0f);
                g.transform.localScale = new Vector3(5f, 0.12f, H * 2f);
                g.GetComponent<Renderer>().sharedMaterial = glasMat;

                // Sprossen quer, sonst ist es eine leuchtende Platte
                for (float sz = -H + 4f; sz < H; sz += 8f)
                {
                    var sp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sp.name = $"Dach_Sprosse_{lx}_{sz}";
                    sp.transform.SetParent(_mapRoot, true);
                    sp.transform.position = new Vector3(lx, deckUnten - 0.02f, sz);
                    sp.transform.localScale = new Vector3(5.2f, 0.16f, 0.3f);
                    var spc = sp.GetComponent<Collider>();
                    if (spc != null) Object.DestroyImmediate(spc);
                    sp.GetComponent<Renderer>().sharedMaterial =
                        MapMat(new Color(0.18f, 0.19f, 0.20f));
                }

                // Das Tageslicht, das durch das Band faellt. Breit, ohne
                // Schatten - Schatten von so grossen Kegeln waeren zu teuer.
                // Drei Kegel je Band: einer allein liess die Enden der 90 m
                // langen Halle im Dunkeln.
                foreach (float lz in new[] { -28f, 0f, 28f })
                    ShaftLight($"Dach_Tageslicht_{lx}_{lz}",
                               new Vector3(lx, deckUnten - 0.6f, lz),
                               new Vector3(90f, 0f, 0f), 34f, 116f,
                               // Runter von 3,4: seit das Umgebungslicht den
                               // Innenraum traegt, brannten die Lichtflecken auf
                               // dem Boden zu reinem Weiss aus (gemessen 5,6 %
                               // der Bildflaeche bei 255). Ein Lichtfleck soll
                               // hell sein, aber noch Struktur zeigen.
                               new Color(0.78f, 0.85f, 1f), 2.3f);
                // Kein Schatten an den Lichtbaendern. Ein Versuch mit vier
                // weichen Schattenkegeln (34 m Reichweite, 116 Grad) sah zwar
                // gut aus, kostete aber gemessen: 60 FPS / min 57 wurden zu
                // 57,7 FPS / min 30. Die Lichtflecken sind das nicht wert -
                // die Ankerlichter an den Engstellen werfen weiter Schatten.
            }

            // ---- Hallenstrahler an den Bindern
            foreach (float hx in new[] { -30f, 0f, 30f })
            {
                foreach (float hz in new[] { -24f, 0f, 24f })
                {
                    var schirm = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    schirm.name = $"Dach_Strahler_{hx}_{hz}";
                    schirm.transform.SetParent(_mapRoot, true);
                    schirm.transform.position = new Vector3(hx, traeger - 1.3f, hz);
                    schirm.transform.localScale = new Vector3(1.1f, 0.22f, 1.1f);
                    var sc = schirm.GetComponent<Collider>();
                    if (sc != null) Object.DestroyImmediate(sc);
                    schirm.GetComponent<Renderer>().sharedMaterial =
                        GlowMat(new Color(1f, 0.93f, 0.78f), 2.2f);

                    // Aufhaengung
                    var stange = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stange.name = $"Dach_Strahlerstange_{hx}_{hz}";
                    stange.transform.SetParent(_mapRoot, true);
                    stange.transform.position = new Vector3(hx, traeger - 0.7f, hz);
                    stange.transform.localScale = new Vector3(0.08f, 0.6f, 0.08f);
                    var stc = stange.GetComponent<Collider>();
                    if (stc != null) Object.DestroyImmediate(stc);
                    stange.GetComponent<Renderer>().sharedMaterial =
                        MapMat(new Color(0.2f, 0.2f, 0.22f));
                }
            }

            // Nur die drei mittleren Strahler bekommen wirklich Licht - neun
            // Punktlichter waeren Verschwendung, drei reichen fuer die Tiefe.
            foreach (float hz in new[] { -24f, 0f, 24f })
                PointLightAt($"Dach_Strahlerlicht_{hz}", new Vector3(0f, traeger - 2f, hz),
                             new Color(1f, 0.88f, 0.72f), 36f, 4.2f);

            // ---- Alles aus dem NavMesh heraushalten
            var dachRoot = new GameObject("Dach");
            dachRoot.transform.SetParent(_mapRoot, false);
            var mod = dachRoot.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            mod.overrideArea = false;
            // NavMeshModifier wirkt nur mit AffectsAgentType/Children-Schalter.
            var soMod = new SerializedObject(mod);
            var kinder = soMod.FindProperty("m_ApplyToChildren");
            if (kinder != null) { kinder.boolValue = true; soMod.ApplyModifiedPropertiesWithoutUndo(); }

            var umzug = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < _mapRoot.childCount; i++)
            {
                var kind = _mapRoot.GetChild(i);
                if (kind != dachRoot.transform && kind.name.StartsWith("Dach_"))
                    umzug.Add(kind);
            }
            foreach (var t in umzug) t.SetParent(dachRoot.transform, true);
        }

        static void BuildWerkSites()
        {
            MakeBombSite("BombZone_A", 0, -20f);
            MakeBombSite("BombZone_B", 1, 20f);
            SiteLetter(-20f, 0f, 'A', new Color(1f, 0.75f, 0.15f));
            SiteLetter(20f, 0f, 'B', new Color(0.35f, 0.75f, 1f));
            PointLightAt("SiteLight_A", new Vector3(-20f, 5f, 0f), new Color(1f, 0.72f, 0.35f), 15f, 4.5f, shadows: true);
            PointLightAt("SiteLight_B", new Vector3(20f, 5f, 0f), new Color(0.55f, 0.75f, 1f), 15f, 4.5f, shadows: true);

            foreach (int sgn in new[] { -1, 1 })
            {
                string side = sgn < 0 ? "A" : "B";
                float cx = 20f * sgn;
                CoverHigh($"SiteH1_{side}", cx - 3f * sgn, 0f, 4f, 2f, 2f);
                CoverHigh($"SiteH1b_{side}", cx - 3f * sgn, 0f, -4f, 2f, 2f);
                CoverMid($"SiteM1_{side}", cx + 3f * sgn, 0f, 0f, 2.5f, 2.5f);
                CoverLow($"SiteL1_{side}", cx, 0f, 5f, 3f, 1f);
                CoverLow($"SiteL1b_{side}", cx, 0f, -5f, 3f, 1f);
                // Vorher hing hier ein Rahmen frei in der Luft - dafuer gibt
                // es in einem Werk kein Vorbild. Ein haengendes Hallenschild
                // schon: es zeigt den Platz genauso von weitem an und haengt
                // sichtbar an Stangen unter dem Dach.
                SiteHangingSign($"SiteSign_{side}", cx, 0f, side[0],
                                sgn < 0 ? new Color(1f, 0.75f, 0.15f)
                                        : new Color(0.35f, 0.75f, 1f));
            }
        }

        /// <summary>
        /// Haengendes Hallenschild ueber einem Bombenplatz: eine Blechtafel an
        /// zwei Stangen, mit dem Buchstaben darauf. Ersetzt den frueher frei
        /// schwebenden Rahmen - dieselbe Aufgabe (Platz von weitem erkennen),
        /// aber mit einem Vorbild in der Wirklichkeit.
        /// </summary>
        static void SiteHangingSign(string name, float x, float z, char letter, Color c)
        {
            const float tafelY = 6.4f;

            // Aufhaengung bis unter das Dach
            foreach (float dx in new[] { -1.6f, 1.6f })
            {
                var stange = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stange.name = $"{name}_Stange{dx}";
                stange.transform.SetParent(_mapRoot, true);
                stange.transform.position = new Vector3(x + dx, (tafelY + 12.6f) * 0.5f, z);
                stange.transform.localScale = new Vector3(0.07f, (12.6f - tafelY) * 0.5f, 0.07f);
                var col = stange.GetComponent<Collider>();
                if (col != null) Object.DestroyImmediate(col);
                stange.GetComponent<Renderer>().sharedMaterial =
                    MapMat(new Color(0.22f, 0.22f, 0.24f));
            }

            // Die Tafel selbst
            Surfaced($"{name}_Tafel", x, tafelY, z, 4.2f, 2.4f, 0.16f,
                     "deckung_metall", new Vector2(1.2f, 1.2f),
                     new Color(0.24f, 0.25f, 0.27f), metallic: 0.3f, smoothness: 0.3f);

            // Der Buchstabe darauf - aufgemalt, leicht leuchtend, damit er im
            // Halbdunkel der Halle noch lesbar ist. Eine Werksbeschilderung ist
            // oft hinterleuchtet, das ist also kein Arcade-Rest.
            var mat = GlowMat(c, 0.8f);
            // Auf BEIDE Seiten: ein Schild, das nur von einer Seite lesbar ist,
            // ist von der anderen eine schwarze Platte. Genau so sah es im
            // ersten Bild aus.
            void Bar(float bx, float by, float bw, float bh)
            {
                foreach (float dz in new[] { -0.11f, 0.11f })
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    b.name = $"{name}_Bar";
                    b.transform.SetParent(_mapRoot, true);
                    b.transform.position = new Vector3(x + bx, tafelY + by, z + dz);
                    b.transform.localScale = new Vector3(bw, bh, 0.06f);
                    var col = b.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);
                    b.GetComponent<Renderer>().sharedMaterial = mat;
                }
            }
            Bar(-0.55f, 0f, 0.22f, 1.5f);
            Bar(0.55f, 0f, 0.22f, 1.5f);
            Bar(0f, 0f, 1.3f, 0.22f);
            Bar(0f, 0.65f, 1.3f, 0.22f);
            if (letter != 'A') Bar(0f, -0.65f, 1.3f, 0.22f);
        }

        /// <summary>
        /// NICHT MEHR BENUTZT - ersetzt durch <see cref="SiteHangingSign"/>.
        /// Bleibt stehen, falls der schwebende Rahmen doch nochmal gebraucht
        /// wird; ein Aufruf genuegt.
        ///
        /// Markierungs-Rahmen ueber einem Bombenplatz: VIER duenne Leisten,
        /// keine Platte.
        ///
        /// Vorher stand hier ein einziger Stripe-Aufruf mit 8 mal 8 Metern.
        /// Stripe war urspruenglich der orange leuchtende Streifen; als er in
        /// der Optik-Runde auf dunkles Metall umgestellt wurde (richtig fuer
        /// schmale Kantenleisten), wurde daraus eine fast schwarze Vollplatte,
        /// die 4,4 m ueber jedem Bombenplatz schwebte und ihn von oben komplett
        /// verdeckte. Das waren die beruechtigten "schwarzen Flaechen".
        ///
        /// Jetzt bleibt der Rahmen ein Rahmen: man sieht den Platz darunter.
        /// </summary>
        static void SiteFrameOutline(string name, float x, float y, float z, float size)
        {
            float halb = size * 0.5f;
            float dicke = 0.35f;

            Stripe($"{name}_N", x, y, z + halb, size, 0.14f, dicke);
            Stripe($"{name}_S", x, y, z - halb, size, 0.14f, dicke);
            Stripe($"{name}_W", x - halb, y, z, dicke, 0.14f, size);
            Stripe($"{name}_E", x + halb, y, z, dicke, 0.14f, size);
        }

        static void BuildWerkConnectors()
        {
            // Quergänge auf z ~ +/- 17 (durch die Wand-Lücken) mit Deckung
            foreach (int sgn in new[] { -1, 1 })
            {
                float z = 17f * sgn;
                CoverMid($"Conn_iL_{sgn}", -12f, 0f, z, 3f, 2f);
                CoverMid($"Conn_iR_{sgn}", 12f, 0f, z, 3f, 2f);
                CoverHigh($"Conn_oL_{sgn}", -25f, 0f, z, 2f, 2f);
                CoverHigh($"Conn_oR_{sgn}", 25f, 0f, z, 2f, 2f);
            }
        }

        static void BuildWerkLights()
        {
            // warme Akzentlichter an den Knotenpunkten der Halle
            PointLightAt("HalleLight_1", new Vector3(0f, 6f, 20f), new Color(1f, 0.7f, 0.42f), 20f, 10f, shadows: true);
            PointLightAt("HalleLight_2", new Vector3(0f, 6f, -20f), new Color(1f, 0.7f, 0.42f), 20f, 10f, shadows: true);
            PointLightAt("HalleLight_3", new Vector3(0f, 7f, 34f), new Color(1f, 0.6f, 0.34f), 18f, 8f);
            PointLightAt("HalleLight_4", new Vector3(0f, 7f, -34f), new Color(1f, 0.6f, 0.34f), 18f, 8f);
        }

        /// <summary>
        /// Grosse Industrie-Modelle im Werk: Hallenkran, Rolltore, Wand- und
        /// Deckenleuchten. Alles **rein optisch** - kein Collider, kein Licht,
        /// keine NavMesh-Aenderung. Damit bleiben Sichtlinien, Wege und Balance
        /// exakt so, wie sie vorher waren.
        ///
        /// Fehlt ein Modell, passiert schlicht nichts: die Karte ist ohne diese
        /// Deko vollstaendig spielbar, deshalb gibt es hier keinen Wuerfel-Ersatz.
        ///
        /// Die Masze stammen aus AssetImporterTools.ReportDecoBounds:
        ///   overhead_crane              12.49 x 5.13 x 4.00, Mitte y -1.35 (Pivot oben)
        ///   rollershutter_door           3.08 x 2.40 x 0.30, Mitte x -1.00 (Pivot rechts)
        ///   security_light               0.32 x 0.53 x 0.42, Mitte z +0.18 (Pivot an der Wand)
        ///   mounted_fluorescent_lights   0.91 x 0.04 x 0.65, flach (Pivot in der Platte)
        /// </summary>
        static void BuildWerkModelle()
        {
            // Hallenkran quer ueber die Mittelachse.
            //
            // Screenshot-Test 2026-09-03: bei y 10.4 schwebte er frei im Himmel
            // und sah aus wie ein UFO - die Karte ist oben offen, es gibt keine
            // Decke, an der er haengen koennte. Ein echter Hallenkran liegt auf
            // den Wandkronen auf. Der Pivot sitzt oben, der Koerper reicht 3,9 m
            // nach unten; bei y 9.4 sitzt die Unterkante also auf rund 5,5 m -
            // genau auf Hoehe der Wandkronen.
            //
            // Nur EINER, in der Mitte. Der zweite bei z 30 stand ueber dem
            // offenen Spawn-Bereich und hatte gar nichts, worauf er liegen kann.
            DecoModel("overhead_crane", new Vector3(0f, 9.4f, 0f), Quaternion.identity);

            // Rolltore an den Innenseiten der Nord- und Suedwand (Wand bei z +/-44).
            // Der Pivot liegt am rechten Rand, deshalb der Versatz von +1.5 in x.
            foreach (int sgn in new[] { -1, 1 })
            {
                float z = 43.4f * sgn;
                float yaw = sgn > 0 ? 180f : 0f;       // Front zeigt zur Kartenmitte
                DecoModel("rollershutter_door", new Vector3(-10f + 1.5f, 0f, z), Quaternion.Euler(0f, yaw, 0f));
                DecoModel("rollershutter_door", new Vector3(10f + 1.5f, 0f, z), Quaternion.Euler(0f, yaw, 0f));
            }

            // Wandleuchten an den Engstellen der beiden Aussenbahnen.
            // Pivot sitzt an der Wand, der Koerper steht in +z - der Gierwinkel
            // dreht ihn also von der Wand weg in den Raum.
            foreach (int sgn in new[] { -1, 1 })
            {
                float x = 15.5f * sgn;
                float yaw = sgn > 0 ? 90f : 270f;
                for (int zi = -1; zi <= 1; zi++)
                    DecoModel("security_light", new Vector3(x, 3.8f, zi * 22f), Quaternion.Euler(0f, yaw, 0f));
            }

            // Flache Deckenleuchten laengs durch die Halle, versetzt zu den
            // Haengelampen (die stehen auf Vielfachen von 15).
            for (int zi = -2; zi <= 2; zi++)
                DecoModel("mounted_fluorescent_lights", new Vector3(0f, 6.3f, zi * 15f + 7.5f),
                    Quaternion.Euler(0f, 90f, 0f), 1.6f);
        }

        static void BuildDecorationWerk()
        {
            _decoRoot = new GameObject("Deko").transform;
            _decoRoot.SetParent(_mapRoot, true);

            float[,] barrels =
            {
                { -40f, -40f }, { 40f, -40f }, { -40f, 40f }, { 40f, 40f },
                { -15f, 22f }, { 16f, -22f }, { -30f, 8f }, { 30f, -8f },
                { -9f, -14f }, { 9f, 14f }, { -22f, 30f }, { 22f, -30f },
                { 0f, 37f }, { 0f, -37f }, { -37f, 0f }, { 37f, 0f },
            };
            for (int i = 0; i < barrels.GetLength(0); i++)
                Barrel(barrels[i, 0], barrels[i, 1]);

            // Hängelampen entlang Halle und Tunneln
            for (int zi = -2; zi <= 2; zi++)
            {
                Lamp(0f, zi * 15f, 6.5f);
                Lamp(-15.5f, zi * 15f, 4.6f);
                Lamp(15.5f, zi * 15f, 4.6f);
            }

            // Rohre an den Aussenwänden
            Pipe("Rohr_W", new Vector3(-44f, 4.2f, 0f), Quaternion.Euler(90f, 0f, 0f), Quaternion.identity);
            Pipe("Rohr_E", new Vector3(44f, 4.2f, 0f), Quaternion.Euler(90f, 0f, 0f), Quaternion.identity);
            Pipe("Rohr_N", new Vector3(0f, 5f, 44f), Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 90f, 0f));
            Pipe("Rohr_S", new Vector3(0f, 5f, -44f), Quaternion.Euler(0f, 0f, 90f), Quaternion.Euler(0f, 90f, 0f));

            // Sandsack-Reihen vor beiden Spawns
            for (int s = -1; s <= 1; s += 2)
            for (int i = -3; i <= 3; i++)
            {
                var p = new Vector3(i * 0.8f, 0f, s * 38f);
                if (DecoModel("sandsack", p, RandomYaw())) continue;
                Deco("Sandsack", PrimitiveType.Cube,
                    new Vector3(i * 0.8f, 0.3f, s * 38f), new Vector3(0.7f, 0.5f, 0.5f),
                    new Color(0.32f, 0.3f, 0.24f));
            }

            // dunkle Boden-Flecken (Grunge)
            var rnd = new System.Random(1234);
            for (int i = 0; i < 26; i++)
            {
                float gx = (float)(rnd.NextDouble() * 84f - 42f);
                float gz = (float)(rnd.NextDouble() * 84f - 42f);
                Deco("Fleck", PrimitiveType.Quad, new Vector3(gx, 0.02f, gz),
                    new Vector3(3f + (float)rnd.NextDouble() * 5f, 3f + (float)rnd.NextDouble() * 5f, 1f),
                    // Screenshot-Test: 0.05 war fast schwarz und sah aus wie Loecher.
                    // Jetzt dezenter Schmutz statt Abgrund.
                    new Color(0.22f, 0.22f, 0.23f), Quaternion.Euler(90f, 0f, 0f));
            }

            // Muni-/Holzkisten als Deko-Deckung (nur wenn Modelle vorhanden)
            DecoModel("muni_kiste", new Vector3(-15f, 0f, -6f), RandomYaw());
            DecoModel("muni_kiste", new Vector3(15f, 0f, 6f), RandomYaw());
            DecoModel("holz_kiste", new Vector3(-30f, 0f, -18f), RandomYaw());
            DecoModel("holz_kiste", new Vector3(30f, 0f, 18f), RandomYaw());

            // Masten in zwei Ecken
            Deco("Mast_A", PrimitiveType.Cylinder, new Vector3(-42f, 5f, -42f),
                new Vector3(0.16f, 5f, 0.16f), new Color(0.12f, 0.13f, 0.14f));
            Deco("Mast_B", PrimitiveType.Cylinder, new Vector3(42f, 5f, 42f),
                new Vector3(0.16f, 5f, 0.16f), new Color(0.12f, 0.13f, 0.14f));

            // Grosse Industrie-Modelle (Kran, Rolltore, Leuchten) - nur Optik.
            BuildWerkModelle();
        }

        // ---- P5: Karte entklotzen (nur Deko, keine Collider) ----

        // Screenshot-Test 2026-09-04: die P5-Deko war fast schwarz und damit
        // unsichtbar. Deutlich heller, damit Fensterrahmen, Gelaender, Saeulen-
        // koepfe usw. wirklich zu sehen sind.
        static readonly Color _steel = new Color(0.20f, 0.21f, 0.23f);
        static readonly Color _concrete = new Color(0.40f, 0.40f, 0.42f);

        /// <summary>Fensterrahmen aus vier duennen Balken (+ Kreuzsprosse) an
        /// einer Wand. <paramref name="faceZ"/> ist die Wandnormale (+1/-1 = Z,
        /// sonst X).</summary>
        static void WindowFrame(string name, Vector3 center, float w, float h, bool alongZ)
        {
            float t = 0.08f;
            Vector3 sx = alongZ ? new Vector3(w, t, t) : new Vector3(t, t, w);
            Vector3 sy = new Vector3(t, h, t);
            Deco(name + "_top", PrimitiveType.Cube, center + Vector3.up * h * 0.5f, sx, _steel);
            Deco(name + "_bot", PrimitiveType.Cube, center - Vector3.up * h * 0.5f, sx, _steel);
            Vector3 side = alongZ ? new Vector3(0f, 0f, w * 0.5f) : new Vector3(w * 0.5f, 0f, 0f);
            Deco(name + "_l", PrimitiveType.Cube, center - side, sy, _steel);
            Deco(name + "_r", PrimitiveType.Cube, center + side, sy, _steel);
            Deco(name + "_bar", PrimitiveType.Cube, center, sx * 0.98f, _steel);
        }

        /// <summary>Truemmerhaufen: ein paar kleine gekippte Broecken.</summary>
        static void RubblePile(string name, Vector3 at, int seed)
        {
            var r = new System.Random(seed);
            int n = 4 + r.Next(4);
            for (int i = 0; i < n; i++)
            {
                float s = 0.25f + (float)r.NextDouble() * 0.7f;
                var p = at + new Vector3((float)(r.NextDouble() - 0.5) * 1.6f, s * 0.4f,
                                         (float)(r.NextDouble() - 0.5) * 1.6f);
                var rot = Quaternion.Euler((float)r.NextDouble() * 40f, (float)r.NextDouble() * 360f,
                                           (float)r.NextDouble() * 40f);
                Deco(name + i, PrimitiveType.Cube, p, new Vector3(s, s * 0.7f, s * 1.1f), _concrete, rot);
            }
        }

        /// <summary>Deko-Gelaender (kein Collider) an einer Kante entlang X oder Z.</summary>
        static void DecoRail(string name, Vector3 center, float length, bool alongX)
        {
            Vector3 barS = alongX ? new Vector3(length, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, length);
            Deco(name + "_top", PrimitiveType.Cube, center + Vector3.up * 0.55f, barS, _steel);
            Deco(name + "_mid", PrimitiveType.Cube, center + Vector3.up * 0.30f, barS, _steel);
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.5f));
            for (int i = 0; i <= posts; i++)
            {
                float f = (i / (float)posts - 0.5f) * length;
                Vector3 p = center + (alongX ? new Vector3(f, 0.3f, 0f) : new Vector3(0f, 0.3f, f));
                Deco(name + "_p" + i, PrimitiveType.Cube, p, new Vector3(0.06f, 0.6f, 0.06f), _steel);
            }
        }

        static void SandbagStack(string name, Vector3 at, int seed)
        {
            var r = new System.Random(seed);
            for (int row = 0; row < 3; row++)
            for (int c = -1; c <= 1; c++)
            {
                var p = at + new Vector3(c * 0.42f + (row % 2) * 0.2f, 0.18f + row * 0.28f, 0f);
                if (DecoModel("sandsack", p, RandomYaw())) continue;
                Deco(name + row + "_" + c, PrimitiveType.Cube, p,
                    new Vector3(0.55f, 0.28f, 0.42f), new Color(0.30f, 0.28f, 0.22f),
                    Quaternion.Euler(0f, (float)r.NextDouble() * 20f - 10f, 0f));
            }
        }

        static void BuildDetailWerk()
        {
            // Fensterrahmen hoch in den Hallen-Trennwaenden (x = +-9), dort wo
            // die Lichtschaechte hereinfallen.
            foreach (int sgn in new[] { -1, 1 })
            {
                float x = 9f * sgn;
                WindowFrame($"Fensterrahmen_H{sgn}_a", new Vector3(x, 5.4f, 12f), 2.6f, 2.2f, false);
                WindowFrame($"Fensterrahmen_H{sgn}_b", new Vector3(x, 5.4f, 28f), 2.6f, 2.2f, false);
            }
            // Fensterrahmen in den Aussenwaenden N/S
            for (int i = -1; i <= 1; i++)
            {
                WindowFrame($"Fensterrahmen_N{i}", new Vector3(i * 20f, 5.5f, 44.5f), 3f, 2.4f, true);
                WindowFrame($"Fensterrahmen_S{i}", new Vector3(i * 20f, 5.5f, -44.5f), 3f, 2.4f, true);
            }

            // Saeulenkoepfe und -sockel auf die vorhandenen Hallen-Saeulen
            foreach (int sgn in new[] { -1, 1 })
            {
                float x = 5f * sgn;
                Deco($"SaeuleKopf_{sgn}", PrimitiveType.Cube, new Vector3(x, 7.9f, 33f),
                    new Vector3(2.1f, 0.4f, 2.1f), _concrete);
                Deco($"SaeuleSockel_{sgn}", PrimitiveType.Cube, new Vector3(x, 0.25f, 33f),
                    new Vector3(2.2f, 0.5f, 2.2f), _concrete);
            }

            // Deko-Gelaender an der Mittelpodest-Kante und den Balkon-Innenkanten
            DecoRail("MidRail_B", new Vector3(0f, 1.35f, 5f), 13f, true);
            DecoRail("MidRail_A", new Vector3(0f, 1.35f, -5f), 13f, true);
            foreach (int sgn in new[] { -1, 1 })
                DecoRail($"BalcRailDeko_{sgn}", new Vector3(25f * sgn, 2.75f, 0f), 11f, false);

            // Truemmerhaufen an Wandfuessen und in den Ecken
            RubblePile("Truemmer_a", new Vector3(-42f, 0f, 20f), 11);
            RubblePile("Truemmer_b", new Vector3(42f, 0f, -20f), 22);
            RubblePile("Truemmer_c", new Vector3(-8f, 0f, -43f), 33);
            RubblePile("Truemmer_d", new Vector3(10f, 0f, 43f), 44);
            RubblePile("Truemmer_e", new Vector3(-20f, 0f, 8f), 55);
            RubblePile("Truemmer_f", new Vector3(20f, 0f, -8f), 66);

            // Extra Sandsackstellungen an beiden Bombenplaetzen
            SandbagStack("SackSite_A1", new Vector3(-17f, 0f, 6f), 101);
            SandbagStack("SackSite_A2", new Vector3(-23f, 0f, -6f), 102);
            SandbagStack("SackSite_B1", new Vector3(17f, 0f, -6f), 103);
            SandbagStack("SackSite_B2", new Vector3(23f, 0f, 6f), 104);

            // Kabelstraenge an den Wandoberkanten (leichter Durchhang: 2 Segmente)
            foreach (int sgn in new[] { -1, 1 })
            {
                float z = 43.5f * sgn;
                Deco($"Kabel_N{sgn}_a", PrimitiveType.Cube, new Vector3(-12f, 6.2f, z),
                    new Vector3(24f, 0.05f, 0.05f), _steel, Quaternion.Euler(0f, 0f, 3f));
                Deco($"Kabel_N{sgn}_b", PrimitiveType.Cube, new Vector3(12f, 6.2f, z),
                    new Vector3(24f, 0.05f, 0.05f), _steel, Quaternion.Euler(0f, 0f, -3f));
            }

            // Wandkaesten (Elektrik) an den Hallenwaenden
            foreach (int sgn in new[] { -1, 1 })
            {
                float x = 8.6f * sgn;
                Deco($"Wandkasten_{sgn}_a", PrimitiveType.Cube, new Vector3(x, 1.6f, 6f),
                    new Vector3(0.15f, 0.5f, 0.35f), _steel);
                Deco($"Wandkasten_{sgn}_b", PrimitiveType.Cube, new Vector3(x, 1.4f, 22f),
                    new Vector3(0.15f, 0.4f, 0.3f), _steel);
            }

            // Dachlatten quer ueber den Aussenwegen - brechen das einfallende Licht
            foreach (int sgn in new[] { -1, 1 })
            {
                float x = 36f * sgn;
                for (int i = -4; i <= 4; i++)
                    Deco($"Dachlatte_{sgn}_{i}", PrimitiveType.Cube, new Vector3(x, 7.6f, i * 4f),
                        new Vector3(8f, 0.12f, 0.18f), _steel);
            }
        }

        static void MakeBombSite(string name, int siteId, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_mapRoot, true);
            go.transform.position = new Vector3(x, 1.6f, 0f);

            var site = go.AddComponent<BombSite>();
            var so = new SerializedObject(site);
            so.FindProperty("_siteId").intValue = siteId;
            so.FindProperty("_halfExtents").vector3Value = new Vector3(6f, 2.5f, 7f);
            so.ApplyModifiedPropertiesWithoutUndo();

            // sichtbare Markierung auf dem Platzboden: nur ein Rahmen (kein
            // roter Teppich - Screenshot-Test: das dominierte alles).
            // Warngelb statt Rot: so sind Gefahrenbereiche in einer Halle
            // wirklich markiert, und es liest sich nicht als Spielfeldlinie.
            var edge = new Color(0.46f, 0.40f, 0.16f);
            Tinted(name + "_MarkN", x, 1.28f, 4.8f, 9f, 0.05f, 0.4f, edge);
            Tinted(name + "_MarkS", x, 1.28f, -4.8f, 9f, 0.05f, 0.4f, edge);
            Tinted(name + "_MarkE", x + 4.3f, 1.28f, 0f, 0.4f, 0.05f, 10f, edge);
            Tinted(name + "_MarkW", x - 4.3f, 1.28f, 0f, 0.4f, 0.05f, 10f, edge);
        }

        static void BuildSite(string name, float x)
        {
            float y = 1.2f;
            // Gleiche Ursache wie bei Platform(): dunkle Toenung plus
            // smoothness 0.05 machte die Bombenplatz-Podeste pechschwarz.
            Surfaced(name + "_Platform", x, y * 0.5f, 0f, 11f, y, 12f, "platte",
                     new Vector2(0.4f, 0.4f), new Color(0.82f, 0.82f, 0.84f),
                     metallic: 0f, smoothness: 0.32f);

            // Rampe von beiden Seiten (Alpha- und Bravo-Zugang)
            Ramp(name + "_RampB", x, 8f, 3f, y, 4f);
            Ramp(name + "_RampA", x, -8f, -3f, y, 4f);

            // etwas Deckung auf dem Platz
            Crate(name + "_Cover1", x - 3f, y + 0.7f, 2f, 1.5f, 1.4f, 1.5f);
            Crate(name + "_Cover2", x + 3f, y + 0.7f, -2f, 1.5f, 1.4f, 1.5f);
        }

        static void AddHitboxes(GameObject root, Health owner)
        {
            // Schritt 5: vier Zonen statt nur Kopf und Koerper.
            //
            // Der Torso wird schmaler als vorher (0.35 -> 0.26), weil Arme und
            // Beine jetzt eigene Flaechen daneben haben. Die aeussere Silhouette
            // bleibt dadurch ungefaehr gleich gross - wer vorher getroffen hat,
            // trifft weiter, nur zaehlt es jetzt je nach Stelle anders.

            var body = new GameObject("Hitbox_Body") { layer = 6 };
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.15f, 0f);
            var bc = body.AddComponent<CapsuleCollider>();
            bc.radius = 0.26f; bc.height = 0.95f; bc.direction = 1;
            body.AddComponent<Hitbox>().Configure(KoerperZone.Torso, owner);

            var head = new GameObject("Hitbox_Head") { layer = 6 };
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            head.AddComponent<SphereCollider>().radius = 0.22f;
            head.AddComponent<Hitbox>().Configure(KoerperZone.Kopf, owner);

            // Arme: links und rechts neben dem Torso.
            foreach (int seite in new[] { -1, 1 })
            {
                var arm = new GameObject(seite < 0 ? "Hitbox_ArmL" : "Hitbox_ArmR") { layer = 6 };
                arm.transform.SetParent(root.transform, false);
                arm.transform.localPosition = new Vector3(seite * 0.31f, 1.25f, 0f);
                var ac = arm.AddComponent<CapsuleCollider>();
                ac.radius = 0.11f; ac.height = 0.75f; ac.direction = 1;
                arm.AddComponent<Hitbox>().Configure(KoerperZone.Arm, owner);
            }

            // Beine: unterhalb des Torsos.
            foreach (int seite in new[] { -1, 1 })
            {
                var bein = new GameObject(seite < 0 ? "Hitbox_BeinL" : "Hitbox_BeinR") { layer = 6 };
                bein.transform.SetParent(root.transform, false);
                bein.transform.localPosition = new Vector3(seite * 0.13f, 0.42f, 0f);
                var lc = bein.AddComponent<CapsuleCollider>();
                lc.radius = 0.13f; lc.height = 0.85f; lc.direction = 1;
                bein.AddComponent<Hitbox>().Configure(KoerperZone.Bein, owner);
            }
        }

        static GameObject BuildBotPrefab(WeaponCatalog catalog, AbilityCatalog abilityCatalog, BotStats botStats)
        {
            var root = new GameObject("Bot");
            root.AddComponent<NetworkObject>();
            root.layer = 7; // Character - vom Trefferstrahl ausgenommen

            var health = root.AddComponent<Health>();
            root.AddComponent<TeamMember>();
            root.AddComponent<TeamTint>();
            root.AddComponent<CharacterVisual>();   // stilisierte Figur statt Kapsel

            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            netTransform.Interpolate = true;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.4f;
            agent.height = 1.8f;
            agent.baseOffset = 0f;
            agent.speed = botStats.MoveSpeed;
            agent.angularSpeed = 360f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.2f;
            agent.autoBraking = true;

            // Sichtbarer Koerper (nur Optik) - Trefferflaechen sind eigene Hitboxen
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);

            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(root.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(eyes.transform, false);
            muzzle.transform.localPosition = new Vector3(0.2f, -0.1f, 0.5f);

            var weaponComponent = root.AddComponent<NetworkWeapon>();
            root.AddComponent<TracerEffect>();
            root.AddComponent<MuzzleFlash>();      // Muendungsfeuer pro Schuss
            root.AddComponent<ShellEjector>();     // fliegende Patronenhuelsen
            root.AddComponent<FootstepSounds>();   // Schritt-Geraeusche nach Tempo
            root.AddComponent<Wallet>();
            root.AddComponent<BombAction>();
            root.AddComponent<AbilityHolder>();
            var purchaseAgent = root.AddComponent<PurchaseAgent>();
            root.AddComponent<BotBuyer>();
            root.AddComponent<BotLocomotion>();  // Gewicht wie beim Spieler
            var brain = root.AddComponent<BotBrain>();
            root.AddComponent<BotObjective>();   // Bomben-Modus: legen / bewachen / entschaerfen
            var lifecycle = root.AddComponent<BotLifecycle>();

            var soWeapon = new SerializedObject(weaponComponent);
            soWeapon.FindProperty("_catalog").objectReferenceValue = catalog;
            soWeapon.FindProperty("_defaultPrimary").intValue = 4;   // Bot-Sturmgewehr (nur fuer Tests)
            soWeapon.FindProperty("_defaultSecondary").intValue = 3; // Pistole
            soWeapon.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            soWeapon.FindProperty("_hitMask").intValue = (1 << 0) | (1 << 6);
            soWeapon.ApplyModifiedPropertiesWithoutUndo();

            var soPurchase = new SerializedObject(purchaseAgent);
            soPurchase.FindProperty("_catalog").objectReferenceValue = catalog;
            soPurchase.FindProperty("_abilityCatalog").objectReferenceValue = abilityCatalog;
            soPurchase.ApplyModifiedPropertiesWithoutUndo();

            var soBotAbility = new SerializedObject(root.GetComponent<AbilityHolder>());
            soBotAbility.FindProperty("_catalog").objectReferenceValue = abilityCatalog;
            soBotAbility.ApplyModifiedPropertiesWithoutUndo();

            var soBrain = new SerializedObject(brain);
            soBrain.FindProperty("_stats").objectReferenceValue = botStats;
            soBrain.FindProperty("_eyes").objectReferenceValue = eyes.transform;
            soBrain.FindProperty("_sightBlockers").intValue = 1 << 0;
            soBrain.ApplyModifiedPropertiesWithoutUndo();

            // Schritt 5: Blutungen und Zonenfolgen liegen neben der Health,
            // die selbst unveraendert bleibt.
            root.AddComponent<Bleeding>();
            AddHitboxes(root, health);

            var soHealth = new SerializedObject(health);
            soHealth.FindProperty("_maxHealth").intValue = 100;
            soHealth.ApplyModifiedPropertiesWithoutUndo();

            var soLife = new SerializedObject(lifecycle);
            var hide = soLife.FindProperty("_hideOnDeath");
            hide.arraySize = 1;
            hide.GetArrayElementAtIndex(0).objectReferenceValue = body;
            soLife.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BotPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || prefab == null)
            {
                Debug.LogError("[Infront] Bot-Prefab konnte nicht gespeichert werden.");
                return null;
            }

            AssetDatabase.ImportAsset(BotPrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BotPrefabPath);
            Debug.Log("[Infront] Bot-Prefab bereit.");
            return prefab;
        }

        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- 3D-Kulisse hinter dem Menü + langsame Kamerafahrt ----
            BuildMenuBackdrop();

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.043f, 0.051f, 0.059f);
            cam.allowHDR = true;
            cam.fieldOfView = 48f;
            camGo.transform.position = new Vector3(0f, 3.6f, -7f);
            camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            var menuCamData = camGo.AddComponent<UniversalAdditionalCameraData>();
            menuCamData.renderPostProcessing = true;
            menuCamData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();
            // Schritt 7: Ohrenklingeln haengt am Hoerer und daempft alles,
            // was nach einer nahen Explosion noch zu hoeren ist.
            camGo.AddComponent<EarRinging>();
            camGo.AddComponent<MenuCameraRig>();

            // PostFx im Kino-Look: Tiefenunschaerfe hinter dem Menue + dunklere Raender.
            var postFxGo = new GameObject("PostFx");
            var postFx = postFxGo.AddComponent<PostFxController>();
            var soPost = new SerializedObject(postFx);
            var menuLookProp = soPost.FindProperty("_menuLook");
            if (menuLookProp != null) menuLookProp.boolValue = true;
            soPost.ApplyModifiedPropertiesWithoutUndo();

            // Altes IMGUI-Menue bleibt als Rueckfallebene im Baum (F10 schaltet um).
            new GameObject("MainMenu").AddComponent<MainMenu>();

            // Neues Menue mit Unity UI Toolkit.
            var panel = BuildUiPanel();
            var uiGo = new GameObject("MenuUI");
            var doc = uiGo.AddComponent<UIDocument>();
            var soDoc = new SerializedObject(doc);
            var panelProp = soDoc.FindProperty("m_PanelSettings");
            if (panelProp != null) panelProp.objectReferenceValue = panel;
            var sortProp = soDoc.FindProperty("m_SortingOrder");
            if (sortProp != null) sortProp.floatValue = 0f;
            soDoc.ApplyModifiedPropertiesWithoutUndo();
            uiGo.AddComponent<MainMenuUi>();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log($"[Infront] Menue-Szene gespeichert: {saved} -> {MenuScenePath}");
        }

        /// <summary>
        /// Kleine atmosphärische 3D-Kulisse hinter dem Menü: ein Stück Halle mit
        /// Containern, Fässern, Hängelampen, warmem Licht, kreisendem Scheinwerfer
        /// und Nebel. Die Kamera (<see cref="MenuCameraRig"/>) schwenkt langsam
        /// darüber. Benutzt dieselben Bausteine wie die Karte.
        /// </summary>
        static void BuildMenuBackdrop()
        {
            _mats.Clear();
            _glowMats.Clear();
            _texMats.Clear();
            _mapRoot = new GameObject("Backdrop").transform;

            // Dunkler Boden
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
                g.name = "BackdropGround";
                g.transform.SetParent(_mapRoot, true);
                g.transform.localScale = new Vector3(6f, 1f, 6f);
                var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BackdropGround" };
                var gc = new Color(0.06f, 0.065f, 0.08f);
                gm.color = gc;
                if (gm.HasProperty("_BaseColor")) gm.SetColor("_BaseColor", gc);
                if (gm.HasProperty("_Smoothness")) gm.SetFloat("_Smoothness", 0.2f);
                g.GetComponent<Renderer>().sharedMaterial = gm;
            }

            // Rückwand + Säulen als Silhouette
            Block("BD_Wall", 0f, 4f, 20f, 40f, 8f, 2f);
            Block("BD_Pillar_L", -9f, 4f, 16f, 1.8f, 8f, 1.8f);
            Block("BD_Pillar_R", 9f, 4f, 15f, 1.8f, 8f, 1.8f);
            Stripe("BD_WallGlow", 0f, 7.4f, 19.2f, 40f, 0.14f, 0.3f);

            // Container-Stapel, versetzt in der Tiefe
            Crate("BD_Cont_1", -5f, 1.3f, 9f, 5.5f, 2.6f, 2.6f);
            Crate("BD_Cont_1b", -5f, 3.7f, 9f, 4.6f, 2.4f, 2.4f);
            Crate("BD_Cont_2", 5.5f, 1.3f, 11f, 2.6f, 2.6f, 6f);
            Crate("BD_Cont_3", 2f, 1.1f, 5f, 2.2f, 2.2f, 2.2f);
            Crate("BD_Cont_4", -8.5f, 1.1f, 4f, 2.2f, 2.2f, 3f);

            // Vordergrund-Rahmen (seitlich, damit die Mitte frei bleibt)
            Crate("BD_Fg_L", -7.5f, 0.7f, -2f, 2.4f, 1.4f, 2.4f);
            Crate("BD_Fg_R", 7.5f, 0.9f, -1f, 2.2f, 1.8f, 2.2f);

            _decoRoot = new GameObject("BackdropDeko").transform;
            _decoRoot.SetParent(_mapRoot, true);
            Barrel(-3.4f, 1.5f);
            Barrel(-2.6f, 1.9f);
            Barrel(6.5f, 3.5f);
            Barrel(-9f, -1f);
            Lamp(-3f, 8f, 6.2f);
            Lamp(4f, 12f, 6.4f);
            Deco("BD_Mast", PrimitiveType.Cylinder, new Vector3(-12f, 5f, 12f),
                new Vector3(0.16f, 5f, 0.16f), new Color(0.12f, 0.13f, 0.14f));

            // Vordergrund-Silhouetten dicht vor der Kamera: fast schwarz, ohne
            // Collider. Die Tiefenunschaerfe im PostFx laesst alles dahinter weich
            // verschwimmen - diese Rahmen bleiben die naechste, klarste Ebene und
            // geben dem Bild echte Tiefe. Absichtlich unter bzw. ueber der
            // schwenkenden Kamera platziert, damit sie nie hindurchfaehrt.
            var silCol = new Color(0.013f, 0.015f, 0.020f);
            Deco("BD_Sil_Rail",  PrimitiveType.Cube, new Vector3(0f,   0.55f, -2.4f), new Vector3(30f,  1.5f, 0.35f), silCol);
            Deco("BD_Sil_PostA", PrimitiveType.Cube, new Vector3(-4.2f, 1.1f, -2.4f), new Vector3(0.5f, 2.6f, 0.5f),  silCol);
            Deco("BD_Sil_PostB", PrimitiveType.Cube, new Vector3(3.6f,  1.0f, -2.3f), new Vector3(0.5f, 2.4f, 0.5f),  silCol);
            Deco("BD_Sil_PostC", PrimitiveType.Cube, new Vector3(-9.5f, 1.2f, -2.5f), new Vector3(0.55f, 3f,  0.55f), silCol);
            Deco("BD_Sil_Beam",  PrimitiveType.Cube, new Vector3(-1f,   8.6f, -1.6f), new Vector3(26f,  0.6f, 0.6f),  silCol);
            Deco("BD_Sil_Hang",  PrimitiveType.Cube, new Vector3(5f,    7.4f, -1.6f), new Vector3(0.4f, 2.6f, 0.4f),  silCol);

            // Warmes Innenlicht + ein rotes Flackerlicht für Stimmung
            PointLightAt("BD_Warm_1", new Vector3(-2f, 4.5f, 7f), new Color(1f, 0.68f, 0.4f), 20f, 12f);
            PointLightAt("BD_Warm_2", new Vector3(5f, 4.8f, 12f), new Color(1f, 0.6f, 0.34f), 18f, 9f);
            FlickerLight("BD_Red", new Vector3(-6f, 2.2f, 8f), new Color(1f, 0.32f, 0.2f), 12f, 6f);

            // Zwei kreisende Suchscheinwerfer hoch oben, gegenläufig.
            // P6: langsamer und schwächer - ruhiger, ernster.
            MenuSearchlight("BD_Searchlight_1", new Vector3(1f, 8.5f, 10f),
                new Color(0.78f, 0.82f, 0.9f), 7f, 8f);
            MenuSearchlight("BD_Searchlight_2", new Vector3(-6f, 8.8f, 14f),
                new Color(0.95f, 0.78f, 0.6f), -5f, 7f);

            // Treibender Staub im Lichtkegel - baut sein Partikelsystem selbst.
            var dustGo = new GameObject("BD_Dust");
            dustGo.transform.SetParent(_mapRoot, true);
            dustGo.transform.position = new Vector3(0f, 4.5f, 8f);
            dustGo.AddComponent<ParticleSystem>();
            dustGo.AddComponent<MenuDust>();

            // ---- Mehr Leben in der Kulisse ----

            // Drehende Radar-Antenne auf dem rechten Containerstapel
            MenuRadar("BD_Radar", new Vector3(5.5f, 2.7f, 11f));

            // Rot blinkendes Signallicht oben auf dem linken Stapel
            MenuBeacon("BD_Beacon", new Vector3(-5f, 5.3f, 9f), new Color(1f, 0.28f, 0.2f));

            // Neon-Schild an der Rückwand. P6: viel gedeckter, kaum noch Leuchten -
            // ein müder Rest-Schimmer statt eines poppigen Eisblau.
            Stripe("BD_Neon", -3.5f, 5.4f, 19.2f, 6f, 0.12f, 0.25f);
            var neonGo = _mapRoot.Find("BD_Neon");
            if (neonGo != null)
                neonGo.GetComponent<Renderer>().sharedMaterial = GlowMat(new Color(0.26f, 0.4f, 0.5f));
            FlickerLight("BD_NeonLight", new Vector3(-3.5f, 5.2f, 18f), new Color(0.32f, 0.44f, 0.55f), 7f, 1.6f);

            // Aufsteigender Dampf aus einer Ecke - zweites MenuDust, gröber eingestellt
            var steamGo = new GameObject("BD_Steam");
            steamGo.transform.SetParent(_mapRoot, true);
            steamGo.transform.position = new Vector3(-3.2f, 0.6f, 4.5f);
            steamGo.AddComponent<ParticleSystem>();
            var steam = steamGo.AddComponent<MenuDust>();
            var soSteam = new SerializedObject(steam);
            var sBox = soSteam.FindProperty("_boxSize");
            if (sBox != null) sBox.vector3Value = new Vector3(2.4f, 3.5f, 2.4f);
            var sCount = soSteam.FindProperty("_count");
            if (sCount != null) sCount.intValue = 46;
            var sTint = soSteam.FindProperty("_tint");
            if (sTint != null) sTint.colorValue = new Color(0.55f, 0.62f, 0.72f, 0.32f);
            soSteam.ApplyModifiedPropertiesWithoutUndo();

            // Sonne + dunkler prozeduraler Himmel
            var lightGo = new GameObject("BackdropSun");
            lightGo.transform.SetParent(_mapRoot, true);
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0.5f;
            sun.color = new Color(0.7f, 0.78f, 0.95f);
            sun.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(38f, 150f, 0f);

            string skyPath = SettingsDir + "/ArenaSky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                var sh = Shader.Find("Skybox/Procedural");
                if (sh != null)
                {
                    sky = new Material(sh) { name = "ArenaSky" };
                    if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.4f);
                    if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.35f);
                    if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.18f, 0.22f, 0.3f));
                    if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.17f, 0.17f, 0.18f));
                    AssetDatabase.CreateAsset(sky, skyPath);
                    AssetDatabase.SaveAssets();
                }
            }
            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                // Menue-Kulisse: darf stimmungsvoll dunkel bleiben, aber der
                // Bodenrueckwurf war praktisch aus - deshalb waren die
                // Unterseiten aller Objekte schwarze Loecher.
                RenderSettings.ambientSkyColor = new Color(0.16f, 0.18f, 0.23f);
                RenderSettings.ambientEquatorColor = new Color(0.12f, 0.13f, 0.15f);
                RenderSettings.ambientGroundColor = new Color(0.09f, 0.09f, 0.10f);
            }
        }

        /// <summary>
        /// Ein langsam kreisender Suchscheinwerfer für die Menü-Kulisse.
        /// Negatives <paramref name="degPerSec"/> dreht andersherum.
        /// </summary>
        static void MenuSearchlight(string name, Vector3 pos, Color color, float degPerSec, float intensity)
        {
            var spinGo = new GameObject(name);
            spinGo.transform.SetParent(_mapRoot, true);
            spinGo.transform.position = pos;
            var spin = spinGo.AddComponent<SlowSpin>();
            var soSpin = new SerializedObject(spin);
            var dps = soSpin.FindProperty("_degreesPerSecond");
            if (dps != null) dps.floatValue = degPerSec;
            soSpin.ApplyModifiedPropertiesWithoutUndo();

            var beamGo = new GameObject(name + "_Beam");
            beamGo.transform.SetParent(spinGo.transform, false);
            beamGo.transform.localRotation = Quaternion.Euler(55f, 0f, 0f);
            var beam = beamGo.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.color = color;
            beam.range = 26f;
            beam.spotAngle = 34f;
            beam.intensity = intensity;
            beam.shadows = LightShadows.None;
        }

        /// <summary>
        /// Kleine drehende Radar-Antenne für die Menü-Kulisse: fester Mast,
        /// darauf eine langsam kreisende Schüssel. Rein optisch.
        /// </summary>
        static void MenuRadar(string name, Vector3 basePos)
        {
            // Mast
            var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = name + "_Mast";
            mast.transform.SetParent(_mapRoot, true);
            mast.transform.position = basePos + new Vector3(0f, 1f, 0f);
            mast.transform.localScale = new Vector3(0.14f, 1f, 0.14f);
            var mc = mast.GetComponent<Collider>();
            if (mc != null) Object.DestroyImmediate(mc);
            mast.GetComponent<Renderer>().sharedMaterial = MapMat(new Color(0.12f, 0.13f, 0.15f));

            // Dreh-Knoten auf der Mastspitze
            var spinGo = new GameObject(name + "_Spin");
            spinGo.transform.SetParent(_mapRoot, true);
            spinGo.transform.position = basePos + new Vector3(0f, 2.05f, 0f);
            var spin = spinGo.AddComponent<SlowSpin>();
            var soSpin = new SerializedObject(spin);
            var dps = soSpin.FindProperty("_degreesPerSecond");
            if (dps != null) dps.floatValue = 26f;
            soSpin.ApplyModifiedPropertiesWithoutUndo();

            // Schüssel: leicht gekippte flache Platte
            var dish = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dish.name = name + "_Dish";
            dish.transform.SetParent(spinGo.transform, false);
            dish.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            dish.transform.localRotation = Quaternion.Euler(0f, 0f, 62f);
            dish.transform.localScale = new Vector3(1.3f, 0.12f, 0.9f);
            var dc = dish.GetComponent<Collider>();
            if (dc != null) Object.DestroyImmediate(dc);
            dish.GetComponent<Renderer>().sharedMaterial = MapMat(new Color(0.18f, 0.19f, 0.22f));

            // kleiner leuchtender Sender vorn an der Schüssel
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = name + "_Tip";
            tip.transform.SetParent(spinGo.transform, false);
            tip.transform.localPosition = new Vector3(1.15f, 0f, 0f);
            tip.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
            var tc = tip.GetComponent<Collider>();
            if (tc != null) Object.DestroyImmediate(tc);
            tip.GetComponent<Renderer>().sharedMaterial = GlowMat(new Color(0.4f, 0.8f, 1f));
        }

        /// <summary>
        /// Blinkendes Signallicht: leuchtender Würfel auf kurzem Pfosten plus ein
        /// flackerndes Punktlicht. Rein optisch.
        /// </summary>
        static void MenuBeacon(string name, Vector3 pos, Color color)
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = name + "_Post";
            post.transform.SetParent(_mapRoot, true);
            post.transform.position = pos + new Vector3(0f, -0.35f, 0f);
            post.transform.localScale = new Vector3(0.12f, 0.7f, 0.12f);
            var pc = post.GetComponent<Collider>();
            if (pc != null) Object.DestroyImmediate(pc);
            post.GetComponent<Renderer>().sharedMaterial = MapMat(new Color(0.12f, 0.13f, 0.15f));

            var bulb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bulb.name = name + "_Bulb";
            bulb.transform.SetParent(_mapRoot, true);
            bulb.transform.position = pos;
            bulb.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
            var bc = bulb.GetComponent<Collider>();
            if (bc != null) Object.DestroyImmediate(bc);
            bulb.GetComponent<Renderer>().sharedMaterial = GlowMat(color);

            FlickerLight(name + "_Light", pos, color, 10f, 5f);
        }

        /// <summary>
        /// Erzeugt (einmalig) das PanelSettings-Asset fuer die UI-Toolkit-Oberflaeche
        /// und das Standard-Laufzeit-Thema, das die Grund-Optik der Bedienelemente
        /// liefert. Liegt unter Resources, damit auch der Ladebildschirm
        /// (LoadingOverlay) es per Resources.Load findet.
        /// </summary>
        static PanelSettings BuildUiPanel()
        {
            Directory.CreateDirectory(UiResourcesDir);

            if (!File.Exists(UiThemePath))
            {
                File.WriteAllText(UiThemePath,
                    "/* Standard-Laufzeit-Thema von Unity - Grund-Optik der Bedienelemente. */\n" +
                    "@import url(\"unity-theme://default\");\n" +
                    "VisualElement {}\n");
            }
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(UiThemePath, ImportAssetOptions.ForceUpdate);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UiThemePath);
            if (theme == null)
                Debug.LogWarning("[Infront] Laufzeit-Thema nicht ladbar - Menue nutzt nur eigene Styles.");

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(UiPanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, UiPanelPath);
            }
            if (theme != null) panel.themeStyleSheet = theme;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = false;
            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            Debug.Log("[Infront] UI-Panel bereit: " + UiPanelPath);
            return panel;
        }

        static GameObject BuildMatchManagerPrefab()
        {
            var root = new GameObject("MatchManager");
            root.AddComponent<NetworkObject>();
            var match = root.AddComponent<MatchManager>();
            root.AddComponent<HighlightTracker>();   // erkennt Doppelkill / Ace / Clutch

            // Grössere Karte "Werk" -> etwas mehr Rundenzeit für die Rotationen.
            var soMatch = new SerializedObject(match);
            var roundDur = soMatch.FindProperty("_roundDuration");
            if (roundDur != null) roundDur.floatValue = 135f;
            soMatch.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, MatchManagerPrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            if (!ok || prefab == null)
            {
                Debug.LogError("[Infront] MatchManager-Prefab konnte nicht gespeichert werden.");
                return null;
            }
            AssetDatabase.ImportAsset(MatchManagerPrefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(MatchManagerPrefabPath);
        }

        static GameObject BuildBombPrefab()
        {
            var root = new GameObject("Bomb");
            root.AddComponent<NetworkObject>();
            root.AddComponent<Bomb>();
            root.AddComponent<BombExplosionFx>();   // Explosions-Optik (per RPC ausgeloest)

            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            netTransform.Interpolate = true;

            // Sichtbarer Koerper (nur Optik, kein Collider - Kugeln fliegen durch)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.4f, 0.3f, 0.5f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BombPrefabPath, out bool ok);
            Object.DestroyImmediate(root);
            if (!ok || prefab == null)
            {
                Debug.LogError("[Infront] Bomben-Prefab konnte nicht gespeichert werden.");
                return null;
            }
            AssetDatabase.ImportAsset(BombPrefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefabPath);
        }

        static void BuildArenaScene(GameObject playerPrefab, GameObject dummyPrefab, GameObject botPrefab, GameObject matchManagerPrefab, GameObject bombPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;   // leicht gedimmt - das Post-Processing hebt wieder an
            light.color = new Color(1f, 0.95f, 0.88f);
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;   // P2: Schatten deutlich lesbar
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // P3: echter HDRI-Himmel, sonst der dunkle prozedurale Himmel (wie bisher).
            {
                var hdriSky = Infront.AssetLibrary.Surface("himmel");   // Resources/Materials/himmel.mat
                bool isSkybox = hdriSky != null && hdriSky.shader != null
                                && hdriSky.shader.name.Contains("Skybox");

                if (isSkybox)
                {
                    RenderSettings.skybox = hdriSky;

                    // Licht kommt aus der HDRI - Umgebungslicht vom Himmel nehmen.
                    //
                    // Versucht und wieder verworfen (2026-09-04): auf Trilight
                    // umstellen, um den Bodenrueckwurf getrennt einstellen zu
                    // koennen. Klang richtig - die Unterseite des Laufstegs
                    // bekommt aus der HDRI naemlich gar nichts, weil deren
                    // untere Haelfte der dunkle Boden des Fotos ist.
                    // Gemessen war es dreimal schlechter: der Bildmittelwert
                    // fiel von 83 auf 30, der schwarze Anteil stieg von 14,5 %
                    // auf 42 %. Grund: bei Trilight zaehlt ambientIntensity
                    // nicht mehr, und drei Farbwerte bringen viel weniger Licht
                    // als die ganze HDRI mal 1,65.
                    // Die dunklen Unterseiten sind ein Fall fuer gebackenes
                    // Licht (Phase 3), nicht fuer diesen Schalter.
                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                    // P2: Umgebungslicht etwas runter, damit die neuen Schatten
                    // lesbar sind - aber NICHT so weit, dass Schattenseiten
                    // schwarz absaufen (Screenshot-Test 2026-09-04: 0.4 war zu
                    // dunkel). 0.62 haelt Kontrast und laesst die Ecken sehen.
                    // Seit die Halle ein Dach hat, kommt die Sonne nicht mehr
                    // direkt herein. Das Umgebungslicht traegt jetzt den
                    // Innenraum - bei 0,62 waren ganze Waende schwarz.
                    // Rundgang 2026-09-04 gemessen: bei 1,1 waren 27 % jedes
                    // Bildes praktisch schwarz und 51 % unter der Grenze, wo man
                    // noch etwas erkennt. Ganze Wandseiten, die von der Sonne
                    // wegzeigen, bekamen gar kein Licht mehr - im Suedgang war
                    // die eine Wand hell und die gegenueberliegende schwarz.
                    // Seit die Halle ein Dach hat, ist das Umgebungslicht die
                    // Hauptlichtquelle im Innenraum und muss entsprechend tragen.
                    // Wirkt nur im Skybox-Modus; bleibt gesetzt, falls jemand
                    // zurueckschaltet.
                    RenderSettings.ambientIntensity = 1.65f;
                    light.intensity = 0.85f;                  // Sonne wieder etwas hoeher
                    DynamicGI.UpdateEnvironment();
                }
                else
                {
                    string skyPath = SettingsDir + "/ArenaSky.mat";
                    var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
                    if (sky == null)
                    {
                        var sh = Shader.Find("Skybox/Procedural");
                        if (sh != null)
                        {
                            sky = new Material(sh) { name = "ArenaSky" };
                            AssetDatabase.CreateAsset(sky, skyPath);
                        }
                    }
                    if (sky != null)
                    {
                        if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.4f);
                        if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 0.35f);
                        if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.18f, 0.22f, 0.3f));
                        if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.17f, 0.17f, 0.18f));
                        EditorUtility.SetDirty(sky);
                        RenderSettings.skybox = sky;
                        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                        // P2: etwas dunkler fuer Schatten-Kontrast, aber nicht schwarz
                        // (Screenshot-Test 2026-09-04).
                        // Wie oben: der Innenraum lebt vom Umgebungslicht.
                        // Der Bodenwert ist der Rueckwurf des Betonbodens - der
                        // war mit 0,035 praktisch aus, obwohl Beton kraeftig
                        // zurueckwirft. Davon leben die Unterseiten der Kisten.
                        RenderSettings.ambientSkyColor = new Color(0.34f, 0.37f, 0.44f);
                        RenderSettings.ambientEquatorColor = new Color(0.26f, 0.27f, 0.30f);
                        RenderSettings.ambientGroundColor = new Color(0.17f, 0.17f, 0.18f);
                    }
                }
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);   // 100x100 m (Karte "Werk")
            // Boden: echter Asphalt, sonst dunkler kuehler Farbton (wie bisher).
            {
                var real = Infront.AssetLibrary.Surface("boden");
                if (real != null && real.HasProperty("_BaseMap") && real.GetTexture("_BaseMap") != null)
                {
                    var gm = new Material(real) { name = "GroundMat" };
                    // Plane ist 10x10 Einheiten je Scale-Einheit -> hier 60x60 m.
                    gm.SetTextureScale("_BaseMap", new Vector2(30f, 30f));
                    if (gm.HasProperty("_BumpMap")) gm.SetTextureScale("_BumpMap", new Vector2(30f, 30f));
                    ground.GetComponent<Renderer>().sharedMaterial = gm;
                }
                else
                {
                    var gm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "GroundMat" };
                    var gc = new Color(0.30f, 0.31f, 0.33f);   // Beton, nicht Kohle
                    gm.color = gc;
                    if (gm.HasProperty("_BaseColor")) gm.SetColor("_BaseColor", gc);
                    if (gm.HasProperty("_Smoothness")) gm.SetFloat("_Smoothness", 0.12f);
                    ground.GetComponent<Renderer>().sharedMaterial = gm;
                }
            }

            // NavMesh-Flaeche: wird zur Laufzeit gebacken (NavMeshBaker)
            var navGo = new GameObject("Navigation");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            navGo.AddComponent<NavMeshBaker>();

            BuildMap();
            BuildReflexionssonden();

            // Mehrere Spawn-Punkte
            var spawnParent = new GameObject("SpawnPoints").transform;
            var spawns = new System.Collections.Generic.List<(Vector3 pos, int team)>();
            // 6 pro Team, im Spawn-Raum, jeder in einer LÜCKE der Rückwand.
            //
            // Vorher standen vier der sechs direkt hinter einem Riegel: die
            // Rückwand aus BuildWerkSpawns besteht aus fünf Blöcken bei
            // x = 0, ±15, ±36, und die Blöcke bei ±36 reichen von ±31 bis ±41 -
            // genau dort lagen die Spawns bei ±32 und ±34. Wer dort erschien,
            // startete die Runde mit einer Betonwand 1,4 m vor dem Gesicht.
            // Gemessen mit einem Strahlenfächer aus der Spielerkamera; ein
            // Fächer bis nur 30° hatte "alles frei" gemeldet, weil er an der
            // Wand vorbeizielte.
            //
            // Die Lücken liegen bei x = -31..-19, -11..-5, +5..+11, +19..+31.
            // SpawnFreiTests wacht darüber, dass das so bleibt.
            float[] laneX = { -27f, -23f, -8f, 8f, 23f, 27f };
            foreach (float sx in laneX)
            {
                spawns.Add((new Vector3(sx, 1f, -42f), Team.Alpha));
                spawns.Add((new Vector3(sx, 1f, 42f), Team.Bravo));
            }
            foreach (var (pos, team) in spawns)
            {
                var sp = new GameObject($"SpawnPoint_{Team.Name(team)}");
                sp.transform.SetParent(spawnParent, true);
                sp.transform.position = pos;
                sp.transform.rotation = Quaternion.LookRotation(team == Team.Alpha ? Vector3.forward : Vector3.back);
                var comp = sp.AddComponent<SpawnPoint>();
                var soSp = new SerializedObject(comp);
                soSp.FindProperty("_teamId").intValue = team;
                soSp.ApplyModifiedPropertiesWithoutUndo();
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 85f;
            cam.allowHDR = true;
            camGo.AddComponent<AudioListener>();
            // Schritt 7: Ohrenklingeln haengt am Hoerer und daempft alles,
            // was nach einer nahen Explosion noch zu hoeren ist.
            camGo.AddComponent<EarRinging>();
            camGo.AddComponent<FirstPersonCamera>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;   // Bild-Aufwertung (PostFxController)
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.transform.position = new Vector3(0f, 3f, -6f);
            camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            // Bild-Aufwertung: baut zur Laufzeit das globale Post-Processing-Volume.
            new GameObject("PostFx").AddComponent<PostFxController>();

            // Wetter pro Runde (rein optisch): Nebelfarbe, Sonnenstärke, flache
            // Nebelbank und treibender Staub. Die Sichtweite ändert sich NICHT.
            new GameObject("Weather").AddComponent<WeatherDirector>();
            var groundFogGo = new GameObject("GroundFog");
            groundFogGo.AddComponent<GroundFog>();
            BuildAtmosphereDust();

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null)
                nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = false;
            nm.NetworkConfig.EnableSceneManagement = false;
            nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
            if (dummyPrefab != null)
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = dummyPrefab });
            if (botPrefab != null)
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = botPrefab });
            if (matchManagerPrefab != null)
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = matchManagerPrefab });
            if (bombPrefab != null)
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = bombPrefab });

            nmGo.AddComponent<MatchBootstrap>();

            var spawnerGo = new GameObject("DummySpawner");
            var spawner = spawnerGo.AddComponent<DummySpawner>();
            if (dummyPrefab != null)
            {
                var soSpawner = new SerializedObject(spawner);
                soSpawner.FindProperty("_dummyPrefab").objectReferenceValue = dummyPrefab.GetComponent<NetworkObject>();
                var positions = soSpawner.FindProperty("_positions");
                positions.arraySize = 1;
                positions.GetArrayElementAtIndex(0).vector3Value = new Vector3(0f, 1f, 16f);
                soSpawner.ApplyModifiedPropertiesWithoutUndo();
            }

            // Teams + Bots + MatchManager
            var directorGo = new GameObject("MatchDirector");
            var director = directorGo.AddComponent<MatchDirector>();
            var soDir = new SerializedObject(director);
            if (botPrefab != null)
                soDir.FindProperty("_botPrefab").objectReferenceValue = botPrefab.GetComponent<NetworkObject>();
            if (matchManagerPrefab != null)
                soDir.FindProperty("_matchManagerPrefab").objectReferenceValue = matchManagerPrefab.GetComponent<NetworkObject>();
            if (bombPrefab != null)
                soDir.FindProperty("_bombPrefab").objectReferenceValue = bombPrefab.GetComponent<NetworkObject>();
            soDir.FindProperty("_teamSize").intValue = 3;
            soDir.FindProperty("_statsEasy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsEasyPath);
            soDir.FindProperty("_statsNormal").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsPath);
            soDir.FindProperty("_statsHard").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsHardPath);
            soDir.ApplyModifiedPropertiesWithoutUndo();

            var hudGo = new GameObject("HUD");
            hudGo.AddComponent<HudController>();   // zusammenhaengendes HUD (UI Toolkit)
            hudGo.AddComponent<PauseMenu>();
            hudGo.AddComponent<ScreenshotKey>();
            hudGo.AddComponent<CursorController>();
            hudGo.AddComponent<KillFeedHud>();
            hudGo.AddComponent<HighlightBanner>();   // Doppelkill/Ace/Clutch-Banner + Laufbahn
            hudGo.AddComponent<CinematicMoments>();  // Zeitlupe bei Ace/Clutch/Matchgewinn
            hudGo.AddComponent<MatchAudio>();   // Runden-/Bomben-Toene
            hudGo.AddComponent<AmbientWar>();   // Wind + fernes Gefecht/Artillerie/Hubschrauber
            hudGo.AddComponent<ImpactPool>();   // Einschlagfunken + bleibende Loecher

            EditorUtility.SetDirty(nm);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Infront] Arena gespeichert: {saved} -> {ScenePath}");

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
        }
    }
}
