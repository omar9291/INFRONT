using UnityEngine;

namespace Infront
{
    /// <summary>Die fünf Wetterlagen. Reihenfolge = Index in <see cref="WeatherDirector.Presets"/>.</summary>
    public enum WeatherKind { Klar = 0, Dunst = 1, Staubwind = 2, Bodennebel = 3, Rauch = 4 }

    /// <summary>
    /// Wetter pro Runde - REIN OPTISCH. Auf ausdrückliche Ansage des Nutzers
    /// ändert sich die Sichtweite im Spiel NICHT:
    ///  - der Distanz-Nebel bleibt unter <see cref="MaxSafeFogDensity"/>
    ///    (auf 60 m ist ein Gegner immer klar lesbar),
    ///  - die Bot-Sichtweite wird nirgends angefasst,
    ///  - die schwere Stimmung kommt aus Farbe, Sonnenstärke, Staub
    ///    (<see cref="AtmosphereDust"/>) und der flachen Nebelbank
    ///    (<see cref="GroundFog"/>, unter Hüfthöhe - verdeckt niemanden).
    ///
    /// Host-Modus V1 hat nur den einen echten Spieler, deshalb kein
    /// NetworkVariable, sondern eine feste Tabelle plus ein Sitzungs-Zufall.
    /// Jede Runde wird weich (~2 s) auf die neue Lage geblendet.
    ///
    /// Hängt an einem eigenen Objekt in der Arena (SceneBuilder). Bei
    /// "Bild: Schlicht" macht der Director nichts - der PostFxController hat
    /// den Nebel dann schon aus.
    ///
    /// NICHT prüfbar: wie es aussieht. Prüfbar: Lage wechselt zwischen Runden,
    /// jede Lage bleibt im sicheren Nebel-Band, "Schlicht" schaltet alles ab.
    /// </summary>
    public sealed class WeatherDirector : MonoBehaviour
    {
        public struct Preset
        {
            public Color Fog;
            public float FogDensity;
            public Color DustTint;
            public float Dust01;
            public float GroundFog01;
            public float SunMul;

            public static Preset Lerp(Preset a, Preset b, float t) => new Preset
            {
                Fog = Color.Lerp(a.Fog, b.Fog, t),
                FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
                DustTint = Color.Lerp(a.DustTint, b.DustTint, t),
                Dust01 = Mathf.Lerp(a.Dust01, b.Dust01, t),
                GroundFog01 = Mathf.Lerp(a.GroundFog01, b.GroundFog01, t),
                SunMul = Mathf.Lerp(a.SunMul, b.SunMul, t),
            };
        }

        /// <summary>Obergrenze für die Distanz-Nebeldichte. Darüber würde die
        /// Sichtweite spürbar sinken - das ist ausgeschlossen. Bei dieser Dichte
        /// kommen von einem Gegner auf 60 m noch rund 50 % Kontrast an.</summary>
        public const float MaxSafeFogDensity = 0.013f;

        public static readonly Preset[] Presets =
        {
            // Klar - kühl, fast wie bisher
            new Preset { Fog = new Color(0.55f, 0.60f, 0.68f), FogDensity = 0.0055f,
                DustTint = new Color(0.78f, 0.80f, 0.86f), Dust01 = 0.25f, GroundFog01 = 0.00f, SunMul = 1.00f },
            // Dunst - milchiger Schleier
            new Preset { Fog = new Color(0.60f, 0.62f, 0.66f), FogDensity = 0.0100f,
                DustTint = new Color(0.74f, 0.76f, 0.80f), Dust01 = 0.55f, GroundFog01 = 0.30f, SunMul = 0.88f },
            // Staubwind - warm-braun, viel Staub
            new Preset { Fog = new Color(0.62f, 0.52f, 0.40f), FogDensity = 0.0105f,
                DustTint = new Color(0.80f, 0.64f, 0.44f), Dust01 = 1.00f, GroundFog01 = 0.18f, SunMul = 0.80f },
            // Bodennebel - dichte Bank unten, Fernsicht bleibt frei
            new Preset { Fog = new Color(0.52f, 0.56f, 0.60f), FogDensity = 0.0070f,
                DustTint = new Color(0.70f, 0.74f, 0.78f), Dust01 = 0.40f, GroundFog01 = 1.00f, SunMul = 0.92f },
            // Rauch nach Beschuss - grau, Sonne stark gedämpft
            new Preset { Fog = new Color(0.34f, 0.34f, 0.36f), FogDensity = 0.0120f,
                DustTint = new Color(0.46f, 0.46f, 0.48f), Dust01 = 0.70f, GroundFog01 = 0.55f, SunMul = 0.65f },
        };

        static int _seedSalt;

        MatchManager _hooked;
        Light _sun;
        float _sunBase = 1f;
        GroundFog _groundFog;
        AtmosphereDust[] _dust = System.Array.Empty<AtmosphereDust>();

        WeatherKind _kind = WeatherKind.Klar;
        System.Random _rng;

        Preset _cur;
        Preset _tgt;
        float _refindTimer;

        public WeatherKind CurrentWeatherForTests => _kind;
        public bool BlendingForTests => Mathf.Abs(_cur.FogDensity - _tgt.FogDensity) > 0.00005f
                                        || Mathf.Abs(_cur.GroundFog01 - _tgt.GroundFog01) > 0.01f;

        void Awake()
        {
            _rng = new System.Random(System.Environment.TickCount + (_seedSalt++ * 7919));
            _cur = _tgt = Presets[0];
        }

        void OnEnable() => FindRefs();
        void OnDestroy() => Unhook();

        void FindRefs()
        {
            _groundFog = FindAnyObjectByType<GroundFog>();
            _dust = FindObjectsByType<AtmosphereDust>(FindObjectsSortMode.None);

            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional)
                {
                    _sun = l;
                    _sunBase = l.intensity;
                    break;
                }
            }
        }

        void Unhook()
        {
            if (_hooked == null) return;
            _hooked.RoundStarted -= OnRoundStarted;
            _hooked = null;
        }

        void Update()
        {
            // An den MatchManager hängen, sobald es ihn gibt (wie MatchAudio).
            var mm = MatchManager.Instance;
            if (mm != _hooked)
            {
                Unhook();
                _hooked = mm;
                if (_hooked != null)
                {
                    _hooked.RoundStarted += OnRoundStarted;
                    if (_sun == null || _groundFog == null || _dust.Length == 0) FindRefs();
                    PickWeather(first: true);
                }
            }

            // Solange noch etwas fehlt, alle ~1 s erneut suchen (Objekte der
            // Szene können bei OnEnable noch nicht alle da gewesen sein).
            if (_sun == null || _groundFog == null || _dust.Length == 0)
            {
                _refindTimer -= Time.deltaTime;
                if (_refindTimer <= 0f) { FindRefs(); _refindTimer = 1f; }
            }

            // Schlicht: Finger weg. Sonne zurücksetzen, Nebelbank + Staub aus.
            if (GameSettings.GraphicsQuality != GameSettings.Graphics.Voll)
            {
                if (_sun != null) _sun.intensity = _sunBase;
                _groundFog?.SetTarget(0f, Color.white);
                foreach (var d in _dust) if (d != null) d.SetWeather(0f, Color.white);
                return;
            }

            // Weiche exponentielle Angleichung - nach ~2 s praktisch angekommen.
            float k = 1f - Mathf.Exp(-Time.deltaTime / 0.6f);
            _cur = Preset.Lerp(_cur, _tgt, k);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = _cur.Fog;
            RenderSettings.fogDensity = Mathf.Min(_cur.FogDensity, MaxSafeFogDensity);

            if (_sun != null) _sun.intensity = _sunBase * _cur.SunMul;

            _groundFog?.SetTarget(_cur.GroundFog01, _cur.DustTint);
            foreach (var d in _dust) if (d != null) d.SetWeather(_cur.Dust01, _cur.DustTint);
        }

        void OnRoundStarted() => PickWeather(first: false);

        void PickWeather(bool first)
        {
            int n = Presets.Length;
            int idx;
            if (first)
            {
                idx = _rng.Next(n);
            }
            else
            {
                // garantiert eine ANDERE Lage als bisher
                idx = ((int)_kind + 1 + _rng.Next(n - 1)) % n;
            }

            _kind = (WeatherKind)idx;
            _tgt = Presets[idx];
            if (first) _cur = _tgt;
        }

        // ---- Test-Haken (Optik selbst ist nicht prüfbar) ----

        /// <summary>Wählt wie bei einem echten Rundenstart die nächste Lage und
        /// gibt sie zurück. Für Tests, weil der MatchManager dort ausgesetzt ist.</summary>
        public WeatherKind PickNextForTests()
        {
            PickWeather(first: false);
            return _kind;
        }

        /// <summary>Eine bestimmte Lage erzwingen (Blende läuft normal an).</summary>
        public void ForceWeatherForTests(WeatherKind kind)
        {
            _kind = kind;
            _tgt = Presets[(int)kind];
        }
    }
}
