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

            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = 0f };
            player.SetInputSource(input);

            // Auf echte Physik-Ticks warten, nicht auf Frames: headless rasen die
            // Frames durch, ohne dass FixedUpdate (die Server-Simulation) tickt.
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();

            Vector3 start = player.transform.position;
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            Vector3 end = player.transform.position;

            float forward = end.z - start.z;
            Assert.Greater(forward, 2f,
                $"Spieler ist nicht nach vorne gelaufen. start={start} end={end} (dz={forward:F2})");
        }
    }
}
