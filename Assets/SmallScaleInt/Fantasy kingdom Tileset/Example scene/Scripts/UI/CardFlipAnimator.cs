using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections;
using UnityEngine;

/// <summary>
/// Simple card-flip animation that rotates and scales a tooltip to mimic turning a card.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "CardFlipAnimator")]
public sealed class CardFlipAnimator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Transform that should be animated. Defaults to this transform.")]
    private RectTransform target;

    [SerializeField]
    [Tooltip("Total duration of the flip animation in seconds.")]
    [Min(0.05f)]
    private float duration = 0.3f;

    [SerializeField]
    [Tooltip("Maximum rotation (in degrees) applied around the Y axis during the flip.")]
    private float rotateAngle = 90f;

    [SerializeField]
    [Tooltip("Optional canvas group whose alpha is animated during the flip.")]
    private CanvasGroup canvasGroup;

    [SerializeField]
    [Tooltip("Animation curve used to ease the rotation/scale over time.")]
    private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine playRoutine;
    private Vector3 initialScale = Vector3.one;
    private Quaternion initialRotation = Quaternion.identity;
    private float initialAlpha = 1f;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnValidate()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }
    }

    /// <summary>
    /// Plays the flip animation from the beginning.
    /// </summary>
    public void Play()
    {
        EnsureInitialized();

        if (!isActiveAndEnabled || target == null)
        {
            return;
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        playRoutine = StartCoroutine(PlayRoutine());
    }

    /// <summary>
    /// Immediately stops the animation and restores the initial state.
    /// </summary>
    public void ResetState()
    {
        EnsureInitialized();

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (target != null)
        {
            target.localScale = initialScale;
            target.localRotation = initialRotation;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = initialAlpha;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (target == null)
        {
            target = transform as RectTransform;
        }

        if (target != null)
        {
            initialScale = target.localScale;
            initialRotation = target.localRotation;
        }

        if (canvasGroup != null)
        {
            initialAlpha = canvasGroup.alpha;
        }

        initialized = true;
    }

    private IEnumerator PlayRoutine()
    {
        EnsureInitialized();

        float totalDuration = Mathf.Max(0.05f, duration);
        float halfDuration = totalDuration * 0.5f;

        if (target != null)
        {
            float collapsedScale = Mathf.Max(0.01f, 0.01f * initialScale.x);
            target.localScale = new Vector3(collapsedScale, initialScale.y, initialScale.z);
            target.localRotation = Quaternion.Euler(0f, Mathf.Sign(rotateAngle) * rotateAngle, 0f);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }

        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);
            float eased = easeCurve != null && easeCurve.length > 0 ? Mathf.Clamp01(easeCurve.Evaluate(t)) : t;

            if (target != null)
            {
                float scaleX = Mathf.Lerp(0.01f * initialScale.x, initialScale.x, eased);
                target.localScale = new Vector3(scaleX, initialScale.y, initialScale.z);

                float rotationY = Mathf.Lerp(Mathf.Sign(rotateAngle) * rotateAngle, 0f, eased);
                target.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            }

            if (canvasGroup != null)
            {
                float alphaProgress = halfDuration > 0f && t < 0.5f
                    ? Mathf.Clamp01(elapsed / halfDuration)
                    : 1f;
                canvasGroup.alpha = Mathf.Lerp(0f, initialAlpha > 0f ? initialAlpha : 1f, alphaProgress);
            }

            yield return null;
        }

        if (target != null)
        {
            target.localScale = initialScale;
            target.localRotation = initialRotation;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = initialAlpha > 0f ? initialAlpha : 1f;
        }

        playRoutine = null;
    }
}


}




