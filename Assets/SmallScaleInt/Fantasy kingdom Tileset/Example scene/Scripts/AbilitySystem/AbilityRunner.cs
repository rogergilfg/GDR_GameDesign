using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset;
using PlayerStatBonus = SmallScale.FantasyKingdomTileset.PlayerStats.PlayerStatBonus;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Executes <see cref="AbilityDefinition"/> assets on a GameObject, handling cooldowns, costs and step execution.
    /// Works for both player and enemy actors.
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom("AbilitySystem")]
    public class AbilityRunner : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Ability definitions granted to this actor at start. Additional abilities can be added at runtime.")]
        private AbilityDefinition[] startingAbilities = System.Array.Empty<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Optional default target transform used for direction/targeting lookups (e.g., the player).")]
        private Transform defaultTarget;

        [SerializeField]
        [Tooltip("When enabled, basic debug information is written to the console.")]
        private bool debugLog;

        private readonly Dictionary<AbilityDefinition, AbilityRuntimeState> _runtimeStates = new();
        private readonly List<AbilityDefinition> _runningAbilities = new();
        private readonly List<Coroutine> _backgroundStepCoroutines = new();
        private readonly HashSet<AbilityDefinition> _knownAbilities = new();
        private readonly List<AbilityDefinition> _runtimeGrantedAbilities = new();
        private readonly Dictionary<AbilityDefinition, PassiveRuntimeEffect> _activePassiveEffects = new();
        private readonly List<ProcTriggerModifier> _activeProcTriggers = new();
        private readonly List<PassiveAbilityModifier> _activeModifiersWithUpdate = new();

        // Layer indices are resolved at runtime (Awake/Start), not in field initializers.
        // See Unity restriction on NameToLayer in constructors/initializers.

        public Animator CachedAnimator { get; private set; }
        public Rigidbody2D CachedRigidbody2D { get; private set; }
        public GenericTopDownController CachedTopDownController { get; private set; }
        public EnemyAI CachedEnemyAI { get; private set; }
        public NeutralNpcAI CachedNeutralNpcAI { get; private set; }
        public CompanionAI CachedCompanionAI { get; private set; }
        public TurretAI CachedTurretAI { get; private set; }
        public EnemyHealth2D CachedEnemyHealth { get; private set; }
        public PlayerMana CachedPlayerMana { get; private set; }
        public PlayerHealth CachedPlayerHealth { get; private set; }
        public PlayerExperience CachedPlayerExperience { get; private set; }
        public PlayerStats CachedPlayerStats { get; private set; }
        public AbilityActorKind OwnerKind { get; private set; } = AbilityActorKind.Unknown;
        public bool IsPlayerControlled => OwnerKind == AbilityActorKind.Player;
        public bool IsEnemyControlled => OwnerKind == AbilityActorKind.Enemy;
        public bool IsNeutralControlled => OwnerKind == AbilityActorKind.Neutral;
        public Transform CachedTarget => defaultTarget;

        public event System.Action<AbilityDefinition> AbilityStarted;
        public event System.Action<AbilityDefinition> AbilityCompleted;
        public event System.Action<AbilityDefinition> AbilityGranted;
        public event System.Action<AbilityDefinition> AbilityRemoved;

        void Awake()
        {
            CacheComponents();
            SubscribeEnemyHealth();

            if (defaultTarget == null && CachedEnemyAI != null)
            {
                defaultTarget = CachedEnemyAI.player;
            }

            foreach (var ability in startingAbilities)
            {
                if (!ability) continue;
                EnsureState(ability);
                ApplyPassiveAbility(ability);
            }
        }

        void OnDisable()
        {
            CancelAllAbilities();
        }

        void OnEnable()
        {
            CancelAllAbilitiesInternal();
        }

        void OnDestroy()
        {
            ClearPassiveEffects();
            UnsubscribeEnemyHealth();
        }

        void Update()
        {
            if (!defaultTarget && CachedEnemyAI != null && CachedEnemyAI.player != null)
            {
                defaultTarget = CachedEnemyAI.player;
            }

            if (!CachedPlayerExperience && OwnerKind == AbilityActorKind.Player && PlayerExperience.Instance != null)
            {
                CachedPlayerExperience = PlayerExperience.Instance;
            }

            // Update active proc triggers for duration/expiration handling
            for (int i = 0; i < _activeProcTriggers.Count; i++)
            {
                _activeProcTriggers[i]?.Update();
            }

            // Update active modifiers that have Update methods (e.g., DoT debuffs)
            for (int i = _activeModifiersWithUpdate.Count - 1; i >= 0; i--)
            {
                var modifier = _activeModifiersWithUpdate[i];
                if (modifier == null || !modifier.Enabled)
                {
                    _activeModifiersWithUpdate.RemoveAt(i);
                    continue;
                }

                // Use reflection to call Update method if it exists
                var updateMethod = modifier.GetType().GetMethod("Update", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (updateMethod != null)
                {
                    updateMethod.Invoke(modifier, null);
                }
            }
        }

        void CacheComponents()
        {
            CachedAnimator = GetComponentInChildren<Animator>();
            CachedRigidbody2D = GetComponent<Rigidbody2D>();
            CachedTopDownController = GetComponent<GenericTopDownController>();
            CachedEnemyAI = GetComponent<EnemyAI>();
            CachedNeutralNpcAI = GetComponent<NeutralNpcAI>();
            CachedCompanionAI = GetComponent<CompanionAI>();
            CachedTurretAI = GetComponent<TurretAI>();
            CachedEnemyHealth = GetComponent<EnemyHealth2D>();
            DetermineOwnerKind();
            CachedPlayerMana = ResolvePlayerMana();
            CachedPlayerHealth = ResolvePlayerHealth();
            CachedPlayerExperience = GetComponent<PlayerExperience>();
            if (!CachedPlayerExperience && OwnerKind == AbilityActorKind.Player)
            {
                CachedPlayerExperience = PlayerExperience.Instance;
            }
            CachedPlayerStats = GetComponent<PlayerStats>();
            if (!CachedPlayerStats && OwnerKind == AbilityActorKind.Player)
            {
                CachedPlayerStats = PlayerStats.Instance;
            }
        }

        public void SetDefaultTarget(Transform target)
        {
            defaultTarget = target;
        }

        void DetermineOwnerKind()
        {
            int playerLayer = LayerMask.NameToLayer("Player");
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int neutralLayer = LayerMask.NameToLayer("Neutral");

            if (CachedCompanionAI)
            {
                OwnerKind = AbilityActorKind.Neutral;
                return;
            }

            if (CachedTurretAI)
            {
                OwnerKind = AbilityActorKind.Neutral;
                return;
            }

            int layer = gameObject.layer;
            if (playerLayer != -1 && layer == playerLayer)
            {
                OwnerKind = AbilityActorKind.Player;
                return;
            }

            if (enemyLayer != -1 && layer == enemyLayer)
            {
                OwnerKind = AbilityActorKind.Enemy;
                return;
            }

            if (neutralLayer != -1 && layer == neutralLayer)
            {
                OwnerKind = AbilityActorKind.Neutral;
                return;
            }

            if (CachedEnemyAI)
            {
                OwnerKind = AbilityActorKind.Enemy;
                return;
            }

            if (CachedNeutralNpcAI)
            {
                OwnerKind = AbilityActorKind.Neutral;
                return;
            }

            if (CachedTopDownController || GetComponent<PlayerStats>() || GetComponent<PlayerHealth>())
            {
                OwnerKind = AbilityActorKind.Player;
                return;
            }

            OwnerKind = AbilityActorKind.Unknown;
        }

        PlayerMana ResolvePlayerMana()
        {
            var local = GetComponent<PlayerMana>();
            if (local) return local;
            return IsPlayerControlled ? PlayerMana.Instance : null;
        }

        PlayerHealth ResolvePlayerHealth()
        {
            var local = GetComponent<PlayerHealth>();
            if (local) return local;
            return IsPlayerControlled ? PlayerHealth.Instance : null;
        }

        public AbilityRuntimeState GetState(AbilityDefinition definition)
        {
            return EnsureState(definition);
        }

        public IEnumerable<AbilityDefinition> EnumerateAbilities()
        {
            if (startingAbilities != null)
            {
                for (int i = 0; i < startingAbilities.Length; i++)
                {
                    var ability = startingAbilities[i];
                    if (ability) yield return ability;
                }
            }

            for (int i = 0; i < _runtimeGrantedAbilities.Count; i++)
            {
                var ability = _runtimeGrantedAbilities[i];
                if (ability) yield return ability;
            }
        }

        public bool TryAddAbility(AbilityDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            // Check if already known in HashSet
            bool alreadyKnown = _knownAbilities.Contains(definition);

            // Also check if it's a starting ability (in case HashSet wasn't populated yet)
            bool isStartingAbility = false;
            if (startingAbilities != null)
            {
                for (int i = 0; i < startingAbilities.Length; i++)
                {
                    if (startingAbilities[i] == definition)
                    {
                        isStartingAbility = true;
                        break;
                    }
                }
            }

            // Ensure state and passive effects are applied
            EnsureState(definition);
            ApplyPassiveAbility(definition);

            // Only add to runtime granted list if it's not already known AND not a starting ability
            if (!alreadyKnown && !isStartingAbility)
            {
                _runtimeGrantedAbilities.Add(definition);
                _knownAbilities.Add(definition);
                AbilityGranted?.Invoke(definition);
                return true;
            }

            return false;
        }

        public bool TryRemoveAbility(AbilityDefinition definition, bool cancelIfRunning = true)
        {
            if (definition == null)
            {
                return false;
            }

            bool removed = _runtimeGrantedAbilities.Remove(definition);
            _knownAbilities.Remove(definition);
            RemovePassiveAbility(definition);

            if (_runtimeStates.TryGetValue(definition, out AbilityRuntimeState state))
            {
                if (cancelIfRunning && state.IsRunning && state.RunningCoroutine != null)
                {
                    StopCoroutine(state.RunningCoroutine);
                }

                _runtimeStates.Remove(definition);
            }

            _runningAbilities.Remove(definition);

            if (removed)
            {
                AbilityRemoved?.Invoke(definition);
            }

            return removed;
        }

        AbilityRuntimeState EnsureState(AbilityDefinition definition)
        {
            if (definition == null) return null;
            if (!_runtimeStates.TryGetValue(definition, out AbilityRuntimeState state))
            {
                state = definition.CreateRuntimeState();
                _runtimeStates.Add(definition, state);
            }

            _knownAbilities.Add(definition);
            return state;
        }

        public bool IsAbilityRunning(AbilityDefinition definition)
        {
            var state = GetState(definition);
            return state != null && state.IsRunning;
        }

        public bool AnyAbilityRunning
        {
            get
            {
                PruneStaleRunningAbilities();
                return _runningAbilities.Count > 0;
            }
        }

        void PruneStaleRunningAbilities()
        {
            for (int i = _runningAbilities.Count - 1; i >= 0; i--)
            {
                var ability = _runningAbilities[i];
                if (ability == null)
                {
                    _runningAbilities.RemoveAt(i);
                    continue;
                }

                if (!_runtimeStates.TryGetValue(ability, out AbilityRuntimeState state))
                {
                    _runningAbilities.RemoveAt(i);
                    continue;
                }

                if (!state.IsRunning || state.RunningCoroutine == null)
                {
                    state.IsRunning = false;
                    state.RunningCoroutine = null;
                    _runningAbilities.RemoveAt(i);
                }
            }
        }

        public bool TryActivateAbility(AbilityDefinition definition, AbilityActivationParameters parameters, bool ignoreCooldown = false)
        {
            if (definition == null)
            {
                if (debugLog) Debug.LogWarning($"{name}: Tried to activate null ability.", this);
                return false;
            }

            if (definition.IsPassive)
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' is passive and cannot be activated manually.", this);
                return false;
            }

            var state = EnsureState(definition);
            if (!ignoreCooldown && !state.IsReady)
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' rejected (cooldown/charges).", this);
                return false;
            }

            if (definition.RequireIdleRunner && AnyAbilityRunning)
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' rejected (runner busy).", this);
                return false;
            }

            if (CachedEnemyHealth != null && CachedEnemyHealth.IsDead)
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' rejected (owner dead).", this);
                return false;
            }

            if (IsPlayerControlled && PlayerHealth.IsPlayerDead)
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' rejected (player dead).", this);
                return false;
            }

            var context = new AbilityRuntimeContext(this, definition, state, parameters);
            if (!EvaluateRequirements(context))
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' rejected (requirements).", this);
                return false;
            }

            if (!ConsumeResourceCost(context))
            {
                if (debugLog) Debug.Log($"{name}: '{definition.DisplayName}' rejected (resource cost).", this);
                state.ResetCooldown();
                return false;
            }

            state.ConsumeCharge();

            var coroutine = StartCoroutine(AbilityCoroutine(context));
            state.IsRunning = true;
            state.RunningCoroutine = coroutine;
            _runningAbilities.Add(definition);
            AbilityStarted?.Invoke(definition);
            return true;
        }

        bool EvaluateRequirements(AbilityRuntimeContext context)
        {
            var definition = context.Definition;
            var requirements = definition != null ? definition.ActivationRequirements : null;
            if (requirements == null || requirements.Count == 0) return true;

            foreach (var requirement in requirements)
            {
                if (requirement == null) continue;
                if (!requirement.Check(context, out string failureReason))
                {
                    if (debugLog && !string.IsNullOrEmpty(failureReason) && definition != null)
                    {
                        Debug.Log($"{name}: Requirement failed for '{definition.DisplayName}' -> {failureReason}", this);
                    }
                    return false;
                }
            }

            return true;
        }

        bool ConsumeResourceCost(AbilityRuntimeContext context)
        {
            float manaCost = context.Definition != null ? context.Definition.ManaCost : 0f;
            if (manaCost <= 0f) return true;

            if (context.OwnerKind != AbilityActorKind.Player)
            {
                return true;
            }

            var mana = AcquirePlayerMana();
            if (mana == null)
            {
                return false;
            }

            return mana.TrySpend(manaCost);
        }

        PlayerMana AcquirePlayerMana()
        {
            if (CachedPlayerMana == null)
            {
                CachedPlayerMana = ResolvePlayerMana();
            }

            return CachedPlayerMana;
        }

        IEnumerator AbilityCoroutine(AbilityRuntimeContext context)
        {
            var definition = context.Definition;
            var state = context.State;
            float cooldown = definition != null ? definition.CooldownSeconds : 0f;
            Exception failure = null;

            _backgroundStepCoroutines.Clear();

            try
            {
                var steps = definition != null ? definition.Steps : null;
                if (steps != null)
                {
                    foreach (var step in steps)
                    {
                        if (step == null) continue;
                        if (!step.ShouldExecuteForOwner(context.OwnerKind))
                        {
                            if (debugLog)
                            {
                                Debug.Log($"{name}: Skipping step '{step.GetType().Name}' (owner filter).", this);
                            }
                            continue;
                        }

                        IEnumerator routine = null;
                        try
                        {
                            routine = step.Execute(context);
                        }
                        catch (Exception ex)
                        {
                            failure = ex;
                            break;
                        }

                        if (routine != null)
                        {
                            if (step.WaitForCompletion)
                            {
                                yield return StartCoroutine(RunCoroutineSafely(routine, ex =>
                                {
                                    if (failure == null) failure = ex;
                                }));
                                if (failure != null)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                Coroutine background = StartCoroutine(RunCoroutineSafely(routine, ex =>
                                {
                                    if (failure == null) failure = ex;
                                }));
                                if (background != null)
                                {
                                    _backgroundStepCoroutines.Add(background);
                                }
                            }
                        }

                        if (context.CancelRequested || !isActiveAndEnabled || failure != null)
                        {
                            break;
                        }
                    }
                }

                if (_backgroundStepCoroutines.Count > 0)
                {
                    for (int i = _backgroundStepCoroutines.Count - 1; i >= 0; i--)
                    {
                        var background = _backgroundStepCoroutines[i];
                        if (background != null)
                        {
                            yield return background;
                        }
                    }
                    _backgroundStepCoroutines.Clear();
                }
            }
            finally
            {
                CompleteAbilityRun(definition, state, cooldown, context);
            }

            if (failure != null)
            {
                string abilityName = definition != null ? definition.DisplayName : "Unknown";
                Debug.LogError($"{name}: Ability '{abilityName}' threw an exception and was aborted.\n{failure}", this);
            }
        }

        void CompleteAbilityRun(AbilityDefinition definition, AbilityRuntimeState state, float cooldownSeconds, AbilityRuntimeContext context)
        {
            bool skipCooldown = context != null && context.CancelRequested;

            if (_backgroundStepCoroutines.Count > 0)
            {
                for (int i = _backgroundStepCoroutines.Count - 1; i >= 0; i--)
                {
                    var background = _backgroundStepCoroutines[i];
                    if (background != null)
                    {
                        StopCoroutine(background);
                    }
                }
                _backgroundStepCoroutines.Clear();
            }

            if (state != null)
            {
                if (!skipCooldown)
                {
                    state.NextReadyTime = Time.time + Mathf.Max(0f, cooldownSeconds);
                }
                else
                {
                    if (!state.HasUnlimitedCharges)
                    {
                        state.RefillCharges();
                    }
                }

                state.IsRunning = false;
                state.RunningCoroutine = null;
            }

            _runningAbilities.Remove(definition);
            AbilityCompleted?.Invoke(definition);
        }

        IEnumerator RunCoroutineSafely(IEnumerator routine, Action<Exception> onException)
        {
            if (routine == null)
            {
                yield break;
            }

            try
            {
                while (true)
                {
                    bool moveNext;
                    try
                    {
                        moveNext = routine.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        onException?.Invoke(ex);
                        yield break;
                    }

                    if (!moveNext)
                    {
                        yield break;
                    }

                    yield return routine.Current;
                }
            }
            finally
            {
                if (routine is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        #region Inspector helpers

        public IReadOnlyList<AbilityDefinition> ActiveAbilities => startingAbilities;

        /// <summary>
        /// Read-only list of abilities that were granted at runtime (e.g., from skill tree).
        /// Visible in inspector for debugging.
        /// </summary>
        public IReadOnlyList<AbilityDefinition> RuntimeGrantedAbilities => _runtimeGrantedAbilities;

#if UNITY_EDITOR
        void OnValidate()
        {
            if (startingAbilities == null) startingAbilities = System.Array.Empty<AbilityDefinition>();
        }
#endif

        #endregion

        void SubscribeEnemyHealth()
        {
            if (CachedEnemyHealth != null)
            {
                CachedEnemyHealth.OnDied += HandleOwnerDied;
            }
        }

        void UnsubscribeEnemyHealth()
        {
            if (CachedEnemyHealth != null)
            {
                CachedEnemyHealth.OnDied -= HandleOwnerDied;
            }
        }

        void HandleOwnerDied()
        {
            CancelAllAbilities();
        }

        public void CancelAllAbilities()
        {
            CancelAllAbilitiesInternal();
        }

        void ApplyPassiveAbility(AbilityDefinition definition)
        {
            if (definition == null || !definition.IsPassive)
            {
                return;
            }

            if (_activePassiveEffects.ContainsKey(definition))
            {
                return;
            }

            var modifiers = definition.PassiveModifiers;
            if (modifiers != null)
            {
                // Process modifiers and group them by proc triggers
                ProcTriggerModifier currentTrigger = null;
                var controlledModifiers = new System.Collections.Generic.List<PassiveAbilityModifier>();
                bool hasProcTrigger = false;

                for (int i = 0; i < modifiers.Count; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier == null || !modifier.Enabled) continue;

                    if (modifier is ProcTriggerModifier trigger)
                    {
                        hasProcTrigger = true;

                        // If we had a previous trigger, finalize its controlled modifiers
                        if (currentTrigger != null && controlledModifiers.Count > 0)
                        {
                            currentTrigger.SetControlledModifiers(controlledModifiers.ToArray());
                            controlledModifiers.Clear();
                        }

                        // Apply the new trigger and track it
                        currentTrigger = trigger;
                        currentTrigger.SetAbilityDefinition(definition);
                        currentTrigger.Apply(this);
                        _activeProcTriggers.Add(currentTrigger);
                    }
                    else
                    {
                        if (currentTrigger != null)
                        {
                            // This modifier is controlled by the current trigger
                            controlledModifiers.Add(modifier);
                        }
                        else
                        {
                            // No trigger, apply modifier directly (permanent)

                            // Set ability definition if modifier supports it (for UI registration)
                            var setAbilityDefMethod = modifier.GetType().GetMethod("SetAbilityDefinition", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (setAbilityDefMethod != null)
                            {
                                setAbilityDefMethod.Invoke(modifier, new object[] { definition });
                            }

                            modifier.Apply(this);

                            // Track modifiers that have Update methods
                            var updateMethod = modifier.GetType().GetMethod("Update", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (updateMethod != null && !_activeModifiersWithUpdate.Contains(modifier))
                            {
                                _activeModifiersWithUpdate.Add(modifier);
                            }
                        }
                    }
                }

                // Finalize the last trigger's controlled modifiers
                if (currentTrigger != null && controlledModifiers.Count > 0)
                {
                    currentTrigger.SetControlledModifiers(controlledModifiers.ToArray());
                }

                // Notify UI for permanent passives (no proc trigger)
                if (!hasProcTrigger && PassiveBuffPanelController.Instance != null)
                {
                    PassiveBuffPanelController.Instance.RegisterPermanentPassive(definition);
                }
            }

            // Track in effects dictionary
            _activePassiveEffects.Add(definition, new PassiveRuntimeEffect());
        }

        void RemovePassiveAbility(AbilityDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (!_activePassiveEffects.ContainsKey(definition))
            {
                return;
            }

            var modifiers = definition.PassiveModifiers;
            if (modifiers != null)
            {
                foreach (var modifier in modifiers)
                {
                    if (modifier != null && modifier.Enabled)
                    {
                        modifier.Remove(this);

                        // Remove from active proc triggers list if it's a trigger
                        if (modifier is ProcTriggerModifier trigger)
                        {
                            _activeProcTriggers.Remove(trigger);
                        }

                        // Remove from active modifiers with update list
                        _activeModifiersWithUpdate.Remove(modifier);
                    }
                }
            }

            // Notify UI to remove passive
            if (PassiveBuffPanelController.Instance != null)
            {
                PassiveBuffPanelController.Instance.UnregisterPassive(definition);
            }

            _activePassiveEffects.Remove(definition);
        }


        sealed class PassiveRuntimeEffect
        {
            // Empty marker class for tracking applied passive abilities
        }

        void ClearPassiveEffects()
        {
            if (_activePassiveEffects.Count == 0)
            {
                return;
            }

            var abilities = new List<AbilityDefinition>(_activePassiveEffects.Keys);
            for (int i = 0; i < abilities.Count; i++)
            {
                RemovePassiveAbility(abilities[i]);
            }
        }

        public bool HasAnyUnlockedAbility()
        {
            if (_knownAbilities.Count > 0 || _runtimeGrantedAbilities.Count > 0)
            {
                return true;
            }

            if (startingAbilities != null)
            {
                for (int i = 0; i < startingAbilities.Length; i++)
                {
                    if (startingAbilities[i] != null)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        void CancelAllAbilitiesInternal()
        {
            foreach (var ability in _runningAbilities.ToArray())
            {
                if (_runtimeStates.TryGetValue(ability, out var state))
                {
                    if (state.RunningCoroutine != null)
                    {
                        StopCoroutine(state.RunningCoroutine);
                    }

                    state.RunningCoroutine = null;
                    state.IsRunning = false;
                }
            }

            _runningAbilities.Clear();
            foreach (var coroutine in _backgroundStepCoroutines)
            {
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                }
            }
            _backgroundStepCoroutines.Clear();
        }

        public void ForceResetRunner()
        {
            CancelAllAbilities();
            CancelAllAbilitiesInternal();
            foreach (var kvp in _runtimeStates)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.IsRunning = false;
                    kvp.Value.RunningCoroutine = null;
                    kvp.Value.ResetCooldown();
                }
            }
        }
    }
}



