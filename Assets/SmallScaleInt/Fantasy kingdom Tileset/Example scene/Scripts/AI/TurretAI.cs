using UnityEngine;
using SmallScaleInc.CharacterCreatorFantasy;

namespace SmallScale.FantasyKingdomTileset
{
    /// <summary>
    /// Simple stationary turret that detects hostile targets within a radius and fires Projectile2D prefabs toward them.
    /// Supports directional sprites for 8 compass directions and optional muzzle flash prefabs.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CompanionHealth))]
    public class TurretAI : MonoBehaviour
    {
        private static readonly float[] DirectionThresholds =
        {
            22.5f, 67.5f, 112.5f, 157.5f, 202.5f, 247.5f, 292.5f, 337.5f
        };

        [Header("Engagement")]
        [Tooltip("Maximum distance at which the turret will detect and track enemies.")]
        [Min(0.5f)]
        [SerializeField] private float engageRadius = 10f;

        [Tooltip("Optional buffer beyond engage radius before forgetting a target. 0 = same as engage radius.")]
        [Min(0f)]
        [SerializeField] private float disengagePadding = 2f;

        [Tooltip("Physics layers considered valid targets (must have EnemyAI.IDamageable).")]
        [SerializeField] private LayerMask targetLayers = 1 << 9; // Default to Enemy layer if project uses it.

        [Tooltip("When enabled, the turret only shoots targets with clear line of sight.")]
        [SerializeField] private bool requireLineOfSight = true;

        [Tooltip("Layers considered blocking for line of sight tests.")]
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Projectile Settings")]
        [Tooltip("Projectile prefab (must contain Projectile2D).")]
        [SerializeField] private Projectile2D projectilePrefab;

        [Tooltip("Transform used for spawning projectiles. Defaults to this transform when null.")]
        [SerializeField] private Transform muzzleTransform;

        [Tooltip("Seconds between shots while a valid target is present.")]
        [Min(0.05f)]
        [SerializeField] private float fireInterval = 1.25f;

        [Tooltip("Damage dealt by each projectile.")]
        [Min(1)]
        [SerializeField] private int projectileDamage = 15;

        public int CurrentProjectileDamage => Mathf.Max(1, projectileDamage);

        [Tooltip("Projectile speed (units per second).")]
        [Min(0.1f)]
        [SerializeField] private float projectileSpeed = 12f;

        [Tooltip("Lifetime of spawned projectiles (seconds). Determines max range together with speed).")]
        [Min(0.1f)]
        [SerializeField] private float projectileLifetime = 4f;

        [Header("Sprites")]
        [Tooltip("Sprite renderer that displays the turret's body.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Sprite when aiming East (0Â°).")]
        [SerializeField] private Sprite eastSprite;

        [Tooltip("Sprite when aiming North-East (45Â°).")]
        [SerializeField] private Sprite northEastSprite;

        [Tooltip("Sprite when aiming North (90Â°).")]
        [SerializeField] private Sprite northSprite;

        [Tooltip("Sprite when aiming North-West (135Â°).")]
        [SerializeField] private Sprite northWestSprite;

        [Tooltip("Sprite when aiming West (180Â°).")]
        [SerializeField] private Sprite westSprite;

        [Tooltip("Sprite when aiming South-West (225Â°).")]
        [SerializeField] private Sprite southWestSprite;

        [Tooltip("Sprite when aiming South (270Â°).")]
        [SerializeField] private Sprite southSprite;

        [Tooltip("Sprite when aiming South-East (315Â°).")]
        [SerializeField] private Sprite southEastSprite;

        [Header("Muzzle Flash")]
        [Tooltip("Optional VFX prefab spawned whenever the turret fires.")]
        [SerializeField] private GameObject muzzleFlashPrefab;

        [Tooltip("Lifetime for spawned muzzle flash VFX. 0 = let prefab handle cleanup.")]
        [Min(0f)]
        [SerializeField] private float muzzleFlashLifetime = 1f;

        [Header("Recoil")]
        [Tooltip("Transform that visually recoils when firing. Defaults to this object's transform.")]
        [SerializeField] private Transform recoilTransform;

        [Tooltip("Distance pushed backwards on each shot.")]
        [Min(0f)]
        [SerializeField] private float recoilDistance = 0.15f;

        [Tooltip("Seconds taken to reach peak recoil.")]
        [Min(0f)]
        [SerializeField] private float recoilOutDuration = 0.05f;

        [Tooltip("Seconds taken to settle back to the rest position.")]
        [Min(0f)]
        [SerializeField] private float recoilReturnDuration = 0.1f;

        [Header("Lifetime")]
        [Tooltip("When enabled, the turret will despawn after the specified lifetime.")]
        [SerializeField] private bool useLifetime = false;

        [Tooltip("Lifetime in seconds before despawning.")]
        [Min(0.1f)]
        [SerializeField] private float lifetimeSeconds = 15f;

        [SerializeField] private Vector2 lifetimeBarSize = new Vector2(1.1f, 0.12f);
        [SerializeField] private Vector2 lifetimeBarOffset = new Vector2(0f, 1.2f);
        [SerializeField] private Color lifetimeBarBackColor = new Color(0f, 0f, 0f, 0.4f);
        [SerializeField] private Color lifetimeBarFillColor = new Color(1f, 0.8f, 0.2f, 0.95f);

        [Header("Idle Animation")]
        [Tooltip("Play a subtle idle scan animation when no targets are in range.")]
        [SerializeField] private bool useIdleScanAnimation = true;

        [Tooltip("Seconds each idle frame is held.")]
        [Min(0.05f)]
        [SerializeField] private float idleFrameDuration = 0.6f;

        [Tooltip("How many completed idle sweeps before the turret shifts to a new trio of directions.")]
        [Min(1)]
        [SerializeField] private int idleSweepsBeforeShift = 2;

        [Header("Health")]
        [Tooltip("If enabled, the turret will display a Companion-style health bar when damaged.")]
        [SerializeField] private bool useHealthBar = true;

        [SerializeField] private Vector2 barSize = new Vector2(1.1f, 0.16f);
        [SerializeField] private Vector2 barOffset = new Vector2(0f, 0.9f);
        [SerializeField] private Color barBackColor = new Color(0f, 0f, 0f, 0.45f);
        [SerializeField] private Color barFillColor = new Color(1f, 0.3f, 0.3f, 0.95f);
        [SerializeField] private bool keepBarUpright = true;

        [Header("Scaling")]
        [Tooltip("When enabled the turret inherits a percentage of the player's health and damage.")]
        [SerializeField] private bool scaleStatsWithPlayer = false;
        [Tooltip("Percentage (0-2x) of the player's max health applied to this turret.")]
        [Range(0f, 2f)]
        [SerializeField] private float healthPercentOfPlayer = 0.4f;
        [Tooltip("Percentage (0-2x) of the player's damage applied to the turret projectiles.")]
        [Range(0f, 2f)]
        [SerializeField] private float damagePercentOfPlayer = 0.4f;

        [Header("Death")]
        [Tooltip("Optional prefab spawned when the turret is destroyed.")]
        [SerializeField] private GameObject deathPrefab;

        [Tooltip("Lifetime for the spawned death prefab. 0 = let prefab handle cleanup.")]
        [SerializeField] private float deathPrefabLifetime = 3f;

        readonly Collider2D[] _scanBuffer = new Collider2D[32];
        EnemyAI.IDamageable _currentTargetDamageable;
        Transform _currentTargetTransform;
        float _nextShotTime;
        Sprite[] _directionSprites;
        CompanionHealth _health;
        Transform _barRoot;
        SpriteRenderer _barBack, _barFill;
        static Sprite s_barPixel;
        Vector3 _recoilRestLocalPos;
        Coroutine _recoilRoutine;
        float _lifetimeRemaining;
        Transform _lifetimeBarRoot;
        SpriteRenderer _lifetimeBarBack, _lifetimeBarFill;
        bool _deathTriggered;
        bool _idleActive;
        int _idleCenterIndex;
        int _idlePatternIndex;
        float _nextIdleFrameTime;
        int _idleSweepsCompleted;
        int _idleWindowDirection = 1;
        int _lastDirectionIndex = -1;
        static readonly int[] IdlePatternOffsets = { 0, 1, 0, -1 };
        int _baseProjectileDamage;
        int _baseMaxHealth;
        PlayerStats _playerStats;
        bool _statsSubscribed;

        void Awake()
        {
            _health = GetComponent<CompanionHealth>();
            if (_health)
            {
                _health.OnHealthChanged += HandleHealthChanged;
                _health.onDied += HandleDied;
                _baseMaxHealth = Mathf.Max(1, _health.maxHealth);
            }
            else
            {
                _baseMaxHealth = 0;
            }

            _baseProjectileDamage = Mathf.Max(1, projectileDamage);

            if (!recoilTransform)
            {
                recoilTransform = transform;
            }
            _recoilRestLocalPos = recoilTransform.localPosition;

            if (useLifetime)
            {
                _lifetimeRemaining = Mathf.Max(0.01f, lifetimeSeconds);
            }

            CacheDirectionSprites();
            _idleCenterIndex = WrapDirectionIndex(Random.Range(0, _directionSprites != null && _directionSprites.Length > 0 ? _directionSprites.Length : 1));
            if (!spriteRenderer)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (!muzzleTransform)
            {
                muzzleTransform = transform;
            }

            CreateHealthBar();
            CreateLifetimeBar();
        }

        void OnEnable()
        {
            if (scaleStatsWithPlayer)
            {
                TryRegisterStatScaling();
            }
            else
            {
                ApplyBaseStats();
            }
        }

        void OnDisable()
        {
            UnregisterPlayerStatListener();
        }

        void Update()
        {
            if (scaleStatsWithPlayer)
            {
                if (!_statsSubscribed)
                {
                    TryRegisterStatScaling();
                }
            }
            else if (_statsSubscribed)
            {
                UnregisterPlayerStatListener();
                ApplyBaseStats();
            }

            if (!projectilePrefab)
            {
                return;
            }

            if (HandleDeathState())
            {
                return;
            }

            if (useLifetime && HandleLifetimeCountdown())
            {
                return;
            }

            ValidateCurrentTarget();

            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(targetLayers);
            filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);
            int detectedCount = Physics2D.OverlapCircle(transform.position, engageRadius, filter, _scanBuffer);
            if (detectedCount > 0)
            {
                AcquireTargetFromBuffer(detectedCount);
            }
            else
            {
                ClearCurrentTarget();
            }

            if (_currentTargetTransform)
            {
                UpdateIdleAnimation(false);
                Vector3 targetPos = _currentTargetTransform.position;
                Vector3 muzzlePos = muzzleTransform ? muzzleTransform.position : transform.position;
                Vector2 aimDirection = (targetPos - muzzlePos);
                UpdateDirectionSprite(aimDirection);

                if (Time.time >= _nextShotTime && HasLineOfSightToTarget(muzzlePos, targetPos))
                {
                    FireProjectile(muzzlePos, aimDirection);
                    _nextShotTime = Time.time + fireInterval;
                }
            }
            else
            {
                UpdateIdleAnimation(true);
            }
        }

        void LateUpdate()
        {
            if (useHealthBar && _barRoot)
            {
                _barRoot.position = (Vector2)transform.position + barOffset;
                if (keepBarUpright)
                {
                    _barRoot.rotation = Quaternion.identity;
                }
            }

            if (_lifetimeBarRoot)
            {
                _lifetimeBarRoot.position = (Vector2)transform.position + lifetimeBarOffset;
                if (keepBarUpright)
                {
                    _lifetimeBarRoot.rotation = Quaternion.identity;
                }
            }
        }

        void OnDestroy()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
                _health.onDied -= HandleDied;
            }
            UnregisterPlayerStatListener();
            DestroyHealthBar();
            DestroyLifetimeBar();
        }

        void ValidateCurrentTarget()
        {
            if (_currentTargetTransform == null)
            {
                return;
            }

            float maxDistance = engageRadius + disengagePadding;
            if (maxDistance <= 0f)
            {
                maxDistance = engageRadius;
            }

            Vector2 toTarget = _currentTargetTransform.position - transform.position;
            if (toTarget.sqrMagnitude > maxDistance * maxDistance)
            {
                ClearCurrentTarget();
                return;
            }

            if (_currentTargetDamageable is Component comp && !comp)
            {
                ClearCurrentTarget();
            }
        }

        void AcquireTargetFromBuffer(int count)
        {
            float bestDistance = float.PositiveInfinity;
            Transform bestTransform = null;
            EnemyAI.IDamageable bestDamageable = null;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _scanBuffer[i];
                if (!col) continue;

                var damageable = col.GetComponentInParent<EnemyAI.IDamageable>();
                if (damageable == null) continue;

                Component component = damageable as Component;
                if (component && component.transform == transform)
                    continue;

                Vector3 pos = component != null ? component.transform.position : col.transform.position;
                float dist = Vector3.Distance(transform.position, pos);

                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestTransform = component != null ? component.transform : col.transform;
                    bestDamageable = damageable;
                }
            }

            if (bestTransform != null)
            {
                _currentTargetTransform = bestTransform;
                _currentTargetDamageable = bestDamageable;
            }
        }

        void ClearCurrentTarget()
        {
            _currentTargetTransform = null;
            _currentTargetDamageable = null;
        }

        bool HasLineOfSightToTarget(Vector3 origin, Vector3 target)
        {
            if (!requireLineOfSight)
            {
                return true;
            }

            Vector2 dir = target - origin;
            RaycastHit2D hit = Physics2D.Raycast(origin, dir.normalized, dir.magnitude, lineOfSightMask);
            if (!hit.collider)
            {
                return true;
            }

            if (_currentTargetTransform == null)
            {
                return false;
            }

            return hit.transform.IsChildOf(_currentTargetTransform);
        }

        void FireProjectile(Vector3 muzzlePosition, Vector2 aimDirection)
        {
            if (aimDirection.sqrMagnitude < 0.0001f)
            {
                aimDirection = Vector2.right;
            }

            Vector2 shotDir = aimDirection.normalized;
            Projectile2D projectile = Instantiate(projectilePrefab, muzzlePosition, Quaternion.identity);
            projectile.Init(shotDir * projectileSpeed, Mathf.Max(1, projectileDamage), projectileLifetime, targetLayers, transform);

            if (muzzleFlashPrefab)
            {
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, shotDir);
                GameObject vfx = Instantiate(muzzleFlashPrefab, muzzlePosition, rotation);
                if (muzzleFlashLifetime > 0f)
                {
                    Destroy(vfx, muzzleFlashLifetime);
                }
            }

            TriggerRecoil(shotDir);
        }

        void UpdateDirectionSprite(Vector2 aimDirection)
        {
            if (_directionSprites == null || _directionSprites.Length == 0)
            {
                return;
            }

            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = Vector2.right;
            }

            int index = GetDirectionIndex(aimDirection);
            ApplyDirectionSprite(index);
            _idleActive = false;
        }

        int GetDirectionIndex(Vector2 dir)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (angle < 0f)
            {
                angle += 360f;
            }

            if (angle < DirectionThresholds[0] || angle >= DirectionThresholds[7])
                return 0; // East
            if (angle < DirectionThresholds[1])
                return 1; // NE
            if (angle < DirectionThresholds[2])
                return 2; // North
            if (angle < DirectionThresholds[3])
                return 3; // NW
            if (angle < DirectionThresholds[4])
                return 4; // West
            if (angle < DirectionThresholds[5])
                return 5; // SW
            if (angle < DirectionThresholds[6])
                return 6; // South
            return 7; // SE
        }

        void ApplyDirectionSprite(int directionIndex)
        {
            if (spriteRenderer == null || _directionSprites == null || _directionSprites.Length == 0)
            {
                return;
            }

            int index = WrapDirectionIndex(directionIndex);
            if (_lastDirectionIndex == index)
            {
                return;
            }

            var sprite = _directionSprites[index];
            if (!sprite) return;
            spriteRenderer.sprite = sprite;
            _lastDirectionIndex = index;
        }

        void CacheDirectionSprites()
        {
            _directionSprites = new[]
            {
                eastSprite,
                northEastSprite,
                northSprite,
                northWestSprite,
                westSprite,
                southWestSprite,
                southSprite,
                southEastSprite
            };
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, engageRadius);
        }

        void HandleHealthChanged(int current, int max)
        {
            if (!useHealthBar) return;
            UpdateHealthBar();
            SetBarVisible(current < max);
        }

        void CreateHealthBar()
        {
            if (!useHealthBar || _barRoot) return;

            if (!s_barPixel)
            {
                var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false)
                {
                    name = "TurretHealthPixel",
                    filterMode = FilterMode.Point
                };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                s_barPixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            _barRoot = new GameObject("TurretHealthBar").transform;
            _barRoot.SetParent(transform, false);
            _barRoot.localPosition = barOffset;

            var backObj = new GameObject("Back");
            backObj.transform.SetParent(_barRoot, false);
            _barBack = backObj.AddComponent<SpriteRenderer>();
            _barBack.sprite = s_barPixel;
            _barBack.color = barBackColor;
            _barBack.sortingOrder = 120;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_barRoot, false);
            _barFill = fillObj.AddComponent<SpriteRenderer>();
            _barFill.sprite = s_barPixel;
            _barFill.color = barFillColor;
            _barFill.sortingOrder = 121;

            UpdateHealthBar(true);
            SetBarVisible(false);
        }

        void UpdateHealthBar(bool force = false)
        {
            if (!useHealthBar || !_barRoot || !_health) return;
            float pct = Mathf.Clamp01(_health.currentHealth / (float)Mathf.Max(1, _health.maxHealth));
            LayoutBar(pct);
        }

        void LayoutBar(float pct)
        {
            if (_barBack)
            {
                _barBack.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);
                _barBack.transform.localPosition = Vector3.zero;
            }

            if (_barFill)
            {
                float clamped = Mathf.Clamp01(pct);
                _barFill.transform.localScale = new Vector3(barSize.x * clamped, barSize.y, 1f);
                _barFill.transform.localPosition = new Vector3(-barSize.x * 0.5f * (1f - clamped), 0f, 0f);
            }
        }

        void SetBarVisible(bool visible)
        {
            if (_barBack) _barBack.enabled = visible;
            if (_barFill) _barFill.enabled = visible;
        }

        void DestroyHealthBar()
        {
            if (_barRoot)
            {
                Destroy(_barRoot.gameObject);
            }
            _barRoot = null;
            _barBack = null;
            _barFill = null;
        }

        void TriggerRecoil(Vector2 shotDir)
        {
            if (!recoilTransform || recoilDistance <= 0f)
                return;

            if (_recoilRoutine != null)
            {
                StopCoroutine(_recoilRoutine);
            }

            _recoilRoutine = StartCoroutine(RecoilRoutine(shotDir));
        }

        System.Collections.IEnumerator RecoilRoutine(Vector2 shotDir)
        {
            if (shotDir.sqrMagnitude < 0.0001f)
            {
                shotDir = Vector2.right;
            }

            Vector3 dir = new Vector3(shotDir.x, shotDir.y, 0f).normalized;
            Vector3 offsetWorld = -dir * recoilDistance;
            Vector3 offsetLocal = recoilTransform.parent ? recoilTransform.parent.InverseTransformVector(offsetWorld) : offsetWorld;
            Vector3 targetPos = _recoilRestLocalPos + offsetLocal;

            float outDuration = Mathf.Max(0.0001f, recoilOutDuration);
            float t = 0f;
            Vector3 startPos = recoilTransform.localPosition;

            while (t < outDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / outDuration);
                float smooth = Mathf.Sin(lerp * Mathf.PI * 0.5f); // ease-out
                recoilTransform.localPosition = Vector3.Lerp(startPos, targetPos, smooth);
                yield return null;
            }

            recoilTransform.localPosition = targetPos;

            float returnDuration = Mathf.Max(0.0001f, recoilReturnDuration);
            t = 0f;
            startPos = targetPos;
            while (t < returnDuration)
            {
                t += Time.deltaTime;
                float lerp = Mathf.Clamp01(t / returnDuration);
                float smooth = 1f - Mathf.Cos(lerp * Mathf.PI * 0.5f); // ease-in
                recoilTransform.localPosition = Vector3.Lerp(startPos, _recoilRestLocalPos, smooth);
                yield return null;
            }

            recoilTransform.localPosition = _recoilRestLocalPos;
            _recoilRoutine = null;
        }

        bool HandleLifetimeCountdown()
        {
            if (!useLifetime)
            {
                return false;
            }

            _lifetimeRemaining -= Time.deltaTime;
            UpdateLifetimeBar();

            if (_lifetimeRemaining <= 0f)
            {
                TriggerDeath();
                return true;
            }

            return false;
        }

        void CreateLifetimeBar()
        {
            if (!useLifetime || _lifetimeBarRoot) return;

            if (!s_barPixel)
            {
                var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false)
                {
                    name = "TurretLifetimePixel",
                    filterMode = FilterMode.Point
                };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                s_barPixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            }

            _lifetimeBarRoot = new GameObject("TurretLifetimeBar").transform;
            _lifetimeBarRoot.SetParent(transform, false);
            _lifetimeBarRoot.localPosition = lifetimeBarOffset;

            var backObj = new GameObject("Back");
            backObj.transform.SetParent(_lifetimeBarRoot, false);
            _lifetimeBarBack = backObj.AddComponent<SpriteRenderer>();
            _lifetimeBarBack.sprite = s_barPixel;
            _lifetimeBarBack.color = lifetimeBarBackColor;
            _lifetimeBarBack.sortingOrder = 130;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(_lifetimeBarRoot, false);
            _lifetimeBarFill = fillObj.AddComponent<SpriteRenderer>();
            _lifetimeBarFill.sprite = s_barPixel;
            _lifetimeBarFill.color = lifetimeBarFillColor;
            _lifetimeBarFill.sortingOrder = 131;

            UpdateLifetimeBar(true);
        }

        void UpdateLifetimeBar(bool force = false)
        {
            if (!useLifetime || !_lifetimeBarRoot) return;
            float pct = Mathf.Clamp01(_lifetimeRemaining / Mathf.Max(0.01f, lifetimeSeconds));
            LayoutLifetimeBar(pct);
        }

        void LayoutLifetimeBar(float pct)
        {
            if (_lifetimeBarBack)
            {
                _lifetimeBarBack.transform.localScale = new Vector3(lifetimeBarSize.x, lifetimeBarSize.y, 1f);
                _lifetimeBarBack.transform.localPosition = Vector3.zero;
            }

            if (_lifetimeBarFill)
            {
                float clamped = Mathf.Clamp01(pct);
                _lifetimeBarFill.transform.localScale = new Vector3(lifetimeBarSize.x * clamped, lifetimeBarSize.y, 1f);
                float xOffset = lifetimeBarSize.x * 0.5f * (1f - clamped);
                _lifetimeBarFill.transform.localPosition = new Vector3(-xOffset, 0f, 0f);
            }
        }

        void DestroyLifetimeBar()
        {
            if (_lifetimeBarRoot)
            {
                Destroy(_lifetimeBarRoot.gameObject);
            }
            _lifetimeBarRoot = null;
            _lifetimeBarBack = null;
            _lifetimeBarFill = null;
        }

        void UpdateIdleAnimation(bool shouldIdle)
        {
            if (!useIdleScanAnimation || spriteRenderer == null || _directionSprites == null || _directionSprites.Length == 0)
            {
                return;
            }

            if (!shouldIdle)
            {
                _idleActive = false;
                return;
            }

            if (!_idleActive)
            {
                _idleActive = true;
                InitializeIdleWindow();
            }

            if (Time.time < _nextIdleFrameTime)
            {
                return;
            }

            _nextIdleFrameTime = Time.time + Mathf.Max(0.1f, idleFrameDuration);
            int offset = IdlePatternOffsets[_idlePatternIndex];
            _idlePatternIndex = (_idlePatternIndex + 1) % IdlePatternOffsets.Length;
            if (_idlePatternIndex == 0)
            {
                _idleSweepsCompleted++;
                if (_idleSweepsCompleted >= Mathf.Max(1, idleSweepsBeforeShift))
                {
                    ShiftIdleWindow();
                    _idleSweepsCompleted = 0;
                }
            }

            int directionIndex = WrapDirectionIndex(_idleCenterIndex + offset);
            ApplyDirectionSprite(directionIndex);
        }

        void InitializeIdleWindow()
        {
            PickIdleCenter();
            _idlePatternIndex = 0;
            _idleSweepsCompleted = 0;
            _idleWindowDirection = Random.value > 0.5f ? 1 : -1;
            _nextIdleFrameTime = 0f;
        }

        void PickIdleCenter()
        {
            int count = _directionSprites != null ? _directionSprites.Length : 0;
            if (count <= 0) return;

            for (int attempt = 0; attempt < count * 2; attempt++)
            {
                int index = Random.Range(0, count);
                if (!HasSpriteAt(index) || !HasSpriteAt(index + 1) || !HasSpriteAt(index - 1))
                {
                    continue;
                }

                _idleCenterIndex = WrapDirectionIndex(index);
                return;
            }

            _idleCenterIndex = 0;
        }

        void ShiftIdleWindow()
        {
            int count = _directionSprites != null ? _directionSprites.Length : 0;
            if (count <= 0) return;

            for (int attempt = 0; attempt < count; attempt++)
            {
                _idleCenterIndex = WrapDirectionIndex(_idleCenterIndex + _idleWindowDirection);
                if (HasSpriteAt(_idleCenterIndex) && HasSpriteAt(_idleCenterIndex + 1) && HasSpriteAt(_idleCenterIndex - 1))
                {
                    return;
                }
            }

            PickIdleCenter();
            _idleWindowDirection = -_idleWindowDirection;
        }

        bool HasSpriteAt(int index)
        {
            if (_directionSprites == null || _directionSprites.Length == 0)
            {
                return false;
            }

            int wrapped = WrapDirectionIndex(index);
            return wrapped >= 0 && wrapped < _directionSprites.Length && _directionSprites[wrapped] != null;
        }

        int WrapDirectionIndex(int index)
        {
            int count = _directionSprites != null ? _directionSprites.Length : 0;
            if (count <= 0) return 0;
            int wrapped = index % count;
            if (wrapped < 0) wrapped += count;
            return wrapped;
        }

        bool HandleDeathState()
        {
            if (_deathTriggered)
            {
                return true;
            }

            if (_health && _health.IsDead)
            {
                TriggerDeath();
                return true;
            }

            return false;
        }

        void HandleDied()
        {
            TriggerDeath();
        }

        void TriggerDeath()
        {
            if (_deathTriggered)
                return;

            _deathTriggered = true;

            if (deathPrefab)
            {
                GameObject instance = Instantiate(deathPrefab, transform.position, transform.rotation);
                if (deathPrefabLifetime > 0f)
                {
                    Destroy(instance, deathPrefabLifetime);
                }
            }

            DisableRenderers();
            DisableColliders();
            DestroyHealthBar();
            DestroyLifetimeBar();

            if (_lifetimeBarRoot)
            {
                Destroy(_lifetimeBarRoot.gameObject);
            }

            // Ensure Update stops doing work
            enabled = false;

            Destroy(gameObject);
        }

        void TryRegisterStatScaling()
        {
            if (_statsSubscribed)
            {
                return;
            }

            _playerStats = PlayerStats.Instance;
            if (_playerStats == null)
            {
                return;
            }

            _playerStats.StatsChanged += HandlePlayerStatsChanged;
            _statsSubscribed = true;
            HandlePlayerStatsChanged(_playerStats.CurrentStats);
        }

        void UnregisterPlayerStatListener()
        {
            if (!_statsSubscribed)
            {
                return;
            }

            if (_playerStats != null)
            {
                _playerStats.StatsChanged -= HandlePlayerStatsChanged;
            }

            _playerStats = null;
            _statsSubscribed = false;
        }

        void HandlePlayerStatsChanged(PlayerStats.StatSnapshot snapshot)
        {
            if (!scaleStatsWithPlayer)
            {
                return;
            }

            int playerMaxHealth = PlayerHealth.Instance != null ? PlayerHealth.Instance.maxHealth : Mathf.Max(1, snapshot.Health);
            if (_health != null && playerMaxHealth > 0)
            {
                int scaledHealth = Mathf.Max(1, Mathf.RoundToInt(playerMaxHealth * Mathf.Clamp(healthPercentOfPlayer, 0f, 2f)));
                _health.ApplyScaledMaxHealth(scaledHealth);
            }

            int playerDamageStat = Mathf.Max(0, snapshot.Strength + snapshot.WeaponDamage);
            int scaledDamage = Mathf.Max(1, Mathf.RoundToInt(playerDamageStat * Mathf.Clamp(damagePercentOfPlayer, 0f, 2f)));
            projectileDamage = scaledDamage;
        }

        void ApplyBaseStats()
        {
            projectileDamage = Mathf.Max(1, _baseProjectileDamage);
            if (_health != null && _baseMaxHealth > 0)
            {
                _health.ApplyScaledMaxHealth(_baseMaxHealth);
            }
        }

        void DisableRenderers()
        {
            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                sr.enabled = false;
            }
        }

        void DisableColliders()
        {
            var colliders = GetComponentsInChildren<Collider2D>(true);
            foreach (var col in colliders)
            {
                col.enabled = false;
            }
        }
    }
}




