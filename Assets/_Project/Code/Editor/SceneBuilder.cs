using System.IO;
using Infront;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Infront.EditorTools
{
    /// <summary>
    /// Erzeugt per Code das Spieler-Prefab und die Test-Arena.
    /// Nichts davon wird von Hand in der Unity-Oberflaeche gebaut.
    ///
    /// Menue: "Infront/Setup/2 - Arena und Spieler bauen"
    /// Headless: Unity -batchmode -quit -executeMethod Infront.EditorTools.SceneBuilder.Build
    /// </summary>
    public static class SceneBuilder
    {
        const string PrefabDir = "Assets/_Project/Prefabs";
        const string SceneDir = "Assets/_Project/Scenes";
        const string PlayerPrefabPath = PrefabDir + "/Player.prefab";
        const string ScenePath = SceneDir + "/Arena.unity";

        [MenuItem("Infront/Setup/2 - Arena und Spieler bauen")]
        public static void Build()
        {
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(SceneDir);
            AssetDatabase.Refresh();

            GameObject playerPrefab = BuildPlayerPrefab();
            BuildArenaScene(playerPrefab);

            Debug.Log("SCENE_BUILD_OK");
        }

        [MenuItem("Infront/Setup/0 - Alles aufsetzen (URP + Arena)")]
        public static void SetupEverything()
        {
            UrpSetup.Run();
            Build();
            Debug.Log("FULL_SETUP_OK");
        }

        static GameObject BuildPlayerPrefab()
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
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server; // server-autoritativ
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            netTransform.Interpolate = true;

            root.AddComponent<NetworkPlayerController>();

            // Sichtbarer Koerper (nur Optik, keine Collider)
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            // Nase, damit die Blickrichtung sichtbar ist
            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.4f, 0.45f);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath, out bool ok);
            Object.DestroyImmediate(root);

            if (!ok || prefab == null)
            {
                Debug.LogError("[Infront] Spieler-Prefab konnte nicht gespeichert werden.");
                return null;
            }

            AssetDatabase.ImportAsset(PlayerPrefabPath, ImportAssetOptions.ForceUpdate);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            bool hasNetworkObject = prefab.GetComponent<NetworkObject>() != null;
            Debug.Log($"[Infront] Spieler-Prefab bereit. NetworkObject={hasNetworkObject}");
            return prefab;
        }

        static void BuildArenaScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Licht
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Boden: 60 x 60 Meter (Plane ist 10 m, Scale 6)
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);

            // Ein paar Kisten als Deckung / Orientierung
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

            // Spawn-Punkt
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.position = new Vector3(0f, 1f, 0f);

            // Kamera mit Schulterkamera-Skript
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ShoulderCamera>();
            camGo.transform.position = new Vector3(0f, 3f, -6f);
            camGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);

            // NetworkManager + Transport + Auto-Host
            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var transport = nmGo.AddComponent<UnityTransport>();

            if (nm.NetworkConfig == null)
                nm.NetworkConfig = new NetworkConfig();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = playerPrefab;
            nm.NetworkConfig.ConnectionApproval = false;
            nm.NetworkConfig.EnableSceneManagement = false;

            if (playerPrefab != null)
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });

            nmGo.AddComponent<MatchBootstrap>();

            EditorUtility.SetDirty(nm);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[Infront] Arena gespeichert: {saved} -> {ScenePath}");

            // In die Build-Settings aufnehmen (als einzige/erste Szene)
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
