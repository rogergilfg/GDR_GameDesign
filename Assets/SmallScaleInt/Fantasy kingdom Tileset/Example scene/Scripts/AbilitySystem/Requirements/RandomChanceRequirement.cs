using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Rolls a random chance check and only allows activation when the roll falls within the defined success range.")]
    public sealed class RandomChanceRequirement : AbilityRequirement
    {
        [SerializeField]
        [Range(0f, 1f)]
        [Tooltip("Probability that the ability may proceed (0 = never, 1 = always).")]
        private float successProbability = 0.5f;

        [SerializeField]
        [Tooltip("Optional seed offset to decorrelate abilities when Random.state is reused.")]
        private int randomSeedOffset = 0;

        public override bool IsMet(AbilityRuntimeContext context, out string failureReason)
        {
            failureReason = string.Empty;
            if (successProbability >= 1f) return true;
            if (successProbability <= 0f)
            {
                failureReason = "Random chance failed";
                return false;
            }

            int seed = randomSeedOffset;
            if (context.Owner)
            {
                seed += context.Owner.GetInstanceID();
            }

            var previousState = Random.state;
            if (randomSeedOffset != 0)
            {
                Random.InitState(seed + (int)(Time.time * 1000f));
            }

            bool success = Random.value <= successProbability;
            if (randomSeedOffset != 0)
            {
                Random.state = previousState;
            }

            if (!success)
            {
                failureReason = "Random chance failed";
            }

            return success;
        }
    }
}






