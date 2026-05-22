using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Requires the caster to be facing the target based on a dot product check against a chosen forward source.")]
    public sealed class FacingRequirement : AbilityRequirement
    {
        private enum ForwardSource
        {
            TransformRight,
            RigidbodyVelocity,
            DesiredDirection,
            ToTarget
        }

        [SerializeField]
        [Tooltip("Source vector used as the forward direction when evaluating the dot product.")]
        private ForwardSource forwardSource = ForwardSource.TransformRight;

        [SerializeField]
        [Tooltip("Minimum dot product between forward and the direction to the target. Range -1..1.")]
        [Range(-1f, 1f)] private float minimumDot = 0f;

        [SerializeField]
        [Tooltip("If true the requirement fails when no target is available.")]
        private bool requireTarget = true;

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;
            Vector2 forward = GetForwardVector(context);
            if (forward.sqrMagnitude < 0.0001f)
            {
                failureReason = "Forward vector too small";
                return false;
            }

            Transform target = context.Target;
            if (!target)
            {
                if (requireTarget)
                {
                    failureReason = "No target";
                    return false;
                }

                return true;
            }

            Vector2 toTarget = ((Vector2)target.position - (Vector2)context.Transform.position).normalized;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            float dot = Vector2.Dot(forward.normalized, toTarget);
            if (dot < minimumDot)
            {
                failureReason = $"dot {dot:F2} < {minimumDot:F2}";
                return false;
            }

            return true;
        }

        Vector2 GetForwardVector(AbilityRuntimeContext context)
        {
            switch (forwardSource)
            {
                case ForwardSource.RigidbodyVelocity:
                    if (context.Rigidbody2D)
                    {
#if UNITY_2022_2_OR_NEWER
                        var vel = context.Rigidbody2D.linearVelocity;
#else
                        var vel = context.Rigidbody2D.velocity;
#endif
                        if (vel.sqrMagnitude > 0.0001f) return vel;
                    }
                    break;
                case ForwardSource.DesiredDirection:
                    if (context.DesiredDirection.sqrMagnitude > 0.0001f)
                        return context.DesiredDirection.normalized;
                    break;
                case ForwardSource.ToTarget:
                    if (context.Target)
                        return ((Vector2)context.Target.position - (Vector2)context.Transform.position).normalized;
                    break;
            }

            // Fallback
            return context.Transform.right;
        }
    }
}





