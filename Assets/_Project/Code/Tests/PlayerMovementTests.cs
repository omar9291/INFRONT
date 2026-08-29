using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-1-Tests: Charakter spawnt im Host-Modus und bewegt sich
    /// server-autoritativ. Bots sind waehrend dieser Tests eingefroren.
    /// </summary>
    public sealed class PlayerMovementTests
    {
        [UnityTearDown] public IEnumerator TearDown() => MatchTestHarness.Teardown();

        [UnityTest]
        public IEnumerator Spieler_spawnt_im_Host_Modus()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            Assert.IsTrue(player.IsSpawned);
            Assert.IsTrue(NetworkManager.Singleton.IsHost);
        }

        [UnityTest]
        public IEnumerator Spieler_laeuft_auf_Vorwaerts_Eingabe_nach_vorne()
        {
            NetworkPlayerController player = null;
            yield return MatchTestHarness.LoadReady((p, m) => player = p);

            MatchTestHarness.ClearArena();
            MatchTestHarness.PlacePlayer(player, new Vector3(0f, 1f, 0f), 0f);
            player.SetMovementEnabled(true);
            var input = new FakePlayerInput { Move = new Vector2(0f, 1f), LookYaw = 0f };
            player.SetInputSource(input);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Vector3 start = player.transform.position;
            Vector3 forwardDir = player.transform.forward;
            for (int i = 0; i < 100; i++) yield return new WaitForFixedUpdate();
            Vector3 end = player.transform.position;

            float moved = Vector3.Dot(end - start, forwardDir);
            Assert.Greater(moved, 2f, $"Spieler ist nicht vorwaerts gelaufen. start={start} end={end} (vor={moved:F2})");
        }
    }
}
