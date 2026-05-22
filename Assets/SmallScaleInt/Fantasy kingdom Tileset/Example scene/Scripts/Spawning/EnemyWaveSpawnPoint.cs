using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;

namespace SmallScaleInc.CharacterCreatorFantasy
{
    /// <summary>
    /// Specialized spawner that focuses on sequential wave/horde encounters.
    /// Drop it in the scene, configure the waves, and the system handles the rest.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyWaveSpawnPoint : MonoBehaviour
    {
        [System.Serializable]
        public class WaveDefinition
        {
            [Tooltip("Optional label to make inspector lists easier to navigate.")]
            public string waveName = "Wave";
            [Min(1)] public int enemyCount = 5;
            [Tooltip("Delay before this wave begins (after the previous wave finished).")]
            public float delayBeforeWave = 0f;
            [Tooltip("Delay between individual spawns in this wave.")]
            public float spawnInterval = 0.25f;
            [Tooltip("Allow elite prefabs for this wave.")]
            public bool allowElites = true;
            [Tooltip("Override global elite chance for this wave. Leave negative to use global setting.")]
            [Range(0f, 1f)] public float eliteChanceOverride = -1f;
            [Tooltip("Specific enemy prefabs that always spawn at the start of this wave (bosses, elites, etc.).")]
            public List<GameObject> guaranteedEnemies = new List<GameObject>();
        }

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

        [Header("Spawn Limits")]
        [Tooltip("Clamp on simultaneous living enemies. 0 = unlimited.")]
        public int maxSimultaneousEnemies = 0;

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

        [Header("Wave Settings")]
        [Tooltip("Definitions for each wave. At least one entry is required.")]
        public WaveDefinition[] waves = new WaveDefinition[0];
        [Tooltip("Delay between waves when not waiting for them to be cleared.")]
        public float delayBetweenWaves = 5f;
        [Tooltip("Wait until all living enemies are dead before progressing to the next wave.")]
        public bool waitForWaveClear = true;
        [Tooltip("Loop wave list indefinitely.")]
        public bool loopWaves = false;
        [Tooltip("Extra delay before restarting the wave list when Loop Waves is enabled.")]
        public float loopRestartDelay = 20f;

        [Header("Announcements")]
        [Tooltip("Show floating text when each wave begins.")]
        public bool announceWaves = true;
        [Tooltip("Offset from the spawner position where announcements appear.")]
        public Vector3 announcementOffset = new Vector3(0f, 2.5f, 0f);
        [Tooltip("Format for wave announcements. {0} = wave name, {1} = wave number.")]
        public string waveAnnouncementFormat = "Wave: {0}";
        [Tooltip("Show a final announcement after all waves are defeated (when not looping).")]
        public bool announceCompletion = true;
        [Tooltip("Message displayed when the entire horde is defeated.")]
        public string hordeCompleteMessage = "Horde defeated!";

        [Header("Aggro Options")]
        [Tooltip("Alert spawned enemies so they immediately engage the player.")]
        public bool forceAggroOnSpawn = true;
        [Tooltip("Disable return-to-post for spawned enemies so they remain in combat.")]
        public bool disableReturnHomeOnSpawn = true;

        [Header("Completion Loot")]
        [Tooltip("Loot prefab spawned when the final wave is cleared.")]
        public LootPickup completionLootPrefab;
        [Tooltip("Offset applied to the spawner position when spawning completion loot.")]
        public Vector3 completionLootOffset = Vector3.zero;
        [Tooltip("Scatter applied around the spawn offset for each loot drop.")]
        public Vector2 completionLootScatter = new Vector2(0.45f, 0.45f);
        [Tooltip("Items that always drop when the horde is completed.")]
        public List<GearItem> completionGuaranteedLoot = new List<GearItem>();
        [Tooltip("When enabled, random completion loot is drawn from the local list instead of the shared database.")]
        public bool completionUseLocalRandomPool = false;
        [Tooltip("Optional local pool used when 'Completion Use Local Random Pool' is enabled.")]
        public List<GearItem> completionLocalRandomDrops = new List<GearItem>();
        [Tooltip("Database consulted for random completion loot when not using a local pool.")]
        public GearItemDatabase completionGearDatabase;
        [Tooltip("Base probability that a random completion loot drop occurs.")]
        [Range(0f, 1f)] public float completionBaseDropChance = 0.4f;
        [Tooltip("Multiplier applied to the chance after each successful completion loot drop.")]
        [Range(0f, 1f)] public float completionSubsequentDropChanceMultiplier = 0.35f;
        [Tooltip("Maximum number of random completion loot drops.")]
        [Range(1, 5)] public int completionMaxDropCount = 3;

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
        readonly List<GearItem> _completionEligible = new List<GearItem>(32);
        PlayerInventory _cachedInventory;
        Transform _cachedPlayerTransform;
        bool _activated;
        Coroutine _waveRoutine;
        bool _playerEventsHooked;
        bool _reactivateAfterRespawn;
        bool _hordeComplete;
        bool _completionLootDropped;

        void OnValidate()
        {
            activationRadius = Mathf.Max(0.1f, activationRadius);
            spawnRadius = Mathf.Max(0f, spawnRadius);
            maxSimultaneousEnemies = Mathf.Max(0, maxSimultaneousEnemies);
            if (spawnCountRandomOffset.x > spawnCountRandomOffset.y)
            {
                int swap = spawnCountRandomOffset.x;
                spawnCountRandomOffset.x = spawnCountRandomOffset.y;
                spawnCountRandomOffset.y = swap;
            }

            if (waves != null)
            {
                for (int i = 0; i < waves.Length; i++)
                {
                    if (waves[i] == null) continue;
                    waves[i].enemyCount = Mathf.Max(1, waves[i].enemyCount);
                    waves[i].spawnInterval = Mathf.Max(0f, waves[i].spawnInterval);
                    waves[i].delayBeforeWave = Mathf.Max(0f, waves[i].delayBeforeWave);
                    if (waves[i].eliteChanceOverride >= 0f)
                        waves[i].eliteChanceOverride = Mathf.Clamp01(waves[i].eliteChanceOverride);
                }
            }
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
            _hordeComplete = false;
            _completionLootDropped = false;

            if (waves == null || waves.Length == 0)
            {
                if (verboseLogging)
                    Debug.LogWarning("[EnemyWaveSpawnPoint] Activate called but no waves configured.", this);
                return;
            }

            _waveRoutine = StartCoroutine(RunWaveSpawning());
        }

        IEnumerator RunWaveSpawning()
        {
            if (waves == null || waves.Length == 0)
                yield break;

            do
            {
                for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
                {
                    WaveDefinition wave = waves[waveIndex];
                    if (wave == null)
                        continue;

                    if (wave.delayBeforeWave > 0f)
                        yield return new WaitForSeconds(wave.delayBeforeWave);

                    if (verboseLogging)
                        Debug.Log($"[EnemyWaveSpawnPoint] Starting wave {waveIndex + 1}", this);

                    AnnounceWaveStart(waveIndex, wave);
                    yield return SpawnWave(wave);

                    if (waitForWaveClear)
                    {
                        while (GetLivingCount() > 0)
                            yield return null;
                    }
                    else if (delayBetweenWaves > 0f && (waveIndex < waves.Length - 1 || loopWaves))
                    {
                        yield return new WaitForSeconds(delayBetweenWaves);
                    }
                }

                if (loopWaves)
                {
                    if (loopRestartDelay > 0f)
                        yield return new WaitForSeconds(loopRestartDelay);
                }
            }
            while (loopWaves);

            AnnounceHordeComplete();
            _waveRoutine = null;
        }

        IEnumerator SpawnWave(WaveDefinition wave)
        {
            if (wave.guaranteedEnemies != null && wave.guaranteedEnemies.Count > 0)
            {
                for (int i = 0; i < wave.guaranteedEnemies.Count; i++)
                {
                    var prefab = wave.guaranteedEnemies[i];
                    if (!prefab)
                        continue;
                    while (!HasSpawnSlot())
                        yield return null;
                    SpawnPrefabInstance(prefab);
                    if (wave.spawnInterval > 0f)
                        yield return new WaitForSeconds(wave.spawnInterval);
                }
            }

            int targetCount = ResolveSpawnCount(wave.enemyCount);
            int spawned = 0;
            while (spawned < targetCount)
            {
                while (!HasSpawnSlot())
                    yield return null;

                float eliteChanceForWave = wave.eliteChanceOverride >= 0f ? wave.eliteChanceOverride : eliteChance;
                if (SpawnSingle(wave.allowElites, eliteChanceForWave))
                {
                    spawned++;
                }
                else
                {
                    break;
                }

                if (wave.spawnInterval > 0f && spawned < targetCount)
                    yield return new WaitForSeconds(wave.spawnInterval);
            }
        }

        void SpawnBatch(int count, bool allowElites, float eliteChanceOverride)
        {
            for (int i = 0; i < count; i++)
            {
                if (!HasSpawnSlot())
                    break;
                if (!SpawnSingle(allowElites, eliteChanceOverride))
                    break;
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

            Vector2 offset = spawnRadius <= 0f ? Vector2.zero : UnityEngine.Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(offset.x, offset.y, 0f);
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

        GameObject ChoosePrefab(bool allowElites, float eliteChanceOverride)
        {
            bool canSpawnElite = allowElites && allowEliteSpawns && eliteEnemies.Count > 0;
            bool hasRegular = regularEnemies.Count > 0;
            if (!canSpawnElite && !hasRegular)
            {
                Debug.LogWarning("[EnemyWaveSpawnPoint] No enemy prefabs assigned.", this);
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
            _waveRoutine = null;
            ClearLivingEnemies(destroyEnemies);
            _hordeComplete = false;
            _completionLootDropped = false;
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

        void AnnounceWaveStart(int waveIndex, WaveDefinition wave)
        {
            if (!announceWaves)
                return;
            if (!CombatTextManager.Instance)
                return;

            string baseName = !string.IsNullOrWhiteSpace(wave.waveName)
                ? wave.waveName
                : $"Wave {waveIndex + 1}";

            string message;
            if (string.IsNullOrEmpty(waveAnnouncementFormat))
                message = baseName;
            else
            {
                try
                {
                    message = string.Format(waveAnnouncementFormat, baseName, waveIndex + 1);
                }
                catch (FormatException)
                {
                    message = baseName;
                }
            }

            CombatTextManager.Instance.SpawnStatus(message, transform.position + announcementOffset);
        }

        void AnnounceHordeComplete()
        {
            if (loopWaves || _hordeComplete)
                return;

            _hordeComplete = true;

            if (announceCompletion && !string.IsNullOrWhiteSpace(hordeCompleteMessage) && CombatTextManager.Instance)
            {
                CombatTextManager.Instance.SpawnStatus(hordeCompleteMessage, transform.position + announcementOffset);
            }

            TrySpawnCompletionLoot();
        }

        void TrySpawnCompletionLoot()
        {
            if (_completionLootDropped)
                return;
            _completionLootDropped = true;

            if (!completionLootPrefab)
                return;

            if (completionGuaranteedLoot != null)
            {
                for (int i = 0; i < completionGuaranteedLoot.Count; i++)
                {
                    SpawnCompletionLoot(completionGuaranteedLoot[i]);
                }
            }

            IReadOnlyList<GearItem> sourceList = completionUseLocalRandomPool
                ? (completionLocalRandomDrops != null && completionLocalRandomDrops.Count > 0 ? completionLocalRandomDrops : null)
                : (completionGearDatabase != null ? completionGearDatabase.Items : null);

            if (sourceList == null || sourceList.Count == 0)
                return;

            BuildCompletionEligibleList(sourceList, !completionUseLocalRandomPool);
            if (_completionEligible.Count == 0)
                return;

            float chance = Mathf.Clamp01(completionBaseDropChance);
            int dropsSpawned = 0;
            int dropLimit = Mathf.Clamp(completionMaxDropCount, 1, 5);

            while (dropsSpawned < dropLimit && _completionEligible.Count > 0 && chance > 0f)
            {
                if (UnityEngine.Random.value > chance)
                    break;

                GearItem selected = PickCompletionWeightedItem(_completionEligible);
                if (selected == null)
                    break;

                SpawnCompletionLoot(selected);
                _completionEligible.Remove(selected);
                dropsSpawned++;

                chance *= Mathf.Clamp01(completionSubsequentDropChanceMultiplier);
            }
        }

        void SpawnCompletionLoot(GearItem gear)
        {
            if (gear == null || completionLootPrefab == null)
                return;

            Vector3 origin = transform.position + completionLootOffset;
            Vector2 scatter = SampleCompletionScatter();
            Vector3 finalPos = origin + new Vector3(scatter.x, scatter.y, 0f);

            LootPickup pickup = Instantiate(completionLootPrefab, origin, Quaternion.identity);
            pickup.Initialize(gear, ResolvePlayerInventory(), finalPos, ResolvePlayerTransform());
        }

        void BuildCompletionEligibleList(IReadOnlyList<GearItem> source, bool respectAvailabilityFlag)
        {
            _completionEligible.Clear();
            if (source == null)
                return;

            int playerLevel = PlayerExperience.Instance != null ? PlayerExperience.Instance.CurrentLevel : 0;
            for (int i = 0; i < source.Count; i++)
            {
                GearItem gear = source[i];
                if (gear == null)
                    continue;
                if (respectAvailabilityFlag && !gear.CanAppearInRandomDrops)
                    continue;
                if (gear.RandomDropWeight <= 0f)
                    continue;
                if (gear.RequiredLevel > 0 && gear.RequiredLevel > playerLevel)
                    continue;
                _completionEligible.Add(gear);
            }
        }

        GearItem PickCompletionWeightedItem(List<GearItem> pool)
        {
            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Mathf.Max(0f, pool[i].RandomDropWeight);
            }
            if (totalWeight <= 0f)
                return null;

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            for (int i = 0; i < pool.Count; i++)
            {
                GearItem gear = pool[i];
                float weight = Mathf.Max(0f, gear.RandomDropWeight);
                if (weight <= 0f)
                    continue;
                if (roll <= weight)
                    return gear;
                roll -= weight;
            }
            return pool[pool.Count - 1];
        }

        Vector2 SampleCompletionScatter()
        {
            if (completionLootScatter == Vector2.zero)
                return Vector2.zero;
            float x = completionLootScatter.x == 0f ? 0f : UnityEngine.Random.Range(-completionLootScatter.x, completionLootScatter.x);
            float y = completionLootScatter.y == 0f ? 0f : UnityEngine.Random.Range(-completionLootScatter.y, completionLootScatter.y);
            return new Vector2(x, y);
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
                    ai.contextSeekWeight = Mathf.Max(ai.contextSeekWeight, 1.85f);
                    ai.contextRangeWeight = Mathf.Min(ai.contextRangeWeight, 0.25f);
                    ai.contextWanderWeight = 0f;
                    ai.noiseAmplitude = 0f;
                    ai.noiseFreq = 0f;
                    ai.allowSideFlip = false;
                    ai.preferredRadius = Mathf.Min(ai.preferredRadius, 0.75f);
                    ai.attackThreshold = Mathf.Min(ai.attackThreshold, 0.35f);
                    ai.chaseOverrideEnterEdge = Mathf.Min(ai.chaseOverrideEnterEdge, 1.5f);
                    ai.chaseOverrideExitEdge = Mathf.Min(ai.chaseOverrideExitEdge, 0.4f);
                    ai.chaseOverrideSeekMultiplier = Mathf.Max(ai.chaseOverrideSeekMultiplier, 4f);
                    ai.stuckKickSpeedMultiplier = Mathf.Max(ai.stuckKickSpeedMultiplier, 1.4f);
                    ai.stopToFire = false;
                    ai.rangedMinRange = Mathf.Min(ai.rangedMinRange, 2.5f);
                    ai.rangedMaxRange = Mathf.Min(ai.rangedMaxRange, 4f);
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
}




