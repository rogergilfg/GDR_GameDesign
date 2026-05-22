using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Instantly rotates the caster to face the current target, optionally waiting one frame for animator updates.")]
    [MovedFrom("AbilitySystem")]
    public sealed class FaceTargetStep : AbilityStep
    {
        private enum TargetMode
        {
            CurrentTarget,              // Use context.Target
            MeleeHitbox_SingleTarget    // Melee hitbox: closest target only
        }

        [SerializeField]
        [Tooltip("Which target to face.")]
        private TargetMode targetMode = TargetMode.CurrentTarget;

        [SerializeField]
        [Tooltip("If true, the step waits a single frame allowing other systems to update animator states.")]
        private bool waitOneFrame = false;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform target = null;

            if (targetMode == TargetMode.CurrentTarget)
            {
                target = context.Target;
            }
            else if (targetMode == TargetMode.MeleeHitbox_SingleTarget)
            {
                target = MeleeTargetUtility.GetMeleeTarget(context);
            }
            if (target)
            {
                Vector2 toTarget = target.position - context.Transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                    context.Transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            if (waitOneFrame)
            {
                yield return null;
            }
        }
    }
}





