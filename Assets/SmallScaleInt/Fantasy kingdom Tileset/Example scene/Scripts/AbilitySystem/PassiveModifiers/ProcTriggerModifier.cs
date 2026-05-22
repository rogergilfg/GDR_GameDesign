using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Defines trigger conditions and duration for subsequent modifiers in the list.
    /// Place this before other modifiers to make them proc-based instead of permanent.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Controls when subsequent modifiers activate (on damage taken/dealt, etc.) with duration and stacking support.")]
    public sealed class ProcTriggerModifier : PassiveAbilityModifier
    {
        public enum TriggerType
        {
            OnDamageTaken,      // Triggers when owner takes damage
            OnDamageDealt,      // Triggers when owner deals damage (includes both abilities and melee attacks)
            OnKill,             // Triggers when owner kills an enemy
            OnAbilityUsed,      // Triggers when owner uses any ability
            OnCriticalHit,      // Triggers when owner lands a critical hit
            OnDodge,            // Triggers when owner dodges an attack
            OnLowHealth,        // Triggers when owner drops below health threshold
            OnDeath,            // Triggers when owner dies (before death is finalized)
        }

        [SerializeField]
        [Tooltip("What event triggers the proc.")]
        private TriggerType triggerType = TriggerType.OnDamageDealt;

        [SerializeField]
        [Tooltip("Chance (0-1) that the proc will activate when triggered. 1.0 = 100% chance.")]
        [Range(0f, 1f)]
        private float procChance = 1f;

        [SerializeField]
        [Tooltip("How long (in seconds) the proc effect lasts. 0 = permanent until removed.")]
        [Min(0f)]
        private float duration = 2f;

        [SerializeField]
        [Tooltip("Maximum number of times this proc can stack. 1 = no stacking, 0 = unlimited.")]
        [Min(0)]
        private int maxStacks = 1;

        [SerializeField]
        [Tooltip("If true, each new proc resets the duration timer. If false, stacks have independent timers.")]
        private bool refreshDuration = true;

        [SerializeField]
        [Tooltip("For OnLowHealth trigger: health percentage threshold (0.3 = trigger below 30% health).")]
        [Range(0f, 1f)]
        private float lowHealthThreshold = 0.3f;

        [SerializeField]
        [Tooltip("Minimum damage required to trigger (for damage-based triggers). 0 = any damage.")]
        [Min(0)]
        private int minDamageThreshold = 0;

        // Runtime state
        private AbilityRunner _runner;
        private AbilityDefinition _abilityDefinition;
        private PassiveAbilityModifier[] _controlledModifiers;
        private int _currentStacks;
        private float _procEndTime;
        private float[] _stackEndTimes;
        private bool _wasLowHealth;

        public bool IsActive => _currentStacks > 0;
        public int CurrentStacks => _currentStacks;
        public TriggerType Trigger => triggerType;
        public float ProcChance => procChance;
        public float Duration => duration;

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled) return;

            _runner = runner;
            _currentStacks = 0;
            _procEndTime = 0f;
            _wasLowHealth = false;

            if (maxStacks > 1)
            {
                _stackEndTimes = new float[maxStacks];
            }

            // Subscribe to relevant events based on trigger type
            SubscribeToTriggers();
        }

        public override void Remove(AbilityRunner runner)
        {
            if (!enabled) return;

            UnsubscribeFromTriggers();
            DeactivateProc();

            _runner = null;
            _controlledModifiers = null;
            _stackEndTimes = null;
        }

        /// <summary>
        /// Called by AbilityRunner to link this trigger to subsequent modifiers in the list.
        /// </summary>
        public void SetControlledModifiers(PassiveAbilityModifier[] modifiers)
        {
            _controlledModifiers = modifiers;
        }

        /// <summary>
        /// Called by AbilityRunner to set the ability definition for UI display.
        /// </summary>
        public void SetAbilityDefinition(AbilityDefinition definition)
        {
            _abilityDefinition = definition;
        }

        private void SubscribeToTriggers()
        {
            if (_runner == null) return;

            switch (triggerType)
            {
                case TriggerType.OnDamageTaken:
                    if (_runner.CachedPlayerHealth != null)
                    {
                        _runner.CachedPlayerHealth.OnDamageTaken += HandleDamageTaken;
                    }
                    else if (_runner.CachedEnemyHealth != null)
                    {
                        _runner.CachedEnemyHealth.OnDamageTaken += HandleDamageTaken;
                    }
                    break;

                case TriggerType.OnDamageDealt:
                case TriggerType.OnCriticalHit:
                    if (_runner.CachedPlayerStats != null)
                    {
                        _runner.CachedPlayerStats.OnDamageDealt += HandleDamageDealt;
                    }
                    break;

                case TriggerType.OnKill:
                    // TODO: Add OnEnemyKilled event to PlayerStats or combat system
                    // if (_runner.CachedPlayerStats != null)
                    // {
                    //     _runner.CachedPlayerStats.OnEnemyKilled += HandleKill;
                    // }
                    break;

                case TriggerType.OnAbilityUsed:
                    _runner.AbilityStarted += HandleAbilityUsed;
                    break;

                case TriggerType.OnLowHealth:
                    // Checked in Update via MonoBehaviour hook
                    break;

                case TriggerType.OnDeath:
                    if (_runner.CachedPlayerHealth != null)
                    {
                        _runner.CachedPlayerHealth.OnDamageTaken += HandleDeathCheck;
                    }
                    else if (_runner.CachedEnemyHealth != null)
                    {
                        _runner.CachedEnemyHealth.OnDamageTaken += HandleDeathCheck;
                    }
                    break;
            }
        }

        private void UnsubscribeFromTriggers()
        {
            if (_runner == null) return;

            switch (triggerType)
            {
                case TriggerType.OnDamageTaken:
                    if (_runner.CachedPlayerHealth != null)
                    {
                        _runner.CachedPlayerHealth.OnDamageTaken -= HandleDamageTaken;
                    }
                    else if (_runner.CachedEnemyHealth != null)
                    {
                        _runner.CachedEnemyHealth.OnDamageTaken -= HandleDamageTaken;
                    }
                    break;

                case TriggerType.OnDamageDealt:
                case TriggerType.OnCriticalHit:
                    if (_runner.CachedPlayerStats != null)
                    {
                        _runner.CachedPlayerStats.OnDamageDealt -= HandleDamageDealt;
                    }
                    break;

                case TriggerType.OnKill:
                    // TODO: Uncomment when events are added
                    // if (_runner.CachedPlayerStats != null)
                    // {
                    //     _runner.CachedPlayerStats.OnEnemyKilled -= HandleKill;
                    // }
                    break;

                case TriggerType.OnAbilityUsed:
                    _runner.AbilityStarted -= HandleAbilityUsed;
                    break;

                case TriggerType.OnDeath:
                    if (_runner.CachedPlayerHealth != null)
                    {
                        _runner.CachedPlayerHealth.OnDamageTaken -= HandleDeathCheck;
                    }
                    else if (_runner.CachedEnemyHealth != null)
                    {
                        _runner.CachedEnemyHealth.OnDamageTaken -= HandleDeathCheck;
                    }
                    break;
            }
        }

        private void HandleDamageTaken(int damage)
        {
            if (minDamageThreshold > 0 && damage < minDamageThreshold) return;
            TryTriggerProc();
        }

        private void HandleDamageDealt(int damage, bool wasCritical)
        {
            if (minDamageThreshold > 0 && damage < minDamageThreshold) return;

            if (triggerType == TriggerType.OnCriticalHit && !wasCritical) return;

            TryTriggerProc();
        }

        private void HandleKill()
        {
            TryTriggerProc();
        }

        private void HandleAbilityUsed(AbilityDefinition ability)
        {
            TryTriggerProc();
        }

        private void HandleDeathCheck(int damage)
        {
            // Check if this damage would be fatal
            bool wouldDie = false;

            if (_runner.CachedPlayerHealth != null)
            {
                wouldDie = _runner.CachedPlayerHealth.currentHealth <= 0;
            }
            else if (_runner.CachedEnemyHealth != null)
            {
                wouldDie = _runner.CachedEnemyHealth.CurrentHealth <= 0;
            }

            if (wouldDie)
            {
                TryTriggerProc();
            }
        }

        private void TryTriggerProc()
        {
            if (!enabled || _runner == null) return;

            // Check if ability is off cooldown (has charges available)
            if (_abilityDefinition != null && _runner != null)
            {
                var state = _runner.GetState(_abilityDefinition);
                if (state != null && !state.IsReady)
                {
                    // Ability is on cooldown, don't trigger proc
                    return;
                }
            }

            if (Random.value <= procChance)
            {
                ActivateProc();

                // Consume a charge from the ability if it has charges
                if (_abilityDefinition != null && _runner != null)
                {
                    var state = _runner.GetState(_abilityDefinition);
                    if (state != null && !state.HasUnlimitedCharges)
                    {
                        state.ConsumeCharge();
                    }
                }
            }
        }

        private void ActivateProc()
        {
            bool wasActive = _currentStacks > 0;
            bool shouldApplyModifiers = false;

            // Handle stacking
            if (maxStacks == 1)
            {
                _currentStacks = 1;
                if (refreshDuration || !wasActive)
                {
                    _procEndTime = Time.time + duration;
                }
                shouldApplyModifiers = !wasActive; // Only apply on first activation
            }
            else if (maxStacks == 0 || _currentStacks < maxStacks)
            {
                if (maxStacks > 1 && _stackEndTimes != null)
                {
                    _stackEndTimes[_currentStacks] = Time.time + duration;
                }

                _currentStacks++;
                shouldApplyModifiers = true; // Apply for each new stack

                if (refreshDuration)
                {
                    _procEndTime = Time.time + duration;
                }
            }
            else if (refreshDuration && maxStacks > 0)
            {
                // At max stacks, just refresh duration (don't add more stacks)
                _procEndTime = Time.time + duration;
                shouldApplyModifiers = false;
            }

            // Apply controlled modifiers for each stack
            if (shouldApplyModifiers && _controlledModifiers != null)
            {
                foreach (var modifier in _controlledModifiers)
                {
                    if (modifier != null && modifier.Enabled)
                    {
                        modifier.Apply(_runner);
                    }
                }
            }

            // Notify UI
            if (PassiveBuffPanelController.Instance != null && _abilityDefinition != null)
            {
                PassiveBuffPanelController.Instance.RegisterProcPassive(
                    _abilityDefinition, this, duration, _procEndTime, _currentStacks);
            }
        }

        private void DeactivateProc()
        {
            if (_currentStacks == 0 || _controlledModifiers == null) return;

            // Remove controlled modifiers for each stack
            int stacksToRemove = _currentStacks;
            for (int i = 0; i < stacksToRemove; i++)
            {
                foreach (var modifier in _controlledModifiers)
                {
                    if (modifier != null && modifier.Enabled)
                    {
                        modifier.Remove(_runner);
                    }
                }
            }

            _currentStacks = 0;
            _procEndTime = 0f;

            if (_stackEndTimes != null)
            {
                System.Array.Clear(_stackEndTimes, 0, _stackEndTimes.Length);
            }

            // Notify UI
            if (PassiveBuffPanelController.Instance != null && _abilityDefinition != null)
            {
                PassiveBuffPanelController.Instance.UnregisterPassive(_abilityDefinition);
            }
        }

        /// <summary>
        /// Must be called every frame by AbilityRunner to handle duration expiration.
        /// </summary>
        public void Update()
        {
            if (!enabled || _currentStacks == 0 || duration <= 0f) return;

            if (triggerType == TriggerType.OnLowHealth)
            {
                CheckLowHealthTrigger();
            }

            // Update controlled modifiers that need per-frame updates (e.g., DoT effects)
            if (_controlledModifiers != null && _currentStacks > 0)
            {
                foreach (var modifier in _controlledModifiers)
                {
                    if (modifier != null && modifier.Enabled)
                    {
                        // Check if the modifier has an Update method using reflection
                        var updateMethod = modifier.GetType().GetMethod("Update", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (updateMethod != null)
                        {
                            updateMethod.Invoke(modifier, null);
                        }
                    }
                }
            }

            // Check if proc has expired
            if (refreshDuration)
            {
                if (Time.time >= _procEndTime)
                {
                    DeactivateProc();
                }
            }
            else if (_stackEndTimes != null)
            {
                // Check individual stack timers
                int expiredStacks = 0;
                for (int i = 0; i < _currentStacks; i++)
                {
                    if (Time.time >= _stackEndTimes[i])
                    {
                        expiredStacks++;
                    }
                }

                if (expiredStacks > 0)
                {
                    // Remove modifiers for each expired stack
                    for (int i = 0; i < expiredStacks; i++)
                    {
                        foreach (var modifier in _controlledModifiers)
                        {
                            if (modifier != null && modifier.Enabled)
                            {
                                modifier.Remove(_runner);
                            }
                        }
                    }

                    _currentStacks -= expiredStacks;
                    if (_currentStacks <= 0)
                    {
                        DeactivateProc();
                    }
                    else
                    {
                        // Update UI with new stack count
                        if (PassiveBuffPanelController.Instance != null && _abilityDefinition != null)
                        {
                            PassiveBuffPanelController.Instance.UpdateBuffStacks(_abilityDefinition, _currentStacks);
                        }
                    }
                }
            }
        }

        private void CheckLowHealthTrigger()
        {
            if (_runner == null) return;

            bool isLowHealth = false;

            if (_runner.CachedPlayerHealth != null)
            {
                float healthPercent = (float)_runner.CachedPlayerHealth.currentHealth / _runner.CachedPlayerHealth.maxHealth;
                isLowHealth = healthPercent <= lowHealthThreshold;
            }
            else if (_runner.CachedEnemyHealth != null)
            {
                float healthPercent = (float)_runner.CachedEnemyHealth.CurrentHealth / _runner.CachedEnemyHealth.MaxHealth;
                isLowHealth = healthPercent <= lowHealthThreshold;
            }

            // Trigger on transition to low health
            if (isLowHealth && !_wasLowHealth)
            {
                TryTriggerProc();
            }

            _wasLowHealth = isLowHealth;
        }
    }
}






