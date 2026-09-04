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
            go.AddComponent<LoadingOverlay>();   // Ladebildschirm, ueberlebt Szenenwechsel
            go.AddComponent<AudioService>();     // Ton-Ausgabe, ueberlebt Szenenwechsel
            go.AddComponent<PerfOverlay>();      // Leistungsanzeige (F3), standardmaessig aus
            go.AddComponent<BootFlow>();         // Startbildschirm, laeuft genau einmal
            go.AddComponent<ZugaenglichkeitAnwender>();   // Schriftgroesse der Oberflaeche
            DontDestroyOnLoad(go);
        }

        public void ToMenu() => Go(MenuScene);
        public void ToArena() => Go(ArenaScene);

        void Go(string scene)
        {
            if (_busy) return;
            PauseMenu.ForceResume();   // nie in Zeitlupe in einen Szenenwechsel
            StartCoroutine(Switch(scene));
        }

        IEnumerator Switch(string scene)
        {
            _busy = true;

            var overlay = LoadingOverlay.Instance;
            if (overlay != null)
            {
                string label = scene == ArenaScene
                    ? (GameSettings.GameMode == GameSettings.Mode.Bombe ? "BOMBE" : "AUSSCHEIDEN")
                    : "HAUPTMENUE";
                overlay.Begin(label);
                overlay.SetProgress(0.05f, "VORBEREITEN");
            }
            float startedAt = Time.unscaledTime;

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

            if (overlay != null) overlay.SetProgress(0.25f, "NETZWERK TRENNEN");

            BotBrain.GloballyFrozen = false;
            Combatants.Reset();
            SpawnService.Reset();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (overlay != null) overlay.SetProgress(0.35f, "KARTE LADEN");

            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single);
            while (op != null && !op.isDone)
            {
                if (overlay != null)
                    overlay.SetProgress(0.35f + 0.6f * Mathf.Clamp01(op.progress / 0.9f),
                                        op.progress < 0.5f ? "KARTE LADEN" : "GEGNER AUFSTELLEN");
                yield return null;
            }
            if (overlay != null) overlay.SetProgress(1f, "BEREIT");

            // Mindestanzeige, damit der Ladebildschirm nicht nur aufblitzt.
            // Im Testlauf (batchmode) faellt das weg, damit die Ablauf-Tests
            // ihr Timing behalten.
            if (overlay != null && !Application.isBatchMode)
            {
                const float minShow = 1.5f;
                while (Time.unscaledTime - startedAt < minShow) yield return null;
                yield return overlay.PlayOutAndHide();
            }
            else if (overlay != null)
            {
                overlay.ForceHideForTests();
            }

            _busy = false;
        }
    }
}
