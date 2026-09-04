using System.Collections;
using System.IO;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

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
        const string SurveyOutDir = "/Users/user/UnityProjects/INFRONT/Screenshots/rundgang";

        static readonly (Vector3 pos, Vector3 look, string name)[] Stops =
        {
            (new Vector3(0f, 13f, -40f), new Vector3(0f, 2f, 0f),   "spawn"),
            (new Vector3(0f, 9f, -16f),  new Vector3(0f, 1.5f, 8f), "podest"),
            (new Vector3(0f, 6.5f, 22f), new Vector3(0f, 2f, 40f),  "halle"),
            (new Vector3(-13f, 7f, -7f), new Vector3(-22f, 1.5f, 2f), "site_a"),
            (new Vector3(15f, 7f, 7f),   new Vector3(24f, 1.5f, -2f), "site_b"),
            (new Vector3(-30f, 8f, 15f), new Vector3(-40f, 1.5f, -12f), "lane"),
            // Unter das Dach gerueckt: seit die Halle ein Dach hat, wuerde die
            // alte Position bei 19 m nur noch Blech zeigen.
            (new Vector3(0f, 11.5f, -20f), new Vector3(0f, 0.5f, 6f),  "vogelperspektive"),
        };

        /// <summary>Ein Halt des Rundgangs. <c>DachAus</c> blendet das Hallendach
        /// aus, damit die Karte von oben ueberhaupt zu sehen ist.</summary>
        readonly struct Halt
        {
            public readonly Vector3 Pos;
            public readonly Vector3 Look;
            public readonly string Name;
            public readonly bool DachAus;
            public Halt(Vector3 pos, Vector3 look, string name, bool dachAus = false)
            { Pos = pos; Look = look; Name = name; DachAus = dachAus; }
        }

        /// <summary>
        /// Rundgang (<c>-survey</c>): deutlich dichter als der normale Foto-Modus.
        /// Zuerst die ganze Karte von oben ohne Dach, dann die Mitte auf Augenhoehe
        /// in alle acht Richtungen, dann die Mitte von allen vier Seiten, dann
        /// Bombenplaetze und Gaenge, zuletzt der Blick nach oben ins Dach.
        /// Boden liegt bei y ~ 1.28, Augenhoehe also ~ 2.9.
        /// </summary>
        static readonly Halt[] SurveyStops = BaueRundgang();

        static Halt[] BaueRundgang()
        {
            var l = new System.Collections.Generic.List<Halt>();
            const float aug = 2.9f;

            // A - Uebersicht, Dach ausgeblendet.
            l.Add(new Halt(new Vector3(0f, 82f, 0f), new Vector3(0f, 1.3f, 0f), "a0_gesamt", true));
            l.Add(new Halt(new Vector3(-52f, 32f, -52f), new Vector3(-12f, 1.5f, -12f), "a1_quad_nw", true));
            l.Add(new Halt(new Vector3(52f, 32f, -52f), new Vector3(12f, 1.5f, -12f), "a2_quad_no", true));
            l.Add(new Halt(new Vector3(52f, 32f, 52f), new Vector3(12f, 1.5f, 12f), "a3_quad_so", true));
            l.Add(new Halt(new Vector3(-52f, 32f, 52f), new Vector3(-12f, 1.5f, 12f), "a4_quad_sw", true));

            // B - Mitte auf Augenhoehe, volle Drehung. Genau hier meldet der
            // Spieler, dass etwas "bugt".
            string[] himmel = { "n", "no", "o", "so", "s", "sw", "w", "nw" };
            for (int i = 0; i < 8; i++)
            {
                float grad = i * 45f;
                var r = Quaternion.Euler(0f, grad, 0f) * Vector3.forward;
                l.Add(new Halt(new Vector3(0f, aug, 0f),
                               new Vector3(0f, aug, 0f) + r * 30f + Vector3.down * 0.5f,
                               $"b{i}_mitte_{himmel[i]}"));
            }

            // C - die Mitte von allen vier Seiten, halbe Hoehe.
            l.Add(new Halt(new Vector3(0f, 6f, -14f), new Vector3(0f, 2f, 0f), "c0_mitte_von_nord"));
            l.Add(new Halt(new Vector3(14f, 6f, 0f), new Vector3(0f, 2f, 0f), "c1_mitte_von_ost"));
            l.Add(new Halt(new Vector3(0f, 6f, 14f), new Vector3(0f, 2f, 0f), "c2_mitte_von_sued"));
            l.Add(new Halt(new Vector3(-14f, 6f, 0f), new Vector3(0f, 2f, 0f), "c3_mitte_von_west"));

            // D - Bombenplaetze und Gaenge auf Augenhoehe.
            l.Add(new Halt(new Vector3(-13f, aug, 6f), new Vector3(-24f, 2.2f, -2f), "d0_site_a"));
            l.Add(new Halt(new Vector3(13f, aug, -6f), new Vector3(24f, 2.2f, 2f), "d1_site_b"));
            l.Add(new Halt(new Vector3(0f, aug, -34f), new Vector3(0f, 2.4f, 0f), "d2_gang_nord"));
            l.Add(new Halt(new Vector3(0f, aug, 34f), new Vector3(0f, 2.4f, 0f), "d3_gang_sued"));
            l.Add(new Halt(new Vector3(-30f, 4.25f, -8f), new Vector3(-30f, 3.6f, 30f), "d4_rand_west"));
            l.Add(new Halt(new Vector3(30f, 4.25f, 8f), new Vector3(30f, 3.6f, -30f), "d5_rand_ost"));

            // E - nach oben, Dach und Lichtbaender.
            l.Add(new Halt(new Vector3(0f, aug, 0f), new Vector3(0f, 14f, 6f), "e0_dach_mitte"));
            l.Add(new Halt(new Vector3(-20f, aug, 0f), new Vector3(-20f, 14f, 8f), "e1_dach_west"));

            // F - Rauchwolke. Wird beim ersten f-Halt eigens gesetzt, sonst
            // haengt es vom Zufall ab, ob gerade eine Granate fliegt - und
            // ungeprueft bleibt eine Wirkung, die man nicht gesehen hat.
            l.Add(new Halt(new Vector3(-13f, aug, 6f), new Vector3(-20f, 2.2f, 0f), "f0_rauch_nah"));
            l.Add(new Halt(new Vector3(-20f, 7f, 16f), new Vector3(-20f, 2f, 0f), "f1_rauch_fern"));

            return l.ToArray();
        }

        Camera _cam;
        Vector3 _flyPos;
        Quaternion _flyRot;
        bool _flying;
        bool _rauchGesetzt;

        /// <summary>
        /// Baut eine eigene Kamera fuer den Rundgang.
        ///
        /// Vorher wurde die Kamera des Spielers ausgeliehen. Stirbt der Spieler
        /// mitten im Rundgang - und das passiert, es laeuft ja ein echtes
        /// Gefecht - verschwindet sie, und ab da zeigen alle weiteren Bilder
        /// dasselbe. Genau so waren beim zweiten Lauf die Bilder 24/25 und
        /// 26/27 paarweise gleich. Eine eigene Kamera haengt an nichts.
        /// </summary>
        void KameraSichern()
        {
            if (_cam != null) return;

            var quelle = Camera.main;
            var go = new GameObject("Rundgang_Kamera");
            // Als Hauptkamera kennzeichnen: alles, was ueber Camera.main geht
            // (Namensschilder, Trefferanzeige, Ton), findet sonst gar nichts
            // mehr, sobald die Spielerkamera stillgelegt ist.
            go.tag = "MainCamera";
            DontDestroyOnLoad(go);
            var c = go.AddComponent<Camera>();
            if (quelle != null)
            {
                c.fieldOfView = quelle.fieldOfView;
                c.nearClipPlane = quelle.nearClipPlane;
                c.farClipPlane = quelle.farClipPlane;
                c.cullingMask = quelle.cullingMask;
                c.clearFlags = quelle.clearFlags;
                c.backgroundColor = quelle.backgroundColor;
                // Nicht zerstoeren, nur stilllegen - das Spiel laeuft weiter.
                quelle.enabled = false;
                var fpc = quelle.GetComponent<FirstPersonCamera>();
                if (fpc != null) fpc.enabled = false;
                var rig = quelle.transform.Find("ViewModel");
                if (rig != null) rig.gameObject.SetActive(false);
            }
            else
            {
                c.fieldOfView = 70f;
                c.nearClipPlane = 0.05f;
                c.farClipPlane = 400f;
            }
            c.depth = 100f;

            // WICHTIG: In URP haengt die Bildaufwertung an der KAMERA, nicht am
            // Volume. Eine frisch erzeugte Kamera hat renderPostProcessing =
            // false. Ohne diese Zeilen fotografiert der Rundgang ein Spiel ohne
            // ACES-Tonwertkurve, ohne Bloom, ohne Vignette - also nicht das,
            // was der Spieler sieht. Beim ersten Lauf mit eigener Kamera sah
            // das Bild dadurch schlagartig "besser" aus (Mittelwert 83 -> 111,
            // schwarzer Anteil 14,5 % -> 0 %), waehrend gleichzeitig die
            // ausgebrannten Stellen von 0,5 % auf 5,1 % sprangen. Das war kein
            // Fortschritt, sondern eine fehlende Tonwertkurve.
            var vonDaten = quelle != null ? quelle.GetUniversalAdditionalCameraData() : null;
            var meineDaten = c.GetUniversalAdditionalCameraData();
            if (meineDaten != null)
            {
                if (vonDaten != null)
                {
                    meineDaten.renderPostProcessing = vonDaten.renderPostProcessing;
                    meineDaten.antialiasing = vonDaten.antialiasing;
                    meineDaten.antialiasingQuality = vonDaten.antialiasingQuality;
                    meineDaten.renderShadows = vonDaten.renderShadows;
                    meineDaten.volumeLayerMask = vonDaten.volumeLayerMask;
                    meineDaten.volumeTrigger = vonDaten.volumeTrigger;
                    meineDaten.dithering = vonDaten.dithering;
                    meineDaten.stopNaN = vonDaten.stopNaN;
                }
                else
                {
                    meineDaten.renderPostProcessing = true;
                    meineDaten.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    meineDaten.renderShadows = true;
                }
            }

            _cam = c;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var argv = System.Environment.GetCommandLineArgs();
            if (!argv.Contains("-autoshot") && !argv.Contains("-survey")) return;

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
            bool istRundgang = System.Environment.GetCommandLineArgs().Contains("-survey");
            string outDir = Arg("-outdir", istRundgang ? SurveyOutDir : DefaultOutDir);
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

                // Beim Rundgang die Runde anhalten. Sonst endet sie mitten im
                // Durchlauf, und ueber der halben Bilderserie liegt die dunkle
                // Tafel "Team Alpha gewinnt die Runde" - gemessen zog das den
                // Bildmittelwert von 79 auf 25 und sah aus wie ein Lichtfehler.
                // Die Bots kaempfen weiter, nur gewinnen kann niemand.
                if (istRundgang) mm.SuspendedForTests = true;
                try { mm.RequestEndBuyTimeRpc(); }   // Host = Server: Freeze/Kaufzeit sofort beenden
                catch (System.Exception e) { Debug.LogWarning($"[Infront] AutoShot: {e.Message}"); }
            }

            // Eigene Kamera bauen; die des Spielers wird dabei stillgelegt.
            {
                var vm = Object.FindAnyObjectByType<ViewModel>();
                if (vm != null) vm.enabled = false;
            }
            KameraSichern();

            // Leistungsanzeige einblenden.
            var perf = Object.FindAnyObjectByType<PerfOverlay>();
            if (perf != null && !perf.VisibleForTests) perf.Toggle();

            // Bot-Faehigkeiten aus - sonst fliegen staendig Granaten durchs Bild.
            foreach (var ah in Object.FindObjectsByType<AbilityHolder>(FindObjectsSortMode.None))
                ah.enabled = false;

            // Optional Wetter erzwingen.
            if (weather >= 0)
            {
                var wd = Object.FindAnyObjectByType<WeatherDirector>();
                if (wd != null) wd.ForceWeatherForTests((WeatherKind)Mathf.Clamp(weather, 0, 4));
            }

            yield return new WaitForSecondsRealtime(3f);   // Bots laufen an, Wetter blendet

            // Welche Route? Der Rundgang ist der dichte Blick auf die ganze Karte.
            bool rundgang = System.Environment.GetCommandLineArgs().Contains("-survey");
            Halt[] route;
            if (rundgang)
            {
                route = SurveyStops;
            }
            else
            {
                route = new Halt[Stops.Length];
                for (int i = 0; i < Stops.Length; i++)
                    route[i] = new Halt(Stops[i].pos, Stops[i].look, Stops[i].name);
            }

            // Das Dach ist ein eigener Wurzelknoten - fuer die Sicht von oben
            // wird es kurz ausgeblendet und danach wieder eingeschaltet.
            GameObject dach = GameObject.Find("Dach");
            float halt = rundgang ? 1.4f : 2.6f;

            // Kamera an die Punkte fliegen und je ein Bild machen.
            for (int i = 0; i < route.Length; i++)
            {
                var p = route[i];
                if (dach != null && dach.activeSelf != !p.DachAus) dach.SetActive(!p.DachAus);

                // Stirbt der Spieler mitten im Rundgang, ist die alte Kamera
                // weg und die Bilder zeigen irgendetwas. Deshalb bei jedem Halt
                // nachsehen. (Genau das ist beim ersten Lauf passiert: die
                // letzten drei Bilder zeigten die Decke statt der Karte.)
                KameraSichern();

                if (p.Name.StartsWith("f") && !_rauchGesetzt)
                {
                    _rauchGesetzt = true;
                    var wolke = new GameObject("Rundgang_Rauch");
                    wolke.transform.position = new Vector3(-20f, 2.0f, 0f);
                    wolke.AddComponent<SmokeVolume>().Init(4.5f, 120f);
                    yield return new WaitForSecondsRealtime(1.6f);   // aufziehen lassen
                    var pruef = wolke.GetComponentInChildren<ParticleSystem>();
                    Debug.Log(pruef == null
                        ? "[Infront] RUNDGANG_RAUCH: kein ParticleSystem gefunden"
                        : $"[Infront] RUNDGANG_RAUCH: Partikel={pruef.particleCount} " +
                          $"laeuft={pruef.isPlaying} pos={wolke.transform.position} " +
                          $"mat={(pruef.GetComponent<ParticleSystemRenderer>().material != null ? pruef.GetComponent<ParticleSystemRenderer>().material.shader.name : "-")}");
                }

                _flyPos = p.Pos;
                Vector3 dir = (p.Look - p.Pos).normalized;
                // Kein Roll: bei steilem Blick nach unten einen anderen Hoch-Hinweis nehmen.
                Vector3 up = Mathf.Abs(dir.y) > 0.92f ? Vector3.forward : Vector3.up;
                _flyRot = Quaternion.LookRotation(dir, up);
                _flying = true;

                // Sofort hinsetzen, dann ruhig halten - keine schiefen Zwischenbilder.
                if (_cam != null) _cam.transform.SetPositionAndRotation(_flyPos, _flyRot);
                yield return new WaitForSecondsRealtime(halt);   // Kampf-Geschehen abwarten

                string dateiname = $"{tag}{i + 1:00}_{p.Name}";
                Capture(outDir, dateiname);

                // Auf die Datei warten, bevor die Kamera weiterzieht.
                //
                // ScreenCapture.CaptureScreenshot arbeitet nebenher. Ein Bild
                // mit 1600x900 wird als PNG etwa 1,4 MB gross, und das Packen
                // dauert laenger als der Abstand zwischen zwei Halten. Dadurch
                // staut es sich auf: die ersten Bilder stimmen, die spaeteren
                // zeigen immer aeltere Ansichten. Genau deshalb sahen die
                // letzten beiden Bilder gleich aus, obwohl im Protokoll die
                // richtigen Kamerastandorte standen.
                string erwartet = Path.Combine(outDir, dateiname + ".png");
                float wartet = 0f;
                while (!File.Exists(erwartet) && wartet < 8f)
                {
                    wartet += Time.unscaledDeltaTime;
                    yield return null;
                }
                // Noch kurz stehen bleiben, damit die Datei fertig geschrieben ist.
                yield return new WaitForSecondsRealtime(0.25f);

                // Nachweisen, dass die Kamera wirklich dort steht, wo sie soll -
                // und ob noch eine zweite Kamera mitmischt. Bild 23 zeigte
                // hartnaeckig die Decke, obwohl der Halt woanders liegt.
                if (rundgang)
                {
                    var oben = Camera.allCameras;
                    string liste = "";
                    foreach (var k in oben)
                        liste += $"{k.name}(t{k.depth:F0},{(k.enabled ? "an" : "aus")}) ";
                    Debug.Log($"[Infront] RUNDGANG_HALT {p.Name} soll={p.Pos} " +
                              $"ist={(_cam != null ? _cam.transform.position.ToString("F2") : "-")} " +
                              $"kameras={oben.Length}: {liste}");
                }
            }

            if (dach != null) dach.SetActive(true);

            yield return new WaitForSecondsRealtime(1f);
            Debug.Log($"[Infront] AutoShot fertig -> {outDir}");
            Application.Quit();
        }

        void LateUpdate()
        {
            if (_flying && _cam != null)
                _cam.transform.SetPositionAndRotation(_flyPos, _flyRot);   // hart halten
        }

        // LateUpdate allein reicht nicht. Stirbt der Spieler mitten im Rundgang,
        // uebernimmt die Zuschauerkamera - und die sucht sich Camera.main,
        // also seit dem MainCamera-Kennzeichen genau unsere eigene Kamera.
        // Laeuft sie nach uns, gewinnt sie, und ab da zeigen alle Bilder
        // dasselbe: so waren 24/25 und 26/27 paarweise gleich, aufgenommen im
        // Abstand einer Sekunde vom selben Fleck.
        //
        // Der Zeitpunkt unmittelbar vor dem Zeichnen kommt nach jedem
        // LateUpdate. Wer die Kamera vorher verschoben hat, ist damit egal.
        void OnEnable()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += VorDemZeichnen;
        }

        void OnDisable()
        {
            UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= VorDemZeichnen;
        }

        void VorDemZeichnen(UnityEngine.Rendering.ScriptableRenderContext ctx, Camera cam)
        {
            if (_flying && _cam != null && cam == _cam)
                _cam.transform.SetPositionAndRotation(_flyPos, _flyRot);
        }

        static void Capture(string dir, string name)
        {
            string path = Path.Combine(dir, $"{name}.png");
            ScreenCapture.CaptureScreenshot(path, 1);
            Debug.Log($"[Infront] AutoShot: {path}");
        }
    }
}
