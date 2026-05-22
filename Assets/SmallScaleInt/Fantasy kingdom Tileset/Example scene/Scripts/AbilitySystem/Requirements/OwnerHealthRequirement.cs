using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Restricts activation based on the caster's current health percentage, with optional requirement for a health component.")]
    public sealed class OwnerHealthRequirement : AbilityRequirement
    {
        [SerializeField]
        [Tooltip("Minimum owner health percentage (0-1) required to pass.")]
        private float minHealthPercent = 0f;

        [SerializeField]
        [Tooltip("Maximum owner health percentage (0-1) allowed to pass.")]
        private float maxHealthPercent = 1f;

        [SerializeField]
        [Tooltip("Fail if no compatible health component is found.")]
        private bool requireHealthComponent = true;

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;

            if (!context.Owner)
            {
                failureReason = "No owner";
                return false;
            }

            float ratio = GetHealthRatio(context);
            if (ratio < 0f)
            {
                if (requireHealthComponent)
                {
                    failureReason = "No health component";
                    return false;
                }
                return true;
            }

            if (ratio < minHealthPercent)
            {
                failureReason = "Owner health below minimum";
                return false;
            }

            if (ratio > maxHealthPercent)
            {
                failureReason = "Owner health above maximum";
                return false;
            }

            return true;
        }

        static float GetHealthRatio(AbilityRuntimeContext context)
        {
            if (context == null)
                return -1f;

            float ratio = GetContextHealthRatio(context);
            if (ratio >= 0f)
                return ratio;

            return GetHealthRatioFromTransform(context.Owner ? context.Owner.transform : null);
        }

        static float GetContextHealthRatio(AbilityRuntimeContext context)
        {
            if (context.EnemyHealth != null)
            {
                if (context.EnemyHealth.IsDead) return 0f;
                return ComputeRatio(context.EnemyHealth.CurrentHealth, context.EnemyHealth.MaxHealth);
            }

            if (context.PlayerHealth != null)
            {
                if (PlayerHealth.IsPlayerDead) return 0f;
                return ComputeRatio(context.PlayerHealth.currentHealth, context.PlayerHealth.maxHealth);
            }

            if (context.NeutralAI != null)
            {
                int max = Mathf.Max(1, context.NeutralAI.maxHealth);
                int current = Mathf.Clamp(context.NeutralAI.CurrentHealth, 0, max);
                return ComputeRatio(current, max);
            }

            if (context.Owner)
            {
                var companion = context.Owner.GetComponentInParent<CompanionHealth>();
                if (companion != null)
                {
                    return companion.IsDead ? 0f : ComputeRatio(companion.currentHealth, companion.maxHealth);
                }
            }

            return -1f;
        }

        static float GetHealthRatioFromTransform(Transform transform)
        {
            if (!transform) return -1f;

            var companionHealth = transform.GetComponentInParent<CompanionHealth>();
            if (companionHealth)
            {
                return companionHealth.IsDead ? 0f : ComputeRatio(companionHealth.currentHealth, companionHealth.maxHealth);
            }

            var neutral = transform.GetComponentInParent<NeutralNpcAI>();
            if (neutral)
            {
                return ComputeRatio(Mathf.Max(0, neutral.CurrentHealth), Mathf.Max(1, neutral.maxHealth));
            }

            var enemyHealth = transform.GetComponentInParent<EnemyHealth2D>();
            if (enemyHealth)
            {
                if (enemyHealth.IsDead) return 0f;
                return ComputeRatio(enemyHealth.CurrentHealth, enemyHealth.MaxHealth);
            }

            var playerHealth = transform.GetComponentInParent<PlayerHealth>();
            if (playerHealth)
            {
                if (PlayerHealth.IsPlayerDead) return 0f;
                return ComputeRatio(playerHealth.currentHealth, playerHealth.maxHealth);
            }

            return -1f;
        }

        static float ComputeRatio(int current, int max)
        {
            return Mathf.Clamp01(current / (float)Mathf.Max(1, max));
        }
    }
}






