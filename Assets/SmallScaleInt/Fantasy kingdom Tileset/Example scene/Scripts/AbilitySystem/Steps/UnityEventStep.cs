using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Events;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Invokes a serialized UnityEvent when the step fires, letting designers hook arbitrary responses.")]
    [MovedFrom("AbilitySystem")]
    public sealed class UnityEventStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("Event invoked when the step executes.")]
        private UnityEvent onExecute;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            onExecute?.Invoke();
            yield break;
        }
    }
}





