using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>Simple health component for companion NPCs.</summary>
    [DisallowMultipleComponent]
    public class CompanionHealth : MonoBehaviour, EnemyAI.IDamageable
    {
        public int maxHealth = 120;
        public int currentHealth = 120;
        public Animator animator;
        public string isDeadBool = "IsDead";
        public string dieTrigger = "Die";
        public System.Action onDied;
        public event System.Action<int, int> OnHealthChanged;
        [Header("Damage Handling")]
        [Tooltip("Seconds of invulnerability after taking a hit (prevents multi-hit bursts).")]
        [SerializeField, Min(0f)] private float invulnAfterHit = 0.12f;

        [Header("Regeneration")]
        [SerializeField] private bool enableRegeneration = false;
        [SerializeField] private bool regenOnlyOutOfCombat = true;
        [SerializeField] private bool regenUsePercentage = true;
        [SerializeField, Range(0f, 1f)] private float regenPercentPerTick = 0.02f;
        [SerializeField, Min(0)] private int regenFlatAmountPerTick = 2;
        [SerializeField, Min(0.05f)] private float regenTickInterval = 1f;
        [SerializeField, Min(0f)] private float regenCombatLockDuration = 4f;

        public bool IsDead => currentHealth <= 0;

        float _nextRegenTickAt = -1f;
        float _regenCombatLockUntil = -1f;
        float _canBeHitAt = 0f;

        void Awake()
        {
            currentHealth = Mathf.Clamp(currentHealth, 1, maxHealth);
            if (!animator) animator = GetComponent<Animator>();
            RaiseHealthChanged();
            _nextRegenTickAt = -1f;
            _regenCombatLockUntil = -1f;
            _canBeHitAt = 0f;
        }

        void Update()
        {
            HandleRegeneration();
        }

        public void TakeDamage(int amount, Vector2 hitDir)
        {
            if (IsDead) return;
            if (Time.time < _canBeHitAt) return;
            int finalDamage = Mathf.Max(1, amount);
            var shieldHandler = AbilityShieldHandler.GetExisting(transform);
            if (shieldHandler != null)
            {
                finalDamage = shieldHandler.AbsorbDamage(finalDamage);
                if (finalDamage <= 0)
                {
                    _canBeHitAt = Time.time + invulnAfterHit;
                    return;
                }
            }

            currentHealth = Mathf.Clamp(currentHealth - finalDamage, 0, maxHealth);
            RaiseHealthChanged();
            _canBeHitAt = Time.time + invulnAfterHit;

            if (enableRegeneration && regenOnlyOutOfCombat)
            {
                float lockUntil = Time.time + regenCombatLockDuration;
                _regenCombatLockUntil = _regenCombatLockUntil > lockUntil ? _regenCombatLockUntil : lockUntil;
            }

            if (IsDead) Die();
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(1, amount), 1, maxHealth);
            RaiseHealthChanged();
        }

        public void ApplyScaledMaxHealth(int newMaxHealth, bool preserveCurrentRatio = true)
        {
            newMaxHealth = Mathf.Max(1, newMaxHealth);
            if (newMaxHealth == maxHealth)
            {
                return;
            }

            int nextCurrent = currentHealth;
            if (!IsDead)
            {
                if (preserveCurrentRatio && maxHealth > 0)
                {
                    float ratio = Mathf.Clamp01(currentHealth / (float)maxHealth);
                    nextCurrent = Mathf.Max(1, Mathf.RoundToInt(newMaxHealth * ratio));
                }
                nextCurrent = Mathf.Clamp(nextCurrent, 1, newMaxHealth);
            }
            else
            {
                nextCurrent = 0;
            }

            maxHealth = newMaxHealth;
            currentHealth = nextCurrent;
            RaiseHealthChanged();
        }

        void Die()
        {
            if (animator)
            {
                if (!string.IsNullOrEmpty(isDeadBool)) animator.SetBool(isDeadBool, true);
                if (!string.IsNullOrEmpty(dieTrigger)) animator.SetTrigger(dieTrigger);
            }
            onDied?.Invoke();
            RaiseHealthChanged();
        }

        void RaiseHealthChanged() => OnHealthChanged?.Invoke(currentHealth, maxHealth);

        void HandleRegeneration()
        {
            if (!enableRegeneration)
            {
                _nextRegenTickAt = -1f;
                return;
            }

            if (IsDead) return;
            if (currentHealth >= maxHealth) return;

            float now = Time.time;
            if (regenOnlyOutOfCombat && now < _regenCombatLockUntil)
            {
                return;
            }

            float interval = Mathf.Max(0.05f, regenTickInterval);
            if (_nextRegenTickAt < 0f)
            {
                _nextRegenTickAt = now + interval;
                return;
            }

            if (now < _nextRegenTickAt)
            {
                return;
            }

            int amount = regenUsePercentage
                ? Mathf.Max(1, Mathf.RoundToInt(maxHealth * Mathf.Clamp01(regenPercentPerTick)))
                : Mathf.Max(1, regenFlatAmountPerTick);

            if (amount <= 0)
            {
                _nextRegenTickAt = now + interval;
                return;
            }

            Heal(amount);
            _nextRegenTickAt = now + interval;
        }
    }
}



