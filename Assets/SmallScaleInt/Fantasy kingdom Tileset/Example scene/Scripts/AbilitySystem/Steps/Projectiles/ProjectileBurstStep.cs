using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Launches a configurable burst of projectiles, sourcing spawn/damage data from overrides or EnemyAI defaults.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ProjectileBurstStep : AbilityStep
    {
        [Header("Projectiles")]
        [SerializeField]
        private ProjectileInfo projectile = new ProjectileInfo();

        [Header("Burst Pattern")]
        [SerializeField]
        [Tooltip("How many projectiles are launched in the burst.")]
        private int projectileCount = 6;

        [SerializeField]
        [Tooltip("Offset applied to the base angle in degrees.")]
        private float angleOffset = 0f;

        [SerializeField]
        [Tooltip("Align the first projectile toward the current target when available.")]
        private bool alignToTargetDirection = true;

        [SerializeField]
        [Tooltip("Fire projectiles sequentially instead of all at once.")]
        private bool fireSequentially = false;

        [SerializeField]
        [Tooltip("Delay between sequential shots (seconds).")]
        private float sequentialDelay = 0.1f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            int count = Mathf.Max(1, projectileCount);
            Projectile2D prefab = ResolveProjectilePrefab(context);
            if (!prefab)
            {
                yield break;
            }

            Transform spawnTransform = ResolveSpawnTransform(context);
            Vector2 spawnPosition = spawnTransform ? (Vector2)spawnTransform.position : (Vector2)context.Transform.position;

            float damage = ResolveProjectileDamage(context);
            float speed = ResolveProjectileSpeed(context);
            float life = ResolveProjectileLife(context);
            LayerMask mask = ResolveHitMask(context);

            float baseAngle = angleOffset;
            if (alignToTargetDirection && context.Target)
            {
                Vector2 toTarget = (Vector2)context.Target.position - spawnPosition;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    baseAngle += Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                }
            }

            float step = 360f / count;

            if (!fireSequentially || sequentialDelay <= 0f || count == 1)
            {
                for (int i = 0; i < count; i++)
                {
                    FireProjectile(prefab, spawnTransform, spawnPosition, baseAngle + step * i, speed, (int)damage, life, mask, context.Transform);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    if (context.CancelRequested) yield break;

                    FireProjectile(prefab, spawnTransform, spawnPosition, baseAngle + step * i, speed, (int)damage, life, mask, context.Transform);

                    if (i < count - 1)
                    {
                        float end = Time.time + sequentialDelay;
                        while (Time.time < end)
                        {
                            if (context.CancelRequested) yield break;
                            yield return null;
                        }
                    }
                }
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

        void FireProjectile(Projectile2D prefab, Transform spawnTransform, Vector2 fallbackPos, float angleDeg,
            float speed, int damage, float life, LayerMask mask, Transform owner)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = Vector2.right;
            }
            dir.Normalize();

            Quaternion rotation = spawnTransform ? spawnTransform.rotation : Quaternion.identity;
            Vector2 spawnPos = spawnTransform ? (Vector2)spawnTransform.position : fallbackPos;

            Projectile2D proj = Object.Instantiate(prefab, spawnPos, rotation);
            proj.Init(dir * speed, damage, life, mask, owner);
        }
    }
}






