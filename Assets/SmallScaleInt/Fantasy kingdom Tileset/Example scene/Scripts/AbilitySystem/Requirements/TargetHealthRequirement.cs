using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Validates the target's current health percentage against min/max thresholds before activation.")]
    public sealed class TargetHealthRequirement : AbilityRequirement
    {
        [SerializeField]
        [Tooltip("Minimum health percentage required on the current target.")]
        private float minHealthPercent = 0f;

        [SerializeField]
        [Tooltip("Maximum health percentage allowed on the current target.")]
        private float maxHealthPercent = 1f;

        [SerializeField]
        [Tooltip("If true, fail when no target or health component is available.")]
        private bool requireValidTarget = true;

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;
            Transform target = context.Target;
            if (!target)
            {
                if (requireValidTarget)
                {
                    failureReason = "No target";
                    return false;
                }
                return true;
            }

            float ratio = GetHealthRatio(target);
            if (ratio < 0f)
            {
                if (requireValidTarget)
                {
                    failureReason = "Target missing health";
                    return false;
                }
                return true;
            }

            if (ratio < minHealthPercent)
            {
                failureReason = "Target health below minimum";
                return false;
            }

            if (ratio > maxHealthPercent)
            {
                failureReason = "Target health above maximum";
                return false;
            }

            return true;
        }

        static float GetHealthRatio(Transform transform)
        {
            if (!transform) return -1f;

            var enemyHealth = transform.GetComponentInParent<EnemyHealth2D>();
            if (enemyHealth)
            {
                if (enemyHealth.IsDead) return 0f;
                return Mathf.Clamp01(enemyHealth.CurrentHealth / (float)Mathf.Max(1, enemyHealth.MaxHealth));
            }

            var playerHealth = transform.GetComponentInParent<PlayerHealth>();
            if (playerHealth)
            {
                if (PlayerHealth.IsPlayerDead) return 0f;
                return Mathf.Clamp01(playerHealth.currentHealth / (float)Mathf.Max(1, playerHealth.maxHealth));
            }

            return -1f;
        }
    }
}






