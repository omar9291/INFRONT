using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Automatischer Foto-Modus: Der Entwickler kann das Spiel auf diesem
    /// Rechner weder ansehen noch bedienen (Bildschirmaufnahme und synthetische
    /// Tasten sind gesperrt). Also fotografiert das Spiel sich selbst.
    ///
    /// Aktiviert nur mit dem Startparameter <c>-autoshot</c>:
    ///   open Builds/INFRONT.app --args -autoshot
    /// Optionale Parameter:
    ///   -weather N   erzwingt eine Wetterlage (0..4: Klar/Dunst/Staubwind/Bodennebel/Rauch)
    ///   -outdir PFAD Zielordner (Standard: &lt;Projekt&gt;/Screenshots/auto)
    ///
    /// Ablauf: ein Menue-Bild -> Match starten -> Kaufzeit sofort beenden ->
    /// eine freie Kamera fliegt an feste Punkte der Karte (Spawn-Blick, Podest,
    /// Halle, beide Bombenplaetze, ein Aussenweg) und macht an jedem ein Bild,
    /// waehrend die Bots kaempfen -> beenden.
    ///
    /// Reines Entwickler-Hilfsmittel. Ohne den Parameter passiert gar nichts.
    /// </summary>
    public sealed class AutoShot : MonoBehaviour
    {
        const string DefaultOutDir = "/Users/user/UnityProjects/INFRONT/Screenshots/auto";

        static readonly (Vector3 pos, Vector3 look, string name)[] Stops =
        {
            (new Vector3(0f, 13f, -40f), new Vector3(0f, 2f, 0f),   "spawn"),
            (new Vector3(0f, 9f, -16f),  new Vector3(0f, 1.5f, 8f), "podest"),
            (new Vector3(0f, 6.5f, 22f), new Vector3(0f, 2f, 40f),  "halle"),
            (new Vector3(-13f, 7f, -7f), new Vector3(-22f, 1.5f, 2f), "site_a"),
            (new Vector3(15f, 7f, 7f),   new Vector3(24f, 1.5f, -2f), "site_b"),
            (new Vector3(-30f, 8f, 15f), new Vector3(-40f, 1.5f, -12f), "lane"),
            (new Vector3(0f, 22f, 0f),   new Vector3(0f, 0f, 0.01f), "vogelperspektive"),
        };

        Camera _cam;
        Vector3 _flyPos;
        Quaternion _flyRot;
        bool _flying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (!System.Environment.GetCommandLineArgs().Contains("-autoshot")) return;

            GameSettings.DisplayMode = GameSettings.Anzeige.Fenster;   // Fenster statt Vollbild
            GameSettings.GameMode = GameSettings.Mode.Ausscheiden;     // sauberes Gefecht fuer die Fotos

            var go = new GameObject("AutoShot");
            go.AddComponent<AutoShot>();
            DontDestroyOnLoad(go);
        }

        static string Arg(string key, string fallback)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == key) return a[i + 1];
            return fallback;
        }

        IEnumerator Start()
        {
            string outDir = Arg("-outdir", DefaultOutDir);
            int weather = int.TryParse(Arg("-weather", "-1"), out var w) ? w : -1;
            string tag = weather >= 0 ? $"w{weather}_" : "";

            Directory.CreateDirectory(outDir);
            Screen.SetResolution(1600, 900, FullScreenMode.Windowed);

            yield return new WaitForSecondsRealtime(2.5f);
            Capture(outDir, $"{tag}00_menu");

            if (GameFlow.Instance != null) GameFlow.Instance.ToArena();

            // Auf Arena + Spieler warten.
            float t = 0f;
            NetworkPlayerController player = null;
            while (t < 25f && (MatchManager.Instance == null || player == null))
            {
                player = Object.FindAnyObjectByType<NetworkPlayerController>();
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);

            // Kaufzeit/Freeze sofort beenden, damit die Bots loslaufen.
            var mm = MatchManager.Instance;
            if (mm != null)
            {
                mm.SkipFreezeForTests = true;
                try { mm.RequestEndBuyTimeRpc(); }   // Host = Server: Freeze/Kaufzeit sofort beenden
                catch (System.Exception e) { Debug.LogWarning($"[Infront] AutoShot: {e.Message}"); }
            }

            // Kamera uebernehmen: FirstPersonCamera abschalten, Waffe ausblenden.
            _cam = Camera.main;
            if (_cam != null)
            {
                var fpc = _cam.GetComponent<FirstPersonCamera>();
                if (fpc != null) fpc.enabled = false;
                var vm = Object.FindAnyObjectByType<ViewModel>();
                if (vm != null)
                {
                    vm.enabled = false;
                    var rig = _cam.transform.Find("ViewModel");   // die Waffe haengt an der Kamera
                    if (rig != null) rig.gameObject.SetActive(false);
                }
            }

            // Leistungsanzeige einblenden.
            var perf = Object.FindAnyObjectByType<PerfOverlay>();
            if (perf != null && !perf.VisibleForTests) perf.Toggle();

            // Optional Wetter erzwingen.
            if (weather >= 0)
            {
                var wd = Object.FindAnyObjectByType<WeatherDirector>();
                if (wd != null) wd.ForceWeatherForTests((WeatherKind)Mathf.Clamp(weather, 0, 4));
            }

            yield return new WaitForSecondsRealtime(3f);   // Bots laufen an, Wetter blendet

            // Kamera an die Punkte fliegen und je ein Bild machen.
            for (int i = 0; i < Stops.Length; i++)
            {
                var p = Stops[i];
                _flyPos = p.pos;
                _flyRot = Quaternion.LookRotation((p.look - p.pos).normalized, Vector3.up);
                _flying = true;

                yield return new WaitForSecondsRealtime(2.6f);   // Kampf-Geschehen abwarten
                Capture(outDir, $"{tag}{i + 1:00}_{p.name}");
            }

            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[Infront] AutoShot fertig -> {outDir}");
            Application.Quit();
        }

        void LateUpdate()
        {
            if (_flying && _cam != null)
            {
                // Weich hinfliegen.
                _cam.transform.position = Vector3.Lerp(_cam.transform.position, _flyPos, 0.12f);
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, _flyRot, 0.12f);
            }
        }

        static void Capture(string dir, string name)
        {
            string path = Path.Combine(dir, $"{name}.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[Infront] AutoShot: {path}");
        }
    }
}
