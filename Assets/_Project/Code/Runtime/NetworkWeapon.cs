using System;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Server-autoritative Waffe (Hitscan / Soforttreffer).
    ///
    ///  - Der Spieler-Client haelt die Feuertaste; jeder faellige Schuss schickt
    ///    eine Anfrage (FireRpc) an den Server.
    ///  - Ein Bot laeuft schon auf dem Server und ruft direkt ServerTryFire() auf.
    ///  - Der Server prueft Feuerrate, Munition und Nachladen, macht den Raycast
    ///    ueber die IAimSource und zieht Schaden ab.
    ///  - Munition und Nachlade-Status sind NetworkVariables (nur Server schreibt).
    ///  - Die Schussspur geht per Rpc an alle (nur Optik).
    ///
    /// clientRenderTime ist fuer spaetere Lag-Kompensation vorgesehen
    /// (siehe NETCODE.md) und wird jetzt noch nicht ausgewertet.
    /// </summary>
    public sealed class NetworkWeapon : NetworkBehaviour
    {
        [SerializeField] WeaponStats _stats;
        [SerializeField] Transform _muzzle;
        [SerializeField] LayerMask _hitMask = ~0;

        IAimSource _aim;
        NetworkPlayerController _playerController;
        TeamMember _team;

        readonly NetworkVariable<int> _ammo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _reloading = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        double _nextFireTime;
        double _reloadFinishTime;
        double _clientNextFire;

        public int Ammo => _ammo.Value;
        public bool IsReloading => _reloading.Value;
        public int MagazineSize => _stats != null ? _stats.MagazineSize : 0;
        public WeaponStats Stats => _stats;

        public event Action<Vector3, Vector3> FireVisual;
        public event Action<GameObject, int> ServerHitConfirmed;

        double ServerNow => NetworkManager.ServerTime.Time;

        void Awake()
        {
            _aim = GetComponent<IAimSource>();
            _playerController = GetComponent<NetworkPlayerController>();
            _team = GetComponent<TeamMember>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _stats != null)
            {
                _ammo.Value = _stats.MagazineSize;
                _reloading.Value = false;
            }
        }

        void Update()
        {
            if (_playerController == null || !IsOwner || _playerController.Input == null)
                return;

            var input = _playerController.Input;

            if (input.ReloadPressed)
                RequestReloadRpc();

            if (input.FireHeld && ClientReadyToTry())
                FireRpc(ServerNow);
        }

        void LateUpdate()
        {
            if (IsServer)
                TickReload();
        }

        bool ClientReadyToTry()
        {
            if (_stats == null) return false;
            double now = Time.timeAsDouble;
            if (now < _clientNextFire) return false;
            _clientNextFire = now + _stats.ShotInterval;
            return true;
        }

        void TickReload()
        {
            if (_reloading.Value && ServerNow >= _reloadFinishTime)
            {
                _ammo.Value = _stats.MagazineSize;
                _reloading.Value = false;
            }
        }

        /// <summary>Nur Server: Magazin sofort voll, Nachladen abbrechen (Rundenstart).</summary>
        public void ServerRefillMagazine()
        {
            if (!IsServer || _stats == null) return;
            _ammo.Value = _stats.MagazineSize;
            _reloading.Value = false;
        }

        /// <summary>Nur Server (z.B. vom Bot aufgerufen). Gibt zurueck, ob geschossen wurde.</summary>
        public bool ServerTryFire()
        {
            return IsServer && DoFire(ServerNow);
        }

        [Rpc(SendTo.Server)]
        void RequestReloadRpc() => ServerStartReload();

        /// <summary>Nur Server. Auch vom Bot nutzbar.</summary>
        public void ServerStartReload()
        {
            if (!IsServer || _stats == null || _reloading.Value)
                return;
            if (_ammo.Value >= _stats.MagazineSize)
                return;

            _reloading.Value = true;
            _reloadFinishTime = ServerNow + _stats.ReloadTime;
        }

        [Rpc(SendTo.Server)]
        void FireRpc(double clientRenderTime)
        {
            DoFire(clientRenderTime);
        }

        bool DoFire(double clientRenderTime)
        {
            if (_stats == null || _aim == null || _reloading.Value)
                return false;
            if (ServerNow < _nextFireTime)
                return false;
            if (_ammo.Value <= 0)
            {
                ServerStartReload();
                return false;
            }

            _nextFireTime = ServerNow + _stats.ShotInterval;
            _ammo.Value -= 1;

            Vector3 origin = _muzzle != null ? _muzzle.position : _aim.AimOrigin;
            Vector3 direction = _aim.AimDirection;
            Vector3 endPoint = origin + direction * _stats.Range;

            var hits = Physics.RaycastAll(origin, direction, _stats.Range, _hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var hitObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitObject != null && hitObject == NetworkObject)
                    continue; // eigene Kollider ueberspringen

                var damageable = hit.collider.GetComponentInParent<IDamageable>();

                // Verbuendete: Kugel fliegt hindurch (kein Freundschaftsbeschuss)
                var otherTeam = hit.collider.GetComponentInParent<TeamMember>();
                if (damageable != null && otherTeam != null && _team != null
                    && Team.AreFriendly(_team.TeamId, otherTeam.TeamId))
                {
                    continue;
                }

                endPoint = hit.point;

                if (damageable != null && damageable.IsAlive)
                {
                    damageable.ApplyDamage(_stats.Damage, gameObject);
                    ServerHitConfirmed?.Invoke(hit.collider.gameObject, _stats.Damage);
                }
                break;
            }

            ShowFireEffectRpc(origin, endPoint);
            return true;
        }

        [Rpc(SendTo.Everyone)]
        void ShowFireEffectRpc(Vector3 origin, Vector3 endPoint)
        {
            FireVisual?.Invoke(origin, endPoint);
        }
    }
}
