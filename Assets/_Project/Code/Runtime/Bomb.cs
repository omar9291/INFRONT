using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Infront
{
    /// <summary>
    /// Die Bombe im Bomben-Modus. Ein einziges Netzwerk-Objekt pro Match,
    /// vom <see cref="MatchDirector"/> erzeugt, vom <see cref="MatchManager"/>
    /// pro Runde neu vergeben.
    ///
    /// Zustaende:
    ///   Inactive - keine Runde / kein Bomben-Modus
    ///   Carried  - ein Angreifer traegt sie (folgt seiner Position)
    ///   Dropped  - sie liegt am Boden, ein Angreifer kann sie aufheben
    ///   Planted  - gelegt, der Zuender laeuft
    ///
    /// Nur der Server rechnet. Clients lesen Zustand, Traeger, Platz,
    /// Fortschritt und Zuenderzeit fuers HUD.
    ///
    /// Legen/Entschaerfen laeuft ueber einen Start-/Endzeitpunkt (ServerTime),
    /// genau wie die Kaufzeit im MatchManager - wird der Vorgang unterbrochen,
    /// faellt der Fortschritt auf 0 zurueck.
    /// </summary>
    public sealed class Bomb : NetworkBehaviour
    {
        public enum State { Inactive = 0, Carried = 1, Dropped = 2, Planted = 3 }

        public static Bomb Instance { get; private set; }

        [Header("Zeiten")]
        [SerializeField] float _plantSeconds = 3.2f;
        [SerializeField] float _defuseSeconds = 10f;
        [SerializeField] float _defuseWithKitSeconds = 5f;
        [SerializeField] float _fuseSeconds = 40f;

        [Header("Reichweiten")]
        [SerializeField] float _defuseRange = 3f;
        [SerializeField] float _pickupRange = 2.2f;

        [Header("Explosion")]
        [SerializeField] float _blastRadius = 14f;
        [SerializeField] int _blastDamage = 500;

        readonly NetworkVariable<int> _state = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<ulong> _carrierId = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<int> _siteId = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _actionStart = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _actionEnd = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<double> _detonateTime = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // nur Server: wer gerade entschaerft (0 = niemand)
        ulong _defuserId;
        readonly List<TeamMember> _buffer = new();

        public State CurrentState => (State)_state.Value;
        public bool IsPlanted => _state.Value == (int)State.Planted;
        public bool IsCarried => _state.Value == (int)State.Carried;
        public ulong CarrierId => _carrierId.Value;
        public int PlantedSiteId => _siteId.Value;
        public double DetonateTime => _detonateTime.Value;

        double ServerNow => NetworkManager.ServerTime.Time;

        public float FuseSecondsLeft =>
            IsPlanted ? Mathf.Max(0f, (float)(_detonateTime.Value - ServerNow + PauseComp)) : 0f;

        // Solo-Pause: die ServerTime tickt bei timeScale=0 weiter - fuer die
        // Anzeige die schon verstrichene Pausenzeit gutschreiben (auf Fortsetzen
        // schiebt der MatchManager _detonateTime ohnehin um genau das nach).
        double PauseComp =>
            MatchManager.Instance != null && MatchManager.Instance.IsSoloPaused
                ? MatchManager.Instance.SoloPauseElapsedForHud : 0.0;

        /// <summary>0..1 waehrend gelegt wird (Zustand Carried).</summary>
        public float PlantProgress01 => IsCarried ? Action01() : 0f;

        /// <summary>0..1 waehrend entschaerft wird (Zustand Planted).</summary>
        public float DefuseProgress01 => IsPlanted ? Action01() : 0f;

        float Action01()
        {
            double start = _actionStart.Value, end = _actionEnd.Value;
            if (end <= start) return 0f;
            return Mathf.Clamp01((float)((ServerNow - start) / (end - start)));
        }

        public bool IsCarriedBy(GameObject go)
        {
            if (!IsCarried || go == null) return false;
            var no = go.GetComponentInParent<NetworkObject>();
            return no != null && no.NetworkObjectId == _carrierId.Value;
        }

        // Parkplatz weit unter der Karte, solange die Bombe inaktiv ist.
        static readonly Vector3 Parked = new(0f, -80f, 0f);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer) transform.position = Parked;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------------------------------------------------------
        //  Rundensteuerung (vom MatchManager)
        // ---------------------------------------------------------------

        /// <summary>Nur Server: neue Runde - Zustand zuruecksetzen und einem
        /// zufaelligen lebenden Angreifer die Bombe geben.</summary>
        public void ServerBeginRound(int attackingTeam)
        {
            if (!IsServer) return;

            ServerReset();

            TeamMember carrier = null;
            _buffer.Clear();
            foreach (var m in Combatants.Everyone)
                if (m != null && m.TeamId == attackingTeam && m.Health != null && m.Health.IsAlive)
                    _buffer.Add(m);
            if (_buffer.Count > 0)
                carrier = _buffer[Random.Range(0, _buffer.Count)];

            if (carrier != null)
                ServerGiveTo(carrier);
        }

        /// <summary>Nur Server: komplett zurueck auf Inactive.</summary>
        public void ServerReset()
        {
            if (!IsServer) return;
            _state.Value = (int)State.Inactive;
            _carrierId.Value = 0;
            _siteId.Value = -1;
            _actionStart.Value = 0;
            _actionEnd.Value = 0;
            _detonateTime.Value = 0;
            _defuserId = 0;
            transform.position = Parked;
        }

        // ---------------------------------------------------------------
        //  Server-Tick
        // ---------------------------------------------------------------

        /// <summary>Nur Server: alle laufenden Zeitpunkte (Legen/Entschaerfen und
        /// Zuender) um delta Sekunden nach hinten schieben - fuer die Solo-Pause.</summary>
        public void ServerShiftTimes(double delta)
        {
            if (!IsServer || delta <= 0) return;
            if (_actionStart.Value != 0) { _actionStart.Value += delta; _actionEnd.Value += delta; }
            if (_detonateTime.Value != 0) _detonateTime.Value += delta;
        }

        void Update()
        {
            if (!IsServer) return;

            var mm = MatchManager.Instance;
            if (mm == null || !mm.IsBombMode || mm.CurrentPhase != MatchManager.Phase.Playing)
                return;
            if (mm.IsSoloPaused) return;   // echte Pause: der Zuender steht still

            switch ((State)_state.Value)
            {
                case State.Carried: TickCarried(mm); break;
                case State.Dropped: TickDropped(mm); break;
                case State.Planted: TickPlanted(mm); break;
            }
        }

        // Nur Optik: das Piepen der gelegten Bombe, auf jeder Instanz. Wird
        // schneller, je weniger Zeit bis zur Zündung bleibt.
        float _beepTimer;
        void LateUpdate()
        {
            if (!IsPlanted) { _beepTimer = 0f; return; }
            if (MatchManager.Instance != null && MatchManager.Instance.IsSoloPaused) return;

            _beepTimer -= Time.deltaTime;
            if (_beepTimer > 0f) return;

            float k = Mathf.Clamp01(FuseSecondsLeft / Mathf.Max(1f, _fuseSeconds));
            _beepTimer = Mathf.Lerp(0.15f, 1.2f, k);
            AudioService.Instance?.PlayAt(SoundId.BombePiep, transform.position, 0.7f);
        }

        void TickCarried(MatchManager mm)
        {
            var carrier = Resolve(_carrierId.Value);
            if (carrier == null || carrier.Health == null || !carrier.Health.IsAlive)
            {
                Vector3 dropAt = carrier != null ? carrier.transform.position : transform.position;
                ServerDrop(dropAt);
                return;
            }

            // Bombe folgt dem Traeger
            transform.position = carrier.transform.position + Vector3.up * 0.4f;

            var action = carrier.GetComponent<BombAction>();
            int site = BombSite.SiteAt(carrier.transform.position);
            bool canPlant = action != null && action.IsUsing && site >= 0 && !mm.IsFrozen;

            if (canPlant)
            {
                if (_actionStart.Value == 0)
                {
                    _actionStart.Value = ServerNow;
                    _actionEnd.Value = ServerNow + _plantSeconds;
                }
                else if (ServerNow >= _actionEnd.Value)
                {
                    ServerPlant(site, carrier);
                }
            }
            else if (_actionStart.Value != 0)
            {
                _actionStart.Value = 0;
                _actionEnd.Value = 0;
            }
        }

        void TickDropped(MatchManager mm)
        {
            TeamMember best = null;
            float bestDist = _pickupRange;

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.TeamId != mm.AttackingTeam) continue;
                if (m.Health == null || !m.Health.IsAlive) continue;
                float d = Vector3.Distance(transform.position, m.transform.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = m;
                }
            }

            if (best != null)
                ServerGiveTo(best);
        }

        void TickPlanted(MatchManager mm)
        {
            if (ServerNow >= _detonateTime.Value)
            {
                ServerDetonate();
                return;
            }

            TeamMember defuser = FindDefuser(mm);
            if (defuser == null)
            {
                _defuserId = 0;
                _actionStart.Value = 0;
                _actionEnd.Value = 0;
                return;
            }

            ulong id = defuser.NetworkObject != null ? defuser.NetworkObject.NetworkObjectId : 0;
            bool hasKit = defuser.GetComponent<BombAction>()?.HasKit ?? false;
            float dur = hasKit ? _defuseWithKitSeconds : _defuseSeconds;

            if (id != _defuserId || _actionStart.Value == 0)
            {
                _defuserId = id;
                _actionStart.Value = ServerNow;
                _actionEnd.Value = ServerNow + dur;
            }
            else if (ServerNow >= _actionEnd.Value)
            {
                ServerDefuse(defuser);
            }
        }

        TeamMember FindDefuser(MatchManager mm)
        {
            // Bevorzugt den bisherigen Entschaerfer, wenn er noch passt.
            TeamMember current = _defuserId != 0 ? Resolve(_defuserId) : null;
            if (IsValidDefuser(current, mm)) return current;

            foreach (var m in Combatants.Everyone)
                if (IsValidDefuser(m, mm))
                    return m;
            return null;
        }

        bool IsValidDefuser(TeamMember m, MatchManager mm)
        {
            if (m == null || m.TeamId != mm.DefendingTeam) return false;
            if (m.Health == null || !m.Health.IsAlive) return false;
            var action = m.GetComponent<BombAction>();
            if (action == null || !action.IsUsing) return false;
            return Vector3.Distance(transform.position, m.transform.position) <= _defuseRange;
        }

        // ---------------------------------------------------------------
        //  Zustandswechsel
        // ---------------------------------------------------------------

        void ServerGiveTo(TeamMember carrier)
        {
            _state.Value = (int)State.Carried;
            _carrierId.Value = carrier.NetworkObject != null ? carrier.NetworkObject.NetworkObjectId : 0;
            _siteId.Value = -1;
            _actionStart.Value = 0;
            _actionEnd.Value = 0;
            transform.position = carrier.transform.position + Vector3.up * 0.4f;
        }

        void ServerDrop(Vector3 position)
        {
            _state.Value = (int)State.Dropped;
            _carrierId.Value = 0;
            _actionStart.Value = 0;
            _actionEnd.Value = 0;
            transform.position = position + Vector3.up * 0.2f;
        }

        void ServerPlant(int siteId, TeamMember planter)
        {
            _state.Value = (int)State.Planted;
            _siteId.Value = siteId;
            _carrierId.Value = 0;
            _actionStart.Value = 0;
            _actionEnd.Value = 0;
            _defuserId = 0;
            _detonateTime.Value = ServerNow + _fuseSeconds;

            if (planter != null)
                transform.position = planter.transform.position + Vector3.up * 0.2f;

            MatchManager.Instance?.ServerOnBombPlanted(planter);
        }

        void ServerDefuse(TeamMember defuser)
        {
            ServerReset();
            MatchManager.Instance?.ServerOnBombDefused(defuser);
        }

        void ServerDetonate()
        {
            Vector3 center = transform.position;
            ExplodedRpc(center);
            ServerReset();

            foreach (var m in Combatants.Everyone)
            {
                if (m == null || m.Health == null || !m.Health.IsAlive) continue;
                float d = Vector3.Distance(center, m.transform.position);
                if (d > _blastRadius) continue;
                int dmg = Mathf.RoundToInt(Mathf.Lerp(_blastDamage, 0f, d / _blastRadius));
                if (dmg > 0)
                    m.Health.ApplyDamage(dmg, gameObject, true);   // Explosion geht an der Weste vorbei
            }

            MatchManager.Instance?.ServerOnBombDetonated();
        }

        // ---------------------------------------------------------------
        //  Hilfen
        // ---------------------------------------------------------------

        [Rpc(SendTo.Everyone)]
        void ExplodedRpc(Vector3 center) => GetComponent<BombExplosionFx>()?.Play(center);

        static TeamMember Resolve(ulong networkObjectId)
        {
            if (networkObjectId == 0) return null;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            return nm.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out var no) && no != null
                ? no.GetComponent<TeamMember>()
                : null;
        }

        // ---------------------------------------------------------------
        //  Nur fuer Tests
        // ---------------------------------------------------------------

        public void ServerGiveToForTests(TeamMember carrier)
        {
            if (IsServer && carrier != null) ServerGiveTo(carrier);
        }

        public void ServerPlantForTests(int siteId) => ServerPlantForTests(siteId, null);

        public void ServerPlantForTests(int siteId, TeamMember planter)
        {
            if (!IsServer) return;
            Vector3 pos = BombSite.CenterOf(siteId) + Vector3.up * 0.2f;
            transform.position = pos;
            _state.Value = (int)State.Planted;
            _siteId.Value = siteId;
            _carrierId.Value = 0;
            _actionStart.Value = 0;
            _actionEnd.Value = 0;
            _defuserId = 0;
            _detonateTime.Value = ServerNow + _fuseSeconds;
            MatchManager.Instance?.ServerOnBombPlanted(planter);
        }

        public void ServerSetDetonateInForTests(float seconds)
        {
            if (IsServer) _detonateTime.Value = ServerNow + seconds;
        }
    }
}
