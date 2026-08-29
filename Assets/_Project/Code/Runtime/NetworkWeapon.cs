using System;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Server-autoritative Waffe (Hitscan / Soforttreffer).
    ///
    ///  - Der Client haelt die Feuertaste. Jeder faellige Schuss schickt eine
    ///    Anfrage (FireRpc) an den Server. Der Client rechnet NICHT selbst,
    ///    ob getroffen wurde.
    ///  - Der Server prueft Feuerrate, Munition und Nachlade-Status, macht den
    ///    Raycast von seinem eigenen Ziel-Drehpunkt aus und zieht Schaden ab.
    ///  - Munition und Nachlade-Status sind NetworkVariables, die nur der
    ///    Server schreibt.
    ///  - Die Schussspur wird per ClientRpc an alle geschickt (nur Optik).
    ///
    /// Der Parameter clientRenderTime ist fuer spaetere Lag-Kompensation
    /// vorgesehen (siehe NETCODE.md) und wird jetzt noch nicht ausgewertet.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class NetworkWeapon : NetworkBehaviour
    {
        [SerializeField] WeaponStats _stats;
        [SerializeField] Transform _muzzle;
        [SerializeField] LayerMask _hitMask = ~0;

        NetworkPlayerController _controller;

        readonly NetworkVariable<int> _ammo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _reloading = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Nur Server
        double _nextFireTime;
        double _reloadFinishTime;

        public int Ammo => _ammo.Value;
        public bool IsReloading => _reloading.Value;
        public int MagazineSize => _stats != null ? _stats.MagazineSize : 0;
        public WeaponStats Stats => _stats;

        /// <summary>Alle Clients: (Ursprung, Endpunkt) einer abgegebenen Schussspur.</summary>
        public event Action<Vector3, Vector3> FireVisual;
        /// <summary>Server: ein Schuss hat getroffen. (getroffenes Objekt, Schaden).</summary>
        public event Action<GameObject, int> ServerHitConfirmed;

        double ServerNow => NetworkManager.ServerTime.Time;

        void Awake()
        {
            _controller = GetComponent<NetworkPlayerController>();
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
            if (!IsOwner || _controller == null || _controller.Input == null)
                return;

            var input = _controller.Input;

            if (input.ReloadPressed)
                RequestReloadRpc();

            if (input.FireHeld && CanClientTryFire())
                FireRpc(ServerNow);
        }

        // Der Client drosselt grob mit, damit nicht jeder Frame eine RPC rausgeht.
        // Die echte Pruefung macht trotzdem der Server.
        double _clientNextFire;
        bool CanClientTryFire()
        {
            if (_stats == null) return false;
            double now = Time.timeAsDouble;
            if (now < _clientNextFire) return false;
            _clientNextFire = now + _stats.ShotInterval;
            return true;
        }

        void Server_TickReload()
        {
            if (_reloading.Value && ServerNow >= _reloadFinishTime)
            {
                _ammo.Value = _stats.MagazineSize;
                _reloading.Value = false;
            }
        }

        void LateUpdate()
        {
            if (IsServer)
                Server_TickReload();
        }

        [Rpc(SendTo.Server)]
        void RequestReloadRpc()
        {
            if (_stats == null || _reloading.Value)
                return;
            if (_ammo.Value >= _stats.MagazineSize)
                return;

            _reloading.Value = true;
            _reloadFinishTime = ServerNow + _stats.ReloadTime;
        }

        [Rpc(SendTo.Server)]
        void FireRpc(double clientRenderTime)
        {
            if (_stats == null || _reloading.Value)
                return;
            if (ServerNow < _nextFireTime)
                return;
            if (_ammo.Value <= 0)
                return;

            _nextFireTime = ServerNow + _stats.ShotInterval;
            _ammo.Value -= 1;

            Transform aim = _controller.AimPivot != null ? _controller.AimPivot : transform;
            Vector3 origin = _muzzle != null ? _muzzle.position : aim.position;
            Vector3 direction = aim.forward;
            Vector3 endPoint = origin + direction * _stats.Range;

            var hits = Physics.RaycastAll(origin, direction, _stats.Range, _hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                // Eigene Kollider (CharacterController des Schuetzen) ueberspringen
                var hitObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitObject != null && hitObject == NetworkObject)
                    continue;

                endPoint = hit.point;

                var damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.ApplyDamage(_stats.Damage, OwnerClientId);
                    ServerHitConfirmed?.Invoke(hit.collider.gameObject, _stats.Damage);
                }
                break; // erster gueltiger Treffer stoppt die Kugel
            }

            ShowFireEffectRpc(origin, endPoint);
        }

        [Rpc(SendTo.Everyone)]
        void ShowFireEffectRpc(Vector3 origin, Vector3 endPoint)
        {
            FireVisual?.Invoke(origin, endPoint);
        }
    }
}
