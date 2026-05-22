using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Blocks activation unless the target lies within a configurable minimum/maximum distance of the caster.")]
    public sealed class DistanceRequirement : AbilityRequirement
    {
        [Tooltip("If true, the requirement fails when no target is provided.")]
        [SerializeField] private bool requireTarget = true;

        [Tooltip("Minimum distance required to activate. <= 0 disables the check.")]
        [SerializeField] private float minDistance = 0f;

        [Tooltip("Maximum distance allowed to activate. <= 0 disables the check.")]
        [SerializeField] private float maxDistance = 0f;

        [Tooltip("When enabled, distances are evaluated in the X/Y plane.")]
        [SerializeField] private bool use2D = true;

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;
            var target = context.Target;
            if (!target)
            {
                if (requireTarget)
                {
                    failureReason = "No target";
                    return false;
                }

                return true;
            }

            Vector3 origin = context.Transform.position;
            Vector3 destination = target.position;
            if (use2D)
            {
                origin.z = 0f;
                destination.z = 0f;
            }

            float distance = Vector3.Distance(origin, destination);

            if (minDistance > 0f && distance < minDistance)
            {
                failureReason = $"distance {distance:F2} < {minDistance:F2}";
                return false;
            }

            if (maxDistance > 0f && distance > maxDistance)
            {
                failureReason = $"distance {distance:F2} > {maxDistance:F2}";
                return false;
            }

            return true;
        }
    }
}





