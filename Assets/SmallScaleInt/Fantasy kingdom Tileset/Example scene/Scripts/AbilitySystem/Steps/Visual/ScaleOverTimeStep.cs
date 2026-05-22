using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [System.Serializable]
    [AbilityComponentDescription("Animates a transform's scale over time using easing curves, then optionally restores it on completion.")]
    [MovedFrom("AbilitySystem")]
    public sealed class ScaleOverTimeStep : AbilityStep
    {
        private enum TargetMode
        {
            Owner,
            OwnerAnimator,
            Custom
        }

        [Header("Target")]
        [SerializeField]
        [Tooltip("Select which transform to scale (owner root, owner animator, or a custom transform).")]
        private TargetMode targetMode = TargetMode.Owner;

        [SerializeField]
        [Tooltip("Transform to scale when Target Mode is Custom.")]
        private Transform customTarget;

        [Header("Scale Animation")]
        [SerializeField]
        [Tooltip("Multiplier applied relative to the starting scale.")]
        private Vector3 scaleMultiplier = new Vector3(1.2f, 1.2f, 1.2f);

        [SerializeField]
        [Tooltip("Time spent scaling from the original size to the multiplied size (seconds).")]
        private float growDuration = 0.2f;

        [SerializeField]
        [Tooltip("Time the scaled size is held before shrinking (seconds).")]
        private float holdDuration = 0f;

        [SerializeField]
        [Tooltip("Time spent returning to the original scale (seconds).")]
        private float shrinkDuration = 0.2f;

        [SerializeField]
        [Tooltip("Use SmoothStep easing instead of linear interpolation.")]
        private bool useSmoothStep = true;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            Transform target = ResolveTarget(context);
            if (!target)
            {
                yield break;
            }

            Vector3 originalScale = target.localScale;
            Vector3 targetScale = new Vector3(
                originalScale.x * scaleMultiplier.x,
                originalScale.y * scaleMultiplier.y,
                originalScale.z * scaleMultiplier.z);

            float grow = Mathf.Max(0f, growDuration);
            float hold = Mathf.Max(0f, holdDuration);
            float shrink = Mathf.Max(0f, shrinkDuration);

            // Grow phase
            if (grow > 0f)
            {
                float elapsed = 0f;
                while (elapsed < grow)
                {
                    if (context.CancelRequested)
                    {
                        target.localScale = originalScale;
                        yield break;
                    }

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / grow);
                    if (useSmoothStep)
                    {
                        t = Mathf.SmoothStep(0f, 1f, t);
                    }

                    target.localScale = Vector3.LerpUnclamped(originalScale, targetScale, t);
                    yield return null;
                }
            }

            target.localScale = targetScale;

            // Hold phase
            if (hold > 0f)
            {
                float end = Time.time + hold;
                while (Time.time < end)
                {
                    if (context.CancelRequested)
                    {
                        target.localScale = originalScale;
                        yield break;
                    }

                    yield return null;
                }
            }

            // Shrink phase
            if (shrink > 0f)
            {
                float elapsed = 0f;
                while (elapsed < shrink)
                {
                    if (context.CancelRequested)
                    {
                        target.localScale = originalScale;
                        yield break;
                    }

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / shrink);
                    if (useSmoothStep)
                    {
                        t = Mathf.SmoothStep(0f, 1f, t);
                    }

                    target.localScale = Vector3.LerpUnclamped(targetScale, originalScale, t);
                    yield return null;
                }
            }

            target.localScale = originalScale;
        }

        Transform ResolveTarget(AbilityRuntimeContext context)
        {
            return targetMode switch
            {
                TargetMode.OwnerAnimator => context.Animator ? context.Animator.transform : context.Transform,
                TargetMode.Custom => customTarget ? customTarget : context.Transform,
                _ => context.Transform
            };
        }
    }
}






