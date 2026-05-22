using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    /// <summary>
    /// Flexible enemy spawner that handles radius-based spawning plus optional respawns.
    /// Drop it in the scene, assign prefabs, and configure the knobs in the inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySpawnPoint : MonoBehaviour
    {
        [Header("Activation")]
        [Tooltip("Automatically activate the spawner when the scene loads.")]
        public bool autoActivate = true;
        [Tooltip("If true, the spawner waits until the player enters Activation Radius before starting.")]
        public bool requirePlayerInRange = false;
        [Tooltip("Radius used when checking for player proximity activation.")]
        public float activationRadius = 8f;
        [Tooltip("Optional explicit activation target. Defaults to the player.")]
        public Transform activationTarget;

        [Header("Prefabs")]
        [Tooltip("Regular enemy prefabs that can be spawned.")]
        public List<GameObject> regularEnemies = new List<GameObject>();
        [Tooltip("Elite enemy prefabs. Used when elites are allowed/rolled.")]
        public List<GameObject> eliteEnemies = new List<GameObject>();
        [Tooltip("Allow elites to spawn when at least one elite prefab is assigned.")]
        public bool allowEliteSpawns = true;
        [Tooltip("Chance (0-1) that a spawned unit will use an elite prefab.")]
        [Range(0f, 1f)] public float eliteChance = 0.15f;

        [Header("Spawn Layout")]
        [Tooltip("Radius around this transform that enemies can spawn within.")]
        public float spawnRadius = 3f;
        [Tooltip("Color used for gizmo visualization of the spawn radius.")]
        public Color gizmoColor = new Color(1f, 0.55f, 0.1f, 0.25f);
        [Tooltip("Randomize the Y-axis rotation (useful for 3D prefabs).")]
        public bool randomizeRotation = false;
        [Tooltip("When enabled, enemies spawn exactly at the center of the spawner (ignores spawn radius/perimeter settings).")]
        public bool spawnAtCenter = false;
        [Tooltip("When enabled, enemies spawn along the perimeter of the spawn radius instead of anywhere inside it.")]
        public bool perimeterSpawn = false;
        [Tooltip("Maximum attempts to nudge a perimeter spawn point inward if obstructed.")]
        public int perimeterSpawnAdjustmentSteps = 4;
        [Tooltip("Step size (units) used when nudging perimeter spawns inward to avoid obstacles.")]
        public float perimeterSpawnAdjustmentStepSize = 0.35f;
        [Tooltip("Spawn the initial batch sequentially with a delay between each enemy.")]
        public bool sequentialSpawn = false;
        [Tooltip("Delay between sequential spawns when Sequential Spawn is enabled.")]
        public float sequentialSpawnDelay = 0.4f;

        [Header("Initial Spawn / Respawn")]
        [Tooltip("Number of enemies spawned immediately when the spawner activates.")]
        public int initialSpawnCount = 3;
        [Tooltip("Clamp on simultaneous living enemies. 0 = unlimited.")]
        public int maxSimultaneousEnemies = 0;
        [Tooltip("Allow dead enemies to respawn after a delay.")]
        public bool respawnEnemies = false;
        [Tooltip("Random delay between respawns (x <= y).")]
        public Vector2 respawnDelayRange = new Vector2(20f, 30f);

        [Header("Spawn Count Randomization")]
        [Tooltip("When true, each spawn batch picks a random count using the offset range below.")]
        public bool useSpawnCountRandomRange = false;
        [Tooltip("Random offset (min/max) applied to configured counts when random range is enabled. Example: -5 / +5 turns a base of 10 into 5-15.")]
        public Vector2Int spawnCountRandomOffset = new Vector2Int(-5, 5);

        [Header("Engagement Overrides")]
        [Tooltip("When enabled, enemies spawned by this spawner will use a custom engage radius.")]
        public bool overrideEngageRadius = false;
        [Tooltip("Engage radius applied to spawned enemies when overriding. Matches EnemyAI.engageRadius.")]
        [Min(0.1f)] public float engageRadiusOverride = 8f;
        [Tooltip("Draw a gizmo showing the custom engage radius when selected.")]
        public bool drawEngageRadiusGizmo = true;
        [Tooltip("Color used for the engage radius gizmo.")]
        public Color engageRadiusGizmoColor = new Color(0.35f, 0.85f, 0.35f, 0.25f);

        [Header("Enemy Stat Scaling")]
        [Tooltip("When enabled, enemies spawned by this spawner will have their health and damage multiplied by the values below.")]
        public bool applySpawnStatMultipliers = false;
        [Tooltip("Multiplier applied to each spawned enemy's max health.")]
        [Min(0f)] public float enemyHealthMultiplier = 1f;
        [Tooltip("Multiplier applied to each spawned enemy's damage output.")]
        [Min(0f)] public float enemyDamageMultiplier = 1f;
        [Tooltip("When true, scaled enemies start at full health after the multiplier is applied.")]
        public bool fillScaledEnemiesToMaxHealth = true;

        [Header("Behavior Overrides")]
        [Tooltip("Apply custom combat behaviour profiles to enemies spawned by this spawner.")]
        public bool overrideBehaviorSettings = false;
        [Tooltip("Behaviour profile applied when overrides are enabled.")]
        public SpawnedEnemyBehaviour behaviourProfile = SpawnedEnemyBehaviour.Default;

        [Header("Aggro Options")]
        [Tooltip("Alert spawned enemies so they immediately engage the player.")]
        public bool forceAggroOnSpawn = true;
        [Tooltip("Disable return-to-post for spawned enemies so they remain in combat.")]
        public bool disableReturnHomeOnSpawn = true;

        [Header("Debug")]
        public bool drawSpawnRadiusGizmo = true;
        public bool verboseLogging = false;

        class TrackedEnemy
        {
            public EnemyHealth2D Health;
            public EnemyHealth2D Source;
            public System.Action DeathHandler;
        }

        readonly List<TrackedEnemy> _living = new List<TrackedEnemy>();
        PlayerInventory _cachedInventory;
        Transform _cachedPlayerTransform;
        bool _activated;
        bool _playerEventsHooked;
        bool _reactivateAfterRespawn;

        void OnValidate()
        {
            activationRadius = Mathf.Max(0.1f, activationRadius);
            spawnRadius = Mathf.Max(0f, spawnRadius);
            initialSpawnCount = Mathf.Max(0, initialSpawnCount);
            maxSimultaneousEnemies = Mathf.Max(0, maxSimultaneousEnemies);
            respawnDelayRange.x = Mathf.Max(0f, respawnDelayRange.x);
            respawnDelayRange.y = Mathf.Max(respawnDelayRange.x, respawnDelayRange.y);
            if (spawnCountRandomOffset.x > spawnCountRandomOffset.y)
            {
                int swap = spawnCountRandomOffset.x;
                spawnCountRandomOffset.x = spawnCountRandomOffset.y;
                spawnCountRandomOffset.y = swap;
            }
            sequentialSpawnDelay = Mathf.Max(0.01f, sequentialSpawnDelay);
            perimeterSpawnAdjustmentSteps = Mathf.Max(0, perimeterSpawnAdjustmentSteps);
            perimeterSpawnAdjustmentStepSize = Mathf.Max(0.01f, perimeterSpawnAdjustmentStepSize);
        }

        void Start()
        {
            if (autoActivate && !requirePlayerInRange)
                Activate();
        }

        void OnEnable()
        {
            SubscribePlayerEvents();
        }

        void Update()
        {
            CleanupTrackedEnemies();

            if (!_activated)
            {
                if (_reactivateAfterRespawn && (!requirePlayerInRange || IsActivationTargetInRange()))
                {
                    _reactivateAfterRespawn = false;
                    Activate();
                    return;
                }

                if (autoActivate && requirePlayerInRange && IsActivationTargetInRange())
                {
                    Activate();
                }
            }
        }

        void OnDisable()
        {
            ResetSpawnerState(false, true);
            UnsubscribePlayerEvents();
            _reactivateAfterRespawn = false;
        }

        public void Activate()
        {
            if (_activated)
                return;

            _activated = true;
            int count = ResolveSpawnCount(initialSpawnCount);
            if (count > 0)
                SpawnBatch(count, allowEliteSpawns, eliteChance);
        }

        void SpawnBatch(int count, bool allowElites, float eliteChanceOverride)
        {
            if (sequentialSpawn)
            {
                StartCoroutine(SpawnBatchSequential(count, allowElites, eliteChanceOverride));
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (!HasSpawnSlot())
                    break;
                if (!SpawnSingle(allowElites, eliteChanceOverride))
                    break;
            }
        }

        IEnumerator SpawnBatchSequential(int count, bool allowElites, float eliteChanceOverride)
        {
            int spawned = 0;
            WaitForSeconds wait = new WaitForSeconds(sequentialSpawnDelay);

            while (spawned < count && _activated)
            {
                if (HasSpawnSlot() && SpawnSingle(allowElites, eliteChanceOverride))
                {
                    spawned++;
                }

                if (spawned >= count)
                    break;

                yield return wait;
            }
        }

        bool SpawnSingle(bool allowElites, float eliteChanceOverride)
        {
            GameObject prefab = ChoosePrefab(allowElites, eliteChanceOverride);
            return SpawnPrefabInstance(prefab);
        }

        bool SpawnPrefabInstance(GameObject prefab)
        {
            if (!prefab)
                return false;

            Vector3 spawnPos = ResolveSpawnPosition(prefab);
            Quaternion rotation = prefab.transform.rotation;
            if (randomizeRotation)
                rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            GameObject instance = Instantiate(prefab, spawnPos, rotation);
            var health = instance.GetComponentInChildren<EnemyHealth2D>();
            var ai = instance.GetComponentInChildren<EnemyAI>();

            ApplySpawnStatScaling(health, ai);
            ApplyEngageRadiusOverride(ai);
            ApplyBehaviourOverrides(ai);

            if (health)
                RegisterEnemy(health);

            if (ai)
            {
                if (disableReturnHomeOnSpawn)
                    ai.returnToPost = false;
                if (forceAggroOnSpawn)
                    ai.OnAllyAlerted(transform.position, false);
            }

            return true;
        }

        Vector3 ResolveSpawnPosition(GameObject prefab)
        {
            if (spawnAtCenter)
            {
                return transform.position;
            }

            return perimeterSpawn ? ResolvePerimeterSpawnPosition(prefab) : ResolveInteriorSpawnPosition();
        }

        Vector3 ResolveInteriorSpawnPosition()
        {
            if (spawnRadius <= 0f)
            {
                return transform.position;
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
            return transform.position + new Vector3(offset.x, offset.y, 0f);
        }

        Vector3 ResolvePerimeterSpawnPosition(GameObject prefab)
        {
            if (spawnRadius <= Mathf.Epsilon)
            {
                return transform.position;
            }

            Vector2 direction = UnityEngine.Random.insideUnitCircle;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.right;
            }
            direction.Normalize();

            Vector3 target = transform.position + new Vector3(direction.x, direction.y, 0f) * spawnRadius;

            int steps = Mathf.Max(0, perimeterSpawnAdjustmentSteps);
            float stepSize = Mathf.Max(0.01f, perimeterSpawnAdjustmentStepSize);
            Vector3 inward = -new Vector3(direction.x, direction.y, 0f);
            Vector3 position = target;

            for (int i = 0; i <= steps; i++)
            {
                if (!IsObstructed(prefab, position))
                {
                    return position;
                }
                position += inward * stepSize;
            }

            return position;
        }

        bool IsObstructed(GameObject prefab, Vector3 position)
        {
            Collider2D prefabCollider = prefab != null ? prefab.GetComponentInChildren<Collider2D>() : null;
            float radius = 0.25f;

            if (prefabCollider != null)
            {
                Vector3 extents = prefabCollider.bounds.extents;
                radius = Mathf.Max(extents.x, extents.y, 0.25f);
            }

            Collider2D hit = Physics2D.OverlapCircle(position, radius, ~0);
            return hit != null;
        }

        GameObject ChoosePrefab(bool allowElites, float eliteChanceOverride)
        {
            bool canSpawnElite = allowElites && allowEliteSpawns && eliteEnemies.Count > 0;
            bool hasRegular = regularEnemies.Count > 0;
            if (!canSpawnElite && !hasRegular)
            {
                Debug.LogWarning("[EnemySpawnPoint] No enemy prefabs assigned.", this);
                return null;
            }

            bool spawnElite = false;
            if (canSpawnElite)
            {
                float chance = eliteChanceOverride >= 0f ? eliteChanceOverride : eliteChance;
                spawnElite = UnityEngine.Random.value < Mathf.Clamp01(chance);
            }

            if (spawnElite)
            {
                return eliteEnemies[UnityEngine.Random.Range(0, eliteEnemies.Count)];
            }

            if (hasRegular)
                return regularEnemies[UnityEngine.Random.Range(0, regularEnemies.Count)];

            // Elite list existed but chance failed: fallback to the first elite entry.
            return eliteEnemies.Count > 0 ? eliteEnemies[UnityEngine.Random.Range(0, eliteEnemies.Count)] : null;
        }

        void RegisterEnemy(EnemyHealth2D health)
        {
            if (!health)
                return;

            var entry = new TrackedEnemy { Health = health, Source = health };
            System.Action handler = null;
            handler = () =>
            {
                if (entry.Source)
                    entry.Source.OnDied -= handler;
                entry.Health = null;
                entry.Source = null;
                OnTrackedEnemyDied(entry);
            };
            entry.DeathHandler = handler;
            health.OnDied += handler;
            _living.Add(entry);
        }

        void OnTrackedEnemyDied(TrackedEnemy entry)
        {
            _living.Remove(entry);

            bool allowRespawn = respawnEnemies;
            if (!allowRespawn || !_activated)
                return;

            if (!HasSpawnSlot())
                return;

            float delay = UnityEngine.Random.Range(respawnDelayRange.x, respawnDelayRange.y);
            StartCoroutine(RespawnAfterDelay(Mathf.Max(0.1f, delay)));
        }

        IEnumerator RespawnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_activated)
                yield break;

            if (!HasSpawnSlot())
                yield break;

            SpawnSingle(allowEliteSpawns, eliteChance);
        }

        bool HasSpawnSlot()
        {
            if (maxSimultaneousEnemies <= 0)
                return true;
            CleanupTrackedEnemies();
            return GetLivingCount() < maxSimultaneousEnemies;
        }

        int GetLivingCount()
        {
            int count = 0;
            for (int i = 0; i < _living.Count; i++)
            {
                if (_living[i].Health)
                    count++;
            }
            return count;
        }

        void CleanupTrackedEnemies()
        {
            for (int i = _living.Count - 1; i >= 0; i--)
            {
                if (_living[i].Health)
                    continue;
                if (_living[i].DeathHandler != null && _living[i].Source)
                    _living[i].Source.OnDied -= _living[i].DeathHandler;
                _living.RemoveAt(i);
            }
        }

        void ResetSpawnerState(bool destroyEnemies, bool forceDeactivate)
        {
            StopAllCoroutines();
            ClearLivingEnemies(destroyEnemies);
            if (forceDeactivate)
                _activated = false;
        }

        void ClearLivingEnemies(bool destroyEnemies)
        {
            for (int i = 0; i < _living.Count; i++)
            {
                var entry = _living[i];
                if (entry.Source && entry.DeathHandler != null)
                    entry.Source.OnDied -= entry.DeathHandler;
                if (destroyEnemies && entry.Source)
                    Destroy(entry.Source.gameObject);
            }
            _living.Clear();
        }

        void SubscribePlayerEvents()
        {
            if (_playerEventsHooked)
                return;
            PlayerHealth.OnPlayerDied += HandlePlayerDied;
            PlayerHealth.OnPlayerRespawned += HandlePlayerRespawned;
            _playerEventsHooked = true;
        }

        void UnsubscribePlayerEvents()
        {
            if (!_playerEventsHooked)
                return;
            PlayerHealth.OnPlayerDied -= HandlePlayerDied;
            PlayerHealth.OnPlayerRespawned -= HandlePlayerRespawned;
            _playerEventsHooked = false;
        }

        void HandlePlayerDied()
        {
            bool wasActive = _activated;
            ResetSpawnerState(true, true);
            _reactivateAfterRespawn = wasActive;
        }

        void HandlePlayerRespawned()
        {
            if (!_reactivateAfterRespawn)
                return;
            if (!requirePlayerInRange || IsActivationTargetInRange())
            {
                _reactivateAfterRespawn = false;
                Activate();
            }
        }

        Transform ResolveActivationTarget()
        {
            if (activationTarget)
                return activationTarget;
            return PlayerHealth.Instance ? PlayerHealth.Instance.transform : null;
        }

        bool IsActivationTargetInRange()
        {
            Transform target = ResolveActivationTarget();
            if (!target)
                return false;
            return Vector2.Distance(target.position, transform.position) <= activationRadius;
        }


        void ApplySpawnStatScaling(EnemyHealth2D health, EnemyAI ai)
        {
            if (!applySpawnStatMultipliers)
                return;

            if (health && enemyHealthMultiplier > 0f && !Mathf.Approximately(enemyHealthMultiplier, 1f))
            {
                health.ApplyExternalHealthMultiplier(enemyHealthMultiplier, fillScaledEnemiesToMaxHealth);
            }

            if (ai && enemyDamageMultiplier > 0f && !Mathf.Approximately(enemyDamageMultiplier, 1f))
            {
                ai.damageMultiplier *= enemyDamageMultiplier;
            }
        }

        void ApplyEngageRadiusOverride(EnemyAI ai)
        {
            if (!overrideEngageRadius || ai == null)
            {
                return;
            }

            ai.engageRadius = Mathf.Max(0.1f, engageRadiusOverride);
        }

        void ApplyBehaviourOverrides(EnemyAI ai)
        {
            if (!overrideBehaviorSettings || ai == null)
            {
                return;
            }

            switch (behaviourProfile)
            {
                case SpawnedEnemyBehaviour.Aggressive:
                    bool isRanged = ai.projectilePrefab != null;
                    if (isRanged)
                    {
                        break;
                    }

                    ai.maxSimultaneousAttackers = int.MaxValue / 2;
                    ai.attackThreshold = Mathf.Min(ai.attackThreshold, 0.1f);
                    ai.contextSeekWeight = Mathf.Max(ai.contextSeekWeight, 1.85f);
                    ai.contextRangeWeight = Mathf.Min(ai.contextRangeWeight, 0.25f);
                    ai.contextWanderWeight = 0f;
                    ai.noiseAmplitude = 0f;
                    ai.noiseFreq = 0f;
                    ai.allowSideFlip = false;
                    ai.preferredRadius = Mathf.Min(ai.preferredRadius, 0.75f);
                    ai.stopToFire = false;
                    ai.rangedMinRange = Mathf.Min(ai.rangedMinRange, 2.5f);
                    ai.rangedMaxRange = Mathf.Min(ai.rangedMaxRange, 4f);
                    ai.chaseOverrideEnterEdge = Mathf.Min(ai.chaseOverrideEnterEdge, 1.5f);
                    ai.chaseOverrideExitEdge = Mathf.Min(ai.chaseOverrideExitEdge, 0.4f);
                    ai.chaseOverrideSeekMultiplier = Mathf.Max(ai.chaseOverrideSeekMultiplier, 4f);
                    ai.stuckKickSpeedMultiplier = Mathf.Max(ai.stuckKickSpeedMultiplier, 1.4f);
                    break;

                case SpawnedEnemyBehaviour.Hard:
                    ai.contextDirectionCount = Mathf.Max(ai.contextDirectionCount, 24);
                    ai.contextProbeDistance = Mathf.Max(ai.contextProbeDistance, 2.1f);
                    ai.contextProbeRadius = Mathf.Clamp(ai.contextProbeRadius, 0.25f, 0.45f);
                    ai.contextObstacleWeight = Mathf.Max(ai.contextObstacleWeight, 1.4f);
                    ai.contextSeparationWeight = Mathf.Max(ai.contextSeparationWeight, 1.2f);
                    ai.contextRangeWeight = Mathf.Max(ai.contextRangeWeight, 1.25f);
                    ai.contextWanderWeight = Mathf.Max(ai.contextWanderWeight, 0.45f);
                    ai.noiseAmplitude = Mathf.Max(ai.noiseAmplitude, 0.35f);
                    ai.allowSideFlip = true;
                    ai.pathRepathTime = Mathf.Min(ai.pathRepathTime, 0.65f);
                    ai.pathProgressDistance = Mathf.Min(ai.pathProgressDistance, 0.25f);
                    ai.chaseOverrideEnterEdge = Mathf.Max(ai.chaseOverrideEnterEdge, 4f);
                    ai.chaseOverrideExitEdge = Mathf.Max(ai.chaseOverrideExitEdge, 1.5f);
                    ai.chaseOverrideSeekMultiplier = Mathf.Min(ai.chaseOverrideSeekMultiplier, 3f);
                    ai.stuckKickDelay = Mathf.Min(ai.stuckKickDelay, 1f);
                    ai.stuckKickDistance = Mathf.Min(ai.stuckKickDistance, 0.9f);
                    ai.stopToFire = true;
                    float smartMin = Mathf.Max(0.8f, ai.shotCooldownRange.x * 0.75f);
                    float smartMax = Mathf.Max(smartMin + 0.2f, ai.shotCooldownRange.y * 0.85f);
                    ai.shotCooldownRange = new Vector2(smartMin, smartMax);
                    ai.attackHitRadius = Mathf.Max(ai.attackHitRadius, 1.2f);
                    ai.attackHitEdge = Mathf.Max(ai.attackHitEdge, 0.7f);
                    break;

                case SpawnedEnemyBehaviour.Default:
                default:
                    break;
            }
        }

        PlayerInventory ResolvePlayerInventory()
        {
            if (_cachedInventory)
                return _cachedInventory;
            _cachedInventory = FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            return _cachedInventory;
        }

        Transform ResolvePlayerTransform()
        {
            if (_cachedPlayerTransform)
                return _cachedPlayerTransform;

            PlayerInventory inventory = ResolvePlayerInventory();
            if (inventory)
            {
                _cachedPlayerTransform = inventory.transform;
                return _cachedPlayerTransform;
            }

            if (PlayerExperience.Instance)
            {
                _cachedPlayerTransform = PlayerExperience.Instance.transform;
                return _cachedPlayerTransform;
            }

            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj)
                _cachedPlayerTransform = playerObj.transform;

            return _cachedPlayerTransform;
        }

        int ResolveSpawnCount(int baseCount)
        {
            int count = Mathf.Max(0, baseCount);
            if (!useSpawnCountRandomRange)
                return count;

            int minOffset = spawnCountRandomOffset.x;
            int maxOffset = spawnCountRandomOffset.y;
            if (minOffset > maxOffset)
            {
                int tmp = minOffset;
                minOffset = maxOffset;
                maxOffset = tmp;
            }

            int offset = UnityEngine.Random.Range(minOffset, maxOffset + 1);
            count = Mathf.Max(0, count + offset);
            return count;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (drawSpawnRadiusGizmo)
            {
                UnityEditor.Handles.color = gizmoColor;
                UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.forward, spawnRadius);
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, activationRadius);
            }

            if (overrideEngageRadius && drawEngageRadiusGizmo)
            {
                UnityEditor.Handles.color = engageRadiusGizmoColor;
                UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, engageRadiusOverride);
            }
        }
#endif
    }

    public enum SpawnedEnemyBehaviour
    {
        Default,
        Aggressive,
        Hard
    }
}




