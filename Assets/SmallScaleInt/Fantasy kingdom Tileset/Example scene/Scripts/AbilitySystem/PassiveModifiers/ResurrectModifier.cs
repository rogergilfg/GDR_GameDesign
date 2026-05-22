using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Prevents fatal damage by healing the owner when they would die.
    /// Should be used with ProcTrigger set to OnDeath for limited-use resurrection (e.g., once per 60 seconds).
    /// Can also be used as a permanent passive for unlimited resurrections (though not recommended for balance).
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Prevents death by healing when taking fatal damage. Use with OnDeath ProcTrigger for cooldown-based resurrection.")]
    public sealed class ResurrectModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("Flat amount of health to restore when preventing death. Adds with percentage heal.")]
        [Min(0)]
        private int healAmount = 50;

        [SerializeField]
        [Tooltip("Percentage of max health to restore when preventing death (0.3 = 30%). Adds with flat heal.")]
        [Range(0f, 1f)]
        private float healPercent = 0f;

        [SerializeField]
        [Tooltip("If true, plays a visual effect at the resurrection position.")]
        private bool spawnVfx = true;

        [SerializeField]
        [Tooltip("Optional VFX prefab to spawn at resurrection location.")]
        private GameObject resurrectVfxPrefab;

        [SerializeField]
        [Tooltip("Duration in seconds before VFX is destroyed.")]
        [Min(0f)]
        private float vfxDuration = 2f;

        private AbilityRunner _runner;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;
            _runner = runner;

            // Heal the owner immediately when this modifier is applied
            Heal();
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;
            _runner = null;
        }

        private void Heal()
        {
            if (_runner == null) return;

            // Heal player
            if (_runner.CachedPlayerHealth != null)
            {
                int maxHealth = _runner.CachedPlayerHealth.maxHealth;

                // Calculate total heal (flat + percentage)
                int percentHeal = Mathf.RoundToInt(maxHealth * healPercent);
                int totalHeal = healAmount + percentHeal;

                // Ensure at least 1 HP is healed
                totalHeal = Mathf.Max(1, totalHeal);

                _runner.CachedPlayerHealth.currentHealth = Mathf.Min(_runner.CachedPlayerHealth.currentHealth + totalHeal, maxHealth);

                Debug.Log($"[ResurrectModifier] Player saved from death! Healed for {totalHeal} health ({healAmount} flat + {percentHeal} from {healPercent * 100}%) (now at {_runner.CachedPlayerHealth.currentHealth}/{maxHealth})");

                // Spawn VFX
                if (spawnVfx && resurrectVfxPrefab != null)
                {
                    SpawnResurrectVfx(_runner.transform.position);
                }
            }
            // Heal enemy
            else if (_runner.CachedEnemyHealth != null)
            {
                int maxHealth = _runner.CachedEnemyHealth.MaxHealth;
                int currentHealth = _runner.CachedEnemyHealth.CurrentHealth;

                // Calculate total heal (flat + percentage)
                int percentHeal = Mathf.RoundToInt(maxHealth * healPercent);
                int totalHeal = healAmount + percentHeal;

                // Ensure at least 1 HP is healed
                totalHeal = Mathf.Max(1, totalHeal);

                int newHealth = Mathf.Min(currentHealth + totalHeal, maxHealth);

                // Use reflection to set currentHealth since it's private
                var healthField = typeof(EnemyHealth2D).GetField("currentHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (healthField != null)
                {
                    healthField.SetValue(_runner.CachedEnemyHealth, newHealth);
                }

                Debug.Log($"[ResurrectModifier] Enemy saved from death! Healed for {totalHeal} health ({healAmount} flat + {percentHeal} from {healPercent * 100}%) (now at {newHealth}/{maxHealth})");

                // Spawn VFX
                if (spawnVfx && resurrectVfxPrefab != null)
                {
                    SpawnResurrectVfx(_runner.transform.position);
                }
            }
        }

        private void SpawnResurrectVfx(Vector3 position)
        {
            if (resurrectVfxPrefab == null) return;

            GameObject vfx = Object.Instantiate(resurrectVfxPrefab, position, Quaternion.identity);
            if (vfx != null && vfxDuration > 0f)
            {
                Object.Destroy(vfx, vfxDuration);
            }
        }
    }
}






