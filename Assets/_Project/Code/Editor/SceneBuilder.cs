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
        const string WeaponStatsPath = SettingsDir + "/Sturmgewehr.asset";
        const string BotWeaponStatsPath = SettingsDir + "/Bot_Sturmgewehr.asset";
        const string BotStatsPath = SettingsDir + "/Bot_Standard.asset";
        const string BotPrefabPath = PrefabDir + "/Bot.prefab";
        const string ScenePath = SceneDir + "/Arena.unity";

        [MenuItem("Infront/Setup/2 - Arena und Spieler bauen")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            Directory.CreateDirectory(SettingsDir);
            AssetDatabase.Refresh();

            WeaponStats weapon = CreateWeaponStats(WeaponStatsPath, "Sturmgewehr", 18);
            WeaponStats botWeapon = CreateWeaponStats(BotWeaponStatsPath, "Bot-Sturmgewehr", 12);
            BotStats botStats = CreateBotStats();
            GameObject playerPrefab = BuildPlayerPrefab(weapon);
            GameObject dummyPrefab = BuildDummyPrefab();
            GameObject botPrefab = BuildBotPrefab(botWeapon, botStats);
            BuildArenaScene(playerPrefab, dummyPrefab, botPrefab);

            Debug.Log("SCENE_BUILD_OK");
        }

        [MenuItem("Infront/Setup/0 - Alles aufsetzen (URP + Arena)")]
        public static void SetupEverything()
        {
            UrpSetup.Run();
            Build();
            Debug.Log("FULL_SETUP_OK");
        }

        static WeaponStats CreateWeaponStats(string path, string name, int damage)
        {
            var stats = AssetDatabase.LoadAssetAtPath<WeaponStats>(path);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<WeaponStats>();
                stats.FireRate = 9f;
                stats.MagazineSize = 30;
                stats.ReloadTime = 2f;
                stats.Range = 200f;
                AssetDatabase.CreateAsset(stats, path);
            }
            stats.DisplayName = name;
            stats.Damage = damage;
            EditorUtility.SetDirty(stats);
            AssetDatabase.SaveAssets();
            return stats;
        }

        static BotStats CreateBotStats()
        {
            var stats = AssetDatabase.LoadAssetAtPath<BotStats>(BotStatsPath);
            if (stats == null)
            {
                stats = ScriptableObject.CreateInstance<BotStats>();
                AssetDatabase.CreateAsset(stats, BotStatsPath);
                AssetDatabase.SaveAssets();
            }
            return stats;
        }

        static GameObject BuildPlayerPrefab(WeaponStats weapon)
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.4f;
            controller.height = 1.8f;
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.3f;

            root.AddComponent<NetworkObject>();

            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            netTransform.Interpolate = true;

            var playerController = root.AddComponent<NetworkPlayerController>();
            root.AddComponent<Health>();

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
            var lifecycle = root.AddComponent<PlayerLifecycle>();

            // Referenzen per SerializedObject setzen (private [SerializeField])
            var so = new SerializedObject(playerController);
            so.FindProperty("_aimPivot").objectReferenceValue = aimPivot.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            var soWeapon = new SerializedObject(weaponComponent);
            soWeapon.FindProperty("_stats").objectReferenceValue = weapon;
            soWeapon.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            soWeapon.ApplyModifiedPropertiesWithoutUndo();

            var soLife = new SerializedObject(lifecycle);
            var hideArray = soLife.FindProperty("_hideOnDeath");
            hideArray.arraySize = 2;
            hideArray.GetArrayElementAtIndex(0).objectReferenceValue = body;
            hideArray.GetArrayElementAtIndex(1).objectReferenceValue = nose;
            soLife.ApplyModifiedPropertiesWithoutUndo();

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
            root.AddComponent<Health>();
            var dummy = root.AddComponent<TargetDummy>();

            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            netTransform.Interpolate = false;

            // Sichtbarer Koerper MIT Collider (muss getroffen werden koennen)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);

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

        static GameObject BuildBotPrefab(WeaponStats weapon, BotStats botStats)
        {
            var root = new GameObject("Bot");
            root.AddComponent<NetworkObject>();

            var health = root.AddComponent<Health>();

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

            // Sichtbarer Koerper MIT Collider (muss getroffen werden koennen)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);

            var eyes = new GameObject("Eyes");
            eyes.transform.SetParent(root.transform, false);
            eyes.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(eyes.transform, false);
            muzzle.transform.localPosition = new Vector3(0.2f, -0.1f, 0.5f);

            var weaponComponent = root.AddComponent<NetworkWeapon>();
            var brain = root.AddComponent<BotBrain>();
            var lifecycle = root.AddComponent<BotLifecycle>();

            var soWeapon = new SerializedObject(weaponComponent);
            soWeapon.FindProperty("_stats").objectReferenceValue = weapon;
            soWeapon.FindProperty("_muzzle").objectReferenceValue = muzzle.transform;
            soWeapon.ApplyModifiedPropertiesWithoutUndo();

            var soBrain = new SerializedObject(brain);
            soBrain.FindProperty("_stats").objectReferenceValue = botStats;
            soBrain.FindProperty("_eyes").objectReferenceValue = eyes.transform;
            soBrain.ApplyModifiedPropertiesWithoutUndo();

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

        static void BuildArenaScene(GameObject playerPrefab, GameObject dummyPrefab, GameObject botPrefab)
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

            var rng = new System.Random(12345);
            var boxParent = new GameObject("Boxes").transform;
            for (int i = 0; i < 12; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Box_{i:00}";
                box.transform.SetParent(boxParent, true);
                float x = (float)(rng.NextDouble() * 40.0 - 20.0);
                float z = (float)(rng.NextDouble() * 40.0 - 20.0);
                float s = (float)(rng.NextDouble() * 1.5 + 1.5);
                box.transform.position = new Vector3(x, s * 0.5f, z);
                box.transform.localScale = new Vector3(s, s, s);
            }

            // Mehrere Spawn-Punkte
            var spawnParent = new GameObject("SpawnPoints").transform;
            Vector3[] spawnPositions =
            {
                new(0f, 1f, -4f), new(-8f, 1f, -2f), new(8f, 1f, -2f), new(0f, 1f, 4f),
            };
            foreach (var pos in spawnPositions)
            {
                var sp = new GameObject("SpawnPoint");
                sp.transform.SetParent(spawnParent, true);
                sp.transform.position = pos;
                sp.AddComponent<SpawnPoint>();
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ShoulderCamera>();
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

            // Bots
            var botSpawnerGo = new GameObject("BotSpawner");
            var botSpawner = botSpawnerGo.AddComponent<BotSpawner>();
            if (botPrefab != null)
            {
                var soBot = new SerializedObject(botSpawner);
                soBot.FindProperty("_botPrefab").objectReferenceValue = botPrefab.GetComponent<NetworkObject>();
                soBot.FindProperty("_count").intValue = 3;
                soBot.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(nm);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Infront] Arena gespeichert: {saved} -> {ScenePath}");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
