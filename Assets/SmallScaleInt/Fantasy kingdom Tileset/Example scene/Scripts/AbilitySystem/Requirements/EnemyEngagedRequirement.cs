using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Allows activation only when the enemy AI has a target inside its leash radius (optionally requiring an EnemyAI component).")]
    public sealed class EnemyEngagedRequirement : AbilityRequirement
    {
        [Tooltip("If true the requirement fails when no EnemyAI is present.")]
        [SerializeField] private bool requireEnemyAi = true;

        [Tooltip("Optional override leash radius. <= 0 uses EnemyAI.leashRadius.")]
        [SerializeField] private float leashRadiusOverride = 0f;

        protected override bool ShouldEvaluateForOwner(AbilityActorKind ownerKind)
        {
            return ownerKind == AbilityActorKind.Enemy;
        }

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;
            EnemyAI ai = context.EnemyAI;

            if (!ai)
            {
                if (requireEnemyAi)
                {
                    failureReason = "No EnemyAI";
                    return false;
                }

                return true;
            }

            Transform player = ai.player != null ? ai.player : context.Target;
            if (!player)
            {
                failureReason = "No target";
                return false;
            }

            float leash = leashRadiusOverride > 0f ? leashRadiusOverride : Mathf.Max(0.01f, ai.leashRadius);
            float dist = Vector2.Distance(ai.transform.position, player.position);
            if (dist > leash)
            {
                failureReason = $"distance {dist:F2} > leash {leash:F2}";
                return false;
            }

            return true;
        }
    }
}





