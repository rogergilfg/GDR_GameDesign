using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    /// <summary>
    /// Forces the runner to wait for a cast time before continuing to the next step.
    /// Displays a temporary cast bar above the owner and supports configurable interrupt conditions.
    /// </summary>
    [System.Serializable]
    [AbilityComponentDescription("Waits for a cast duration before executing the next step. Shows a cast bar and can be interrupted by damage or movement.")]
    public sealed class CastTimeStep : AbilityStep
    {
        [Header("Timing")]
        [SerializeField, Min(0f)]
        [Tooltip("Base cast duration in seconds.")]
        private float castDuration = 2f;

        [SerializeField]
        [Tooltip("Optional additive random variance applied to the cast duration.")]
        private Vector2 durationVariance = Vector2.zero;

        [Header("Interruption")]
        [SerializeField]
        [Tooltip("When disabled the cast is interrupted as soon as the owner begins moving.")]
        private bool canCastWhileMoving = false;

        [SerializeField]
        [Tooltip("When enabled, taking damage cancels the cast.")]
        private bool interruptOnDamage = true;

        [SerializeField]
        [Tooltip("When enabled, the ability sequence is cancelled if the cast is interrupted.")]
        private bool cancelAbilityOnInterrupt = true;

        [Header("Cast Bar")]
        [SerializeField]
        [Tooltip("Show a progress bar above the owner while casting.")]
        private bool showCastBar = true;

        [SerializeField]
        [Tooltip("World-space size of the cast bar (default roughly equals 25x5 pixels).")]
        private Vector2 barSize = new Vector2(35f, 5f);

        [SerializeField]
        [Tooltip("Offset from the owner's pivot for the cast bar.")]
        private Vector2 barOffset = new Vector2(0f, 0.3f);

        [SerializeField]
        [Tooltip("Background colour of the cast bar.")]
        private Color barBackColor = new Color(0f, 0f, 0f, 0.8f);

        [SerializeField]
        [Tooltip("Fill colour of the cast bar.")]
        private Color barFillColor = new Color(0.2f, 0.85f, 1f, 0.95f);

        [Header("Cast Effect")]
        [SerializeField]
        [Tooltip("Optional prefab spawned while the cast is active.")]
        private GameObject castEffectPrefab;

        [SerializeField]
        [Tooltip("Additional offset applied when spawning the cast effect.")]
        private Vector3 castEffectOffset = Vector3.zero;

        [SerializeField]
        [Tooltip("Parent the cast effect to the owner so it follows their movement.")]
        private bool parentCastEffectToOwner = true;

        [SerializeField]
        [Tooltip("Seconds after completion to destroy the cast effect (0 = immediate).")]
        private float castEffectCleanupDelay = 0f;

        [Header("Animation")]
        [SerializeField]
        [Tooltip("Animator trigger fired when casting begins (defaults to \"Taunt\"). Leave empty to skip.")]
        private string castTriggerName = "Special1";

        [SerializeField]
        [Tooltip("Optional trigger fired when the cast ends or is interrupted.")]
        private string exitTriggerName = string.Empty;

        [SerializeField]
        [Tooltip("Re-fire the cast trigger periodically to keep the animation looping during long casts.")]
        private bool repeatCastTrigger = true;

        [SerializeField, Min(0.05f)]
        [Tooltip("Seconds between cast trigger refreshes when repeat is enabled.")]
        private float castTriggerRefreshInterval = 0.3f;

        [SerializeField]
        [Tooltip("Name of the animator state to loop (optional). When set, the step can play the state directly.")]
        private string castStateName = string.Empty;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized time where the looping segment begins (0 = start of the clip).")]
        private float loopStartNormalizedTime = 0f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized time where the looping segment ends (1 = end of the clip).")]
        private float loopEndNormalizedTime = 1f;

        [SerializeField]
        [Tooltip("When enabled, the animator state is restarted at the specified normalized time window each refresh.")]
        private bool useLoopWindow = false;

        public override IEnumerator Execute(AbilityRuntimeContext context)
        {
            if (context == null || context.Owner == null)
            {
                yield break;
            }

            float totalDuration = Mathf.Max(0f, castDuration + Random.Range(durationVariance.x, durationVariance.y));
            if (totalDuration <= 0f)
            {
                yield break;
            }

            CastBarVisual bar = null;
            if (showCastBar)
            {
                bar = CastBarVisual.Create(context.Transform, barOffset, barSize, barBackColor, barFillColor);
            }

            GameObject effectInstance = null;
            if (castEffectPrefab != null)
            {
                Vector3 spawnPos = context.Transform.position + castEffectOffset;
                Transform parent = parentCastEffectToOwner ? context.Transform : null;
                effectInstance = Object.Instantiate(castEffectPrefab, spawnPos, Quaternion.identity, parent);
            }

            bool interrupted = false;
            System.Action<int> damageTakenHandler = null;
            UnityAction neutralDamageHandler = null;

            string activeCastTrigger = (!string.IsNullOrEmpty(castTriggerName) && context.Animator != null)
                ? castTriggerName
                : null;
            Animator animator = context.Animator;
            float loopDuration = ComputeLoopDuration(animator);
            float nextTriggerRefresh = float.PositiveInfinity;
            float refreshInterval = Mathf.Max(0.05f, castTriggerRefreshInterval);

            if (!string.IsNullOrEmpty(activeCastTrigger))
            {
                animator?.SetTrigger(activeCastTrigger);
                if (!string.IsNullOrEmpty(castStateName) && animator != null)
                {
                    animator.Play(castStateName, 0, Mathf.Clamp01(loopStartNormalizedTime));
                }
                if (repeatCastTrigger)
                {
                    if (loopDuration > 0.05f && useLoopWindow)
                    {
                        nextTriggerRefresh = loopDuration;
                    }
                    else
                    {
                        nextTriggerRefresh = refreshInterval;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(castStateName) && animator != null)
            {
                animator.Play(castStateName, 0, Mathf.Clamp01(loopStartNormalizedTime));
                if (repeatCastTrigger)
                {
                    if (loopDuration > 0.05f && useLoopWindow)
                    {
                        nextTriggerRefresh = loopDuration;
                    }
                    else
                    {
                        nextTriggerRefresh = refreshInterval;
                    }
                }
            }

            if (interruptOnDamage)
            {
                if (context.PlayerHealth != null)
                {
                    damageTakenHandler = _ => interrupted = true;
                    context.PlayerHealth.OnDamageTaken += damageTakenHandler;
                }
                else if (context.EnemyHealth != null)
                {
                    damageTakenHandler = _ => interrupted = true;
                    context.EnemyHealth.OnDamageTaken += damageTakenHandler;
                }
                else if (context.NeutralAI != null)
                {
                    neutralDamageHandler = () => interrupted = true;
                    context.NeutralAI.onDamaged.AddListener(neutralDamageHandler);
                }
            }

            float elapsed = 0f;
            while (!interrupted && elapsed < totalDuration)
            {
                if (context.CancelRequested || context.Owner == null)
                {
                    interrupted = true;
                    break;
                }

                if (!canCastWhileMoving)
                {
                    if ((context.TopDownController && context.TopDownController.isMoving) ||
                        (context.EnemyAI && context.EnemyAI.isMoving) ||
                        (context.NeutralAI && context.NeutralAI.isMoving))
                    {
                        interrupted = true;
                        break;
                    }
                }

                elapsed += Time.deltaTime;

                if (!float.IsPositiveInfinity(nextTriggerRefresh) && elapsed >= nextTriggerRefresh)
                {
                    if (!string.IsNullOrEmpty(castStateName) && animator != null)
                    {
                        animator.Play(castStateName, 0, Mathf.Clamp01(loopStartNormalizedTime));
                    }
                    else if (!string.IsNullOrEmpty(activeCastTrigger))
                    {
                        animator?.SetTrigger(activeCastTrigger);
                    }

                    if (loopDuration > 0.05f && useLoopWindow)
                    {
                        nextTriggerRefresh += loopDuration;
                    }
                    else
                    {
                        nextTriggerRefresh += refreshInterval;
                    }
                }
                float pct = Mathf.Clamp01(elapsed / totalDuration);
                bar?.SetProgress(pct);

                yield return null;
            }

            bar?.Dispose();

            if (effectInstance != null)
            {
                float delay = Mathf.Max(0f, castEffectCleanupDelay);
                Object.Destroy(effectInstance, delay);
            }

            if (damageTakenHandler != null)
            {
                if (context.PlayerHealth != null)
                {
                    context.PlayerHealth.OnDamageTaken -= damageTakenHandler;
                }
                else if (context.EnemyHealth != null)
                {
                    context.EnemyHealth.OnDamageTaken -= damageTakenHandler;
                }
            }

            if (neutralDamageHandler != null && context.NeutralAI != null)
            {
                context.NeutralAI.onDamaged.RemoveListener(neutralDamageHandler);
            }

            if (!string.IsNullOrEmpty(exitTriggerName) && animator != null)
            {
                animator.SetTrigger(exitTriggerName);
            }

            if (!string.IsNullOrEmpty(activeCastTrigger) && string.IsNullOrEmpty(exitTriggerName) && animator != null)
            {
                animator.ResetTrigger(activeCastTrigger);
            }

            if (interrupted && cancelAbilityOnInterrupt)
            {
                context.RequestCancel();
            }
        }

        sealed class CastBarVisual
        {
            private static Sprite s_Pixel;

            private readonly Transform root;
            private readonly SpriteRenderer back;
            private readonly SpriteRenderer fill;
            private readonly Vector2 size;

            CastBarVisual(Transform root, SpriteRenderer back, SpriteRenderer fill, Vector2 size)
            {
                this.root = root;
                this.back = back;
                this.fill = fill;
                this.size = size;
            }

            public static CastBarVisual Create(Transform owner, Vector2 offset, Vector2 size, Color backColor, Color fillColor)
            {
                if (owner == null)
                {
                    return null;
                }

                EnsurePixelSprite();

                Transform barRoot = new GameObject("CastBar").transform;
                barRoot.SetParent(owner, false);
                barRoot.localPosition = offset;

                var backGO = new GameObject("Back");
                backGO.transform.SetParent(barRoot, false);
                var backRenderer = backGO.AddComponent<SpriteRenderer>();
                backRenderer.sprite = s_Pixel;
                backRenderer.color = backColor;
                backRenderer.sortingOrder = 11000;

                var fillGO = new GameObject("Fill");
                fillGO.transform.SetParent(barRoot, false);
                var fillRenderer = fillGO.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = s_Pixel;
                fillRenderer.color = fillColor;
                fillRenderer.sortingOrder = 11001;

                var visual = new CastBarVisual(barRoot, backRenderer, fillRenderer, size);
                visual.SetProgress(0f);
                return visual;
            }

            public void SetProgress(float pct)
            {
                pct = Mathf.Clamp01(pct);
                if (back)
                {
                    back.transform.localPosition = Vector3.zero;
                    back.transform.localScale = new Vector3(size.x, size.y, 1f);
                }

                if (fill)
                {
                    float width = size.x * pct;
                    fill.transform.localScale = new Vector3(width, size.y, 1f);
                    fill.transform.localPosition = Vector3.zero;
                }
            }

            public void Dispose()
            {
                if (root != null)
                {
                    Object.Destroy(root.gameObject);
                }
            }

            static void EnsurePixelSprite()
            {
                if (s_Pixel != null)
                {
                    return;
                }

                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                s_Pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                s_Pixel.name = "CastBarPixel";
            }
        }

        float ComputeLoopDuration(Animator animator)
        {
            if (!useLoopWindow || animator == null || string.IsNullOrEmpty(castStateName))
            {
                return 0f;
            }

            float start = Mathf.Clamp01(loopStartNormalizedTime);
            float end = Mathf.Clamp01(loopEndNormalizedTime);
            if (end <= start)
            {
                return 0f;
            }

            float clipLength = ResolveClipLength(animator, castStateName);
            if (clipLength <= 0f)
            {
                return 0f;
            }

            return (end - start) * clipLength;
        }

        static float ResolveClipLength(Animator animator, string clipName)
        {
            if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(clipName))
            {
                return 0f;
            }

            var clips = animator.runtimeAnimatorController.animationClips;
            if (clips == null)
            {
                return 0f;
            }

            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name == clipName)
                {
                    return clip.length;
                }
            }

            return 0f;
        }
    }
}



