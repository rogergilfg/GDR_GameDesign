using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;


namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [CreateAssetMenu(menuName = "Ability System/Ability Definition", fileName = "AbilityDefinition")]
    [MovedFrom("AbilitySystem")]
    public class AbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField]
        [Tooltip("Optional identifier used by code to locate a definition at runtime.")]
        private string abilityId = Guid.NewGuid().ToString();

        [SerializeField]
        [Tooltip("Display name exposed in UI.")]
        private string displayName = "New Ability";

        [SerializeField]
        [TextArea(3, 6)]
        [Tooltip("Description of what this ability does, shown in tooltips.")]
        private string description = "";

        [SerializeField]
        [Tooltip("Icon presented in action bars or ability tooltips.")]
        private Sprite icon;

        [Header("Usage")]
        [SerializeField]
        [Tooltip("Mana cost applied to PlayerMana when the ability fires. 0 = free.")]
        private float manaCost = 0f;

        [SerializeField]
        [Tooltip("Seconds before this ability can be fired again.")]
        private float cooldownSeconds = 1.0f;

        [SerializeField]
        [Tooltip("Maximum simultaneous charges. 0 or less = unlimited.")]
        private int maxCharges = 0;

        [SerializeField]
        [Tooltip("Charges available when the runner spawns. -1 uses max charges.")]
        private int startingCharges = -1;

        [SerializeField]
        [Tooltip("When enabled, the runner must not already be casting another ability.")]
        private bool requireIdleRunner = true;

        [Header("Activation Requirements")]
        [SerializeField]
        [SerializeReference]
        [Tooltip("Requirements that must succeed before the ability can execute.")]
        private List<AbilityRequirement> activationRequirements = new();

        [Header("Execution Sequence")]
        [SerializeReference]
        [Tooltip("Ordered list of steps executed when the ability fires.")]
        private List<AbilityStep> steps = new();

        [Header("Passive Effects")]
        [SerializeField]
        [Tooltip("Marks this ability as passive so it grants bonuses automatically and stays out of action slots.")]
        private bool isPassive = false;

        [SerializeField]
        [Tooltip("Marks this ability as a debuff (negative effect). Affects UI frame color: red for debuffs, green for buffs.")]
        private bool isDebuff = false;

        [SerializeReference]
        [Tooltip("Passive modifiers that grant bonuses while this ability is equipped.")]
        private List<PassiveAbilityModifier> passiveModifiers = new();

        public string AbilityId => abilityId;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public float ManaCost => manaCost;
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);
        public int MaxCharges => maxCharges;
        public int StartingCharges => startingCharges;
        public bool RequireIdleRunner => requireIdleRunner;
        public IReadOnlyList<AbilityRequirement> ActivationRequirements => activationRequirements;
        public IReadOnlyList<AbilityStep> Steps => steps;
        public bool IsPassive => isPassive;
        public bool IsDebuff => isDebuff;
        public IReadOnlyList<PassiveAbilityModifier> PassiveModifiers => passiveModifiers;

        public AbilityRuntimeState CreateRuntimeState()
        {
            return new AbilityRuntimeState(this);
        }

        public void AddRequirement(AbilityRequirement requirement)
        {
            if (requirement == null) return;
            activationRequirements ??= new List<AbilityRequirement>();
            activationRequirements.Add(requirement);
        }

        public void AddStep(AbilityStep step)
        {
            if (step == null) return;
            steps ??= new List<AbilityStep>();
            steps.Add(step);
        }

        public void ClearRequirements() => activationRequirements?.Clear();
        public void ClearSteps() => steps?.Clear();

#if UNITY_EDITOR
        void OnValidate()
        {
            activationRequirements ??= new List<AbilityRequirement>();
            steps ??= new List<AbilityStep>();
            passiveModifiers ??= new List<PassiveAbilityModifier>();
        }
#endif

        public override string ToString() => string.IsNullOrEmpty(displayName) ? base.ToString() : displayName;
    }

    [Serializable]
    public sealed class AbilityRuntimeState
    {
        public AbilityDefinition Definition { get; }
        public float NextReadyTime { get; set; } // Legacy - kept for compatibility
        public bool IsRunning { get; set; }
        public Coroutine RunningCoroutine { get; set; }

        // New charge system - each charge has its own cooldown time
        private List<float> chargeReadyTimes = new List<float>();
        private int maxCharges;

        public AbilityRuntimeState(AbilityDefinition definition)
        {
            Definition = definition;
            NextReadyTime = -999f;

            maxCharges = definition.MaxCharges;
            if (maxCharges <= 0)
            {
                // Unlimited charges
                maxCharges = 0;
            }
            else
            {
                // Initialize charge ready times
                int starting = definition.StartingCharges;
                // Treat non-positive starting values (including 0) as "start full" to be migration-safe
                int initialCharges = starting > 0 ? Mathf.Clamp(starting, 0, maxCharges) : maxCharges;

                chargeReadyTimes = new List<float>(maxCharges);
                for (int i = 0; i < maxCharges; i++)
                {
                    // Available charges start at -999 (ready)
                    if (i < initialCharges)
                    {
                        chargeReadyTimes.Add(-999f);
                    }
                    else
                    {
                        // For missing charges at start, schedule a cooldown so they refill over time
                        float cd = Mathf.Max(0f, definition.CooldownSeconds);
                        float readyAt = cd > 0f ? Time.time + cd : -999f;
                        chargeReadyTimes.Add(readyAt);
                    }
                }
            }
        }

        public bool HasUnlimitedCharges => maxCharges <= 0;

        public int ChargesRemaining
        {
            get
            {
                if (HasUnlimitedCharges) return int.MaxValue;

                int count = 0;
                float currentTime = Time.time;
                for (int i = 0; i < chargeReadyTimes.Count; i++)
                {
                    if (currentTime >= chargeReadyTimes[i])
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool HasCharges => HasUnlimitedCharges || ChargesRemaining > 0;

        // Get the time when the next charge will be ready
        public float GetNextChargeReadyTime()
        {
            if (HasUnlimitedCharges) return -999f;

            float earliest = float.MaxValue;
            float currentTime = Time.time;

            for (int i = 0; i < chargeReadyTimes.Count; i++)
            {
                float readyTime = chargeReadyTimes[i];
                if (readyTime > currentTime && readyTime < earliest)
                {
                    earliest = readyTime;
                }
            }

            return earliest == float.MaxValue ? -999f : earliest;
        }

        public bool IsOffCooldown => !IsRunning && ChargesRemaining > 0;

        public bool IsReady => IsOffCooldown && HasCharges;

        // Returns the cooldown time for the first charge that's on cooldown
        public float GetCooldownFillAmount()
        {
            if (HasUnlimitedCharges || Definition.CooldownSeconds <= 0f) return 0f;

            float nextReadyTime = GetNextChargeReadyTime();
            if (nextReadyTime < 0f) return 0f; // All charges ready

            float timeRemaining = nextReadyTime - Time.time;
            if (timeRemaining <= 0f) return 0f;

            return Mathf.Clamp01(timeRemaining / Definition.CooldownSeconds);
        }

        public void ConsumeCharge()
        {
            if (HasUnlimitedCharges) return;

            // Find the first ready charge and put it on cooldown
            float currentTime = Time.time;
            for (int i = 0; i < chargeReadyTimes.Count; i++)
            {
                if (currentTime >= chargeReadyTimes[i])
                {
                    chargeReadyTimes[i] = currentTime + Definition.CooldownSeconds;

                    // Update legacy NextReadyTime to the earliest charge
                    NextReadyTime = GetNextChargeReadyTime();
                    return;
                }
            }
        }

        public void RefillCharges(int amount = 1)
        {
            if (HasUnlimitedCharges) return;

            // Refill the specified number of charges
            int refilled = 0;
            float currentTime = Time.time;

            for (int i = 0; i < chargeReadyTimes.Count && refilled < amount; i++)
            {
                if (chargeReadyTimes[i] > currentTime)
                {
                    chargeReadyTimes[i] = -999f; // Make it ready
                    refilled++;
                }
            }
        }

        // Get max charges for UI display
        public int GetMaxCharges()
        {
            return HasUnlimitedCharges ? 0 : maxCharges;
        }

        public void ResetCooldown()
        {
            float readyTime = Time.time;
            for (int i = 0; i < chargeReadyTimes.Count; i++)
            {
                chargeReadyTimes[i] = readyTime - Mathf.Abs(i * 0.01f);
            }
            NextReadyTime = readyTime;
        }
    }

    [Serializable]
    public abstract class AbilityRequirement
    {
        [SerializeField]
        [Tooltip("Actor types that should evaluate this requirement. Others automatically pass.")]
        AbilityActorKindMask ownerFilter = AbilityActorKindMask.All;

        public AbilityActorKindMask OwnerFilter => ownerFilter;

        public bool Check(AbilityRuntimeContext context, out string failureReason)
        {
            if (!ShouldEvaluateForOwner(context.OwnerKind))
            {
                failureReason = string.Empty;
                return true;
            }

            return IsMet(context, out failureReason);
        }

        protected virtual bool ShouldEvaluateForOwner(AbilityActorKind ownerKind)
        {
            AbilityActorKindMask mask = ownerKind.ToMask();
            AbilityActorKindMask filter = ownerFilter == AbilityActorKindMask.None ? AbilityActorKindMask.All : ownerFilter;
            return (filter & mask) != 0;
        }

        public abstract bool IsMet(AbilityRuntimeContext context, out string failureReason);
    }

    [Serializable]
    public abstract class AbilityStep
    {
        [SerializeField]
        [Tooltip("Actor types that should execute this step. Others skip it completely.")]
        AbilityActorKindMask ownerFilter = AbilityActorKindMask.All;

        [SerializeField]
        [Tooltip("When enabled, the runner waits for this step to finish before moving to the next one. Disable for fire-and-forget behaviour.")]
        bool waitForCompletion = true;

        public AbilityActorKindMask OwnerFilter => ownerFilter;
        public bool WaitForCompletion => waitForCompletion;

        public bool ShouldExecuteForOwner(AbilityActorKind ownerKind)
        {
            AbilityActorKindMask mask = ownerKind.ToMask();
            AbilityActorKindMask filter = ownerFilter == AbilityActorKindMask.None ? AbilityActorKindMask.All : ownerFilter;
            return (filter & mask) != 0;
        }

        public abstract IEnumerator Execute(AbilityRuntimeContext context);
    }
}






