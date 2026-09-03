using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Der Krieg drumherum. Zwischen den Schüssen war es bisher komplett still -
    /// das war der grösste Grund, warum sich die Karte "tot" anfühlte. Dieses
    /// Bauteil legt eine Klangkulisse darüber:
    ///
    ///  - ein dauerhaftes leises Windbett (eigene Schleifen-AudioSource),
    ///  - in unregelmässigen Abständen ferne Ereignisse: Dauerfeuer,
    ///    Artillerie-Einschläge, ein vorbeiziehender Hubschrauber, knarzendes
    ///    Metall - jeweils 3D an einer weit entfernten Stelle.
    ///
    /// Alles rein Stimmung: keine Spiel-Logik, keine Bot-Wahrnehmung
    /// (<see cref="SoundEvents"/> wird NICHT gefüttert), keine Balance-Wirkung.
    /// In der Kaufzeit ist es ruhiger. Ferne Ereignisse pausieren, solange der
    /// MatchManager für Tests ausgesetzt ist.
    ///
    /// Fehlt eine echte Tondatei, nimmt der <see cref="AudioService"/> den
    /// Platzhalter aus <see cref="ProceduralSfx"/> - später eintauschbar unter
    /// <c>Assets/_Project/Audio/Resources/</c> (wind.wav, artillerie.wav ...).
    ///
    /// NICHT prüfbar: wie es klingt. Prüfbar: Wind läuft in Schleife, ferne
    /// Ereignisse feuern in einem Zeitfenster, Kaufzeit senkt die Lautstärke.
    /// </summary>
    public sealed class AmbientWar : MonoBehaviour
    {
        // Wie laut das Windbett grundsätzlich ist (vor Gesamtlautstärke/Wetter).
        const float WindBase = 0.16f;

        AudioSource _wind;
        WeatherDirector _weather;
        MatchManager _match;

        float _windGain;          // 0..1, blendet weich
        float _nextEventIn;
        float _clock;

        // ---- Test-Haken ----
        public bool WindRunningForTests => _wind != null && _wind.isPlaying && _wind.loop;
        public float WindVolumeForTests => _wind != null ? _wind.volume : 0f;
        public int EventCountForTests { get; private set; }
        public SoundId? LastEventForTests { get; private set; }
        /// <summary>Test kann die Kaufzeit erzwingen (true) oder ausschliessen (false).
        /// null = normal aus dem MatchManager lesen.</summary>
        public bool? BuyTimeOverrideForTests { get; set; }

        void Start()
        {
            var go = new GameObject("WindBed");
            go.transform.SetParent(transform, false);
            _wind = go.AddComponent<AudioSource>();
            _wind.clip = AudioService.Instance != null
                ? AudioService.Instance.Resolve(SoundId.Wind)
                : ProceduralSfx.Build(SoundId.Wind);
            _wind.loop = true;
            _wind.spatialBlend = 0f;      // 2D, direkt am Ohr
            _wind.volume = 0f;
            _wind.playOnAwake = false;
            _wind.Play();

            _nextEventIn = Random.Range(9f, 16f);   // erstes Ereignis nicht sofort
        }

        void Update()
        {
            if (_weather == null) _weather = FindAnyObjectByType<WeatherDirector>();
            if (_match == null) _match = MatchManager.Instance;

            float dt = Time.deltaTime;
            _clock += dt;

            bool buyTime = BuyTimeOverrideForTests ?? (_match != null && _match.IsBuyTime);

            // --- Windbett -------------------------------------------------
            float weatherMul = 1f;
            if (_weather != null)
            {
                switch (_weather.CurrentWeatherForTests)
                {
                    case WeatherKind.Staubwind: weatherMul = 1.8f; break;
                    case WeatherKind.Rauch:     weatherMul = 1.3f; break;
                    case WeatherKind.Dunst:     weatherMul = 1.1f; break;
                }
            }
            float targetGain = (buyTime ? 0.6f : 1f) * weatherMul;
            _windGain = Mathf.MoveTowards(_windGain, targetGain, dt * 1.2f);
            float master = Mathf.Clamp01(GameSettings.SfxVolume);
            if (_wind != null) _wind.volume = WindBase * _windGain * master;

            // --- Ferne Ereignisse --------------------------------------
            bool eventsAllowed = _match == null || !_match.SuspendedForTests;
            if (!eventsAllowed) return;

            _nextEventIn -= dt;
            if (_nextEventIn <= 0f)
            {
                FireEvent();
                float baseGap = buyTime ? Random.Range(14f, 26f) : Random.Range(7f, 15f);
                _nextEventIn = baseGap;
            }
        }

        void FireEvent()
        {
            var audio = AudioService.Instance;
            if (audio == null) return;

            // Gewichtete Auswahl.
            float r = Random.value;
            SoundId id;
            if (r < 0.45f) id = SoundId.FernesFeuergefecht;
            else if (r < 0.65f) id = SoundId.Artillerie;
            else if (r < 0.82f) id = SoundId.Hubschrauber;
            else id = SoundId.MetallKnarzen;

            Vector3 listener = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            Vector3 pos = PickPositionFor(id, listener);

            float vol; float delay = 0f;
            switch (id)
            {
                case SoundId.Artillerie:
                    vol = 0.55f;
                    delay = Mathf.Min(Vector3.Distance(listener, pos) / 340f, 1.2f);   // Schall braucht
                    break;
                case SoundId.Hubschrauber:  vol = 0.4f; break;
                case SoundId.MetallKnarzen: vol = 0.5f; break;
                default:                    vol = 0.35f; break;   // fernes Feuergefecht
            }

            audio.PlayAt(id, pos, vol, 0.06f, delay);
            EventCountForTests++;
            LastEventForTests = id;
        }

        static Vector3 PickPositionFor(SoundId id, Vector3 listener)
        {
            // weit weg, ausserhalb der Kampfzone
            float ang = Random.value * Mathf.PI * 2f;
            float dist = Random.Range(55f, 90f);
            var p = listener + new Vector3(Mathf.Cos(ang) * dist, 0f, Mathf.Sin(ang) * dist);
            p.y = id == SoundId.Hubschrauber ? Random.Range(25f, 45f)
                : id == SoundId.MetallKnarzen ? Random.Range(3f, 8f)
                : Random.Range(1f, 12f);
            // Knarzen kommt aus der Nähe (eine Wand ächzt), nicht von weit weg.
            if (id == SoundId.MetallKnarzen)
                p = listener + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * Random.Range(8f, 20f)
                    + Vector3.up * Random.Range(3f, 8f);
            return p;
        }

        /// <summary>Nur Tests: sofort ein fernes Ereignis auslösen.</summary>
        public void FireEventForTests() => FireEvent();
    }
}
