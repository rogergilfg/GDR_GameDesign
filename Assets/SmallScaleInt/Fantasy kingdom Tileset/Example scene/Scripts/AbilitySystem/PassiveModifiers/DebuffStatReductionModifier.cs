using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Debuff that reduces the target's stats by a flat amount.
    /// Only works on player targets (enemies don't use the PlayerStats system).
    /// Has built-in duration support with optional refresh on reapply.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Reduces target's stats by a flat amount (debuff).")]
    public sealed class DebuffStatReductionModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Flat stat reductions applied to the target.")]
        private PlayerStats.PlayerStatBonus statReduction;

        [SerializeField]
        [Tooltip("Duration in seconds. If > 0, debuff will auto-remove after duration. If 0, debuff is permanent.")]
        [Min(0f)]
        private float duration = 5f;

        [SerializeField]
        [Tooltip("If true, reapplying this debuff will reset the duration timer.")]
        private bool refreshDuration = true;

        private bool _isApplied;
        private float _expirationTime;
        private AbilityRunner _runner;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            _runner = runner;
            var playerStats = runner.CachedPlayerStats;
            if (playerStats == null || statReduction.IsZero) return;

            if (!_isApplied)
            {
                // Apply negative bonuses (reductions)
                playerStats.ApplyTemporaryStatBonus(statReduction.Negated());
                _isApplied = true;
            }
            else if (refreshDuration && duration > 0f)
            {
                // Already applied, just refresh duration
                _expirationTime = Time.time + duration;
                return;
            }

            // Set expiration time if duration is set
            if (duration > 0f)
            {
                _expirationTime = Time.time + duration;
            }
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled || !_isApplied) return;

            var playerStats = runner.CachedPlayerStats;
            if (playerStats != null && !statReduction.IsZero)
            {
                // Remove the negative bonuses (restore stats)
                playerStats.ApplyTemporaryStatBonus(statReduction);
                _isApplied = false;
            }

            _runner = null;
        }

        /// <summary>
        /// Called every frame to check duration expiration.
        /// </summary>
        public void Update()
        {
            if (!_isApplied || duration <= 0f || _runner == null) return;

            if (Time.time >= _expirationTime)
            {
                Remove(_runner);
            }
        }
    }
}






