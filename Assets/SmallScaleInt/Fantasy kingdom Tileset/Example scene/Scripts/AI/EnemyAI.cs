using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityRandom = UnityEngine.Random;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SmallScale.FantasyKingdomTileset;
using SmallScale.FantasyKingdomTileset.Balance;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    /// <summary>
    /// Lightweight, obstacle-agnostic enemy AI supporting Melee and Ranged.
    /// - Distance-only engage/leash
    /// - Adds Home/Spawn + Roam (idle wander) + ReturnHome
    /// - Melee: shaping (approach/orbit/noise) + ticketed close-range attack
    /// - Ranged: keeps distance band, stops to shoot, randomized cooldown
    /// - Separation to avoid stacking
    /// - Drives same Animator booleans/triggers as your player
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class EnemyAI : MonoBehaviour
    {
        // Keep it nested here
        public interface IDamageable
        {
            void TakeDamage(int amount, Vector2 hitDir);
        }

        public static event Action<Transform, IDamageable, float> DamageDealt;

        public enum AIType { Melee, Ranged }
        enum Mode { Roam, Engage, ReturnHome }

        struct PatrolPoint
        {
            public Transform Transform;
            public Vector2 OffsetFromAnchor;
        }

        public enum DefaultBehaviour
        {
            HoldPosition,
            Patrol,
            FreeRoam,
            LocalRoam
        }

        public enum PatrolMode
        {
            Loop,
            PingPong,
            Random
        }

        #region Inspector

        [Header("Behaviour")]
        [Tooltip("Default behaviour when the enemy is not actively engaging a target.")]
        public DefaultBehaviour startingBehaviour = DefaultBehaviour.FreeRoam;
        [Tooltip("Optional offset applied to the home position when holding.")]
        public Vector2 holdOffset = Vector2.zero;
        [Tooltip("Distance considered close enough when holding position.")]
        public float holdPositionTolerance = 0.15f;

        [Header("Patrol")]
        [Tooltip("Optional world-space patrol points used when the behaviour is Patrol.")]
        public Transform[] patrolWaypoints;
        [Tooltip("Patrol traversal logic when more than one waypoint is present.")]
        public PatrolMode patrolMode = PatrolMode.Loop;
        [Tooltip("Time range spent idling between patrol steps.")]
        public Vector2 patrolPauseDuration = new Vector2(0.8f, 1.6f);
        [Tooltip("Distance considered close enough to a patrol waypoint.")]
        public float patrolWaypointTolerance = 0.2f;

        [Header("General")]
        [Tooltip("Select whether this enemy behaves as a melee chaser or a ranged fighter.")]
        public AIType type = AIType.Melee;
        [Tooltip("Transform the AI considers the player/primary target.")]
        public Transform player;

        [Header("Loot")]
        [Tooltip("If enabled, this enemy can drop random gear when defeated.")]
        public bool enableRandomGearDrops = false;

        [Header("Home / Spawn (NEW)")]
        [Tooltip("If set, this is the home point. If null, initial position is used.")]
        public Transform spawnPoint;
        [Tooltip("When true the enemy will return to its home position after disengaging.")]
        public bool returnToPost = true;
        [Tooltip("Distance range from the home position used when picking roam destinations.")]
        public Vector2 roamDistance = new Vector2(6f, 12f);
        [Tooltip("Distance considered 'at home' when returning.")]
        public float homeSnapDistance = 0.25f;

        [Header("Roam Behaviour (NEW)")]
        [Tooltip("Idle roaming speed.")]
        public float roamSpeed = 1.2f;
        [Tooltip("Acceleration while roaming.")]
        public float roamAccel = 10f;
        [Tooltip("Time range spent idling between free roam hops.")]
        public Vector2 roamPause = new Vector2(1.5f, 3.5f);
        [Tooltip("Distance considered close enough to a roam destination.")]
        public float roamDestinationTolerance = 0.5f;

        [Header("Local Roam Behaviour (NEW)")]
        [Tooltip("Radius around the spawn/home used by the LocalRoam behaviour.")]
        public float localRoamRadius = 1.5f;
        [Tooltip("Time range the enemy will keep moving while using LocalRoam.")]
        public Vector2 localRoamMoveDuration = new Vector2(1.0f, 2.2f);
        [Tooltip("Time range spent idling while using LocalRoam.")]
        public Vector2 localRoamPauseDuration = new Vector2(0.5f, 1.4f);
        [Tooltip("Arrival distance threshold used by LocalRoam.")]
        public float localRoamArriveThreshold = 0.2f;

        Vector2 _lastMoveDir = Vector2.right;      // remember last non-zero move direction for idle facing
        [Header("Roam/Return Polishing")]
        [Tooltip("Distance from the roam target/home where the AI starts easing off the throttle.")]
        public float arriveBrakeRadius = 0.8f;     // start slowing down when this close to roam target/home


        [Header("Threat Memory")]
        [Tooltip("Seconds the enemy remembers a hostile after losing sight of them.")]
        public float threatMemoryDuration = 12f;
        [Tooltip("Maximum distance a remembered hostile can be before being forgotten.")]
        public float threatForgetDistance = 20f;


        [Header("Layers")]
        [Tooltip("LayerMask used when checking hits against the player (melee/projectile).")]
        public LayerMask playerMask;   // for melee hit / projectile hit
        [Tooltip("LayerMask used to detect neutral NPCs that enemies can engage.")]
        public LayerMask neutralNpcMask;
        [Tooltip("Radius around the enemy used to look for neutral NPC targets.")]
        public float neutralNpcAggroRadius = 8f;
        [Tooltip("LayerMask representing other enemies for separation/avoidance.")]
        public LayerMask enemyMask;    // for separation
        [Tooltip("Layers treated as solid when sampling steering context.")]
        public LayerMask obstacleMask;

        [Header("Tile Breaking")]
        [Tooltip("When no path exists, enemies wait and recheck after this many seconds.")]
        public float pathRecheckDelayWhenBlocked = 2f;

        [Header("Threat System")]
        [Tooltip("Base threat assigned to the player whenever spotted.")]
        public float playerBaseThreat = 8f;
        [Tooltip("Base threat assigned to companions / neutral allies.")]
        public float neutralBaseThreat = 6f;
        [Tooltip("Threat added per point of damage dealt to this enemy.")]
        public float damageThreatMultiplier = 0.25f;
        [Tooltip("Passive threat added whenever a hostile refreshes visibility.")]
        public float passiveThreatIncrement = 1.5f;
        [Tooltip("Minimum threat retained for a tracked target.")]
        public float minThreat = 0.5f;
        [Tooltip("Threat decay per second when a hostile is not interacting.")]
        public float threatDecayPerSecond = 1.5f;
        [Tooltip("How strongly distance influences threat evaluation.")]
        public float proximityThreatWeight = 4f;

        [Header("Engage / Leash")]
        [Tooltip("Distance from home/player at which the AI will swap from roaming to engaging.")]
        public float engageRadius = 8f;
        [Tooltip("Maximum chase distance before the AI gives up and heads back home.")]
        public float leashRadius  = 14f;

        [Header("Health")]
        [Tooltip("Seconds of stun applied to this AI after taking a hit.")]
        public float hitStun   = 0.1f;
        float _stunUntil = -1f;

        [Header("Boss")]
        [Tooltip("When enabled, this enemy is treated as a boss and uses the boss-specific UI.")]
        public bool isBoss = false;
        [Tooltip("Display name pushed to the boss health bar UI when this enemy is marked as a boss.")]
        public string EnemyName = "Boss";
        [Tooltip("Prefab containing the boss health bar UI elements.")]
        public BossHealthBarUI bossHealthBarPrefab;
        [Tooltip("Panel transform that will host the spawned boss health bar UI.")]
        public RectTransform bossHealthBarPanel;

        [Header("Kinematics")]
        [Tooltip("Maximum horizontal speed used when roaming or idling.")]
        public float walkSpeed = 1.8f;
        [Tooltip("Maximum horizontal speed while actively engaging the target.")]
        public float runSpeed  = 3.2f;
        [Tooltip("Acceleration force applied when steering.")]
        public float accel     = 18f;
        [Tooltip("Optional drag applied every frame to smooth out movement.")]
        public float drag      = 0.0f; // optional soft damping

        [Header("Separation")]
        [Tooltip("Radius around the enemy used to keep distance from allies.")]
        public float separationRadius   = 1.1f;
        [Tooltip("Force multiplier pushing this enemy away from nearby allies.")]
        public float separationStrength = 2.8f;

        [Header("Stuck Recovery")]
        [Tooltip("Seconds of near-zero movement before forcing a movement nudge.")]
        public float stuckKickDelay = 1.25f;
        [Tooltip("Cooldown between forced movement nudges.")]
        public float stuckKickCooldown = 1.5f;
        [Tooltip("Minimum edge distance (melee) or range error (ranged) before a nudge is allowed.")]
        public float stuckKickDistance = 1.1f;
        [Tooltip("Extra slack for ranged enemies before applying a stuck nudge.")]
        public float stuckKickRangeBuffer = 0.4f;
        [Tooltip("Multiplier applied to the run speed when forcing a stuck nudge.")]
        public float stuckKickSpeedMultiplier = 1.1f;

        [Header("Context Steering (NEW)")]
        [Range(8, 32)] public int contextDirectionCount = 16;
        [Tooltip("Distance used when probing for immediate obstacles.")]
        public float contextProbeDistance = 1.6f;
        [Tooltip("Radius of the probe used when checking for obstacles.")]
        public float contextProbeRadius = 0.3f;
        [Tooltip("How strongly should the AI favour the navigation/path direction.")]
        public float contextSeekWeight = 1.1f;
        [Tooltip("How strongly should the AI try to maintain its preferred distance.")]
        public float contextRangeWeight = 1.0f;
        [Tooltip("Strength of the wandering/noise force injected into the steering context.")]
        public float contextWanderWeight = 0.35f;
        [Tooltip("How strongly should detected obstacles be avoided.")]
        public float contextObstacleWeight = 1.0f;
        [Tooltip("Scaler applied to the separation vector before feeding the context solver.")]
        public float contextSeparationWeight = 1.0f;

        [Header("Pathfinding (NEW)")]
        [Tooltip("Seconds without path progress before forcing a rebuild.")]
        public float pathRepathTime = 0.8f;
        [Tooltip("Distance considered meaningful progress when following a path.")]
        public float pathProgressDistance = 0.3f;

        // ---------- MELEE ----------
        [Header("Melee Distances")]
        [Tooltip("Desired orbit radius around the player.")]
        public float preferredRadius = 2.2f;
        [Tooltip("Edge distance required to trigger an attack (accounting for colliders).")]
        public float attackEnterEdge = 0.75f;
        [Tooltip("During the hit window, we must remain within this edge distance to count the hit.")]
        public float attackHitEdge   = 1.00f;

        [Header("Melee Shaping Curves")]
        public AnimationCurve approachCurve = new AnimationCurve(
            new Keyframe(0.0f, 1.10f),
            new Keyframe(1.0f, 0.70f),
            new Keyframe(2.0f, 0.35f),
            new Keyframe(3.0f, 0.10f)
        );
        public AnimationCurve orbitCurve = new AnimationCurve(
            new Keyframe(0.0f, 1.40f),
            new Keyframe(0.6f, 1.20f),
            new Keyframe(1.5f, 0.60f),
            new Keyframe(3.0f, 0.20f)
        );
        public AnimationCurve retreatCurve = new AnimationCurve(
            new Keyframe(0.0f, 1.00f),
            new Keyframe(0.4f, 0.55f),
            new Keyframe(1.0f, 0.10f)
        );
        public AnimationCurve attackCurve = new AnimationCurve(
            new Keyframe(0.00f, 1.00f),
            new Keyframe(0.20f, 0.95f),
            new Keyframe(0.60f, 0.35f),
            new Keyframe(1.00f, 0.00f)
        );

        [Header("Melee Chase Override")]
        [Tooltip("If the melee enemy is this far (edge distance) or more from the player it will temporarily focus on chasing.")]
        public float chaseOverrideEnterEdge = 3.5f;
        [Tooltip("When the melee enemy gets this close (edge distance) the chase override is released and regular logic resumes.")]
        public float chaseOverrideExitEdge = 1.2f;
        [Tooltip("Additional weight applied to the forward steering while the chase override is active.")]
        public float chaseOverrideSeekMultiplier = 3.5f;

        [Header("Melee Noise / Variety")]
        [Tooltip("Strength of the wandering sway applied while orbiting the player.")]
        public float noiseAmplitude = 0.7f;
        [Tooltip("Frequency of the wander sway applied while orbiting the player.")]
        public float noiseFreq      = 0.9f;
        [Tooltip("Allow melee enemies to flip their orbit side when stuck.")]
        public bool  allowSideFlip  = true;
        [Tooltip("Chance per frame to trigger a side flip when allowed.")]
        public float sideFlipChance = 0.003f;

        [Header("Melee Attack Gating")]
        [Tooltip("How many melee enemies can swing at the player at the same time.")]
        public int maxSimultaneousAttackers = 2;
        [Tooltip("Ticket value required to start an attack (higher = more selective).")]
        [Range(0.1f, 1f)] public float attackThreshold = 0.55f;
        [Tooltip("Maximum time the enemy is allowed to spend lunging before bailing out.")]
        public float lungeMaxTime = 1.2f;
        [Tooltip("Random variance added to melee attack cooldown between swings.")]
        public Vector2 attackCooldownJitter = new Vector2(0.1f, 0.5f);

        [Header("Melee Damage")]
        [Tooltip("Base damage dealt when a melee swing connects.")]
        public int meleeDamage = 10;

        [Tooltip("Multiplier applied to meleeDamage. Abilities can modify this at runtime.")]
        public float damageMultiplier = 1f;

        /// <summary>Current melee damage after accounting for multipliers.</summary>
        public int CurrentMeleeDamage => ApplyGlobalDamageMultiplier(
            Mathf.RoundToInt(Mathf.Max(1, meleeDamage) * Mathf.Max(0.01f, damageMultiplier)));

        public int CurrentProjectileDamage => ApplyGlobalDamageMultiplier(
            Mathf.RoundToInt(Mathf.Max(1, projectileDamage) * Mathf.Max(0.01f, damageMultiplier)));

        private int ApplyGlobalDamageMultiplier(int baseAmount)
        {
            float multiplier = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.EnemyDamageMultiplier : 1f;
            multiplier = Mathf.Max(0f, multiplier);
            int adjusted = Mathf.RoundToInt(baseAmount * multiplier);
            if (multiplier > 0f && adjusted <= 0 && baseAmount > 0)
            {
                adjusted = 1;
            }

            return Mathf.Max(0, adjusted);
        }

        /// <summary>Returns true when external systems have paused this AI.</summary>
        public bool IsExternallyPaused => _externallyPaused;

        /// <summary>Set by movement logic - true when the enemy is moving.</summary>
        [HideInInspector] public bool isMoving = false;

        [Header("Melee Attack Anim + Hit Check")]
        [Tooltip("Animator trigger names fired when a melee attack starts.")]
        public string[] meleeAttackTriggers = new[] { "Attack1", "Attack2", "Attack3" };
        [Tooltip("Offset from the enemy pivot for melee hit detection.")]
        public Vector2  attackHitOffset = new Vector2(0.5f, 0f);
        [Tooltip("Radius of the melee hit detection circle.")]
        public float    attackHitRadius = 0.6f;
        [Tooltip("Delay from trigger to when the hit check becomes active.")]
        public float    attackHitDelay  = 0.22f;
        [Tooltip("Duration that the melee hit check remains active.")]
        public float    attackHitWindow = 0.16f;

        // ---------- RANGED ----------
        [Header("Ranged â€” Distance Band")]
        [Tooltip("Preferred minimum distance to the player.")]
        public float rangedMinRange = 4.5f;
        [Tooltip("Preferred maximum distance to the player.")]
        public float rangedMaxRange = 7.0f;
        [Tooltip("Hysteresis so they don't jitter on the boundary.")]
        public float rangedBandHysteresis = 0.5f;

        [Header("Ranged â€” Firing")]
        [Tooltip("While firing, stand still.")]
        public bool stopToFire = true;
        [Tooltip("Cooldown between shots (seconds).")]
        public Vector2 shotCooldownRange = new Vector2(3f, 5f);
        [Tooltip("Delay from trigger to projectile spawn (wind-up).")]
        public float shotWindup = 0.15f;
        [Tooltip("Animator trigger(s) for ranged shot.")]
        public string[] rangedShootTriggers = new[] { "Attack3" };

        [Header("Ranged â€” Projectile")]
        [Tooltip("Projectile prefab spawned when this enemy fires.")]
        public Projectile2D projectilePrefab;
        [Tooltip("Optional override for the projectile spawn transform.")]
        public Transform   projectileSpawn;  // if null, uses this transform
        [Tooltip("Launch speed applied to the projectile.")]
        public float       projectileSpeed = 9f;
        [Tooltip("Damage dealt by each projectile hit.")]
        public int         projectileDamage = 10;
        [Tooltip("Random angle variation added to each shot.")]
        public float       projectileSpreadDegrees = 4f;
        [Tooltip("Seconds before fired projectiles self-destruct.")]
        public float       projectileLife = 3.5f;

        [Header("Blocked Path Projectiles")]
        [Tooltip("When true, enemies that cannot reach their target will fire projectiles to chip away blocking tiles.")]
        public bool fireProjectilesWhenBlocked = true;
        [Tooltip("Maximum distance to the target for blocked-path shots.")]
        public float blockedShotRange = 9f;
        [Tooltip("Cooldown between forced shots while pathing is blocked.")]
        public float blockedShotCooldown = 1.25f;
        [Tooltip("Animator trigger(s) played before firing a blocked-path projectile.")]
        public string[] blockedShotTriggers;
        [Tooltip("Wind-up delay (seconds) between blocked shot animation trigger and projectile spawn.")]
        public float blockedShotWindup = 0.3f;

        // --- Ranged aim lock ---
        bool   _hasAimLock = false;
        Vector2 _aimLockDir = Vector2.right;

        [Header("Animator (shared)")]
        [Tooltip("Dot product threshold deciding when to play the strafe animation blend.")]
        [Range(0f,1f)] public float strafeDotThreshold = 0.35f;

        

        [Header("Debug")]
        [Tooltip("Draw helper gizmos such as roam radius, attack ranges, etc.")]
        public bool drawGizmos = true;
        [Tooltip("Print verbose debug logs for this AI.")]
        public bool debugLog   = false;

        #endregion

        #region Private

        Animator _anim;
        Rigidbody2D _rb;
        CircleCollider2D _myCircle;
        Collider2D _playerCollider;
        EnemyHealth2D _hp;
        AbilityRunner _abilityRunner;

        Transform _currentTarget;
        bool _targetIsPlayer = false;
        Collider2D _currentTargetCollider;
        NeutralNpcAI _currentNeutralTarget;
        CompanionAI _currentCompanionTarget;
        readonly Collider2D[] _neutralScanBuffer = new Collider2D[8];

        class TrackedTarget
        {
            public Transform Root;
            public bool IsPlayer;
            public PlayerHealth Player;
            public NeutralNpcAI Neutral;
            public CompanionAI Companion;
            public Collider2D Collider;
            public float LastSeenAt;
            public float LastKnownDistance;
            public float Threat;
            public float LastThreatUpdate;
            public bool IsValid => Root
                && (!IsPlayer || !PlayerHealth.IsPlayerDead)
                && (Neutral == null || !Neutral.IsDead)
                && (Companion == null || !Companion.IsDead);
        }

        readonly List<TrackedTarget> _trackedTargets = new List<TrackedTarget>();
        readonly Dictionary<Transform, TrackedTarget> _trackedLookup = new Dictionary<Transform, TrackedTarget>();

        // Mode/state
        Mode _mode = Mode.Roam;
        public bool InCombat => _mode == Mode.Engage;
        public bool IsBoss => isBoss;
        public string BossDisplayName => string.IsNullOrWhiteSpace(EnemyName) ? gameObject.name : EnemyName;
        public BossHealthBarUI BossHealthBarPrefab => bossHealthBarPrefab;
        public RectTransform BossHealthBarPanel => bossHealthBarPanel;
        public event Action<bool> CombatStateChanged;
        Vector2 _home;
        bool _hasHome;

        void SetMode(Mode newMode)
        {
            if (_mode == newMode) return;
            bool wasEngaged = _mode == Mode.Engage;
            _mode = newMode;
            bool isEngaged = _mode == Mode.Engage;
            if (wasEngaged != isEngaged)
            {
                CombatStateChanged?.Invoke(isEngaged);
                if (!isEngaged)
                    ClearTrackedTargets();
            }
        }

        Vector2 _vel;
        float   _orbitSide = 1f;
        float   _noiseSeedA, _noiseSeedB;
        float   _lastMoveTime;
        float   _lastStuckKickAt;

        // Pathfinding
        readonly List<Vector2> _pathBuffer = new List<Vector2>();
        int   _pathIndex = 0;
        Vector2 _pathDestination = Vector2.zero;
        float _pathNextRebuildAt = 0f;
        bool  _pathValid = false;
        Vector2 _lastProgressPos;
        float _lastProgressTime;
        float _lastForcedRepathAt;
        bool _pendingPathBlock;
        float _nextPathRetryAt;

        // Roam phase
        Vector2 _roamTarget;
        bool    _hasRoamTarget = false;
        float   _roamPhaseEndAt = 0f;
        bool    _roamPause = false;

        // Patrol state
        int   _patrolIndex = 0;
        int   _patrolDirection = 1;
        bool  _patrolWaiting = false;
        float _patrolResumeAt = 0f;
        bool  _patrolInitialized = false;
        readonly List<PatrolPoint> _patrolPointsCache = new List<PatrolPoint>();
        int _patrolCacheHash = int.MinValue;

        bool HasPatrolWaypoints
        {
            get
            {
                EnsurePatrolCache();
                return _patrolPointsCache.Count > 0;
            }
        }
        DefaultBehaviour _activeBehaviour;

        // melee attacker gate
        static readonly Dictionary<Transform,int> ActiveAttackers = new Dictionary<Transform,int>();
        bool  _holdingTicket = false;
        Transform _ticketTarget;
        float _lastAttackAt  = -999f;
        bool  _attacking     = false;     // used by both melee & ranged (blocks l ocomotion when true)
        Coroutine _attackRoutine;
        float _lungeBeganAt  = 0f;

        // ranged
        float _nextShotAt = 0f;
        float _nextBlockedShotAt = 0f;
        Coroutine _blockedShotRoutine;

        // context steering buffers
        Vector2[] _contextDirs;
        float[]   _contextInterest;
        float[]   _contextDanger;


        // animator hashes
        readonly int H_Direction        = Animator.StringToHash("Direction");
        readonly int H_DirIndex         = Animator.StringToHash("DirIndex");
        readonly int H_IsWalk           = Animator.StringToHash("IsWalk");
        readonly int H_IsRun            = Animator.StringToHash("IsRun");
        readonly int H_IsRunBackwards   = Animator.StringToHash("IsRunBackwards");
        readonly int H_IsStrafeLeft     = Animator.StringToHash("IsStrafeLeft");
        readonly int H_IsStrafeRight    = Animator.StringToHash("IsStrafeRight");

        PlayerHealth _playerHealth;
        bool _playerIsDeadSignal = false;

        bool _chaseOverrideActive = false;

        bool _externallyPaused = false;
        bool _externallyPausedIndefinite = false;
        float _externalPauseResumeAt = -1f;


        #endregion

        #region Unity

        void OnValidate()
        {
            contextDirectionCount = Mathf.Clamp(contextDirectionCount, 8, 32);
            EnsureContextArrays();
            pathRepathTime = Mathf.Max(0.1f, pathRepathTime);
            pathProgressDistance = Mathf.Max(0.01f, pathProgressDistance);
            stuckKickDelay = Mathf.Max(0f, stuckKickDelay);
            stuckKickCooldown = Mathf.Max(0f, stuckKickCooldown);
            stuckKickDistance = Mathf.Max(0f, stuckKickDistance);
            stuckKickRangeBuffer = Mathf.Max(0f, stuckKickRangeBuffer);
            stuckKickSpeedMultiplier = Mathf.Max(0.1f, stuckKickSpeedMultiplier);
            roamDistance.x = Mathf.Max(0f, roamDistance.x);
            roamDistance.y = Mathf.Max(0f, roamDistance.y);
            roamPause.x = Mathf.Max(0f, roamPause.x);
            roamPause.y = Mathf.Max(0f, roamPause.y);
            roamDestinationTolerance = Mathf.Max(0f, roamDestinationTolerance);
            localRoamRadius = Mathf.Max(0f, localRoamRadius);
            localRoamArriveThreshold = Mathf.Max(0f, localRoamArriveThreshold);
            localRoamMoveDuration.x = Mathf.Max(0f, localRoamMoveDuration.x);
            localRoamMoveDuration.y = Mathf.Max(0f, localRoamMoveDuration.y);
            localRoamPauseDuration.x = Mathf.Max(0f, localRoamPauseDuration.x);
            localRoamPauseDuration.y = Mathf.Max(0f, localRoamPauseDuration.y);
            blockedShotRange = Mathf.Max(0f, blockedShotRange);
            blockedShotCooldown = Mathf.Max(0.1f, blockedShotCooldown);
            _patrolCacheHash = int.MinValue;
        }

        void Awake()
        {
            EnsureContextArrays();

            _anim = GetComponent<Animator>();
            _rb   = GetComponent<Rigidbody2D>();
            _myCircle = GetComponent<CircleCollider2D>();
            _hp = GetComponent<EnemyHealth2D>();
            _abilityRunner = GetComponent<AbilityRunner>();

            TilemapPathfinder.EnsureInitialized();
            _pathDestination = transform.position;
            _pathNextRebuildAt = Time.time;
            _lastProgressPos = transform.position;
            _lastProgressTime = Time.time;
            _lastForcedRepathAt = -10f;
            _lastMoveTime = Time.time;
            _lastStuckKickAt = -10f;

            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
            if (player) _playerCollider = player.GetComponent<Collider2D>();
            if (player) _playerHealth = player.GetComponent<PlayerHealth>();

            _currentTarget = player;
            _targetIsPlayer = player != null;
            _currentTargetCollider = _playerCollider;

            // establish home
            _home = spawnPoint ? (Vector2)spawnPoint.position : (Vector2)transform.position;
            _hasHome = true;

            EnsureBossHealthBarPanelReference();


            // per-enemy randomization
            int seed = GetInstanceID();
            var r = new System.Random(seed);
            _orbitSide  = (r.NextDouble() < 0.5) ? -1f : +1f;
            _noiseSeedA = (float)r.NextDouble() * 1000f;
            _noiseSeedB = (float)r.NextDouble() * 1000f;

            // seed first shot time so multiple archers de-sync
            _nextShotAt = Time.time + UnityRandom.Range(shotCooldownRange.x, shotCooldownRange.y);

            // initialise default behaviour state
            InitializeBehaviourState();
        }

        void OnEnable()
        {
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.OnPlayerDied += OnPlayerDiedHandler;
                PlayerHealth.OnPlayerRespawned += OnPlayerRespawnedHandler;
            }
        }

        void OnDisable()
        {
            ClearTrackedTargets();
            ReleaseTicket();
            if (PlayerHealth.Instance != null)
            {
                PlayerHealth.OnPlayerDied -= OnPlayerDiedHandler;
                PlayerHealth.OnPlayerRespawned -= OnPlayerRespawnedHandler;
            }
            _pendingPathBlock = false;
        }


        void Update()
        {

            if (_hp && _hp.IsDead)
            {
                ClearTrackedTargets();
                FreezeMotion();
                // also push idle forward so animator doesn't keep aiming/turning
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            if (_externallyPaused)
            {
                if (!_externallyPausedIndefinite && _externalPauseResumeAt > 0f && Time.time >= _externalPauseResumeAt)
                {
                    _externallyPaused = false;
                    _externalPauseResumeAt = -1f;
                }

                if (_externallyPaused)
                {
                    FreezeMotion();
                    PushAnimatorIdleFacing(_lastMoveDir);
                    return;
                }
                else
                {
                    _externallyPausedIndefinite = false;
                }
            }

            RefreshCurrentTarget();

            // Drop neutral NPC targets the moment they die so we can immediately
            // retarget another hostile (player or different NPC) and resume movement.
            if (_currentNeutralTarget && _currentNeutralTarget.IsDead)
            {
                CancelAttackRoutine();
                var neutralRoot = _currentNeutralTarget.transform;
                if (neutralRoot)
                    UnregisterHostile(neutralRoot);
                _currentNeutralTarget = null;
                RefreshCurrentTarget();
            }

            if (_currentCompanionTarget && _currentCompanionTarget.IsDead)
            {
                CancelAttackRoutine();
                var compRoot = _currentCompanionTarget.transform;
                if (compRoot)
                    UnregisterHostile(compRoot);
                _currentCompanionTarget = null;
                RefreshCurrentTarget();
            }

            Transform target = _currentTarget;

            if (_pendingPathBlock)
            {
                bool firedBlockedShot = TryShootBlockedPathProjectile(target);
                if (Time.time >= _nextPathRetryAt)
                {
                    _pendingPathBlock = false;
                    ClearPath();
                }
                else
                {
                    FreezeMotion();
                    if (firedBlockedShot)
                        PushAnimatorIdleFacingPlayer();
                    else
                        PushAnimatorIdleFacing(_lastMoveDir);
                    return;
                }
            }

            if (!target && _attacking)
                CancelAttackRoutine();

            // If player is dead (via signal or direct check), do not engage.
            // Let them return home and idle/roam.
            bool playerDead = _targetIsPlayer && (PlayerHealth.IsPlayerDead || _playerIsDeadSignal);
            if (_targetIsPlayer && playerDead)
            {
                // If we were engaging, transition once to ReturnHome; otherwise keep current mode.
                if (_mode == Mode.Engage)
                {
                    if (returnToPost && _hasHome)
                        SetMode(Mode.ReturnHome);
                    else
                    {
                        SetMode(Mode.Roam);
                        ClearPath();
                        ReleaseTicket();
                    }
                }

                // Never attack while player is dead.
                _attacking = false;
                ReleaseTicket();

                // Drive behavior based on current mode:
                if (_mode == Mode.ReturnHome && returnToPost && _hasHome)
                {
                    DoReturnHome(Time.deltaTime);
                }
                else
                {
                    // Already home or no home configured â†’ just roam.
                    if (_mode != Mode.Roam)
                    {
                        SetMode(Mode.Roam);
                        ClearPath();
                        ReleaseTicket();
                    }
                    DoRoam(Time.deltaTime);
                }

                // Nice idle facing when not moving.
                #if UNITY_2022_2_OR_NEWER
                        if (_rb && _rb.linearVelocity.sqrMagnitude < 0.0001f) PushAnimatorIdleFacing(_lastMoveDir);

                #else
                        if (_rb && _rb.velocity.sqrMagnitude < 0.0001f) PushAnimatorIdleFacing(_lastMoveDir);

                #endif
                return; // nothing else while player is dead
            }

            if (Time.time < _stunUntil)
            {
                FreezeMotion();
                PushAnimatorIdleFacingForward(); // stunned: don't face player
                return;
            }

            if (!target)
            {
                DoRoam(Time.deltaTime);
                return;
            }

            // base vectors
            Vector2 pos  = transform.position;
            Vector2 ppos = target.position;
            Vector2 toP  = ppos - pos;
            float centerDist = toP.magnitude;

            // Mode transitions
            switch (_mode)
            {
                case Mode.Roam:
                    if (centerDist <= engageRadius)
                    {
                        SetMode(Mode.Engage);
                        ClearPath();
                        ReleaseTicket();
                    }
                    else { DoRoam(Time.deltaTime); return; }
                    break;

                case Mode.Engage:
                    if (centerDist > leashRadius)
                    {
                        if (returnToPost && _hasHome)
                        {
                            SetMode(Mode.ReturnHome);
                            ClearPath();
                            ReleaseTicket();
                        }
                        else
                        {
                            SetMode(Mode.Roam);
                            ClearPath();
                            ReleaseTicket();
                            DoRoam(Time.deltaTime);
                            return;
                        }
                    }
                    break;

                case Mode.ReturnHome:
                    if (!returnToPost || !_hasHome)
                    {
                        SetMode(Mode.Roam);
                        ClearPath();
                        ReleaseTicket();
                        DoRoam(Time.deltaTime);
                        return;
                    }
                    if (centerDist <= engageRadius)
                    {
                        SetMode(Mode.Engage);
                        ClearPath();
                        ReleaseTicket();
                    }
                    else { DoReturnHome(Time.deltaTime); return; }
                    break;
            }

            // If here -> Engage
            Vector2 dir   = (centerDist > 0.0001f) ? toP / centerDist : Vector2.right;
            Vector2 right = new Vector2(dir.y, -dir.x);

            if (type == AIType.Melee)
                TickMelee(centerDist, dir, right);
            else
                TickRanged(centerDist, dir, right);
        }

        void RefreshCurrentTarget()
        {
            Vector2 myPos = transform.position;

            PruneTrackedTargets(myPos);

            bool playerAlive = player && !PlayerHealth.IsPlayerDead;
            if (playerAlive)
            {
                float dist = Vector2.Distance(myPos, player.position);
                float trackRadius = Mathf.Max(leashRadius, engageRadius, neutralNpcAggroRadius);
                if (dist <= trackRadius || _targetIsPlayer || _mode == Mode.Engage)
                {
                    RegisterHostile(player, _playerCollider, forceEngage: false);
                }
            }

            if (neutralNpcMask.value != 0)
            {
                float scanRadius = Mathf.Max(neutralNpcAggroRadius, engageRadius);
                ContactFilter2D neutralFilter = new ContactFilter2D { useTriggers = true };
                neutralFilter.SetLayerMask(neutralNpcMask);
                neutralFilter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
                int count = Physics2D.OverlapCircle(myPos, scanRadius, neutralFilter, _neutralScanBuffer);
                for (int i = 0; i < count; i++)
                {
                    var col = _neutralScanBuffer[i];
                    if (!col) continue;
                    var npc = col.GetComponentInParent<NeutralNpcAI>();
                    if (npc)
                    {
                        if (npc.IsDead) continue;
                        RegisterHostile(npc.transform, col, forceEngage: false);
                        continue;
                    }

                    var companion = col.GetComponentInParent<CompanionAI>();
                    if (companion == null || companion.IsDead) continue;
                    RegisterHostile(companion.transform, col, forceEngage: false);
                }
            }

            Transform best = null;
            bool bestIsPlayer = false;
            Collider2D bestCollider = null;
            NeutralNpcAI bestNeutral = null;
            CompanionAI bestCompanion = null;
            float bestDistance = float.PositiveInfinity;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < _trackedTargets.Count; i++)
            {
                var tracked = _trackedTargets[i];
                if (tracked == null || !tracked.IsValid) continue;
                var root = tracked.Root;
                if (!root || AbilityStealthUtility.IsInvisible(root)) continue;
                float dist = Vector2.Distance(myPos, root.position);
                tracked.LastKnownDistance = dist;

                float threatValue = Mathf.Max(minThreat, tracked.Threat);
                float proximityScore = proximityThreatWeight / Mathf.Max(0.5f, dist);
                float timeSinceSeen = Mathf.Max(0f, Time.time - tracked.LastSeenAt);
                float recencyScore = Mathf.Max(0f, threatMemoryDuration - timeSinceSeen) * 0.1f;
                float totalScore = threatValue + proximityScore + recencyScore;

                if (totalScore > bestScore + 0.0001f || (Mathf.Abs(totalScore - bestScore) <= 0.0001f && dist < bestDistance))
                {
                    bestScore = totalScore;
                    bestDistance = dist;
                    best = root;
                    bestIsPlayer = tracked.IsPlayer;
                    bestNeutral = tracked.Neutral;
                    bestCompanion = tracked.Companion;
                    bestCollider = ResolveCollider(tracked);
                }
            }

            if (!best && playerAlive && !AbilityStealthUtility.IsInvisible(player))
            {
                best = player;
                bestIsPlayer = true;
                bestCollider = _playerCollider;
            }

            if (_holdingTicket && _ticketTarget && _ticketTarget != best)
            {
                ReleaseTicket();
            }

            _currentTarget = best;
            _targetIsPlayer = bestIsPlayer;
            _currentTargetCollider = bestCollider;
            _currentNeutralTarget = bestNeutral;
            _currentCompanionTarget = bestCompanion;
            if (_holdingTicket && !_ticketTarget)
                _holdingTicket = false;
        }

        bool TryResolveThreat(Transform source, out Transform root, out PlayerHealth playerThreat, out NeutralNpcAI neutralThreat, out CompanionAI companionThreat)
        {
            root = null;
            playerThreat = null;
            neutralThreat = null;
            companionThreat = null;
            if (!source) return false;

            playerThreat = source.GetComponentInParent<PlayerHealth>();
            if (playerThreat)
            {
                root = playerThreat.transform;
            }
            else
            {
                neutralThreat = source.GetComponentInParent<NeutralNpcAI>();
                if (neutralThreat)
                {
                    root = neutralThreat.transform;
                }
                else
                {
                    companionThreat = source.GetComponentInParent<CompanionAI>();
                    if (companionThreat && !companionThreat.IsDead)
                        root = companionThreat.transform;
                }
            }

            if (!root)
                root = source;

            if (!root || root == transform || root.IsChildOf(transform))
                return false;

            return true;
        }

        public void RegisterHostile(Transform threat, Collider2D knownCollider = null, bool forceEngage = true)
        {
            if (!TryResolveThreat(threat, out var root, out var playerThreat, out var neutralThreat, out var companionThreat))
                return;
            if (AbilityStealthUtility.IsInvisible(root))
                return;

            if (!_trackedLookup.TryGetValue(root, out var tracked))
            {
                tracked = new TrackedTarget
                {
                    Root = root,
                    IsPlayer = playerThreat != null,
                    Player = playerThreat,
                    Neutral = neutralThreat,
                    Companion = companionThreat,
                    Collider = knownCollider,
                    LastSeenAt = Time.time,
                    Threat = playerThreat ? playerBaseThreat : neutralBaseThreat,
                    LastThreatUpdate = Time.time
                };
                _trackedTargets.Add(tracked);
                _trackedLookup[root] = tracked;
            }
            else
            {
                tracked.LastSeenAt = Time.time;
                float baseThreat = tracked.IsPlayer ? playerBaseThreat : neutralBaseThreat;
                if (tracked.Threat < baseThreat)
                    tracked.Threat = baseThreat;
                if (tracked.Threat < minThreat)
                    tracked.Threat = minThreat;
                tracked.LastThreatUpdate = Time.time;
                if (playerThreat && !tracked.IsPlayer)
                {
                    tracked.IsPlayer = true;
                    tracked.Player = playerThreat;
                }
                if (neutralThreat && tracked.Neutral != neutralThreat)
                    tracked.Neutral = neutralThreat;
                if (companionThreat && tracked.Companion != companionThreat)
                    tracked.Companion = companionThreat;
                if (knownCollider)
                    tracked.Collider = knownCollider;
            }

            if ((!tracked.Collider || !tracked.Collider.enabled) && tracked.Root)
                tracked.Collider = ResolveCollider(tracked);

            tracked.LastKnownDistance = Vector2.Distance(transform.position, tracked.Root.position);

            if (forceEngage && _mode != Mode.Engage)
            {
                SetMode(Mode.Engage);
                ClearPath();
            }
        }

        void AddThreat(Transform threat, float amount)
        {
            if (!TryResolveThreat(threat, out var root, out _, out _, out _))
                return;
            if (AbilityStealthUtility.IsInvisible(root))
                return;
            if (!_trackedLookup.TryGetValue(root, out var tracked))
                return;

            float delta = amount > 0f ? Mathf.Max(0.1f, amount * damageThreatMultiplier) : passiveThreatIncrement;
            tracked.Threat = Mathf.Max(minThreat, tracked.Threat + delta);
            tracked.LastThreatUpdate = Time.time;
            tracked.LastSeenAt = Time.time;
        }

        public void UnregisterHostile(Transform threat)
        {
            if (!TryResolveThreat(threat, out var root, out _, out _, out _))
                return;

            if (!root) return;
            if (_trackedLookup.TryGetValue(root, out var tracked))
            {
                int index = _trackedTargets.IndexOf(tracked);
                if (index >= 0)
                    RemoveTrackedTargetAt(index);
                else
                {
                    _trackedLookup.Remove(root);
                    if (_currentTarget == root)
                    {
                        _currentTarget = null;
                        _currentTargetCollider = null;
                        _currentNeutralTarget = null;
                        _currentCompanionTarget = null;
                        _targetIsPlayer = false;
                        ReleaseTicket();
                    }
                }
            }
        }

        void ClearTrackedTargets()
        {
            _trackedTargets.Clear();
            _trackedLookup.Clear();
            _currentTarget = null;
            _currentTargetCollider = null;
            _currentNeutralTarget = null;
            _currentCompanionTarget = null;
            _targetIsPlayer = false;
            ReleaseTicket();
        }

        void PruneTrackedTargets(Vector2 myPos)
        {
            float forgetDist = Mathf.Max(threatForgetDistance, leashRadius * 1.2f, engageRadius * 1.1f);
            for (int i = _trackedTargets.Count - 1; i >= 0; i--)
            {
                var tracked = _trackedTargets[i];
                bool remove = tracked == null || !tracked.IsValid || !tracked.Root;
                if (!remove)
                {
                    float now = Time.time;
                    float dt = Mathf.Max(0f, now - tracked.LastThreatUpdate);
                    if (dt > 0f && threatDecayPerSecond > 0f)
                    {
                        tracked.Threat = Mathf.Max(minThreat, tracked.Threat - threatDecayPerSecond * dt);
                        tracked.LastThreatUpdate = now;
                    }

                    float dist = Vector2.Distance(myPos, tracked.Root.position);
                    tracked.LastKnownDistance = dist;
                    float timeSinceSeen = Time.time - tracked.LastSeenAt;

                    if (dist > forgetDist && timeSinceSeen > threatMemoryDuration * 0.5f)
                        remove = true;
                    else if (timeSinceSeen > threatMemoryDuration && dist > engageRadius * 0.75f)
                        remove = true;
                }

                if (remove)
                    RemoveTrackedTargetAt(i);
            }
        }

        void RemoveTrackedTargetAt(int index)
        {
            if (index < 0 || index >= _trackedTargets.Count) return;
            var tracked = _trackedTargets[index];
            if (tracked != null && tracked.Root)
                _trackedLookup.Remove(tracked.Root);

            if (_currentTarget == tracked?.Root)
            {
                _currentTarget = null;
                _currentTargetCollider = null;
                _currentNeutralTarget = null;
                _currentCompanionTarget = null;
                _targetIsPlayer = false;
                ReleaseTicket();
            }

            _trackedTargets.RemoveAt(index);
        }

        bool AnyTrackedTargetWithinRange(float range, Transform exclude = null)
        {
            if (range <= 0f) return false;

            float rangeSqr = range * range;
            Vector2 myPos = transform.position;

            for (int i = 0; i < _trackedTargets.Count; i++)
            {
                var tracked = _trackedTargets[i];
                if (tracked == null || !tracked.IsValid) continue;
                if (!tracked.Root) continue;
                if (exclude && tracked.Root == exclude) continue;

                Vector2 toTarget = (Vector2)tracked.Root.position - myPos;
                if (toTarget.sqrMagnitude <= rangeSqr)
                    return true;
            }

            return false;
        }

        Collider2D ResolveCollider(TrackedTarget tracked)
        {
            if (tracked == null) return null;

            if (tracked.Collider && tracked.Collider.enabled)
                return tracked.Collider;

            Collider2D col = null;
            if (tracked.Player)
            {
                col = tracked.Player.GetComponent<Collider2D>();
                if (!col)
                    col = tracked.Player.GetComponentInChildren<Collider2D>();
            }

            if (!col && tracked.Neutral)
            {
                col = tracked.Neutral.GetComponent<Collider2D>();
                if (!col)
                    col = tracked.Neutral.GetComponentInChildren<Collider2D>();
            }

            if (!col && tracked.Root)
            {
                col = tracked.Root.GetComponent<Collider2D>();
                if (!col)
                    col = tracked.Root.GetComponentInChildren<Collider2D>();
            }

            tracked.Collider = col;
            return col;
        }

        public static void NotifyDamageDealt(IDamageable target, Transform attacker, float damageAmount = 0f)
        {
            if (target == null || attacker == null) return;

            float normalizedDamage = Mathf.Max(0f, damageAmount);
            DamageDealt?.Invoke(attacker, target, normalizedDamage);

            if (target is Component comp)
            {
                var enemy = comp.GetComponentInParent<EnemyAI>();
                if (!enemy) return;

                Collider2D attackerCollider = attacker.GetComponent<Collider2D>();
                if (!attackerCollider)
                    attackerCollider = attacker.GetComponentInChildren<Collider2D>();

                enemy.RegisterHostile(attacker, attackerCollider, forceEngage: true);
                enemy.AddThreat(attacker, normalizedDamage);

                var enemyHealth = enemy.GetComponent<EnemyHealth2D>();
                if (enemyHealth)
                {
                    enemyHealth.RegisterLastAttacker(attacker);
                }
            }
        }

        #endregion

        #region Context Steering Helpers

        void EnsureContextArrays()
        {
            int count = Mathf.Clamp(contextDirectionCount, 8, 32);
            if (_contextDirs != null && _contextDirs.Length == count)
            {
                if (_contextInterest == null || _contextInterest.Length != count)
                    _contextInterest = new float[count];
                if (_contextDanger == null || _contextDanger.Length != count)
                    _contextDanger = new float[count];
                return;
            }

            _contextDirs = new Vector2[count];
            _contextInterest = new float[count];
            _contextDanger = new float[count];

            float step = Mathf.PI * 2f / count;
            for (int i = 0; i < count; i++)
            {
                float angle = step * i;
                _contextDirs[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        void ClearContextBuffers()
        {
            if (_contextDirs == null || _contextInterest == null || _contextDanger == null)
                EnsureContextArrays();

            int count = _contextInterest.Length;
            System.Array.Clear(_contextInterest, 0, count);
            System.Array.Clear(_contextDanger, 0, count);
        }

        void AddContextInterest(Vector2 dir, float weight)
        {
            if (_contextDirs == null) EnsureContextArrays();
            if (weight <= 0f) return;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            for (int i = 0; i < _contextDirs.Length; i++)
            {
                float align = Vector2.Dot(_contextDirs[i], dir);
                if (align <= 0f) continue;
                _contextInterest[i] += align * weight;
            }
        }

        void AddContextDanger(Vector2 dir, float weight)
        {
            if (_contextDirs == null) EnsureContextArrays();
            if (weight <= 0f) return;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            for (int i = 0; i < _contextDirs.Length; i++)
            {
                float align = Vector2.Dot(_contextDirs[i], dir);
                if (align <= 0f) continue;
                float danger = align * weight;
                if (danger > _contextDanger[i])
                    _contextDanger[i] = danger;
            }
        }

        void InjectObstacleDanger(Vector2 origin)
        {
            if (obstacleMask.value == 0 || contextProbeDistance <= 0f)
                return;

            float maxDist = Mathf.Max(0.05f, contextProbeDistance);
            float radius = Mathf.Max(0f, contextProbeRadius);

            for (int i = 0; i < _contextDirs.Length; i++)
            {
                Vector2 dir = _contextDirs[i];
                RaycastHit2D hit = Physics2D.CircleCast(origin, radius, dir, maxDist, obstacleMask);
                if (hit.collider)
                {
                    float normalAlign = Mathf.Abs(Vector2.Dot(hit.normal, dir));
                    if (normalAlign < 0.25f)
                        continue;

                    float t = 1f - Mathf.Clamp01(hit.distance / maxDist);
                    float danger = t * contextObstacleWeight * Mathf.Lerp(0.25f, 1f, normalAlign);
                    if (danger > _contextDanger[i])
                        _contextDanger[i] = danger;
                }
            }
        }

        Vector2 ResolveContextVector(out float strength)
        {
            Vector2 result = Vector2.zero;
            float total = 0f;

            for (int i = 0; i < _contextDirs.Length; i++)
            {
                float interest = Mathf.Max(0f, _contextInterest[i]);
                float danger = Mathf.Clamp01(_contextDanger[i]);
                float weight = interest * (1f - danger);
                if (weight <= 0f) continue;

                result += _contextDirs[i] * weight;
                total += weight;
            }

            strength = (_contextDirs.Length > 0) ? Mathf.Clamp01(total / _contextDirs.Length) : 0f;
            if (total > 0.0001f)
                result /= total;

            return result;
        }

        #endregion

        #region Roam / ReturnHome (NEW)

        Vector2 GetHomeAnchor()
        {
            if (_hasHome)
                return _home;
            if (spawnPoint)
                return spawnPoint.position;
            return transform.position;
        }

        Vector2 GetPatrolAnchor()
        {
            return GetHomeAnchor();
        }

        int ComputePatrolHash()
        {
            if (patrolWaypoints == null)
                return 0;

            unchecked
            {
                int hash = patrolWaypoints.Length;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    var wp = patrolWaypoints[i];
                    hash = (hash * 397) ^ (wp ? wp.GetInstanceID() : 0);
                }
                return hash;
            }
        }

        void EnsurePatrolCache()
        {
            Vector2 anchor = GetPatrolAnchor();
            int hash = ComputePatrolHash();

            if (hash != _patrolCacheHash)
            {
                _patrolCacheHash = hash;
                _patrolPointsCache.Clear();

                if (patrolWaypoints != null)
                {
                    for (int i = 0; i < patrolWaypoints.Length; i++)
                    {
                        var wp = patrolWaypoints[i];
                        if (!wp) continue;

                        _patrolPointsCache.Add(new PatrolPoint
                        {
                            Transform = wp,
                            OffsetFromAnchor = (Vector2)wp.position - anchor
                        });
                    }
                }

                int count = _patrolPointsCache.Count;
                _patrolIndex = (count > 0) ? Mathf.Clamp(_patrolIndex, 0, count - 1) : 0;
                return;
            }

            for (int i = 0; i < _patrolPointsCache.Count; i++)
            {
                var point = _patrolPointsCache[i];
                if (point.Transform)
                {
                    Vector2 currentOffset = (Vector2)point.Transform.position - anchor;
                    if ((currentOffset - point.OffsetFromAnchor).sqrMagnitude > 0.000001f)
                    {
                        point.OffsetFromAnchor = currentOffset;
                        _patrolPointsCache[i] = point;
                    }
                }
            }
        }

        void InitializeBehaviourState()
        {
            _patrolWaiting = false;
            _patrolInitialized = false;
            _activeBehaviour = startingBehaviour;

            switch (startingBehaviour)
            {
                case DefaultBehaviour.Patrol:
                    ResetPatrolState(startPaused: false);
                    break;
                case DefaultBehaviour.HoldPosition:
                    ClearPath();
                    _roamPause = true;
                    FreezeMotion();
                    break;
                case DefaultBehaviour.LocalRoam:
                    _roamPause = false;
                    BeginLocalRoamPhase(false);
                    break;
                default:
                    ResetFreeRoamState(startPaused: false);
                    break;
            }
        }

        void DoRoam(float dt)
        {
            if (startingBehaviour != _activeBehaviour)
                InitializeBehaviourState();

            switch (startingBehaviour)
            {
                case DefaultBehaviour.HoldPosition:
                    DoHoldPosition(dt);
                    break;
                case DefaultBehaviour.Patrol:
                    DoPatrol(dt);
                    break;
                case DefaultBehaviour.LocalRoam:
                    DoLocalRoam(dt);
                    break;
                default:
                    DoFreeRoam(dt);
                    break;
            }
        }

        void DoRoamCommon(float dt, float arriveThreshold, System.Action<bool> beginPhase)
        {
            if (!_hasHome) { _home = transform.position; _hasHome = true; }

            if (Time.time >= _roamPhaseEndAt)
            {
                _roamPause = !_roamPause;
                beginPhase(_roamPause);
            }

            if (_roamPause)
            {
                FreezeMotion();
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            Vector2 pos = transform.position;
            Vector2 toT = _roamTarget - pos;
            float d = toT.magnitude;

            Vector2 forward = d > 0.0001f ? (toT / d) : _lastMoveDir;
            Vector2 pathForward = GetPathDirection(_roamTarget, 1.0f, 0.25f, 0.18f);
            if (pathForward.sqrMagnitude > 0.0001f) forward = pathForward;

            float t = (arriveBrakeRadius > 0.0001f) ? Mathf.Clamp01(d / arriveBrakeRadius) : 1f;
            float targetSpeed = Mathf.Lerp(0.0f, roamSpeed, t);

            Vector2 desired = forward * targetSpeed;
            HandlePathStuck(targetSpeed);
            _vel = Vector2.MoveTowards(_vel, desired, roamAccel * dt);

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * dt);

            if (_vel.sqrMagnitude > 0.0001f) _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f) _lastMoveTime = Time.time;

            if (d <= arriveThreshold)
            {
                _roamPause = true;
                beginPhase(true);
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            PushAnimatorByForward(_vel);
        }

        void DoFreeRoam(float dt)
        {
            if (!_hasHome) { _home = transform.position; _hasHome = true; }

            if (_roamPause)
            {
                if (Time.time >= _roamPhaseEndAt)
                {
                    _roamPause = false;
                    _roamPhaseEndAt = -1f;
                    ClearPath();
                }
                else
                {
                    FreezeMotion();
                    PushAnimatorIdleFacing(_lastMoveDir);
                    return;
                }
            }

            Vector2 pos = transform.position;
            float tolerance = Mathf.Max(0.05f, roamDestinationTolerance);

            if (!_hasRoamTarget)
            {
                if (!TryPickFreeRoamDestination())
                {
                    BeginFreeRoamPause();
                    FreezeMotion();
                    PushAnimatorIdleFacing(_lastMoveDir);
                    return;
                }
            }

            Vector2 toTarget = _roamTarget - pos;
            float distance = toTarget.magnitude;

            if (distance <= tolerance)
            {
                _hasRoamTarget = false;
                BeginFreeRoamPause();
                FreezeMotion();
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            Vector2 forward = distance > 0.0001f ? (toTarget / distance) : _lastMoveDir;
            Vector2 pathForward = GetPathDirection(_roamTarget, 1.0f, tolerance, Mathf.Max(0.1f, tolerance * 0.5f));
            if (pathForward.sqrMagnitude > 0.0001f)
                forward = pathForward;

            float t = (arriveBrakeRadius > 0.0001f) ? Mathf.Clamp01(distance / arriveBrakeRadius) : 1f;
            float targetSpeed = Mathf.Lerp(0.0f, roamSpeed, t);

            Vector2 desired = forward * targetSpeed;
            HandlePathStuck(targetSpeed);
            _vel = Vector2.MoveTowards(_vel, desired, roamAccel * dt);

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * dt);

            if (_vel.sqrMagnitude > 0.0001f) _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f) _lastMoveTime = Time.time;

            PushAnimatorByForward(_vel);
        }

        void BeginFreeRoamPause()
        {
            float minPause = Mathf.Min(roamPause.x, roamPause.y);
            float maxPause = Mathf.Max(roamPause.x, roamPause.y);
            minPause = Mathf.Max(0f, minPause);
            maxPause = Mathf.Max(minPause, maxPause);

            _roamPause = true;
            _roamPhaseEndAt = Time.time + UnityRandom.Range(minPause, maxPause);
            _hasRoamTarget = false;
            _vel = Vector2.zero;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            ClearPath();
        }

        void ResetFreeRoamState(bool startPaused)
        {
            _hasRoamTarget = false;
            if (startPaused)
            {
                BeginFreeRoamPause();
            }
            else
            {
                _roamPause = false;
                _roamPhaseEndAt = -1f;
                _vel = Vector2.zero;
                if (_rb) _rb.linearVelocity = Vector2.zero;
                ClearPath();
            }
        }

        bool TryPickFreeRoamDestination()
        {
            float minDistance = Mathf.Max(0.5f, Mathf.Min(roamDistance.x, roamDistance.y));
            float maxDistance = Mathf.Max(minDistance + 0.1f, Mathf.Max(roamDistance.x, roamDistance.y));
            Vector2 origin = _home;
            const int MAX_ATTEMPTS = 8;

            ClearPath();

            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                Vector2 dir = UnityRandom.insideUnitCircle;
                if (dir.sqrMagnitude < 0.0001f)
                    dir = Vector2.right;
                else
                    dir.Normalize();

                float lerpT = MAX_ATTEMPTS <= 1 ? 0f : (float)attempt / (MAX_ATTEMPTS - 1);
                float distance = Mathf.Lerp(maxDistance, minDistance, lerpT);
                Vector2 candidate = origin + dir * distance;

                if (!TilemapPathfinder.HasColliderMap)
                {
                    _roamTarget = candidate;
                    _hasRoamTarget = true;
                    _pathDestination = candidate;
                    _pathIndex = 0;
                    _pathValid = false;
                    _pathNextRebuildAt = Time.time;
                    return true;
                }

                if (TryBuildPath(candidate))
                {
                    _roamTarget = candidate;
                    _hasRoamTarget = true;
                    _pathNextRebuildAt = Time.time;
                    return true;
                }
            }

            _hasRoamTarget = false;
            ClearPath();
            return false;
        }

        void DoLocalRoam(float dt)
        {
            DoRoamCommon(dt, localRoamArriveThreshold, BeginLocalRoamPhase);
        }

        void BeginLocalRoamPhase(bool pause)
        {
            BeginRoamPhase(pause, localRoamPauseDuration, localRoamMoveDuration, localRoamRadius);
        }

        void BeginRoamPhase(bool pause, Vector2 pauseDurationRange, Vector2 moveDurationRange, float radius)
        {
            if (pause)
            {
                float minPause = Mathf.Min(pauseDurationRange.x, pauseDurationRange.y);
                float maxPause = Mathf.Max(pauseDurationRange.x, pauseDurationRange.y);
                minPause = Mathf.Max(0f, minPause);
                maxPause = Mathf.Max(minPause, maxPause);
                _roamPhaseEndAt = Time.time + UnityRandom.Range(minPause, maxPause);
                _vel = Vector2.zero;
                if (_rb) _rb.linearVelocity = Vector2.zero;

                PushAnimatorIdleFacing(_lastMoveDir);
                ClearPath();
            }
            else
            {
                Vector2 center = GetHomeAnchor();
                ClearPath();

                const int MAX_ATTEMPTS = 6;
                bool picked = false;
                float minMove = Mathf.Min(moveDurationRange.x, moveDurationRange.y);
                float maxMove = Mathf.Max(moveDurationRange.x, moveDurationRange.y);
                minMove = Mathf.Max(0f, minMove);
                maxMove = Mathf.Max(minMove, maxMove);
                float clampedRadius = Mathf.Max(0f, radius);
                for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
                {
                    Vector2 candidate = center + UnityRandom.insideUnitCircle * clampedRadius;
                    _roamTarget = candidate;

                    if (TilemapPathfinder.HasColliderMap)
                    {
                        if (!TilemapPathfinder.IsWalkable(candidate))
                            continue;

                        if (!TryBuildPath(candidate))
                            continue;
                    }

                    picked = true;
                    break;
                }

                if (!picked)
                {
                    _roamTarget = center;
                    _pathDestination = _roamTarget;
                    _pathValid = false;
                }

                _pathNextRebuildAt = Time.time;
                _roamPhaseEndAt = Time.time + UnityRandom.Range(minMove, maxMove);
            }
        }

        Vector2 GetHoldPosition()
        {
            Vector2 anchor = GetHomeAnchor();
            return anchor + holdOffset;
        }

        void DoHoldPosition(float dt)
        {
            if (!_hasHome) { _home = transform.position; _hasHome = true; }

            Vector2 holdPos = GetHoldPosition();
            Vector2 pos = transform.position;
            Vector2 toAnchor = holdPos - pos;
            float d = toAnchor.magnitude;

            float tolerance = Mathf.Max(0.01f, holdPositionTolerance);
            if (d <= tolerance)
            {
                FreezeMotion();
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            Vector2 forward = d > 0.0001f ? (toAnchor / d) : _lastMoveDir;
            Vector2 pathForward = GetPathDirection(holdPos, 0.5f, tolerance, 0.18f);
            if (pathForward.sqrMagnitude > 0.0001f) forward = pathForward;

            float t = (arriveBrakeRadius > 0.0001f) ? Mathf.Clamp01(d / arriveBrakeRadius) : 1f;
            float targetSpeed = Mathf.Lerp(0.0f, Mathf.Max(roamSpeed, walkSpeed), t);

            Vector2 desired = forward * targetSpeed;
            HandlePathStuck(targetSpeed);
            _vel = Vector2.MoveTowards(_vel, desired, Mathf.Max(roamAccel, accel) * dt);

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * dt);

            if (_vel.sqrMagnitude > 0.0001f) _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f) _lastMoveTime = Time.time;

            PushAnimatorByForward(_vel);
        }

        void DoPatrol(float dt)
        {
            if (!HasPatrolWaypoints)
            {
                DoHoldPosition(dt);
                return;
            }

            if (!_patrolInitialized)
                ResetPatrolState(startPaused: false);

            if (_patrolWaiting)
            {
                if (Time.time >= _patrolResumeAt)
                {
                    _patrolWaiting = false;
                }
                else
                {
                    FreezeMotion();
                    PushAnimatorIdleFacing(_lastMoveDir);
                    return;
                }
            }

            if (!TryGetPatrolDestination(out var destination))
            {
                DoHoldPosition(dt);
                return;
            }

            Vector2 pos = transform.position;
            Vector2 toDest = destination - pos;
            float d = toDest.magnitude;

            Vector2 forward = d > 0.0001f ? (toDest / d) : _lastMoveDir;
            Vector2 pathForward = GetPathDirection(destination, 0.5f, patrolWaypointTolerance, 0.18f);
            if (pathForward.sqrMagnitude > 0.0001f) forward = pathForward;

            float t = (arriveBrakeRadius > 0.0001f) ? Mathf.Clamp01(d / arriveBrakeRadius) : 1f;
            float targetSpeed = Mathf.Lerp(0.0f, walkSpeed, t);

            Vector2 desired = forward * targetSpeed;
            HandlePathStuck(targetSpeed);
            _vel = Vector2.MoveTowards(_vel, desired, accel * dt);

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * dt);

            if (_vel.sqrMagnitude > 0.0001f) _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f) _lastMoveTime = Time.time;

            if (d <= Mathf.Max(0.01f, patrolWaypointTolerance))
            {
                BeginPatrolPause();
                AdvancePatrolWaypoint();
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            PushAnimatorByForward(_vel);
        }

        void ResetPatrolState(bool startPaused)
        {
            EnsurePatrolCache();
            _patrolIndex = 0;
            _patrolDirection = 1;
            _patrolInitialized = HasPatrolWaypoints;
            _patrolWaiting = false;
            _patrolResumeAt = Time.time;
            ClearPath();

            if (startPaused && HasPatrolWaypoints)
            {
                BeginPatrolPause();
            }
        }

        bool TryGetPatrolDestination(out Vector2 destination)
        {
            EnsurePatrolCache();
            destination = GetHoldPosition();

            int count = _patrolPointsCache.Count;
            if (count == 0)
                return false;

            _patrolIndex = Mathf.Clamp(_patrolIndex, 0, count - 1);
            Vector2 anchor = GetPatrolAnchor();

            for (int attempt = 0; attempt < count; attempt++)
            {
                int idx = (_patrolIndex + attempt) % count;
                if (idx < 0 || idx >= _patrolPointsCache.Count)
                    continue;

                var point = _patrolPointsCache[idx];
                destination = anchor + point.OffsetFromAnchor;
                _patrolIndex = idx;
                return true;
            }

            return false;
        }

        void AdvancePatrolWaypoint()
        {
            EnsurePatrolCache();

            int count = _patrolPointsCache.Count;
            if (count <= 1)
                return;

            switch (patrolMode)
            {
                case PatrolMode.Random:
                    int newIndex = _patrolIndex;
                    for (int attempt = 0; attempt < 6; attempt++)
                    {
                        newIndex = UnityRandom.Range(0, count);
                        if (newIndex != _patrolIndex || count == 1)
                            break;
                    }
                    _patrolIndex = newIndex;
                    break;
                case PatrolMode.PingPong:
                    if (_patrolDirection == 0)
                        _patrolDirection = 1;
                    _patrolIndex += _patrolDirection;
                    if (_patrolIndex >= count)
                    {
                        _patrolDirection = -1;
                        _patrolIndex = Mathf.Max(0, count - 2);
                    }
                    else if (_patrolIndex < 0)
                    {
                        _patrolDirection = 1;
                        _patrolIndex = (count > 1) ? 1 : 0;
                    }
                    break;
                default:
                    _patrolIndex = (_patrolIndex + 1) % count;
                    break;
            }
        }

        void BeginPatrolPause()
        {
            ClearPath();
            float min = Mathf.Min(patrolPauseDuration.x, patrolPauseDuration.y);
            float max = Mathf.Max(patrolPauseDuration.x, patrolPauseDuration.y);
            min = Mathf.Max(0f, min);
            max = Mathf.Max(min, max);

            if (max <= 0f)
            {
                _patrolWaiting = false;
                _patrolResumeAt = Time.time;
                return;
            }

            _patrolWaiting = true;
            _patrolResumeAt = Time.time + UnityRandom.Range(min, max);
            FreezeMotion();
        }

        void DoReturnHome(float dt)
        {
            if (!returnToPost || !_hasHome)
            {
                SetMode(Mode.Roam);
                return;
            }

            Vector2 pos = transform.position;
            Vector2 toHome = _home - pos;
            float d = toHome.magnitude;

            // If "at home", transition to Roam + pause, but don't teleport
            if (d <= homeSnapDistance)
            {
                _vel = Vector2.zero;
                if (_rb) _rb.linearVelocity = Vector2.zero;

                SetMode(Mode.Roam);
                OnReturnedHome();

                // Face the direction we approached from (toward home)
                Vector2 last = toHome.sqrMagnitude > 0.0001f ? toHome.normalized : _lastMoveDir;
                _lastMoveDir = last;
                PushAnimatorIdleFacing(_lastMoveDir);
                return;
            }

            // Smooth approach back home (brake near target)
            Vector2 forward = toHome / Mathf.Max(0.0001f, d);
            Vector2 pathForward = GetPathDirection(_home, 0.8f, 0.15f, 0.2f);
            if (pathForward.sqrMagnitude > 0.0001f) forward = pathForward;
            float t = (arriveBrakeRadius > 0.0001f) ? Mathf.Clamp01(d / arriveBrakeRadius) : 1f;
            float targetSpeed = Mathf.Lerp(0.0f, Mathf.Max(roamSpeed, walkSpeed), t);

            Vector2 desired = forward * targetSpeed;
            HandlePathStuck(targetSpeed);
            _vel = Vector2.MoveTowards(_vel, desired, Mathf.Max(roamAccel, accel) * dt);

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * dt);

            if (_vel.sqrMagnitude > 0.0001f) _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f) _lastMoveTime = Time.time;

            // face movement
            PushAnimatorByForward(_vel);
        }

        void OnReturnedHome()
        {
            ClearPath();

            switch (startingBehaviour)
            {
                case DefaultBehaviour.FreeRoam:
                    ResetFreeRoamState(startPaused: true);
                    break;
                case DefaultBehaviour.LocalRoam:
                    _roamPause = true;
                    BeginLocalRoamPhase(true);
                    break;
                case DefaultBehaviour.Patrol:
                    ResetPatrolState(startPaused: true);
                    break;
                case DefaultBehaviour.HoldPosition:
                    FreezeMotion();
                    break;
            }
        }


        #endregion

        #region Melee

        void TickMelee(float centerDist, Vector2 dir, Vector2 right)
        {
            float edgeDist = GetEdgeDistance(centerDist);

            Vector2 targetPos = _currentTarget ? (Vector2)_currentTarget.position : (Vector2)transform.position;
            Vector2 navForward = GetPathDirection(targetPos, 0.35f, 0.35f, 0.18f);
            if (navForward.sqrMagnitude < 0.0001f) navForward = dir;

            float chaseEnter = Mathf.Max(0f, chaseOverrideEnterEdge);
            float chaseExit = Mathf.Max(0f, chaseOverrideExitEdge);
            if (chaseEnter > 0f)
            {
                if (chaseExit >= chaseEnter)
                    chaseExit = Mathf.Max(0f, chaseEnter * 0.5f);

                if (!_chaseOverrideActive && edgeDist >= chaseEnter)
                    _chaseOverrideActive = true;
                else if (_chaseOverrideActive && edgeDist <= chaseExit)
                    _chaseOverrideActive = false;
            }
            else
            {
                _chaseOverrideActive = false;
            }

            float time = Time.time;
            float noiseA = (Mathf.PerlinNoise(_noiseSeedA, time * noiseFreq) - 0.5f) * 2f;
            float noiseB = (Mathf.PerlinNoise(_noiseSeedB, time * noiseFreq * 1.17f) - 0.5f) * 2f;

            float sinceLast = time - _lastAttackAt;
            float retreatFactor = retreatCurve.Evaluate(Mathf.Max(0f, sinceLast));
            float desire = attackCurve.Evaluate(edgeDist);
            float bias = Mathf.Lerp(-0.08f, 0.08f, Mathf.Abs(Mathf.Sin(_noiseSeedA)));
            desire = Mathf.Clamp01(desire + bias + noiseA * 0.05f);
            desire *= Mathf.Clamp01(1f - 0.75f * retreatFactor);

            bool room = HaveTicketOrAvailable();
            bool want = (desire > attackThreshold) && room;

            if (want && !_holdingTicket)
            {
                AcquireTicket();
                _lungeBeganAt = Time.time;
            }
            else if (!want && !_attacking)
            {
                ReleaseTicket();
            }

            ClearContextBuffers();

            if (_chaseOverrideActive)
            {
                Vector2 chaseDir = navForward.sqrMagnitude > 0.0001f ? navForward : dir;
                float weight = contextSeekWeight * Mathf.Max(1f, chaseOverrideSeekMultiplier);
                AddContextInterest(chaseDir, weight);
                AddContextInterest(dir, weight * 0.6f);

                Vector2 sep = ComputeSeparation(transform.position);
                if (sep.sqrMagnitude > 0.0001f)
                    AddContextInterest(sep, sep.magnitude * separationStrength * contextSeparationWeight);
            }
            else
            {
                float approach = approachCurve.Evaluate(edgeDist) * contextSeekWeight;
                AddContextInterest(navForward, approach);
                AddContextInterest(dir, approach * 0.5f);

                float orbit = orbitCurve.Evaluate(Mathf.Abs(preferredRadius - centerDist));
                AddContextInterest(right * _orbitSide, orbit);

                float radialErr = centerDist - preferredRadius;
                if (Mathf.Abs(radialErr) > 0.01f)
                {
                    Vector2 correction = radialErr > 0f ? navForward : -navForward;
                    float weight = Mathf.Clamp01(Mathf.Abs(radialErr) / Mathf.Max(0.1f, preferredRadius));
                    AddContextInterest(correction, weight * contextRangeWeight);
                }

                if (allowSideFlip && UnityRandom.value < sideFlipChance)
                    _orbitSide *= -1f;

                Vector2 wander = new Vector2(noiseA, noiseB);
                if (wander.sqrMagnitude > 0.0001f)
                    AddContextInterest(wander, contextWanderWeight * noiseAmplitude);

                Vector2 sep = ComputeSeparation(transform.position);
                if (sep.sqrMagnitude > 0.0001f)
                    AddContextInterest(sep, sep.magnitude * separationStrength * contextSeparationWeight);

                if (want)
                {
                    AddContextInterest(navForward, contextSeekWeight * 1.4f);
                    AddContextInterest(dir, contextSeekWeight);
                }
                else
                {
                    AddContextInterest(-dir, retreatFactor * contextRangeWeight);
                    AddContextDanger(dir, Mathf.Lerp(0.45f, 0.05f, desire));
                }
            }

            if (_holdingTicket && !_attacking && (Time.time - _lungeBeganAt) > lungeMaxTime)
                ReleaseTicket();

            InjectObstacleDanger(transform.position);

            Vector2 contextMove = ResolveContextVector(out float strength);
            float moveScale = Mathf.Clamp01(strength + 0.25f);
            Vector2 desiredVel = contextMove.sqrMagnitude > 0.0001f
                ? contextMove.normalized * Mathf.Lerp(walkSpeed, runSpeed, moveScale) * moveScale
                : Vector2.zero;

            if (!_attacking)
            {
                Vector2 fallbackForward = navForward.sqrMagnitude > 0.0001f ? navForward : dir;
                MaybeKickStuckMovement(ref desiredVel, fallbackForward, edgeDist - stuckKickDistance);
            }

            HandlePathStuck(desiredVel.magnitude);
            _vel = Vector2.MoveTowards(_vel, desiredVel, accel * Time.deltaTime);
            if (drag > 0f) _vel *= Mathf.Clamp01(1f - drag * Time.deltaTime);
            if (_attacking) _vel = Vector2.zero;

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * Time.deltaTime);

            if (_vel.sqrMagnitude > 0.0001f)
                _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f)
                _lastMoveTime = Time.time;

            if (!_attacking && _holdingTicket)
            {
                Vector2 moveForward = _vel.sqrMagnitude > 0.01f ? _vel.normalized : right;
                float facing = Vector2.Dot(moveForward, dir);
                if (edgeDist <= attackEnterEdge && facing > -0.25f)
                    BeginAttackRoutine(DoMeleeAttack());
            }

            PushAnimator(dir, _vel);
            if (debugLog) Debug.Log($"[{name}] MELEE edge={edgeDist:F2} v={_vel.magnitude:F2} ctx={strength:F2} ticket={_holdingTicket} atk={_attacking}");
        }

        IEnumerator DoMeleeAttack()
        {
            _attacking = true;
            _vel = Vector2.zero;
            if (_rb) _rb.linearVelocity = Vector2.zero;

            if (meleeAttackTriggers != null && meleeAttackTriggers.Length > 0)
            {
                string trig = meleeAttackTriggers[UnityRandom.Range(0, meleeAttackTriggers.Length)];
                _anim.SetTrigger(trig);
            }

            yield return new WaitForSeconds(attackHitDelay);

            float end = Time.time + attackHitWindow;
            while (Time.time < end)
            {
                if (!_currentTarget)
                    break;

                float center = Vector2.Distance(transform.position, _currentTarget.position);
                if (GetEdgeDistance(center) <= attackHitEdge)
                    TryDoMeleeHit();
                yield return null;
            }

            _lastAttackAt = Time.time + UnityRandom.Range(attackCooldownJitter.x, attackCooldownJitter.y);
            _attacking = false;
            ReleaseTicket();
            _attackRoutine = null;
        }

        bool TryDoMeleeHit()
        {
            Vector2 hitCenter = (Vector2)transform.position + attackHitOffset;
            var hits = Physics2D.OverlapCircleAll(hitCenter, attackHitRadius, playerMask | neutralNpcMask);
            bool hit = false;
            foreach (var h in hits)
            {
                var dmg = h.GetComponentInParent<EnemyAI.IDamageable>();
                if (dmg != null)
                {
                    hit |= ApplyMeleeDamage(dmg, h.transform.position);
                }
            }

            if (!hit && _currentTarget)
            {
                var dmg = _currentTarget.GetComponentInParent<EnemyAI.IDamageable>();
                if (dmg != null)
                {
                    hit |= ApplyMeleeDamage(dmg, _currentTarget.position);
                }
            }

            return hit;
        }

        bool ApplyMeleeDamage(EnemyAI.IDamageable damageable, Vector3 hitPosition)
        {
            if (damageable == null) return false;

            Vector2 pushDir = ((Vector2)hitPosition - (Vector2)transform.position).normalized;

            if (damageable is PlayerHealth playerHealth)
            {
                playerHealth.TakeDamage(CurrentMeleeDamage, pushDir, transform);
            }
            else
            {
                damageable.TakeDamage(CurrentMeleeDamage, pushDir);
            }

            return true;
        }

        #endregion

        #region Ranged

        void TickRanged(float centerDist, Vector2 dir, Vector2 right)
        {
            float min = rangedMinRange;
            float max = rangedMaxRange;
            float minIn = min - rangedBandHysteresis;
            float maxOut = max + rangedBandHysteresis;

            Vector2 targetPos = _currentTarget ? (Vector2)_currentTarget.position : (Vector2)transform.position;
            Vector2 navForward = GetPathDirection(targetPos, 0.35f, 0.35f, 0.18f);
            if (navForward.sqrMagnitude < 0.0001f) navForward = dir;

            float time = Time.time;
            float noiseA = (Mathf.PerlinNoise(_noiseSeedA, time * noiseFreq) - 0.5f) * 2f;
            float noiseB = (Mathf.PerlinNoise(_noiseSeedB, time * noiseFreq * 1.23f) - 0.5f) * 2f;

            ClearContextBuffers();

            AddContextInterest(navForward, contextSeekWeight * 0.5f);

            bool targetBeyondMax = centerDist > max;
            bool shouldPrioritizeChase = targetBeyondMax && !AnyTrackedTargetWithinRange(max, _currentTarget);

            if (shouldPrioritizeChase)
            {
                Vector2 chaseDir = navForward.sqrMagnitude > 0.0001f ? navForward : dir;
                float chaseWeight = contextSeekWeight * Mathf.Max(1f, chaseOverrideSeekMultiplier);
                AddContextInterest(chaseDir, chaseWeight);
                AddContextInterest(dir, chaseWeight * 0.6f);
            }
            else if (centerDist < minIn)
            {
                float push = Mathf.Clamp01((minIn - centerDist) / Mathf.Max(0.1f, minIn));
                AddContextInterest(-navForward, push * contextRangeWeight * 1.4f);
                AddContextInterest(-dir, push * contextSeekWeight * 0.6f);
                AddContextDanger(dir, 0.45f);
            }
            else if (centerDist > maxOut)
            {
                float pull = Mathf.Clamp01((centerDist - maxOut) / Mathf.Max(0.1f, maxOut));
                AddContextInterest(navForward, pull * contextRangeWeight * 1.2f);
                AddContextInterest(dir, pull * contextSeekWeight);
            }
            else
            {
                AddContextInterest(right * _orbitSide, contextSeekWeight * 0.9f);
                if (allowSideFlip && UnityRandom.value < sideFlipChance)
                    _orbitSide *= -1f;
            }

            Vector2 wander = new Vector2(noiseA, noiseB);
            if (wander.sqrMagnitude > 0.0001f)
                AddContextInterest(wander, contextWanderWeight * noiseAmplitude);

            Vector2 sep = ComputeSeparation(transform.position);
            if (sep.sqrMagnitude > 0.0001f)
                AddContextInterest(sep, sep.magnitude * separationStrength * contextSeparationWeight);

            InjectObstacleDanger(transform.position);

            if (!_attacking || !stopToFire)
            {
                Vector2 contextMove = ResolveContextVector(out float strength);
                float moveScale = Mathf.Clamp01(strength + 0.2f);
                Vector2 desiredVel = contextMove.sqrMagnitude > 0.0001f
                    ? contextMove.normalized * Mathf.Lerp(walkSpeed, runSpeed, moveScale) * moveScale
                    : Vector2.zero;

                float approachNeed = centerDist > maxOut ? centerDist - maxOut : 0f;
                float retreatNeed = centerDist < minIn ? (minIn - centerDist) : 0f;
                Vector2 fallbackDir = Vector2.zero;
                float requirement = 0f;

                if (approachNeed > retreatNeed && approachNeed > 0f)
                {
                    fallbackDir = navForward.sqrMagnitude > 0.0001f ? navForward : dir;
                    requirement = approachNeed - stuckKickRangeBuffer;
                }
                else if (retreatNeed > 0f)
                {
                    fallbackDir = navForward.sqrMagnitude > 0.0001f ? -navForward : -dir;
                    requirement = retreatNeed - stuckKickRangeBuffer;
                }

                if (shouldPrioritizeChase && fallbackDir.sqrMagnitude < 0.0001f)
                {
                    fallbackDir = navForward.sqrMagnitude > 0.0001f ? navForward : dir;
                    requirement = Mathf.Max(0f, (centerDist - max) - stuckKickRangeBuffer);
                }

                MaybeKickStuckMovement(ref desiredVel, fallbackDir, requirement);

                HandlePathStuck(desiredVel.magnitude);
                _vel = Vector2.MoveTowards(_vel, desiredVel, accel * Time.deltaTime);
                if (drag > 0f) _vel *= Mathf.Clamp01(1f - drag * Time.deltaTime);
            }

            if (_attacking && stopToFire)
            {
                _vel = Vector2.zero;
            }

            if (_rb) _rb.linearVelocity = _vel;
            else     transform.position += (Vector3)(_vel * Time.deltaTime);

            if (_vel.sqrMagnitude > 0.0001f)
                _lastMoveDir = _vel.normalized;
            if (_vel.sqrMagnitude > 0.01f)
                _lastMoveTime = Time.time;

            bool insideBand = centerDist >= min && centerDist <= max;
            if (!_attacking && insideBand && Time.time >= _nextShotAt)
                BeginAttackRoutine(DoRangedShot(dir));

            Vector2 faceDir = _hasAimLock ? _aimLockDir : dir;
            PushAnimator(faceDir, _vel);
            if (debugLog) Debug.Log($"[{name}] RANGED dist={centerDist:F2} v={_vel.magnitude:F2} atk={_attacking}");
        }

        IEnumerator DoRangedShot(Vector2 dirToPlayer)
        {
            _attacking = true;
            _vel = Vector2.zero;
            if (_rb) _rb.linearVelocity = Vector2.zero;

            // trigger shoot anim (optional)
            if (rangedShootTriggers != null && rangedShootTriggers.Length > 0)
            {
                string trig = rangedShootTriggers[UnityRandom.Range(0, rangedShootTriggers.Length)];
                _anim.SetTrigger(trig);
            }

            // -------- LOCK AIM NOW (at wind-up start) --------
            Transform spawn = projectileSpawn ? projectileSpawn : transform;
            Vector2 lockedDir;
            if (_currentTarget)
            {
                Vector2 raw = (Vector2)_currentTarget.position - (Vector2)spawn.position;
                lockedDir = raw.sqrMagnitude > 0.0001f ? raw.normalized : dirToPlayer;
            }
            else
            {
                lockedDir = dirToPlayer;
            }

            _hasAimLock = true;
            _aimLockDir = lockedDir;   // lets TickRanged face this during windup

            // wind-up delay (telegraph time)
            if (shotWindup > 0f) yield return new WaitForSeconds(shotWindup);

            // -------- FIRE using the lockedDir (not re-aiming) --------
            if (projectilePrefab != null)
            {
                float spread = UnityRandom.Range(-projectileSpreadDegrees, projectileSpreadDegrees);
                Vector2 shotDir = (Vector2)(Quaternion.Euler(0, 0, spread) * (Vector3)lockedDir);

                var proj = Instantiate(projectilePrefab, spawn.position, Quaternion.identity);
                proj.Init(shotDir * projectileSpeed, CurrentProjectileDamage, projectileLife, playerMask | neutralNpcMask, transform);
            }

            // cooldown
            _nextShotAt = Time.time + UnityRandom.Range(shotCooldownRange.x, shotCooldownRange.y);

            // small post-shot stall so locomotion doesnâ€™t immediately override â€œshootâ€ look
            yield return new WaitForSeconds(0.05f);

            // clear aim lock
            _hasAimLock = false;

            _attacking = false;
            _attackRoutine = null;
        }


        #endregion

        #region Pathfinding

        void ClearPath()
        {
            _pathBuffer.Clear();
            _pathIndex = 0;
            _pathValid = false;
            _pathDestination = transform.position;
            _pathNextRebuildAt = Time.time;
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
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                return true;
            }

            bool success = TilemapPathfinder.TryFindPath(transform.position, destination, _pathBuffer);
            _pathValid = success && _pathBuffer.Count > 0;
            if (!_pathValid)
            {
                _pathBuffer.Clear();
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                _pendingPathBlock = true;
                _nextPathRetryAt = Time.time + Mathf.Max(0.5f, pathRecheckDelayWhenBlocked);
                return false;
            }
            else
            {
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                _lastForcedRepathAt = Time.time;
                _pendingPathBlock = false;
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

        void HandlePathStuck(float desiredSpeed)
        {
            if (!TilemapPathfinder.HasColliderMap || !_pathValid)
            {
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                return;
            }

            if (desiredSpeed <= 0.01f)
            {
                _lastProgressPos = transform.position;
                _lastProgressTime = Time.time;
                return;
            }

            Vector2 pos = transform.position;
            float sqrMoved = (pos - _lastProgressPos).sqrMagnitude;
            if (sqrMoved >= pathProgressDistance * pathProgressDistance)
            {
                _lastProgressPos = pos;
                _lastProgressTime = Time.time;
                return;
            }

            if (Time.time - _lastProgressTime < pathRepathTime)
                return;

            if (Time.time - _lastForcedRepathAt < 0.3f)
                return;

            if (TilemapPathfinder.HasColliderMap)
            {
                Vector2 blocked = (_pathIndex < _pathBuffer.Count) ? _pathBuffer[_pathIndex] : (Vector2)transform.position;
                TilemapPathfinder.RegisterTemporaryObstacle(blocked, 1.5f);
            }

            ClearPath();
            _lastForcedRepathAt = Time.time;
            _lastProgressPos = pos;
            _lastProgressTime = Time.time;
        }

        void MaybeKickStuckMovement(ref Vector2 desiredVelocity, Vector2 fallbackDir, float distanceRequirement)
        {
            if (stuckKickDelay <= 0f)
                return;
            if (distanceRequirement <= 0f)
                return;
            if (desiredVelocity.sqrMagnitude > 0.01f)
                return;
            if (fallbackDir.sqrMagnitude < 0.0001f)
                return;

            float now = Time.time;
            if (now - _lastMoveTime < stuckKickDelay)
                return;
            if (now - _lastStuckKickAt < Mathf.Max(0.1f, stuckKickCooldown))
                return;

            Vector2 forcedDir = fallbackDir.normalized;
            float speed = Mathf.Max(runSpeed, walkSpeed) * Mathf.Max(0.1f, stuckKickSpeedMultiplier);
            desiredVelocity = forcedDir * speed;
            _lastStuckKickAt = now;
            _lastMoveTime = now;
            ClearPath();
        }

        bool TryShootBlockedPathProjectile(Transform target)
        {
            if (!fireProjectilesWhenBlocked)
                return false;
            if (!projectilePrefab || !target)
                return false;
            if (_mode != Mode.Engage)
                return false;
            if (Time.time < _nextBlockedShotAt)
                return false;
            if (_blockedShotRoutine != null)
                return false;

            _blockedShotRoutine = StartCoroutine(DoBlockedShotRoutine(target));
            return true;
        }

        IEnumerator DoBlockedShotRoutine(Transform target)
        {
            _vel = Vector2.zero;
#if UNITY_2022_2_OR_NEWER
            if (_rb) _rb.linearVelocity = Vector2.zero;
#else
            if (_rb) _rb.velocity = Vector2.zero;
#endif

            if (blockedShotTriggers != null && blockedShotTriggers.Length > 0 && _anim)
            {
                string trig = blockedShotTriggers[UnityRandom.Range(0, blockedShotTriggers.Length)];
                _anim.SetTrigger(trig);
            }

            if (blockedShotWindup > 0f)
                yield return new WaitForSeconds(blockedShotWindup);

            FireBlockedShotNow(target);
            _blockedShotRoutine = null;
        }

        void FireBlockedShotNow(Transform target)
        {
            if (!projectilePrefab || !target)
                return;
            if (_mode != Mode.Engage)
                return;

            Transform spawn = projectileSpawn ? projectileSpawn : transform;
            Vector3 spawnPos = spawn.position;
            Vector2 origin = spawnPos;
            Vector2 toTarget = (Vector2)target.position - origin;
            float distance = toTarget.magnitude;
            if (distance <= 0.05f)
                return;
            float maxRange = Mathf.Max(0.01f, blockedShotRange);
            if (maxRange > 0f && distance > maxRange)
                return;

            Vector2 dir = toTarget / Mathf.Max(0.0001f, distance);
            float spread = projectileSpreadDegrees != 0f
                ? UnityRandom.Range(-projectileSpreadDegrees, projectileSpreadDegrees)
                : 0f;
            Vector2 shotDir = dir;
            if (Mathf.Abs(spread) > 0.001f)
            {
                shotDir = (Vector2)(Quaternion.Euler(0f, 0f, spread) * (Vector3)dir);
            }
            if (shotDir.sqrMagnitude < 0.0001f)
                return;
            shotDir.Normalize();

            var proj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
            proj.Init(shotDir * projectileSpeed, CurrentProjectileDamage, projectileLife, playerMask | neutralNpcMask, transform);

            _nextBlockedShotAt = Time.time + Mathf.Max(0.1f, blockedShotCooldown);
        }

        static Vector2 DirectionTo(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return Vector2.zero;
            return delta.normalized;
        }

        public void EnsureBossHealthBarPanelReference()
        {
            if (!isBoss || bossHealthBarPanel)
                return;

            var panelGo = GameObject.Find("BossHealthBarPanel");
            if (!panelGo)
                return;

            var rect = panelGo.GetComponent<RectTransform>();
            if (rect)
                bossHealthBarPanel = rect;
        }

        #endregion

        #region Damage / Helpers


        IEnumerator DisableAfterDeath()
        {
            yield return new WaitForSeconds(1.2f);
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            if (_rb) _rb.simulated = false;
            enabled = false;
        }

        void FreezeMotion()
        {
            _vel = Vector2.zero;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            _lastMoveTime = Time.time;
        }

        public void SetExternalPause(bool paused)
        {
            if (paused)
            {
                _externallyPaused = true;
                _externallyPausedIndefinite = true;
                _externalPauseResumeAt = -1f;
                FreezeMotion();
            }
            else
            {
                _externallyPaused = false;
                _externallyPausedIndefinite = false;
                _externalPauseResumeAt = -1f;
            }
        }

        public void PauseForSeconds(float seconds)
        {
            if (_externallyPausedIndefinite)
                return;

            _externallyPaused = true;
            _externallyPausedIndefinite = false;
            float resumeAt = Time.time + Mathf.Max(0f, seconds);
            if (_externalPauseResumeAt < resumeAt)
                _externalPauseResumeAt = resumeAt;
            FreezeMotion();
        }

        void BeginAttackRoutine(IEnumerator routine)
        {
            if (routine == null) return;

            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);

            _attackRoutine = StartCoroutine(routine);
        }

        void CancelAttackRoutine()
        {
            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_attacking)
            {
                _attacking = false;
                ReleaseTicket();
            }

            _hasAimLock = false;
            _aimLockDir = Vector2.zero;
            _vel = Vector2.zero;
            if (_rb) _rb.linearVelocity = Vector2.zero;
        }

        public void OnHit(Vector2 hitDir)
        {
            CancelAttackRoutine();
            if (_abilityRunner && _abilityRunner.isActiveAndEnabled)
            {
                _abilityRunner.CancelAllAbilities();
            }

            _lastAttackAt = Time.time;
            _lungeBeganAt = Time.time;

            float stunEnd = Time.time + Mathf.Max(0f, hitStun);
            _stunUntil = Mathf.Max(_stunUntil, stunEnd);
        }

        float GetEdgeDistance(float centerDist)
        {
            float me = WorldRadius(_myCircle);
            float other = WorldRadius(_currentTargetCollider);
            return Mathf.Max(0f, centerDist - (me + other));
        }

        static float WorldRadius(Collider2D c)
        {
            if (!c) return 0.4f;
            if (c is CircleCollider2D cc)
            {
                var s = cc.transform.lossyScale;
                float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
                return cc.radius * scale;
            }

            return c.bounds.extents.magnitude;
        }

        Vector2 ComputeSeparation(Vector2 myPos)
        {
            if (enemyMask.value == 0 || separationStrength <= 0f) return Vector2.zero;

            const int MAX = 12;
            Collider2D[] buf = new Collider2D[MAX];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(myPos, separationRadius, filter, buf);
            Vector2 force = Vector2.zero; int n = 0;
            for (int i = 0; i < count; i++)
            {
                var c = buf[i];
                if (c == null || c.attachedRigidbody == _rb) continue;
                if (c.transform == transform || c.transform.IsChildOf(transform)) continue;

                Vector2 d = (Vector2)transform.position - (Vector2)c.transform.position;
                float m = d.magnitude;
                if (m > 0.001f)
                {
                    force += d.normalized * (1f / m);
                    n++;
                }
            }
            if (n > 0) force = (force / n);
            return force;
        }

        // Face player (forwardToPlayer) during ENGAGE; roaming/return uses movement-facing
        void PushAnimator(Vector2 forwardToPlayer, Vector2 currentVel)
        {
            Vector2 forward = forwardToPlayer; // face player
            Vector2 moveDir = currentVel.sqrMagnitude > 0.0001f ? currentVel.normalized : Vector2.zero;
            Vector2 right = new Vector2(forward.y, -forward.x);

            int dirIdx = DirIndex(forward);
            _anim.SetFloat(H_Direction, dirIdx);
            _anim.SetInteger(H_DirIndex, dirIdx);

            isMoving = currentVel.sqrMagnitude > 0.01f;
            float dotF = Vector2.Dot(moveDir, forward);
            float dotB = Vector2.Dot(moveDir, -forward);
            float dotR = Vector2.Dot(moveDir, right);
            float dotL = Vector2.Dot(moveDir, -right);

            bool run = isMoving && dotF > 0.5f && currentVel.magnitude > (runSpeed * 0.6f);
            bool walk = isMoving && dotF > 0.5f && currentVel.magnitude <= (runSpeed * 0.6f);

            bool strafeR = isMoving && (dotR > strafeDotThreshold);
            bool strafeL = isMoving && (dotL > strafeDotThreshold);

            bool block = _attacking || Time.time < _stunUntil;

            _anim.SetBool(H_IsRun,          block ? false : run);
            _anim.SetBool(H_IsWalk,         block ? false : walk);
            _anim.SetBool(H_IsRunBackwards, block ? false : (isMoving && dotB > 0.5f));
            _anim.SetBool(H_IsStrafeLeft,   block ? false : strafeL);
            _anim.SetBool(H_IsStrafeRight,  block ? false : strafeR);
        }

        // Used outside Engage (Roam/ReturnHome)
        void PushAnimatorByForward(Vector2 currentVel)
        {
            Vector2 moveDir = currentVel.sqrMagnitude > 0.0001f ? currentVel.normalized : Vector2.right;
            Vector2 forward = moveDir; // face where we're going
            Vector2 right = new Vector2(forward.y, -forward.x);

            int dirIdx = DirIndex(forward);
            _anim.SetFloat(H_Direction, dirIdx);
            _anim.SetInteger(H_DirIndex, dirIdx);

            isMoving = currentVel.sqrMagnitude > 0.01f;
            float dotF = Vector2.Dot(moveDir, forward);
            float dotB = Vector2.Dot(moveDir, -forward);
            float dotR = Vector2.Dot(moveDir, right);
            float dotL = Vector2.Dot(moveDir, -right);
            const float TH = 0.5f;

            _anim.SetBool(H_IsRun,          false);
            _anim.SetBool(H_IsWalk,         isMoving && dotF > TH);
            _anim.SetBool(H_IsRunBackwards, isMoving && dotB > TH);
            _anim.SetBool(H_IsStrafeLeft,   isMoving && dotL > TH);
            _anim.SetBool(H_IsStrafeRight,  isMoving && dotR > TH);
        }

        void PushAnimatorIdleFacingPlayer()
        {
            Transform target = _currentTarget ? _currentTarget : player;
            if (!target) { PushAnimatorIdleFacingForward(); return; }
            Vector2 fp = ((Vector2)target.position - (Vector2)transform.position).normalized;
            PushAnimator(fp, Vector2.zero);
        }

        void PushAnimatorIdleFacingForward()
        {
            Vector2 forward = Vector2.right;
            PushAnimator(forward, Vector2.zero);
        }

        int DirIndex(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return 0;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0) ang += 360f;

            if (ang >= 337.5f || ang < 22.5f)   return 0; // E
            if (ang < 67.5f)                    return 4; // NE
            if (ang < 112.5f)                   return 3; // N
            if (ang < 157.5f)                   return 5; // NW
            if (ang < 202.5f)                   return 1; // W
            if (ang < 247.5f)                   return 7; // SW
            if (ang < 292.5f)                   return 2; // S
            return 6; // SE
        }

        bool HaveTicketOrAvailable()
        {
            if (_holdingTicket)
            {
                if (_ticketTarget && _ticketTarget == _currentTarget)
                    return true;

                ReleaseTicket();
            }

            if (!_currentTarget) return false;
            int c = ActiveAttackers.TryGetValue(_currentTarget, out var v) ? v : 0;
            return c < Mathf.Max(1, maxSimultaneousAttackers);
        }
        void AcquireTicket()
        {
            if (_holdingTicket || !_currentTarget) return;
            int c = ActiveAttackers.TryGetValue(_currentTarget, out var v) ? v : 0;
            ActiveAttackers[_currentTarget] = c + 1;
            _holdingTicket = true;
            _ticketTarget = _currentTarget;
        }
        void ReleaseTicket()
        {
            if (!_holdingTicket) return;

            Transform target = _ticketTarget ? _ticketTarget : _currentTarget;
            if (!target)
            {
                _holdingTicket = false;
                _ticketTarget = null;
                return;
            }

            int c = ActiveAttackers.TryGetValue(target, out var v) ? v : 0;
            c = Mathf.Max(0, c - 1);
            if (c == 0) ActiveAttackers.Remove(target);
            else ActiveAttackers[target] = c;
            _holdingTicket = false;
            _ticketTarget = null;
        }

        #endregion

        #region Gizmos

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            // Home + roam area
            Vector3 home = spawnPoint ? spawnPoint.position : transform.position;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
            float roamRange = Mathf.Max(roamDistance.x, roamDistance.y);
            roamRange = Mathf.Max(0f, roamRange);
            float gizmoRadius = startingBehaviour == DefaultBehaviour.LocalRoam ? localRoamRadius : roamRange;
            Gizmos.DrawWireSphere(home, gizmoRadius);
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
            Gizmos.DrawSphere(home, 0.07f);

            if (startingBehaviour == DefaultBehaviour.HoldPosition)
            {
                Vector3 hold = home + (Vector3)holdOffset;
                Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.85f);
                Gizmos.DrawSphere(hold, 0.05f);
                Gizmos.DrawLine(home, hold);
            }

            if (startingBehaviour == DefaultBehaviour.Patrol && patrolWaypoints != null && patrolWaypoints.Length > 0)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
                Vector3 first = Vector3.zero;
                Vector3 last = Vector3.zero;
                bool haveFirst = false;
                bool haveLast = false;
                for (int i = 0; i < patrolWaypoints.Length; i++)
                {
                    var wp = patrolWaypoints[i];
                    if (!wp) continue;
                    Vector3 pos = wp.position;
                    Gizmos.DrawSphere(pos, 0.05f);
                    if (haveLast)
                        Gizmos.DrawLine(last, pos);
                    else if (!haveFirst)
                    {
                        first = pos;
                        haveFirst = true;
                    }

                    last = pos;
                    haveLast = true;
                }

                if (patrolMode == PatrolMode.Loop && haveFirst && haveLast && (first - last).sqrMagnitude > 0.0001f)
                {
                    Gizmos.DrawLine(last, first);
                }
            }

            // Engage + Leash
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, engageRadius);

            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, leashRadius);

            if (type == AIType.Melee)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, preferredRadius);

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position + (Vector3)attackHitOffset, attackHitRadius);
            }
            else
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(transform.position, rangedMinRange);
                Gizmos.DrawWireSphere(transform.position, rangedMaxRange);

                // projectile spawn marker
                if (projectileSpawn)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawSphere(projectileSpawn.position, 0.06f);
                }
            }
        }

        // === paste this inside EnemyAI (same namespace/class) ===
        public void OnAllyAlerted(Vector2 sourcePos, bool mayChain)
        {

            TryEnsurePlayerRef();
            if (player)
            {
                // Wake up & engage immediately
                SetMode(Mode.Engage);

                // small nudge so they feel responsive the moment theyâ€™re alerted
                Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    Vector2 dir = toPlayer.normalized;
                    Vector2 desired = dir * Mathf.Max(walkSpeed, roamSpeed);
                    _vel = Vector2.MoveTowards(_vel, desired, accel * Time.deltaTime * 2f);
                    if (_rb) _rb.linearVelocity = _vel;
                }
            }
        }

        // If you also removed this helper earlier, include it too:
        void TryEnsurePlayerRef()
        {
            if (!player)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p) player = p.transform;
            }
        }

        void OnPlayerDiedHandler()
        {
            _playerIsDeadSignal = true;
            _attacking = false;
            ReleaseTicket();
            FreezeMotion();
            // Immediately switch to return-home/roam logic
            if (returnToPost && _hasHome)
                SetMode(Mode.ReturnHome);
            else
                SetMode(Mode.Roam);
        }

        void OnPlayerRespawnedHandler()
        {
            _playerIsDeadSignal = false;
            // Let normal engage range bring them back in
        }

        // Idle facing in arbitrary direction (no movement flags)
        void PushAnimatorIdleFacing(Vector2 forward)
        {
            if (forward.sqrMagnitude < 0.0001f) forward = _lastMoveDir.sqrMagnitude > 0.0001f ? _lastMoveDir : Vector2.right;
            int dirIdx = DirIndex(forward);
            _anim.SetFloat(H_Direction, dirIdx);
            _anim.SetInteger(H_DirIndex, dirIdx);

            _anim.SetBool(H_IsRun, false);
            _anim.SetBool(H_IsWalk, false);
            _anim.SetBool(H_IsRunBackwards, false);
            _anim.SetBool(H_IsStrafeLeft, false);
            _anim.SetBool(H_IsStrafeRight, false);
        }


        #endregion
    }
}








