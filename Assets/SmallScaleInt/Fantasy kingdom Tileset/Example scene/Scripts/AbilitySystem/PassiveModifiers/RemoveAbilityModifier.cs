using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Passive version of RemoveAbilityStep – automatically strips abilities after a delay.
    /// </summary>
    [System.Serializable]
    [PassiveModifierDescription("Removes this passive ability (and optionally others) after waiting for a delay.")]
    public sealed class RemoveAbilityModifier : PassiveAbilityModifier
    {
        [SerializeField]
        [Tooltip("When true, the passive ability that owns this modifier will be removed.")]
        private bool removeThisAbility = true;

        [SerializeField]
        [Tooltip("Any additional abilities to remove from the owner.")]
        private List<AbilityDefinition> additionalAbilities = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Seconds to wait before removing the abilities.")]
        [Min(0f)]
        private float delaySeconds = 0f;

        [SerializeField]
        [Tooltip("If true, cancel the ability if it is currently running when removed.")]
        private bool cancelIfRunning = true;

        private AbilityDefinition _ownerAbility;
        private AbilityRunner _runner;
        private Coroutine _removalRoutine;

        /// <summary>
        /// Called from AbilityRunner (via reflection) so the modifier knows what ability owns it.
        /// </summary>
        public void SetAbilityDefinition(AbilityDefinition definition)
        {
            _ownerAbility = definition;
        }

        public override void Apply(AbilityRunner runner)
        {
            if (!enabled || runner == null)
            {
                return;
            }

            _runner = runner;

            if (_removalRoutine != null)
            {
                runner.StopCoroutine(_removalRoutine);
                _removalRoutine = null;
            }

            _removalRoutine = runner.StartCoroutine(RemoveAfterDelay());
        }

        public override void Remove(AbilityRunner runner)
        {
            if (_removalRoutine != null && _runner != null)
            {
                _runner.StopCoroutine(_removalRoutine);
                _removalRoutine = null;
            }

            _runner = null;
        }

        IEnumerator RemoveAfterDelay()
        {
            float delay = Mathf.Max(0f, delaySeconds);
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            TriggerRemoval();
        }

        void TriggerRemoval()
        {
            var runner = _runner;
            if (runner == null)
            {
                return;
            }

            if (removeThisAbility && _ownerAbility != null)
            {
                runner.TryRemoveAbility(_ownerAbility, cancelIfRunning);
            }

            if (additionalAbilities == null || additionalAbilities.Count == 0)
            {
                return;
            }

            for (int i = 0; i < additionalAbilities.Count; i++)
            {
                var ability = additionalAbilities[i];
                if (!ability)
                {
                    continue;
                }

                runner.TryRemoveAbility(ability, cancelIfRunning);
            }
        }
    }
}



