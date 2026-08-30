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
        Health _health;

        readonly NetworkVariable<int> _ammo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _reloading = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        double _nextFireTime;
        double _reloadFinishTime;
        double _clientNextFire;
        float _spread;          // Server: Aufbau-Streuung
        int _clientShot;        // Client: Schuss-Index fuers Rueckstoss-Muster
        double _lastClientShot;

        public int Ammo => _ammo.Value;
        public bool IsReloading => _reloading.Value;
        public int MagazineSize => _stats != null ? _stats.MagazineSize : 0;
        public WeaponStats Stats => _stats;

        public event Action<Vector3, Vector3> FireVisual;
        public event Action<GameObject, int> ServerHitConfirmed;
        /// <summary>NUR beim schiessenden Client: ein eigener Schuss hat getroffen.</summary>
        public event Action LocalHitConfirmed;

        double ServerNow => NetworkManager.ServerTime.Time;

        void Awake()
        {
            _aim = GetComponent<IAimSource>();
            _playerController = GetComponent<NetworkPlayerController>();
            _team = GetComponent<TeamMember>();
            _health = GetComponent<Health>();
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
            {
                ApplyLocalRecoil();
                FireRpc(ServerNow);
            }
        }

        void LateUpdate()
        {
            if (IsServer)
            {
                TickReload();
                if (_stats != null)
                    _spread = Mathf.Max(0f, _spread - _stats.SpreadRecovery * Time.deltaTime);
            }
        }

        void ApplyLocalRecoil()
        {
            if (_stats == null || _playerController == null) return;
            if (ServerNow - _lastClientShot > 0.3) _clientShot = 0;
            _lastClientShot = ServerNow;

            float up = _stats.RecoilUp;
            float side = _stats.RecoilSide * Mathf.Sin(_clientShot * 1.2f) * Mathf.Clamp01(_clientShot / 3f);
            _playerController.AddRecoil(up, side);
            _clientShot++;
        }

        float ServerMovementSpread()
        {
            if (_stats == null) return 0f;
            Vector3 v = Vector3.zero;
            bool grounded = true;
            var cc = GetComponent<CharacterController>();
            if (cc != null && cc.enabled) { v = cc.velocity; grounded = cc.isGrounded; }
            else
            {
                var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled) v = agent.velocity;
            }
            float speed = new Vector2(v.x, v.z).magnitude;
            if (!grounded) return _stats.SpreadAir;
            if (speed < 0.5f) return _stats.SpreadStand;
            if (speed < 7f) return Mathf.Lerp(_stats.SpreadStand, _stats.SpreadWalk, (speed - 0.5f) / 6.5f);
            return _stats.SpreadSprint;
        }

        static Vector3 ApplyCone(Vector3 dir, float degrees)
        {
            if (degrees <= 0.01f) return dir;
            float rad = degrees * Mathf.Deg2Rad;
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(rad);
            return (Quaternion.LookRotation(dir) * new Vector3(r.x, r.y, 1f)).normalized;
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
            if (_health != null && !_health.IsAlive)
                return false; // Tote schiessen nicht
            if (MatchManager.Instance != null && MatchManager.Instance.IsFrozen)
                return false; // Startsperre
            if (ServerNow < _nextFireTime)
                return false;
            if (_ammo.Value <= 0)
            {
                ServerStartReload();
                return false;
            }

            _nextFireTime = ServerNow + _stats.ShotInterval;
            _ammo.Value -= 1;

            Vector3 rayOrigin = _aim.AimOrigin;
            float totalSpread = Mathf.Min(ServerMovementSpread() + _spread, _stats.SpreadMax);
            Vector3 direction = ApplyCone(_aim.AimDirection, totalSpread);
            _spread = Mathf.Min(_spread + _stats.SpreadPerShot, _stats.SpreadMax);
            Vector3 endPoint = rayOrigin + direction * _stats.Range;

            var hits = Physics.RaycastAll(rayOrigin, direction, _stats.Range, _hitMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var hitObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitObject != null && hitObject == NetworkObject)
                    continue; // eigene Kollider

                var box = hit.collider.GetComponent<Hitbox>();
                Health targetHealth = box != null ? box.Owner : hit.collider.GetComponentInParent<Health>();
                var otherTeam = targetHealth != null ? targetHealth.GetComponent<TeamMember>() : null;

                // Verbuendete: Kugel fliegt hindurch
                if (targetHealth != null && otherTeam != null && _team != null
                    && Team.AreFriendly(_team.TeamId, otherTeam.TeamId))
                    continue;

                endPoint = hit.point;

                if (targetHealth != null && targetHealth.IsAlive)
                {
                    bool head = box != null && box.IsHead;
                    int dmg = head
                        ? Mathf.RoundToInt(_stats.Damage * _stats.HeadshotMultiplier)
                        : _stats.Damage;
                    targetHealth.ApplyDamage(dmg, gameObject);
                    ServerHitConfirmed?.Invoke(hit.collider.gameObject, dmg);
                    HitConfirmedRpc();
                }
                break;
            }

            Vector3 tracerOrigin = _muzzle != null ? _muzzle.position : rayOrigin;
            ShowFireEffectRpc(tracerOrigin, endPoint);
            return true;
        }

        [Rpc(SendTo.Owner)]
        void HitConfirmedRpc() => LocalHitConfirmed?.Invoke();

        [Rpc(SendTo.Everyone)]
        void ShowFireEffectRpc(Vector3 origin, Vector3 endPoint)
        {
            FireVisual?.Invoke(origin, endPoint);
        }
    }
}
