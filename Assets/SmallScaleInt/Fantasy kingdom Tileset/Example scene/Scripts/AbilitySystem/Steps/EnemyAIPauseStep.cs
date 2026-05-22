using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Temporarily toggles EnemyAI external pause while the step plays, ensuring scripted casts aren't interrupted.")]
    [MovedFrom("AbilitySystem")]
    public sealed class EnemyAIPauseStep : AbilityStep
    {
        [SerializeField]
        [Tooltip("Whether to call EnemyAI.SetExternalPause(true) when the step starts.")]
        private bool pause = true;

        [SerializeField]
        [Tooltip("Duration the AI remains paused. <= 0 skips waiting.")]
        private float duration = 0.2f;

        [SerializeField]
        [Tooltip("Automatically resume the AI when the step finishes.")]
        private bool resumeOnExit = true;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            EnemyAI ai = context.EnemyAI;
            if (!ai) yield break;

            if (pause)
            {
                ai.SetExternalPause(true);
            }
            else
            {
                ai.SetExternalPause(false);
            }

            if (duration > 0f)
            {
                float end = Time.time + duration;
                while (Time.time < end)
                {
                    if (context.CancelRequested) break;
                    yield return null;
                }
            }

            if (resumeOnExit && pause)
            {
                ai.SetExternalPause(false);
            }
        }
    }
}





