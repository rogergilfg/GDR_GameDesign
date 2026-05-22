using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Fires projectiles that apply a passive ability to targets they hit.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ProjectileApplyAbilityStep : AbilityStep
    {
        private enum SpreadMode
        {
            Single,
            EvenCone,
            RandomCone
        }

        [Header("Abilities")]
        [SerializeField]
        [Tooltip("Passive abilities applied to targets struck by the projectile.")]
        private List<AbilityDefinition> abilitiesToApply = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Duration in seconds before the applied ability is removed. 0 = permanent.")]
        [Min(0f)]
        private float duration = 0f;

        [SerializeField]
        [Tooltip("Allow the same ability to be applied multiple times to the same target.")]
        private bool allowStacking = false;

        [Header("Target Filter")]
        [SerializeField]
        [Tooltip("Only apply the ability when the hit object is on one of these layers.")]
        private bool filterTargetsByLayer = false;

        [SerializeField]
        [Tooltip("Layers that are eligible to receive the applied ability when filtering is enabled.")]
        private LayerMask targetLayerMask = ~0;

        [Header("Projectile")]
        [SerializeField]
        private ProjectileInfo projectile = new ProjectileInfo();

        [Header("Volley")]
        [SerializeField]
        [Tooltip("Number of projectiles fired in the volley.")]
        private int projectileCount = 1;

        [SerializeField]
        [Tooltip("Pattern used when firing more than one projectile.")]
        private SpreadMode spreadMode = SpreadMode.Single;

        [SerializeField]
        [Tooltip("Total cone angle in degrees used for cone spreads.")]
        private float coneAngleDegrees = 20f;

        [SerializeField]
        [Tooltip("Random seed offset applied when using RandomCone. Leave at 0 to use Unity's global random.")]
        private int randomSeedOffset = 0;

        [Header("Direction")]
        [SerializeField]
        [Tooltip("Use the player's mouse aim when no desired direction is provided.")]
        private bool useMouseAimForPlayers = true;

        [SerializeField]
        [Tooltip("Fallback direction when no other direction can be resolved.")]
        private Vector2 fallbackDirection = Vector2.right;

        [Header("Muzzle")]
        [SerializeField]
        [Tooltip("Offset applied when spawning the projectile relative to the spawn transform (local space).")]
        private Vector3 muzzleLocalOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Spawn a muzzle VFX when the volley fires.")]
        private bool spawnMuzzleVfx = true;

        [SerializeField]
        [Tooltip("Prefab spawned at the muzzle. Optional.")]
        private GameObject muzzleVfxPrefab;

        [SerializeField]
        [Tooltip("Seconds before the muzzle VFX is destroyed (<= 0 keeps it alive).")]
        private float muzzleVfxLifetime = 2f;

        [SerializeField]
        [Tooltip("Scale multiplier applied to the muzzle VFX instance.")]
        private float muzzleVfxScale = 1f;

        [Header("Impact")]
        [SerializeField]
        [Tooltip("Prefab spawned where the projectile impacts. Optional.")]
        private GameObject hitVfxPrefab;

#pragma warning disable 0414
        [SerializeField]
        [Tooltip("Seconds before the hit VFX is destroyed (<= 0 keeps it alive).")]
        private float hitVfxLifetime = 2f;

        [SerializeField]
        [Tooltip("Scale multiplier applied to the hit VFX instance.")]
        private float hitVfxScale = 1f;
#pragma warning restore 0414

        [Header("Lifetime & Piercing")]
        [SerializeField]
        [Tooltip("Override projectile lifetime when firing (<= 0 keeps default).")]
        private float overrideProjectileLifetime = -1f;

        [SerializeField]
        [Tooltip("Number of targets each projectile can pierce (0 = no pierce).")] private int pierceCount = 0;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (!HasValidAbilities())
            {
                yield break;
            }

            Projectile2D prefab = ResolveProjectilePrefab(context);
            if (!prefab)
            {
                yield break;
            }

            Transform spawnTransform = ResolveSpawnTransform(context);
            Vector2 basePosition = spawnTransform ? (Vector2)spawnTransform.position : (Vector2)context.Transform.position;
            float baseZ = spawnTransform ? spawnTransform.position.z : (context.Transform ? context.Transform.position.z : 0f);

            float projectileSpeed = ResolveProjectileSpeed(context);
            int projectileDamage = ResolveProjectileDamage(context);
            float projectileLife = ResolveProjectileLife(context);
            if (overrideProjectileLifetime > 0f)
            {
                projectileLife = overrideProjectileLifetime;
            }
            LayerMask hitMask = ResolveHitMask(context);

            Vector2 baseDirection = ResolveBaseDirection(context);
            if (baseDirection.sqrMagnitude < 0.0001f)
            {
                baseDirection = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection : Vector2.right;
            }
            baseDirection.Normalize();

            int count = Mathf.Max(1, projectileCount);

            if (spawnMuzzleVfx && muzzleVfxPrefab)
            {
                Quaternion fallbackRotation = spawnTransform ? spawnTransform.rotation :
                    (context.Transform ? context.Transform.rotation : Quaternion.identity);
                Vector3 muzzleWorldPosition = ComputeMuzzleWorldPosition(basePosition, baseZ, baseDirection);
                SpawnMuzzleVfx(muzzleWorldPosition, baseDirection, fallbackRotation);
            }

            if (spreadMode == SpreadMode.Single || count == 1)
            {
                FireProjectile(prefab, spawnTransform, basePosition, baseZ, baseDirection, projectileSpeed, projectileDamage, projectileLife, hitMask, context.Transform, context);
            }
            else
            {
                switch (spreadMode)
                {
                    case SpreadMode.EvenCone:
                        FireEvenCone(prefab, spawnTransform, basePosition, baseZ, baseDirection, projectileSpeed, projectileDamage, projectileLife, hitMask, context.Transform, count, context);
                        break;
                    case SpreadMode.RandomCone:
                        FireRandomCone(prefab, spawnTransform, basePosition, baseZ, baseDirection, projectileSpeed, projectileDamage, projectileLife, hitMask, context.Transform, count, context);
                        break;
                }
            }

            yield break;
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

            if (context.EnemyAI)
            {
                return context.EnemyAI.projectileDamage;
            }

            return 10;
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

        void FireEvenCone(Projectile2D prefab, Transform spawnTransform, Vector2 basePosition, float baseZ, Vector2 baseDirection,
            float speed, int damage, float life, LayerMask mask, Transform owner, int count, AbilityRuntimeContext context)
        {
            float totalAngle = Mathf.Clamp(coneAngleDegrees, 0f, 360f);
            if (count == 1 || totalAngle <= 0.01f)
            {
                FireProjectile(prefab, spawnTransform, basePosition, baseZ, baseDirection, speed, damage, life, mask, owner, context);
                return;
            }

            float halfAngle = totalAngle * 0.5f;
            float step = count > 1 ? totalAngle / (count - 1) : 0f;

            for (int i = 0; i < count; i++)
            {
                float angle = -halfAngle + step * i;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                FireProjectile(prefab, spawnTransform, basePosition, baseZ, dir, speed, damage, life, mask, owner, context);
            }
        }

        void FireRandomCone(Projectile2D prefab, Transform spawnTransform, Vector2 basePosition, float baseZ, Vector2 baseDirection,
            float speed, int damage, float life, LayerMask mask, Transform owner, int count, AbilityRuntimeContext context)
        {
            float halfAngle = Mathf.Clamp(coneAngleDegrees, 0f, 360f) * 0.5f;
            if (halfAngle <= 0.01f)
            {
                halfAngle = 0f;
            }

            Random.State originalState = Random.state;
            if (randomSeedOffset != 0)
            {
                Random.InitState(Time.frameCount + randomSeedOffset);
            }

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(-halfAngle, halfAngle);
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * baseDirection;
                FireProjectile(prefab, spawnTransform, basePosition, baseZ, dir, speed, damage, life, mask, owner, context);
            }

            if (randomSeedOffset != 0)
            {
                Random.state = originalState;
            }
        }

        void FireProjectile(Projectile2D prefab, Transform spawnTransform, Vector2 basePosition, float baseZ, Vector2 direction,
            float speed, int damage, float life, LayerMask mask, Transform owner, AbilityRuntimeContext context)
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
            RegisterAbilityCallback(proj, context);

            if (pierceCount > 0 || hitVfxPrefab)
            {
                // Impact VFX and piercing not supported on this projectile implementation.
            }
        }

        Vector3 ComputeMuzzleWorldPosition(Vector2 basePosition, float baseZ, Vector2 direction)
        {
            Vector2 rotatedOffset = RotateOffsetToDirection(muzzleLocalOffset, direction);
            return new Vector3(basePosition.x + rotatedOffset.x, basePosition.y + rotatedOffset.y, baseZ + muzzleLocalOffset.z);
        }

        void SpawnMuzzleVfx(Vector3 position, Vector2 direction, Quaternion fallbackRotation)
        {
            if (!muzzleVfxPrefab) return;

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

        void RegisterAbilityCallback(Projectile2D projectileInstance, AbilityRuntimeContext context)
        {
            if (!projectileInstance)
            {
                return;
            }

            projectileInstance.RegisterDamageableHitCallback(target =>
            {
                if (!target)
                {
                    return;
                }

                if (!IsTargetLayerAllowed(target))
                {
                    return;
                }

                for (int i = 0; i < abilitiesToApply.Count; i++)
                {
                    var ability = abilitiesToApply[i];
                    if (!IsAbilityValid(ability))
                        continue;

                    AbilityApplicationUtility.TryApplyAbility(ability, target, context, allowStacking, duration);
                }
            });
        }

        bool IsTargetLayerAllowed(Transform target)
        {
            if (!filterTargetsByLayer)
            {
                return true;
            }

            Transform current = target;
            while (current != null)
            {
                int layerBit = 1 << current.gameObject.layer;
                if ((targetLayerMask.value & layerBit) != 0)
                {
                    return true;
                }
                current = current.parent;
            }

            return false;
        }

        bool HasValidAbilities()
        {
            if (abilitiesToApply == null || abilitiesToApply.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < abilitiesToApply.Count; i++)
            {
                if (IsAbilityValid(abilitiesToApply[i]))
                {
                    return true;
                }
            }

            return false;
        }

        void ApplyProjectileVisualOverrides(Projectile2D instance)
        {
            if (!instance || projectile == null)
            {
                return;
            }

            if (projectile.overrideSpriteColor)
            {
                var renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    var sr = renderers[i];
                    if (!sr)
                    {
                        continue;
                    }

                    sr.color = projectile.spriteOverrideColor;
                }
            }
        }

        bool IsAbilityValid(AbilityDefinition ability)
        {
            return ability && ability.IsPassive;
        }
    }
}




