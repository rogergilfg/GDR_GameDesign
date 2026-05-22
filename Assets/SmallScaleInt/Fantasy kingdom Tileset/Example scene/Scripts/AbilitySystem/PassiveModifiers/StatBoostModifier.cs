using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [PassiveModifierDescription("Provides flat stat bonuses to player stats (health, defense, damage, crit, etc.)")]
    [MovedFrom("AbilitySystem")]
    public sealed class StatBoostModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Flat stat bonuses applied while this passive is active.")]
        private PlayerStats.PlayerStatBonus statBonus;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            var playerStats = runner.CachedPlayerStats;
            if (playerStats != null && !statBonus.IsZero)
            {
                playerStats.ApplyTemporaryStatBonus(statBonus);
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            var playerStats = runner.CachedPlayerStats;
            if (playerStats != null && !statBonus.IsZero)
            {
                playerStats.ApplyTemporaryStatBonus(statBonus.Negated());
            }
        }
    }
}






