using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [PassiveModifierDescription("Increases experience earned by the player.")]
    public sealed class ExperienceBoostModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage increase to experience earned by the player (0.1 = +10% XP).")]
        [Range(0f, 2f)]
        private float playerExperiencePercent = 0.1f;

        private float _appliedMultiplier;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            if (runner.OwnerKind == AbilityActorKind.Player && playerExperiencePercent > 0f)
            {
                _appliedMultiplier = 1f + playerExperiencePercent;
                var experience = runner.CachedPlayerExperience ?? PlayerExperience.Instance;
                if (experience != null)
                {
                    experience.SetExternalMultiplier(_appliedMultiplier);
                }
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            if (_appliedMultiplier > 0f)
            {
                var experience = runner.CachedPlayerExperience ?? PlayerExperience.Instance;
                if (experience != null)
                {
                    experience.SetExternalMultiplier(1f);
                }
                _appliedMultiplier = 0f;
            }
        }
    }
}






