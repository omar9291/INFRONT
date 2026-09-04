using UnityEngine;
using UnityEngine.AI;

namespace Infront
{
    /// <summary>
    /// Gibt dem Bot dasselbe Gewicht wie dem Spieler.
    ///
    /// Warum es das gibt: seit dem Gewichts-Umbau hat der Spieler Anlauf,
    /// Bremsweg, Landestarre, Atem und Tempoverlust beim Bluten. Der Bot lief
    /// weiter mit einer festen NavMeshAgent-Geschwindigkeit - also
    /// gleichmaessig, sofort auf Tempo und ohne jeden Nachteil. Ein Gegner,
    /// der gleitet, macht jede Grafik-Arbeit wieder kaputt.
    ///
    /// Der Weg bleibt beim NavMeshAgent - der kann Wege suchen, und das soll er
    /// auch weiter tun. Hier wird nur bestimmt, WIE SCHNELL er ihn gehen darf
    /// und was das Tempo fuers Schiessen bedeutet.
    ///
    /// Bewusst nicht getan: den Bot auf den CharacterController des Spielers
    /// umstellen. Dann muesste die Wegsuche komplett neu gebaut werden, und der
    /// sichtbare Gewinn waere derselbe.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class BotLocomotion : MonoBehaviour
    {
        /// <summary>Was der Bot gerade vorhat. Bestimmt das Grundtempo.</summary>
        public enum Absicht
        {
            /// <summary>Umherlaufen. Gemuetliches Gehtempo.</summary>
            Gehen = 0,
            /// <summary>Hin zum letzten bekannten Ort. Hier wird gerannt.</summary>
            Rennen = 1,
            /// <summary>Feindkontakt. Waffe oben, also langsam.</summary>
            Kampf = 2,
            /// <summary>Stehen (geblendet, eingefroren, tot).</summary>
            Stehen = 3,
        }

        // Dieselben Verhaeltnisse wie beim Spieler: 4,6 gehen / 7,2 sprinten /
        // 2,6 im Anschlag. Als Vielfaches von BotStats.MoveSpeed, damit die
        // Schwierigkeitsstufen weiter wirken.
        [Header("Tempo (Vielfaches von BotStats.MoveSpeed)")]
        [SerializeField] float _gehFaktor = 1f;
        [SerializeField] float _rennFaktor = 1.56f;
        [SerializeField] float _kampfFaktor = 0.62f;

        [Header("Gewicht")]
        [Tooltip("Sekunden vom Gehen bis auf volles Renntempo.")]
        [SerializeField] float _rennAufbau = 1.1f;
        [Tooltip("Sekunden vom Rennen zurueck auf Gehtempo.")]
        [SerializeField] float _rennAbbau = 0.5f;
        [SerializeField] float _beschleunigung = 14f;
        [SerializeField] float _drehTempo = 220f;

        [Header("Schiessen")]
        [Tooltip("Sekunden vom Stehenbleiben, bis die Waffe wieder ruhig liegt.")]
        [SerializeField] float _setzZeit = 0.5f;
        [Tooltip("Zusaetzliche Streuung in Grad, solange der Bot in Bewegung ist.")]
        [SerializeField] float _laufStreuung = 4.5f;
        [Tooltip("Ab diesem Tempo gilt der Bot als rennend und schiesst nicht.")]
        [SerializeField] float _rennSchwelle = 5.6f;

        NavMeshAgent _agent;
        Bleeding _bleeding;
        BotStats _stats;

        float _rennRampe;      // 0 = Gehtempo, 1 = volles Renntempo
        float _ruhe;           // 0 = in Bewegung, 1 = steht ruhig
        Absicht _absicht = Absicht.Gehen;

        /// <summary>Rennt der Bot gerade wirklich (nicht nur: will er)?</summary>
        public bool Rennt { get; private set; }

        /// <summary>Soll der Bot stehen bleiben?</summary>
        public bool Angehalten { get; private set; }

        /// <summary>0 = volle Bewegung, 1 = steht ruhig. Steuert die Streuung.</summary>
        public float Ruhe01 => Mathf.Clamp01(_ruhe);

        /// <summary>
        /// Zusaetzlicher Zielfehler in Grad: aus Bewegung und aus verletzten
        /// Armen. Ein rennender, angeschossener Bot trifft schlechter - genau
        /// wie der Spieler.
        /// </summary>
        public float StreuungsMalus =>
            (1f - Ruhe01) * _laufStreuung + (_bleeding != null ? _bleeding.ZusatzStreuung : 0f);

        /// <summary>
        /// Darf der Bot schiessen? Wer rennt, hat die Waffe unten. Das ist der
        /// sichtbarste Unterschied zu vorher: Bots koennen nicht mehr im vollen
        /// Lauf treffen.
        /// </summary>
        public bool DarfSchiessen => !Rennt;

        /// <summary>Aktuelles Tempo in Meter pro Sekunde. Nur zum Ablesen.</summary>
        public float Tempo => _agent != null && _agent.enabled ? _agent.velocity.magnitude : 0f;

        public Absicht AbsichtForTests => _absicht;
        public float RennRampeForTests => _rennRampe;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _bleeding = GetComponent<Bleeding>();
        }

        /// <summary>Die Kennwerte des Bots. Ohne sie bleibt das Grundtempo, was es ist.</summary>
        public void SetStats(BotStats stats) => _stats = stats;

        /// <summary>Was der Bot gerade vorhat. Ruft das Hirn jeden Frame.</summary>
        public void SetzeAbsicht(Absicht a) => _absicht = a;

        void Update() => Schritt(Time.deltaTime);

        /// <summary>Ein Rechenschritt. Ausgelagert, damit Tests ihn ohne Warten treiben koennen.</summary>
        public void Schritt(float dt)
        {
            if (_agent == null || !_agent.enabled) return;
            if (dt <= 0f) return;

            float basis = _stats != null ? _stats.MoveSpeed : 4.5f;

            // Renn-Rampe: Anlauf und Auslauf. Ohne sie steht der Bot in einem
            // Frame auf vollem Tempo - das ist genau das Gleiten von vorher.
            bool willRennen = _absicht == Absicht.Rennen;
            float ziel = willRennen ? 1f : 0f;
            float rate = willRennen
                ? (_rennAufbau > 0f ? dt / _rennAufbau : 1f)
                : (_rennAbbau > 0f ? dt / _rennAbbau : 1f);
            _rennRampe = Mathf.MoveTowards(_rennRampe, ziel, rate);

            float faktor;
            switch (_absicht)
            {
                case Absicht.Stehen: faktor = 0f; break;
                case Absicht.Kampf:  faktor = _kampfFaktor; break;
                default:
                    faktor = Mathf.Lerp(_gehFaktor, _rennFaktor, _rennRampe);
                    break;
            }

            float tempo = basis * faktor;
            if (_bleeding != null) tempo *= _bleeding.TempoFaktor;

            _agent.speed = Mathf.Max(0f, tempo);
            _agent.acceleration = _beschleunigung;
            _agent.angularSpeed = _drehTempo;
            // isStopped darf nur angefasst werden, wenn der Agent wirklich auf
            // dem NavMesh steht - sonst wirft Unity einen Fehler.
            Angehalten = _absicht == Absicht.Stehen;
            if (_agent.isOnNavMesh) _agent.isStopped = Angehalten;

            // Rennt er wirklich? Nach dem Tempo, nicht nach der Absicht - ein
            // Bot, der gegen eine Wand laeuft, rennt nicht.
            Rennt = _agent.velocity.magnitude > _rennSchwelle;

            // Ruhe: steigt beim Stehen, faellt sofort beim Losgehen.
            bool steht = _agent.velocity.sqrMagnitude < 0.09f;   // < 0,3 m/s
            if (steht)
                _ruhe = _setzZeit > 0f ? Mathf.MoveTowards(_ruhe, 1f, dt / _setzZeit) : 1f;
            else
                _ruhe = Mathf.MoveTowards(_ruhe, 0f, dt * 6f);
        }

        // --- Tests ----------------------------------------------------------

        /// <summary>Nur fuer Tests: Tempo vortaeuschen, ohne NavMesh.</summary>
        public void SetTempoForTests(float mps)
        {
            Rennt = mps > _rennSchwelle;
            _ruhe = mps < 0.3f ? _ruhe : 0f;
        }

        public float RennSchwelleForTests => _rennSchwelle;
        public float LaufStreuungForTests => _laufStreuung;
    }
}
