using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Awards health, mana, or attribute points to the caster or their target using AbilityEffectUtility helpers.")]
    [MovedFrom("AbilitySystem")]
    public sealed class GrantResourceStep : AbilityStep
    {
        private enum ResourceType
        {
            Health,
            Mana,
            AttributePoints
        }

        private enum ResourceRecipient
        {
            Owner,
            Target
        }

        [SerializeField]
        [Tooltip("Which resource to grant.")]
        private ResourceType resourceType = ResourceType.Mana;

        [SerializeField]
        [Tooltip("Who receives the resource.")]
        private ResourceRecipient recipient = ResourceRecipient.Owner;

        [SerializeField]
        [Tooltip("Amount granted. For attribute points, values are rounded to integers.")]
        private float amount = 10f;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform target = recipient == ResourceRecipient.Owner ? context.Transform : context.Target;
            if (!target)
            {
                yield break;
            }

            switch (resourceType)
            {
                case ResourceType.Health:
                    if (amount > 0f)
                    {
                        AbilityEffectUtility.TryHeal(target, Mathf.RoundToInt(amount));
                    }
                    break;
                case ResourceType.Mana:
                    AbilityEffectUtility.TryGrantMana(context, amount, recipient == ResourceRecipient.Target);
                    break;
                case ResourceType.AttributePoints:
                    if (PlayerStats.Instance != null)
                    {
                        int points = Mathf.RoundToInt(amount);
                        if (points > 0)
                        {
                            PlayerStats.Instance.GrantAttributePoints(points);
                        }
                    }
                    break;
            }

            yield break;
        }
    }
}






