using System.Collections;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Helper methods shared by ability steps that apply passive abilities to targets.
    /// </summary>
    public static class AbilityApplicationUtility
    {
        public static bool TryApplyAbility(AbilityDefinition ability, Transform targetTransform, AbilityRuntimeContext context, bool allowStacking, float durationSeconds)
        {
            if (!ability || !ability.IsPassive || !targetTransform)
            {
                return false;
            }

            AbilityRunner targetRunner = targetTransform.GetComponent<AbilityRunner>() ?? targetTransform.GetComponentInParent<AbilityRunner>();
            if (!targetRunner)
            {
                return false;
            }

            if (!allowStacking)
            {
                foreach (var existingAbility in targetRunner.EnumerateAbilities())
                {
                    if (existingAbility == ability)
                    {
                        return false;
                    }
                }
            }

            bool added = targetRunner.TryAddAbility(ability);
            if (!added)
            {
                return false;
            }

            if (durationSeconds > 0f)
            {
                targetRunner.StartCoroutine(RemoveAfterDelay(targetRunner, ability, durationSeconds));
            }

            return true;
        }

        static IEnumerator RemoveAfterDelay(AbilityRunner runner, AbilityDefinition ability, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (runner != null && ability != null)
            {
                runner.TryRemoveAbility(ability, cancelIfRunning: false);
            }
        }
    }
}



