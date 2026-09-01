using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Ton-Rückmeldung nur für den lokalen Spieler (2D, direkt am Ohr):
    ///  - kurzer "Tock", wenn ein eigener Schuss trifft; heller Ton bei Kopftreffer
    ///  - eigener Abschuss-Ton, wenn man einen Gegner ausschaltet
    ///  - dumpfer Ton, wenn man selbst ausgeschaltet wird
    ///
    /// Läuft wie <see cref="DamageFeedback"/> nur beim Besitzer.
    /// </summary>
    public sealed class CombatAudio : NetworkBehaviour
    {
        Health _health;
        NetworkWeapon _weapon;
        MatchManager _hookedMatch;
        ulong _myId;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) { enabled = false; return; }

            _myId = NetworkObject != null ? NetworkObject.NetworkObjectId : 0;
            _health = GetComponent<Health>();
            _weapon = GetComponent<NetworkWeapon>();

            if (_weapon != null) _weapon.LocalHitConfirmed += OnHitConfirmed;
            if (_health != null) _health.Died += OnDied;
        }

        public override void OnNetworkDespawn()
        {
            if (_weapon != null) _weapon.LocalHitConfirmed -= OnHitConfirmed;
            if (_health != null) _health.Died -= OnDied;
            if (_hookedMatch != null) _hookedMatch.KillReported -= OnKillReported;
        }

        void Update()
        {
            // MatchManager erst nach dem Spawn da - wie im KillFeedHud nachziehen.
            var mm = MatchManager.Instance;
            if (mm == _hookedMatch) return;
            if (_hookedMatch != null) _hookedMatch.KillReported -= OnKillReported;
            _hookedMatch = mm;
            if (_hookedMatch != null) _hookedMatch.KillReported += OnKillReported;
        }

        void OnHitConfirmed(bool head, bool lethal)
        {
            AudioService.Instance?.Play2D(head ? SoundId.TrefferKopf : SoundId.TrefferMarke, 0.6f);
        }

        void OnDied()
        {
            AudioService.Instance?.Play2D(SoundId.EigenerTod, 0.8f);
        }

        void OnKillReported(ulong killerId, ulong victimId)
        {
            if (killerId != 0 && killerId == _myId && killerId != victimId)
                AudioService.Instance?.Play2D(SoundId.Abschuss, 0.7f);
        }
    }
}
