using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Infront
{
    /// <summary>
    /// Die EINE Stelle fuer Netzwerk-Abbau und Szenenwechsel. Menue, Pause und
    /// Rundenende benutzen sie gemeinsam - so gibt es nur einen Ablauf, der
    /// aufraeumt, statt drei, die sich widersprechen.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public const string MenuScene = "Menu";
        public const string ArenaScene = "Arena";

        public static GameFlow Instance { get; private set; }
        bool _busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("GameFlow");
            Instance = go.AddComponent<GameFlow>();
            DontDestroyOnLoad(go);
        }

        public void ToMenu() => Go(MenuScene);
        public void ToArena() => Go(ArenaScene);

        void Go(string scene)
        {
            if (_busy) return;
            StartCoroutine(Switch(scene));
        }

        IEnumerator Switch(string scene)
        {
            _busy = true;

            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsListening)
                    NetworkManager.Singleton.Shutdown();
                yield return null;

                float t = 0f;
                while (NetworkManager.Singleton != null
                       && NetworkManager.Singleton.ShutdownInProgress && t < 5f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                if (NetworkManager.Singleton != null)
                    Destroy(NetworkManager.Singleton.gameObject);
                yield return null;
            }

            BotBrain.GloballyFrozen = false;
            Combatants.Reset();
            SpawnService.Reset();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            _busy = false;
        }
    }
}
