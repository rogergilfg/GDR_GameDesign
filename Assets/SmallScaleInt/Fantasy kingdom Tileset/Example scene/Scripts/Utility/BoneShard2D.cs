using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;

/// Simple top-down bone shard FX for 2D games .
[RequireComponent(typeof(SpriteRenderer))]
[MovedFrom(true, null, null, "BoneShard2D")]
public class BoneShard2D : MonoBehaviour
{
    public enum MoveMode { Physics2D, Scripted }

    [Header("Movement")]
    public MoveMode moveMode = MoveMode.Physics2D;
    [Tooltip("Only used in Physics2D mode; we force gravityScale = 0.")]
    public float linearDrag = 3f;
    public float angularDrag = 0.2f;

    [Tooltip("Only used in Scripted mode.")]
    public float scriptedDamp = 2.5f; // larger = slows quicker

    [Header("Spin")]
    public bool randomSpin = true;
    public Vector2 spinRangeDegPerSec = new Vector2(-360f, 360f);

    [Header("Lifetime")]
    public float lifeTime = 1.2f;
    public float fadeOut = 0.25f; // last portion of lifetime fades

    [Header("Sorting")]
    public int sortingOrderOffset = 5;

    Rigidbody2D _rb;
    SpriteRenderer _sr;
    Vector3 _startPos;
    float _zLock;
    float _dieAt;
    Color _baseColor;
    Vector2 _velScripted;
    float _angVelScripted;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        _baseColor = _sr.color;
        _zLock = transform.position.z;

        if (moveMode == MoveMode.Physics2D)
        {
            _rb = GetComponent<Rigidbody2D>();
            if (!_rb) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.gravityScale = 0f;               // <- important for top-down
            _rb.linearDamping = linearDrag;
            _rb.angularDamping = angularDrag;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        }
    }

    void OnEnable()
    {
        _dieAt = Time.time + Mathf.Max(0.05f, lifeTime);

        // ensure above the enemy a bit (optional)
        if (_sr) _sr.sortingOrder += sortingOrderOffset;

        // keep Z locked for 2D
        _startPos = transform.position;
        _startPos.z = _zLock;
        transform.position = _startPos;
    }

    /// <summary>
    /// Initialize shard with a velocity (world units/sec).
    /// </summary>
    public void Init(Vector2 initialVelocity)
    {
        if (moveMode == MoveMode.Physics2D)
        {
            if (!_rb) _rb = GetComponent<Rigidbody2D>();
            if (_rb)
            {
                _rb.gravityScale = 0f; // safety net if prefab had non-zero gravity
                _rb.linearVelocity = initialVelocity;

                if (randomSpin)
                    _rb.angularVelocity = Random.Range(spinRangeDegPerSec.x, spinRangeDegPerSec.y);
            }
        }
        else
        {
            // scripted motion
            _velScripted = initialVelocity;
            if (randomSpin)
                _angVelScripted = Random.Range(spinRangeDegPerSec.x, spinRangeDegPerSec.y);
        }
    }

    void Update()
    {
        // Scripted motion path
        if (moveMode == MoveMode.Scripted)
        {
            float dt = Time.deltaTime;
            // integrate pos
            transform.position += (Vector3)(_velScripted * dt);
            // damp velocity exponentially for nice ease-out
            float decay = Mathf.Exp(-scriptedDamp * dt);
            _velScripted *= decay;

            // spin
            if (randomSpin && Mathf.Abs(_angVelScripted) > 0.01f)
                transform.Rotate(0f, 0f, _angVelScripted * dt);
        }

        // Keep Z flat in either mode (prevents drifting)
        var p = transform.position;
        if (!Mathf.Approximately(p.z, _zLock))
        {
            p.z = _zLock;
            transform.position = p;
        }

        // Fade out near end
        float tLeft = _dieAt - Time.time;
        if (tLeft <= fadeOut)
        {
            float k = Mathf.Clamp01(tLeft / Mathf.Max(0.0001f, fadeOut));
            var c = _baseColor; c.a *= k;
            if (_sr) _sr.color = c;
        }

        if (Time.time >= _dieAt) Destroy(gameObject);
    }
    
    public void Init(Vector2 initialVelocity, float angularVelocityDegPerSec)
    {
        // Use the single-arg initializer for common setup
        Init(initialVelocity);

        // Donâ€™t let randomSpin overwrite the explicit spin weâ€™re setting
        randomSpin = false;

        if (moveMode == MoveMode.Physics2D)
        {
            if (!_rb) _rb = GetComponent<Rigidbody2D>();
            if (_rb)
            {
                _rb.gravityScale = 0f;
                _rb.angularVelocity = angularVelocityDegPerSec; // deg/sec in 2D
            }
        }
        else // Scripted
        {
            _angVelScripted = angularVelocityDegPerSec;
        }
    }

}


}




