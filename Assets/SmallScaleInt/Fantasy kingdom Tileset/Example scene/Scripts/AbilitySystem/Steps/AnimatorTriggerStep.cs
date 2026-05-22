using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Fires a trigger on the caster's Animator to drive a specific animation state or effect.")]
    [MovedFrom("AbilitySystem")]
    public sealed class AnimatorTriggerStep : AbilityStep
    {
        private enum AnimatorCommand
        {
            SetTrigger,
            ResetTrigger,
            SetBoolTrue,
            SetBoolFalse
        }

        [SerializeField]
        [Tooltip("Animator command sent when this step executes.")]
        private AnimatorCommand command = AnimatorCommand.SetTrigger;

        [SerializeField]
        [Tooltip("Animator parameter affected by this step.")]
        private string parameterName = "Ability";

        [SerializeField]
        [Tooltip("Alternative trigger used while the owner is moving. Leave blank to reuse Parameter Name.")]
        private string movingParameterName = string.Empty;

        [SerializeField]
        [Tooltip("Squared velocity threshold (units^2) used to decide if the owner is moving when no controller is present.")]
        private float movementThresholdSqr = 0.01f;

        [SerializeField]
        [Tooltip("Optional wait after sending the command (so the animation can play).")]
        private float postDelay = 0f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            var animator = context.Animator;
            if (animator)
            {
                string parameterToUse = ResolveParameterName(context);
                if (!string.IsNullOrEmpty(parameterToUse))
                {
                    switch (command)
                    {
                        case AnimatorCommand.SetTrigger:
                            animator.SetTrigger(parameterToUse);
                            break;
                        case AnimatorCommand.ResetTrigger:
                            animator.ResetTrigger(parameterToUse);
                            break;
                        case AnimatorCommand.SetBoolTrue:
                            animator.SetBool(parameterToUse, true);
                            break;
                        case AnimatorCommand.SetBoolFalse:
                            animator.SetBool(parameterToUse, false);
                            break;
                    }
                }
            }

            if (postDelay > 0f)
            {
                float end = Time.time + postDelay;
                while (Time.time < end)
                {
                    if (context.CancelRequested) yield break;
                    yield return null;
                }
            }
        }

        string ResolveParameterName(AbilityRuntimeContext context)
        {
            if (string.IsNullOrEmpty(parameterName))
            {
                return null;
            }

            bool isMoving = IsOwnerMoving(context);
            if (!string.IsNullOrEmpty(movingParameterName) && isMoving)
            {
                return movingParameterName;
            }

            return parameterName;
        }

        bool IsOwnerMoving(AbilityRuntimeContext context)
        {
            // First priority: Check the controller's isMoving field if available
            var controller = context.TopDownController;
            if (controller != null)
            {
                return controller.isMoving;
            }

            // Second priority: Check EnemyAI's isMoving field
            if (context.EnemyAI != null)
            {
                return context.EnemyAI.isMoving;
            }

            // Third priority: Check NeutralNpcAI's isMoving field
            if (context.NeutralAI != null)
            {
                return context.NeutralAI.isMoving;
            }

            // Fallback: Check Rigidbody2D velocity for entities without controllers
            Rigidbody2D rb = context.Rigidbody2D;

            if (!rb && context.Transform)
            {
                context.Transform.TryGetComponent(out rb);
            }

            if (rb)
            {
#if UNITY_2022_2_OR_NEWER
                Vector2 vel = rb.linearVelocity;
#else
                Vector2 vel = rb.velocity;
#endif
                if (vel.sqrMagnitude > movementThresholdSqr)
                {
                    return true;
                }
            }

            // Final fallback to DesiredDirection (for entities without any movement system)
            return context.DesiredDirection.sqrMagnitude > movementThresholdSqr;
        }
    }
}





