using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Lightweight helper that periodically polls a set of abilities and fires the first one that is ready.
    /// Intended for prototyping enemy behaviours before integrating with a full AI director.
    /// </summary>
    [DisallowMultipleComponent]
    [MovedFrom("AbilitySystem")]
    public sealed class EnemyAbilityAutoCaster : MonoBehaviour
    {
        [SerializeField] private AbilityRunner abilityRunner;
        [SerializeField] private AbilityDefinition[] abilities = System.Array.Empty<AbilityDefinition>();
        [SerializeField] private float checkInterval = 0.2f;
        [SerializeField] private bool randomizeOrder = false;

        float _nextCheck;

        void Reset()
        {
            abilityRunner = GetComponent<AbilityRunner>();
        }

        void Awake()
        {
            if (!abilityRunner) abilityRunner = GetComponent<AbilityRunner>();
        }

        void Update()
        {
            if (!abilityRunner || abilities == null || abilities.Length == 0) return;
            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + Mathf.Max(0.02f, checkInterval);

            if (randomizeOrder)
            {
                int start = Random.Range(0, abilities.Length);
                for (int i = 0; i < abilities.Length; i++)
                {
                    var ability = abilities[(start + i) % abilities.Length];
                    if (TryCast(ability)) break;
                }
            }
            else
            {
                foreach (var ability in abilities)
                {
                    if (TryCast(ability)) break;
                }
            }
        }

        bool TryCast(AbilityDefinition ability)
        {
            if (!ability || ability.IsPassive) return false;
            var state = abilityRunner.GetState(ability);
            if (state == null || !state.IsReady) return false;

            var parameters = new AbilityActivationParameters
            {
                Target = abilityRunner.CachedTarget
            };

            return abilityRunner.TryActivateAbility(ability, parameters);
        }
    }
}





