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
        const float SilentBelow = 1.2f;   // darunter: kein Schritt
        const float SprintFrom  = 7.5f;   // ab hier: laute Schritte

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
            audio.PlayAt(TierFor(speed), transform.position, vol, 0.08f);
        }

        /// <summary>Welche Schritt-Lautstärke zu diesem Tempo passt.</summary>
        public static SoundId TierFor(float speed)
        {
            if (speed >= SprintFrom) return SoundId.SchrittLaut;
            if (speed >= 3.5f) return SoundId.SchrittNormal;
            return SoundId.SchrittLeise;
        }

        /// <summary>Sekunden bis zum nächsten Schritt - schneller bei mehr Tempo.</summary>
        public static float StepIntervalFor(float speed)
        {
            return Mathf.Clamp(3.6f / Mathf.Max(speed, 0.5f), 0.28f, 0.6f);
        }
    }
}
