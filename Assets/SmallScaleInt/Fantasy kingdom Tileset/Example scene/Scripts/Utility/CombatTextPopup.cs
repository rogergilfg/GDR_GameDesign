using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using TMPro;

[MovedFrom(true, null, null, "CombatTextPopup")]
public class CombatTextPopup : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text text;

    // Runtime
    Camera _cam;
    RectTransform _rt;
    float _t, _duration;
    float _baseScale = 1f;
    Vector3 _worldStart;
    Vector3 _vel;              // world-space velocity
    float _gravity;            // world-space gravity
    AnimationCurve _alphaCurve;
    AnimationCurve _scaleCurve;
    AnimationCurve _heightCurve;
    Vector2 _screenOffset;

    Color _baseColor;
    bool _active;

    public bool IsActive => _active;
    public Vector3 CurrentWorldPosition
    {
        get
        {
            if (!_rt) _rt = GetComponent<RectTransform>();
            return _rt ? _rt.position : transform.position;
        }
    }

    public void Init(Camera cam)
    {
        _cam = cam ? cam : Camera.main;
        if (!_rt) _rt = GetComponent<RectTransform>();
        if (!text) text = GetComponent<TMP_Text>();
        // some sane TMP defaults
        if (text)
        {
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
        }
        gameObject.SetActive(false);
        _active = false;
    }

    public void Play(
        Vector3 worldPos, string content, Color color,
        float baseScale, float duration,
        Vector2 initialVelocity, float gravity,
        AnimationCurve alphaCurve, AnimationCurve scaleCurve, AnimationCurve heightCurve,
        Vector2? screenOffset = null)
    {
        if (!_rt) _rt = GetComponent<RectTransform>();
        if (!text) text = GetComponent<TMP_Text>();

        // assign ALL runtime state
        text.text     = content;
        _baseColor    = color;
        text.color    = _baseColor;

        _worldStart   = worldPos;
        _vel          = new Vector3(initialVelocity.x, initialVelocity.y, 0f);
        _gravity      = gravity;

        _alphaCurve   = alphaCurve;
        _scaleCurve   = scaleCurve;
        _heightCurve  = heightCurve;
        _screenOffset = screenOffset ?? Vector2.zero;

        _baseScale    = Mathf.Max(0.001f, baseScale);
        _duration     = Mathf.Max(0.1f, duration);
        _t            = 0f;

        // initial placement/scale
        if (_rt) {
            _rt.position   = _worldStart;
            _rt.localScale = Vector3.one * _baseScale; // << fixed base scale
        }

        _active = true;
        gameObject.SetActive(true);
        UpdateNow(0f); // first frame update
    }

    void Update()
    {
        if (!_active) return;

        float dt = Time.deltaTime;
        _t += dt;

        UpdateNow(dt);

        if (_t >= _duration)
        {
            _active = false;
            gameObject.SetActive(false);
            CombatTextManager.Instance?.ReturnToPool(this);
        }
    }

    void UpdateNow(float dt)
    {
        // physics-ish motion
        _vel.y += -_gravity * dt;
        _worldStart += _vel * dt;

        float k = Mathf.Clamp01(_t / Mathf.Max(0.0001f, _duration));
        float jump = _heightCurve != null ? _heightCurve.Evaluate(k) : 0f;
        Vector3 wp = _worldStart + new Vector3(0f, jump, 0f);

        if (_rt) _rt.position = wp + new Vector3(_screenOffset.x, _screenOffset.y, 0f);

        // scale from base (no compounding)
        float s = _scaleCurve != null ? _scaleCurve.Evaluate(k) : 1f;
        if (_rt) _rt.localScale = Vector3.one * (_baseScale * s);

        // alpha
        float aK = _alphaCurve != null ? _alphaCurve.Evaluate(k) : 1f;
        var c = _baseColor; c.a *= aK;
        if (text) text.color = c;
    }
}



}




