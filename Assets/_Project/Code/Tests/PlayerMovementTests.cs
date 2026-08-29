using System.Collections;
using Infront;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-1-Test: Der Charakter spawnt im Host-Modus und bewegt sich
    /// server-autoritativ auf Eingabe hin.
    /// Laeuft headless:
    ///   Unity -batchmode -runTests -testPlatform PlayMode -projectPath ...
    /// </summary>
    public sealed class PlayerMovementTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null;
                Object.Destroy(NetworkManager.Singleton.gameObject);
            }
            yield return null;
        }

        static IEnumerator StartHostAndGetPlayer(System.Action<NetworkPlayerController> onReady)
        {
            SceneManager.LoadScene("Arena");
            yield return null;
            yield return null;

            Assert.IsNotNull(NetworkManager.Singleton, "Kein NetworkManager in der Arena-Szene.");

            float timeout = 8f;
            while (!NetworkManager.Singleton.IsListening && timeout > 0f)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(NetworkManager.Singleton.IsListening, "Host wurde nicht gestartet.");

            NetworkPlayerController player = null;
            timeout = 8f;
            while (player == null && timeout > 0f)
            {
                player = Object.FindAnyObjectByType<NetworkPlayerController>();
                timeout -= Time.deltaTime;
                yield return null;
            }
            Assert.IsNotNull(player, "Es wurde kein Spieler-Objekt gespawnt.");
            onReady(player);
        }

        [UnityTest]
        public IEnumerator Spieler_spawnt_im_Host_Modus()
        {
            NetworkPlayerController player = null;
            yield return StartHostAndGetPlayer(p => player = p);

            Assert.IsTrue(player.IsSpawned, "Spieler ist nicht als NetworkObject gespawnt.");
            Assert.IsTrue(NetworkManager.Singleton.IsHost, "NetworkManager laeuft nicht als Host.");
        }

        [UnityTest]
        public IEnumerator Spieler_laeuft_auf_Vorwaerts_Eingabe_nach_vorne()
        {
            NetworkPlayerController player = null;
            yield return StartHostAndGetPlayer(p => player = p);

            // Warten, bis der MatchManager die Startaufstellung erledigt hat,
            // dann alle Bots stilllegen - dieser Test prueft nur die Bewegung,
            // kein Gefecht.
            float w = 0f;
            while (Infront.MatchManager.Instance == null && w < 5f) { w += Time.deltaTime; yield return null; }
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            foreach (var brain in Object.FindObjectsByType<Infront.BotBrain>(FindObjectsSortMode.None))
                brain.SetActive(false);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            // Blickrichtung des Spielers als "vorne" nehmen (nicht die Weltachse).
            float faceYaw = player.transform.eulerAngles.y;
            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = faceYaw };
            player.SetInputSource(input);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Vector3 start = player.transform.position;
            Vector3 forwardDir = player.transform.forward;
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            Vector3 end = player.transform.position;

            float movedForward = Vector3.Dot(end - start, forwardDir);
            Assert.Greater(movedForward, 2f,
                $"Spieler ist nicht in Blickrichtung gelaufen. start={start} end={end} (vor={movedForward:F2})");
        }
    }
}
