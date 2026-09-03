using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Schritt-Geräusche für Spieler und Bots. Läuft auf jeder Instanz rein
    /// örtlich - kein Netzwerk nötig, weil die Position ohnehin über
    /// NetworkTransform verteilt wird und wir daraus die Geschwindigkeit
    /// ableiten.
    ///
    /// Die Lautstärke hängt am Tempo: langsames Gehen ist kaum zu hören,
    /// Sprinten weithin. Genau deshalb wird Sprinten zu einer Entscheidung -
    /// schnell da sein, aber verraten werden.
    /// </summary>
    public sealed class FootstepSounds : MonoBehaviour
    {
        // Schritt 7: an die neuen Tempi aus Schritt 2 angepasst. Vorher stand
        // SprintFrom auf 7.5 - der Sprint ist seit Schritt 2 aber nur noch
        // 7.2 m/s schnell. Sprinten waere damit lautlos geworden, also genau
        // das Gegenteil der Absicht.
        const float SilentBelow = 0.9f;   // darunter: kein Schritt
        const float SprintFrom  = 6.2f;   // ab hier: laute Schritte
        const float NormalFrom  = 2.8f;   // ab hier: normale Schritte

        // Untergrund: Metall klingt hell und hart, Beton neutral, Schutt weich.
        public enum Untergrund { Beton = 0, Metall = 1, Schutt = 2 }

        Health _health;
        NetworkObject _netObject;
        Vector3 _lastPos;
        float _timer;

        void Awake()
        {
            _health = GetComponent<Health>();
            _netObject = GetComponent<NetworkObject>();
            _lastPos = transform.position;
        }

        void OnEnable() => _lastPos = transform.position;

        void Update()
        {
            if (_health != null && !_health.IsAlive) { _timer = 0f; _lastPos = transform.position; return; }

            Vector3 delta = transform.position - _lastPos;
            _lastPos = transform.position;

            // Großer Sprung in einem Frame = Teleport (Rundenstart) -> kein Schritt.
            if (delta.sqrMagnitude > 9f) { _timer = 0f; return; }

            float speed = new Vector2(delta.x, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

            if (speed < SilentBelow) { _timer = 0f; return; }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = StepIntervalFor(speed);

            var audio = AudioService.Instance;
            if (audio == null) return;

            // Die eigenen Schritte etwas leiser - das Ohr sitzt direkt darüber.
            bool mine = _netObject != null && _netObject.IsOwner;
            float vol = mine ? 0.35f : 1f;
            // Schritt 7: der Untergrund faerbt den Schritt. Es gibt keine
            // eigenen Aufnahmen je Belag - stattdessen aendern sich Tonhoehe,
            // Lautstaerke und Streuung. Das reicht, um Metall von Beton zu
            // unterscheiden, ohne neue Dateien zu brauchen.
            var boden = UntergrundUnter(transform.position);
            audio.PlayAt(TierFor(speed), transform.position,
                vol * LautstaerkeFaktor(boden), StreuungFuer(boden));
        }

        /// <summary>Welche Schritt-Lautstärke zu diesem Tempo passt.</summary>
        public static SoundId TierFor(float speed)
        {
            if (speed >= SprintFrom) return SoundId.SchrittLaut;
            if (speed >= NormalFrom) return SoundId.SchrittNormal;
            return SoundId.SchrittLeise;
        }

        /// <summary>Sekunden bis zum nächsten Schritt - schneller bei mehr Tempo.</summary>
        /// <summary>
        /// Was liegt unter den Fuessen? Es gibt keine Material-Kennzeichnung in
        /// der Karte, deshalb wird am Namen des getroffenen Objekts erkannt -
        /// die Karte wird ohnehin komplett aus Code gebaut, die Namen sind also
        /// verlaesslich.
        /// </summary>
        public static Untergrund UntergrundUnter(Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * 0.4f, Vector3.down,
                                 out RaycastHit hit, 2.2f,
                                 1 << 0, QueryTriggerInteraction.Ignore))
                return Untergrund.Beton;

            string n = hit.collider.name;
            if (n.Contains("Platform") || n.Contains("Dais") || n.Contains("Ramp")
                || n.Contains("Balc") || n.Contains("Steg") || n.Contains("Gitter"))
                return Untergrund.Metall;
            if (n.Contains("Fleck") || n.Contains("Schutt") || n.Contains("Rubble")
                || n.Contains("Sand"))
                return Untergrund.Schutt;
            return Untergrund.Beton;
        }

        /// <summary>Metall traegt weiter, Schutt schluckt.</summary>
        public static float LautstaerkeFaktor(Untergrund u) => u switch
        {
            Untergrund.Metall => 1.35f,
            Untergrund.Schutt => 0.7f,
            _ => 1f,
        };

        /// <summary>Schutt klingt unregelmaessig, Metall gleichmaessig hart.</summary>
        public static float StreuungFuer(Untergrund u) => u switch
        {
            Untergrund.Metall => 0.05f,
            Untergrund.Schutt => 0.22f,
            _ => 0.08f,
        };

        public static float StepIntervalFor(float speed)
        {
            return Mathf.Clamp(3.6f / Mathf.Max(speed, 0.5f), 0.28f, 0.6f);
        }
    }
}
