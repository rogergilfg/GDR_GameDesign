using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Pauses the sequence for a duration (with optional random variance) before continuing to the next step.")]
    [MovedFrom("AbilitySystem")]
    public sealed class WaitStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("Exact wait duration in seconds.")]
        private float duration = 0.25f;

        [SerializeField]
        [Tooltip("Optional random additional time added to the duration.")]
        private Vector2 randomVariance = Vector2.zero;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            float total = Mathf.Max(0f, duration + Random.Range(randomVariance.x, randomVariance.y));
            float end = Time.time + total;
            while (Time.time < end)
            {
                if (context.CancelRequested || !context.Owner) yield break;
                yield return null;
            }
        }
    }
}





