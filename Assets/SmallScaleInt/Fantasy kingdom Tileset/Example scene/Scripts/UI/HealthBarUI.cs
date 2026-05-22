using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

[DisallowMultipleComponent]
[MovedFrom(true, null, null, "HealthBarDualSliderUI")]
public class HealthBarDualSliderUI : MonoBehaviour
{
    [Header("Sliders")]
    [Tooltip("Top slider (no BG). This is the fast, foreground bar.")]
    public Slider frontSlider;
    [Tooltip("Bottom slider (has BG). This is the trailing 'chip' bar.")]
    public Slider chipSlider;

    [Header("Optional: Fill Images (for coloring/gradient)")]
    public Image frontFillImage;   // assign the Fill image inside the front slider
    public Image chipFillImage;    // assign the Fill image inside the chip slider
    public Gradient frontGradient; // color across 0..1 for front
    public Color chipColor = new Color(1f, 1f, 1f, 0.75f);

    [Header("Text (TMP)")]
    public TMP_Text valueText;     // "85 / 100"

    [Header("Timings")]
    [Tooltip("How fast the front bar moves toward the target (units per second in normalized space).")]
    public float frontLerpSpeed = 12f;
    [Tooltip("Wait before chip starts catching up (on DAMAGE).")]
    public float chipDelay = 0.15f;
    [Tooltip("How fast the chip bar moves once it starts (normalized units per second).")]
    public float chipLerpSpeed = 4f;

    [Header("Optional Overlays")]
    public Image damageFlashOverlay;  // red overlay image (Canvas Image on top of the bar)
    public Image healGlowOverlay;     // green overlay image

    [Header("Flash Timings")]
    public bool enableDamageFlash = true;
    public float damageFlashIn = 0.06f;
    public float damageFlashOut = 0.25f;
    public AnimationCurve damageEase = AnimationCurve.EaseInOut(0,0,1,1);

    public bool enableHealGlow = true;
    public float healGlowIn = 0.06f;
    public float healGlowOut = 0.30f;
    public AnimationCurve healEase = AnimationCurve.EaseInOut(0,0,1,1);

    // runtime state
    int   _max = 100;
    int   _current = 100;
    float _target01 = 1f;
    float _front01  = 1f;
    float _chip01   = 1f;
    float _chipDelayTimer = 0f;

    Coroutine _damageFlashCo, _healGlowCo;

    void Awake()
    {
        // Set up sliders as 0..1 normalized bars
        InitSlider(frontSlider);
        InitSlider(chipSlider);

        // Make sure top slider renders above bottom slider in hierarchy
        if (frontFillImage && frontGradient != null)
            frontFillImage.color = frontGradient.Evaluate(1f);
        if (chipFillImage)
            chipFillImage.color = chipColor;

        PushImmediate();
        UpdateValueText();
    }

    void InitSlider(Slider s)
    {
        if (!s) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.value = 1f;
        s.interactable = false;
        if (s.fillRect) s.fillRect.gameObject.SetActive(true);
        if (s.handleRect) s.handleRect.gameObject.SetActive(false); // no handle knob
        // For a clean look, set the FRONT sliderâ€™s Background image disabled in the prefab.
    }

    void Update()
    {
        // Move front toward target
        _front01 = Mathf.MoveTowards(_front01, _target01, frontLerpSpeed * Time.deltaTime);

        // Chip behavior
        if (!Mathf.Approximately(_chip01, _target01))
        {
            if (_chipDelayTimer > 0f)
            {
                _chipDelayTimer -= Time.deltaTime; // still waiting (on damage case)
            }
            else
            {
                // converge chip toward the greater of target/front (works for heal/damage)
                float goal = Mathf.Max(_target01, _front01);
                _chip01 = Mathf.MoveTowards(_chip01, goal, chipLerpSpeed * Time.deltaTime);
            }
        }

        PushImmediate();
    }

    void PushImmediate()
    {
        if (frontSlider) frontSlider.value = _front01;
        if (chipSlider)  chipSlider.value  = _chip01;

        if (frontFillImage && frontGradient != null)
            frontFillImage.color = frontGradient.Evaluate(_front01);
        if (chipFillImage)
            chipFillImage.color = chipColor;
    }

    void UpdateValueText()
    {
        if (valueText)
            valueText.text = $"{_current} / {_max}";
    }

    // --- Public API ---

    public void SetMax(int max, int current)
    {
        _max     = Mathf.Max(1, max);
        _current = Mathf.Clamp(current, 0, _max);

        _target01 = _current / (float)_max;
        _front01  = _target01;
        _chip01   = _target01;
        _chipDelayTimer = 0f;

        PushImmediate();
        UpdateValueText();
    }

    public void AnimateTo(int newValue)
    {
        newValue = Mathf.Clamp(newValue, 0, _max);
        bool wasDamage = newValue < _current;
        bool wasHeal   = newValue > _current;

        _current = newValue;
        _target01 = _current / (float)_max;

        if (wasDamage)
        {
            // front moves now; chip waits then drops
            _chipDelayTimer = chipDelay;

            if (enableDamageFlash && damageFlashOverlay)
            {
                if (_damageFlashCo != null) StopCoroutine(_damageFlashCo);
                _damageFlashCo = StartCoroutine(FlashOverlay(damageFlashOverlay, damageFlashIn, damageFlashOut, damageEase));
            }
        }
        else if (wasHeal)
        {
            // satisfying heal: chip pops up immediately, front eases up
            _chip01 = Mathf.Max(_chip01, _target01);
            _chipDelayTimer = 0f;

            if (enableHealGlow && healGlowOverlay)
            {
                if (_healGlowCo != null) StopCoroutine(_healGlowCo);
                _healGlowCo = StartCoroutine(FlashOverlay(healGlowOverlay, healGlowIn, healGlowOut, healEase));
            }
        }

        UpdateValueText();
    }

    IEnumerator FlashOverlay(Image img, float inT, float outT, AnimationCurve curve)
    {
        if (!img) yield break;
        if (curve == null) curve = AnimationCurve.EaseInOut(0,0,1,1);

        img.enabled = true;
        Color c = img.color; c.a = 0f; img.color = c;

        // IN
        float t = 0f;
        while (t < inT)
        {
            t += Time.unscaledDeltaTime;
            float k = curve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.0001f, inT)));
            c.a = k; img.color = c;
            yield return null;
        }

        // OUT
        t = 0f;
        while (t < outT)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - curve.Evaluate(Mathf.Clamp01(t / Mathf.Max(0.0001f, outT)));
            c.a = k; img.color = c;
            yield return null;
        }

        c.a = 0f; img.color = c; img.enabled = false;
    }
}



}




