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
        const string BotStatsPath = SettingsDir + "/Bot_Normal.asset";
        const string BotStatsEasyPath = SettingsDir + "/Bot_Leicht.asset";
        const string BotStatsHardPath = SettingsDir + "/Bot_Schwer.asset";
        const string MenuScenePath = SceneDir + "/Menu.unity";
        const string BotPrefabPath = PrefabDir + "/Bot.prefab";
        const string MatchManagerPrefabPath = PrefabDir + "/MatchManager.prefab";
        const string ScenePath = SceneDir + "/Arena.unity";

        [MenuItem("Infront/Setup/2 - Arena und Spieler bauen")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(SettingsDir);
            AssetDatabase.Refresh();

            WeaponCatalog catalog = CreateWeaponCatalog();
            BotStats botStats = CreateBotStats();
            GameObject playerPrefab = BuildPlayerPrefab(catalog);
            GameObject dummyPrefab = BuildDummyPrefab();
            GameObject botPrefab = BuildBotPrefab(catalog, botStats);
            GameObject matchManagerPrefab = BuildMatchManagerPrefab();
            BuildArenaScene(playerPrefab, dummyPrefab, botPrefab, matchManagerPrefab);
            BuildMenuScene();

            Debug.Log("SCENE_BUILD_OK");
        }

        [MenuItem("Infront/Setup/0 - Alles aufsetzen (URP + Arena)")]
        public static void SetupEverything()
        {
            UrpSetup.Run();
            Build();
            Debug.Log("FULL_SETUP_OK");
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
                w.DisplayName = "Sturmgewehr"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.Damage = 18; w.FireRate = 9f; w.MagazineSize = 30; w.ReloadTime = 2f; w.Range = 200f;
                w.RecoilUp = 0.85f; w.RecoilSide = 0.3f; w.SwitchTime = 0.5f;
                w.SpreadStand = 0.15f; w.SpreadWalk = 1.4f; w.SpreadSprint = 3.2f;
            });
            var mp = MakeWeapon("Maschinenpistole", w =>
            {
                w.DisplayName = "Maschinenpistole"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.Damage = 12; w.FireRate = 14f; w.MagazineSize = 30; w.ReloadTime = 1.8f; w.Range = 120f;
                w.RecoilUp = 0.5f; w.RecoilSide = 0.25f; w.SwitchTime = 0.4f;
                w.SpreadStand = 0.4f; w.SpreadWalk = 1.2f; w.SpreadSprint = 2.5f;
            });
            var sniper = MakeWeapon("Scharfschuetzengewehr", w =>
            {
                w.DisplayName = "Scharfschuetzengewehr"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.Damage = 120; w.FireRate = 1.1f; w.MagazineSize = 5; w.ReloadTime = 3.2f; w.Range = 300f;
                w.RecoilUp = 4f; w.RecoilSide = 0.2f; w.SwitchTime = 0.9f;
                w.SpreadStand = 0.02f; w.SpreadWalk = 4f; w.SpreadSprint = 9f; w.SpreadAir = 12f;
                w.HeadshotMultiplier = 2f;
            });
            var pistole = MakeWeapon("Pistole", w =>
            {
                w.DisplayName = "Pistole"; w.SlotKind = WeaponStats.Slot.Pistole;
                w.Damage = 14; w.FireRate = 5f; w.MagazineSize = 14; w.ReloadTime = 1.5f; w.Range = 90f;
                w.RecoilUp = 1.2f; w.RecoilSide = 0.4f; w.SwitchTime = 0.3f;
                w.SpreadStand = 0.4f; w.SpreadWalk = 1.5f; w.SpreadSprint = 3f;
            });
            var botRifle = MakeWeapon("Bot_Sturmgewehr", w =>
            {
                w.DisplayName = "Sturmgewehr"; w.SlotKind = WeaponStats.Slot.Primaer;
                w.Damage = 12; w.FireRate = 9f; w.MagazineSize = 30; w.ReloadTime = 2f; w.Range = 200f;
                w.RecoilUp = 0.4f; w.RecoilSide = 0.2f; w.SwitchTime = 0.5f;
                w.SpreadStand = 0.3f; w.SpreadWalk = 1.6f; w.SpreadSprint = 3.5f;
            });

            var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(cat, CatalogPath);
            }
            cat.Weapons = new[] { sturmgewehr, mp, sniper, pistole, botRifle };
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            return cat;
        }

        static BotStats CreateBotStats()
        {
            // Normal = Standardwerte des Assets
            var normal = LoadOrCreateBotStats(BotStatsPath, spread: 5f, reaction: 0.35f, view: 28f);
            // Leicht: zittriger, langsamere Reaktion, sieht schlechter
            LoadOrCreateBotStats(BotStatsEasyPath, spread: 9f, reaction: 0.7f, view: 20f);
            // Schwer: praeziser, schnelle Reaktion, sieht weiter
            LoadOrCreateBotStats(BotStatsHardPath, spread: 2.5f, reaction: 0.18f, view: 34f);
            return normal;
        }

        static BotStats LoadOrCreateBotStats(string path, float spread, float reaction, float view)
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
            EditorUtility.SetDirty(stats);
            AssetDatabase.SaveAssets();
            return stats;
        }

        static GameObject BuildPlayerPrefab(WeaponCatalog catalog)
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
            root.AddComponent<FriendlyNameplates>();
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
            root.AddComponent<DamageFeedback>();
            var lifecycle = root.AddComponent<PlayerLifecycle>();

            // Referenzen per SerializedObject setzen (private [SerializeField])
            var so = new SerializedObject(playerController);
            so.FindProperty("_aimPivot").objectReferenceValue = aimPivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var soWeapon = new SerializedObject(weaponComponent);
            soWeapon.FindProperty("_catalog").objectReferenceValue = catalog;
            soWeapon.FindProperty("_defaultPrimary").intValue = 0;   // Sturmgewehr
            soWeapon.FindProperty("_defaultSecondary").intValue = 3; // Pistole
            soWeapon.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            soWeapon.FindProperty("_hitMask").intValue = (1 << 0) | (1 << 6);
            soWeapon.ApplyModifiedPropertiesWithoutUndo();

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
            => Tinted(name, x, y, z, sx, sy, sz, new Color(0.12f, 0.14f, 0.22f));   // Wand: fast schwarz

        static void Crate(string name, float x, float y, float z, float sx, float sy, float sz)
            => Tinted(name, x, y, z, sx, sy, sz, new Color(0.85f, 0.45f, 0.15f));   // Deckung: orange

        static readonly System.Collections.Generic.Dictionary<Color, Material> _mats = new();

        static Material MapMat(Color c)
        {
            if (_mats.TryGetValue(c, out var m)) return m;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            m = new Material(shader) { name = "MapMat" };
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            _mats[c] = m;
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

        static void BuildMap()
        {
            _mats.Clear();
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
            }

            // Sichtschutz direkt vor beiden Spawns
            BlockM("SpawnScreen_mid", 0f, 1.5f, 22f, 10f, 3f, 1f);
            BlockM("SpawnScreen_l", -19f, 1.5f, 22f, 8f, 3f, 1f);
            BlockM("SpawnScreen_r", 19f, 1.5f, 22f, 8f, 3f, 1f);

            // Deckung in der Mitte: genug zum Ueberqueren, Sichtachse bleibt lang
            CrateM("MidCrate1", 0f, 0.8f, 14f, 2.5f, 1.6f, 2f);
            CrateM("MidCrate2", -4f, 0.7f, 9f, 2f, 1.4f, 1.8f);
            CrateM("MidCrate3", 4f, 0.7f, 9f, 2f, 1.4f, 1.8f);
            CrateM("MidLow1", -2f, 0.5f, 4f, 3f, 1f, 1.2f);
            CrateM("MidLow2", 5f, 0.5f, 2f, 1.2f, 1f, 3f);
            Block("MidPillar", 0f, 2f, 0f, 1.5f, 4f, 1.5f);  // Saeule genau in der Mitte

            // Seitenbahnen: mehr Deckung, engere Kaempfe
            CrateM("LeftCrateA", -20f, 1f, 14f, 3f, 2f, 3f);
            CrateM("LeftCrateB", -15f, 0.9f, 8f, 2f, 1.8f, 2f);
            CrateM("RightCrateA", 20f, 1f, 14f, 3f, 2f, 3f);
            CrateM("RightCrateB", 15f, 0.9f, 8f, 2f, 1.8f, 2f);

            // Zwei erhoehte Platz-Bereiche (spaeter Bombenplaetze), auf Z=0,
            // damit beide Teams gleich weit haben. Ueber Rampen erreichbar.
            BuildSite("Site_Links", -19f);
            BuildSite("Site_Rechts", 19f);
        }

        static void BuildSite(string name, float x)
        {
            float y = 1.2f;
            Tinted(name + "_Platform", x, y * 0.5f, 0f, 11f, y, 12f, new Color(0.2f, 0.7f, 0.7f));

            // Rampe von beiden Seiten (Alpha- und Bravo-Zugang)
            Ramp(name + "_RampB", x, 8f, 3f, y, 4f);
            Ramp(name + "_RampA", x, -8f, -3f, y, 4f);

            // etwas Deckung auf dem Platz
            Crate(name + "_Cover1", x - 3f, y + 0.7f, 2f, 1.5f, 1.4f, 1.5f);
            Crate(name + "_Cover2", x + 3f, y + 0.7f, -2f, 1.5f, 1.4f, 1.5f);
        }

        static void AddHitboxes(GameObject root, Health owner)
        {
            var body = new GameObject("Hitbox_Body") { layer = 6 };
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            var bc = body.AddComponent<CapsuleCollider>();
            bc.radius = 0.35f; bc.height = 1.3f; bc.direction = 1;
            body.AddComponent<Hitbox>().Configure(false, owner);

            var head = new GameObject("Hitbox_Head") { layer = 6 };
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.75f, 0f);
            head.AddComponent<SphereCollider>().radius = 0.22f;
            head.AddComponent<Hitbox>().Configure(true, owner);
        }

        static GameObject BuildBotPrefab(WeaponCatalog catalog, BotStats botStats)
        {
            var root = new GameObject("Bot");
            root.AddComponent<NetworkObject>();
            root.layer = 7; // Character - vom Trefferstrahl ausgenommen

            var health = root.AddComponent<Health>();
            root.AddComponent<TeamMember>();
            root.AddComponent<TeamTint>();

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
            var brain = root.AddComponent<BotBrain>();
            var lifecycle = root.AddComponent<BotLifecycle>();

            var soWeapon = new SerializedObject(weaponComponent);
            soWeapon.FindProperty("_catalog").objectReferenceValue = catalog;
            soWeapon.FindProperty("_defaultPrimary").intValue = 4;   // Bot-Sturmgewehr
            soWeapon.FindProperty("_defaultSecondary").intValue = 3; // Pistole
            soWeapon.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            soWeapon.FindProperty("_hitMask").intValue = (1 << 0) | (1 << 6);
            soWeapon.ApplyModifiedPropertiesWithoutUndo();

            var soBrain = new SerializedObject(brain);
            soBrain.FindProperty("_stats").objectReferenceValue = botStats;
            soBrain.FindProperty("_eyes").objectReferenceValue = eyes.transform;
            soBrain.FindProperty("_sightBlockers").intValue = 1 << 0;
            soBrain.ApplyModifiedPropertiesWithoutUndo();

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

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            camGo.AddComponent<AudioListener>();

            new GameObject("MainMenu").AddComponent<MainMenu>();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log($"[Infront] Menue-Szene gespeichert: {saved} -> {MenuScenePath}");
        }

        static GameObject BuildMatchManagerPrefab()
        {
            var root = new GameObject("MatchManager");
            root.AddComponent<NetworkObject>();
            root.AddComponent<MatchManager>();

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

        static void BuildArenaScene(GameObject playerPrefab, GameObject dummyPrefab, GameObject botPrefab, GameObject matchManagerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);

            // NavMesh-Flaeche: wird zur Laufzeit gebacken (NavMeshBaker)
            var navGo = new GameObject("Navigation");
            var surface = navGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            navGo.AddComponent<NavMeshBaker>();

            BuildMap();

            // Mehrere Spawn-Punkte
            var spawnParent = new GameObject("SpawnPoints").transform;
            var spawns = new System.Collections.Generic.List<(Vector3 pos, int team)>();
            // 6 pro Team, auf die drei Bahnen verteilt, hinter dem Sichtschutz
            float[] laneX = { -20f, -18f, -1f, 1f, 19f, 21f };
            foreach (float sx in laneX)
            {
                spawns.Add((new Vector3(sx, 1f, -25f), Team.Alpha));
                spawns.Add((new Vector3(sx, 1f, 25f), Team.Bravo));
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
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<FirstPersonCamera>();
            camGo.transform.position = new Vector3(0f, 3f, -6f);
            camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

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
            soDir.FindProperty("_teamSize").intValue = 3;
            soDir.FindProperty("_statsEasy").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsEasyPath);
            soDir.FindProperty("_statsNormal").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsPath);
            soDir.FindProperty("_statsHard").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsHardPath);
            soDir.ApplyModifiedPropertiesWithoutUndo();

            var hudGo = new GameObject("HUD");
            hudGo.AddComponent<MatchHud>();
            hudGo.AddComponent<PauseMenu>();
            hudGo.AddComponent<ScreenshotKey>();
            hudGo.AddComponent<CursorController>();
            hudGo.AddComponent<KillFeedHud>();
            hudGo.AddComponent<Scoreboard>();

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
