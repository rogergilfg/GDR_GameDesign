using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Deals damage to targets inside a cone in front  of the owner,  ctwith optional VFX and sweep damage.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ConeDamageStep : AbilityStep
    {
        public enum AimMode
        {
            DesiredDirection,
            Mouse,
            Target,
            TransformForward,
            Custom
        }

        [Header("Aim")]
        [SerializeField]
        private AimMode aimMode = AimMode.DesiredDirection;

        [SerializeField]
        private Vector2 customDirection = Vector2.right;

        [SerializeField]
        private bool preferMouseForPlayers = true;

        [Header("Cone Shape")]
        [SerializeField]
        private float radius = 2.8f;

        [SerializeField]
        [Range(5f, 180f)]
        private float coneAngleDeg = 60f;

        [SerializeField]
        private Vector2 coneOffset = Vector2.zero;

        [Header("Damage")]
        [SerializeField]
        private int baseDamage = 16;

        [SerializeField]
        [Tooltip("When enabled, adds a percentage of the owner's base damage to the ability damage.")]
        private bool scaleWithOwnerDamage = false;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Percentage of owner's damage to add (0.5 = 50% of owner damage added to base ability damage).")]
        private float ownerDamageScale = 1f;

        [SerializeField]
        [Range(0f, 0.5f)]
        private float damageVariance = 0.08f;

        [SerializeField]
        [Range(0f, 1f)]
        private float critChance = 0.15f;

        [SerializeField]
        [Range(1f, 3f)]
        private float critMultiplier = 1.5f;

        [SerializeField]
        private LayerMask enemyMask;

        [SerializeField]
        private bool showCombatText = true;

        [SerializeField]
        private Vector2 combatTextOffset = new Vector2(0f, 0.25f);

        [Header("Sweep Damage")]
        [SerializeField]
        private bool sequentialDamage = true;

        [SerializeField]
        private float growDuration = 0.25f;

        [SerializeField]
        private float ringThickness = 0.35f;

        [SerializeField]
        private int samplesPerSecond = 90;

        [SerializeField]
        private bool allowMultipleHitsPerTarget = false;

        [Header("Tile Damage")]
        [SerializeField]
        private bool damageTiles = false;
        [SerializeField, Min(1)]
        private int tileDamage = 1;

        [Header("VFX")]
        [SerializeField]
        private GameObject animationPrefab;

        [SerializeField]
        private GameObject particlePrefab;

        [SerializeField]
        private Vector2 vfxSpawnOffset = Vector2.zero;

        [SerializeField]
        private float vfxExtraRotationDeg = 0f;

        [SerializeField]
        private float vfxCleanupDelay = 3f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Vector2 origin = ComputeOrigin(context);
            Vector2 direction = ResolveAimDirection(context, origin);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.right;
            }
            direction.Normalize();

            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + vfxExtraRotationDeg);
            SpawnVfx(origin, rotation);

            if (sequentialDamage)
            {
                yield return ApplySequentialDamage(context, origin, direction, radius);
            }
            else
            {
                ApplyInstantDamage(context, origin, direction, radius);
            }
        }

        Vector2 ComputeOrigin(AbilityRuntimeContext context)
        {
            if (context.Transform)
            {
                Vector3 world = context.Transform.TransformPoint(coneOffset);
                return new Vector2(world.x, world.y);
            }

            return coneOffset;
        }

        Vector2 ResolveAimDirection(AbilityRuntimeContext context, Vector2 origin)
        {
            switch (aimMode)
            {
                case AimMode.Mouse:
                    return ComputeMouseDirection(context, origin);
                case AimMode.Target:
                    if (context.Target)
                    {
                        Vector2 toTarget = (Vector2)context.Target.position - origin;
                        if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
                    }
                    break;
                case AimMode.TransformForward:
                    if (context.Transform)
                    {
                        Vector2 right = context.Transform.right;
                        if (right.sqrMagnitude > 0.0001f) return right.normalized;
                    }
                    break;
                case AimMode.Custom:
                    if (customDirection.sqrMagnitude > 0.0001f) return customDirection.normalized;
                    break;
                case AimMode.DesiredDirection:
                default:
                    if (context.DesiredDirection.sqrMagnitude > 0.0001f) return context.DesiredDirection.normalized;
                    break;
            }

            if (context.IsPlayerControlled && preferMouseForPlayers)
            {
                Vector2 mouse = ComputeMouseDirection(context, origin);
                if (mouse.sqrMagnitude > 0.0001f) return mouse.normalized;
            }

            if (context.Target)
            {
                Vector2 toTarget = (Vector2)context.Target.position - origin;
                if (toTarget.sqrMagnitude > 0.0001f) return toTarget.normalized;
            }

            if (context.Transform)
            {
                Vector2 right = context.Transform.right;
                if (right.sqrMagnitude > 0.0001f) return right.normalized;
            }

            return Vector2.right;
        }

        Vector2 ComputeMouseDirection(AbilityRuntimeContext context, Vector2 origin)
        {
            if (!context.IsPlayerControlled) return Vector2.zero;
            Camera cam = Camera.main;
            if (!cam) return Vector2.zero;

            Vector3 owner = context.Transform ? context.Transform.position : new Vector3(origin.x, origin.y, 0f);
            Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = owner.z;
            return (Vector2)(world - owner);
        }

        IEnumerator ApplySequentialDamage(AbilityRuntimeContext context, Vector2 origin, Vector2 forward, float radiusValue)
        {
            float elapsed = 0f;
            float radiusPerSecond = radiusValue / Mathf.Max(0.0001f, growDuration);
            int iterations = Mathf.Max(1, Mathf.CeilToInt(growDuration * samplesPerSecond));
            float dt = growDuration / iterations;
            float ringHalfThickness = Mathf.Max(0.001f, ringThickness * 0.5f);
            float cosThreshold = Mathf.Cos(coneAngleDeg * 0.5f * Mathf.Deg2Rad);
            HashSet<Component> alreadyHit = allowMultipleHitsPerTarget ? null : new HashSet<Component>();

            for (int i = 0; i < iterations; i++)
            {
                float currentRadius = Mathf.Min(radiusValue, elapsed * radiusPerSecond);
                float innerRadius = Mathf.Max(0f, currentRadius - ringHalfThickness);
                float outerRadius = Mathf.Min(radiusValue, currentRadius + ringHalfThickness);

                ApplyRingDamage(context, origin, forward, innerRadius, outerRadius, cosThreshold, alreadyHit);
                TryDamageTiles(origin, forward, outerRadius);

                elapsed += dt;
                float end = Time.time + dt;
                while (Time.time < end)
                {
                    if (context.CancelRequested) yield break;
                    yield return null;
                }
            }

            if (!allowMultipleHitsPerTarget)
            {
                ApplyRingDamage(context, origin, forward, radiusValue - ringHalfThickness, radiusValue, cosThreshold, alreadyHit);
            }

            TryDamageTiles(origin, forward, radiusValue);
        }

        void ApplyRingDamage(AbilityRuntimeContext context, Vector2 origin, Vector2 forward, float innerRadius, float outerRadius, float cosThreshold, HashSet<Component> alreadyHit)
        {
            const int BufferSize = 128;
            Collider2D[] buffer = new Collider2D[BufferSize];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hitCount = Physics2D.OverlapCircle(origin, outerRadius, filter, buffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = buffer[i];
                if (!col) continue;

                Vector2 to = (Vector2)col.bounds.center - origin;
                float dist = to.magnitude;
                if (dist < innerRadius || dist > outerRadius) continue;

                Vector2 n = dist > 0.0001f ? to / dist : Vector2.right;
                if (Vector2.Dot(n, forward) < cosThreshold) continue;

                var damageable = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable == null) continue;

                if (!allowMultipleHitsPerTarget && alreadyHit != null)
                {
                    Component key = damageable as Component;
                    if (key && alreadyHit.Contains(key)) continue;
                    if (key) alreadyHit.Add(key);
                }

                ApplyDamage(context, damageable, n, col.bounds.center);
            }
        }

        void ApplyInstantDamage(AbilityRuntimeContext context, Vector2 origin, Vector2 forward, float radiusValue)
        {
            float cosThreshold = Mathf.Cos(coneAngleDeg * 0.5f * Mathf.Deg2Rad);
            const int BufferSize = 256;
            Collider2D[] buffer = new Collider2D[BufferSize];
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(enemyMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int hitCount = Physics2D.OverlapCircle(origin, radiusValue, filter, buffer);
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = buffer[i];
                if (!col) continue;

                Vector2 to = (Vector2)col.bounds.center - origin;
                float dist = to.magnitude;
                if (dist > radiusValue) continue;

                Vector2 n = dist > 0.0001f ? to / dist : Vector2.right;
                if (Vector2.Dot(n, forward) < cosThreshold) continue;

                var damageable = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable == null) continue;

                ApplyDamage(context, damageable, n, col.bounds.center);
            }

            TryDamageTiles(origin, forward, radiusValue);
        }

        void TryDamageTiles(Vector2 center, Vector2 forward, float radiusValue)
        {
            if (!damageTiles) return;
            AbilityEffectUtility.TryDamageTilesCone(center, forward, Mathf.Max(0f, radiusValue), coneAngleDeg, tileDamage);
        }

        void ApplyDamage(AbilityRuntimeContext context, EnemyAI.IDamageable damageable, Vector2 hitDirection, Vector3 hitPoint)
        {
            int baseValue = baseDamage;

            // Apply owner damage scaling if enabled
            if (scaleWithOwnerDamage)
            {
                int ownerDamage = context.GetOwnerBaseDamage();
                int scaledDamage = Mathf.RoundToInt(ownerDamage * ownerDamageScale);
                baseValue += scaledDamage;
            }

            bool crit;
            int damage = RollDamage(baseValue, out crit);
            damageable.TakeDamage(damage, hitDirection);
            EnemyAI.NotifyDamageDealt(damageable, context.Transform ? context.Transform : context.Runner.transform, damage);

            if (showCombatText && CombatTextManager.Instance)
            {
                Vector3 pos = hitPoint + (Vector3)combatTextOffset;
                CombatTextManager.Instance.SpawnDamage(damage, pos, crit);
            }
        }

        int RollDamage(int baseValue, out bool crit)
        {
            int modified = baseValue;
            if (PlayerStats.Instance != null)
            {
                modified = PlayerStats.Instance.GetModifiedBaseDamage(baseValue);
            }

            float variance = 1f + Random.Range(-damageVariance, damageVariance);
            int rolled = Mathf.Max(1, Mathf.RoundToInt(modified * variance));
            crit = Random.value < critChance;
            if (crit)
            {
                rolled = Mathf.Max(1, Mathf.RoundToInt(rolled * critMultiplier));
            }
            return rolled;
        }

        void SpawnVfx(Vector2 origin, Quaternion rotation)
        {
            Vector3 offset = rotation * (Vector3)vfxSpawnOffset;
            if (animationPrefab)
            {
                GameObject anim = Object.Instantiate(animationPrefab, origin + (Vector2)offset, rotation);
                if (vfxCleanupDelay > 0f) Object.Destroy(anim, vfxCleanupDelay);
            }

            if (particlePrefab)
            {
                GameObject fx = Object.Instantiate(particlePrefab, origin + (Vector2)offset, rotation);
                if (vfxCleanupDelay > 0f) Object.Destroy(fx, vfxCleanupDelay);
            }
        }
    }
}







