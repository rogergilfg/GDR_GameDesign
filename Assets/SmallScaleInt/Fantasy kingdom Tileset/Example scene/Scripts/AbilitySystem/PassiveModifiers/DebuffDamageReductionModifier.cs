using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Debuff that reduces the target's damage output by a percentage.
    /// Works on enemy targets only (reduces EnemyAI damage multiplier).
    /// For player debuffs, use DebuffStatReductionModifier to reduce WeaponDamage stat.
    /// Has built-in duration support with optional refresh on reapply.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Reduces enemy target's damage output by a percentage (debuff).")]
    public sealed class DebuffDamageReductionModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage reduction to damage (0.5 = 50% damage reduction, 1.0 = 100% damage reduction).")]
        [Range(0f, 1f)]
        private float damageReductionPercent = 0.3f;

        [SerializeField]
        [Tooltip("Duration in seconds. If > 0, debuff will auto-remove after duration. If 0, debuff is permanent.")]
        [Min(0f)]
        private float duration = 5f;

        [SerializeField]
        [Tooltip("If true, reapplying this debuff will reset the duration timer.")]
        private bool refreshDuration = true;

        private float _originalMultiplier;
        private bool _isApplied;
        private float _expirationTime;
        private AbilityRunner _runner;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled || damageReductionPercent <= 0f) return;

            _runner = runner;

            // Apply to enemy only
            if (runner.CachedEnemyAI != null)
            {
                if (!_isApplied)
                {
                    _originalMultiplier = runner.CachedEnemyAI.damageMultiplier;
                    _isApplied = true;
                }
                else if (refreshDuration && duration > 0f)
                {
                    // Already applied, just refresh duration
                    _expirationTime = Time.time + duration;
                    return;
                }

                // Reduce damage by the percentage (e.g., 0.3 reduction = multiply by 0.7)
                runner.CachedEnemyAI.damageMultiplier = _originalMultiplier * (1f - damageReductionPercent);
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

            // Restore enemy damage
            if (runner.CachedEnemyAI != null)
            {
                runner.CachedEnemyAI.damageMultiplier = _originalMultiplier;
            }

            _isApplied = false;
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






