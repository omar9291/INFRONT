using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die Bomben-Eingabe eines Kaempfers. Sitzt an Spieler UND Bot.
    ///
    ///  - Spieler: der besitzende Client meldet die Kanten der E-Taste
    ///    (gedrueckt / losgelassen) an den Server. Der Server tickt selbst,
    ///    genau wie bei der Bewegung - netzsparsam.
    ///  - Bot: die KI ruft <see cref="ServerSetUsing"/> direkt auf dem Server auf.
    ///
    /// Die eigentliche Legen-/Entschaerfen-Logik steht in <see cref="Bomb"/>;
    /// diese Klasse liefert nur "will gerade benutzen" und "hat ein Kit".
    /// </summary>
    public sealed class BombAction : NetworkBehaviour
    {
        readonly NetworkVariable<bool> _using = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _hasKit = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        NetworkPlayerController _player;
        Health _health;
        bool _wasHeld;

        /// <summary>Haelt der Kaempfer gerade die Benutzen-Taste (und lebt)?</summary>
        public bool IsUsing => _using.Value;

        /// <summary>Hat der Kaempfer ein Entschaerfungs-Kit gekauft?</summary>
        public bool HasKit => _hasKit.Value;

        void Awake()
        {
            _player = GetComponent<NetworkPlayerController>();
            _health = GetComponent<Health>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _health != null)
                _health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && _health != null)
                _health.Died -= OnDied;
        }

        void OnDied()
        {
            if (IsServer) _using.Value = false;
        }

        void Update()
        {
            // Nur der besitzende Spieler-Client meldet Tasten. Bots laufen
            // ueber ServerSetUsing.
            if (!IsOwner || _player == null || _player.Input == null)
                return;

            bool held = _player.Input.UseHeld;
            if (held != _wasHeld)
            {
                _wasHeld = held;
                SetUsingRpc(held);
            }
        }

        [Rpc(SendTo.Server)]
        void SetUsingRpc(bool value) => ServerSetUsing(value);

        /// <summary>Nur Server. Auch von der Bot-KI genutzt.</summary>
        public void ServerSetUsing(bool value)
        {
            if (!IsServer) return;
            _using.Value = value && (_health == null || _health.IsAlive);
        }

        /// <summary>Nur Server: Kit geben (Kaufmenue).</summary>
        public void ServerGiveKit()
        {
            if (IsServer) _hasKit.Value = true;
        }

        /// <summary>Nur Server: Kit weg (Tod ohne Ueberleben, Matchstart).</summary>
        public void ServerClearKit()
        {
            if (IsServer) _hasKit.Value = false;
        }
    }
}
