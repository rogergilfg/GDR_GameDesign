
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    /// <summary>
    /// Highly configurable projectile launcher that can operate as single-target, AOE, chaining, cone and beam shots.
    /// </summary>
    [System.Serializable]
    [AbilityComponentDescription("Configurable projectile launcher supporting single target, explosions, chaining, cone volleys and beam shots.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ProjectileStep : AbilityStep
    {
        public enum ProjectileMode
        {
            SingleTargetDamage,
            AreaExplosion,
            AreaDamageOverTime,
            ChainShot,
            MultiShotCone,
            MultiShotBeam
        }

        [Header("Mode")]
        [SerializeField]
        private ProjectileMode mode = ProjectileMode.SingleTargetDamage;

        [Header("Projectile")]
        [SerializeField]
        private ProjectileInfo projectile = new ProjectileInfo();

        [SerializeField]
        [Tooltip("Use the player's mouse aim when no desired direction is provided.")]
        private bool useMouseAimForPlayers = true;

        [SerializeField]
        [Tooltip("Fallback direction when no other direction can be resolved.")]
        private Vector2 fallbackDirection = Vector2.right;

        [SerializeField]
        [Tooltip("Local offset applied when spawning the projectile.")]
        private Vector3 muzzleLocalOffset = Vector3.zero;

        [Header("Damage")]
        [SerializeField]
        private ProjectileDamageSettings impactDamage = new ProjectileDamageSettings();

        [Header("Area Explosion")]
        [SerializeField]
        private AreaExplosionSettings areaSettings = new AreaExplosionSettings();

        [Header("Area Damage Over Time")]
        [SerializeField]
        private AreaDotSettings areaDotSettings = new AreaDotSettings();

        [Header("Chain Shot")]
        [SerializeField]
        private ChainSettings chainSettings = new ChainSettings();

        [Header("Multi-shot Cone")]
        [SerializeField]
        private ConeSettings coneSettings = new ConeSettings();

        [Header("Beam")]
        [SerializeField]
        private BeamSettings beamSettings = new BeamSettings();

        [Header("VFX")]
        [SerializeField]
        private bool spawnMuzzleVfx = true;

        [SerializeField]
        private GameObject muzzleVfxPrefab;

        [SerializeField]
        private float muzzleVfxLifetime = 2f;

        [SerializeField]
        private float muzzleVfxScale = 1f;

        [SerializeField]
        [Tooltip("Prefab spawned when the projectile lands. Optional.")]
        private GameObject hitVfxPrefab;

        [SerializeField]
        private float hitVfxLifetime = 2f;

        [SerializeField]
        private float hitVfxScale = 1f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (context == null)
            {
                yield break;
            }

            switch (mode)
            {
                case ProjectileMode.AreaDamageOverTime:
                    yield return ExecuteAreaDotMode(context);
                    break;
                case ProjectileMode.MultiShotBeam:
                    yield return ExecuteBeamMode(context);
                    break;
                case ProjectileMode.MultiShotCone:
                    yield return ExecuteConeMode(context);
                    break;
                case ProjectileMode.AreaExplosion:
                    yield return ExecuteAreaMode(context);
                    break;
                case ProjectileMode.ChainShot:
                    yield return ExecuteChainMode(context);
                    break;
                default:
                    yield return ExecuteSingleShot(context);
                    break;
            }
        }
        IEnumerator ExecuteSingleShot(AbilityRuntimeContext context)
        {
            FireProjectileWithCallback(context, HandleStandardImpact);
            yield break;
        }

        IEnumerator ExecuteAreaMode(AbilityRuntimeContext context)
        {
            FireProjectileWithCallback(context, target =>
            {
                HandleStandardImpact(target);
                HandleAreaExplosion(target, context);
            });
            yield break;
        }

        IEnumerator ExecuteAreaDotMode(AbilityRuntimeContext context)
        {
            FireProjectileWithCallback(context, target =>
            {
                HandleStandardImpact(target);
                HandleAreaDot(target, context);
            });
            yield break;
        }

        IEnumerator ExecuteChainMode(AbilityRuntimeContext context)
        {
            var visited = new HashSet<Transform>();
            FireProjectileWithCallback(context, target =>
            {
                HandleStandardImpact(target);
                if (target)
                {
                    Transform normalized = NormalizeChainTarget(target);
                    if (normalized)
                    {
                        visited.Add(normalized);
                    }
                }
                StartChainFrom(target, context, chainSettings.MaxChains, visited, 0);
            }, chainSettings.OverrideInitialDamage ? chainSettings.ResolveChainDamage(context, 0, impactDamage) : impactDamage.ResolveDamage(context));
            yield break;
        }

        IEnumerator ExecuteConeMode(AbilityRuntimeContext context)
        {
            Projectile2D prefab = ResolveProjectilePrefab(context);
            if (!prefab)
            {
                yield break;
            }

            Transform spawnTransform = ResolveSpawnTransform(context);
            Vector2 basePosition = spawnTransform ? (Vector2)spawnTransform.position : (Vector2)context.Transform.position;
            float baseZ = spawnTransform ? spawnTransform.position.z : (context.Transform ? context.Transform.position.z : 0f);
            float projectileSpeed = ResolveProjectileSpeed(context);
            float projectileLife = ResolveProjectileLife(context);
            LayerMask hitMask = ResolveHitMask(context);
            Vector2 baseDirection = ResolveBaseDirection(context);
            if (baseDirection.sqrMagnitude < 0.0001f)
            {
                baseDirection = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.right;
            }
            baseDirection.Normalize();

            if (spawnMuzzleVfx && muzzleVfxPrefab)
            {
                Vector3 muzzleWorld = ComputeMuzzleWorldPosition(basePosition, baseZ, baseDirection);
                Quaternion fallbackRotation = spawnTransform ? spawnTransform.rotation : (context.Transform ? context.Transform.rotation : Quaternion.identity);
                SpawnMuzzleVfx(muzzleWorld, baseDirection, fallbackRotation);
            }

            int projectileDamage = ResolveProjectileDamage(context);
            int count = Mathf.Max(1, coneSettings.ProjectileCount);

            if (coneSettings.Spread == ConeSettings.SpreadMode.Even || count == 1)
            {
                float totalAngle = coneSettings.ConeAngleDegrees;
                float halfAngle = totalAngle * 0.5f;
                float step = count > 1 ? totalAngle / (count - 1) : 0f;

                for (int i = 0; i < count; i++)
                {
                    float angle = -halfAngle + step * i;
                    Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                    LaunchProjectile(prefab, basePosition, baseZ, dir, projectileSpeed, projectileDamage, projectileLife, hitMask, context.Transform, context, HandleStandardImpact);
                }
            }
            else
            {
                Random.State originalState = Random.state;
                if (coneSettings.RandomSeedOffset != 0)
                {
                    Random.InitState(Time.frameCount + coneSettings.RandomSeedOffset);
                }

                float halfAngle = coneSettings.ConeAngleDegrees * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    float angle = Random.Range(-halfAngle, halfAngle);
                    Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                    LaunchProjectile(prefab, basePosition, baseZ, dir, projectileSpeed, projectileDamage, projectileLife, hitMask, context.Transform, context, HandleStandardImpact);
                }

                if (coneSettings.RandomSeedOffset != 0)
                {
                    Random.state = originalState;
                }
            }

            yield break;
        }

        IEnumerator ExecuteBeamMode(AbilityRuntimeContext context)
        {
            Transform spawnTransform = ResolveSpawnTransform(context);
            Vector2 origin = spawnTransform ? (Vector2)spawnTransform.position : (Vector2)context.Transform.position;
            float baseZ = spawnTransform ? spawnTransform.position.z : (context.Transform ? context.Transform.position.z : 0f);
            Vector2 baseDirection = ResolveBaseDirection(context);
            if (baseDirection.sqrMagnitude < 0.0001f)
            {
                baseDirection = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.right;
            }
            baseDirection.Normalize();

            int beams = Mathf.Max(1, beamSettings.BeamCount);
            float spread = Mathf.Clamp(beamSettings.SpreadDegrees, 0f, 360f);
            float halfSpread = spread * 0.5f;

            for (int i = 0; i < beams; i++)
            {
                float angle = beams == 1 ? 0f : Mathf.Lerp(-halfSpread, halfSpread, i / Mathf.Max(1f, beams - 1f));
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                FireBeam(origin, baseZ, dir, context);
            }

            yield break;
        }

        void FireBeam(Vector2 origin, float baseZ, Vector2 direction, AbilityRuntimeContext context)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            direction.Normalize();

            float radius = Mathf.Max(0.05f, beamSettings.Radius);
            float range = Mathf.Max(0.5f, beamSettings.Range);
            int targetsPerBeam = Mathf.Max(1, beamSettings.MaxTargetsPerBeam);
            LayerMask mask = beamSettings.HitMask;
            int hitsApplied = 0;
            Vector2 start = origin + RotateOffsetToDirection(muzzleLocalOffset, direction);

            if (spawnMuzzleVfx && muzzleVfxPrefab)
            {
                SpawnMuzzleVfx(new Vector3(start.x, start.y, baseZ + muzzleLocalOffset.z), direction, Quaternion.identity);
            }

            var hits = Physics2D.CircleCastAll(start, radius, direction, range, mask);
            if (hits != null && hits.Length > 0)
            {
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                int damage = beamSettings.Damage.ResolveDamage(context);

                for (int i = 0; i < hits.Length && hitsApplied < targetsPerBeam; i++)
                {
                    var hit = hits[i];
                    if (!hit.collider)
                    {
                        continue;
                    }

                    if (AbilityEffectUtility.TryApplyDamage(context, hit.collider, damage, start))
                    {
                        hitsApplied++;
                    }
                }
            }

            if (beamSettings.DamageTiles)
            {
                Vector2 end = start + direction * range;
                AbilityEffectUtility.TryDamageTilesCircle(end, radius, beamSettings.TileDamage, context.Transform);
            }

            if (beamSettings.BeamVfxPrefab)
            {
                Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                Vector3 spawnPos = new Vector3(start.x, start.y, baseZ + muzzleLocalOffset.z);
                GameObject vfx = Object.Instantiate(beamSettings.BeamVfxPrefab, spawnPos, rotation);
                if (beamSettings.BeamVfxLifetime > 0f)
                {
                    Object.Destroy(vfx, beamSettings.BeamVfxLifetime);
                }
            }
        }
        void FireProjectileWithCallback(AbilityRuntimeContext context, System.Action<Transform> onHit, int overrideDamage = -1)
        {
            Projectile2D prefab = ResolveProjectilePrefab(context);
            if (!prefab)
            {
                return;
            }

            Transform spawnTransform = ResolveSpawnTransform(context);
            Vector2 basePosition = spawnTransform ? (Vector2)spawnTransform.position : (Vector2)context.Transform.position;
            float baseZ = spawnTransform ? spawnTransform.position.z : (context.Transform ? context.Transform.position.z : 0f);

            float projectileSpeed = ResolveProjectileSpeed(context);
            float projectileLife = ResolveProjectileLife(context);
            LayerMask mask = ResolveHitMask(context);

            Vector2 direction = ResolveBaseDirection(context);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.right;
            }
            direction.Normalize();

            if (spawnMuzzleVfx && muzzleVfxPrefab)
            {
                Vector3 muzzleWorld = ComputeMuzzleWorldPosition(basePosition, baseZ, direction);
                Quaternion fallbackRotation = spawnTransform ? spawnTransform.rotation : (context.Transform ? context.Transform.rotation : Quaternion.identity);
                SpawnMuzzleVfx(muzzleWorld, direction, fallbackRotation);
            }

            int projectileDamage = overrideDamage >= 0 ? overrideDamage : ResolveProjectileDamage(context);
            Projectile2D projectileInstance = LaunchProjectile(prefab, basePosition, baseZ, direction, projectileSpeed, projectileDamage, projectileLife, mask, context.Transform, context, onHit);
            RegisterTileHitResponses(projectileInstance, context, baseZ);
        }

        Projectile2D LaunchProjectile(Projectile2D prefab, Vector2 basePosition, float baseZ, Vector2 direction, float speed, int damage, float life, LayerMask mask, Transform owner,
            AbilityRuntimeContext context, System.Action<Transform> onHit)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            direction.Normalize();

            Vector2 rotatedOffset = RotateOffsetToDirection(muzzleLocalOffset, direction);
            Vector3 spawnPos3D = new Vector3(basePosition.x + rotatedOffset.x, basePosition.y + rotatedOffset.y, baseZ + muzzleLocalOffset.z);
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

            Projectile2D proj = Object.Instantiate(prefab, spawnPos3D, rotation);
            proj.Init(direction * speed, damage, life, mask, owner);
            ApplyProjectileVisualOverrides(proj);
            RegisterImpactCallback(proj, onHit);
            return proj;
        }

        void RegisterTileHitResponses(Projectile2D projectileInstance, AbilityRuntimeContext context, float baseZ)
        {
            if (!projectileInstance)
            {
                return;
            }

            if (mode != ProjectileMode.AreaExplosion && mode != ProjectileMode.AreaDamageOverTime)
            {
                return;
            }

            bool triggered = false;
            projectileInstance.RegisterTileHitCallback(point =>
            {
                if (triggered)
                {
                    return;
                }
                triggered = true;

                Vector3 spawnPos = new Vector3(point.x, point.y, baseZ + muzzleLocalOffset.z);
                SpawnHitVfxAt(spawnPos);

                switch (mode)
                {
                    case ProjectileMode.AreaExplosion:
                        HandleAreaExplosionAtPosition(point, null, context);
                        break;
                    case ProjectileMode.AreaDamageOverTime:
                        HandleAreaDotAtPosition(point, null, context, spawnPos);
                        break;
                }
            });
        }

        void ApplyProjectileVisualOverrides(Projectile2D instance)
        {
            if (!instance || projectile == null)
            {
                return;
            }

            if (projectile.overrideSpriteColor)
            {
                var spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < spriteRenderers.Length; i++)
                {
                    var sr = spriteRenderers[i];
                    if (!sr)
                    {
                        continue;
                    }
                    sr.color = projectile.spriteOverrideColor;
                }
            }
        }

        void RegisterImpactCallback(Projectile2D projectileInstance, System.Action<Transform> onHit)
        {
            if (!projectileInstance || onHit == null)
            {
                return;
            }

            projectileInstance.RegisterDamageableHitCallback(target =>
            {
                if (target == null)
                {
                    return;
                }

                onHit(target);
            });
        }

        void HandleStandardImpact(Transform target)
        {
            if (!target)
            {
                return;
            }

            SpawnHitVfxAt(target.position);
        }

        void SpawnHitVfxAt(Vector3 position)
        {
            if (!hitVfxPrefab)
            {
                return;
            }

            GameObject vfx = Object.Instantiate(hitVfxPrefab, position, Quaternion.identity);
            if (hitVfxScale != 1f)
            {
                vfx.transform.localScale *= hitVfxScale;
            }
            if (hitVfxLifetime > 0f)
            {
                Object.Destroy(vfx, hitVfxLifetime);
            }
        }

        void HandleAreaExplosion(Transform target, AbilityRuntimeContext context)
        {
            Vector2 center;
            if (target)
            {
                center = target.position;
            }
            else if (context != null && context.Transform)
            {
                center = context.Transform.position;
            }
            else
            {
                center = Vector2.zero;
            }

            HandleAreaExplosionAtPosition(center, target, context);
        }

        void HandleAreaDot(Transform target, AbilityRuntimeContext context)
        {
            Vector2 center;
            float z = 0f;
            if (target)
            {
                center = target.position;
                z = target.position.z;
            }
            else if (context != null && context.Transform)
            {
                center = context.Transform.position;
                z = context.Transform.position.z;
            }
            else
            {
                center = Vector2.zero;
            }

            HandleAreaDotAtPosition(center, target, context, new Vector3(center.x, center.y, z));
        }

        void HandleAreaExplosionAtPosition(Vector2 center, Transform hitTarget, AbilityRuntimeContext context)
        {
            float radius = areaSettings.Radius;
            if (radius <= 0.01f)
            {
                return;
            }

            var hits = Physics2D.OverlapCircleAll(center, radius, areaSettings.TargetMask);
            int splashDamage = areaSettings.ExplosionDamage.ResolveDamage(context);

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (!hit)
                {
                    continue;
                }

                Transform candidate = hit.transform;
                if (!areaSettings.IncludeHitTarget && hitTarget && (candidate == hitTarget || candidate.IsChildOf(hitTarget)))
                {
                    continue;
                }

                AbilityEffectUtility.TryApplyDamage(context, hit, splashDamage, center);
            }

            if (areaSettings.DamageTiles)
            {
                AbilityEffectUtility.TryDamageTilesCircle(center, radius, areaSettings.TileDamage, context.Transform);
            }

            if (areaSettings.ExplosionVfxPrefab)
            {
                GameObject vfx = Object.Instantiate(areaSettings.ExplosionVfxPrefab, center, Quaternion.identity);
                if (areaSettings.ExplosionVfxScale != 1f)
                {
                    vfx.transform.localScale *= areaSettings.ExplosionVfxScale;
                }
                if (areaSettings.ExplosionVfxLifetime > 0f)
                {
                    Object.Destroy(vfx, areaSettings.ExplosionVfxLifetime);
                }
            }
        }

        void HandleAreaDotAtPosition(Vector2 center, Transform hitTarget, AbilityRuntimeContext context, Vector3 spawnPosition)
        {
            areaDotSettings.StartEffect(context, center, hitTarget, spawnPosition);
        }
        void StartChainFrom(Transform source, AbilityRuntimeContext context, int remainingChains, HashSet<Transform> visited, int hopIndex)
        {
            if (remainingChains <= 0 || context?.Runner == null || !source)
            {
                return;
            }

            context.Runner.StartCoroutine(ChainRoutine(source, context, remainingChains, visited, hopIndex));
        }

        IEnumerator ChainRoutine(Transform source, AbilityRuntimeContext context, int remainingChains, HashSet<Transform> visited, int hopIndex)
        {
            if (chainSettings.DelayBetweenChains > 0f)
            {
                yield return new WaitForSeconds(chainSettings.DelayBetweenChains);
            }

            Transform target = FindNextChainTarget(source, context, visited);
            if (!target)
            {
                yield break;
            }

            LaunchChainProjectile(source, target, context, remainingChains - 1, visited, hopIndex + 1);
        }

        Transform FindNextChainTarget(Transform source, AbilityRuntimeContext context, HashSet<Transform> visited)
        {
            if (!source)
            {
                return null;
            }

            float radius = Mathf.Max(0.1f, chainSettings.SearchRadius);
            LayerMask mask = chainSettings.TargetMask.value != 0 ? chainSettings.TargetMask : ResolveHitMask(context);
            var hits = Physics2D.OverlapCircleAll(source.position, radius, mask);
            Transform best = null;
            Transform repeatCandidate = null;
            float bestScore = float.MaxValue;
            float repeatScore = float.MaxValue;
            bool allowRepeat = chainSettings.AllowRepeatTargets;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (!hit)
                {
                    continue;
                }

                Transform candidate = NormalizeChainTarget(hit.transform);
                if (!candidate || candidate == source)
                {
                    continue;
                }

                bool alreadyVisited = visited != null && visited.Contains(candidate);
                if (alreadyVisited && !allowRepeat)
                {
                    continue;
                }

                float sqrDist = (candidate.position - source.position).sqrMagnitude;

                if (!alreadyVisited)
                {
                    if (sqrDist < bestScore)
                    {
                        bestScore = sqrDist;
                        best = candidate;
                    }
                }
                else if (allowRepeat && sqrDist < repeatScore)
                {
                    repeatScore = sqrDist;
                    repeatCandidate = candidate;
                }
            }

            Transform result = best ? best : (allowRepeat ? repeatCandidate : null);

            if (result && visited != null)
            {
                visited.Add(result);
            }

            return result;
        }

        void LaunchChainProjectile(Transform source, Transform target, AbilityRuntimeContext context, int remainingChains, HashSet<Transform> visited, int hopIndex)
        {
            if (!source || !target)
            {
                return;
            }

            Projectile2D prefab = ResolveProjectilePrefab(context);
            if (!prefab)
            {
                return;
            }

            Vector2 start = source.position;
            float baseZ = source.position.z;
            Vector2 direction = ((Vector2)target.position - start).normalized;
            float speed = ResolveProjectileSpeed(context);
            float life = ResolveProjectileLife(context);
            LayerMask mask = ResolveHitMask(context);
            int damage = chainSettings.ResolveChainDamage(context, hopIndex + 1, impactDamage);

            LaunchProjectile(prefab, start, baseZ, direction, speed, damage, life, mask, context.Transform, context, hitTarget =>
            {
                HandleStandardImpact(hitTarget);
                if (visited != null && hitTarget)
                {
                    Transform normalized = NormalizeChainTarget(hitTarget);
                    if (normalized)
                    {
                        visited.Add(normalized);
                    }
                }
                StartChainFrom(hitTarget, context, remainingChains, visited, hopIndex + 1);
            });

            if (chainSettings.ChainVfxPrefab)
            {
                Vector2 midPoint = (start + (Vector2)target.position) * 0.5f;
                Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
                GameObject vfx = Object.Instantiate(chainSettings.ChainVfxPrefab, midPoint, rotation);
                if (chainSettings.ChainVfxLifetime > 0f)
                {
                    Object.Destroy(vfx, chainSettings.ChainVfxLifetime);
                }
            }
        }

        Vector3 ComputeMuzzleWorldPosition(Vector2 basePosition, float baseZ, Vector2 direction)
        {
            Vector2 rotatedOffset = RotateOffsetToDirection(muzzleLocalOffset, direction);
            return new Vector3(basePosition.x + rotatedOffset.x, basePosition.y + rotatedOffset.y, baseZ + muzzleLocalOffset.z);
        }

        void SpawnMuzzleVfx(Vector3 position, Vector2 direction, Quaternion fallbackRotation)
        {
            if (!muzzleVfxPrefab)
            {
                return;
            }

            Quaternion rotation = direction.sqrMagnitude > 0.0001f
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg)
                : fallbackRotation;

            GameObject instance = Object.Instantiate(muzzleVfxPrefab, position, rotation);

            if (!Mathf.Approximately(muzzleVfxScale, 1f))
            {
                instance.transform.localScale *= muzzleVfxScale;
            }

            if (muzzleVfxLifetime > 0f)
            {
                Object.Destroy(instance, muzzleVfxLifetime);
            }
        }
        Projectile2D ResolveProjectilePrefab(AbilityRuntimeContext context)
        {
            if (projectile.prefabOverride)
            {
                return projectile.prefabOverride;
            }

            if (context.EnemyAI && context.EnemyAI.projectilePrefab)
            {
                return context.EnemyAI.projectilePrefab;
            }

            return null;
        }

        Transform ResolveSpawnTransform(AbilityRuntimeContext context)
        {
            if (projectile.spawnOverride)
            {
                return projectile.spawnOverride;
            }

            if (context.EnemyAI && context.EnemyAI.projectileSpawn)
            {
                return context.EnemyAI.projectileSpawn;
            }

            return context.Transform;
        }

        int ResolveProjectileDamage(AbilityRuntimeContext context)
        {
            if (projectile.damageOverride >= 0)
            {
                return projectile.damageOverride;
            }

            return impactDamage.ResolveDamage(context);
        }

        float ResolveProjectileSpeed(AbilityRuntimeContext context)
        {
            if (projectile.speedOverride > 0f)
            {
                return projectile.speedOverride;
            }

            if (context.EnemyAI)
            {
                return Mathf.Max(0.01f, context.EnemyAI.projectileSpeed);
            }

            return 9f;
        }

        float ResolveProjectileLife(AbilityRuntimeContext context)
        {
            if (projectile.lifeOverride > 0f)
            {
                return projectile.lifeOverride;
            }

            if (context.EnemyAI)
            {
                return Mathf.Max(0.01f, context.EnemyAI.projectileLife);
            }

            return 3.5f;
        }

        LayerMask ResolveHitMask(AbilityRuntimeContext context)
        {
            if (projectile.useCustomHitMask)
            {
                return projectile.hitMaskOverride;
            }

            if (context.EnemyAI)
            {
                return context.EnemyAI.playerMask | context.EnemyAI.neutralNpcMask;
            }

            return projectile.hitMaskOverride;
        }

        Vector2 ResolveBaseDirection(AbilityRuntimeContext context)
        {
            const float epsilon = 0.0001f;

            if (context.IsPlayerControlled)
            {
                if (useMouseAimForPlayers)
                {
                    Vector2 mouseDir = ComputeMouseAimDirection(context);
                    if (mouseDir.sqrMagnitude > epsilon)
                    {
                        return mouseDir.normalized;
                    }
                }

                Vector2 desired = context.DesiredDirection;
                if (desired.sqrMagnitude > epsilon)
                {
                    return desired.normalized;
                }
            }
            else
            {
                Vector2 desired = context.DesiredDirection;
                if (desired.sqrMagnitude > epsilon)
                {
                    return desired.normalized;
                }
            }

            if (context.Target)
            {
                Vector2 toTarget = (Vector2)context.Target.position - (context.Transform ? (Vector2)context.Transform.position : Vector2.zero);
                if (toTarget.sqrMagnitude > epsilon)
                {
                    return toTarget.normalized;
                }
            }

            if (context.Transform)
            {
                Vector2 ownerRight = context.Transform.right;
                if (ownerRight.sqrMagnitude > epsilon)
                {
                    return ownerRight.normalized;
                }
            }

            return fallbackDirection.sqrMagnitude > epsilon ? fallbackDirection.normalized : Vector2.right;
        }

        Transform NormalizeChainTarget(Transform candidate)
        {
            if (!candidate)
            {
                return null;
            }

            var enemyHealth = candidate.GetComponentInParent<EnemyHealth2D>();
            if (enemyHealth)
            {
                return enemyHealth.transform;
            }

            var playerHealth = candidate.GetComponentInParent<PlayerHealth>();
            if (playerHealth)
            {
                return playerHealth.transform;
            }

            var companionHealth = candidate.GetComponentInParent<CompanionHealth>();
            if (companionHealth)
            {
                return companionHealth.transform;
            }

            var runner = candidate.GetComponentInParent<AbilityRunner>();
            if (runner)
            {
                return runner.transform;
            }

            return candidate.root ? candidate.root : candidate;
        }

        Vector2 ComputeMouseAimDirection(AbilityRuntimeContext context)
        {
            Camera cam = Camera.main;
            if (!cam || !context.Transform)
            {
                return Vector2.zero;
            }

            Vector3 ownerPos = context.Transform.position;
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = ownerPos.z;
            return (Vector2)(world - ownerPos);
        }

        static Vector2 RotateOffsetToDirection(Vector3 offset, Vector2 direction)
        {
            Vector2 offset2D = new Vector2(offset.x, offset.y);
            if (offset2D.sqrMagnitude < 0.0000001f)
            {
                return Vector2.zero;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            float angle = Mathf.Atan2(direction.y, direction.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            return new Vector2(cos * offset2D.x - sin * offset2D.y, sin * offset2D.x + cos * offset2D.y);
        }
        [System.Serializable]
        private sealed class ProjectileDamageSettings
        {
            [SerializeField]
            private int damage = 10;

            [SerializeField]
            private bool scaleWithOwnerDamage = false;

            [SerializeField]
            [Range(0f, 3f)]
            private float ownerDamageScale = 1f;

            [SerializeField]
            private int minimumDamage = 1;

            public int ResolveDamage(AbilityRuntimeContext context)
            {
                int amount = damage;
                if (scaleWithOwnerDamage && context != null)
                {
                    amount += Mathf.RoundToInt(context.GetOwnerBaseDamage() * ownerDamageScale);
                }

                return Mathf.Max(minimumDamage, amount);
            }
        }

        [System.Serializable]
        private sealed class AreaExplosionSettings
        {
            [SerializeField]
            [Min(0.1f)]
            private float radius = 2f;

            [SerializeField]
            private LayerMask targetMask = ~0;

            [SerializeField]
            private bool includeHitTarget = true;

            [SerializeField]
            private bool damageTiles = false;

            [SerializeField]
            private int tileDamage = 25;

            [SerializeField]
            private ProjectileDamageSettings explosionDamage = new ProjectileDamageSettings();

            [SerializeField]
            private GameObject explosionVfxPrefab;

            [SerializeField]
            private float explosionVfxLifetime = 2f;

            [SerializeField]
            private float explosionVfxScale = 1f;

            public float Radius => Mathf.Max(0.1f, radius);
            public LayerMask TargetMask => targetMask;
            public bool IncludeHitTarget => includeHitTarget;
            public bool DamageTiles => damageTiles;
            public int TileDamage => Mathf.Max(1, tileDamage);
            public ProjectileDamageSettings ExplosionDamage => explosionDamage;
            public GameObject ExplosionVfxPrefab => explosionVfxPrefab;
            public float ExplosionVfxLifetime => explosionVfxLifetime;
            public float ExplosionVfxScale => explosionVfxScale;
        }

        [System.Serializable]
        private sealed class AreaDotSettings
        {
            [SerializeField]
            private bool useArea = true;

            [SerializeField]
            [Min(0.5f)]
            private float radius = 3f;

            [SerializeField]
            private LayerMask targetMask = ~0;

            [SerializeField]
            private bool includeHitTarget = true;

            [SerializeField]
            private bool followHitTarget = false;

            [SerializeField]
            [Min(0f)]
            private float durationSeconds = 5f;

            [SerializeField]
            [Min(0.1f)]
            private float tickInterval = 1f;

            [SerializeField]
            private ProjectileDamageSettings tickDamage = new ProjectileDamageSettings();

            [SerializeField]
            private bool damageTiles = false;

            [SerializeField]
            private int tileDamage = 5;

            [SerializeField]
            private GameObject persistentAreaPrefab;

            [SerializeField]
            private float areaPrefabLifetime = 0f;

            [SerializeField]
            private bool attachVfxToTarget = false;

            public void StartEffect(AbilityRuntimeContext context, Vector2 center, Transform hitTarget, Vector3 spawnPosition)
            {
                AbilityRunner runner = context != null ? context.Runner : null;
                if (runner == null)
                {
                    ApplyTick(context, center, hitTarget);
                    return;
                }

                runner.StartCoroutine(DamageRoutine(context, center, hitTarget, spawnPosition));
            }

            IEnumerator DamageRoutine(AbilityRuntimeContext context, Vector2 initialCenter, Transform hitTarget, Vector3 spawnPosition)
            {
                GameObject vfxInstance = null;
                bool destroyManually = false;
                if (persistentAreaPrefab)
                {
                    vfxInstance = Object.Instantiate(persistentAreaPrefab, spawnPosition, Quaternion.identity);
                    if (attachVfxToTarget && hitTarget)
                    {
                        vfxInstance.transform.SetParent(hitTarget);
                        vfxInstance.transform.localPosition = Vector3.zero;
                    }

                    if (areaPrefabLifetime > 0f)
                    {
                        Object.Destroy(vfxInstance, areaPrefabLifetime);
                    }
                    else
                    {
                        destroyManually = true;
                    }
                }

                float duration = Mathf.Max(0f, durationSeconds);
                float tickDelay = Mathf.Max(0.1f, tickInterval);

                if (duration <= 0f)
                {
                    ApplyTick(context, ResolveCenter(initialCenter, hitTarget), hitTarget);
                }
                else
                {
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        ApplyTick(context, ResolveCenter(initialCenter, hitTarget), hitTarget);
                        elapsed += tickDelay;
                        if (elapsed >= duration)
                        {
                            break;
                        }
                        yield return new WaitForSeconds(tickDelay);
                    }
                }

                if (destroyManually && vfxInstance)
                {
                    Object.Destroy(vfxInstance);
                }
            }

            Vector2 ResolveCenter(Vector2 initialCenter, Transform hitTarget)
            {
                if (followHitTarget && hitTarget)
                {
                    return hitTarget.position;
                }

                return initialCenter;
            }

            void ApplyTick(AbilityRuntimeContext context, Vector2 center, Transform hitTarget)
            {
                int amount = tickDamage.ResolveDamage(context);
                if (amount <= 0)
                {
                    return;
                }

                if (useArea)
                {
                    float radiusValue = Mathf.Max(0.1f, radius);
                    var hits = Physics2D.OverlapCircleAll(center, radiusValue, targetMask);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        Collider2D col = hits[i];
                        if (!col)
                        {
                            continue;
                        }

                        Transform candidate = col.transform;
                        if (!includeHitTarget && hitTarget && (candidate == hitTarget || candidate.IsChildOf(hitTarget)))
                        {
                            continue;
                        }

                        AbilityEffectUtility.TryApplyDamage(context, col, amount, center);
                    }

                    if (damageTiles)
                    {
                        AbilityEffectUtility.TryDamageTilesCircle(center, radiusValue, Mathf.Max(1, tileDamage), context != null ? context.Transform : null);
                    }
                }
                else
                {
                    if (!hitTarget)
                    {
                        return;
                    }

                    Vector2 dir = (Vector2)hitTarget.position - center;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        dir = Vector2.right;
                    }
                    AbilityEffectUtility.TryApplyDamage(context, hitTarget, amount, dir);
                }
            }
        }

        [System.Serializable]
        private sealed class ChainSettings
        {
            [SerializeField]
            [Min(0)]
            private int maxChains = 3;

            [SerializeField]
            [Min(0.5f)]
            private float searchRadius = 4f;

            [SerializeField]
            private LayerMask targetMask = ~0;

            [SerializeField]
            [Range(0f, 0.95f)]
            private float damageFalloffPerHop = 0.2f;

            [SerializeField]
            private bool allowRepeatTargets = false;

            [SerializeField]
            [Min(0f)]
            private float delayBetweenChains = 0.05f;

            [SerializeField]
            private bool overrideInitialDamage = false;

            [SerializeField]
            private ProjectileDamageSettings chainDamage = new ProjectileDamageSettings();

            [SerializeField]
            private GameObject chainVfxPrefab;

            [SerializeField]
            private float chainVfxLifetime = 1.25f;

            public int MaxChains => Mathf.Max(0, maxChains);
            public float SearchRadius => Mathf.Max(0.1f, searchRadius);
            public LayerMask TargetMask => targetMask;
            public bool AllowRepeatTargets => allowRepeatTargets;
            public float DelayBetweenChains => Mathf.Max(0f, delayBetweenChains);
            public bool OverrideInitialDamage => overrideInitialDamage;
            public GameObject ChainVfxPrefab => chainVfxPrefab;
            public float ChainVfxLifetime => chainVfxLifetime;

            public int ResolveChainDamage(AbilityRuntimeContext context, int hopIndex, ProjectileDamageSettings fallback)
            {
                ProjectileDamageSettings settings = overrideInitialDamage ? chainDamage : fallback;
                int baseDamage = settings.ResolveDamage(context);
                float falloff = Mathf.Clamp01(1f - damageFalloffPerHop);
                float multiplier = Mathf.Pow(falloff, Mathf.Max(0, hopIndex));
                return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
            }
        }

        [System.Serializable]
        private sealed class ConeSettings
        {
            public enum SpreadMode
            {
                Even,
                Random
            }

            [SerializeField]
            private int projectileCount = 3;

            [SerializeField]
            [Range(0f, 180f)]
            private float coneAngleDegrees = 35f;

            [SerializeField]
            private SpreadMode spread = SpreadMode.Even;

            [SerializeField]
            private int randomSeedOffset = 0;

            public int ProjectileCount => Mathf.Max(1, projectileCount);
            public float ConeAngleDegrees => Mathf.Max(0f, coneAngleDegrees);
            public SpreadMode Spread => spread;
            public int RandomSeedOffset => randomSeedOffset;
        }

        [System.Serializable]
        private sealed class BeamSettings
        {
            [SerializeField]
            private int beamCount = 3;

            [SerializeField]
            [Range(0f, 180f)]
            private float spreadDegrees = 20f;

            [SerializeField]
            [Min(0.25f)]
            private float range = 7f;

            [SerializeField]
            [Min(0.05f)]
            private float radius = 0.25f;

            [SerializeField]
            private int maxTargetsPerBeam = 3;

            [SerializeField]
            private LayerMask hitMask = ~0;

            [SerializeField]
            private bool damageTiles = false;

            [SerializeField]
            private int tileDamage = 15;

            [SerializeField]
            private ProjectileDamageSettings damage = new ProjectileDamageSettings();

            [SerializeField]
            private GameObject beamVfxPrefab;

            [SerializeField]
            private float beamVfxLifetime = 0.5f;

            public int BeamCount => Mathf.Max(1, beamCount);
            public float SpreadDegrees => Mathf.Max(0f, spreadDegrees);
            public float Range => Mathf.Max(0.25f, range);
            public float Radius => Mathf.Max(0.05f, radius);
            public int MaxTargetsPerBeam => Mathf.Max(1, maxTargetsPerBeam);
            public LayerMask HitMask => hitMask;
            public bool DamageTiles => damageTiles;
            public int TileDamage => Mathf.Max(1, tileDamage);
            public ProjectileDamageSettings Damage => damage;
            public GameObject BeamVfxPrefab => beamVfxPrefab;
            public float BeamVfxLifetime => beamVfxLifetime;
        }
    }
}




