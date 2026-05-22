using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    [System.Serializable]
    [AbilityComponentDescription("Applies damage to all valid targets within a shaped area around the caster or a sampled position.")]
    [MovedFrom("AbilitySystem")]
    public sealed class DamageAreaStep : AbilityStep
    {
        private enum AreaAnchor
        {
            Owner,
            Target,
            Custom,
            MousePosition
        }

        [SerializeField]
        [Tooltip("Where the area is centred.")]
        private AreaAnchor anchor = AreaAnchor.Owner;

        [SerializeField]
        [Tooltip("Custom transform used when Anchor is Custom.")]
        private Transform customAnchor;

        [SerializeField]
        [Tooltip("Radius of the damage area.")]
        private float radius = 2f;

        [SerializeField]
        [Tooltip("Layers that can be damaged.")]
        private LayerMask targetMask = ~0;

        [SerializeField]
        [Tooltip("Optional offset applied after choosing the anchor position.")]
        private Vector2 offset = Vector2.zero;

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Amount of damage applied to each target.")]
        private int damage = 10;

        [SerializeField]
        [Tooltip("When enabled, adds a percentage of the owner's base damage to the ability damage.")]
        private bool scaleWithOwnerDamage = false;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Percentage of owner's damage to add (0.5 = 50% of owner damage added to base ability damage).")]
        private float ownerDamageScale = 1f;

        [SerializeField]
        [Tooltip("Maximum number of targets damaged per execution. 0 = unlimited.")]
        private int maxTargets = 0;

        [Header("Grow Mechanic")]
        [SerializeField]
        [Tooltip("When enabled, damage grows from center outward in rings, hitting closer targets first.")]
        private bool sequentialDamage = false;

        [SerializeField]
        [Tooltip("Duration for the damage to grow from center to full radius.")]
        private float growDuration = 0.25f;

        [SerializeField]
        [Tooltip("Thickness of each damage ring when sequential damage is enabled.")]
        private float ringThickness = 0.35f;

        [SerializeField]
        [Tooltip("Number of damage samples per second during growth.")]
        private int samplesPerSecond = 90;

        [SerializeField]
        [Tooltip("If false, each target can only be hit once during the entire grow sequence.")]
        private bool allowMultipleHitsPerTarget = false;

        [Header("Tile Damage")]
        [SerializeField]
        [Tooltip("When enabled, damages tiles in the area.")]
        private bool damageTiles = false;

        [SerializeField]
        [Min(1)]
        [Tooltip("Amount of damage applied to tiles.")]
        private int tileDamage = 1;

        readonly Collider2D[] _buffer = new Collider2D[128];

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Vector2? centerPos = ResolveAnchor(context);
            if (!centerPos.HasValue)
            {
                yield break;
            }

            Vector2 center = centerPos.Value + offset;

            // Calculate final damage amount
            int amount = damage;
            if (scaleWithOwnerDamage)
            {
                int ownerDamage = context.GetOwnerBaseDamage();
                int scaledDamage = Mathf.RoundToInt(ownerDamage * ownerDamageScale);
                amount += scaledDamage;
            }

            if (amount <= 0)
            {
                yield break;
            }

            // Choose between sequential (growing) or instant damage
            if (sequentialDamage)
            {
                yield return ApplySequentialDamage(context, center, amount);
            }
            else
            {
                ApplyInstantDamage(context, center, amount);
                TryDamageTiles(center);
            }
        }

        IEnumerator ApplySequentialDamage(AbilityRuntimeContext context, Vector2 center, int damageAmount)
        {
            float elapsed = 0f;
            float radiusPerSecond = radius / Mathf.Max(0.0001f, growDuration);
            int iterations = Mathf.Max(1, Mathf.CeilToInt(growDuration * samplesPerSecond));
            float dt = growDuration / iterations;
            float ringHalfThickness = Mathf.Max(0.001f, ringThickness * 0.5f);
            HashSet<Component> alreadyHit = allowMultipleHitsPerTarget ? null : new HashSet<Component>();

            for (int i = 0; i < iterations; i++)
            {
                float currentRadius = Mathf.Min(radius, elapsed * radiusPerSecond);
                float innerRadius = Mathf.Max(0f, currentRadius - ringHalfThickness);
                float outerRadius = Mathf.Min(radius, currentRadius + ringHalfThickness);

                ApplyRingDamage(context, center, innerRadius, outerRadius, damageAmount, alreadyHit);
                TryDamageTiles(center, outerRadius);

                elapsed += dt;
                float end = Time.time + dt;
                while (Time.time < end)
                {
                    if (context.CancelRequested) yield break;
                    yield return null;
                }
            }

            // Final pass to ensure we hit everything at full radius
            if (!allowMultipleHitsPerTarget)
            {
                ApplyRingDamage(context, center, radius - ringHalfThickness, radius, damageAmount, alreadyHit);
            }

            TryDamageTiles(center, radius);
        }

        void ApplyRingDamage(AbilityRuntimeContext context, Vector2 center, float innerRadius, float outerRadius, int damageAmount, HashSet<Component> alreadyHit)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(center, outerRadius, filter, _buffer);
            int applied = 0;

            for (int i = 0; i < count; i++)
            {
                if (maxTargets > 0 && applied >= maxTargets)
                    break;

                var col = _buffer[i];
                if (!col) continue;
                if (col.transform == context.Transform) continue; // avoid self

                // Check if target is within the ring
                Vector2 toTarget = (Vector2)col.transform.position - center;
                float dist = toTarget.magnitude;
                if (dist < innerRadius || dist > outerRadius) continue;

                // Check if already hit (if tracking)
                var damageable = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable != null)
                {
                    if (!allowMultipleHitsPerTarget && alreadyHit != null)
                    {
                        Component key = damageable as Component;
                        if (key && alreadyHit.Contains(key)) continue;
                        if (key) alreadyHit.Add(key);
                    }
                }

                if (AbilityEffectUtility.TryApplyDamage(col, damageAmount, center))
                {
                    applied++;
                }
            }
        }

        void ApplyInstantDamage(AbilityRuntimeContext context, Vector2 center, int damageAmount)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int count = Physics2D.OverlapCircle(center, radius, filter, _buffer);
            int applied = 0;

            for (int i = 0; i < count; i++)
            {
                if (maxTargets > 0 && applied >= maxTargets)
                    break;

                var col = _buffer[i];
                if (!col) continue;
                if (col.transform == context.Transform) continue; // avoid self

                if (AbilityEffectUtility.TryApplyDamage(col, damageAmount, center))
                {
                    applied++;
                }
            }
        }

        void TryDamageTiles(Vector2 center, float currentRadius = -1f)
        {
            if (!damageTiles) return;

            // Use the current radius if provided (for sequential damage), otherwise use full radius
            float damageRadius = currentRadius > 0f ? currentRadius : radius;
            AbilityEffectUtility.TryDamageTilesCircle(center, Mathf.Max(0f, damageRadius), tileDamage);
        }

        Vector2? ResolveAnchor(AbilityRuntimeContext context)
        {
            switch (anchor)
            {
                case AreaAnchor.Target:
                    {
                        Transform target = context.Target ? context.Target : context.Transform;
                        return target ? (Vector2)target.position : null;
                    }

                case AreaAnchor.Custom:
                    {
                        Transform custom = customAnchor ? customAnchor : context.Transform;
                        return custom ? (Vector2)custom.position : null;
                    }

                case AreaAnchor.MousePosition:
                    {
                        // Get mouse position in world space
                        Camera cam = Camera.main;
                        if (!cam) return context.Transform ? (Vector2)context.Transform.position : null;

                        Vector3 mouseScreenPos = Input.mousePosition;
                        // Set Z to match the owner's distance from camera for proper world conversion
                        if (context.Transform)
                        {
                            mouseScreenPos.z = cam.WorldToScreenPoint(context.Transform.position).z;
                        }

                        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(mouseScreenPos);
                        Vector2 mousePos2D = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

                        // Clamp to max range if WaitForTargetConfirmationStep set one
                        if (context.ConfirmedTargetMaxRange.HasValue && context.Transform)
                        {
                            Vector2 ownerPos = context.Transform.position;
                            Vector2 direction = (mousePos2D - ownerPos);
                            float distance = direction.magnitude;

                            if (distance > context.ConfirmedTargetMaxRange.Value)
                            {
                                mousePos2D = ownerPos + direction.normalized * context.ConfirmedTargetMaxRange.Value;
                            }
                        }

                        return mousePos2D;
                    }

                default: // Owner
                    return context.Transform ? (Vector2)context.Transform.position : null;
            }
        }
    }
}






