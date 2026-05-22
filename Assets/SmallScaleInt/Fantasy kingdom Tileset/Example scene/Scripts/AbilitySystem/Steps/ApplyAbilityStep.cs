using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
    /// <summary>
    /// Applies a passive ability (buff or debuff) to target(s).
    /// The ability is added to the target's AbilityRunner and will be automatically
    /// removed after the specified duration, or can persist permanently.
    /// </summary>
    [System.Serializable]
    [AbilityComponentDescription("Applies a passive ability (buff/debuff) to target(s), with optional duration and targeting modes.")]
    public sealed class ApplyAbilityStep : AbilityStep
    {
        public enum TargetingMode
        {
            CurrentTarget,              // Apply to context.Target (good for AI with locked target)
            Owner,                      // Apply to owner (self-buff)
            MeleeHitbox_SingleTarget,   // Melee hitbox: closest target only
            MeleeHitbox_MultiTarget,    // Melee hitbox: all targets in range
            MousePosition,              // Apply to targets at mouse position
            AllInRadius,                // Apply to all targets within radius
            AllInCone,                  // Apply to all targets in cone
            ClosestInRadius,            // Apply to closest target in radius
            RandomInRadius              // Apply to random target in radius
        }

        [Header("Abilities")]
        [SerializeField]
        [Tooltip("Passive abilities applied to each valid target.")]
        private List<AbilityDefinition> abilitiesToApply = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Duration in seconds. If > 0, the ability will be removed after this duration. If 0, the ability persists until manually removed.")]
        [Min(0f)]
        private float duration = 0f;

        [SerializeField]
        [Tooltip("If true, allows stacking the same ability multiple times on the same target.")]
        private bool allowStacking = false;

        [Header("Targeting")]
        [SerializeField]
        [Tooltip("How to select targets for ability application.")]
        private TargetingMode targetingMode = TargetingMode.CurrentTarget;

        [SerializeField]
        [Tooltip("Layers that can be targeted.")]
        private LayerMask targetMask = ~0;

        [SerializeField]
        [Tooltip("Radius for area targeting modes (AllInRadius, ClosestInRadius, RandomInRadius, AllInCone).")]
        [Min(0.1f)]
        private float radius = 5f;

        [SerializeField]
        [Tooltip("Cone angle in degrees for AllInCone mode.")]
        [Range(5f, 180f)]
        private float coneAngleDeg = 60f;

        [SerializeField]
        [Tooltip("Maximum number of targets to affect. 0 = unlimited.")]
        [Min(0)]
        private int maxTargets = 0;

        [SerializeField]
        [Tooltip("Offset applied to the targeting position.")]
        private Vector2 offset = Vector2.zero;

        private readonly Collider2D[] _buffer = new Collider2D[32];
        private readonly List<Transform> _validTargets = new List<Transform>();

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (!HasValidAbilities())
            {
                yield break;
            }

            // Gather targets based on targeting mode
            _validTargets.Clear();
            GatherTargets(context, _validTargets);

            if (_validTargets.Count == 0)
            {
                yield break;
            }

            // Apply max targets limit
            int targetsToApply = maxTargets > 0 ? Mathf.Min(maxTargets, _validTargets.Count) : _validTargets.Count;

            // Apply ability to each target
            for (int i = 0; i < targetsToApply; i++)
            {
                ApplyToTarget(_validTargets[i], context);
            }
        }

        private void GatherTargets(AbilityRuntimeContext context, List<Transform> targets)
        {
            switch (targetingMode)
            {
                case TargetingMode.CurrentTarget:
                    if (context.Target != null)
                    {
                        targets.Add(context.Target);
                    }
                    break;

                case TargetingMode.Owner:
                    targets.Add(context.Transform);
                    break;

                case TargetingMode.MeleeHitbox_SingleTarget:
                    Transform meleeTarget = MeleeTargetUtility.GetMeleeTarget(context);
                    if (meleeTarget != null)
                    {
                        targets.Add(meleeTarget);
                    }
                    break;

                case TargetingMode.MeleeHitbox_MultiTarget:
                    GatherMeleeHitboxTargets(context, targets);
                    break;

                case TargetingMode.MousePosition:
                    GatherTargetsAtMousePosition(context, targets);
                    break;

                case TargetingMode.AllInRadius:
                    GatherTargetsInRadius(context, targets, false);
                    break;

                case TargetingMode.ClosestInRadius:
                    GatherClosestTargetInRadius(context, targets);
                    break;

                case TargetingMode.RandomInRadius:
                    GatherRandomTargetInRadius(context, targets);
                    break;

                case TargetingMode.AllInCone:
                    GatherTargetsInCone(context, targets);
                    break;
            }
        }

        private void GatherTargetsAtMousePosition(AbilityRuntimeContext context, List<Transform> targets)
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 center = mouseWorldPos + offset;

            int count = OverlapWithMask(center, radius, targetMask, _buffer);
            for (int i = 0; i < count; i++)
            {
                if (_buffer[i] && _buffer[i].transform != context.Transform)
                {
                    targets.Add(_buffer[i].transform);
                }
            }
        }

        private void GatherMeleeHitboxTargets(AbilityRuntimeContext context, List<Transform> targets)
        {
            // Get melee hitbox parameters based on owner type
            Vector2 hitOffset = Vector2.zero;
            float hitRadius = 0.6f;
            LayerMask hitMask = targetMask;

            // Try to get parameters from PlayerMeleeHitbox
            var playerMeleeHitbox = context.Transform.GetComponent<PlayerMeleeHitbox>();
            if (playerMeleeHitbox != null)
            {
                hitOffset = playerMeleeHitbox.hitOffset;
                hitRadius = playerMeleeHitbox.hitRadius;
                hitMask = playerMeleeHitbox.enemyMask;
            }
            else
            {
                // Try to get parameters from EnemyAI
                var enemyAI = context.EnemyAI;
                if (enemyAI != null)
                {
                    hitOffset = enemyAI.attackHitOffset;
                    hitRadius = enemyAI.attackHitRadius;
                    hitMask = enemyAI.playerMask;
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

            // Detect all targets in melee range
            int count = OverlapWithMask(hitCenter, hitRadius, hitMask, _buffer);
            Debug.Log($"[ApplyAbilityStep] GatherMeleeHitboxTargets: Found {count} colliders in melee range. HitCenter: {hitCenter}, Radius: {hitRadius}, Mask: {hitMask.value}");

            for (int i = 0; i < count; i++)
            {
                if (_buffer[i] && _buffer[i].transform != context.Transform)
                {
                    targets.Add(_buffer[i].transform);
                    Debug.Log($"[ApplyAbilityStep] Added target: {_buffer[i].transform.name}");
                }
            }

            Debug.Log($"[ApplyAbilityStep] Total targets gathered: {targets.Count}");
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

        private void GatherTargetsInRadius(AbilityRuntimeContext context, List<Transform> targets, bool excludeSelf = true)
        {
            Vector2 center = (Vector2)context.Transform.position + offset;
            int count = OverlapWithMask(center, radius, targetMask, _buffer);

            for (int i = 0; i < count; i++)
            {
                if (_buffer[i] && (!excludeSelf || _buffer[i].transform != context.Transform))
                {
                    targets.Add(_buffer[i].transform);
                }
            }
        }

        private void GatherClosestTargetInRadius(AbilityRuntimeContext context, List<Transform> targets)
        {
            Vector2 center = (Vector2)context.Transform.position + offset;
            int count = OverlapWithMask(center, radius, targetMask, _buffer);

            Transform closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                if (_buffer[i] && _buffer[i].transform != context.Transform)
                {
                    float dist = Vector2.Distance(center, _buffer[i].transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = _buffer[i].transform;
                    }
                }
            }

            if (closest != null)
            {
                targets.Add(closest);
            }
        }

        private void GatherRandomTargetInRadius(AbilityRuntimeContext context, List<Transform> targets)
        {
            Vector2 center = (Vector2)context.Transform.position + offset;
            int count = OverlapWithMask(center, radius, targetMask, _buffer);

            var tempList = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                if (_buffer[i] && _buffer[i].transform != context.Transform)
                {
                    tempList.Add(_buffer[i].transform);
                }
            }

            if (tempList.Count > 0)
            {
                int randomIndex = Random.Range(0, tempList.Count);
                targets.Add(tempList[randomIndex]);
            }
        }

        private void GatherTargetsInCone(AbilityRuntimeContext context, List<Transform> targets)
        {
            Vector2 center = (Vector2)context.Transform.position + offset;
            Vector2 forward = context.DesiredDirection;

            if (forward.sqrMagnitude < 0.01f)
            {
                forward = (Vector2)context.Transform.right;
            }

            int count = OverlapWithMask(center, radius, targetMask, _buffer);
            float halfAngle = coneAngleDeg * 0.5f;

            for (int i = 0; i < count; i++)
            {
                if (_buffer[i] && _buffer[i].transform != context.Transform)
                {
                    Vector2 toTarget = ((Vector2)_buffer[i].transform.position - center).normalized;
                    float angle = Vector2.Angle(forward, toTarget);

                    if (angle <= halfAngle)
                    {
                        targets.Add(_buffer[i].transform);
                    }
                }
            }
        }

        private void ApplyToTarget(Transform targetTransform, AbilityRuntimeContext context)
        {
            if (!targetTransform)
                return;

            for (int i = 0; i < abilitiesToApply.Count; i++)
            {
                var ability = abilitiesToApply[i];
                if (!IsAbilityValid(ability))
                    continue;

                AbilityApplicationUtility.TryApplyAbility(ability, targetTransform, context, allowStacking, duration);
            }
        }

        bool HasValidAbilities()
        {
            if (abilitiesToApply == null || abilitiesToApply.Count == 0)
                return false;

            for (int i = 0; i < abilitiesToApply.Count; i++)
            {
                if (IsAbilityValid(abilitiesToApply[i]))
                    return true;
            }

            return false;
        }

        bool IsAbilityValid(AbilityDefinition ability)
        {
            return ability && ability.IsPassive;
        }

        private static int OverlapWithMask(Vector2 center, float radius, int layerMask, Collider2D[] buffer)
        {
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(layerMask);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            return Physics2D.OverlapCircle(center, radius, filter, buffer);
        }
    }
}







