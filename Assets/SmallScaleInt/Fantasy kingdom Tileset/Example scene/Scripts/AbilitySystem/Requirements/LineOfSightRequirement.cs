using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Ensures there is an unobstructed line between caster and target using 2D or 3D physics raycasts.")]
    public sealed class LineOfSightRequirement : AbilityRequirement
    {
        [SerializeField]
        [Tooltip("Layers considered as blockers between the owner and the target.")]
        private LayerMask blockerMask = ~0;

        [SerializeField]
        [Tooltip("Optional maximum distance. <= 0 falls back to the actual target distance.")]
        private float maxDistance = 0f;

        [SerializeField]
        [Tooltip("Offset from the owner's position when starting the ray cast.")]
        private Vector3 ownerOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Offset applied to the target's position.")]
        private Vector3 targetOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("When true, Physics2D is used. Otherwise a 3D ray cast is executed.")]
        private bool usePhysics2D = true;

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;

            var target = context.Target;
            if (!target)
            {
                failureReason = "No target";
                return false;
            }

            Vector3 origin = context.Transform.position + ownerOffset;
            Vector3 destination = target.position + targetOffset;
            Vector3 dir = destination - origin;
            float distance = dir.magnitude;
            if (maxDistance > 0f)
            {
                distance = Mathf.Min(distance, maxDistance);
                dir = dir.normalized * distance;
            }

            if (distance <= 0.0001f) return true;

            if (usePhysics2D)
            {
                RaycastHit2D hit = Physics2D.Raycast(origin, dir.normalized, distance, blockerMask);
                if (hit.collider != null && !IsSameTransform(hit.collider.transform, context.Transform))
                {
                    failureReason = $"LOS blocked by {hit.collider.name}";
                    return false;
                }
            }
            else
            {
                if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, distance, blockerMask, QueryTriggerInteraction.Ignore))
                {
                    if (!IsSameTransform(hit.transform, context.Transform))
                    {
                        failureReason = $"LOS blocked by {hit.collider.name}";
                        return false;
                    }
                }
            }

            return true;
        }

        static bool IsSameTransform(Transform a, Transform b)
        {
            if (!a || !b) return false;
            return a == b || a.IsChildOf(b) || b.IsChildOf(a);
        }
    }
}







