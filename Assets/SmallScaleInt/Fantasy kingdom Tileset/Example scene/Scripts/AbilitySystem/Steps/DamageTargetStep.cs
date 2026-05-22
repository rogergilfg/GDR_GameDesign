using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    public enum DamageTargetMode
    {
        CurrentTarget,              // Use context.Target
        MeleeHitbox_SingleTarget,   // Melee hitbox: closest target only
        MeleeHitbox_MultiTarget     // Melee hitbox: all targets in range
    }

    [System.Serializable]
    [AbilityComponentDescription("Applies a single instance of damage to the current target, optionally using the caster's facing or desired direction.")]
    [MovedFrom("AbilitySystem")]
    public sealed class DamageTargetStep : AbilityStep
    {
        [Header("Targeting")]
        [SerializeField]
        [Tooltip("How to determine which target(s) to damage.")]
        private DamageTargetMode targetingMode = DamageTargetMode.CurrentTarget;

        [Header("Damage")]
        [SerializeField]
        [Tooltip("Damage applied to the current target.")]
        private int damage = 10;

        [SerializeField]
        [Tooltip("When enabled, adds a percentage of the owner's base damage to the ability damage.")]
        private bool scaleWithOwnerDamage = false;

        [SerializeField]
        [Range(0f, 2f)]
        [Tooltip("Percentage of owner's damage to add (0.5 = 50% of owner damage added to base ability damage).")]
        private float ownerDamageScale = 1f;

        [SerializeField]
        [Tooltip("Fallback to damaging the owner when no target exists (only for CurrentTarget mode).")]
        private bool damageOwnerIfNoTarget = false;

        [SerializeField]
        [Tooltip("If true, the direction is computed from the owner to the target. Otherwise use the owner's forward direction.")]
        private bool useRelativeDirection = true;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (targetingMode == DamageTargetMode.CurrentTarget)
            {
                yield return ExecuteCurrentTargetMode(context);
            }
            else if (targetingMode == DamageTargetMode.MeleeHitbox_SingleTarget)
            {
                yield return ExecuteMeleeHitboxTargetMode(context);
            }
            else if (targetingMode == DamageTargetMode.MeleeHitbox_MultiTarget)
            {
                yield return ExecuteMeleeHitboxMode(context);
            }
        }

        private IEnumerator ExecuteMeleeHitboxTargetMode(AbilityRuntimeContext context)
        {
            Transform target = MeleeTargetUtility.GetMeleeTarget(context);
            if (!target)
            {
                yield break;
            }

            Vector2 direction;
            if (useRelativeDirection)
            {
                direction = ((Vector2)target.position - (Vector2)context.Transform.position).normalized;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = context.Transform.right;
                }
            }
            else
            {
                direction = context.Transform.right;
            }

            int amount = CalculateDamage(context);

            if (amount > 0)
            {
                AbilityEffectUtility.TryApplyDamage(target, amount, direction);
            }
            yield break;
        }

        private IEnumerator ExecuteCurrentTargetMode(AbilityRuntimeContext context)
        {
            Transform target = context.Target;
            if (!target && damageOwnerIfNoTarget)
            {
                target = context.Transform;
            }

            if (!target)
            {
                yield break;
            }

            Vector2 direction;
            if (useRelativeDirection)
            {
                direction = ((Vector2)target.position - (Vector2)context.Transform.position).normalized;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = context.Transform.right;
                }
            }
            else
            {
                direction = context.Transform.right;
            }

            int amount = CalculateDamage(context);

            if (amount > 0)
            {
                AbilityEffectUtility.TryApplyDamage(target, amount, direction);
            }
            yield break;
        }

        private IEnumerator ExecuteMeleeHitboxMode(AbilityRuntimeContext context)
        {
            // Get melee hitbox parameters based on owner type
            Vector2 hitOffset = Vector2.zero;
            float hitRadius = 0.6f;
            LayerMask targetMask = ~0;

            // Try to get parameters from PlayerMeleeHitbox
            var playerMeleeHitbox = context.Transform.GetComponent<PlayerMeleeHitbox>();
            if (playerMeleeHitbox != null)
            {
                hitOffset = playerMeleeHitbox.hitOffset;
                hitRadius = playerMeleeHitbox.hitRadius;
                targetMask = playerMeleeHitbox.enemyMask;
            }
            else
            {
                // Try to get parameters from EnemyAI
                var enemyAI = context.EnemyAI;
                if (enemyAI != null)
                {
                    hitOffset = enemyAI.attackHitOffset;
                    hitRadius = enemyAI.attackHitRadius;
                    targetMask = enemyAI.playerMask;
                }
            }

            // Calculate hit center based on facing direction
            Vector2 forward = context.DesiredDirection;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = context.Transform.right;
            }

            // Rotate offset based on forward direction
            Vector2 rotatedOffset = RotateVector2(hitOffset, forward);
            Vector2 hitCenter = (Vector2)context.Transform.position + rotatedOffset;

            // Detect targets in melee range
            var hits = Physics2D.OverlapCircleAll(hitCenter, hitRadius, targetMask);
            int amount = CalculateDamage(context);

            if (amount > 0)
            {
                foreach (var h in hits)
                {
                    if (!h) continue;

                    // Calculate damage direction toward each target
                    Vector2 direction = ((Vector2)h.transform.position - (Vector2)context.Transform.position).normalized;
                    if (direction.sqrMagnitude < 0.0001f)
                    {
                        direction = forward;
                    }

                    AbilityEffectUtility.TryApplyDamage(h.transform, amount, direction);
                }
            }

            yield break;
        }

        private int CalculateDamage(AbilityRuntimeContext context)
        {
            int amount = damage;

            // Apply owner damage scaling if enabled
            if (scaleWithOwnerDamage)
            {
                int ownerDamage = context.GetOwnerBaseDamage();
                int scaledDamage = Mathf.RoundToInt(ownerDamage * ownerDamageScale);
                amount += scaledDamage;
            }

            return amount;
        }

        private Vector2 RotateVector2(Vector2 v, Vector2 forward)
        {
            // Calculate angle from right (1, 0) to forward direction
            float angle = Mathf.Atan2(forward.y, forward.x);
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Rotate the vector
            return new Vector2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }
    }
}





