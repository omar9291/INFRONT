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
        [SerializeField] WeaponCatalog _catalog;
        [SerializeField] int _defaultPrimary = 0;
        [SerializeField] int _defaultSecondary = 3;
        [SerializeField] Transform _muzzle;
        [SerializeField] LayerMask _hitMask = ~0;

        WeaponStats _stats;   // aktive Waffe, aus dem Katalog

        IAimSource _aim;
        NetworkPlayerController _playerController;
        TeamMember _team;
        Health _health;

        readonly NetworkVariable<int> _ammo = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> _reloading = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _ammoOther = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _primaryIdx = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _secondaryIdx = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _activeSlot = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        public int ActiveSlot => _activeSlot.Value;
        public string WeaponName => _stats != null ? _stats.DisplayName : "-";
        /// <summary>Hat der Kaempfer gerade eine Primaerwaffe (Platz 1)? -1 = nur Pistole.</summary>
        public bool HasPrimary => _primaryIdx.Value >= 0;
        public int PrimaryIndex => _primaryIdx.Value;
        int ActiveIndex => _activeSlot.Value == 0 ? _primaryIdx.Value : _secondaryIdx.Value;

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
            _activeSlot.OnValueChanged += (_, __) => RefreshStats();
            _primaryIdx.OnValueChanged += (_, __) => RefreshStats();
            _secondaryIdx.OnValueChanged += (_, __) => RefreshStats();

            if (IsServer)
                ServerSetPistolOnly();   // Jede Runde startet man mit der Pistole

            RefreshStats();
        }

        void RefreshStats()
        {
            _stats = _catalog != null ? _catalog.Get(ActiveIndex) : null;
        }

        int PistolMag => _catalog != null && _catalog.Get(_secondaryIdx.Value) != null
            ? _catalog.Get(_secondaryIdx.Value).MagazineSize : 0;

        /// <summary>Nur Server: nur Pistole, keine Primaerwaffe (Rundenstart / nach Tod).</summary>
        public void ServerSetPistolOnly()
        {
            if (!IsServer || _catalog == null) return;

            _primaryIdx.Value = -1;
            _secondaryIdx.Value = Mathf.Clamp(_defaultSecondary, 0, _catalog.Weapons.Length - 1);
            _activeSlot.Value = 1;        // Pistole in der Hand
            _reloading.Value = false;
            RefreshStats();

            _ammo.Value = PistolMag;      // aktive Waffe = Pistole
            _ammoOther.Value = 0;         // kein Primaerplatz belegt
        }

        /// <summary>Nur Server: Primaerwaffe setzen (Kaufmenue). Wechselt in die Hand.</summary>
        public void ServerSetPrimary(int weaponIndex)
        {
            if (!IsServer || _catalog == null) return;

            _primaryIdx.Value = Mathf.Clamp(weaponIndex, 0, _catalog.Weapons.Length - 1);
            _activeSlot.Value = 0;
            _reloading.Value = false;
            RefreshStats();

            var prim = _catalog.Get(_primaryIdx.Value);
            _ammo.Value = prim != null ? prim.MagazineSize : 0;
            _ammoOther.Value = PistolMag;
        }

        /// <summary>Nur Server + nur Tests: die im Prefab hinterlegte Standardwaffe geben.</summary>
        public void ServerEquipDefaultPrimary() => ServerSetPrimary(_defaultPrimary);

        /// <summary>
        /// Alt-API. Bleibt fuer bestehende Aufrufer erhalten und leitet auf die
        /// klareren Methoden um.
        /// </summary>
        public void ServerSetLoadout(int primaryIndex, int secondaryIndex)
        {
            if (!IsServer || _catalog == null) return;
            _secondaryIdx.Value = Mathf.Clamp(secondaryIndex, 0, _catalog.Weapons.Length - 1);
            if (primaryIndex < 0) ServerSetPistolOnly();
            else ServerSetPrimary(primaryIndex);
        }

        [Rpc(SendTo.Server)]
        void RequestSlotRpc(int slot)
        {
            if (!IsServer || slot == _activeSlot.Value || (slot != 0 && slot != 1)) return;
            if (slot == 0 && !HasPrimary) return;   // nichts auf dem Primaerplatz
            if (_stats == null) return;

            int keep = _ammo.Value;
            _ammo.Value = _ammoOther.Value;
            _ammoOther.Value = keep;
            _activeSlot.Value = slot;
            _reloading.Value = false;
            RefreshStats();
            _nextFireTime = ServerNow + (_stats != null ? _stats.SwitchTime : 0.5f);
        }

        void Update()
        {
            if (_playerController == null || !IsOwner || _playerController.Input == null)
                return;

            var input = _playerController.Input;

            if (input.SwitchToSlot >= 0)
                RequestSlotRpc(input.SwitchToSlot);

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

        /// <summary>Nur Server: beide Waffen voll, aktive Waffe auf Primaer, Nachladen abbrechen.</summary>
        public void ServerRefillMagazine()
        {
            if (!IsServer || _catalog == null) return;
            ServerSetLoadout(_primaryIdx.Value, _secondaryIdx.Value);
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
                    // Kopfschuss geht an der Weste vorbei
                    targetHealth.ApplyDamage(dmg, gameObject, head);
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
