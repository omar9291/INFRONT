using System.Collections.Generic;
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

        /// <summary>Nur fuer Tests: legt ALLE Bots still (kein Denken, kein Bewegen, kein Schiessen).</summary>
        public static bool GloballyFrozen;

        [SerializeField] BotStats _stats;
        [SerializeField] Transform _eyes;
        [SerializeField] LayerMask _sightBlockers = ~0;
        [Tooltip("Wie weit der Patrouillen-Punkt beim Rundenstart Richtung Kartenmitte vorrueckt.")]
        [SerializeField] float _advanceDistance = 18f;

        NavMeshAgent _agent;
        Health _health;
        NetworkWeapon _weapon;
        TeamMember _team;
        readonly List<TeamMember> _enemyBuffer = new();

        State _state = State.Patrol;
        bool _active = true;
        float _blindUntil;          // Time.time, bis dahin geblendet (Blendgranate)
        AbilityHolder _abilities;
        float _abilityCheck;        // naechste Pruefung "soll ich eine Faehigkeit zuenden?"
        float _calloutTimer;        // Sperre zwischen zwei Ansagen dieses Bots
        Vector3 _aimError;          // menschlicher Zielfehler, klingt ab
        float _aimErrorRefresh;     // naechster Zielfehler-Stoss
        Vector3 _smoothedAimDir = Vector3.forward;
        bool _helpCalled;           // "Brauche Hilfe" nur einmal pro Kampf

        Transform _target;
        Vector3 _lastKnownPosition;
        float _memoryTimer;
        float _reactionTimer;
        float _perceptionTimer;
        float _patrolTimer;
        Vector3 _patrolCenter;      // aktueller Patrouillen-Mittelpunkt
        Vector3 _baseAnchor;        // Vorrueck-Punkt Richtung Mitte (Rueckfallziel)
        bool _objectiveActive;      // true, solange ein Bomben-Auftrag laeuft

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
            _team = GetComponent<TeamMember>();
            _abilities = GetComponent<AbilityHolder>();
        }

        /// <summary>Nur Server: den Bot fuer ein paar Sekunden blenden
        /// (Blendgranate). Er sieht nichts, schiesst nicht und weicht zurueck.</summary>
        public void ServerBlind(float seconds)
        {
            if (!IsServer || seconds <= 0f) return;
            _blindUntil = Mathf.Max(_blindUntil, Time.time + seconds);
        }

        /// <summary>Ist der Bot gerade geblendet?</summary>
        public bool IsBlind => Time.time < _blindUntil;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                _agent.enabled = false;
                enabled = false;
                return;
            }

            _patrolCenter = _baseAnchor = transform.position;
            if (_stats != null)
                _agent.speed = _stats.MoveSpeed;
            AimDirection = transform.forward;
        }

        /// <summary>Nur Server: Schwierigkeits-Kennwerte setzen (Menue-Auswahl).</summary>
        public void SetStats(BotStats stats)
        {
            if (stats == null) return;
            _stats = stats;
            if (_agent != null) _agent.speed = stats.MoveSpeed;
        }

        /// <summary>
        /// Nur Server: nach dem Teleport an den Spawn den Patrouillen-Punkt ein
        /// Stueck Richtung Kartenmitte schieben (entlang der Blickrichtung, die
        /// beim Spawn zum Gegner zeigt). Ohne das patrouillieren beide Teams
        /// nur in einer Blase um ihren eigenen Spawn und treffen sich nie.
        /// </summary>
        public void ServerAnchorForward()
        {
            if (!IsServer) return;
            Vector3 ahead = transform.position + transform.forward * _advanceDistance;
            _baseAnchor = NavMesh.SamplePosition(ahead, out NavMeshHit hit, 6f, NavMesh.AllAreas)
                ? hit.position
                : transform.position;
            if (!_objectiveActive)
            {
                _patrolCenter = _baseAnchor;
                _patrolTimer = 0f;
            }
        }

        /// <summary>Nur Server: einen festen Zielpunkt vorgeben (Bomben-Auftrag).
        /// Kampf hat weiter Vorrang - das hier steuert nur das Umherlaufen.</summary>
        public void ServerSetObjective(Vector3 point)
        {
            if (!IsServer) return;
            _objectiveActive = true;
            _patrolCenter = point;
        }

        /// <summary>Nur Server: Bomben-Auftrag beenden, zurueck zum Vorrueck-Punkt.</summary>
        public void ServerClearObjective()
        {
            if (!IsServer || !_objectiveActive) return;
            _objectiveActive = false;
            _patrolCenter = _baseAnchor;
            _patrolTimer = 0f;
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
            if (!IsServer || !_active || GloballyFrozen || _stats == null)
                return;
            if (!_agent.enabled || !_agent.isOnNavMesh)
                return;
            if (MatchManager.Instance != null && MatchManager.Instance.IsFrozen)
            {
                if (_agent.hasPath) _agent.ResetPath();
                return;
            }

            _perceptionTimer -= Time.deltaTime;
            if (_perceptionTimer <= 0f)
            {
                _perceptionTimer = PerceptionInterval;
                UpdatePerception();
            }

            // Geblendet: stehen bleiben, nicht schiessen, kurz zurueckziehen.
            if (IsBlind)
            {
                if (_agent.hasPath) _agent.ResetPath();
                AimDirection = transform.forward;
                return;
            }

            MaybeUseAbility();

            switch (_state)
            {
                case State.Patrol: TickPatrol(); break;
                case State.Chase: TickChase(); break;
                case State.Combat: TickCombat(); break;
                case State.Search: TickSearch(); break;
            }

            // Ausserhalb des Kampfes zeigt die Blickrichtung einfach nach vorne.
            // Sonst behaelt ein Zuschauer die alte, eingefrorene Kampf-Richtung.
            if (_state != State.Combat)
            {
                AimDirection = transform.forward;
                _smoothedAimDir = transform.forward;
            }
        }

        void UpdatePerception()
        {
            Transform seen = FindVisibleTarget();
            if (seen != null)
            {
                bool freshSpot = _target == null;
                _target = seen;
                _lastKnownPosition = seen.position;
                _memoryTimer = _stats.MemoryTime;

                if (_state == State.Patrol || _state == State.Search)
                {
                    _state = State.Chase;
                    _reactionTimer = _stats.ReactionTime;
                    _helpCalled = false;
                    if (freshSpot) Callout("Feind gesichtet!", 0.5f);
                }
                return;
            }

            if (_target != null)
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
                return;
            }

            // Nichts gesehen - aber vielleicht etwas GEHOERT (Schuss, Sprint).
            if ((_state == State.Patrol || _state == State.Search)
                && SoundEvents.TryHear(EyePosition,
                        _team != null ? _team.TeamId : Team.None,
                        _stats.Hearing, out Vector3 heardPos))
            {
                _lastKnownPosition = heardPos;
                _memoryTimer = _stats.MemoryTime;
                if (_state != State.Search) Callout("Hoere was!", 0.25f);
                _state = State.Search;
            }
        }

        /// <summary>Eine kurze Ansage in den Kill-Feed (fuer alle sichtbar),
        /// gedrosselt und abhaengig von der Teamwork-Stufe.</summary>
        void Callout(string text, float minTeamwork)
        {
            if (_stats == null || _stats.Teamwork < minTeamwork) return;
            if (Time.time < _calloutTimer) return;
            _calloutTimer = Time.time + Random.Range(4f, 8f);

            string tag = _team != null && !string.IsNullOrEmpty(_team.DisplayName)
                ? _team.DisplayName : "Bot";
            MatchManager.Instance?.ServerReportCallout($"{tag}: {text}",
                _team != null ? _team.TeamId : Team.None);
        }

        Transform FindVisibleTarget()
        {
            Transform best = null;
            float bestDist = float.MaxValue;

            int myTeam = _team != null ? _team.TeamId : Team.None;
            Combatants.CollectEnemies(myTeam, _enemyBuffer);

            foreach (var enemy in _enemyBuffer)
            {
                var enemyObject = enemy.GetComponent<NetworkObject>();
                if (enemyObject == null) continue;

                Vector3 targetPoint = enemy.transform.position + Vector3.up * 1.4f;
                Vector3 toTarget = targetPoint - EyePosition;
                float distance = toTarget.magnitude;

                // Vom Scan-Puls aufgeklaert: der Bot "weiss", wo der Gegner ist -
                // ohne Sichtlinie, ohne Blickwinkel (nur solange der Scan haelt).
                bool scanned = ScanRegistry.IsRevealedTo(enemy,
                    _team != null ? _team.TeamId : Team.None);

                if (!scanned)
                {
                    if (distance > _stats.ViewDistance) continue;
                    if (Vector3.Angle(transform.forward, toTarget) > _stats.ViewAngle) continue;
                    if (IsSightBlocked(toTarget.normalized, distance, enemyObject)) continue;
                    if (SmokeRegistry.Blocks(EyePosition, targetPoint)) continue;   // Rauch dazwischen
                }

                if (distance < bestDist)
                {
                    bestDist = distance;
                    best = enemy.transform;
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
            // Mit Bomben-Auftrag: geradewegs zum Zielpunkt und dort STEHEN
            // bleiben (legen / entschaerfen / bewachen). Kein Umherwandern,
            // sonst reisst der Vorgang staendig ab.
            if (_objectiveActive)
            {
                float d = Vector3.Distance(transform.position, _patrolCenter);
                if (d <= 1.8f)
                {
                    if (_agent.hasPath) _agent.ResetPath();
                    return;
                }
                _patrolTimer -= Time.deltaTime;
                if (_patrolTimer <= 0f || !_agent.hasPath)
                {
                    _patrolTimer = 0.5f;
                    if (NavMesh.SamplePosition(_patrolCenter, out NavMeshHit oh, 5f, NavMesh.AllAreas))
                        _agent.SetDestination(oh.position);
                }
                return;
            }

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

            // Aggressivitaet bestimmt den Wunschabstand: defensiv = Winkel halten
            // und auf Abstand bleiben, aggressiv = ranpushen.
            float desired = Mathf.Lerp(_stats.CombatRange * 1.4f, _stats.CombatRange * 0.6f,
                                       Mathf.Clamp01(_stats.Aggression));
            if (distance > desired * 1.15f)
            {
                _agent.SetDestination(_target.position);
            }
            else if (distance < desired * 0.7f)
            {
                Vector3 away = (transform.position - _target.position).normalized * 3f;
                if (NavMesh.SamplePosition(transform.position + away, out NavMeshHit back, 3f, NavMesh.AllAreas))
                    _agent.SetDestination(back.position);   // zu nah -> etwas zurueck
            }
            else
            {
                _agent.ResetPath();
            }

            Vector3 aimPoint = _target.position + Vector3.up * 1.3f;
            FaceAndAim(aimPoint);

            if (!_helpCalled && _health != null && _health.Current <= _health.Max * 0.35f)
            {
                _helpCalled = true;
                Callout("Brauche Hilfe!", 0.4f);
            }

            _reactionTimer -= Time.deltaTime;
            if (_reactionTimer <= 0f && _weapon != null && AimIsOnTarget(aimPoint))
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
                // Nicht am Ort des verlorenen Kampfes kleben bleiben, sondern
                // zum Auftrag / Vorrueck-Punkt zurueck.
                _patrolCenter = _objectiveActive ? _patrolCenter : _baseAnchor;
                _patrolTimer = 0f;
            }
        }

        /// <summary>Ab und zu pruefen, ob eine Faehigkeit gerade Sinn ergibt.
        /// Angreifer rauchen eine Engstelle ein und blenden vor dem Sturm.</summary>
        void MaybeUseAbility()
        {
            if (_abilities == null) return;
            _abilityCheck -= Time.deltaTime;
            if (_abilityCheck > 0f) return;
            _abilityCheck = 1f;

            // Blendgranate (G): Ziel gesehen, mittlere Distanz -> vor dem Sturm werfen.
            if (_target != null && (_state == State.Chase || _state == State.Combat)
                && _abilities.ChargesInSlot((int)AbilitySlot.G) > 0)
            {
                float d = Vector3.Distance(transform.position, _target.position);
                if (d > 6f && d < 24f && Random.value < 0.5f)
                {
                    FaceAndAim(_target.position + Vector3.up * 1.3f);
                    if (_abilities.ServerTryUse((int)AbilitySlot.G)) return;
                }
            }

            // Rauchwand (Q): auf dem Weg zum Ziel, noch nicht im Kampf.
            if (_state == State.Chase && _abilities.ChargesInSlot((int)AbilitySlot.Q) > 0
                && Random.value < 0.35f)
            {
                _abilities.ServerTryUse((int)AbilitySlot.Q);
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

            Vector3 idealDir = (targetPoint - EyePosition).normalized;

            // Menschliches Zielen: die Blickrichtung zieht mit BEGRENZTER
            // Geschwindigkeit nach - kein sofortiges Einrasten.
            float maxStep = _stats.AimTrackSpeed * Mathf.Deg2Rad * Time.deltaTime;
            _smoothedAimDir = Vector3.RotateTowards(_smoothedAimDir, idealDir, maxStep, 1f);
            if (_smoothedAimDir.sqrMagnitude > 0.0001f) _smoothedAimDir.Normalize();

            // Zielfehler: klingt ab, bekommt ab und zu einen neuen Stoss
            // (Ueberkorrektur / kurz daneben / gelegentlicher Fehlschuss).
            _aimError = Vector3.Lerp(_aimError, Vector3.zero, 4f * Time.deltaTime);
            _aimErrorRefresh -= Time.deltaTime;
            if (_aimErrorRefresh <= 0f)
            {
                _aimErrorRefresh = Random.Range(0.25f, 0.75f);
                float mag = Mathf.Tan(_stats.AimSpread * Mathf.Deg2Rad);
                _aimError += Random.insideUnitSphere * mag;
            }

            AimDirection = (_smoothedAimDir + _aimError).normalized;
        }

        /// <summary>Zeigt die Blickrichtung ungefaehr auf das Ziel? (Erst dann feuern -
        /// so wirkt sich das traege Nachziehen wirklich aus.)</summary>
        bool AimIsOnTarget(Vector3 targetPoint)
        {
            Vector3 ideal = (targetPoint - EyePosition).normalized;
            return Vector3.Angle(AimDirection, ideal) < 6f;
        }

        // Fuer Tests einsehbar
        public bool HasTarget => _target != null;
        public string CurrentState => _state.ToString();
        public Vector3 PatrolCenterForTests => _patrolCenter;
        public Vector3 BaseAnchorForTests => _baseAnchor;
        public bool ObjectiveActiveForTests => _objectiveActive;
    }
}
