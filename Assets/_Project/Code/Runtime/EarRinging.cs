using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Realismus-Etappe Schritt 7: Ohrenklingeln nach nahen Explosionen.
    ///
    /// Bisher gab es nur einen hohen Pfeifton - man hoerte alles andere
    /// unveraendert weiter. Jetzt daempft das Klingeln auch den Rest: die
    /// Lautstaerke faellt und alles klingt dumpf, weil ein Tiefpass ueber dem
    /// Hoerer liegt. Das klingt danach langsam ab.
    ///
    /// Haengt am Hoerer (AudioListener), also an der Kamera. Rein oertlich.
    ///
    /// NICHT pruefbar: ob es gut klingt. Pruefbar: die Daempfung steigt bei
    /// einer nahen Explosion, faellt danach wieder, und weit entfernte
    /// Explosionen loesen sie nicht aus.
    /// </summary>
    public sealed class EarRinging : MonoBehaviour
    {
        [Header("Ausloesen")]
        // Ab dieser Entfernung tut eine Explosion den Ohren nichts mehr.
        [SerializeField] float _maxEntfernung = 14f;
        // Wie stark eine Explosion direkt daneben wirkt.
        [SerializeField, Range(0f, 1f)] float _staerkeNah = 1f;

        [Header("Abklingen")]
        // Sekunden von voller Daempfung bis nichts mehr.
        // 3.5 s Daempfung. Der Pfeifton selbst ist ein eigener Ton und haelt
        // laenger an - dumpf hoert man aber nur waehrend dieser Zeit. Laenger
        // wirkte im Test wie ein Defekt statt wie eine Nachwirkung.
        [SerializeField] float _abklingzeit = 2f;

        [Header("Klang")]
        // Grenzfrequenz bei voller Daempfung - alles darueber ist weg.
        // Tief angesetzt, damit die Daempfung direkt nach der Explosion wirklich
        // dumpf ist. Weil sie linear zurueckfaehrt, ist das Gehoer trotzdem
        // schnell wieder brauchbar.
        [SerializeField] float _cutoffTaub = 200f;

        float _level;
        AudioLowPassFilter _filter;

        public float Level01 => Mathf.Clamp01(_level);
        public float MaxEntfernungForTests => _maxEntfernung;

        // Es gibt mehr als einen Hoerer im Projekt (Menue-Kamera und
        // Arena-Kamera). Ein einzelner statischer Verweis waere Zufall - wer
        // zuletzt aufwacht, gewinnt. Genau daran ist die erste Fassung
        // gescheitert: die Explosion ging an die Menue-Kamera, waehrend der
        // Tiefpass der Arena-Kamera unberuehrt blieb. Deshalb eine Liste, und
        // jeder Hoerer rechnet seine eigene Entfernung aus.
        static readonly System.Collections.Generic.List<EarRinging> _alle = new();

        /// <summary>Der zuletzt erwachte Hoerer. Nur fuer alten Code.</summary>
        public static EarRinging Instance => _alle.Count > 0 ? _alle[_alle.Count - 1] : null;

        /// <summary>
        /// Eine Explosion an dieser Stelle - geht an jeden Hoerer. Jeder
        /// entscheidet selbst, ob er nah genug dran war.
        /// </summary>
        public static void ExplosionAt(Vector3 position)
        {
            // Erst sicherstellen, dass der tatsaechliche Hoerer eins hat.
            // Die Szene bringt das Ohrenklingeln zwar an ihrer Kamera mit,
            // aber welche Kamera am Ende hoert, steht erst zur Laufzeit fest -
            // im Testlauf war es eine andere, und der Tiefpass, den man
            // ablas, gehoerte gar keinem lebenden Ohr mehr.
            EnsureAmHoerer();

            for (int i = _alle.Count - 1; i >= 0; i--)
            {
                if (_alle[i] == null) { _alle.RemoveAt(i); continue; }
                _alle[i].Explosion(position);
            }
        }

        /// <summary>
        /// Haengt das Ohrenklingeln an den gerade aktiven AudioListener, falls
        /// es dort noch keins gibt. Ohne Hoerer passiert nichts.
        /// </summary>
        public static EarRinging EnsureAmHoerer()
        {
            EarRinging ergebnis = null;

            // Der AudioListener ist das Ohr...
            var listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener != null) ergebnis = AnObjekt(listener.gameObject);

            // ...aber der Tiefpass muss auch auf der Kamera liegen, durch die
            // gespielt wird. Im Testlauf waren das zwei verschiedene Objekte,
            // und der Filter auf der Kamera blieb dadurch auf einem alten Wert
            // stehen, den niemand mehr zurueckgesetzt hat.
            var cam = Camera.main;
            if (cam != null && (listener == null || cam.gameObject != listener.gameObject))
            {
                var anKamera = AnObjekt(cam.gameObject);
                if (ergebnis == null) ergebnis = anKamera;
            }

            return ergebnis;
        }

        static EarRinging AnObjekt(GameObject go)
        {
            var vorhanden = go.GetComponent<EarRinging>();
            return vorhanden != null ? vorhanden : go.AddComponent<EarRinging>();
        }

        /// <summary>Gibt es ueberhaupt einen Hoerer mit Ohrenklingeln?</summary>
        public static bool Vorhanden => _alle.Count > 0;

        void Awake()
        {
            if (!_alle.Contains(this)) _alle.Add(this);
            _filter = GetComponent<AudioLowPassFilter>();
            if (_filter == null) _filter = gameObject.AddComponent<AudioLowPassFilter>();
            _filter.cutoffFrequency = 22000f;
        }

        void OnDestroy()
        {
            _alle.Remove(this);
            if (_alle.Count == 0 && AudioService.Instance != null)
                AudioService.Instance.Deafness = 0f;
        }

        /// <summary>
        /// Eine Explosion an dieser Stelle. Je naeher, desto staerker das
        /// Klingeln. Weiter weg als <see cref="_maxEntfernung"/> tut nichts.
        /// </summary>
        public void Explosion(Vector3 position)
        {
            float d = Vector3.Distance(transform.position, position);
            if (d >= _maxEntfernung) return;

            float k = 1f - d / _maxEntfernung;              // 1 direkt daneben
            // Hoch 1.5 statt quadratisch: quadratisch war zu zahm, eine
            // Explosion zwei Meter daneben blieb kaum hoerbar gedaempft.
            float neu = _staerkeNah * Mathf.Pow(k, 1.5f);
            _level = Mathf.Clamp01(Mathf.Max(_level, neu));
        }

        /// <summary>Nur fuer Tests: die Daempfung direkt setzen.</summary>
        public void SetLevelForTests(float v) => _level = Mathf.Clamp01(v);

        void Update()
        {
            if (_level > 0f && _abklingzeit > 0f)
                _level = Mathf.Max(0f, _level - Time.deltaTime / _abklingzeit);

            if (_filter != null)
            {
                // Unter 2 % ganz abschalten: das spart Rechenzeit und stellt
                // sicher, dass das Gehoer wirklich wieder unveraendert ist -
                // ein Tiefpass bei 21 kHz waere zwar unhoerbar, aber eben auch
                // nicht "aus".
                bool spuerbar = _level > 0.05f;
                _filter.enabled = spuerbar;
                _filter.cutoffFrequency = spuerbar
                    ? Mathf.Lerp(22000f, _cutoffTaub, _level)
                    : 22000f;
            }

            // Mehrere Hoerer teilen sich einen AudioService. Deshalb setzt nur
            // der am staerksten betroffene die Daempfung - sonst wuerde ein
            // unbeteiligter Hoerer sie jeden Frame wieder auf null druecken.
            if (AudioService.Instance != null)
            {
                float staerkster = 0f;
                for (int i = 0; i < _alle.Count; i++)
                    if (_alle[i] != null) staerkster = Mathf.Max(staerkster, _alle[i]._level);
                AudioService.Instance.Deafness = staerkster;
            }
        }
    }
}
