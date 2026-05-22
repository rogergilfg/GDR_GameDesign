using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[MovedFrom(true, null, null, "DestructibleProp2D")]
public class DestructibleProp2D : MonoBehaviour
{
    [Header("Setup")]
    public Animator animator;
    public string destroyTrigger = "Destroy";
    [Min(1)] public int maxHits = 1;
    public bool isHeavy;

    [Header("Auto Destroy On Spawn")]
    [Tooltip("If true, this prop immediately starts its destroy animation when spawned.")]
    public bool destroyOnSpawn = false;
    [Tooltip("Optional delay before triggering the destroy on spawn.")]
    [Min(0f)] public float destroyOnSpawnDelay = 0f;

    [Header("Damage Handling")]
    [Tooltip("If true, this prop will ignore all incoming hits (no flash/shake/VFX until you call ForceDestroy()).")]
    public bool ignoreIncomingDamage = false;

    [Header("Generic Idle Mode")]
    public bool useGenericIdleSprite = false;
    public SpriteRenderer idleSprite;
    public bool hideAnimatedSpritesUntilDestroyed = true;

    [Header("Hit Feedback")]
    public bool  flashOnHit = true;
    public Color flashColor = new Color(1f, 0.1f, 0.1f, 1f);
    [Min(0f)] public float flashHold = 0.06f;
    [Min(0f)] public float flashFade = 0.12f;

    [Header("Staged Damage VFX (while not yet destroyed)")]
    [Tooltip("Shown after the first damaging hit and kept active until the prop is destroyed (e.g., small flame, smoke, cracks).")]
    public GameObject stagedDamageVfxPrefab;
    [Tooltip("If set, the staged VFX is parented under this transform. Default = this.transform.")]
    public Transform stagedVfxParentOverride;
    public float stagedVfxCleanupAfterDestroy = 0f; // 0 = destroy immediately on final blow

    [Header("Explosion (on destroy)")]
    public bool explodeOnDestroy = false;
    public float explodeRadius = 2.0f;
    public int explodeDamage = 20;
    public LayerMask enemyMask;
    public GameObject explosionVfx;
    public float explosionVfxCleanup = 2f;

    [Header("Prop Chain Reaction")]
    [Tooltip("When enabled, explosion can damage other destructible props in range, creating chain reactions.")]
    public bool damageOtherProps = false;
    [Tooltip("Number of hits to apply to other props when this prop explodes.")]
    [Min(1)]
    public int propDamageHits = 1;

    [Header("Tile Damage")]
    [Tooltip("When enabled, explosion damages tiles in range.")]
    public bool damageTilesOnExplosion = false;
    [Tooltip("Amount of damage to apply to tiles.")]
    [Min(1)]
    public int tileDamageAmount = 1;

    [Header("Lingering Hazard (post-destruction DoT)")]
    [Tooltip("If true, after the prop is destroyed it leaves a damaging area for a duration (e.g., burning puddle).")]
    public bool spawnLingeringHazard = false;
    public float hazardDuration = 4f;
    public float hazardRadius = 2.0f;         // can be same as explodeRadius or different
    public float hazardDps = 6f;              // damage per second (apportioned per tick)
    public float hazardTickInterval = 0.5f;   // seconds between ticks
    public LayerMask hazardMask;              // usually same as enemyMask; leave 0 to reuse enemyMask
    public GameObject hazardVfx;              // looping fire/smoke decal
    public float hazardVfxCleanup = 4.5f;

    [Header("Floating Combat Text")]
    public bool showCombatText = true;
    public Vector2 fctOffset = new Vector2(0f, 0.25f);

    [Header("Events")]
    public UnityEvent onDestroyed;
    public event Action Destroyed;

    [Header("Impact Shake (non-lethal hits only)")]
    public bool   shakeOnHit = true;
    [Tooltip("Which transform to shake; default = this.transform.")]
    public Transform shakeRoot;
    [Tooltip("Total shake time in seconds.")]
    [Min(0f)] public float shakeDuration = 0.20f;
    [Tooltip("Max local-position offset (units) at start of shake.")]
    [Min(0f)] public float shakePosAmplitude = 0.06f;
    [Tooltip("Max local Z-rotation (degrees) at start of shake.")]
    [Min(0f)] public float shakeRotAmplitude = 3f;
    [Tooltip("Shakes per second (higher = tighter jitter).")]
    [Min(0.1f)] public float shakeFrequency = 22f;
    [Tooltip("0 = constant amplitude, 1 = strong ease-out.")]
    [Range(0f,1f)] public float shakeFalloff = 0.85f;

    // ---- runtime
    int  _hits;
    bool _destroyed;
    Coroutine _flashCo;
    SpriteRenderer[] _animatedSprites;
    GameObject _stagedVfxInstance;
    Coroutine _shakeCo;
    Vector3   _shakeBasePos;
    Quaternion _shakeBaseRot;
    bool _restorePoseOnStop;

    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (!shakeRoot) shakeRoot = transform;

        if (useGenericIdleSprite)
        {
            if (!idleSprite) idleSprite = GetComponent<SpriteRenderer>();

            if (animator && hideAnimatedSpritesUntilDestroyed)
            {
                _animatedSprites = animator.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in _animatedSprites) if (sr) sr.enabled = false;
                animator.enabled = false;
            }

            if (idleSprite) idleSprite.enabled = true;
        }
    }

    void Start()
    {
        if (destroyOnSpawn)
        {
            if (destroyOnSpawnDelay <= 0f) ForceDestroy();
            else StartCoroutine(DestroyOnSpawnDelayed());
        }
    }

    IEnumerator DestroyOnSpawnDelayed()
    {
        yield return new WaitForSeconds(destroyOnSpawnDelay);
        ForceDestroy();
    }

    /// <summary>Public entry to immediately perform the full destroy sequence (same as lethal hit).</summary>
    public void ForceDestroy()
    {
        if (_destroyed) return;
        _hits = maxHits; // so subsequent logic knows we're at lethal
        DoDestroySequence();
    }

    public void ApplyHit(int amount = 1)
    {
        if (_destroyed) return;
        if (ignoreIncomingDamage) return;

        if (flashOnHit) DoFlashOnce();

        int prevHits = _hits;
        _hits += Mathf.Max(1, amount);

        bool willSurvive = _hits < maxHits;

        // Spawn staged damage VFX if it just got damaged but isn't dead yet
        if (willSurvive && prevHits < _hits)
            EnsureStagedVfx();

        // Non-lethal impact shake
        if (willSurvive && shakeOnHit && maxHits > 1)
            PlayShakeOnce();

        if (!willSurvive)
            DoDestroySequence();
    }

    void DoDestroySequence()
    {
        _destroyed = true;

        // stop any ongoing shake & restore pose cleanly
        StopShakeImmediate();

        // Generic idle â†’ reveal animated sprites and enable animator
        if (useGenericIdleSprite)
        {
            if (idleSprite) idleSprite.enabled = false;
            if (animator)
            {
                if (_animatedSprites == null && hideAnimatedSpritesUntilDestroyed)
                    _animatedSprites = animator.GetComponentsInChildren<SpriteRenderer>(true);
                if (_animatedSprites != null)
                    foreach (var sr in _animatedSprites) if (sr) sr.enabled = true;
                animator.enabled = true;
            }
        }

        // Trigger destroy animation
        if (animator) animator.SetTrigger(destroyTrigger);

        // Disable colliders right away so it no longer blocks/gets hit
        foreach (var col in GetComponentsInChildren<Collider2D>(true)) col.enabled = false;

        // Remove staged VFX
        CleanupStagedVfx();

        // Optional explosion/hazard
        if (explodeOnDestroy) DoExplosion();
        if (spawnLingeringHazard) StartCoroutine(HazardRoutine());

        if (onDestroyed != null)
            onDestroyed.Invoke();

        Destroyed?.Invoke();
    }

    public static bool TryHit(Component source, int amount = 1)
    {
        if (!source) return false;
        var prop = source.GetComponentInParent<DestructibleProp2D>();
        if (!prop) return false;
        if (prop.ignoreIncomingDamage) return false;
        prop.ApplyHit(Mathf.Max(1, amount));
        return true;
    }

    // ---------------- Staged Damage VFX ----------------
    void EnsureStagedVfx()
    {
        if (!stagedDamageVfxPrefab) return;
        if (_stagedVfxInstance) return;

        var parent = stagedVfxParentOverride ? stagedVfxParentOverride : transform;
        _stagedVfxInstance = Instantiate(stagedDamageVfxPrefab, parent);
        _stagedVfxInstance.transform.localPosition = Vector3.zero;
        _stagedVfxInstance.transform.localRotation = Quaternion.identity;
        _stagedVfxInstance.transform.localScale    = Vector3.one;
    }

    void CleanupStagedVfx()
    {
        if (!_stagedVfxInstance) return;

        if (stagedVfxCleanupAfterDestroy > 0f)
            Destroy(_stagedVfxInstance, stagedVfxCleanupAfterDestroy);
        else
            Destroy(_stagedVfxInstance);

        _stagedVfxInstance = null;
    }

    // ---------------- Explosion ----------------
    void DoExplosion()
    {
        Vector2 center = transform.position;

        if (explosionVfx)
        {
            var v = Instantiate(explosionVfx, center, Quaternion.identity);
            if (explosionVfxCleanup > 0f) Destroy(v, explosionVfxCleanup);
        }

        const int MAX = 64;
        Collider2D[] buf = new Collider2D[MAX];
        ContactFilter2D anyFilter = BuildFilter((LayerMask)~0);

        // Damage other props if enabled (check all layers for props)
        if (damageOtherProps)
        {
            int propCount = Physics2D.OverlapCircle(center, explodeRadius, anyFilter, buf);
            var hitProps = new HashSet<DestructibleProp2D>();

            for (int i = 0; i < propCount; i++)
            {
                var c = buf[i];
                if (!c) continue;

                var prop = c.GetComponentInParent<DestructibleProp2D>();
                if (prop == null || prop == this) continue; // Skip self
                if (prop.ignoreIncomingDamage) continue;
                if (hitProps.Contains(prop)) continue;

                hitProps.Add(prop);
                prop.ApplyHit(propDamageHits);
            }
        }

        // Damage tiles if enabled
        if (damageTilesOnExplosion && TileDestructionManager.I)
        {
            TileDestructionManager.HitCircle(center, explodeRadius, Mathf.Max(1, tileDamageAmount));
        }

        // Damage enemies/players
        if (enemyMask.value == 0) return;

        ContactFilter2D enemyFilter = BuildFilter(enemyMask);
        int count = Physics2D.OverlapCircle(center, explodeRadius, enemyFilter, buf);
        if (count <= 0) return;

        var already = new HashSet<Component>();

        for (int i = 0; i < count; i++)
        {
            var c = buf[i];
            if (!c) continue;

            var dmg = c.GetComponentInParent<SmallScaleInc.CharacterCreatorFantasy.EnemyAI.IDamageable>();
            if (dmg == null) continue;

            var key = (Component)dmg;
            if (key && already.Contains(key)) continue;

            Vector2 to = ((Vector2)c.bounds.center - center);
            Vector2 push = to.sqrMagnitude < 0.0001f ? Vector2.right : to.normalized;

            dmg.TakeDamage(Mathf.Max(1, explodeDamage), push);

            if (showCombatText && CombatTextManager.Instance)
            {
                Vector3 fctPos = (Vector3)c.bounds.center + (Vector3)fctOffset;
                CombatTextManager.Instance.SpawnDamage(explodeDamage, fctPos, false);
            }

            if (key) already.Add(key);
        }
    }

    // ---------------- Lingering Hazard (DoT area) ----------------
    IEnumerator HazardRoutine()
    {
        Vector2 center = transform.position;

        if (hazardVfx)
        {
            var hv = Instantiate(hazardVfx, center, Quaternion.identity);
            if (hazardVfxCleanup > 0f) Destroy(hv, hazardVfxCleanup);
        }

        float endAt = Time.time + Mathf.Max(0.01f, hazardDuration);
        float tick = Mathf.Max(0.05f, hazardTickInterval);
        int damagePerTick = Mathf.Max(1, Mathf.RoundToInt(hazardDps * tick));

        LayerMask mask = (hazardMask.value != 0) ? hazardMask : enemyMask;
        if (mask.value == 0) yield break; // nothing to hit

        const int MAX = 64;
        Collider2D[] buf = new Collider2D[MAX];
        ContactFilter2D hazardFilter = BuildFilter(mask);

        while (Time.time < endAt)
        {
            int count = Physics2D.OverlapCircle(center, hazardRadius, hazardFilter, buf);
            if (count > 0)
            {
                var already = new HashSet<Component>();
                for (int i = 0; i < count; i++)
                {
                    var c = buf[i];
                    if (!c) continue;

                    var dmg = c.GetComponentInParent<SmallScaleInc.CharacterCreatorFantasy.EnemyAI.IDamageable>();
                    if (dmg == null) continue;

                    var key = (Component)dmg;
                    if (key && already.Contains(key)) continue;

                    Vector2 to = ((Vector2)c.bounds.center - center);
                    Vector2 push = to.sqrMagnitude < 0.0001f ? Vector2.right : to.normalized;

                    dmg.TakeDamage(damagePerTick, push);

                    if (showCombatText && CombatTextManager.Instance)
                    {
                        Vector3 fctPos = (Vector3)c.bounds.center + (Vector3)fctOffset;
                        CombatTextManager.Instance.SpawnDamage(damagePerTick, fctPos, false);
                    }

                    if (key) already.Add(key);
                }
            }

            yield return new WaitForSeconds(tick);
        }

    }

    private static ContactFilter2D BuildFilter(LayerMask mask)
    {
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(mask);
        filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
        return filter;
    }

    // ---------------- Flash ----------------
    void DoFlashOnce()
    {
        if (_flashCo != null) StopCoroutine(_flashCo);
        _flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs == null || srs.Length == 0) { _flashCo = null; yield break; }

        var originals = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
        {
            if (!srs[i]) continue;
            originals[i] = srs[i].color;
            srs[i].color = flashColor;
        }

        if (flashHold > 0f) yield return new WaitForSeconds(flashHold);

        if (flashFade > 0f)
        {
            float t = 0f;
            while (t < flashFade)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / flashFade);
                for (int i = 0; i < srs.Length; i++)
                {
                    if (!srs[i]) continue;
                    srs[i].color = Color.Lerp(flashColor, originals[i], k);
                }
                yield return null;
            }
        }

        for (int i = 0; i < srs.Length; i++)
            if (srs[i]) srs[i].color = originals[i];

        _flashCo = null;
    }

    // ---------------- Shake ----------------
    void PlayShakeOnce()
    {
        if (!shakeRoot || shakeDuration <= 0f || (shakePosAmplitude <= 0f && shakeRotAmplitude <= 0f))
            return;

        if (_shakeCo != null) StopShakeImmediate();

        _shakeBasePos = shakeRoot.localPosition;
        _shakeBaseRot = shakeRoot.localRotation;
        _restorePoseOnStop = true;

        _shakeCo = StartCoroutine(ShakeRoutine());
    }

    void StopShakeImmediate()
    {
        if (_shakeCo != null)
        {
            StopCoroutine(_shakeCo);
            _shakeCo = null;
        }
        if (_restorePoseOnStop && shakeRoot)
        {
            shakeRoot.localPosition = _shakeBasePos;
            shakeRoot.localRotation = _shakeBaseRot;
        }
        _restorePoseOnStop = false;
    }

    IEnumerator ShakeRoutine()
    {
        float t = 0f;

        float phase = UnityEngine.Random.value * 1000f;

        while (t < shakeDuration && !_destroyed)
        {
            t += Time.deltaTime;

            float fall = (shakeFalloff <= 0f) ? 1f : Mathf.Lerp(1f, 1f - shakeFalloff, Mathf.Clamp01(t / shakeDuration));

            float f = shakeFrequency * (phase + t);
            float nx = Mathf.Sin(f * 6.28318f * 0.73f);
            float ny = Mathf.Sin((f + 0.37f) * 6.28318f * 0.91f);
            float nr = Mathf.Sin((f + 0.19f) * 6.28318f * 1.13f);

            Vector3 posOffset = new Vector3(nx, ny, 0f) * (shakePosAmplitude * fall);
            float   rotZ      = nr * (shakeRotAmplitude * fall);

            if (shakeRoot)
            {
                shakeRoot.localPosition = _shakeBasePos + posOffset;
                shakeRoot.localRotation = Quaternion.Euler(0f, 0f, _shakeBaseRot.eulerAngles.z + rotZ);
            }

            yield return null;
        }

        if (shakeRoot)
        {
            shakeRoot.localPosition = _shakeBasePos;
            shakeRoot.localRotation = _shakeBaseRot;
        }
        _shakeCo = null;
        _restorePoseOnStop = false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        if (useGenericIdleSprite && !idleSprite)
            idleSprite = GetComponent<SpriteRenderer>();
    }

    void OnDrawGizmosSelected()
    {
        if (explodeOnDestroy)
        {
            Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, explodeRadius);
        }
        if (spawnLingeringHazard)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, hazardRadius);
        }
    }
#endif
}



}







