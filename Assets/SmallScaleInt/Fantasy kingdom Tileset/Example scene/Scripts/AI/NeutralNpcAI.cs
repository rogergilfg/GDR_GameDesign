using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    /// <summary>
    /// Drop-in friendly AI that can hold position, patrol custom paths and assist players in combat.
    /// Designed to be lightweight and highly tweakable directly from the inspector.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class NeutralNpcAI : MonoBehaviour, EnemyAI.IDamageable
    {
        public enum DefaultBehaviour
        {
            HoldPosition,
            Patrol,
            FreeRoam
        }

        public enum PatrolMode
        {
            Loop,
            PingPong,
            Random
        }

        public enum CombatStyle
        {
            Melee,
            Ranged
        }

        public enum IdleFacingDirection
        {
            East,
            NorthEast,
            North,
            NorthWest,
            West,
            SouthWest,
            South,
            SouthEast
        }

        enum State
        {
            Idle,
            Patrolling,
            Waiting,
            Returning,
            Chasing,
            Attacking,
            Roaming
        }

        [Header("Behaviour")]
        [Tooltip("Initial behaviour when no enemies are around.")]
        public DefaultBehaviour startingBehaviour = DefaultBehaviour.HoldPosition;
        [Tooltip("Optional anchor used when returning after chasing enemies.")]
        public Transform homeAnchor;
        [Tooltip("When true the NPC will run back to its home anchor when combat ends.")]
        public bool returnToPost = true;
        [Tooltip("Time range spent idling between patrol steps.")]
        public Vector2 idlePause = new Vector2(1.2f, 2.8f);

        [Header("Patrol")]
        [Tooltip("World-space patrol points. Leave empty to keep the NPC stationary.")]
        public Transform[] patrolWaypoints;
        [Tooltip("Patrol traversal logic when more than one waypoint is present.")]
        public PatrolMode patrolMode = PatrolMode.Loop;
        [Tooltip("Distance considered close enough to a waypoint.")]
        public float waypointTolerance = 0.15f;
        [Tooltip("Optional offset applied when holding position (helps align to tiles).")]
        public Vector2 holdOffset = Vector2.zero;

        [Header("Idle")]
        [Tooltip("Default direction the NPC faces when idle and not patrolling.")]
        public IdleFacingDirection idleFacingDirection = IdleFacingDirection.East;

        [Header("Free Roam")]
        [Tooltip("Distance range from the home position used when picking roam destinations.")]
        public Vector2 roamDistance = new Vector2(6f, 12f);
        [Tooltip("Time range spent idling between free roam hops.")]
        public Vector2 roamPause = new Vector2(1.5f, 3.5f);
        [Tooltip("Distance considered close enough to a roam destination.")]
        public float roamDestinationTolerance = 0.5f;

        [Header("Locomotion")]
        [Tooltip("Optional Rigidbody2D used for physics-based steering. Falls back to transform translation if omitted.")]
        public Rigidbody2D body;
        [Tooltip("Walking speed used while patrolling or returning to post.")]
        public float walkSpeed = 1.8f;
        [Tooltip("Maximum speed used while chasing hostiles.")]
        public float runSpeed = 3.2f;
        [Tooltip("Acceleration applied when adjusting velocity.")]
        public float acceleration = 18f;
        [Tooltip("Rate at which the NPC slows down when no input is provided.")]
        public float braking = 25f;
        [Tooltip("How quickly the facing direction aligns with the travel direction.")]
        public float facingLerp = 12f;

        [Header("Navigation")]
        [Tooltip("Physics layers considered blocking when steering around obstacles. Leave empty to use the collision matrix.")]
        public LayerMask obstacleMask = ~0;
        [Tooltip("Distance used when probing for obstacles while moving.")]
        public float obstacleCheckDistance = 0.8f;
        [Tooltip("Angle step used when searching for a clear direction during obstacle avoidance.")]
        [Range(5f, 90f)] public float obstacleAvoidanceStep = 25f;
        [Tooltip("Maximum number of step increments tested on each side when avoiding an obstacle.")]
        public int obstacleAvoidanceIterations = 3;
        [Tooltip("Minimum distance considered progress before resetting the stuck detector.")]
        public float stuckDistance = 0.2f;
        [Tooltip("Seconds without progress before forcing a repath.")]
        public float stuckTime = 1.5f;

        [Header("Awareness")]
        [Tooltip("Layers considered hostile.")]
        public LayerMask enemyMask;
        [Tooltip("Radius used to spot hostile targets.")]
        public float detectionRadius = 7f;
        [Tooltip("Once an enemy exceeds this distance the NPC will give up the chase.")]
        public float loseInterestRadius = 10f;
        [Tooltip("If assigned the NPC will prioritise this transform as its target.")]
        public Transform forcedTarget;
        [Tooltip("Draw debug gizmos in the scene view.")]
        public bool showDebugGizmos = true;

        [Header("Combat")]
        public CombatStyle combatStyle = CombatStyle.Melee;
        [Tooltip("Distance from which melee hits land or ranged shots are fired.")]
        public float attackRange = 1.5f;
        [Tooltip("Preferred separation maintained when using ranged attacks.")]
        public float preferredRangedDistance = 5f;
        [Tooltip("Seconds between two consecutive attacks.")]
        public float attackCooldown = 1.2f;
        [Tooltip("Randomised cooldown variance.")]
        public Vector2 attackCooldownJitter = new Vector2(-0.15f, 0.25f);
        [Tooltip("Delay between triggering the animation and applying damage/projectile.")]
        public float attackWindup = 0.25f;
        [Tooltip("Optional delay after the attack finishes before movement resumes.")]
        public float attackRecovery = 0.25f;
        [Tooltip("Base damage applied to EnemyAI targets.")]
        public int attackDamage = 10;
        [Tooltip("Animator triggers randomly selected when starting an attack.")]
        public string[] attackTriggers = new[] { "Attack1", "Attack2" };
        [Tooltip("Projectile prefab used when CombatStyle is Ranged. Must have a Projectile2D component.")]
        public Projectile2D projectilePrefab;
        [Tooltip("Optional spawn transform for projectiles. Falls back to this object position.")]
        public Transform projectileSpawn;
        [Tooltip("Initial projectile speed when using ranged style.")]
        public float projectileSpeed = 10f;
        [Tooltip("Lifetime for spawned projectiles.")]
        public float projectileLife = 4f;

        [Header("Health")]
        [Tooltip("Maximum health for this NPC.")]
        public int maxHealth = 40;
        [Tooltip("Health value used when the scene starts. If zero or negative defaults to maxHealth.")]
        [SerializeField] int currentHealth = 40;
        public int CurrentHealth => currentHealth;
        [Tooltip("Seconds of invulnerability granted after taking damage.")]
        public float invulnerability = 0.1f;
        [Tooltip("Animator trigger fired when the NPC takes damage.")]
        public string takeDamageTrigger = "TakeDamage";
        [Tooltip("Animator trigger fired when the NPC dies.")]
        public string deathTrigger = "Die";

        [Header("Death Animation")]
        [Tooltip("Optional exact animator state path of the death animation. Leave empty to rely on the tag.")]
        public string deathStatePath = "Base Layer.TakeDamage/Die/Die_E";
        [Tooltip("Animator state tag used to identify the death animation when holding the final pose.")]
        public string deathStateTag = "Death";
        [Tooltip("Freeze the animator on the last frame of the death animation once it finishes.")]
        public bool holdLastFrameOnDeath = true;
        [Tooltip("Normalized time threshold considered the end of the death animation before freezing.")]
        [Range(0.8f, 1f)] public float deathHoldNormalizedTime = 0.99f;
        [Tooltip("Maximum seconds to wait for the death animation before giving up on holding the pose.")]
        public float deathHoldTimeout = 5f;

        [Header("Events")]
        public UnityEvent onAggro;
        public UnityEvent onTargetLost;
        public UnityEvent onAttack;
        public UnityEvent onDamaged;
        public UnityEvent onDeath;

        Animator _anim;
        Rigidbody2D _rb;
        readonly Collider2D[] _scanBuffer = new Collider2D[8];

        Vector2 _homePosition;
        Vector2 _desiredVelocity;
        Vector2 _currentVelocity;
        Vector2 _facing = Vector2.right;

        readonly List<Vector2> _pathBuffer = new List<Vector2>();
        int _pathIndex = 0;
        Vector2 _pathDestination = Vector2.zero;
        float _pathNextRebuildAt = 0f;
        bool _pathValid = false;
        readonly RaycastHit2D[] _avoidanceHits = new RaycastHit2D[8];

        State _state = State.Idle;
        int _patrolIndex = 0;
        int _patrolDirection = 1;
        float _waitUntil = -1f;
        float _nextAttackAt = -1f;
        Coroutine _attackRoutine;
        Transform _target;
        bool _hasAggroed = false;

        Vector2 _roamDestination;
        bool _hasRoamDestination;

        Collider2D[] _colliders;
        float _avoidanceRadius = 0.3f;
        int _obstacleLayerMask = ~0;
        float _canBeHitUntil = -1f;
        bool _isDead = false;
        public bool IsDead => _isDead;

        /// <summary>Set by movement logic - true when the NPC is moving.</summary>
        [HideInInspector] public bool isMoving = false;
        EnemyAI _currentEnemy;
        int _deathStateHash = 0;
        Coroutine _deathHoldRoutine;
        Vector2 _lastProgressPos;
        float _lastProgressTime;
        float _lastForcedRepathAt;
        int _consecutiveStuckRebuilds;

        readonly int H_Direction      = Animator.StringToHash("Direction");
        readonly int H_DirIndex       = Animator.StringToHash("DirIndex");
        readonly int H_IsWalk         = Animator.StringToHash("IsWalk");
        readonly int H_IsRun          = Animator.StringToHash("IsRun");
        readonly int H_IsRunBackwards = Animator.StringToHash("IsRunBackwards");
        readonly int H_IsStrafeLeft   = Animator.StringToHash("IsStrafeLeft");
        readonly int H_IsStrafeRight  = Animator.StringToHash("IsStrafeRight");

        void Awake()
        {
            _anim = GetComponent<Animator>();
            _rb   = body ? body : GetComponent<Rigidbody2D>();

            if (_rb)
            {
                _rb.gravityScale = 0f;
                _rb.linearDamping = 0f;
                _rb.angularDamping = 0f;
                _rb.freezeRotation = true;
            }

            _colliders = GetComponentsInChildren<Collider2D>(true);
            currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 1, maxHealth);

            _avoidanceRadius = Mathf.Max(0.05f, CalculateAvoidanceRadius());
            _obstacleLayerMask = obstacleMask.value != 0 ? obstacleMask.value : Physics2D.GetLayerCollisionMask(gameObject.layer);
            _lastProgressPos = transform.position;
            _lastProgressTime = Time.time;
            _lastForcedRepathAt = -10f;
            _consecutiveStuckRebuilds = 0;

            _deathStateHash = string.IsNullOrEmpty(deathStatePath) ? 0 : Animator.StringToHash(deathStatePath);

            TilemapPathfinder.EnsureInitialized();
            ClearPath();

            _homePosition = homeAnchor ? (Vector2)homeAnchor.position : (Vector2)transform.position + holdOffset;
            if (startingBehaviour == DefaultBehaviour.Patrol && HasPatrolPath())
            {
                _state = State.Patrolling;
                _waitUntil = -1f;
            }
            else if (startingBehaviour == DefaultBehaviour.FreeRoam)
            {
                _state = State.Roaming;
                _waitUntil = -1f;
                _hasRoamDestination = false;
            }
            else
            {
                _state = State.Idle;
                _waitUntil = Time.time + Random.Range(idlePause.x, idlePause.y);
            }

            _facing = IdleDirectionToVector(idleFacingDirection);
        }

        Collider2D GetPrimaryCollider()
        {
            if (_colliders == null) return null;
            for (int i = 0; i < _colliders.Length; i++)
            {
                var c = _colliders[i];
                if (c && c.enabled) return c;
            }
            for (int i = 0; i < _colliders.Length; i++)
            {
                var c = _colliders[i];
                if (c) return c;
            }
            return null;
        }

        void OnDisable()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_deathHoldRoutine != null)
            {
                StopCoroutine(_deathHoldRoutine);
                _deathHoldRoutine = null;
            }

            if (_currentEnemy)
            {
                _currentEnemy.UnregisterHostile(transform);
                _currentEnemy = null;
            }

            ClearPath();
        }

        void Update()
        {
            if (_isDead)
                return;

            RefreshTarget();
            UpdateStateMachine();
            UpdateAnimator();
        }

        void FixedUpdate()
        {
            if (_isDead)
            {
                if (_rb) _rb.linearVelocity = Vector2.zero;
                return;
            }
            ApplyMovement();
        }

        void RefreshTarget()
        {
            if (_isDead)
            {
                if (_currentEnemy)
                {
                    _currentEnemy.UnregisterHostile(transform);
                    _currentEnemy = null;
                }
                _target = null;
                return;
            }

            Transform newTarget = forcedTarget ? forcedTarget : AcquireTarget();
            EnemyAI newEnemy = newTarget ? newTarget.GetComponentInParent<EnemyAI>() : null;

            if (newEnemy)
            {
                bool enemyChanged = newEnemy != _currentEnemy;
                if (enemyChanged && _currentEnemy)
                    _currentEnemy.UnregisterHostile(transform);

                bool forceEngage = enemyChanged || !_hasAggroed;
                newEnemy.RegisterHostile(transform, GetPrimaryCollider(), forceEngage);
                _currentEnemy = newEnemy;
            }
            else if (_currentEnemy)
            {
                _currentEnemy.UnregisterHostile(transform);
                _currentEnemy = null;
            }

            if (newTarget && newTarget != _target)
            {
                _target = newTarget;
                ClearPath();
                if (!_hasAggroed)
                {
                    _hasAggroed = true;
                    onAggro?.Invoke();
                }
            }
            else if (!newTarget && _target)
            {
                _target = null;
                _hasAggroed = false;
                onTargetLost?.Invoke();
                ClearPath();
            }
        }

        Transform AcquireTarget()
        {
            float bestDist = float.MaxValue;
            Transform best = null;

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hits = Physics2D.OverlapCircle(transform.position, detectionRadius, filter, _scanBuffer);
            for (int i = 0; i < hits; i++)
            {
                var c = _scanBuffer[i];
                if (!c) continue;
                if (c.transform == transform || c.transform.IsChildOf(transform))
                    continue;

                float dist = Vector2.Distance(transform.position, c.transform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = c.transform;
                }
            }

            if (best && bestDist > loseInterestRadius)
                best = null;

            return best;
        }

        void UpdateStateMachine()
        {
            Vector2 pos = transform.position;
            Vector2 desiredVel = Vector2.zero;
            float desiredSpeed = 0f;

            bool hasTarget = _target != null;

            if (hasTarget)
            {
                float dist = Vector2.Distance(pos, _target.position);
                if (dist > loseInterestRadius)
                {
                    hasTarget = false;
                    _target = null;
                    _hasAggroed = false;
                    onTargetLost?.Invoke();
                    ClearPath();
                }
            }

            if (hasTarget)
            {
                Vector2 toTarget = ((Vector2)_target.position - pos);
                float dist = toTarget.magnitude;
                Vector2 dir = dist > 0.001f ? toTarget / dist : Vector2.zero;
                Vector2 pathDir = GetPathDirection((Vector2)_target.position, 0.25f, 0.2f, 0.18f);
                if (pathDir.sqrMagnitude < 0.0001f)
                    pathDir = dir;

                if (combatStyle == CombatStyle.Ranged)
                {
                    float buffer = 0.3f;
                    if (dist > preferredRangedDistance + buffer)
                    {
                        desiredVel = pathDir;
                        desiredSpeed = runSpeed;
                        _state = State.Chasing;
                    }
                    else if (dist < preferredRangedDistance - buffer)
                    {
                        desiredVel = -dir;
                        desiredSpeed = walkSpeed;
                        _state = State.Chasing;
                    }
                    else
                    {
                        desiredVel = Vector2.zero;
                        desiredSpeed = 0f;
                    }
                }
                else
                {
                    if (dist > attackRange * 0.9f)
                    {
                        desiredVel = pathDir;
                        desiredSpeed = runSpeed;
                        _state = State.Chasing;
                    }
                    else
                    {
                        desiredVel = Vector2.zero;
                        desiredSpeed = 0f;
                    }
                }

                TryStartAttack(dir, dist);

                if (_attackRoutine != null)
                {
                    desiredVel = Vector2.zero;
                    desiredSpeed = 0f;
                }
            }
            else
            {
                if (_attackRoutine != null)
                {
                    StopCoroutine(_attackRoutine);
                    _attackRoutine = null;
                }

                if (returnToPost && !ShouldFreeRoam() && Vector2.Distance(pos, _homePosition) > waypointTolerance)
                {
                    Vector2 toHome = _homePosition - pos;
                    Vector2 pathDir = GetPathDirection(_homePosition, 0.35f, waypointTolerance, 0.18f);
                    desiredVel = pathDir.sqrMagnitude > 0.0001f ? pathDir : toHome.normalized;
                    desiredSpeed = walkSpeed;
                    _state = State.Returning;
                }
                else if (ShouldFreeRoam())
                {
                    RunFreeRoam(ref desiredVel, ref desiredSpeed, pos);
                }
                else if (HasPatrolPath())
                {
                    RunPatrol(ref desiredVel, ref desiredSpeed, pos);
                }
                else
                {
                    _state = State.Idle;
                    desiredVel = Vector2.zero;
                    desiredSpeed = 0f;
                    if (_waitUntil < Time.time)
                        _waitUntil = Time.time + Random.Range(idlePause.x, idlePause.y);
                    ClearPath();
                }
            }

            float accel = desiredSpeed > _currentVelocity.magnitude ? acceleration : braking;
            Vector2 steeringDir = desiredVel.sqrMagnitude > 0.0001f && desiredSpeed > 0.01f
                ? ApplyObstacleAvoidance(desiredVel.normalized, desiredSpeed)
                : desiredVel;
            Vector2 targetVel = steeringDir * desiredSpeed;
            _currentVelocity = Vector2.MoveTowards(_currentVelocity, targetVel, accel * Time.deltaTime);
            _desiredVelocity = _currentVelocity;

            Vector2 desiredFacing = _facing;

            if (_currentVelocity.sqrMagnitude > 0.0001f)
            {
                desiredFacing = _currentVelocity.normalized;
            }
            else if (_target)
            {
                Vector2 dir = ((Vector2)_target.position - pos).normalized;
                if (dir.sqrMagnitude > 0.0001f)
                    desiredFacing = dir;
            }
            else if (_state == State.Idle || _state == State.Waiting)
            {
                desiredFacing = IdleDirectionToVector(idleFacingDirection);
            }

            if (desiredFacing.sqrMagnitude > 0.0001f)
            {
                _facing = Vector2.Lerp(_facing, desiredFacing.normalized, 1f - Mathf.Exp(-facingLerp * Time.deltaTime));
            }

            HandleStuck(desiredSpeed);
        }

        void RunPatrol(ref Vector2 desiredDir, ref float desiredSpeed, Vector2 currentPos)
        {
            if (!HasPatrolPath())
            {
                _state = State.Idle;
                desiredDir = Vector2.zero;
                desiredSpeed = 0f;
                ClearPath();
                return;
            }

            Transform waypoint = patrolWaypoints[_patrolIndex];
            Vector2 target = waypoint ? (Vector2)waypoint.position : currentPos;
            float dist = Vector2.Distance(currentPos, target);

            if (_state == State.Waiting)
            {
                desiredDir = Vector2.zero;
                desiredSpeed = 0f;
                ClearPath();

                if (Time.time >= _waitUntil)
                {
                    AdvancePatrolIndex();
                    _waitUntil = -1f;
                    _state = State.Patrolling;
                }
                return;
            }

            if (dist <= waypointTolerance)
            {
                _waitUntil = Time.time + Random.Range(idlePause.x, idlePause.y);
                desiredDir = Vector2.zero;
                desiredSpeed = 0f;
                _state = State.Waiting;
                ClearPath();
            }
            else
            {
                Vector2 pathDir = GetPathDirection(target, 0.5f, waypointTolerance * 0.5f, Mathf.Max(0.1f, waypointTolerance * 0.5f));
                if (pathDir.sqrMagnitude < 0.0001f)
                    pathDir = (target - currentPos).normalized;
                desiredDir = pathDir;
                desiredSpeed = walkSpeed;
                _state = State.Patrolling;
            }
        }

        void RunFreeRoam(ref Vector2 desiredDir, ref float desiredSpeed, Vector2 currentPos)
        {
            float tolerance = Mathf.Max(0.05f, roamDestinationTolerance);
            bool needsDestination = !_hasRoamDestination;

            if (!needsDestination)
            {
                float dist = Vector2.Distance(currentPos, _roamDestination);
                if (dist <= tolerance)
                {
                    needsDestination = true;
                }
            }

            if (needsDestination)
            {
                if (!TryPickRoamDestination(currentPos))
                {
                    _state = State.Idle;
                    desiredDir = Vector2.zero;
                    desiredSpeed = 0f;
                    return;
                }
            }

            Vector2 pathDir = GetPathDirection(_roamDestination, 0.6f, tolerance, Mathf.Max(0.1f, tolerance * 0.5f));
            if (pathDir.sqrMagnitude < 0.0001f)
                pathDir = (_roamDestination - currentPos).normalized;

            desiredDir = pathDir;
            desiredSpeed = walkSpeed;
            _state = State.Roaming;
        }

        bool TryPickRoamDestination(Vector2 currentPos)
        {
            float minDistance = Mathf.Max(0.5f, Mathf.Min(roamDistance.x, roamDistance.y));
            float maxDistance = Mathf.Max(minDistance + 0.1f, Mathf.Max(roamDistance.x, roamDistance.y));
            Vector2 origin = returnToPost ? _homePosition : currentPos;
            const int maxAttempts = 8;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                Vector2 dir = Random.insideUnitCircle;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector2.right;
                }
                else
                {
                    dir.Normalize();
                }

                float lerpT = maxAttempts <= 1 ? 0f : (float)attempt / (maxAttempts - 1);
                float distance = Mathf.Lerp(maxDistance, minDistance, lerpT);
                Vector2 candidate = origin + dir * distance;

                if (!TilemapPathfinder.HasColliderMap)
                {
                    _roamDestination = candidate;
                    _hasRoamDestination = true;
                    _consecutiveStuckRebuilds = 0;
                    return true;
                }

                if (TryBuildPath(candidate))
                {
                    _roamDestination = candidate;
                    _hasRoamDestination = true;
                    _consecutiveStuckRebuilds = 0;
                    return true;
                }
            }

            _hasRoamDestination = false;
            ClearPath();
            return false;
        }

        void AdvancePatrolIndex()
        {
            if (!HasPatrolPath()) return;

            switch (patrolMode)
            {
                case PatrolMode.Loop:
                    _patrolIndex = (_patrolIndex + 1) % patrolWaypoints.Length;
                    break;
                case PatrolMode.PingPong:
                    if (patrolWaypoints.Length <= 1) return;
                    _patrolIndex += _patrolDirection;
                    if (_patrolIndex >= patrolWaypoints.Length)
                    {
                        _patrolDirection = -1;
                        _patrolIndex = Mathf.Max(0, patrolWaypoints.Length - 2);
                    }
                    else if (_patrolIndex < 0)
                    {
                        _patrolDirection = 1;
                        _patrolIndex = Mathf.Min(patrolWaypoints.Length - 1, 1);
                    }
                    break;
                case PatrolMode.Random:
                    if (patrolWaypoints.Length <= 1) return;
                    int next = _patrolIndex;
                    while (next == _patrolIndex)
                        next = Random.Range(0, patrolWaypoints.Length);
                    _patrolIndex = next;
                    break;
            }

            ClearPath();
        }

        bool ShouldFreeRoam()
        {
            return startingBehaviour == DefaultBehaviour.FreeRoam;
        }

        bool HasPatrolPath()
        {
            if (patrolWaypoints == null || patrolWaypoints.Length == 0)
                return false;

            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i])
                    return true;
            }

            return false;
        }

        void TryStartAttack(Vector2 dirToTarget, float distToTarget)
        {
            if (_attackRoutine != null) return;
            if (_target == null) return;

            if (combatStyle == CombatStyle.Melee)
            {
                if (distToTarget > attackRange + 0.1f) return;
            }
            else
            {
                if (distToTarget > Mathf.Max(attackRange, preferredRangedDistance + 1f)) return;
            }

            if (Time.time < _nextAttackAt) return;

            _attackRoutine = StartCoroutine(AttackRoutine(dirToTarget));
        }

        IEnumerator AttackRoutine(Vector2 dir)
        {
            _state = State.Attacking;
            _desiredVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;

            Vector2 forward = dir.sqrMagnitude > 0.0001f ? dir.normalized : _facing;
            _facing = forward;

            string trig = (attackTriggers != null && attackTriggers.Length > 0)
                ? attackTriggers[Random.Range(0, attackTriggers.Length)]
                : string.Empty;

            if (!string.IsNullOrEmpty(trig))
                _anim.SetTrigger(trig);

            onAttack?.Invoke();

            if (attackWindup > 0f)
                yield return new WaitForSeconds(attackWindup);

            if (combatStyle == CombatStyle.Melee)
                PerformMeleeHit(forward);
            else
                FireProjectile(forward);

            if (attackRecovery > 0f)
                yield return new WaitForSeconds(attackRecovery);

            float jitter = Random.Range(attackCooldownJitter.x, attackCooldownJitter.y);
            _nextAttackAt = Time.time + Mathf.Max(0.05f, attackCooldown + jitter);

            _attackRoutine = null;
        }

        void PerformMeleeHit(Vector2 dir)
        {
            if (!_target) return;

            var damageable = _target.GetComponentInParent<EnemyAI.IDamageable>();
            if (damageable != null)
            {
                int appliedDamage = Mathf.Max(1, attackDamage);
                damageable.TakeDamage(appliedDamage, dir);
                EnemyAI.NotifyDamageDealt(damageable, transform, appliedDamage);
            }
        }

        void FireProjectile(Vector2 dir)
        {
            if (!projectilePrefab)
            {
                PerformMeleeHit(dir);
                return;
            }

            Vector3 spawnPos = projectileSpawn ? projectileSpawn.position : transform.position;
            var proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            Vector2 shotDir = dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;
            proj.Init(shotDir * projectileSpeed, Mathf.Max(1, attackDamage), projectileLife, enemyMask, transform);
        }

        public void TakeDamage(int amount, Vector2 hitDir)
        {
            if (_isDead) return;
            if (Time.time < _canBeHitUntil) return;

            int dmg = Mathf.Max(1, amount);
            var shieldHandler = AbilityShieldHandler.GetExisting(transform);
            if (shieldHandler != null)
            {
                dmg = shieldHandler.AbsorbDamage(dmg);
                if (dmg <= 0)
                {
                    _canBeHitUntil = Time.time + Mathf.Max(0f, invulnerability);
                    return;
                }
            }

            currentHealth = Mathf.Clamp(currentHealth - dmg, 0, maxHealth);
            _canBeHitUntil = Time.time + Mathf.Max(0f, invulnerability);

            if (currentHealth <= 0)
            {
                HandleDeath();
                return;
            }

            if (_anim && !string.IsNullOrEmpty(takeDamageTrigger))
                _anim.SetTrigger(takeDamageTrigger);

            onDamaged?.Invoke();
        }

        void HandleDeath()
        {
            if (_isDead) return;
            _isDead = true;

            if (_currentEnemy)
            {
                _currentEnemy.UnregisterHostile(transform);
                _currentEnemy = null;
            }

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_deathHoldRoutine != null)
            {
                StopCoroutine(_deathHoldRoutine);
                _deathHoldRoutine = null;
            }

            _state = State.Idle;
            _target = null;
            _desiredVelocity = Vector2.zero;
            _currentVelocity = Vector2.zero;
            ClearPath();

            if (_rb)
                _rb.linearVelocity = Vector2.zero;

            if (_anim)
            {
                if (!string.IsNullOrEmpty(takeDamageTrigger))
                    _anim.ResetTrigger(takeDamageTrigger);
                if (!string.IsNullOrEmpty(deathTrigger))
                    _anim.SetTrigger(deathTrigger);
                _anim.SetBool(H_IsWalk, false);
                _anim.SetBool(H_IsRun, false);
                _anim.SetBool(H_IsRunBackwards, false);
                _anim.SetBool(H_IsStrafeLeft, false);
                _anim.SetBool(H_IsStrafeRight, false);

                if (holdLastFrameOnDeath)
                {
                    _anim.speed = 1f;
                    _deathHoldRoutine = StartCoroutine(HoldDeathPoseRoutine());
                }
            }

            if (_colliders != null)
            {
                foreach (var c in _colliders)
                {
                    if (c) c.enabled = false;
                }
            }

            onTargetLost?.Invoke();
            onDeath?.Invoke();
        }

        IEnumerator HoldDeathPoseRoutine()
        {
            if (!_anim) yield break;

            float startTime = Time.time;
            while (Time.time - startTime < deathHoldTimeout)
            {
                var current = _anim.GetCurrentAnimatorStateInfo(0);
                var next = _anim.GetNextAnimatorStateInfo(0);

                if (IsDeathState(current) || IsDeathState(next))
                {
                    while (!IsDeathState(current))
                    {
                        yield return null;
                        current = _anim.GetCurrentAnimatorStateInfo(0);
                    }

                    while (IsDeathState(current) && current.normalizedTime < deathHoldNormalizedTime)
                    {
                        yield return null;
                        current = _anim.GetCurrentAnimatorStateInfo(0);
                    }

                    _anim.Play(current.fullPathHash, 0, 0.999f);
                    _anim.Update(0f);
                    _anim.speed = 0f;
                    break;
                }

                yield return null;
            }

            _deathHoldRoutine = null;
        }

        bool IsDeathState(AnimatorStateInfo state)
        {
            if (state.fullPathHash == 0)
                return false;

            if (_deathStateHash != 0 && state.fullPathHash == _deathStateHash)
                return true;

            if (!string.IsNullOrEmpty(deathStateTag) && state.IsTag(deathStateTag))
                return true;

            return false;
        }

        void ClearPath()
        {
            _pathBuffer.Clear();
            _pathIndex = 0;
            _pathValid = false;
            _pathDestination = transform.position;
            _pathNextRebuildAt = Time.time;
        }

        Vector2 ApplyObstacleAvoidance(Vector2 desiredDir, float desiredSpeed)
        {
            if (obstacleCheckDistance <= 0f || _avoidanceRadius <= 0f)
                return desiredDir;

            float checkDistance = Mathf.Max(0.05f, obstacleCheckDistance * Mathf.Max(1f, desiredSpeed / Mathf.Max(0.01f, walkSpeed)));

            if (!IsDirectionBlocked(desiredDir, checkDistance))
                return desiredDir;

            Vector2 bestDir = desiredDir;
            float bestScore = -Mathf.Infinity;
            bool found = false;

            int iterations = Mathf.Max(1, obstacleAvoidanceIterations);
            for (int step = 1; step <= iterations; step++)
            {
                float angle = obstacleAvoidanceStep * step;

                Vector2 left = Rotate(desiredDir, angle);
                if (!IsDirectionBlocked(left, checkDistance))
                {
                    float score = Vector2.Dot(left, desiredDir);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDir = left;
                        found = true;
                    }
                }

                Vector2 right = Rotate(desiredDir, -angle);
                if (!IsDirectionBlocked(right, checkDistance))
                {
                    float score = Vector2.Dot(right, desiredDir);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestDir = right;
                        found = true;
                    }
                }
            }

            return found ? bestDir.normalized : desiredDir;
        }

        bool IsDirectionBlocked(Vector2 direction, float distance)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            Vector2 origin = transform.position;
            int mask = _obstacleLayerMask;
            if (mask == 0)
                mask = Physics2D.GetLayerCollisionMask(gameObject.layer);

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(mask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hits = Physics2D.CircleCast(origin, _avoidanceRadius, direction, filter, _avoidanceHits, distance);
            for (int i = 0; i < hits; i++)
            {
                var hit = _avoidanceHits[i];
                if (!hit.collider || hit.collider.isTrigger)
                    continue;
                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;
                return true;
            }

            return false;
        }

        void HandleStuck(float desiredSpeed)
        {
            if (desiredSpeed <= 0.01f || _state == State.Waiting || _state == State.Idle)
            {
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                return;
            }

            float travelled = Vector2.Distance(transform.position, _lastProgressPos);
            if (travelled >= Mathf.Max(0.01f, stuckDistance))
            {
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                _consecutiveStuckRebuilds = 0;
                return;
            }

            if (Time.time - _lastProgressTime < stuckTime)
                return;

            if (Time.time - _lastForcedRepathAt < 0.3f)
                return;

            bool advanced = false;
            if ((_state == State.Patrolling || _state == State.Waiting) && HasPatrolPath())
            {
                _consecutiveStuckRebuilds++;
                if (_consecutiveStuckRebuilds >= 2)
                {
                    AdvancePatrolIndex();
                    _waitUntil = -1f;
                    _state = State.Patrolling;
                    _consecutiveStuckRebuilds = 0;
                    advanced = true;
                }
            }
            else if (ShouldFreeRoam() && _state == State.Roaming)
            {
                _consecutiveStuckRebuilds++;
                if (_consecutiveStuckRebuilds >= 2)
                {
                    _hasRoamDestination = false;
                    _waitUntil = -1f;
                    _state = State.Roaming;
                    _consecutiveStuckRebuilds = 0;
                    ClearPath();
                    advanced = true;
                }
            }

            if (!advanced)
                ClearPath();

            _lastForcedRepathAt = Time.time;
            _lastProgressPos = transform.position;
            _lastProgressTime = Time.time;
        }

        bool TryBuildPath(Vector2 destination)
        {
            _pathDestination = destination;
            _pathIndex = 0;

            if (!TilemapPathfinder.HasColliderMap)
            {
                _pathValid = false;
                _pathBuffer.Clear();
                return true;
            }

            bool success = TilemapPathfinder.TryFindPath(transform.position, destination, _pathBuffer);
            _pathValid = success && _pathBuffer.Count > 0;
            if (!_pathValid)
            {
                _pathBuffer.Clear();
            }

            return _pathValid;
        }

        Vector2 GetPathDirection(Vector2 destination, float repathInterval, float destinationTolerance, float waypointRadius)
        {
            Vector2 pos = transform.position;

            if (!TilemapPathfinder.HasColliderMap)
                return DirectionTo(pos, destination);

            bool needRebuild = !_pathValid;
            if (!needRebuild)
            {
                float delta = (_pathDestination - destination).sqrMagnitude;
                if (delta > destinationTolerance * destinationTolerance)
                    needRebuild = true;
                else if (Time.time >= _pathNextRebuildAt)
                    needRebuild = true;
            }

            if (needRebuild)
            {
                if (!TryBuildPath(destination))
                {
                    _pathNextRebuildAt = Time.time + Mathf.Max(0.1f, repathInterval);
                    return DirectionTo(pos, destination);
                }
                _pathNextRebuildAt = Time.time + repathInterval;
            }

            if (_pathBuffer.Count == 0)
                return DirectionTo(pos, destination);

            float waypointRadiusSq = Mathf.Max(0.0001f, waypointRadius * waypointRadius);
            while (_pathIndex < _pathBuffer.Count && (pos - _pathBuffer[_pathIndex]).sqrMagnitude <= waypointRadiusSq)
            {
                _pathIndex++;
            }

            if (_pathIndex >= _pathBuffer.Count)
            {
                _pathValid = false;
                return DirectionTo(pos, destination);
            }

            Vector2 waypoint = _pathBuffer[_pathIndex];
            Vector2 toWaypoint = waypoint - pos;
            if (toWaypoint.sqrMagnitude < 0.0001f)
            {
                _pathIndex++;
                return DirectionTo(pos, destination);
            }

            return toWaypoint.normalized;
        }

        static Vector2 DirectionTo(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return Vector2.zero;
            return delta.normalized;
        }

        static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        float CalculateAvoidanceRadius()
        {
            var primary = GetPrimaryCollider();
            if (!primary)
                return 0.3f;

            switch (primary)
            {
                case CircleCollider2D circle:
                    return circle.radius * Mathf.Max(circle.transform.lossyScale.x, circle.transform.lossyScale.y);
                case CapsuleCollider2D capsule:
                    return Mathf.Max(capsule.size.x, capsule.size.y) * 0.5f * Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.y);
                case BoxCollider2D box:
                    Vector2 half = Vector2.Scale(box.size * 0.5f, new Vector2(Mathf.Abs(box.transform.lossyScale.x), Mathf.Abs(box.transform.lossyScale.y)));
                    return Mathf.Max(half.x, half.y);
                default:
                    return 0.3f;
            }
        }

        void ApplyMovement()
        {
            if (_rb)
            {
                _rb.linearVelocity = _desiredVelocity;
            }
            else
            {
                transform.position += (Vector3)_desiredVelocity * Time.fixedDeltaTime;
            }
        }

        void UpdateAnimator()
        {
            if (!_anim) return;

            Vector2 velocity = _desiredVelocity;
            isMoving = velocity.sqrMagnitude > 0.01f;
            float speed = velocity.magnitude;

            Vector2 facing = _facing.sqrMagnitude > 0.0001f ? _facing.normalized : Vector2.right;
            int dirIdx = DirIndex(facing);

            _anim.SetFloat(H_Direction, dirIdx);
            _anim.SetInteger(H_DirIndex, dirIdx);

            bool allowLocomotionBools = _state != State.Attacking;
            _anim.SetBool(H_IsWalk, allowLocomotionBools && isMoving && speed < (runSpeed * 0.75f));
            _anim.SetBool(H_IsRun, allowLocomotionBools && isMoving && speed >= (runSpeed * 0.75f));
            _anim.SetBool(H_IsRunBackwards, false);
            _anim.SetBool(H_IsStrafeLeft, false);
            _anim.SetBool(H_IsStrafeRight, false);
        }

        int DirIndex(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return 0;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;

            if (ang >= 337.5f || ang < 22.5f) return 0;   // E
            if (ang < 67.5f)  return 4;                   // NE
            if (ang < 112.5f) return 3;                   // N
            if (ang < 157.5f) return 5;                   // NW
            if (ang < 202.5f) return 1;                   // W
            if (ang < 247.5f) return 7;                   // SW
            if (ang < 292.5f) return 2;                   // S
            return 6;                                     // SE
        }

        Vector2 IdleDirectionToVector(IdleFacingDirection dir)
        {
            switch (dir)
            {
                case IdleFacingDirection.NorthEast: return new Vector2(1f, 1f).normalized;
                case IdleFacingDirection.North: return Vector2.up;
                case IdleFacingDirection.NorthWest: return new Vector2(-1f, 1f).normalized;
                case IdleFacingDirection.West: return Vector2.left;
                case IdleFacingDirection.SouthWest: return new Vector2(-1f, -1f).normalized;
                case IdleFacingDirection.South: return Vector2.down;
                case IdleFacingDirection.SouthEast: return new Vector2(1f, -1f).normalized;
                default: return Vector2.right;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            if (HasPatrolPath())
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    var wp = patrolWaypoints[i];
                    if (!wp) continue;
                    Gizmos.DrawSphere(wp.position, 0.1f);

                    var next = patrolWaypoints[(i + 1) % patrolWaypoints.Length];
                    if (next)
                        Gizmos.DrawLine(wp.position, next.position);
                }
            }
        }
    }
}






