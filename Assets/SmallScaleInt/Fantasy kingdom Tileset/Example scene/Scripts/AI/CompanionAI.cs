using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using UnityEngine.Serialization;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    /// <summary>
    /// Lightweight companion behaviour inspired by NeutralNpcAI.
    /// Follows the player, keeps a small buffer, and helps in combat.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public class CompanionAI : MonoBehaviour
    {
        public enum CombatStyle { Melee, Ranged }
        enum State { Following, Chasing, Attacking }

        [Header("Follow Settings")]
        public Transform followTarget;
        [Tooltip("Desired distance from the follow target when idle.")]
        public float followDistance = 1.5f;
        [Tooltip("Run back to the target if farther than this.")]
        public float rejoinDistance = 4.0f;
        [Tooltip("If the companion exceeds this distance from the follow target it instantly teleports next to them (0 = disabled).")]
        public float teleportDistance = 12f;
        [Tooltip("Offset (local space) relative to the follow target used when idle.")]
        public Vector2 idleOffset = new Vector2(0.8f, -0.2f);

        [Header("Navigation")]
        public Rigidbody2D body;
        public float walkSpeed = 2.0f;
        public float runSpeed = 3.4f;
        public float acceleration = 18f;
        public float braking = 24f;
        [Tooltip("Velocity magnitude below this value is snapped to zero so the companion fully stops when idle.")]
        public float idleStopThreshold = 0.05f;
        [Tooltip("Minimum distance to waypoints when following a path."), Range(0.05f, 0.5f)]
        public float waypointTolerance = 0.15f;
        public LayerMask obstacleMask = ~0;
        [Header("Pathfinding")]
        [Tooltip("Enable Tilemap-based pathfinding so the companion can navigate around walls.")]
        public bool enablePathfinding = true;
        [Tooltip("Distance change that forces a path rebuild.")]
        public float pathRefreshDistance = 0.75f;
        [Tooltip("Distance to goal where we consider path complete.")]
        public float pathGoalTolerance = 0.1f;

        [Header("Combat")]
        public CombatStyle combatStyle = CombatStyle.Melee;
        public LayerMask enemyMask;
        public float detectionRadius = 7f;
        public float leashDistance = 12f;
        public float attackRange = 1.1f;
        [FormerlySerializedAs("meleeDamage")]
        [Tooltip("Amount of damage dealt per attack (used for both melee swings and ranged projectiles).")]
        public int damage = 12;
        public float attackCooldown = 0.8f;
        public float attackWindup = 0.1f;
        public string[] attackTriggers = new[] { "Attack1" };
        [Tooltip("Projectile prefab for ranged style (optional).")]
        public Projectile2D projectilePrefab;
        public float projectileSpeed = 8f;
        [Tooltip("Velocity magnitude on the follow target that forces the companion to run to keep up.")]
        public float followTargetRunThreshold = 1.0f;

        [Header("Animator Bools")]
        public string walkBool = "IsWalk";
        public string runBool = "IsRun";

        [Header("Health Bar")]
        public bool useHealthBar = true;
        public Vector2 barSize = new Vector2(1.2f, 0.16f);
        public Vector2 barOffset = new Vector2(0f, 1.15f);
        public Color barBackColor = new Color(0f, 0f, 0f, 0.4f);
        public Color barFillColor = new Color(0.2f, 1f, 0.2f, 0.9f);
        public bool keepBarUpright = true;

        [Header("Scaling")]
        [Tooltip("When enabled the companion inherits a percentage of the player's health and damage.")]
        public bool scaleStatsWithPlayer = false;
        [Tooltip("Percentage (0-2x) of the player's max health applied to this companion.")]
        [Range(0f, 2f)] public float healthPercentOfPlayer = 0.5f;
        [Tooltip("Percentage (0-2x) of the player's damage applied to this companion.")]
        [Range(0f, 2f)] public float damagePercentOfPlayer = 0.5f;

        Animator _anim;
        readonly int H_Direction = Animator.StringToHash("Direction");
        readonly int H_DirIndex = Animator.StringToHash("DirIndex");
        readonly int H_IsWalk = Animator.StringToHash("IsWalk");
        readonly int H_IsRun = Animator.StringToHash("IsRun");
        readonly int H_IsRunBackwards = Animator.StringToHash("IsRunBackwards");
        readonly int H_IsStrafeLeft = Animator.StringToHash("IsStrafeLeft");
        readonly int H_IsStrafeRight = Animator.StringToHash("IsStrafeRight");
        Transform _currentEnemy;
        Transform _playerTransform;
        Vector2 _velocity;
        Vector2 _facing = Vector2.right;
        State _state = State.Following;
        float _nextAttackAt;
        Coroutine _attackRoutine;
        readonly List<Vector2> _path = new List<Vector2>();
        int _pathIndex;
        Vector2 _pathTarget;
        Rigidbody2D _followTargetBody;
        CompanionHealth _health;
        static Sprite _pixel;
        Transform _barRoot;
        SpriteRenderer _barBack, _barFill;
        Transform _defaultFollowTarget;
        Vector2 _holdPosition;
        Vector2 _orderDestination;
        int _baseDamage;
        int _baseMaxHealth;
        PlayerStats _playerStats;
        bool _statsSubscribed;

        enum CompanionOrderMode { Follow, HoldPosition, MoveToPoint, AttackMove }
        CompanionOrderMode _orderMode = CompanionOrderMode.Follow;

        static readonly List<CompanionAI> _activeCompanions = new List<CompanionAI>();
        public static IReadOnlyList<CompanionAI> ActiveCompanions => _activeCompanions;
        public static event Action ActiveCompanionsChanged;

        public bool IsDead => _health && _health.IsDead;

        void Awake()
        {
            _anim = GetComponent<Animator>();
            if (!body) body = GetComponent<Rigidbody2D>();
            if (!followTarget && PlayerHealth.Instance)
                SetFollowTarget(PlayerHealth.Instance.transform);
            else
                CacheFollowTargetBody();

            _health = GetComponent<CompanionHealth>();
            if (!_health)
                _health = gameObject.AddComponent<CompanionHealth>();
            _health.onDied += HandleDeath;
            _health.OnHealthChanged += HandleHealthChanged;
            CreateHealthBar();
            UpdateHealthBar(true);
            _defaultFollowTarget = followTarget;
            _holdPosition = transform.position;
            _baseDamage = Mathf.Max(1, damage);
            _baseMaxHealth = _health != null ? Mathf.Max(1, _health.maxHealth) : 0;
        }

        void OnEnable()
        {
            if (!_activeCompanions.Contains(this))
            {
                _activeCompanions.Add(this);
            }
            ActiveCompanionsChanged?.Invoke();

            if (scaleStatsWithPlayer)
            {
                TryRegisterStatScaling();
            }
            else
            {
                ApplyBaseStats();
            }

            EnemyAI.DamageDealt += HandleDamageDealt;
            CachePlayerTransform();
        }

        void OnDisable()
        {
            _activeCompanions.Remove(this);
            ActiveCompanionsChanged?.Invoke();
            UnregisterPlayerStatListener();
            EnemyAI.DamageDealt -= HandleDamageDealt;
        }

        void Update()
        {
            if (scaleStatsWithPlayer)
            {
                if (!_statsSubscribed)
                {
                    TryRegisterStatScaling();
                }
            }
            else if (_statsSubscribed)
            {
                UnregisterPlayerStatListener();
                ApplyBaseStats();
            }

            if (!followTarget && _orderMode == CompanionOrderMode.Follow)
            {
                var ph = PlayerHealth.Instance;
                if (ph) SetFollowTarget(ph ? ph.transform : null);
            }

            UpdateEnemyTarget();
            UpdateStateMachine();
            UpdateAnimator();
        }

        void LateUpdate()
        {
            if (_barRoot && useHealthBar)
            {
                _barRoot.position = (Vector2)transform.position + barOffset;
                if (keepBarUpright)
                    _barRoot.rotation = Quaternion.identity;
            }
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.onDied -= HandleDeath;
                _health.OnHealthChanged -= HandleHealthChanged;
            }
            UnregisterPlayerStatListener();
            EnemyAI.DamageDealt -= HandleDamageDealt;
        }

        void UpdateEnemyTarget()
        {
            if (_currentEnemy && !IsEnemyValid(_currentEnemy))
            {
                _currentEnemy = null;
            }

            if (_currentEnemy)
            {
                float dist = Vector2.Distance(transform.position, _currentEnemy.position);
                if (dist > leashDistance)
                    _currentEnemy = null;
            }

            if (_currentEnemy) return;

            Collider2D[] buf = new Collider2D[32];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(transform.position, detectionRadius, filter, buf);
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var c = buf[i];
                if (!c) continue;
                Transform t = c.attachedRigidbody ? c.attachedRigidbody.transform : c.transform;
                if (!IsEnemyValid(t)) continue;
                float d = (t.position - transform.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    _currentEnemy = t;
                }
            }
        }

        void UpdateStateMachine()
        {
            if (_attackRoutine != null) return;

            bool allowChase = ShouldChaseEnemy();
            if (_currentEnemy && !IsEnemyValid(_currentEnemy))
            {
                _currentEnemy = null;
            }

            if (_currentEnemy && allowChase)
            {
                _state = State.Chasing;
                Vector2 targetPos = _currentEnemy.position;
                float distToEnemy = Vector2.Distance(transform.position, targetPos);

                bool shouldHoldPosition = combatStyle == CombatStyle.Ranged && distToEnemy <= Mathf.Max(0.05f, attackRange);
                if (shouldHoldPosition)
                {
                    Halt();
                }
                else
                {
                    MoveTowards(targetPos, runSpeed);
                }

                TryAttack();
                return;
            }

            if (_orderMode == CompanionOrderMode.HoldPosition)
            {
                _state = State.Following;
                ExecuteHoldPosition();
                return;
            }

            if (_orderMode == CompanionOrderMode.MoveToPoint || _orderMode == CompanionOrderMode.AttackMove)
            {
                _state = State.Chasing;
                ExecuteMoveOrder();
                return;
            }

            _state = State.Following;
            if (!followTarget)
            {
                Halt();
                return;
            }

            Vector2 desired = (Vector2)followTarget.position + idleOffset;
            float dist = Vector2.Distance(transform.position, desired);
            if (teleportDistance > 0f && dist >= teleportDistance)
            {
                TeleportToAnchor(desired);
                return;
            }
            float speed;
            if (dist > rejoinDistance)
            {
                speed = runSpeed;
            }
            else if (dist > followDistance + 0.2f)
            {
                float t = Mathf.InverseLerp(followDistance + 0.2f, rejoinDistance, dist);
                speed = Mathf.Lerp(walkSpeed, runSpeed, t);
            }
            else
            {
                speed = walkSpeed;
            }

            Vector2 targetVel = GetFollowTargetVelocity();
            if (targetVel.sqrMagnitude >= followTargetRunThreshold * followTargetRunThreshold)
            {
                speed = runSpeed;
            }
            if (dist <= followDistance * 0.8f)
            {
                Halt();
                return;
            }

            MoveTowards(desired, speed);
        }

        void MoveTowards(Vector2 destination, float speed)
        {
            if (enablePathfinding && TryFollowPath(destination, speed))
                return;

            ClearPath();
            Vector2 toDest = destination - (Vector2)transform.position;
            if (toDest.sqrMagnitude <= waypointTolerance * waypointTolerance)
            {
                Halt();
                return;
            }

            DirectMove(toDest, speed);
        }

        void Halt()
        {
            float dt = Time.deltaTime;
            _velocity = Vector2.MoveTowards(_velocity, Vector2.zero, braking * dt);
            ApplyVelocity(dt);
        }

        void TryAttack()
        {
            if (!_currentEnemy) return;
            if (Time.time < _nextAttackAt) return;

            float dist = Vector2.Distance(transform.position, _currentEnemy.position);
            if (dist > attackRange + 0.1f) return;

            _attackRoutine = StartCoroutine(AttackRoutine());
        }

        IEnumerator AttackRoutine()
        {
            _state = State.Attacking;
            _velocity = Vector2.zero;
            ApplyVelocity(0f);
            _nextAttackAt = Time.time + attackCooldown;

            if (_anim && attackTriggers != null && attackTriggers.Length > 0)
            {
                string trig = attackTriggers[UnityEngine.Random.Range(0, attackTriggers.Length)];
                if (!string.IsNullOrEmpty(trig)) _anim.SetTrigger(trig);
            }

            if (attackWindup > 0f)
                yield return new WaitForSeconds(attackWindup);

            if (combatStyle == CombatStyle.Melee)
                PerformMeleeHit();
            else
                PerformRangedShot();

            _attackRoutine = null;
        }

        void PerformMeleeHit()
        {
            if (!_currentEnemy) return;
            Vector2 dir = (_currentEnemy.position - transform.position);
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            Vector2 center = (Vector2)transform.position + dir * attackRange * 0.6f;
            _facing = dir;

            Collider2D[] buf = new Collider2D[16];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(center, 0.35f, filter, buf);
            for (int i = 0; i < count; i++)
            {
                var c = buf[i];
                if (!c) continue;
                var dmg = c.GetComponentInParent<EnemyAI.IDamageable>();
                if (dmg != null)
                {
                    dmg.TakeDamage(damage, dir);
                    EnemyAI.NotifyDamageDealt(dmg, transform, damage);
                    break;
                }
            }
        }

        void PerformRangedShot()
        {
            if (!projectilePrefab || !_currentEnemy) return;
            Vector2 dir = (_currentEnemy.position - transform.position).normalized;
            _facing = dir;
            var proj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            proj.Init(dir * projectileSpeed, Mathf.Max(1, damage), attackCooldown * 2f, enemyMask, transform);
        }

        void UpdateAnimator()
        {
            if (!_anim) return;

            bool moving = _velocity.sqrMagnitude > 0.01f;
            float speed = _velocity.magnitude;

            Vector2 facing = _facing.sqrMagnitude > 0.0001f ? _facing.normalized : Vector2.right;
            int dirIdx = DirIndex(facing);
            _anim.SetFloat(H_Direction, dirIdx);
            _anim.SetInteger(H_DirIndex, dirIdx);

            bool allowLocomotion = _state != State.Attacking;
            bool isRun = allowLocomotion && moving && speed >= runSpeed * 0.75f;
            bool isWalk = allowLocomotion && moving && !isRun;

            _anim.SetBool(H_IsWalk, isWalk);
            _anim.SetBool(H_IsRun, isRun);
            _anim.SetBool(H_IsRunBackwards, false);
            _anim.SetBool(H_IsStrafeLeft, false);
            _anim.SetBool(H_IsStrafeRight, false);

            if (!string.IsNullOrEmpty(walkBool)) _anim.SetBool(walkBool, isWalk);
            if (!string.IsNullOrEmpty(runBool)) _anim.SetBool(runBool, isRun);
        }

        int DirIndex(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return 0;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;

            if (ang >= 337.5f || ang < 22.5f) return 0;
            if (ang < 67.5f) return 4;
            if (ang < 112.5f) return 3;
            if (ang < 157.5f) return 5;
            if (ang < 202.5f) return 1;
            if (ang < 247.5f) return 7;
            if (ang < 292.5f) return 2;
            return 6;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, followDistance);
        }

        bool IsEnemyValid(Transform candidate)
        {
            if (!candidate) return false;
            var health = candidate.GetComponentInParent<EnemyHealth2D>();
            if (health != null && health.IsDead) return false;
            return true;
        }

        void SetFollowTarget(Transform target)
        {
            followTarget = target;
            if (target != null && _defaultFollowTarget == null)
            {
                _defaultFollowTarget = target;
            }
            CacheFollowTargetBody();
        }

        void CachePlayerTransform()
        {
            if (_playerTransform && _playerTransform.gameObject)
            {
                return;
            }

            var player = PlayerHealth.Instance;
            if (player && player.gameObject)
            {
                _playerTransform = player.transform;
            }
        }

        void HandleDamageDealt(Transform attacker, EnemyAI.IDamageable target, float damage)
        {
            _ = damage;
            if (!isActiveAndEnabled || attacker == null || target == null)
            {
                return;
            }

            if (!ShouldChaseEnemy())
            {
                return;
            }

            if (!IsPlayerAttacker(attacker))
            {
                return;
            }

            Transform enemy = ResolveEnemyFromDamageable(target);
            if (!enemy || !IsEnemyValid(enemy))
            {
                return;
            }

            if (_currentEnemy == enemy)
            {
                return;
            }

            _currentEnemy = enemy;
        }

        bool IsPlayerAttacker(Transform attacker)
        {
            CachePlayerTransform();
            if (!_playerTransform)
            {
                return false;
            }

            if (attacker == _playerTransform)
            {
                return true;
            }

            return attacker.IsChildOf(_playerTransform);
        }

        Transform ResolveEnemyFromDamageable(EnemyAI.IDamageable target)
        {
            if (target is Component component)
            {
                var health = component.GetComponentInParent<EnemyHealth2D>();
                if (health)
                {
                    return health.transform;
                }

                var ai = component.GetComponentInParent<EnemyAI>();
                if (ai)
                {
                    return ai.transform;
                }

                return component.transform;
            }

            return null;
        }

        void CacheFollowTargetBody()
        {
            _followTargetBody = followTarget ? followTarget.GetComponent<Rigidbody2D>() : null;
        }

        Vector2 GetFollowTargetVelocity()
        {
            if (!_followTargetBody) return Vector2.zero;
#if UNITY_2022_2_OR_NEWER
            return _followTargetBody.linearVelocity;
#else
            return _followTargetBody.velocity;
#endif
        }

        bool TryFollowPath(Vector2 destination, float speed)
        {
            if (!TilemapPathfinder.HasColliderMap)
                TilemapPathfinder.EnsureInitialized();
            if (!TilemapPathfinder.HasColliderMap)
                return false;

            bool needBuild = _path.Count == 0 ||
                             Vector2.Distance(_pathTarget, destination) > pathRefreshDistance;

            if (needBuild && !BuildPath(destination))
                return false;

            if (_pathIndex >= _path.Count)
            {
                ClearPath();
                return false;
            }

            Vector2 waypoint = _path[_pathIndex];
            float dist = Vector2.Distance(transform.position, waypoint);
            if (dist <= Mathf.Max(0.05f, waypointTolerance))
            {
                if (_pathIndex < _path.Count - 1)
                {
                    _pathIndex++;
                    waypoint = _path[_pathIndex];
                }
                else
                {
                    ClearPath();
                    return false;
                }
            }

            Vector2 toWaypoint = waypoint - (Vector2)transform.position;
            DirectMove(toWaypoint, speed);
            return true;
        }

        bool BuildPath(Vector2 destination)
        {
            _path.Clear();
            _pathIndex = 0;
            _pathTarget = destination;

            if (!TilemapPathfinder.TryFindPath(transform.position, destination, _path))
            {
                ClearPath();
                return false;
            }

            if (_path.Count == 0 || Vector2.Distance(_path[_path.Count - 1], destination) > pathGoalTolerance)
            {
                _path.Add(destination);
            }
            return true;
        }

        void ClearPath()
        {
            _path.Clear();
            _pathIndex = 0;
        }

        void DirectMove(Vector2 toDest, float speed)
        {
            float dt = Time.deltaTime;
            Vector2 desiredVel = toDest.normalized * speed + ComputeSeparationForce();
            float maxDelta = (desiredVel.sqrMagnitude > 0.001f ? acceleration : braking) * dt;
            _velocity = Vector2.MoveTowards(_velocity, desiredVel, maxDelta);

            if (_velocity.sqrMagnitude > 0.001f)
                _facing = _velocity.normalized;

            ApplyVelocity(dt);
        }

        void ApplyVelocity(float deltaTime)
        {
            float stopThreshold = Mathf.Max(0f, idleStopThreshold);
            float stopThresholdSq = stopThreshold > 0f ? stopThreshold * stopThreshold : 0f;
            bool nearZero = stopThresholdSq > 0f
                ? _velocity.sqrMagnitude <= stopThresholdSq
                : _velocity.sqrMagnitude <= 0.0001f;

            if (nearZero)
            {
                _velocity = Vector2.zero;
                if (body)
                {
#if UNITY_2022_2_OR_NEWER
                    body.linearVelocity = Vector2.zero;
#else
                    body.velocity = Vector2.zero;
#endif
                }
            }
            else if (body)
            {
#if UNITY_2022_2_OR_NEWER
                body.linearVelocity = _velocity;
#else
                body.velocity = _velocity;
#endif
            }

            if (!body && !nearZero && deltaTime > 0f)
            {
                transform.position += (Vector3)(_velocity * deltaTime);
            }
        }

        void HandleDeath()
        {
            enabled = false;
            ClearPath();
            if (body)
            {
#if UNITY_2022_2_OR_NEWER
                body.linearVelocity = Vector2.zero;
#else
                body.velocity = Vector2.zero;
#endif
                body.simulated = false;
            }
            DestroyHealthBar();
        }

        Vector2 ComputeSeparationForce()
        {
            const float separationRadius = 0.9f;
            int layerMask = 1 << gameObject.layer;
            Collider2D[] buf = new Collider2D[16];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(layerMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(transform.position, separationRadius, filter, buf);
            Vector2 repel = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var c = buf[i];
                if (!c || c.transform == transform) continue;
                Vector2 toSelf = (Vector2)transform.position - (Vector2)c.transform.position;
                float dist = toSelf.magnitude;
                if (dist < 0.001f) continue;
                float strength = Mathf.Clamp01((separationRadius - dist) / separationRadius);
                repel += toSelf.normalized * strength;
            }
            return repel;
        }

        void TeleportToAnchor(Vector2 destination)
        {
            ClearPath();
            _velocity = Vector2.zero;
            Vector2 finalPos = destination;
            if (teleportDistance > 0f)
            {
                const float spreadRadius = 0.4f;
                Collider2D[] buf = new Collider2D[16];
                ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
                filter.SetLayerMask(1 << gameObject.layer);
                filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.OverlapCircle(destination, spreadRadius, filter, buf);
                for (int i = 0; i < count; i++)
                {
                    var c = buf[i];
                    if (!c || c.transform == transform) continue;
                    Vector2 offset = (Vector2)destination - (Vector2)c.transform.position;
                    if (offset.sqrMagnitude < 0.0001f) offset = UnityEngine.Random.insideUnitCircle * 0.2f;
                    finalPos += offset.normalized * 0.3f;
                }
            }

            transform.position = finalPos;
            if (body)
            {
#if UNITY_2022_2_OR_NEWER
                body.linearVelocity = Vector2.zero;
                body.position = finalPos;
#else
                body.velocity = Vector2.zero;
                body.position = finalPos;
#endif
            }
        }

        void HandleHealthChanged(int current, int max)
        {
            UpdateHealthBar(true);
            bool show = current < max;
            SetBarVisible(show);
        }

        void CreateHealthBar()
        {
            if (!useHealthBar || _barRoot) return;
            if (!_pixel)
            {
                var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false)
                {
                    name = "CompanionHealthPixel",
                    filterMode = FilterMode.Point
                };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            _barRoot = new GameObject("CompanionHealthBar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = barOffset;

            var backObj = new GameObject("Back");
            backObj.transform.SetParent(_barRoot, false);
            _barBack = backObj.AddComponent<SpriteRenderer>();
            _barBack.sprite = _pixel;
            _barBack.color = barBackColor;
            _barBack.sortingOrder = 100;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_barRoot, false);
            _barFill = fillObj.AddComponent<SpriteRenderer>();
            _barFill.sprite = _pixel;
            _barFill.color = barFillColor;
            _barFill.sortingOrder = 101;

            LayoutBar(1f);
            SetBarVisible(false);
        }

        void LayoutBar(float pct)
        {
            if (_barBack)
            {
                _barBack.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
                _barBack.transform.localPosition = Vector3.zero;
            }

            if (_barFill)
            {
                _barFill.transform.localScale = new Vector3(barSize.x * Mathf.Clamp01(pct), barSize.y, 1f);
                _barFill.transform.localPosition = new Vector3(-barSize.x * 0.5f * (1f - Mathf.Clamp01(pct)), 0f, 0f);
            }
        }

        void UpdateHealthBar(bool force = false)
        {
            if (!useHealthBar || !_barRoot || !_health) return;
            float pct = Mathf.Clamp01(_health.currentHealth / (float)Mathf.Max(1, _health.maxHealth));
            LayoutBar(pct);
            if (_health.currentHealth >= _health.maxHealth)
                SetBarVisible(false);
        }

        void SetBarVisible(bool visible)
        {
            if (_barBack) _barBack.enabled = visible;
            if (_barFill) _barFill.enabled = visible;
        }

        void DestroyHealthBar()
        {
            if (_barRoot)
            {
                Destroy(_barRoot.gameObject);
            }
            _barRoot = null;
            _barBack = null;
            _barFill = null;
        }

        bool ShouldChaseEnemy()
        {
            return _orderMode != CompanionOrderMode.MoveToPoint && _orderMode != CompanionOrderMode.HoldPosition;
        }

        void ExecuteHoldPosition()
        {
            float dist = Vector2.Distance(transform.position, _holdPosition);
            if (dist > Mathf.Max(0.15f, followDistance * 0.3f))
            {
                MoveTowards(_holdPosition, walkSpeed);
            }
            else
            {
                Halt();
            }

            TryAttack();
        }

        void ExecuteMoveOrder()
        {
            float arrival = Mathf.Max(0.2f, followDistance * 0.4f);
            float dist = Vector2.Distance(transform.position, _orderDestination);
            if (dist <= arrival)
            {
                if (_orderMode == CompanionOrderMode.AttackMove)
                {
                    IssueFollowCommand();
                }
                else
                {
                    _holdPosition = _orderDestination;
                    _orderMode = CompanionOrderMode.HoldPosition;
                }
                Halt();
                TryAttack();
                return;
            }

            MoveTowards(_orderDestination, runSpeed);
            TryAttack();
        }

        void TryRegisterStatScaling()
        {
            if (!scaleStatsWithPlayer || _statsSubscribed)
            {
                return;
            }

            _playerStats = PlayerStats.Instance;
            if (_playerStats == null)
            {
                return;
            }

            _playerStats.StatsChanged += HandlePlayerStatsChanged;
            _statsSubscribed = true;
            HandlePlayerStatsChanged(_playerStats.CurrentStats);
        }

        void UnregisterPlayerStatListener()
        {
            if (!_statsSubscribed)
            {
                return;
            }

            if (_playerStats != null)
            {
                _playerStats.StatsChanged -= HandlePlayerStatsChanged;
            }

            _playerStats = null;
            _statsSubscribed = false;
        }

        void HandlePlayerStatsChanged(PlayerStats.StatSnapshot snapshot)
        {
            if (!scaleStatsWithPlayer)
            {
                return;
            }

            int playerMaxHealth = PlayerHealth.Instance != null ? PlayerHealth.Instance.maxHealth : Mathf.Max(1, snapshot.Health);
            if (_health != null && playerMaxHealth > 0)
            {
                int scaledHealth = Mathf.Max(1, Mathf.RoundToInt(playerMaxHealth * Mathf.Clamp(healthPercentOfPlayer, 0f, 2f)));
                if (_baseMaxHealth > 0)
                {
                    scaledHealth = Mathf.Max(_baseMaxHealth, scaledHealth);
                }
                _health.ApplyScaledMaxHealth(scaledHealth);
            }

            int playerDamageStat = Mathf.Max(0, snapshot.Strength + snapshot.WeaponDamage);
            int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(playerDamageStat * Mathf.Clamp(damagePercentOfPlayer, 0f, 2f)));
            damage = scaledDamage;
        }

        void ApplyBaseStats()
        {
            damage = Mathf.Max(1, _baseDamage);
            if (_health != null && _baseMaxHealth > 0)
            {
                _health.ApplyScaledMaxHealth(_baseMaxHealth);
            }
        }

        public void IssueFollowCommand()
        {
            _orderMode = CompanionOrderMode.Follow;
            if (_defaultFollowTarget == null && PlayerHealth.Instance != null)
            {
                _defaultFollowTarget = PlayerHealth.Instance.transform;
            }
            followTarget = _defaultFollowTarget;
            CacheFollowTargetBody();
            ClearPath();
        }

        public void IssueHoldCommand(Vector2 position)
        {
            _orderMode = CompanionOrderMode.HoldPosition;
            _holdPosition = position;
            followTarget = null;
            CacheFollowTargetBody();
            ClearPath();
        }

        public void IssueMoveCommand(Vector2 destination, bool attackMove)
        {
            _orderMode = attackMove ? CompanionOrderMode.AttackMove : CompanionOrderMode.MoveToPoint;
            _orderDestination = destination;
            followTarget = null;
            CacheFollowTargetBody();
            ClearPath();
        }

        public static bool HasLivingCompanion()
        {
            for (int i = 0; i < _activeCompanions.Count; i++)
            {
                var companion = _activeCompanions[i];
                if (companion != null && companion.isActiveAndEnabled && !companion.IsDead)
                {
                    return true;
                }
            }

            return false;
        }
    }
}







