using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SmallScale.FantasyKingdomTileset.Balance;

namespace SmallScale.FantasyKingdomTileset
{
[RequireComponent(typeof(Collider2D))]
public class EnemyHealth2D : MonoBehaviour, EnemyAI.IDamageable, AbilitySystem.IOriginalSpriteColorProvider
{
    [Header("Refs")]
    public Animator animator; // assign; falls back to first in children

    public enum OutOfCombatHealMode
    {
        None,
        Gradual,
        InstantFull
    }

    [Header("Health")]
    public int maxHealth = 40;
    [SerializeField] int currentHealth = 40;
    public float invulnAfterHit = 0.1f;
    int _baseMaxHealth;
    bool _capturedBaseHealth;

    [Header("Health Regeneration")]
    [Tooltip("How this enemy recovers health outside of combat.")]
    public OutOfCombatHealMode outOfCombatHealMode = OutOfCombatHealMode.None;
    [Tooltip("Seconds after leaving combat before gradual regeneration begins.")]
    [Min(0f)] public float regenDelay = 3f;
    [Tooltip("Health restored per second while regenerating (Gradual mode only).")]
    [Min(0f)] public float regenPerSecond = 5f;

    // Safe read accessors (used by abilities)
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => _dead;   // NEW

    // (Optional) fire when death begins
    public event System.Action OnDied;

    // Event for damage taken (used by ability system procs)
    public event System.Action<int> OnDamageTaken;

    // Event for before death (used by resurrection abilities)
    // Return true from subscribers to cancel death
#pragma warning disable 0067
    public event System.Func<bool> OnBeforeDeath;
#pragma warning restore 0067


    [Header("Ally Alert (Aggro Shout)")]
    [Tooltip("When this enemy takes damage, alert nearby allies to engage the player.")]
    public bool alertAlliesOnHit = true;
    [Tooltip("How far the alert shout reaches (world units).")]
    public float allyAlertRadius = 8f;
    [Tooltip("Prevents spamming alerts when taking DoT, etc.")]
    public float alertRepeatCooldown = 1.0f;
    [Tooltip("If true, allies may chain their own alerts later when they take damage.")]
    public bool chainAlert = true;
    [Tooltip("LayerMask for allies (put your 'Enemy' layer here).")]
    public LayerMask allyMask;
    [Tooltip("Optional VFX spawned when shouting.")]
    public GameObject alertVfxPrefab;
    public float alertVfxCleanup = 2f;

    [Header("Hit Flash")]
    public Color  hitColor = new Color(1f, 0.2f, 0.2f, 1f);
    public float  flashDuration = 0.08f;
    public bool   includeChildrenSprites = true;
    public bool   skipFlashOnLethal = true;

    [Header("Animator Params")]
    public string dieTrigger = "Die";          // optional if you rely on trigger
    public string isDeadBool = "IsDead";       // strongly recommended
    public string takeDamageTrigger = "TakeDamage";

    [Header("Known Die State Path (optional but fast)")]
    [Tooltip("Exact Animator path, e.g. 'Base Layer.TakeDamage/Die/Die_E'. Leave empty to not force a state.")]
    public string dieStatePath = "Base Layer.TakeDamage/Die/Die_E";

    [Header("Hold Last Frame (optional)")]
    public bool  holdLastFrameOnDeath = true;
    [Range(0.8f, 1f)] public float holdNormalizedTime = 0.99f;
    public float deathHoldTimeout = 5f;

    [Header("Knockback")]
    public bool enableKnockback = true;
    public float knockbackImpulse = 2.2f;
    public bool  pauseAIDuringKnockback = true;
    public float knockbackAIPause = 0.08f;

    [Header("Hit Bones (VFX)")]
    public bool spawnBonesOnHit = true;
    public BoneShard2D boneShardPrefab;
    public Vector2Int bonesOnHit = new Vector2Int(2, 4);
    public int bonesOnDeathBonus = 4;
    public float boneConeDegrees = 70f;
    public Vector2 boneSpawnOffset = new Vector2(0f, 0.35f);
    [Range(0.1f, 3f)] public float boneSpeedScale = 1f;

    [Header("Health Bar")]
    public bool   useHealthBar = true;
    public Vector2 barSize = new Vector2(1.2f, 0.16f);
    public Vector2 barOffset = new Vector2(0f, 1.2f);
    public Color  barBackColor = new Color(0f, 0f, 0f, 0.45f);
    public Color  barFillColor = new Color(0.2f, 1f, 0.2f, 0.95f);
    public bool   hideWhenFull = true;
    public float  barShowSeconds = 1.5f;
    public bool   keepBarUpright = true;

    [Header("Combat Indicator")]
    [FormerlySerializedAs("enableCombatOutline")]
    [Tooltip("Draw a ground indicator underneath this enemy while it is in combat.")]
    public bool enableCombatIndicator = true;
    [Tooltip("Sprite used for the ground indicator.")]
    public Sprite combatIndicatorSprite;
    [FormerlySerializedAs("combatOutlineColor")]
    [Tooltip("Tint applied to the ground indicator sprite.")]
    public Color combatIndicatorColor = new Color(1f, 0.3f, 0.3f, 0.75f);
    [Tooltip("Local position offset applied to the indicator.")]
    public Vector3 combatIndicatorOffset = new Vector3(0f, 0.02f, 0f);
    [Tooltip("Local scale applied to the indicator. Adjust Y to squash the circle into an ellipse.")]
    public Vector3 combatIndicatorScale = new Vector3(1.2f, 0.55f, 1f);
    [Tooltip("Sorting order offset relative to the enemy's primary sprite. Negative numbers place it underneath.")]
    public int combatIndicatorSortingOffset = -5;
    [Tooltip("Enable slow rotation for the combat indicator.")]
    public bool rotateCombatIndicator = false;
    [Tooltip("Rotation speed in degrees per second when rotation is enabled.")]
    public float combatIndicatorRotationSpeed = 45f;
    [Tooltip("Enable a pulsing scale effect when the enemy is at low health.")]
    public bool pulseIndicatorOnLowHealth = false;
    [Range(0f, 1f)]
    [Tooltip("Fraction of max health where the indicator switches to low health visuals.")]
    public float lowHealthThreshold = 0.2f;
    [Tooltip("Speed of the low-health pulse animation.")]
    public float lowHealthPulseSpeed = 4f;
    [Range(0f, 0.5f)]
    [Tooltip("Amplitude of the low-health pulse. 0.2 = Â±20% scale variation.")]
    public float lowHealthPulseAmplitude = 0.15f;
    [Tooltip("Swap the indicator color when the enemy is at low health.")]
    public bool changeIndicatorColorOnLowHealth = false;
    public Color lowHealthIndicatorColor = new Color(1f, 0.15f, 0.1f, 0.9f);
    [Tooltip("Blend the indicator color toward the low-health color based on remaining health.")]
    public bool blendIndicatorColorByHealth = false;

    [Header("Shutdown")]
    public float collidersDisableDelay = 0.15f;
    public float rigidbodySleepDelay   = 0.15f;
    public float destroyAfterSeconds   = -1f; // <0 = keep

    [Header("Death Replacement")]
    [Tooltip("Optional prefab spawned when this enemy dies. If assigned the regular death animation is skipped.")]
    public GameObject deathReplacementPrefab;
    [Tooltip("Hide the enemy's renderers/animator when using the death replacement prefab.")]
    public bool hideEnemyWhenUsingDeathReplacement = true;

    [Header("Hit VFX")]
    public GameObject hitVfxPrefab;
    public Vector2    hitVfxOffset = new Vector2(0f, 0.25f);
    public bool       parentVfxToEnemy = false;
    public float      vfxAutoDestroy = 2f;
    public bool       faceAgainstHitDir = true;

    [Header("Minimap Icon")]
    [Tooltip("Optional icon GameObject (or parent) used by your minimap camera. It will be hidden on death and shown on revive.")]
    public GameObject minimapIconRoot;
    [Tooltip("If enabled, we try to auto-detect a child named 'MinimapIcon' or with 'Minimap/Radar' in the name if no explicit icon is assigned.")]
    public bool autoDetectMinimapIcon = true;

    EnemyAI _ai;
    Rigidbody2D  _rb;
    readonly List<SpriteRenderer> _sprites = new List<SpriteRenderer>();
    readonly List<Color> _orig = new List<Color>();
    readonly Dictionary<SpriteRenderer, Color> _spriteOriginalColorMap = new Dictionary<SpriteRenderer, Color>();
    SpriteRenderer _combatIndicatorRenderer;
    Transform _combatIndicatorRoot;
    Transform _combatIndicatorRotationNode;
    bool _combatIndicatorVisible = false;
    float _combatIndicatorAngle = 0f;
    float _combatIndicatorPulseTime = 0f;
    public bool LastDamageWasAbsorbed { get; private set; }
    bool _combatIndicatorBaseVisible = false;

    struct IndicatorHighlightState
    {
        public bool Active;
        public Color Color;
        public float ScaleMultiplier;
    }

    IndicatorHighlightState _hoverHighlightState;
    IndicatorHighlightState _previewHighlightState;
    float _canBeHitAt = 0f;
    bool  _dead = false;
    int   _diePathHash = 0;
    bool  _visualsHiddenForDeathReplacement = false;
    bool  _externalKnockbackSuppressed = false;
    bool  _externalHitInterruptSuppressed = false;
    float _outOfCombatSince = -1f;
    float _regenAccumulator = 0f;
    float _lastDamageAt = -1f;

    // ------- Global registry for revive targeting -------
    static readonly System.Collections.Generic.List<EnemyHealth2D> _registry = new System.Collections.Generic.List<EnemyHealth2D>();
    public static System.Collections.Generic.IReadOnlyList<EnemyHealth2D> All => _registry;

    // When did we die? (used to prefer oldest/newest corpses if desired)
    public float DiedAt { get; private set; } = -1f;


    // --- alert state ---
    float _lastAlertAt = -999f;

    // --- health bar internals ---
    static Sprite _pixel; // 1x1 white pixel sprite made at runtime
    Transform _barRoot;
    SpriteRenderer _barBack, _barFill;
    float _barHideAt = -1f;
    BossHealthBarUI _bossHealthBar;
    bool UsingBossHealthBar => _bossHealthBar != null;
    bool ShouldShowBossHealthBar => _ai && _ai.InCombat && !_dead;

    void CaptureBaseHealth()
    {
        if (_capturedBaseHealth)
            return;
        _baseMaxHealth = Mathf.Max(1, maxHealth);
        _capturedBaseHealth = true;
    }

    void ApplyBalanceScaling()
    {
        CaptureBaseHealth();
        float healthMultiplier = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.EnemyHealthMultiplier : 1f;
        healthMultiplier = Mathf.Max(0f, healthMultiplier);
        float ratio = maxHealth > 0 ? currentHealth / (float)maxHealth : 1f;
        int newMax = Mathf.Max(1, Mathf.RoundToInt(_baseMaxHealth * healthMultiplier));
        maxHealth = newMax;
        int newCurrent = Mathf.RoundToInt(newMax * ratio);
        if (_dead)
            newCurrent = 0;
        currentHealth = Mathf.Clamp(newCurrent, _dead ? 0 : 1, newMax);
    }

    public void RefreshBalanceFromManager()
    {
        bool wasDead = _dead;
        ApplyBalanceScaling();
        if (!wasDead)
            currentHealth = Mathf.Max(1, Mathf.Min(currentHealth, maxHealth));
        UpdateHealthBar(true);
    }

    /// <summary>
    /// Multiplies this enemy's max health and optionally fills to the new max.
    /// Used by spawners or scripted events for per-instance scaling.
    /// </summary>
    public void ApplyExternalHealthMultiplier(float multiplier, bool fillToMax)
    {
        multiplier = Mathf.Max(0f, multiplier);
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f))
        {
            return;
        }

        float ratio = maxHealth > 0 ? currentHealth / (float)maxHealth : 1f;
        int baseValue = _capturedBaseHealth ? _baseMaxHealth : maxHealth;
        int newMax = Mathf.Max(1, Mathf.RoundToInt(baseValue * multiplier));

        maxHealth = newMax;
        int desiredCurrent = fillToMax ? newMax : Mathf.RoundToInt(newMax * ratio);
        if (_dead)
        {
            desiredCurrent = 0;
        }
        else
        {
            desiredCurrent = Mathf.Clamp(desiredCurrent, 1, newMax);
        }

        currentHealth = desiredCurrent;
        UpdateHealthBar(true);
    }


    /// <summary>Heals this enemy for 'amount' HP. Plays optional green number and refreshes bar.</summary>
    public void Heal(int amount)
    {
        if (_dead) return;
        int before = currentHealth;
        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(1, amount), 1, maxHealth);

        // Optional: floating green text if you want (requires a SpawnHeal method).
        // If you only have SpawnDamage, you can skip or extend your manager with SpawnHeal.
        // CombatTextManager.Instance?.SpawnHeal(currentHealth - before, transform.position + (Vector3)hitVfxOffset);

        UpdateHealthBar(true);
        BumpBarVisibility();
    }


    void Awake()
    {
        if (!animator) animator = GetComponentInChildren<Animator>(true);
        _ai = GetComponent<EnemyAI>();
        CaptureBaseHealth();
        ApplyBalanceScaling();
        _rb = GetComponent<Rigidbody2D>();
        currentHealth = Mathf.Clamp(currentHealth <= 0 ? maxHealth : currentHealth, 1, maxHealth);

        if (includeChildrenSprites)
            _sprites.AddRange(GetComponentsInChildren<SpriteRenderer>(true));
        else
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr) _sprites.Add(sr);
        }
        foreach (var s in _sprites)
        {
            Color baseColor = s ? s.color : Color.white;
            _orig.Add(baseColor);
            if (s && !_spriteOriginalColorMap.ContainsKey(s))
            {
                _spriteOriginalColorMap.Add(s, baseColor);
            }
        }

        if (!string.IsNullOrEmpty(dieStatePath))
            _diePathHash = Animator.StringToHash(dieStatePath);

        if (_ai && _ai.IsBoss)
            SetupBossHealthBar();
        else if (useHealthBar)
            CreateHealthBar();

        UpdateHealthBar(true);

        CreateCombatIndicator();
        SetCombatIndicatorVisible(false);

        RefreshCombatState();

        // Minimap icon setup and initial visibility
        TryAutoDetectMinimapIcon();
        UpdateMinimapIconVisibility();
    }

    void RefreshCombatState()
    {
        if (_ai && _ai.InCombat)
        {
            _outOfCombatSince = -1f;
            _lastDamageAt = Time.time;
        }
        else
        {
            _outOfCombatSince = Time.time;
            _lastDamageAt = -1f;
        }

        _regenAccumulator = 0f;
    }

    void OnEnable()
    {
        if (!_ai) _ai = GetComponent<EnemyAI>();
        if (_ai) _ai.CombatStateChanged += HandleCombatStateChanged;
        if (_ai && _ai.IsBoss && !UsingBossHealthBar)
            SetupBossHealthBar();
        if (UsingBossHealthBar)
        {
            SetBossBarVisible(ShouldShowBossHealthBar);
            _bossHealthBar.UpdateValues(maxHealth, currentHealth);
        }

        LastAttacker = null;

        RefreshCombatState();
        SetCombatIndicatorVisible(_ai && _ai.InCombat && !_dead);

        if (!_registry.Contains(this)) _registry.Add(this);
    }

    void OnDisable()
    {
        if (_ai) _ai.CombatStateChanged -= HandleCombatStateChanged;
        SetCombatIndicatorVisible(false);

        if (UsingBossHealthBar)
            SetBossBarVisible(false);

        _registry.Remove(this);
    }

    void OnDestroy()
    {
        if (Application.isPlaying && _bossHealthBar)
        {
            Destroy(_bossHealthBar.gameObject);
            _bossHealthBar = null;
        }
    }

    void CreateCombatIndicator()
    {
        if (_combatIndicatorRoot)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(_combatIndicatorRoot.gameObject);
            else
#endif
                Destroy(_combatIndicatorRoot.gameObject);
        }

        _combatIndicatorRenderer = null;
        _combatIndicatorRoot = null;
        _combatIndicatorRotationNode = null;
        _combatIndicatorAngle = 0f;
        _combatIndicatorPulseTime = 0f;

        if (!enableCombatIndicator)
            return;

        if (!combatIndicatorSprite)
            return;

        var indicatorObject = new GameObject("CombatIndicator");
        indicatorObject.transform.SetParent(transform, false);
        indicatorObject.layer = gameObject.layer;
        _combatIndicatorRoot = indicatorObject.transform;

        var rotationObject = new GameObject("CombatIndicatorRotate");
        rotationObject.transform.SetParent(_combatIndicatorRoot, false);
        rotationObject.layer = gameObject.layer;
        _combatIndicatorRotationNode = rotationObject.transform;

        var spriteObject = new GameObject("CombatIndicatorSprite");
        spriteObject.transform.SetParent(_combatIndicatorRotationNode, false);
        spriteObject.layer = gameObject.layer;

        var renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = combatIndicatorSprite;
        renderer.enabled = false;
        _combatIndicatorRenderer = renderer;

        ApplyCombatIndicatorAppearance();
    }

    void ApplyCombatIndicatorAppearance()
    {
        bool useLowHealth = ShouldUseLowHealthIndicatorEffects();
        IndicatorHighlightState activeHighlight;
        bool highlightActive = TryGetActiveHighlight(out activeHighlight);

        if (_combatIndicatorRoot)
        {
            Vector3 baseScale = combatIndicatorScale;
            if (highlightActive)
            {
                baseScale *= Mathf.Max(0.05f, activeHighlight.ScaleMultiplier);
            }
            else if (useLowHealth && pulseIndicatorOnLowHealth)
            {
                float amplitude = Mathf.Clamp(lowHealthPulseAmplitude, 0f, 0.5f);
                float pulse = Mathf.Sin(_combatIndicatorPulseTime);
                float multiplier = 1f + pulse * amplitude;
                multiplier = Mathf.Max(0.05f, multiplier);
                baseScale = combatIndicatorScale * multiplier;
            }

            _combatIndicatorRoot.localPosition = combatIndicatorOffset;
            _combatIndicatorRoot.localScale = baseScale;
        }

        if (_combatIndicatorRenderer)
        {
            if (combatIndicatorSprite)
                _combatIndicatorRenderer.sprite = combatIndicatorSprite;

            Color targetColor = combatIndicatorColor;
            if (highlightActive)
            {
                targetColor = activeHighlight.Color;
            }
            else if (blendIndicatorColorByHealth && maxHealth > 0)
            {
                float healthFraction = Mathf.Clamp01((float)currentHealth / maxHealth);
                targetColor = Color.Lerp(lowHealthIndicatorColor, combatIndicatorColor, healthFraction);
            }
            else if (useLowHealth && changeIndicatorColorOnLowHealth)
            {
                targetColor = lowHealthIndicatorColor;
            }

            _combatIndicatorRenderer.color = targetColor;
            _combatIndicatorRenderer.spriteSortPoint = SpriteSortPoint.Center;
            _combatIndicatorRenderer.transform.localPosition = Vector3.zero;
            _combatIndicatorRenderer.transform.localRotation = Quaternion.identity;
            _combatIndicatorRenderer.transform.localScale = Vector3.one;

            SpriteRenderer reference = null;
            int minOrder = int.MaxValue;
            for (int i = 0; i < _sprites.Count; i++)
            {
                var spriteRenderer = _sprites[i];
                if (!spriteRenderer) continue;
                if (reference == null) reference = spriteRenderer;
                if (spriteRenderer.sortingOrder < minOrder)
                    minOrder = spriteRenderer.sortingOrder;
            }

            if (reference)
            {
                _combatIndicatorRenderer.sortingLayerID = reference.sortingLayerID;
                int baseOrder = minOrder == int.MaxValue ? reference.sortingOrder : minOrder;
                _combatIndicatorRenderer.sortingOrder = baseOrder + combatIndicatorSortingOffset;
                _combatIndicatorRenderer.maskInteraction = reference.maskInteraction;
            }
            else
            {
                _combatIndicatorRenderer.sortingLayerID = 0;
                _combatIndicatorRenderer.sortingOrder = combatIndicatorSortingOffset;
                _combatIndicatorRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }

        if (_combatIndicatorRotationNode)
        {
            _combatIndicatorRotationNode.localRotation = Quaternion.Euler(0f, 0f, _combatIndicatorAngle);
        }
    }

    void SetCombatIndicatorVisible(bool visible)
    {
        _combatIndicatorBaseVisible = visible;
        RefreshCombatIndicatorVisibility();
    }

    void RefreshCombatIndicatorVisibility()
    {
        if (_combatIndicatorRenderer == null && enableCombatIndicator && combatIndicatorSprite)
        {
            CreateCombatIndicator();
        }

        if (_combatIndicatorRenderer == null)
        {
            _combatIndicatorVisible = false;
            return;
        }

        IndicatorHighlightState highlight;
        bool highlightActive = TryGetActiveHighlight(out highlight);
        bool shouldShow = enableCombatIndicator && !_dead && (_combatIndicatorBaseVisible || highlightActive);
        if (_combatIndicatorVisible == shouldShow)
        {
            if (shouldShow)
            {
                ApplyCombatIndicatorAppearance();
            }
            return;
        }

        _combatIndicatorVisible = shouldShow;
        if (shouldShow)
        {
            if (!rotateCombatIndicator)
                _combatIndicatorAngle = 0f;
            _combatIndicatorPulseTime = 0f;
            ApplyCombatIndicatorAppearance();
        }
        else
        {
            _combatIndicatorAngle = 0f;
            _combatIndicatorPulseTime = 0f;
            if (_combatIndicatorRotationNode)
                _combatIndicatorRotationNode.localRotation = Quaternion.identity;
        }
        _combatIndicatorRenderer.enabled = shouldShow;
    }

    bool TryGetActiveHighlight(out IndicatorHighlightState highlight)
    {
        if (_previewHighlightState.Active)
        {
            highlight = _previewHighlightState;
            return true;
        }

        if (_hoverHighlightState.Active)
        {
            highlight = _hoverHighlightState;
            return true;
        }

        highlight = default;
        return false;
    }

    void UpdateHighlightState(ref IndicatorHighlightState state, bool enabled, Color color, float scaleMultiplier)
    {
        float sanitizedScale = Mathf.Max(0.05f, scaleMultiplier);
        bool changed = state.Active != enabled;
        if (!changed && enabled)
        {
            changed = state.Color != color || !Mathf.Approximately(state.ScaleMultiplier, sanitizedScale);
        }

        if (!changed)
        {
            return;
        }

        if (enabled)
        {
            state.Active = true;
            state.Color = color;
            state.ScaleMultiplier = sanitizedScale;
        }
        else
        {
            state.Active = false;
            state.ScaleMultiplier = 1f;
        }

        RefreshCombatIndicatorVisibility();
    }

    public void SetHoverHighlight(bool enabled, Color color, float scaleMultiplier = 1f)
    {
        UpdateHighlightState(ref _hoverHighlightState, enabled, color, scaleMultiplier);
    }

    public void SetPreviewHighlight(bool enabled, Color color, float scaleMultiplier = 1f)
    {
        UpdateHighlightState(ref _previewHighlightState, enabled, color, scaleMultiplier);
    }

    bool ShouldUseLowHealthIndicatorEffects()
    {
        if (!enableCombatIndicator) return false;
        return IsBelowLowHealthThreshold();
    }

    bool IsBelowLowHealthThreshold()
    {
        if (maxHealth <= 0) return false;
        float threshold = Mathf.Clamp01(lowHealthThreshold);
        if (threshold <= 0f) return false;
        if (currentHealth <= 0) return true;
        float ratio = (float)currentHealth / maxHealth;
        return ratio <= threshold;
    }

    void HandleCombatStateChanged(bool engaged)
    {
        if (engaged)
        {
            _outOfCombatSince = -1f;
            _regenAccumulator = 0f;
            _lastDamageAt = Time.time;
        }
        else
        {
            _outOfCombatSince = Time.time;
            if (outOfCombatHealMode == OutOfCombatHealMode.InstantFull)
                TryInstantFullHeal();
        }

        SetCombatIndicatorVisible(engaged);
        if (UsingBossHealthBar)
            SetBossBarVisible(engaged && !_dead);
    }

    void Update()
    {
        HandleOutOfCombatHealing();
    }

    void HandleOutOfCombatHealing()
    {
        if (_dead) return;

        bool hasAI = _ai;
        bool inCombat = hasAI && _ai.InCombat;
        if (inCombat)
        {
            _outOfCombatSince = -1f;
            _regenAccumulator = 0f;
            return;
        }

        if (!hasAI && _outOfCombatSince < 0f)
        {
            if (_lastDamageAt < 0f)
                _outOfCombatSince = Time.time;
            else if (Time.time >= _lastDamageAt + regenDelay)
                _outOfCombatSince = _lastDamageAt;
        }
        else if (hasAI && !inCombat && _outOfCombatSince < 0f)
        {
            _outOfCombatSince = Time.time;
        }

        if (outOfCombatHealMode == OutOfCombatHealMode.InstantFull)
        {
            if (_outOfCombatSince >= 0f)
                TryInstantFullHeal();
            return;
        }

        if (outOfCombatHealMode != OutOfCombatHealMode.Gradual) return;
        if (regenPerSecond <= 0f) return;
        if (_outOfCombatSince < 0f) return;
        if (Time.time < _outOfCombatSince + regenDelay) return;
        if (currentHealth >= maxHealth) return;

        _regenAccumulator += regenPerSecond * Time.deltaTime;
        int healAmount = Mathf.FloorToInt(_regenAccumulator);
        if (healAmount <= 0) return;

        _regenAccumulator -= healAmount;
        Heal(healAmount);

        if (currentHealth >= maxHealth)
            _regenAccumulator = 0f;
    }

    void LateUpdate()
    {
        if (!UsingBossHealthBar && useHealthBar && _barRoot != null)
        {
            if (_barHideAt > 0f && Time.time > _barHideAt)
            {
                SetBarVisible(false);
                _barHideAt = -1f;
            }

            _barRoot.position = (Vector2)transform.position + barOffset;
            if (keepBarUpright) _barRoot.rotation = Quaternion.identity;
        }

        if (_combatIndicatorVisible)
        {
            bool lowHealthActive = ShouldUseLowHealthIndicatorEffects();
            if (pulseIndicatorOnLowHealth && lowHealthActive)
            {
                float speed = Mathf.Max(0f, lowHealthPulseSpeed);
                _combatIndicatorPulseTime += Time.deltaTime * speed;
            }
            else
            {
                _combatIndicatorPulseTime = 0f;
            }

            if (rotateCombatIndicator && _combatIndicatorRotationNode)
            {
                _combatIndicatorAngle = Mathf.Repeat(_combatIndicatorAngle + combatIndicatorRotationSpeed * Time.deltaTime, 360f);
                _combatIndicatorRotationNode.localRotation = Quaternion.Euler(0f, 0f, _combatIndicatorAngle);
            }
            else if (_combatIndicatorRotationNode)
            {
                _combatIndicatorAngle = 0f;
                _combatIndicatorRotationNode.localRotation = Quaternion.identity;
            }

            ApplyCombatIndicatorAppearance();
        }
    }

    public void TakeDamage(int amount, Vector2 hitDir)
    {
        if (_dead) return;
        if (Time.time < _canBeHitAt) return;

        LastDamageWasAbsorbed = false;

        // ---- NEW: shout/alert here so it always runs on real damage ----
        TryAlertAllies();

        _lastDamageAt = Time.time;
        _outOfCombatSince = -1f;
        _regenAccumulator = 0f;

        int finalDamage = Mathf.Max(1, amount);

        var shieldHandler = AbilityShieldHandler.GetExisting(transform);
        if (shieldHandler != null)
        {
            finalDamage = shieldHandler.AbsorbDamage(finalDamage);
            if (finalDamage <= 0)
            {
                LastDamageWasAbsorbed = true;
                _canBeHitAt = Time.time + invulnAfterHit;
                return;
            }
        }

        currentHealth -= finalDamage;
        _canBeHitAt = Time.time + invulnAfterHit;

        // Invoke damage taken event for ability system procs
        OnDamageTaken?.Invoke(finalDamage);

        UpdateHealthBar();
        BumpBarVisibility();

        if (currentHealth <= 0)
        {
            SpawnHitVFX(hitDir, lethal: true);
            if (!skipFlashOnLethal) { StopCoroutine(nameof(FlashRoutine)); StartCoroutine(FlashRoutine()); }
            StartCoroutine(DieSequence());
            return;
        }

        // Only call OnHit if hit interrupt is not suppressed (used by DoT effects)
        if (_ai && !_externalHitInterruptSuppressed) _ai.OnHit(hitDir);

        if (animator && HasParam(takeDamageTrigger, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(takeDamageTrigger);

        StopCoroutine(nameof(FlashRoutine));
        StartCoroutine(FlashRoutine());

        if (enableKnockback) ApplyKnockback(hitDir);

        SpawnHitVFX(hitDir, lethal: false);
        SpawnBoneBurst(hitDir, Random.Range(bonesOnHit.x, bonesOnHit.y + 1));
    }

    public Transform LastAttacker { get; private set; }

    public void RegisterLastAttacker(Transform attacker)
    {
        if (!attacker) return;
        LastAttacker = attacker;
    }

    void TryInstantFullHeal()
    {
        if (_dead) return;
        if (currentHealth >= maxHealth) return;

        currentHealth = maxHealth;
        UpdateHealthBar(true);
        BumpBarVisibility();
        _regenAccumulator = 0f;
    }

    public bool Revive(int hp, bool fullReactivate = true)
    {
        // If already alive or object is being destroyed, bail
        if (!_dead) return false;

        // IMPORTANT: if this enemy uses timed self-destroy on death,
        // you must ensure destroyAfterSeconds < 0 for units that can be revived.
        // A scheduled Destroy() cannot be canceled at runtime.

        // Bring back to life
        _dead = false;
        DiedAt = -1f;         // <-- add this

        SetCombatIndicatorVisible(_ai && _ai.InCombat);

        currentHealth = Mathf.Clamp(hp, 1, maxHealth);

        // Animator recover
        if (animator)
        {
            animator.enabled = true;
            // Resume speed (death hold routine may have frozen at 0)
            animator.speed = 1f;

            if (HasParam(isDeadBool, AnimatorControllerParameterType.Bool))
                animator.SetBool(isDeadBool, false);

            // Optional: kick an idle/stand trigger here if you have one
            animator.SetTrigger("Special1");
        }

        // Re-enable AI
        if (_ai) _ai.SetExternalPause(false);

        if (_visualsHiddenForDeathReplacement)
        {
            SetEnemyVisualsVisible(true);
            _visualsHiddenForDeathReplacement = false;
        }

        // Re-enable physics+colliders
        foreach (var c in GetComponentsInChildren<Collider2D>(true))
            c.enabled = true;

        if (_rb)
        {
    #if UNITY_2022_2_OR_NEWER
            _rb.linearVelocity = Vector2.zero;
    #else
            _rb.velocity = Vector2.zero;
    #endif
            _rb.angularVelocity = 0f;
            _rb.simulated = true;
        }

        // Health bar
        UpdateHealthBar(true);
        if (!UsingBossHealthBar)
            SetBarVisible(true);
        BumpBarVisibility();

        _regenAccumulator = 0f;
        _lastDamageAt = -1f;
        RefreshCombatState();

        // Show minimap icon again
        UpdateMinimapIconVisibility();

        return true;
    }

    // ---------- NEW: Ally alert logic (entirely inside health) ----------
    void TryAlertAllies()
    {
        if (!alertAlliesOnHit) return;
        if (Time.time < _lastAlertAt + alertRepeatCooldown) return;
        _lastAlertAt = Time.time;

        // Engage this enemy immediately (let AI handle mode switch)
        if (_ai) _ai.OnAllyAlerted(transform.position, chainAlert);

        // Optional VFX
        if (alertVfxPrefab)
        {
            var v = Instantiate(alertVfxPrefab, transform.position, Quaternion.identity);
            if (alertVfxCleanup > 0f) Destroy(v, alertVfxCleanup);
        }

        // Find allies via physics (layer mask = your 'Enemy' layer)
        const int MAX = 64;
        Collider2D[] buf = new Collider2D[MAX];
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(allyMask);
        filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
        int count = Physics2D.OverlapCircle(transform.position, allyAlertRadius, filter, buf);

        for (int i = 0; i < count; i++)
        {
            var c = buf[i];
            if (!c) continue;
            if (c.transform == transform || c.transform.IsChildOf(transform)) continue;

            var otherAI = c.GetComponentInParent<EnemyAI>();
            if (!otherAI || otherAI == _ai) continue;

            // Wake the ally; let its AI decide how to move/face
            otherAI.OnAllyAlerted(transform.position, chainAlert);
        }
    }

    void ApplyKnockback(Vector2 hitDir)
    {
        if (!_rb || _externalKnockbackSuppressed) return;
        Vector2 dir = hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : Vector2.right;
        _rb.AddForce(dir * knockbackImpulse, ForceMode2D.Impulse);

        if (pauseAIDuringKnockback && _ai && !_ai.IsExternallyPaused)
            _ai.PauseForSeconds(knockbackAIPause);
    }

    public void SetKnockbackSuppressed(bool suppressed)
    {
        _externalKnockbackSuppressed = suppressed;
    }

    public void SetHitInterruptSuppressed(bool suppressed)
    {
        _externalHitInterruptSuppressed = suppressed;
    }

    IEnumerator DieSequence()
    {
        if (_dead) yield break;
        _dead = true;
        DiedAt = Time.time;

        SetCombatIndicatorVisible(false);

        OnDied?.Invoke();

        var abilityRunner = GetComponent<AbilityRunner>();
        if (abilityRunner && abilityRunner.isActiveAndEnabled)
        {
            abilityRunner.CancelAllAbilities();
            abilityRunner.enabled = false;
        }

        if (!animator) animator = GetComponentInChildren<Animator>(true);

        if (!deathReplacementPrefab && animator)
        {
            if (HasParam(takeDamageTrigger, AnimatorControllerParameterType.Trigger))
                animator.ResetTrigger(takeDamageTrigger);

            if (HasParam(isDeadBool, AnimatorControllerParameterType.Bool))
                animator.SetBool(isDeadBool, true);

            if (HasParam(dieTrigger, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(dieTrigger);
                animator.SetTrigger(dieTrigger);
            }

            if (_diePathHash != 0 && animator.HasState(0, _diePathHash))
                animator.CrossFadeInFixedTime(_diePathHash, 0f, 0);

            if (holdLastFrameOnDeath) StartCoroutine(HoldDeathPoseRoutine());
        }
        else if (deathReplacementPrefab)
        {
            Instantiate(deathReplacementPrefab, transform.position, transform.rotation);

            if (hideEnemyWhenUsingDeathReplacement)
            {
                SetEnemyVisualsVisible(false);
                _visualsHiddenForDeathReplacement = true;
            }
        }

        yield return null;

        if (_ai) _ai.SetExternalPause(true);

        if (_rb)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            StartCoroutine(SleepRigidbodySoon());
        }
        StartCoroutine(DisableCollidersSoon());

        if (UsingBossHealthBar)
            SetBossBarVisible(false);
        else
            SetBarVisible(false);

        if (destroyAfterSeconds > 0f)
            Destroy(gameObject, destroyAfterSeconds);

        // Hide minimap icon when dead
        UpdateMinimapIconVisibility();
    }

    IEnumerator HoldDeathPoseRoutine()
    {
        if (!animator) yield break;

        float t0 = Time.time;
        while (Time.time - t0 < deathHoldTimeout)
        {
            var st = animator.GetCurrentAnimatorStateInfo(0);
            var nx = animator.GetNextAnimatorStateInfo(0);

            if (IsDeath(st) || IsDeath(nx))
            {
                while (!IsDeath(st))
                {
                    yield return null;
                    st = animator.GetCurrentAnimatorStateInfo(0);
                }
                while (st.normalizedTime < holdNormalizedTime && IsDeath(st))
                {
                    yield return null;
                    st = animator.GetCurrentAnimatorStateInfo(0);
                }
                animator.Play(st.fullPathHash, 0, 0.999f);
                animator.Update(0f);
                animator.speed = 0f;
                yield break;
            }
            yield return null;
        }
    }

    bool IsDeath(AnimatorStateInfo st)
    {
        if (st.fullPathHash == 0) return false;
        if (_diePathHash != 0) return st.fullPathHash == _diePathHash;
        return false;
    }

    void SetEnemyVisualsVisible(bool visible)
    {
        for (int i = 0; i < _sprites.Count; i++)
        {
            var sr = _sprites[i];
            if (sr) sr.enabled = visible;
        }

        if (animator)
            animator.enabled = visible;
    }

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < _sprites.Count; i++) _sprites[i].color = hitColor;
        yield return new WaitForSeconds(flashDuration);
        for (int i = 0; i < _sprites.Count; i++) _sprites[i].color = _orig[i];
    }

    IEnumerator DisableCollidersSoon()
    {
        yield return new WaitForSeconds(collidersDisableDelay);
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
    }

    IEnumerator SleepRigidbodySoon()
    {
        yield return new WaitForSeconds(rigidbodySleepDelay);
        if (_rb) _rb.simulated = false;
    }

    bool HasParam(string name, AnimatorControllerParameterType type)
    {
        if (!animator) return false;
        foreach (var p in animator.parameters)
            if (p.name == name && p.type == type) return true;
        return false;
    }

    // ---------------- Bones VFX ----------------

    void SpawnBoneBurst(Vector2 hitDir, int count)
    {
        if (!spawnBonesOnHit || !boneShardPrefab || count <= 0) return;

        Vector2 pos = (Vector2)transform.position + boneSpawnOffset;
        BoneBurst.Spawn(
            boneShardPrefab,
            pos,
            hitDir.sqrMagnitude > 0.0001f ? hitDir.normalized : Vector2.right,
            count,
            speedMin: 2.8f * boneSpeedScale,
            speedMax: 5.0f * boneSpeedScale,
            coneDegrees: boneConeDegrees,
            randomUpBias: 0.35f,
            parent: null
        );
    }

    // ---------------- Health bar ----------------

    void SetupBossHealthBar()
    {
        if (UsingBossHealthBar || _ai == null || !_ai.IsBoss)
            return;

        _ai.EnsureBossHealthBarPanelReference();

        var prefab = _ai.BossHealthBarPrefab;
        if (!prefab)
        {
            Debug.LogWarning($"Enemy '{name}' is marked as a boss but has no BossHealthBarPrefab assigned.", this);
            return;
        }

        var panel = _ai.BossHealthBarPanel;
        if (!panel)
        {
            Debug.LogWarning($"Enemy '{name}' is marked as a boss but has no BossHealthBarPanel assigned.", this);
            return;
        }

        _bossHealthBar = Instantiate(prefab, panel);
        _bossHealthBar.Configure(_ai.BossDisplayName, maxHealth, currentHealth);
        SetBossBarVisible(ShouldShowBossHealthBar);
    }

    void CreateHealthBar()
    {
        if (_pixel == null)
        {
            var tex = new Texture2D(1,1, TextureFormat.RGBA32, false);
            tex.SetPixel(0,0, Color.white);
            tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0,0,1,1), new Vector2(0.5f,0.5f), 100f);
            _pixel.name = "1x1_White_Pixel";
        }

        _barRoot = new GameObject("HP_Bar").transform;
        _barRoot.SetParent(transform, false);
        _barRoot.position = (Vector2)transform.position + barOffset;

        var backGO = new GameObject("Back");
        backGO.transform.SetParent(_barRoot, false);
        _barBack = backGO.AddComponent<SpriteRenderer>();
        _barBack.sprite = _pixel;
        _barBack.color = barBackColor;
        _barBack.sortingOrder = 9998;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(_barRoot, false);
        _barFill = fillGO.AddComponent<SpriteRenderer>();
        _barFill.sprite = _pixel;
        _barFill.color = barFillColor;
        _barFill.sortingOrder = 9999;

        LayoutBar(1f);
        SetBarVisible(!hideWhenFull);
    }

    void LayoutBar(float pct)
    {
        pct = Mathf.Clamp01(pct);

        if (_barBack)
        {
            _barBack.transform.localPosition = Vector3.zero;
            _barBack.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
        }
        if (_barFill)
        {
            _barFill.transform.localPosition = Vector3.zero;
            _barFill.transform.localScale = new Vector3(barSize.x * pct, barSize.y, 1f);
        }
    }

    void UpdateHealthBar(bool force = false)
    {
        if (UsingBossHealthBar)
        {
            _bossHealthBar.UpdateValues(maxHealth, currentHealth);
            SetBossBarVisible(ShouldShowBossHealthBar);
            return;
        }

        if (!useHealthBar || _barRoot == null) return;
        float pct = (maxHealth > 0) ? (currentHealth / (float)maxHealth) : 0f;
        LayoutBar(pct);

        if (hideWhenFull && currentHealth >= maxHealth && !_dead)
            SetBarVisible(false);
        else if (force)
            SetBarVisible(true);
    }

    void BumpBarVisibility()
    {
        if (UsingBossHealthBar)
        {
            SetBossBarVisible(ShouldShowBossHealthBar);
            return;
        }

        if (!useHealthBar || _barRoot == null) return;
        SetBarVisible(true);
        _barHideAt = Time.time + Mathf.Max(0.05f, barShowSeconds);
    }

    void SetBarVisible(bool v)
    {
        if (_barBack) _barBack.enabled = v;
        if (_barFill) _barFill.enabled = v;
    }

    void SetBossBarVisible(bool visible)
    {
        if (!UsingBossHealthBar) return;
        _bossHealthBar.SetVisible(visible);
    }

    void SpawnHitVFX(Vector2 hitDir, bool lethal)
    {
        if (!hitVfxPrefab) return;

        Vector3 pos = (Vector2)transform.position + hitVfxOffset;
        Quaternion rot = Quaternion.identity;
        if (faceAgainstHitDir && hitDir.sqrMagnitude > 0.0001f)
        {
            Vector2 fwd = -hitDir.normalized;
            float ang = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
            rot = Quaternion.Euler(0, 0, ang);
        }

        var go = Instantiate(hitVfxPrefab, pos, rot, parentVfxToEnemy ? transform : null);

        var psList = go.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in psList) ps.Play(true);

        if (vfxAutoDestroy > 0f) Destroy(go, vfxAutoDestroy);
    }

    // ---------- Minimap helpers ----------
    void TryAutoDetectMinimapIcon()
    {
        if (minimapIconRoot || !autoDetectMinimapIcon) return;
        try
        {
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var tf = all[i];
                if (!tf) continue;
                string n = tf.name;
                if (string.IsNullOrEmpty(n)) continue;
                string s = n.ToLowerInvariant();
                if (s.Contains("minimap") || s.Contains("mini map") || s.Contains("mapicon") || s.Contains("map_icon") || s.Contains("radar"))
                {
                    minimapIconRoot = tf.gameObject;
                    break;
                }
            }
        }
        catch { /* ignore */ }
    }

    void UpdateMinimapIconVisibility()
    {
        if (!minimapIconRoot) return;
        bool show = !_dead;
        if (minimapIconRoot.activeSelf != show)
            minimapIconRoot.SetActive(show);
    }

    public bool TryGetOriginalSpriteColor(SpriteRenderer renderer, out Color color)
    {
        if (!renderer)
        {
            color = default;
            return false;
        }

        return _spriteOriginalColorMap.TryGetValue(renderer, out color);
    }

    // (Optional) one-shot rebuild if needed by tools/debug
    public static void RebuildRegistry()
    {
        _registry.Clear();
        // includeInactive = true so prefabs/disabled templates can be excluded; we only care about scene instances.
        var all = FindObjectsByType<EnemyHealth2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            // Only track scene instances (active or inactive). Prefabs in Project shouldnâ€™t be included.
            if (all[i].gameObject.scene.IsValid())
                _registry.Add(all[i]);
        }
    }

}
}

// (BoneBurst stays the same)
namespace SmallScale.FantasyKingdomTileset
{
    public static class BoneBurst
{
    public static void Spawn(
        BoneShard2D shardPrefab,
        Vector2 position,
        Vector2 baseDir,
        int count,
        float speedMin = 0.1f,
        float speedMax = 0.5f,
        float coneDegrees = 60f,
        float randomUpBias = 0.25f,
        Transform parent = null)
    {
        if (!shardPrefab || count <= 0) return;

        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector2.right;

        for (int i = 0; i < count; i++)
        {
            float ang = Random.Range(-coneDegrees * 0.5f, coneDegrees * 0.5f);
            Vector2 dir = Quaternion.Euler(0, 0, ang) * baseDir;
            dir += Vector2.up * Random.Range(0f, randomUpBias);
            dir.Normalize();

            float speed = Random.Range(speedMin, speedMax);
            Vector2 vel = dir * speed;

            var shard = Object.Instantiate(shardPrefab, position, Quaternion.identity, parent);
            shard.Init(vel, Random.Range(-120f, 120f));
        }
    }
}
}






