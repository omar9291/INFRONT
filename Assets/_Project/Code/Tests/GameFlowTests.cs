using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Infront.Tests
{
    /// <summary>
    /// Phase-5-Tests: der Kreislauf Menue -> Arena -> Menue -> Arena muss
    /// mehrfach ohne Fehler laufen. Genau hier stuerzen solche Spiele ab.
    /// </summary>
    public sealed class GameFlowTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BotBrain.GloballyFrozen = false;
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
                yield return null;
                Object.Destroy(NetworkManager.Singleton.gameObject);
            }
            Combatants.Reset();
            SpawnService.Reset();
            yield return null;
        }

        static IEnumerator WaitUntil(System.Func<bool> cond, float timeout, string msg)
        {
            float t = 0f;
            while (!cond() && t < timeout) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(cond(), msg);
        }

        [UnityTest]
        public IEnumerator Menue_startet_und_hat_GameFlow()
        {
            yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
            yield return null;

            Assert.IsNotNull(GameFlow.Instance, "GameFlow wurde nicht erzeugt.");
            Assert.IsNotNull(Object.FindAnyObjectByType<MainMenu>(), "Kein MainMenu in der Menue-Szene.");
        }

        [UnityTest]
        public IEnumerator Kreislauf_Menue_Arena_Menue_Arena_zweimal()
        {
            BotBrain.GloballyFrozen = true; // Gefecht aus, wir testen nur den Ablauf

            yield return SceneManager.LoadSceneAsync(GameFlow.MenuScene);
            yield return null;
            Assert.IsNotNull(GameFlow.Instance);

            for (int runde = 1; runde <= 2; runde++)
            {
                GameFlow.Instance.ToArena();
                yield return WaitUntil(
                    () => SceneManager.GetActiveScene().name == "Arena", 8f,
                    $"Runde {runde}: Arena wurde nicht geladen.");
                yield return WaitUntil(
                    () => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening, 8f,
                    $"Runde {runde}: Host startete nicht.");
                yield return WaitUntil(
                    () => MatchManager.Instance != null, 8f,
                    $"Runde {runde}: Kein MatchManager.");
                yield return WaitUntil(
                    () => Object.FindAnyObjectByType<NetworkPlayerController>() != null, 8f,
                    $"Runde {runde}: Kein Spieler.");

                // genau EIN NetworkManager
                Assert.AreEqual(1, Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length,
                    $"Runde {runde}: mehr als ein NetworkManager!");

                for (int i = 0; i < 20; i++) yield return null;

                GameFlow.Instance.ToMenu();
                yield return WaitUntil(
                    () => SceneManager.GetActiveScene().name == "Menu", 8f,
                    $"Runde {runde}: Menue wurde nicht geladen.");
                Assert.IsNull(NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton : null,
                    $"Runde {runde}: Netzwerk laeuft im Menue noch.");
            }
        }
    }
}
