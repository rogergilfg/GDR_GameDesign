using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Removes abilities from the owner after an optional delay. Useful for temporary buffs or cleanup.
    /// </summary>
    [System.Serializable]
    [AbilityComponentDescription("Removes abilities from the owner after an optional delay (useful for temporary effects).")]
    public sealed class RemoveAbilityStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("When true, the ability that owns this step will be removed.")]
        private bool removeThisAbility = true;

        [SerializeField]
        [Tooltip("Additional abilities to remove from the owner.")]
        private List<AbilityDefinition> additionalAbilities = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Seconds to wait before removing the abilities.")]
        [Min(0f)]
        private float delaySeconds = 0f;

        [SerializeField]
        [Tooltip("If true, cancel the ability if it is currently running when removed.")]
        private bool cancelIfRunning = true;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (context == null || context.Runner == null)
            {
                yield break;
            }

            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            AbilityRunner runner = context.Runner;

            if (removeThisAbility && context.Definition != null)
            {
                runner.TryRemoveAbility(context.Definition, cancelIfRunning);
            }

            if (additionalAbilities == null || additionalAbilities.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < additionalAbilities.Count; i++)
            {
                AbilityDefinition ability = additionalAbilities[i];
                if (!ability)
                {
                    continue;
                }

                runner.TryRemoveAbility(ability, cancelIfRunning);
            }
        }
    }
}



