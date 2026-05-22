using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[MovedFrom(true, null, null, "DamageFeedback")]
public class DamageFeedback : MonoBehaviour
{
    public static DamageFeedback I;   // simple singleton
    [Header("Refs")]
    public Image flashImage;

    [Header("Flash")]
    [ColorUsage(false, true)] public Color flashColor = new Color(1f, 0f, 0f, 0.55f);
    public float inTime = 0.04f;     // how fast it pops in
    public float holdTime = 0.03f;   // optional pause at full
    public float outTime = 0.18f;    // fade out time
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0, 1,1);

    [Header("Hit-Stop (optional)")]
    public bool useHitStop = true;
    [Range(0f, 1f)] public float timeScaleDuringHit = 0.0f; // 0 = full freeze
    public float hitStopDuration = 0.06f; // unscaled seconds

    [Header("Camera Shake (optional)")]
    public bool useCameraShake = true;
    public float shakeAmp = 0.35f;
    public float shakeDur = 0.25f;

    // In DamageFeedback.cs (add fields)
    [Header("Heal Flash")]
    public bool enableHealFlash = true;
    [ColorUsage(false, true)] public Color healFlashColor = new Color(0f, 1f, 0.4f, 0.45f);
    public float healInTime = 0.06f;
    public float healHoldTime = 0.02f;
    public float healOutTime = 0.25f;
    public AnimationCurve healEase = null; // can be null -> fallback to 'ease'


    Coroutine _co;

    void Awake()
    {
        I = this;
        if (!flashImage) flashImage = GetComponentInChildren<Image>();
        if (flashImage) flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
    }

    public void PlayHeal()
    {
        if (!enableHealFlash) return;

        // Heals: no hitstop or shake
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FlashRoutine(
            color: healFlashColor,
            inT: healInTime,
            holdT: healHoldTime,
            outT: healOutTime,
            curve: healEase ?? ease  
        ));

    }

    // Keep your existing Play() for damage, but call the shared routine:
    public void Play()
    {
        if (useHitStop) StartCoroutine(HitStop());
        if (useCameraShake)
        {
            var cam = FindFirstObjectByType<SmallScaleInc.TopDownPixelCharactersPack1.SmoothCameraFollow>();
            if (cam) cam.Shake(shakeAmp, shakeDur);
        }
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FlashRoutine(
            color: flashColor,
            inT: inTime,
            holdT: holdTime,
            outT: outTime,
            curve: ease
        ));
    }

    // Replace your old FlashRoutine with this overload:
    IEnumerator FlashRoutine(Color color, float inT, float holdT, float outT, AnimationCurve curve)
    {
        if (!flashImage) yield break;
        if (curve == null) curve = AnimationCurve.EaseInOut(0,0,1,1);

        // IN
        float t = 0f;
        while (t < inT)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.0001f, inT)));
            var c = color; c.a = color.a * k;
            flashImage.color = c;
            yield return null;
        }

        // HOLD
        if (holdT > 0f) yield return new WaitForSecondsRealtime(holdT);

        // OUT
        t = 0f;
        while (t < outT)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - curve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.0001f, outT)));
            var c = color; c.a = color.a * k;
            flashImage.color = c;
            yield return null;
        }

        var clr = flashImage.color; clr.a = 0f; flashImage.color = clr;
        _co = null;
    }


    IEnumerator HitStop()
    {
        float prev = Time.timeScale;
        Time.timeScale = timeScaleDuringHit;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = prev;
    }
}



}




