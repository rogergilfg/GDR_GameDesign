using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [PassiveModifierDescription("Heals the owner for a percentage of damage dealt (lifesteal effect).")]
    public sealed class LifestealModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage of damage dealt that is returned as health (0.1 = 10% lifesteal).")]
        [Range(0f, 1f)]
        private float lifestealPercent = 0.1f;

        [SerializeField]
        [Tooltip("Flat amount of health restored per hit regardless of damage dealt.")]
        [Min(0)]
        private int lifestealFlat = 0;

        [SerializeField]
        [Tooltip("Maximum health that can be restored per hit (0 = no limit).")]
        [Min(0)]
        private int lifestealCap = 0;

        [SerializeField]
        [Tooltip("If true, lifesteal only works on living targets (doesn't heal from hitting destructibles).")]
        private bool lifestealLivingOnly = true;

        private float _appliedPercent;
        private int _appliedFlat;
        private int _appliedCap;
        private bool _appliedLivingOnly;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            if (lifestealPercent > 0f || lifestealFlat > 0)
            {
                _appliedPercent = lifestealPercent;
                _appliedFlat = lifestealFlat;
                _appliedCap = lifestealCap;
                _appliedLivingOnly = lifestealLivingOnly;

                var playerStats = runner.CachedPlayerStats;
                if (playerStats != null)
                {
                    playerStats.SetLifestealPercent(lifestealPercent);
                    playerStats.SetLifestealFlat(lifestealFlat);
                    playerStats.SetLifestealCap(lifestealCap);
                    playerStats.SetLifestealLivingOnly(lifestealLivingOnly);
                }
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            if (_appliedPercent > 0f || _appliedFlat > 0)
            {
                var playerStats = runner.CachedPlayerStats;
                if (playerStats != null)
                {
                    playerStats.SetLifestealPercent(0f);
                    playerStats.SetLifestealFlat(0);
                    playerStats.SetLifestealCap(0);
                    playerStats.SetLifestealLivingOnly(false);
                }

                _appliedPercent = 0f;
                _appliedFlat = 0;
                _appliedCap = 0;
                _appliedLivingOnly = false;
            }
        }
    }
}






