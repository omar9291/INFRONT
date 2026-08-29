using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace Infront
{
    /// <summary>
    /// Ein Bot-Gegner. Laeuft NUR auf dem Server; die Position verteilt
    /// NetworkTransform. Auf anderen Instanzen sind Brain und NavMeshAgent aus.
    ///
    /// Zustandsautomat:
    ///   Patrol  - laeuft Zufallsziele in der Naehe ab
    ///   Chase   - Ziel gesehen, laeuft hin bis auf Kampfabstand
    ///   Combat  - in Reichweite und Sicht: zielt (mit Streuung) und feuert
    ///   Search  - Ziel verloren: geht zur letzten bekannten Stelle, sucht kurz
    ///
    /// Als IAimSource liefert der Bot der NetworkWeapon Ursprung und (leicht
    /// verrauschte) Richtung auf sein Ziel.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Health))]
    public sealed class BotBrain : NetworkBehaviour, IAimSource
    {
        enum State { Patrol, Chase, Combat, Search }

        [SerializeField] BotStats _stats;
        [SerializeField] Transform _eyes;
        [SerializeField] LayerMask _sightBlockers = ~0;

        NavMeshAgent _agent;
        Health _health;
        NetworkWeapon _weapon;

        State _state = State.Patrol;
        bool _active = true;

        Transform _target;
        Vector3 _lastKnownPosition;
        float _memoryTimer;
        float _reactionTimer;
        float _perceptionTimer;
        float _patrolTimer;
        Vector3 _patrolCenter;

        // ~10 Wahrnehmungspruefungen pro Sekunde statt jeden Frame
        const float PerceptionInterval = 0.1f;

        Vector3 EyePosition => _eyes != null ? _eyes.position : transform.position + Vector3.up * 1.6f;

        public Vector3 AimOrigin => EyePosition;
        public Vector3 AimDirection { get; private set; } = Vector3.forward;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _health = GetComponent<Health>();
            _weapon = GetComponent<NetworkWeapon>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                _agent.enabled = false;
                enabled = false;
                return;
            }

            _patrolCenter = transform.position;
            if (_stats != null)
                _agent.speed = _stats.MoveSpeed;
            AimDirection = transform.forward;
        }

        /// <summary>Nur Server: KI an/aus (z.B. waehrend Tod).</summary>
        public void SetActive(bool value)
        {
            _active = value;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && !value)
                _agent.ResetPath();
        }

        void Update()
        {
            if (!IsServer || !_active || _stats == null)
                return;
            if (!_agent.enabled || !_agent.isOnNavMesh)
                return;

            _perceptionTimer -= Time.deltaTime;
            if (_perceptionTimer <= 0f)
            {
                _perceptionTimer = PerceptionInterval;
                UpdatePerception();
            }

            switch (_state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Chase: TickChase(); break;
                case State.Combat: TickCombat(); break;
                case State.Search: TickSearch(); break;
            }
        }

        void UpdatePerception()
        {
            Transform seen = FindVisibleTarget();
            if (seen != null)
            {
                _target = seen;
                _lastKnownPosition = seen.position;
                _memoryTimer = _stats.MemoryTime;

                if (_state == State.Patrol || _state == State.Search)
                {
                    _state = State.Chase;
                    _reactionTimer = _stats.ReactionTime;
                }
            }
            else if (_target != null)
            {
                _memoryTimer -= PerceptionInterval;
                if (_memoryTimer <= 0f)
                {
                    _target = null;
                    _state = State.Search;
                    _memoryTimer = _stats.MemoryTime;
                }
                else if (_state == State.Combat)
                {
                    _state = State.Chase;
                }
            }
        }

        Transform FindVisibleTarget()
        {
            Transform best = null;
            float bestDist = float.MaxValue;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var playerObject = client.PlayerObject;
                if (playerObject == null) continue;

                var playerHealth = playerObject.GetComponent<Health>();
                if (playerHealth == null || !playerHealth.IsAlive) continue;

                Vector3 targetPoint = playerObject.transform.position + Vector3.up * 1.4f;
                Vector3 toTarget = targetPoint - EyePosition;
                float distance = toTarget.magnitude;

                if (distance > _stats.ViewDistance) continue;
                if (Vector3.Angle(transform.forward, toTarget) > _stats.ViewAngle) continue;

                if (IsSightBlocked(toTarget.normalized, distance, playerObject))
                    continue;

                if (distance < bestDist)
                {
                    bestDist = distance;
                    best = playerObject.transform;
                }
            }

            return best;
        }

        bool IsSightBlocked(Vector3 direction, float distance, NetworkObject target)
        {
            var hits = Physics.RaycastAll(EyePosition, direction, distance, _sightBlockers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                var owner = hit.collider.GetComponentInParent<NetworkObject>();
                if (owner == NetworkObject) continue; // eigener Koerper
                if (owner == target) return false;    // Ziel zuerst getroffen -> frei
                return true;                          // etwas anderes blockiert
            }
            return false;
        }

        void TickPatrol()
        {
            _patrolTimer -= Time.deltaTime;
            if (_patrolTimer <= 0f || !_agent.hasPath)
            {
                _patrolTimer = Random.Range(3f, 6f);
                Vector2 offset = Random.insideUnitCircle * _stats.PatrolRadius;
                Vector3 goal = _patrolCenter + new Vector3(offset.x, 0f, offset.y);
                if (NavMesh.SamplePosition(goal, out NavMeshHit navHit, 4f, NavMesh.AllAreas))
                    _agent.SetDestination(navHit.position);
            }
        }

        void TickChase()
        {
            if (_target != null)
                _lastKnownPosition = _target.position;

            _agent.SetDestination(_lastKnownPosition);

            float distance = Vector3.Distance(transform.position, _lastKnownPosition);
            if (_target != null && distance <= _stats.CombatRange)
                _state = State.Combat;
        }

        void TickCombat()
        {
            if (_target == null)
            {
                _state = State.Search;
                return;
            }

            _lastKnownPosition = _target.position;

            float distance = Vector3.Distance(transform.position, _target.position);
            if (distance > _stats.CombatRange * 1.1f)
            {
                _agent.SetDestination(_target.position);
            }
            else
            {
                _agent.ResetPath();
            }

            FaceAndAim(_target.position + Vector3.up * 1.3f);

            _reactionTimer -= Time.deltaTime;
            if (_reactionTimer <= 0f && _weapon != null)
                _weapon.ServerTryFire();
        }

        void TickSearch()
        {
            _agent.SetDestination(_lastKnownPosition);
            _memoryTimer -= Time.deltaTime;

            float distance = Vector3.Distance(transform.position, _lastKnownPosition);
            if (distance < 1.5f || _memoryTimer <= 0f)
            {
                _state = State.Patrol;
                _patrolCenter = transform.position;
                _patrolTimer = 0f;
            }
        }

        void FaceAndAim(Vector3 targetPoint)
        {
            Vector3 flatDir = targetPoint - transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(flatDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, 8f * Time.deltaTime);
            }

            Vector3 aimDir = (targetPoint - EyePosition).normalized;
            // Zielfehler: kleiner Zufallskegel
            float spreadRad = _stats.AimSpread * Mathf.Deg2Rad;
            Vector3 noise = Random.insideUnitSphere * Mathf.Tan(spreadRad);
            AimDirection = (aimDir + noise).normalized;
        }

        // Fuer Tests einsehbar
        public bool HasTarget => _target != null;
        public string CurrentState => _state.ToString();
    }
}
