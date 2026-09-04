using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Profiling;

namespace Infront
{
    /// <summary>
    /// Leistungsanzeige zum Selber-Ablesen (der Entwickler kann das Spiel auf
    /// diesem Rechner nicht ansehen - also zeigt es das Spiel selbst).
    ///
    ///  - Bildrate: aktuell, geglättet, Minimum/Maximum und der 1%-Tiefpunkt
    ///    (die schlechtesten Frames - dort merkt man Ruckler)
    ///  - Frame-Zeit in Millisekunden
    ///  - Arbeitsspeicher (von Unity belegt)
    ///  - aktive Tonquellen
    ///  - Auflösung, GPU, Bild-Einstellung, VSync
    ///  - kleiner Balken-Verlauf der letzten ~2 Sekunden
    ///
    /// **F3** blendet die Anzeige ein und aus. Standardmäßig AUS - sie ist ein
    /// Entwickler-Hilfsmittel, kein Teil des Spiels. Überlebt den Szenenwechsel
    /// (wie <see cref="AudioService"/>), wird per <see cref="GameFlow"/>
    /// gestartet.
    ///
    /// Alles per Code (OnGUI), keine Assets, kein Fremdpaket - passt zum Rest
    /// des Projekts und ist headless prüfbar (die Zahlen, nicht wie es aussieht).
    /// </summary>
    public sealed class PerfOverlay : MonoBehaviour
    {
        const int HistoryLen = 120;          // ~2 s bei 60 fps
        const float SampleEvery = 0.5f;      // Sekunden zwischen Min/Max/1%-Neuberechnung

        bool _visible;
        float _dt;                           // geglättete Frame-Zeit
        readonly float[] _history = new float[HistoryLen];
        int _historyHead;
        int _historyCount;

        float _fpsMin = float.MaxValue;
        float _fpsMax;
        float _fps1Low;
        float _sampleTimer;

        GUIStyle _style;
        Texture2D _bg;
        Texture2D _bar;
        readonly StringBuilder _sb = new StringBuilder(256);

        // ---- Test-Haken (wie es aussieht, ist nicht prüfbar) ----
        public bool VisibleForTests => _visible;
        public float SmoothFpsForTests => _dt > 0f ? 1f / _dt : 0f;
        public float OnePercentLowForTests => _fps1Low;
        public float MaxFpsForTests => _fpsMax;
        public int SamplesForTests => _historyCount;

        /// <summary>Sorgt dafür, dass es die Anzeige gibt (GameFlow ruft das,
        /// Tests können es auch direkt).</summary>
        public static PerfOverlay Ensure()
        {
            var existing = FindAnyObjectByType<PerfOverlay>();
            if (existing != null) return existing;
            var go = new GameObject("PerfOverlay");
            var po = go.AddComponent<PerfOverlay>();
            DontDestroyOnLoad(go);
            return po;
        }

        void Awake()
        {
            for (int i = 0; i < HistoryLen; i++) _history[i] = 1f / 60f;
            _dt = 1f / 60f;

            _bg = SolidTexture(new Color(0f, 0f, 0f, 0.72f));
            _bar = SolidTexture(Color.white);
        }

        void OnDestroy()
        {
            if (_bg != null) Destroy(_bg);
            if (_bar != null) Destroy(_bar);
        }

        static Texture2D SolidTexture(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }

        /// <summary>Ein-/Ausblenden (F3 ruft das, Tests auch).</summary>
        public void Toggle() => _visible = !_visible;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[Key.F3].wasPressedThisFrame)
                Toggle();

            // Frame-Zeit glätten (exponentiell) und in den Verlauf schreiben.
            float raw = Mathf.Max(Time.unscaledDeltaTime, 0.00001f);
            _dt += (raw - _dt) * 0.1f;

            _history[_historyHead] = raw;
            _historyHead = (_historyHead + 1) % HistoryLen;
            if (_historyCount < HistoryLen) _historyCount++;

            _sampleTimer += raw;
            if (_sampleTimer >= SampleEvery)
            {
                _sampleTimer = 0f;
                Recompute();
            }
        }

        void Recompute()
        {
            if (_historyCount == 0) return;

            // Frame-Zeiten -> FPS, sortieren für den 1%-Tiefpunkt.
            var fps = new float[_historyCount];
            for (int i = 0; i < _historyCount; i++)
            {
                float ft = _history[i];
                fps[i] = ft > 0f ? 1f / ft : 0f;
            }
            System.Array.Sort(fps);

            _fpsMin = fps[0];
            _fpsMax = fps[_historyCount - 1];

            // 1%-Tiefpunkt: Mittel der schlechtesten 1 % (mind. 1 Frame).
            int worst = Mathf.Max(1, _historyCount / 100);
            float sum = 0f;
            for (int i = 0; i < worst; i++) sum += fps[i];
            _fps1Low = sum / worst;
        }

        void OnGUI()
        {
            if (!_visible) return;
            if (Event.current.type != EventType.Repaint) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    richText = true,
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                    normal = { textColor = Color.white },
                };

            float fps = _dt > 0f ? 1f / _dt : 0f;
            float ms = _dt * 1000f;
            long ram = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            long ramReserved = Profiler.GetTotalReservedMemoryLong() / (1024 * 1024);
            int voices = CountAudioSources();

            _sb.Clear();
            _sb.Append("<b>INFRONT  F3</b>\n");
            _sb.Append(Colorize($"{fps,5:0.0} FPS", fps)).Append($"   {ms,5:0.0} ms\n");
            _sb.Append($"min {_fpsMin,4:0}   max {_fpsMax,4:0}   1% ")
               .Append(Colorize($"{_fps1Low,3:0}", _fps1Low)).Append('\n');
            _sb.Append($"RAM {ram} / {ramReserved} MB\n");
            _sb.Append($"Audio  {voices} sources active\n");
            _sb.Append($"{Screen.width}x{Screen.height}   VSync {QualitySettings.vSyncCount}\n");
            _sb.Append($"Graphics: {GameSettings.GraphicsQuality}\n");
            _sb.Append(SystemInfo.graphicsDeviceName);

            const float w = 260f, x = 8f, y = 8f;
            float h = 128f + 34f;   // Text + Verlaufsbalken

            GUI.DrawTexture(new Rect(x, y, w, h), _bg);
            GUI.Label(new Rect(x + 8f, y + 6f, w - 16f, h - 40f), _sb.ToString(), _style);

            DrawGraph(new Rect(x + 8f, y + h - 30f, w - 16f, 24f));
        }

        void DrawGraph(Rect r)
        {
            GUI.DrawTexture(r, _bg);
            if (_historyCount < 2) return;

            // Skala: 0..90 fps auf die Höhe.
            const float scaleMax = 90f;
            int n = _historyCount;
            float bw = r.width / n;

            for (int i = 0; i < n; i++)
            {
                int idx = (_historyHead - n + i + HistoryLen * 2) % HistoryLen;
                float ft = _history[idx];
                float f = ft > 0f ? 1f / ft : 0f;
                float hNorm = Mathf.Clamp01(f / scaleMax);
                float barH = hNorm * r.height;

                GUI.color = f >= 58f ? new Color(0.4f, 0.85f, 0.4f, 0.9f)
                          : f >= 45f ? new Color(0.95f, 0.8f, 0.3f, 0.9f)
                          : new Color(0.9f, 0.35f, 0.3f, 0.95f);
                GUI.DrawTexture(new Rect(r.x + i * bw, r.yMax - barH, Mathf.Max(1f, bw - 0.5f), barH), _bar);
            }

            // 60-fps-Linie
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            float y60 = r.yMax - (60f / scaleMax) * r.height;
            GUI.DrawTexture(new Rect(r.x, y60, r.width, 1f), _bar);
            GUI.color = Color.white;
        }

        static string Colorize(string text, float fps)
        {
            string hex = fps >= 58f ? "6fd96f" : fps >= 45f ? "f2cc4d" : "e65a4d";
            return $"<color=#{hex}>{text}</color>";
        }

        static int CountAudioSources()
        {
            int n = 0;
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying) n++;
            return n;
        }
    }
}
