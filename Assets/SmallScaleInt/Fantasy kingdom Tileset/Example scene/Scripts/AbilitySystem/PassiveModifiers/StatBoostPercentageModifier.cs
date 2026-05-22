using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Percentage-based stat bonuses structure.
    /// Each value represents a percentage multiplier (e.g., 0.5 = 50% increase, 1.0 = 100% increase).
    /// </summary>
    [System.Serializable]
    public struct PercentageStatBonus
    {
        [Tooltip("Percentage increase to Strength (0.5 = 50% increase).")]
        [Range(0f, 10f)]
        public float StrengthPercent;

        [Tooltip("Percentage increase to Defense (0.5 = 50% increase).")]
        [Range(0f, 10f)]
        public float DefensePercent;

        [Tooltip("Percentage increase to Health (0.5 = 50% increase).")]
        [Range(0f, 10f)]
        public float HealthPercent;

        [Tooltip("Percentage increase to Intelligence (0.5 = 50% increase).")]
        [Range(0f, 10f)]
        public float IntelligencePercent;

        [Tooltip("Percentage increase to Knowledge (0.5 = 50% increase).")]
        [Range(0f, 10f)]
        public float KnowledgePercent;

        [Tooltip("Percentage increase to Weapon Damage (0.5 = 50% increase).")]
        [Range(0f, 10f)]
        public float WeaponDamagePercent;

        [Tooltip("Percentage increase applied to the owner's total max health after all other modifiers (0.5 = +50% max HP).")]
        [Range(0f, 10f)]
        public float TotalHealthPercent;

        public bool HasStatBonuses =>
            StrengthPercent != 0f ||
            DefensePercent != 0f ||
            HealthPercent != 0f ||
            IntelligencePercent != 0f ||
            KnowledgePercent != 0f ||
            WeaponDamagePercent != 0f;

        public bool HasTotalHealthBonus => TotalHealthPercent != 0f;

        public bool IsZero =>
            !HasStatBonuses &&
            !HasTotalHealthBonus;
    }

    [System.Serializable]
    [PassiveModifierDescription("Provides percentage-based stat bonuses (e.g., +50% damage, +25% health).")]
    public sealed class StatBoostPercentageModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Percentage-based stat bonuses applied while this passive is active.")]
        private PercentageStatBonus percentageBonus;

        private PlayerStats.PlayerStatBonus _appliedBonus;
        private bool _isApplied;
        private PlayerStats _playerStats;
        private PlayerHealth _playerHealth;
        private int _appliedTotalHealthBonus;
        private bool _statsSubscribed;
        private bool _suppressStatsChanged;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled || percentageBonus.IsZero) return;

            _playerStats = runner.CachedPlayerStats ? runner.CachedPlayerStats : PlayerStats.Instance;
            _playerHealth = runner.CachedPlayerHealth ? runner.CachedPlayerHealth : PlayerHealth.Instance;

            bool hasStatTargets = _playerStats != null && percentageBonus.HasStatBonuses;
            bool hasHealthTarget = _playerHealth != null && percentageBonus.HasTotalHealthBonus;

            if (!hasStatTargets && !hasHealthTarget)
            {
                _playerStats = null;
                _playerHealth = null;
                return;
            }

            if (_playerStats != null && (percentageBonus.HasStatBonuses || percentageBonus.HasTotalHealthBonus))
            {
                _playerStats.StatsChanged += HandleStatsChanged;
                _statsSubscribed = true;
            }
            else
            {
                _statsSubscribed = false;
            }

            RecalculateBonus();
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            if (_statsSubscribed && _playerStats != null)
            {
                _playerStats.StatsChanged -= HandleStatsChanged;
            }
            _statsSubscribed = false;

            RemoveAppliedBonus();
            RemoveTotalHealthBonus();

            _playerStats = null;
            _playerHealth = null;
        }

        void HandleStatsChanged(PlayerStats.StatSnapshot snapshot)
        {
            if (_suppressStatsChanged)
            {
                return;
            }

            RecalculateBonus();
        }

        void RecalculateBonus()
        {
            RemoveAppliedBonus();
            RemoveTotalHealthBonus();

            if (_playerStats != null && percentageBonus.HasStatBonuses)
            {
                var snapshot = _playerStats.CurrentStats;
                _appliedBonus = new PlayerStats.PlayerStatBonus
                {
                    Strength = Mathf.RoundToInt(snapshot.Strength * percentageBonus.StrengthPercent),
                    Defense = Mathf.RoundToInt(snapshot.Defense * percentageBonus.DefensePercent),
                    Health = Mathf.RoundToInt(snapshot.Health * percentageBonus.HealthPercent),
                    Intelligence = Mathf.RoundToInt(snapshot.Intelligence * percentageBonus.IntelligencePercent),
                    Knowledge = Mathf.RoundToInt(snapshot.Knowledge * percentageBonus.KnowledgePercent),
                    WeaponDamage = Mathf.RoundToInt(snapshot.WeaponDamage * percentageBonus.WeaponDamagePercent)
                };

                ApplyBonus();
            }
            else
            {
                _appliedBonus = default;
                _isApplied = false;
            }

            ApplyTotalHealthBonus();
        }

        void ApplyBonus()
        {
            if (_playerStats == null || _appliedBonus.IsZero)
            {
                _isApplied = false;
                return;
            }

            _suppressStatsChanged = true;
            _playerStats.ApplyTemporaryStatBonus(_appliedBonus);
            _suppressStatsChanged = false;
            _isApplied = true;
        }

        void RemoveAppliedBonus()
        {
            if (!_isApplied || _playerStats == null || _appliedBonus.IsZero)
            {
                _isApplied = false;
                return;
            }

            _suppressStatsChanged = true;
            _playerStats.ApplyTemporaryStatBonus(_appliedBonus.Negated());
            _suppressStatsChanged = false;
            _isApplied = false;
        }

        void ApplyTotalHealthBonus()
        {
            if (_playerHealth == null) return;
            if (percentageBonus.TotalHealthPercent <= 0f) return;

            int baseMax = Mathf.Max(1, _playerHealth.maxHealth);
            float ratio = baseMax > 0 ? (float)_playerHealth.currentHealth / baseMax : 1f;
            int bonus = Mathf.RoundToInt(baseMax * percentageBonus.TotalHealthPercent);
            if (bonus <= 0) return;

            _playerHealth.maxHealth = baseMax + bonus;
            if (PlayerHealth.IsPlayerDead || _playerHealth.currentHealth <= 0)
            {
                _playerHealth.currentHealth = 0;
            }
            else
            {
                int newCurrent = Mathf.RoundToInt(_playerHealth.maxHealth * ratio);
                _playerHealth.currentHealth = Mathf.Clamp(newCurrent, 1, _playerHealth.maxHealth);
            }

            _appliedTotalHealthBonus = bonus;
        }

        void RemoveTotalHealthBonus()
        {
            if (_playerHealth == null) return;
            if (_appliedTotalHealthBonus == 0) return;

            int currentMax = Mathf.Max(1, _playerHealth.maxHealth);
            float ratio = currentMax > 0 ? (float)_playerHealth.currentHealth / currentMax : 1f;
            int newMax = Mathf.Max(1, currentMax - _appliedTotalHealthBonus);

            _playerHealth.maxHealth = newMax;
            if (PlayerHealth.IsPlayerDead || _playerHealth.currentHealth <= 0)
            {
                _playerHealth.currentHealth = 0;
            }
            else
            {
                int newCurrent = Mathf.RoundToInt(newMax * ratio);
                _playerHealth.currentHealth = Mathf.Clamp(newCurrent, 1, newMax);
            }

            _appliedTotalHealthBonus = 0;
        }
    }
}






