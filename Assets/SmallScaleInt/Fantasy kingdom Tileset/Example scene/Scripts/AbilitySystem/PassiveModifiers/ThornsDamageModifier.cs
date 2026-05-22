using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [PassiveModifierDescription("Reflects damage back to attackers when hit (thorns effect).")]
    public sealed class ThornsDamageModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage of incoming damage reflected back to attackers (0.1 = 10% thorns).")]
        [Range(0f, 1f)]
        private float thornDamagePercent = 0.1f;

        [SerializeField]
        [Tooltip("Flat amount of damage reflected back to attackers on each hit.")]
        [Min(0)]
        private int thornDamageFlat = 0;

        private float _appliedPercent;
        private int _appliedFlat;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            if (thornDamagePercent > 0f || thornDamageFlat > 0)
            {
                _appliedPercent = thornDamagePercent;
                _appliedFlat = thornDamageFlat;

                var playerStats = runner.CachedPlayerStats;
                if (playerStats != null)
                {
                    playerStats.SetThornDamagePercent(thornDamagePercent);
                    playerStats.SetThornDamageFlat(thornDamageFlat);
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
                    playerStats.SetThornDamagePercent(0f);
                    playerStats.SetThornDamageFlat(0);
                }

                _appliedPercent = 0f;
                _appliedFlat = 0;
            }
        }
    }
}






