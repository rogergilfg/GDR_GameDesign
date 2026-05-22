using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Temporarily adjusts movement speed multipliers on the caster or their target, optionally spawning VFX.")]
    [MovedFrom("AbilitySystem")]
    public sealed class MovementModifierStep : AbilityStep
    {
        private enum TargetEntity
        {
            Owner,
            Target
        }

        [SerializeField]
        [Tooltip("Who receives the movement modifier.")]
        private TargetEntity targetEntity = TargetEntity.Owner;

        [SerializeField]
        [Tooltip("Multiplier applied to walk speed.")]
        private float walkSpeedMultiplier = 1.25f;

        [SerializeField]
        [Tooltip("Multiplier applied to run speed.")]
        private float runSpeedMultiplier = 1.25f;

        [SerializeField]
        [Tooltip("Duration of the modifier in seconds.")]
        private float duration = 3f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform target = targetEntity == TargetEntity.Owner ? context.Transform : context.Target;
            if (!target)
            {
                yield break;
            }

            var controller = target.GetComponentInParent<GenericTopDownController>();
            if (!controller)
            {
                yield break;
            }

            float originalWalk = controller.walkSpeed;
            float originalRun = controller.runSpeed;

            controller.walkSpeed *= walkSpeedMultiplier;
            controller.runSpeed *= runSpeedMultiplier;

            if (duration > 0f)
            {
                float end = Time.time + duration;
                while (Time.time < end)
                {
                    if (context.CancelRequested) break;
                    yield return null;
                }
            }

            controller.walkSpeed = originalWalk;
            controller.runSpeed = originalRun;
        }
    }
}






