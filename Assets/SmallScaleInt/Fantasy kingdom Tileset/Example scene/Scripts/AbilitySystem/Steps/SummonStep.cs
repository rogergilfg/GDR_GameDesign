using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Spawns allied units from a prefab list, respecting spawn limits, placement rules, and optional sequential delays.")]
    [MovedFrom("AbilitySystem")]
    public sealed class SummonStep : AbilityStep
    {
        [Header("Summon Prefabs")]
        [SerializeField]
        [Tooltip("Candidate prefabs spawned by this step. Null entries are ignored.")]
        private List<GameObject> summonPrefabs = new List<GameObject>();

        [Header("Spawn Count")]
        [SerializeField]
        [Tooltip("Inclusive range for how many prefabs are spawned per execution.")]
        private Vector2Int spawnCountRange = new Vector2Int(1, 1);

        [SerializeField]
        [Tooltip("When true the step will use a smaller count if spawn limits prevent the desired amount.")]
        private bool allowPartialSummonsWhenCapped = true;

        [SerializeField]
        [Tooltip("Spawn enemies sequentially with a delay instead of all at once.")]
        private bool spawnSequentially = true;

        [SerializeField]
        [Tooltip("Delay between sequential spawns.")]
        private float delayBetweenSpawns = 0.25f;

        [Header("Limits")]
        [SerializeField]
        [Tooltip("Maximum number of summons that may remain active at once. <= 0 for unlimited.")]
        private int maxActiveSummons = 4;

        [SerializeField]
        [Tooltip("Total number of summons allowed over the lifetime of the ability. <= 0 for unlimited.")]
        private int totalSummonLimit = 0;

        [SerializeField]
        [Tooltip("When enabled, reaching the max active summons will replace an existing summon instead of failing. The companion with the lowest health is removed (ties break randomly).")]
        private bool replaceWhenFull = false;

        private enum SpawnAnchor
        {
            Caster,
            PlayerTarget,
            Custom,
            MousePosition
        }

        [Header("Anchors")]
        [SerializeField]
        [Tooltip("Where the spawn position should be centred.")]
        private SpawnAnchor anchor = SpawnAnchor.Caster;

        [SerializeField]
        [Tooltip("Optional transform used when Spawn Anchor is Custom.")]
        private Transform customAnchor;

        [SerializeField]
        [Tooltip("Additional offset applied after selecting the anchor.")]
        private Vector2 manualOffset = Vector2.zero;

        [SerializeField]
        [Tooltip("Minimum radial distance from the anchor when sampling a spawn position.")]
        private float minSpawnDistance = 0f;

        [SerializeField]
        [Tooltip("Maximum radial distance from the anchor when sampling a spawn position.")]
        private float maxSpawnDistance = 2.5f;

        [SerializeField]
        [Tooltip("When false the offset uses Preferred Direction instead of a random angle.")]
        private bool randomizeAngle = true;

        [SerializeField]
        [Tooltip("Direction used when Randomize Angle is disabled.")]
        private Vector2 preferredDirection = Vector2.right;

        [SerializeField]
        [Tooltip("Optional parent assigned to spawned prefabs.")]
        private Transform parentOverride;

        [SerializeField]
        [Tooltip("Local Z offset added after positioning the spawn.")]
        private float spawnZOffset = 0f;

        [Header("Spawn Effects")]
        [SerializeField]
        [Tooltip("Optional VFX prefab spawned at each summon location.")]
        private GameObject spawnEffectPrefab;

        [SerializeField]
        [Tooltip("Parent assigned to spawn effects. Leave empty for world space.")]
        private Transform spawnEffectParent;

        [SerializeField]
        [Tooltip("Additional offset applied to the VFX spawn position.")]
        private Vector3 spawnEffectOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("When true the VFX matches the summon rotation. Otherwise the prefab rotation is used.")]
        private bool spawnEffectMatchesSummonRotation = false;

        [SerializeField]
        [Tooltip("Lifetime of the spawned VFX. <= 0 keeps it forever.")]
        private float spawnEffectLifetime = 3f;

        [Header("Placement Checks")]
        [SerializeField]
        [Tooltip("Avoid spawning on colliders using an overlap test.")]
        private bool preventOverlap = false;

        [SerializeField]
        [Tooltip("Radius used for the overlap test.")]
        private float overlapRadius = 0.5f;

        [SerializeField]
        [Tooltip("Physics layers treated as blockers during placement checks.")]
        private LayerMask overlapMask = ~0;

        [SerializeField]
        [Tooltip("Maximum attempts to find a clear position when Prevent Overlap is enabled.")]
        private int maxPlacementAttempts = 6;

        private enum RotationMode
        {
            Prefab,
            Anchor,
            FixedZ,
            RandomZ
        }

        [Header("Rotation")]
        [SerializeField]
        [Tooltip("How the spawned prefab is oriented.")]
        private RotationMode rotationMode = RotationMode.Prefab;

        [SerializeField]
        [Tooltip("Fixed Z rotation used when Rotation Mode is FixedZ.")]
        private float fixedRotationZ = 0f;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Log spawn positions to the console for debugging.")]
        private bool logSpawnPositions = false;

        readonly List<GameObject> _activeSummons = new List<GameObject>();
        readonly Collider2D[] _placementBuffer = new Collider2D[16];
        int _totalSummoned;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            CleanupSummonList();
            if (!EnsurePrefabList()) yield break;

            int requestedCount = UnityEngine.Random.Range(spawnCountRange.x, spawnCountRange.y + 1);
            requestedCount = Mathf.Max(0, requestedCount);

            int capacity = Mathf.Min(GetActiveCapacity(), GetTotalCapacity());
            bool requiresReplacement = false;
            if (capacity <= 0)
            {
                if (replaceWhenFull && maxActiveSummons > 0 && _activeSummons.Count >= maxActiveSummons)
                {
                    requiresReplacement = true;
                }
                else
                {
                    yield break;
                }
            }

            int spawnCount = requiresReplacement ? requestedCount : Mathf.Min(requestedCount, capacity);
            if (spawnCount <= 0) yield break;

            if (!requiresReplacement && spawnCount < requestedCount && !allowPartialSummonsWhenCapped) 
            {
                yield break;
            }

            if (requiresReplacement && maxActiveSummons > 0)
            {
                RemoveSummonsToFit(spawnCount);
            }

            if (spawnSequentially)
            {
                for (int i = 0; i < spawnCount; i++)
                {
                    TrySpawnOne(context);
                    if (i < spawnCount - 1 && delayBetweenSpawns > 0f)
                    {
                        float end = Time.time + delayBetweenSpawns;
                        while (Time.time < end)
                        {
                            if (context.CancelRequested) yield break;
                            yield return null;
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < spawnCount; i++)
                {
                    TrySpawnOne(context);
                }
            }
        }

        bool EnsurePrefabList()
        {
            for (int i = summonPrefabs.Count - 1; i >= 0; i--)
            {
                if (!summonPrefabs[i])
                {
                    summonPrefabs.RemoveAt(i);
                }
            }

            return summonPrefabs.Count > 0;
        }

        void TrySpawnOne(AbilityRuntimeContext context)
        {
            var prefab = summonPrefabs[UnityEngine.Random.Range(0, summonPrefabs.Count)];
            if (!prefab) return;

            Vector3 spawnPos;
            Quaternion rotation;

            if (anchor == SpawnAnchor.MousePosition)
            {
                spawnPos = SampleSpawnPositionFromMouse(context);
                rotation = ResolveRotation(context.Transform);
            }
            else
            {
                Transform anchorTransform = ResolveAnchor(context);
                if (!anchorTransform) anchorTransform = context.Transform;

                spawnPos = SampleSpawnPosition(context, anchorTransform);
                rotation = ResolveRotation(anchorTransform);
            }

            GameObject instance = Object.Instantiate(prefab, spawnPos, rotation);
            if (parentOverride)
            {
                instance.transform.SetParent(parentOverride, true);
            }

            if (!preventOverlap)
            {
                ForceSpawnPosition(instance, spawnPos);
            }

            SpawnSummonEffect(spawnPos, rotation);
            RegisterSummon(instance);
        }

        Transform ResolveAnchor(AbilityRuntimeContext context)
        {
            return anchor switch
            {
                SpawnAnchor.PlayerTarget => context.Target ? context.Target : context.Transform,
                SpawnAnchor.Custom => customAnchor ? customAnchor : context.Transform,
                _ => context.Transform
            };
        }

        Vector3 SampleSpawnPosition(AbilityRuntimeContext context, Transform anchorTransform)
        {
            Vector3 origin = anchorTransform.position;
            Vector2 offset2D = Vector2.zero;
            Vector2 manual = manualOffset;
            int attempts = Mathf.Max(1, preventOverlap ? maxPlacementAttempts : 1);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                offset2D = ComputeOffset(context, anchorTransform);
                Vector2 candidate2D = (Vector2)origin + offset2D + manual;
                Vector2 resolved = preventOverlap ? ResolvePlacement(candidate2D) : candidate2D;
                origin.x = resolved.x;
                origin.y = resolved.y;
                if (!preventOverlap || IsPlacementClear(resolved))
                    break;
            }

            origin.z += spawnZOffset;
            if (logSpawnPositions)
            {
                Debug.Log($"[SummonStep] Spawn at {origin}");
            }
            return origin;
        }

        Vector3 SampleSpawnPositionFromMouse(AbilityRuntimeContext context)
        {
            Camera cam = Camera.main;
            Vector3 origin;

            if (context.ConfirmedTargetPosition.HasValue)
            {
                origin = context.ConfirmedTargetPosition.Value;
            }
            else if (!cam)
            {
                // Fallback to owner position if no camera
                origin = context.Transform ? context.Transform.position : Vector3.zero;
            }
            else
            {
                Vector3 mouseScreenPos = Input.mousePosition;
                // Set Z to match the owner's distance from camera for proper world conversion
                if (context.Transform)
                {
                    mouseScreenPos.z = cam.WorldToScreenPoint(context.Transform.position).z;
                }

                Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);

                // Clamp to max range if WaitForTargetConfirmationStep set one
                if (context.ConfirmedTargetMaxRange.HasValue && context.Transform)
                {
                    Vector2 ownerPos = context.Transform.position;
                    Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);
                    Vector2 direction = (mousePos2D - ownerPos);
                    float distance = direction.magnitude;

                    if (distance > context.ConfirmedTargetMaxRange.Value)
                    {
                        mousePos2D = ownerPos + direction.normalized * context.ConfirmedTargetMaxRange.Value;
                        mouseWorldPos = new Vector3(mousePos2D.x, mousePos2D.y, mouseWorldPos.z);
                    }
                }

                origin = mouseWorldPos;
            }

            // Apply offset logic similar to regular spawn
            Vector2 manual = manualOffset;
            Vector2 desired2D = (Vector2)origin + manual;
            if (maxSpawnDistance > 0f)
            {
                float min = Mathf.Min(minSpawnDistance, maxSpawnDistance);
                float max = Mathf.Max(minSpawnDistance, maxSpawnDistance);
                float distance = max <= 0f ? 0f : UnityEngine.Random.Range(min, max);
                Vector2 direction = randomizeAngle ? UnityEngine.Random.insideUnitCircle.normalized : preferredDirection.normalized;
                desired2D += direction * distance;
            }

            Vector2 resolvedMouse = preventOverlap ? ResolvePlacement(desired2D) : desired2D;
            origin.x = resolvedMouse.x;
            origin.y = resolvedMouse.y;

            origin.z += spawnZOffset;
            if (logSpawnPositions)
            {
                Debug.Log($"[SummonStep] Spawn at mouse position: {origin}");
            }
            return origin;
        }

        Vector2 ComputeOffset(AbilityRuntimeContext context, Transform anchorTransform)
        {
            float min = Mathf.Min(minSpawnDistance, maxSpawnDistance);
            float max = Mathf.Max(minSpawnDistance, maxSpawnDistance);
            float distance = max <= 0f ? 0f : UnityEngine.Random.Range(min, max);

            Vector2 direction = randomizeAngle ? UnityEngine.Random.insideUnitCircle.normalized : GetPreferredDirection(context, anchorTransform);
            return direction * distance;
        }

        Vector2 GetPreferredDirection(AbilityRuntimeContext context, Transform anchorTransform)
        {
            Vector2 dir = preferredDirection;
            if (anchor == SpawnAnchor.PlayerTarget && context.Target)
            {
                dir = (Vector2)context.Target.position - (Vector2)anchorTransform.position;
            }

            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }

            return dir.normalized;
        }

        bool IsPlacementClear(Vector2 position)
        {
            if (!preventOverlap) return true;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(overlapMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(position, overlapRadius, filter, _placementBuffer);
            return count == 0;
        }

        Vector2 ResolvePlacement(Vector2 desired)
        {
            if (!preventOverlap)
                return desired;

            if (IsPlacementClear(desired))
                return desired;

            Vector2 pushed = PushOutOfColliders(desired);
            if (IsPlacementClear(pushed))
                return pushed;

            const int angleSteps = 16;
            float stepDistance = Mathf.Max(0.1f, overlapRadius * 0.75f);
            int rings = Mathf.Max(1, maxPlacementAttempts);

            for (int ring = 1; ring <= rings; ring++)
            {
                float radius = ring * stepDistance;
                for (int i = 0; i < angleSteps; i++)
                {
                    float angle = (Mathf.PI * 2f * i) / angleSteps;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 candidate = desired + dir * radius;
                    if (IsPlacementClear(candidate))
                        return candidate;
                }
            }

            return desired;
        }

        Vector2 PushOutOfColliders(Vector2 desired, Transform ignoreRoot = null)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(overlapMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(desired, overlapRadius, filter, _placementBuffer);
            if (count <= 0)
                return desired;

            Vector2 push = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                var col = _placementBuffer[i];
                if (!col) continue;
                if (ignoreRoot != null && col.transform != null && col.transform.IsChildOf(ignoreRoot))
                {
                    continue;
                }
                Vector2 closest = col.ClosestPoint(desired);
                Vector2 dir = desired - closest;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    Vector2 centerDir = desired - (Vector2)col.bounds.center;
                    dir = centerDir.sqrMagnitude < 0.0001f ? Vector2.up : centerDir;
                }
                push += dir.normalized;
            }

            if (push.sqrMagnitude < 0.0001f)
                push = Vector2.up;

            float pushDistance = overlapRadius * 1.1f;
            return desired + push.normalized * pushDistance;
        }

        Quaternion ResolveRotation(Transform anchorTransform)
        {
            return rotationMode switch
            {
                RotationMode.Anchor => anchorTransform.rotation,
                RotationMode.FixedZ => Quaternion.Euler(0f, 0f, fixedRotationZ),
                RotationMode.RandomZ => Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f)),
                _ => Quaternion.identity
            };
        }

        void RegisterSummon(GameObject instance)
        {
            if (!instance) return;
            _activeSummons.Add(instance);
            _totalSummoned++;
            SubscribeToDeath(instance);
            CleanupSummonList();
        }

        void ForceSpawnPosition(GameObject instance, Vector3 desiredPosition)
        {
            if (!instance)
            {
                return;
            }

            Transform root = instance.transform;
            root.position = desiredPosition;
            ResolvePostSpawnOverlap(root);
        }

        void ResolvePostSpawnOverlap(Transform spawnTransform)
        {
            if (preventOverlap || overlapRadius <= 0f || spawnTransform == null)
            {
                return;
            }

            Vector2 current = spawnTransform.position;
            Vector2 resolved = PushOutOfColliders(current, spawnTransform);
            if ((resolved - current).sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 adjusted = spawnTransform.position;
            adjusted.x = resolved.x;
            adjusted.y = resolved.y;
            spawnTransform.position = adjusted;
        }

        void SpawnSummonEffect(Vector3 spawnPosition, Quaternion summonRotation)
        {
            if (!spawnEffectPrefab)
            {
                return;
            }

            Quaternion effectRotation = spawnEffectMatchesSummonRotation ? summonRotation : spawnEffectPrefab.transform.rotation;
            Vector3 effectPosition = spawnPosition + spawnEffectOffset;
            Transform effectParent = spawnEffectParent ? spawnEffectParent : null;
            GameObject effectInstance = Object.Instantiate(spawnEffectPrefab, effectPosition, effectRotation, effectParent);
            if (spawnEffectLifetime > 0f)
            {
                Object.Destroy(effectInstance, spawnEffectLifetime);
            }
        }

        void CleanupSummonList()
        {
            for (int i = _activeSummons.Count - 1; i >= 0; i--)
            {
                var summon = _activeSummons[i];
                if (!summon || !summon.activeInHierarchy)
                {
                    _activeSummons.RemoveAt(i);
                }
            }
        }

        int GetActiveCapacity()
        {
            if (maxActiveSummons <= 0) return int.MaxValue;
            return Mathf.Max(0, maxActiveSummons - _activeSummons.Count);
        }

        int GetTotalCapacity()
        {
            if (totalSummonLimit <= 0) return int.MaxValue;
            return Mathf.Max(0, totalSummonLimit - _totalSummoned);
        }

        void SubscribeToDeath(GameObject instance)
        {
            if (!instance) return;

            var enemyHealth = instance.GetComponentInChildren<EnemyHealth2D>();
            if (enemyHealth)
            {
                enemyHealth.OnDied += () => HandleSummonDied(instance);
            }

            var companion = instance.GetComponentInChildren<CompanionHealth>();
            if (companion)
            {
                companion.onDied += () => HandleSummonDied(instance);
            }

            var destroyListener = instance.GetComponent<OnDestroyNotifier>();
            if (!destroyListener)
            {
                destroyListener = instance.AddComponent<OnDestroyNotifier>();
            }
            destroyListener.OnDestroyed += HandleSummonDestroyed;
        }

        void HandleSummonDied(GameObject instance)
        {
            if (!instance) return;
            _activeSummons.Remove(instance);
        }

        void HandleSummonDestroyed(GameObject instance)
        {
            if (!instance) return;
            _activeSummons.Remove(instance);
        }

        void RemoveSummonsToFit(int upcomingSpawns)
        {
            if (maxActiveSummons <= 0) return;
            int requiredSlots = Mathf.Max(0, upcomingSpawns);
            int space = maxActiveSummons - _activeSummons.Count;
            int toRemove = requiredSlots - Mathf.Max(0, space);
            if (toRemove <= 0) return;

            for (int i = 0; i < toRemove; i++)
            {
                GameObject victim = SelectReplacementTarget();
                if (!victim) break;
                DestroyImmediateSafe(victim);
                _activeSummons.Remove(victim);
            }
        }

        GameObject SelectReplacementTarget()
        {
            if (_activeSummons.Count == 0) return null;

            GameObject lowest = null;
            float lowestHealth = float.MaxValue;
            List<GameObject> tied = new List<GameObject>();

            for (int i = 0; i < _activeSummons.Count; i++)
            {
                GameObject summon = _activeSummons[i];
                if (!summon || !summon.activeInHierarchy)
                {
                    lowest = summon;
                    break;
                }

                float health = GetNormalizedHealth(summon);
                if (health < lowestHealth)
                {
                    lowestHealth = health;
                    tied.Clear();
                    tied.Add(summon);
                    lowest = summon;
                }
                else if (Mathf.Approximately(health, lowestHealth))
                {
                    tied.Add(summon);
                }
            }

            if (tied.Count > 1)
            {
                lowest = tied[UnityEngine.Random.Range(0, tied.Count)];
            }

            return lowest;
        }

        static float GetNormalizedHealth(GameObject summon)
        {
            if (!summon) return 0f;

            var companion = summon.GetComponentInChildren<CompanionHealth>();
            if (companion)
            {
                return companion.maxHealth > 0 ? companion.currentHealth / (float)companion.maxHealth : 0f;
            }

            var enemy = summon.GetComponentInChildren<EnemyHealth2D>();
            if (enemy)
            {
                return enemy.MaxHealth > 0 ? enemy.CurrentHealth / (float)enemy.MaxHealth : 0f;
            }

            return 0f;
        }

        static void DestroyImmediateSafe(GameObject obj)
        {
            if (!obj) return;
            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }

        class OnDestroyNotifier : MonoBehaviour
        {
            public System.Action<GameObject> OnDestroyed;
            void OnDestroy()
            {
                OnDestroyed?.Invoke(gameObject);
            }
        }
    }
}










