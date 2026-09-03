using UnityEngine;
using Unity.Netcode;

namespace Infront
{
    /// <summary>
    /// Realismus-Etappe Schritt 5: Blutungen und Folgen von Zonentreffern.
    ///
    /// Ersetzt die reine Lebensanzeige nicht - sie laeuft weiter wie bisher.
    /// Diese Klasse legt sich daneben und nimmt ueber die Zeit zusaetzlich
    /// Leben weg, solange eine Blutung offen ist. Eine Blutung hoert **nicht**
    /// von selbst auf; nur ein Verbandspaket stoppt sie.
    ///
    /// Ausserdem gemerkt: Treffer an Beinen machen langsamer, Treffer an Armen
    /// machen die Waffenfuehrung unruhiger. Beide Werte klingen ueber die Zeit
    /// wieder ab - im Gegensatz zur Blutung.
    ///
    /// Server-autoritativ: nur der Server aendert etwas, die Werte gehen als
    /// NetworkVariable an alle.
    ///
    /// NICHT pruefbar: ob die Werte fair sind. Pruefbar: Blutung laeuft,
    /// stoppt nicht von selbst, der Verband stoppt sie, Zonen wirken.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class Bleeding : NetworkBehaviour
    {
        [Header("Blutung")]
        // Schaden pro Sekunde je offener Blutung.
        [SerializeField] float _schadenProSekunde = 1.6f;
        // Mehr als so viele Blutungen gleichzeitig gibt es nicht.
        [SerializeField] int _maxWunden = 3;
        // Eine Blutung bringt einen nicht unter diesen Rest - verbluten waere
        // in einem Rundenspiel zu hart und nimmt einem jede Chance.
        [SerializeField] int _untergrenze = 12;

        [Header("Folgen von Zonentreffern")]
        [SerializeField] float _beinTrefferMalus = 0.22f;   // je Treffer
        [SerializeField] float _armTrefferMalus = 0.3f;     // je Treffer
        [SerializeField] float _malusAbbauProSekunde = 0.08f;

        readonly NetworkVariable<int> _wunden = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> _beinMalus = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<float> _armMalus = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        Health _health;
        float _rest;    // aufgelaufener Bruchteil eines Schadenspunktes

        /// <summary>Anzahl offener Blutungen.</summary>
        public int Wunden => _wunden.Value;

        /// <summary>Blutet die Figur gerade?</summary>
        public bool Blutet => _wunden.Value > 0;

        /// <summary>0 = unverletzt, 1 = Bein voll ausgefallen. Bremst die Bewegung.</summary>
        public float BeinMalus01 => Mathf.Clamp01(_beinMalus.Value);

        /// <summary>0 = unverletzt, 1 = Arm voll ausgefallen. Macht die Waffe unruhig.</summary>
        public float ArmMalus01 => Mathf.Clamp01(_armMalus.Value);

        /// <summary>Bewegungsfaktor aus dem Beinschaden: 1.0 gesund, 0.55 am schlimmsten.</summary>
        public float TempoFaktor => Mathf.Lerp(1f, 0.55f, BeinMalus01);

        /// <summary>Zusaetzliche Streuung aus dem Armschaden, in Grad.</summary>
        public float ZusatzStreuung => BeinMalus01 * 0f + ArmMalus01 * 1.4f;

        void Awake() => _health = GetComponent<Health>();

        public override void OnNetworkSpawn()
        {
            if (IsServer && _health != null) _health.Revived += ServerAllesHeilen;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _health != null) _health.Revived -= ServerAllesHeilen;
        }

        /// <summary>
        /// Server: einen Treffer in einer Zone verbuchen. Der eigentliche
        /// Schaden laeuft weiter ueber Health - hier kommen nur die Folgen dazu.
        /// </summary>
        public void ServerTreffer(KoerperZone zone)
        {
            if (!IsServer) return;

            if (Random.value < Hitbox.BlutungsChance(zone))
                _wunden.Value = Mathf.Min(_maxWunden, _wunden.Value + 1);

            if (zone == KoerperZone.Bein)
                _beinMalus.Value = Mathf.Clamp01(_beinMalus.Value + _beinTrefferMalus);
            else if (zone == KoerperZone.Arm)
                _armMalus.Value = Mathf.Clamp01(_armMalus.Value + _armTrefferMalus);
        }

        /// <summary>Server: Verbandspaket - stoppt alle Blutungen. Heilt nicht.</summary>
        public void ServerVerbinden()
        {
            if (!IsServer) return;
            _wunden.Value = 0;
        }

        /// <summary>Server: alles zuruecksetzen (neue Runde, Wiederbelebung).</summary>
        public void ServerAllesHeilen()
        {
            if (!IsServer) return;
            _wunden.Value = 0;
            _beinMalus.Value = 0f;
            _armMalus.Value = 0f;
            _rest = 0f;
        }

        void Update()
        {
            if (!IsServer) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Zonen-Malus klingt ab - Blutungen ausdruecklich nicht.
            if (_beinMalus.Value > 0f)
                _beinMalus.Value = Mathf.Max(0f, _beinMalus.Value - _malusAbbauProSekunde * dt);
            if (_armMalus.Value > 0f)
                _armMalus.Value = Mathf.Max(0f, _armMalus.Value - _malusAbbauProSekunde * dt);

            if (_wunden.Value <= 0 || _health == null || !_health.IsAlive) return;
            if (_health.Current <= _untergrenze) return;

            _rest += _schadenProSekunde * _wunden.Value * dt;
            if (_rest < 1f) return;

            int punkte = Mathf.FloorToInt(_rest);
            _rest -= punkte;
            // Nicht unter die Untergrenze bluten.
            punkte = Mathf.Min(punkte, _health.Current - _untergrenze);
            if (punkte > 0) _health.ApplyDamage(punkte, (GameObject)null, true);
        }

        // --- Nur fuer Tests ---------------------------------------------------
        public void SetWundenForTests(int n) => _wunden.Value = Mathf.Clamp(n, 0, _maxWunden);
        public void SetBeinMalusForTests(float v) => _beinMalus.Value = Mathf.Clamp01(v);
        public void SetArmMalusForTests(float v) => _armMalus.Value = Mathf.Clamp01(v);
        public int UntergrenzeForTests => _untergrenze;
    }
}
