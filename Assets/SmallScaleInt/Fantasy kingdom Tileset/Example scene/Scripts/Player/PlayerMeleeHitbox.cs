using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
using SmallScale.FantasyKingdomTileset.Balance;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScaleInc.TopDownPixelCharactersPack1; // <â€” keep

[RequireComponent(typeof(GenericTopDownController))]
[MovedFrom(true, null, null, "PlayerMeleeHitbox")]
public class PlayerMeleeHitbox : MonoBehaviour
{
    [Header("Targeting")]
    public LayerMask enemyMask;

    [Header("Hit Shape")]
    public Vector2 hitOffset = new Vector2(0.7f, 0f);
    public float   hitRadius = 0.6f;

    [Header("Default Timing")]
    public float defaultHitDelay   = 0.16f;
    public float defaultHitWindow  = 0.10f;
    public float defaultRecovery   = 0.15f;

    [Header("Per-Trigger Overrides (optional)")]
    public List<TriggerTiming> overrides = new List<TriggerTiming>()
    {
        new TriggerTiming(){ triggerName="Attack1",   delay=0.16f, window=0.10f, recovery=0.15f },
        new TriggerTiming(){ triggerName="Attack2",   delay=0.18f, window=0.12f, recovery=0.18f },
        new TriggerTiming(){ triggerName="Attack3",   delay=0.20f, window=0.12f, recovery=0.20f },
        new TriggerTiming(){ triggerName="AttackRun", delay=0.14f, window=0.10f, recovery=0.15f },
        new TriggerTiming(){ triggerName="AttackRun2",delay=0.14f, window=0.10f, recovery=0.15f },
    };

    [Header("Damage")]
    [Tooltip("Base damage before variance and crits.")]
    public int damage = 12;

    [Tooltip("Â±% variance applied to base damage. 0.10 = Â±10%.")]
    [Range(0f, 0.5f)] public float damageVariance = 0.10f;

    [Header("Critical Hits")]
    [Tooltip("Chance to crit (0â€“1).")]
    [Range(0f, 1f)] public float critChance = 0.20f;

    [Tooltip("Damage multiplier on crit (e.g., 1.5 = +50%).")]
    [Range(1f, 3f)] public float critMultiplier = 1.5f;

    [Tooltip("Add extra shake if a crit happens on this swing.")]
    public bool boostShakeOnCrit = true;

    [Tooltip("Extra amplitude added to shake on crit (added then clamped 0..1).")]
    [Range(0f, 1f)] public float critShakeBoost = 0.35f;

    [Header("Options")]
    [Tooltip("Only allow each enemy to be hit once per swing window.")]
    public bool oneHitPerTargetPerSwing = true;

    [Header("Camera Shake on Hit")]
    public bool shakeOnHit = true;
    [Range(0f,1f)] public float shakeAmplitude = 0.55f;
    [Min(0f)]      public float shakeDuration  = 0.22f;
    [Tooltip("If empty, we'll use Camera.main.")]
    public SmoothCameraFollow cameraFollow;   // optional reference

    [Header("Destructible prefabs")]
    public LayerMask destructibleMask;    // set to your "Props" layer in the prefab
    public bool damageDestructibles = true;

    [Header("Tile Destruction (test)")]
    public bool enableTileHits = true;                 // toggle tile damage on/off
    public TileDestructionManager tileDamage;          // drop your TileDestructionManager here
    public int tileDamagePerSwingSample = 1;           // damage applied per sample while the hit window runs
    [Min(0f)] public float tileExtraRadius = 0f;       // enlarge the hit circle, optional



    GenericTopDownController _controller;
    Camera _cam;

    bool _attackActive = false;
    Coroutine _swingCo;
    readonly HashSet<Component> _hitThisSwing = new HashSet<Component>();
    bool _shookThisSwing = false;
    PlayerStats _playerStats;
    int _baseDamage;
    int _baseTileDamagePerSample;
    bool _hasCapturedBaseDamage = false;

    [System.Serializable]
    public class TriggerTiming
    {
        public string triggerName;
        public float delay   = 0.16f;
        public float window  = 0.10f;
        public float recovery = 0.15f;
    }

    void Awake()
    {
        _controller = GetComponent<GenericTopDownController>();
        _cam = Camera.main;
        CaptureBaseDamage();

        if (!cameraFollow && Camera.main)
            cameraFollow = Camera.main.GetComponent<SmoothCameraFollow>();
    }

    void OnEnable()
    {
        _controller.OnAnimationTriggerSent += OnAnimTrigger;
        EnsureStatsSubscription();
    }

    void OnDisable()
    {
        _controller.OnAnimationTriggerSent -= OnAnimTrigger;
        if (_playerStats != null)
        {
            _playerStats.StatsChanged -= HandleStatsChanged;
        }
    }

    void Start()
    {
        EnsureStatsSubscription();
    }

    void OnAnimTrigger(string trig)
    {
        if (_attackActive) return;

        var t = GetTiming(trig);
        _swingCo = StartCoroutine(SwingRoutine(t.delay, t.window, t.recovery));
    }

    TriggerTiming GetTiming(string trig)
    {
        foreach (var t in overrides)
            if (!string.IsNullOrEmpty(t.triggerName) && t.triggerName == trig)
                return t;
        return new TriggerTiming(){ delay = defaultHitDelay, window = defaultHitWindow, recovery = defaultRecovery };
    }

    IEnumerator SwingRoutine(float delay, float window, float recovery)
    {
        _attackActive = true;
        _controller.attackLockedExternally = true;

        _hitThisSwing.Clear();
        _shookThisSwing = false;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        float end = Time.time + Mathf.Max(0f, window);
        while (Time.time < end)
        {
            DoHitCheck();
            yield return null;
        }

        if (recovery > 0f) yield return new WaitForSeconds(recovery);

        _controller.attackLockedExternally = false;
        _attackActive = false;
        _swingCo = null;
        _hitThisSwing.Clear();
    }

void DoHitCheck()
{
    Vector3 mouseWorld = _cam ? _cam.ScreenToWorldPoint(Input.mousePosition)
                              : (Vector3)transform.position + Vector3.right;
    mouseWorld.z = transform.position.z;
    Vector2 forward = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
    if (forward.sqrMagnitude < 0.0001f) forward = Vector2.right;

    Vector2 center = (Vector2)transform.position + Rotate2D(hitOffset, forward);

    bool hitSomeoneThisSample = false;
    bool critHappenedThisSample = false;

    // Query both layers at once
    int combinedMask = enemyMask | (damageDestructibles ? destructibleMask : 0);
    var hits = Physics2D.OverlapCircleAll(center, hitRadius, combinedMask);

    foreach (var h in hits)
    {
        if (!h) continue;

        // 1) Enemies
        var dmgTarget = h.GetComponentInParent<EnemyAI.IDamageable>();
        if (dmgTarget != null)
        {
            var key = dmgTarget as Component;
            if (oneHitPerTargetPerSwing && key != null && _hitThisSwing.Contains(key))
                continue;

            bool isCrit;
            int dealt = RollDamage(out isCrit);
            dmgTarget.TakeDamage(dealt, forward);

            EnemyHealth2D enemyHealth = key ? key.GetComponentInParent<EnemyHealth2D>() : null;
            bool absorbed = enemyHealth != null && enemyHealth.LastDamageWasAbsorbed;

            if (!absorbed)
            {
                EnemyAI.NotifyDamageDealt(dmgTarget, transform, dealt);

                // Notify PlayerStats for proc triggers and ability system
                if (PlayerStats.Instance != null)
                {
                    PlayerStats.Instance.NotifyDamageDealt(dealt, isCrit);
                }

                CombatTextManager.Instance?.SpawnDamage(dealt, (Vector3)h.bounds.center, isCrit);

                // Apply lifesteal from melee attack
                ApplyMeleeLifesteal(dealt, true, (Vector3)h.bounds.center);
            }
            else
            {
                AbilityEffectUtility.SpawnAbsorbedText((Vector3)h.bounds.center);
            }

            hitSomeoneThisSample = true;
            if (!absorbed)
            {
                critHappenedThisSample |= isCrit;
            }

            if (oneHitPerTargetPerSwing && key != null) _hitThisSwing.Add(key);
            continue;
        }

        // 2) Destructible props
        if (damageDestructibles && ((destructibleMask.value & (1 << h.gameObject.layer)) != 0))
        {
            var prop = h.GetComponentInParent<DestructibleProp2D>();
            if (prop != null)
            {
                var key = (Component)prop;
                if (oneHitPerTargetPerSwing && _hitThisSwing.Contains(key))
                    continue;

                bool _;
                int dealt = RollDamage(out _);
                prop.ApplyHit(1); // or prop.ApplyHit(dealt);

                // Apply lifesteal from hitting destructible (if allowed)
                ApplyMeleeLifesteal(dealt, false, (Vector3)h.bounds.center);

                hitSomeoneThisSample = true;
                if (oneHitPerTargetPerSwing) _hitThisSwing.Add(key);
            }
        }
    }

    // 3) Tiles (Walls Tilemap) â€” no colliders needed
    if (enableTileHits)
    {
        float r = hitRadius + Mathf.Max(0f, tileExtraRadius);
        TileDestructionManager.HitCircle(center, r, tileDamagePerSwingSample);
    }


    // Camera shake
    if (shakeOnHit && hitSomeoneThisSample && !_shookThisSwing && cameraFollow)
    {
        float amp = shakeAmplitude + (boostShakeOnCrit && critHappenedThisSample ? critShakeBoost : 0f);
        cameraFollow.Shake(Mathf.Clamp01(amp), shakeDuration);
        _shookThisSwing = true;
    }
}



    // --- Damage rolling (variance + crit) ---
    int RollDamage(out bool crit)
    {
        // Â±variance
        float variance = 1f + Random.Range(-damageVariance, damageVariance);
        int rolled = Mathf.RoundToInt(damage * variance);
        rolled = Mathf.Max(1, rolled);

        // crit roll
        crit = Random.value < critChance;
        if (crit)
        {
            rolled = Mathf.RoundToInt(rolled * critMultiplier);
            rolled = Mathf.Max(1, rolled);
        }
        return rolled;
    }

    static Vector2 Rotate2D(Vector2 v, Vector2 forward)
    {
        Vector2 x = forward.normalized;
        Vector2 y = new Vector2(-x.y, x.x);
        return v.x * x + v.y * y;
    }

    void EnsureStatsSubscription()
    {
        if (!_hasCapturedBaseDamage)
            CaptureBaseDamage();

        if (_playerStats == null)
        {
            _playerStats = PlayerStats.Instance;
            if (_playerStats == null)
                _playerStats = FindFirstObjectByType<PlayerStats>();
        }

        if (_playerStats != null)
        {
            _playerStats.StatsChanged -= HandleStatsChanged;
            _playerStats.StatsChanged += HandleStatsChanged;
            ApplyDamageFromStats(_playerStats.CurrentStats);
        }
        else
        {
            damage = Mathf.Max(1, _baseDamage);
            float meleeMult = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.PlayerMeleeDamageMultiplier : 1f;
            float tileMult = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.PlayerTileDamageMultiplier : 1f;

            damage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Max(0f, meleeMult)));
            tileDamagePerSwingSample = Mathf.Max(0, Mathf.RoundToInt(_baseTileDamagePerSample * Mathf.Max(0f, tileMult)));
            if (tileMult > 0f && tileDamagePerSwingSample <= 0 && _baseTileDamagePerSample > 0)
            {
                tileDamagePerSwingSample = 1;
            }
        }
    }

    void HandleStatsChanged(PlayerStats.StatSnapshot snapshot)
    {
        ApplyDamageFromStats(snapshot);
    }

    void ApplyDamageFromStats(PlayerStats.StatSnapshot snapshot)
    {
        if (!_hasCapturedBaseDamage)
            CaptureBaseDamage();

        long total = (long)_baseDamage + snapshot.Strength + snapshot.WeaponDamage;
        if (total < 1)
            total = 1;
        if (total > int.MaxValue)
            total = int.MaxValue;

        float meleeMult = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.PlayerMeleeDamageMultiplier : 1f;
        total = Mathf.RoundToInt(Mathf.Max(0f, meleeMult) * Mathf.Max(1, (int)total));
        if (total < 1)
            total = 1;
        if (total > int.MaxValue)
            total = int.MaxValue;

        damage = (int)total;

        long tileTotal = (long)_baseTileDamagePerSample + snapshot.TileDamageBonus;
        if (tileTotal < 0)
            tileTotal = 0;
        if (tileTotal > int.MaxValue)
            tileTotal = int.MaxValue;

        float tileMult = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.PlayerTileDamageMultiplier : 1f;
        tileTotal = Mathf.RoundToInt(Mathf.Max(0f, tileMult) * Mathf.Max(0, (int)tileTotal));
        if (tileMult > 0f && tileTotal <= 0 && (_baseTileDamagePerSample > 0 || snapshot.TileDamageBonus > 0))
        {
            tileTotal = 1;
        }

        tileDamagePerSwingSample = (int)Mathf.Clamp(tileTotal, 0, int.MaxValue);
    }

    void CaptureBaseDamage()
    {
        _baseDamage = Mathf.Max(1, damage);
        _baseTileDamagePerSample = Mathf.Max(0, tileDamagePerSwingSample);
        _hasCapturedBaseDamage = true;
    }

    void ApplyMeleeLifesteal(int damageDealt, bool isLivingTarget, Vector3 hitPosition)
    {
        if (damageDealt <= 0) return;

        var playerStats = PlayerStats.Instance;
        if (!playerStats) return;

        float lifestealPercent = playerStats.CurrentLifestealPercent;
        int lifestealFlat = playerStats.CurrentLifestealFlat;
        int lifestealCap = playerStats.CurrentLifestealCap;
        bool lifestealLivingOnly = playerStats.CurrentLifestealLivingOnly;

        // Check if we should apply lifesteal
        if (lifestealPercent <= 0f && lifestealFlat <= 0) return;
        if (lifestealLivingOnly && !isLivingTarget) return;

        // Calculate lifesteal amount
        float percentHeal = damageDealt * lifestealPercent;
        int totalHeal = Mathf.RoundToInt(percentHeal) + lifestealFlat;

        // Apply cap if set
        if (lifestealCap > 0)
        {
            totalHeal = Mathf.Min(totalHeal, lifestealCap);
        }

        totalHeal = Mathf.Max(1, totalHeal);

        // Heal the player
        var playerHealth = PlayerHealth.Instance;
        if (playerHealth != null && !PlayerHealth.IsPlayerDead)
        {
            int before = playerHealth.currentHealth;
            int after = Mathf.Clamp(before + totalHeal, 0, playerHealth.maxHealth);
            int actualHealed = after - before;

            if (actualHealed > 0)
            {
                playerHealth.currentHealth = after;

                // Show lifesteal combat text
                if (CombatTextManager.Instance)
                {
                    CombatTextManager.Instance.SpawnLifesteal(actualHealed, hitPosition);
                }
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _baseDamage = Mathf.Max(1, damage);
            _baseTileDamagePerSample = Mathf.Max(0, tileDamagePerSwingSample);
            _hasCapturedBaseDamage = true;
        }
    }
#endif

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = Color.red;
        Vector3 mouseWorld = Camera.main ? Camera.main.ScreenToWorldPoint(Input.mousePosition) : (Vector3)transform.position + Vector3.right;
        mouseWorld.z = transform.position.z;
        Vector2 forward = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
        Vector2 center = (Vector2)transform.position + Rotate2D(hitOffset, forward);
        Gizmos.DrawWireSphere(center, hitRadius);
    }
#endif
}



}





