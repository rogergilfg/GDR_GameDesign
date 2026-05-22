using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Debuff that applies damage over time to the target.
    /// Has built-in duration support with optional refresh on reapply.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Applies damage over time to the target (debuff).")]
    public sealed class DebuffDamageOverTimeModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Damage dealt per tick.")]
        [Min(1)]
        private int damagePerTick = 5;

        [SerializeField]
        [Tooltip("Time between damage ticks in seconds.")]
        [Min(0.1f)]
        private float tickInterval = 1f;

        [SerializeField]
        [Tooltip("Duration in seconds. If > 0, DoT will stop after duration. If 0, DoT is permanent.")]
        [Min(0f)]
        private float duration = 5f;

        [SerializeField]
        [Tooltip("If true, reapplying this debuff will reset the duration timer.")]
        private bool refreshDuration = true;

        [SerializeField]
        [Tooltip("If true, damage is dealt to the owner of the ability runner (self-damage). If false, damage is dealt to the target.")]
        private bool damageOwner = true;

        [SerializeField]
        [Tooltip("When enabled, each tick deals a percentage of the caster's damage instead of a fixed amount.")]
        private bool useCasterDamagePercentage = false;

        [SerializeField]
        [Tooltip("Percentage of the caster's damage dealt per tick (0.25 = 25%).")]
        [Range(0f, 5f)]
        private float casterDamagePercentage = 0.25f;

        private float _nextTickTime;
        private float _expirationTime;
        private bool _isActive;
        private AbilityRunner _runner;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            _runner = runner;

            if (!_isActive)
            {
                _isActive = true;
                _nextTickTime = Time.time + tickInterval;
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
            if (!enabled) return;

            _isActive = false;
            _runner = null;
        }

        /// <summary>
        /// Called every frame to process damage ticks and check duration expiration.
        /// </summary>
        public void Update()
        {
            if (!_isActive || _runner == null) return;

            // Check duration expiration first
            if (duration > 0f && Time.time >= _expirationTime)
            {
                Remove(_runner);
                return;
            }

            // Process damage ticks
            if (Time.time >= _nextTickTime)
            {
                ApplyDamageTick();
                _nextTickTime = Time.time + tickInterval;
            }
        }

        private void ApplyDamageTick()
        {
            int tickDamage = ResolveTickDamage();
            if (tickDamage <= 0)
            {
                return;
            }

            if (damageOwner)
            {
                // Apply damage to owner (self-damage)
                if (_runner.CachedPlayerHealth != null)
                {
                    _runner.CachedPlayerHealth.TakeDamage(tickDamage, Vector2.zero);
                }
                else if (_runner.CachedEnemyHealth != null)
                {
                    _runner.CachedEnemyHealth.TakeDamage(tickDamage, _runner.transform.position);
                }
            }
            else
            {
                // Apply damage to target
                Transform target = _runner.CachedTarget;
                if (target != null)
                {
                    var enemyHealth = target.GetComponent<EnemyHealth2D>();
                    if (enemyHealth != null)
                    {
                        enemyHealth.TakeDamage(tickDamage, target.position);
                    }
                    else
                    {
                        var playerHealth = target.GetComponent<PlayerHealth>();
                        if (playerHealth != null)
                        {
                            playerHealth.TakeDamage(tickDamage, Vector2.zero);
                        }
                    }
                }
            }
        }

        private int ResolveTickDamage()
        {
            int baseDamage = Mathf.Max(1, damagePerTick);
            if (!useCasterDamagePercentage || _runner == null)
            {
                return baseDamage;
            }

            int casterDamage = GetCasterDamageEstimate();
            if (casterDamage <= 0)
            {
                return baseDamage;
            }

            float percent = Mathf.Max(0f, casterDamagePercentage);
            int scaled = Mathf.RoundToInt(casterDamage * percent);
            if (scaled <= 0)
            {
                scaled = baseDamage;
            }

            return Mathf.Max(1, scaled);
        }

        private int GetCasterDamageEstimate()
        {
            if (_runner == null)
            {
                return 0;
            }

            if (_runner.CachedEnemyAI != null)
            {
                return Mathf.Max(1, _runner.CachedEnemyAI.CurrentMeleeDamage);
            }

            if (_runner.CachedNeutralNpcAI != null)
            {
                return Mathf.Max(1, _runner.CachedNeutralNpcAI.attackDamage);
            }

            var companion = _runner.GetComponent<CompanionAI>() ?? _runner.GetComponentInParent<CompanionAI>();
            if (companion != null)
            {
                return Mathf.Max(1, companion.damage);
            }

            var playerMelee = _runner.GetComponent<PlayerMeleeHitbox>() ?? _runner.GetComponentInChildren<PlayerMeleeHitbox>();
            if (playerMelee != null)
            {
                return Mathf.Max(1, playerMelee.damage);
            }

            var turret = _runner.GetComponent<TurretAI>() ?? _runner.GetComponentInParent<TurretAI>();
            if (turret != null)
            {
                return turret.CurrentProjectileDamage;
            }

            if (_runner.OwnerKind == AbilityActorKind.Player && PlayerStats.Instance != null)
            {
                var stats = PlayerStats.Instance.CurrentStats;
                int estimated = stats.Strength + stats.WeaponDamage;
                return Mathf.Max(1, estimated);
            }

            return 0;
        }
    }
}






