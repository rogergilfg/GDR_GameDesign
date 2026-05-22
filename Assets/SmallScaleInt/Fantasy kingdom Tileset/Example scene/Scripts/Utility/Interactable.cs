using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.EventSystems;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScaleInc.TopDownPixelCharactersPack1;
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SmallScale.FantasyKingdomTileset
{
[RequireComponent(typeof(SpriteRenderer))]
public class Interactable : MonoBehaviour
{
    [Header("Sprites")]
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("References")]
    [SerializeField] private GameObject colliderObject;

    [Header("Settings")]
    [SerializeField] private bool startOpen;
    [SerializeField] private bool allowToggle = true;

    [Header("Open Shake")]
    [SerializeField] private bool enableShakeBeforeOpen;
    [SerializeField, Min(0f)] private float shakeDuration = 0.4f;
    [SerializeField, Min(0f)] private float shakeStrength = 0.15f;
    [SerializeField, Min(0.01f)] private float shakeFrequency = 18f;
    [SerializeField] private AnimationCurve shakeIntensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Open VFX")]
    [SerializeField] private bool spawnOpenVfx;
    [SerializeField] private GameObject openVfxPrefab;
    [SerializeField] private Vector3 openVfxOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float openVfxCleanup = 4f;

    [Header("Idle VFX")]
    [SerializeField] private bool spawnIdleVfx;
    [SerializeField] private GameObject idleVfxPrefab;
    [SerializeField] private Vector3 idleVfxOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float idleVfxDelay = 0.35f;
    [SerializeField, Min(0f)] private float idleVfxLifetime;

    [Header("Close VFX")]
    [SerializeField] private bool spawnCloseVfx;
    [SerializeField] private GameObject closeVfxPrefab;
    [SerializeField] private Vector3 closeVfxOffset = Vector3.zero;
    [SerializeField, Min(0f)] private float closeVfxCleanup = 4f;

    [Header("Interaction")]
    [SerializeField] private Transform player;
    [SerializeField, Min(0f)] private float interactionRange = 1.5f;
    [SerializeField, Tooltip("How much world-space padding to remove from the clickable sprite bounds  when detecting pointer hits. Larger values shrink the target area.")]
    private Vector2 pointerBoundsPadding = new Vector2(0.08f, 0.08f);

    // Hover highlight (outline) removed

    [Header("Resource Rewards (Dynamic)")]
    [SerializeField] private bool grantResources;
    [SerializeField, HideInInspector] private ResourceSet resourceRewardsSet;

    [Header("Loot Drops")]
    [SerializeField] private bool spawnLootOnOpen;
    [SerializeField] private LootPickup lootPickupPrefab;
    [SerializeField] private Transform lootSpawnPoint;
    [SerializeField] private Vector2 lootScatterRadius = new Vector2(0.75f, 0.75f);
    [SerializeField] private LootDropDefinition[] lootDrops;




    [Header("Health Reward")]
    [SerializeField] private bool grantHealth;
    [SerializeField] private int flatHealAmount = 25;
    [SerializeField, Range(0f, 1f)] private float percentOfMaxHealth;
    [SerializeField, Range(0f, 0.5f)] private float healVariance = 0.1f;
    [SerializeField] private bool requireMissingHealth;
    [SerializeField] private bool allowOverheal;
    [SerializeField] private Vector3 healTextOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Mana Reward")]
    [SerializeField] private bool grantMana;
    [SerializeField] private float flatManaAmount = 25f;
    [SerializeField, Range(0f, 1f)] private float percentOfMaxMana;
    [SerializeField, Range(0f, 0.5f)] private float manaVariance = 0.1f;
    [SerializeField] private bool requireMissingMana;
    [SerializeField] private Vector3 manaTextOffset = new Vector3(0f, 0.6f, 0f);

    [Header("Resource Costs (Dynamic)")]
    [SerializeField] private bool requireResources;
    [SerializeField, HideInInspector] private ResourceSet resourceCostSet;

    [Header("Resource Feedback")]
    [SerializeField] private bool showResourceText = true;

    [Header("Post Interaction")]
    [SerializeField] private bool destroyAfterUse;

    [Header("Hazard Effect")]
    [SerializeField] private bool spawnHazardOnOpen;
    [SerializeField, Min(0f)] private float hazardDuration = 4f;
    [SerializeField, Min(0f)] private float hazardRadius = 2f;
    [SerializeField, Min(0f)] private float hazardDps = 6f;
    [SerializeField, Min(0.01f)] private float hazardTickInterval = 0.5f;
    [SerializeField] private LayerMask hazardMask;
    [SerializeField] private GameObject hazardVfx;
    [SerializeField, Min(0f)] private float hazardVfxCleanup = 4.5f;
    [SerializeField] private Vector2 hazardFctOffset = new Vector2(0f, 0.25f);
    [SerializeField] private bool showHazardCombatText = true;

    [Header("Destruction")]
    [SerializeField] private bool enableDestruction;
    [SerializeField] private DestructibleProp2D destructibleProp;
    [SerializeField] private bool triggerOpenSequenceOnDestruction = true;
    [SerializeField] private bool destroyGameObjectAfterDestruction;
    [SerializeField, Min(0f)] private float destructionCleanupDelay;

    [Header("Portal Settings")]
    [SerializeField] private bool enablePortal;
    [SerializeField] private Transform portalDestination;
    [SerializeField, Min(0f)] private float portalActivationDelay = 0.15f;
    [SerializeField, Min(0f)] private float portalTravelDelay = 0.15f;
    [SerializeField] private Vector3 portalDestinationOffset = Vector3.zero;
    [SerializeField] private bool fadePlayerDuringTeleport = true;
    [SerializeField, Min(0f)] private float portalFadeOutDuration = 0.3f;
    [SerializeField, Min(0f)] private float portalFadeInDuration = 0.35f;
    [SerializeField] private AnimationCurve portalFadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool chargeResourcesOnEveryPortalUse = true;
    [SerializeField] private bool requireDoubleClickForPortal;
    [SerializeField, Min(0f)] private float portalDoubleClickWindow = 0.35f;
    [SerializeField] private Behaviour[] disableDuringPortal;
    [SerializeField] private GameObject portalDepartureVfx;
    [SerializeField] private Vector3 portalDepartureOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private GameObject portalArrivalVfx;
    [SerializeField] private Vector3 portalArrivalVfxOffset = new Vector3(0f, 0.25f, 0f);
    [SerializeField, Min(0f)] private float portalVfxCleanup = 4f;

    private SpriteRenderer spriteRenderer;
    private Component blockingCollider;
    private bool isOpen;
    private Coroutine hazardCoroutine;
    private Coroutine shakeCoroutine;
    private bool isShaking;
    private Vector3 shakeBaseLocalPosition;
    private PlayerHealth cachedPlayerHealth;
    private PlayerMana cachedPlayerMana;
    private PlayerInventory cachedPlayerInventory;
    private Coroutine portalCoroutine;
    private bool pendingPortalAfterOpen;
    private bool isTeleporting;
    private bool portalDoubleClickArmed;
    private Coroutine portalDoubleClickCoroutine;
    private readonly List<SpriteRenderer> portalSpriteBuffer = new List<SpriteRenderer>();
    private readonly List<Color> portalSpriteBaseColors = new List<Color>();
    private readonly List<Behaviour> disabledDuringPortal = new List<Behaviour>();
    private bool isPointerHovered;
    private bool hasCompletedInteraction;
    private bool destructibleWarningLogged;
    private Coroutine idleVfxCoroutine;
    private GameObject idleVfxInstance;
    private bool runtimeStateRefreshQueued;
    private bool runtimeTargetOpen;
#if UNITY_EDITOR
    private bool editorStateRefreshQueued;
    private bool editorTargetOpen;
#endif

    public event Action<Interactable, Transform> PortalTeleporting;
    public event Action<Interactable, Transform> PortalTeleported;
    public event Action<Interactable, Transform> PortalSequenceCompleted;

    private static readonly List<Interactable> ActiveInteractables = new List<Interactable>();
    private static int cachedPointerFrame = -1;
    private static Interactable cachedPointerInteractable;
    private static Interactable hoveredInteractable;

    private void Awake()
    {
        CacheComponents();
        ResolvePlayer();
        SubscribeToDestructible();
    }

    private void OnEnable()
    {
        if (!ActiveInteractables.Contains(this))
        {
            ActiveInteractables.Add(this);
        }

        SetPointerHover(false);
        SubscribeToDestructible();

        if (Application.isPlaying && isOpen)
        {
            StartIdleVfxRoutine();
        }
    }

    private void OnDisable()
    {
        StopShakeRoutine();
        StopPortalRoutine();
        StopIdleVfxRoutine();
        UnsubscribeFromDestructible();
        ActiveInteractables.Remove(this);
        if (hoveredInteractable == this)
        {
            hoveredInteractable = null;
        }
        SetPointerHover(false);
        if (ActiveInteractables.Count == 0)
        {
            cachedPointerInteractable = null;
            cachedPointerFrame = -1;
        }
        else if (cachedPointerInteractable == this)
        {
            cachedPointerInteractable = null;
        }
    }

    private void Start()
    {
        ApplyState(startOpen);
    }

    private void Update()
    {
        ApplyQueuedRuntimeState();

        Interactable pointerTarget = GetInteractableUnderPointer();
        if (BuildMenuController.IsAnyMenuOpen)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && pointerTarget == this)
        {
            TryInteract();
        }
    }

    private void OnValidate()
    {
        CacheComponents();

        bool targetState = Application.isPlaying ? isOpen : startOpen;
        RequestStateRefresh(targetState);

        if (Application.isPlaying)
        {
            UnsubscribeFromDestructible();
            SubscribeToDestructible();
        }
    }

    private void TryInteract()
    {
        if (enableDestruction && hasCompletedInteraction)
        {
            return;
        }

        if (!IsPlayerInRange())
        {
            return;
        }

        bool hasPortal = enablePortal && portalDestination != null;

        if (!isOpen)
        {
            if (isShaking)
            {
                return;
            }

            if (!TryConsumeResources())
            {
                return;
            }

            pendingPortalAfterOpen = hasPortal;

            if (enableShakeBeforeOpen && shakeDuration > 0f && shakeStrength > 0f)
            {
                StartShakeRoutine();
            }
            else
            {
                ApplyState(true);
                OnOpened();
            }
        }
        else if (hasPortal)
        {
            if (requireDoubleClickForPortal && !portalDoubleClickArmed)
            {
                ArmPortalDoubleClick();
                return;
            }

            if (chargeResourcesOnEveryPortalUse && requireResources)
            {
                if (!TryConsumeResources())
                {
                    DisarmPortalDoubleClick();
                    return;
                }
            }

            TryStartPortalRoutine();
        }
        else if (allowToggle && !isShaking)
        {
            ApplyState(false);
        }
    }

    private void OnOpened(bool triggeredByDestruction = false)
    {
        CompleteInteraction(triggeredByDestruction);
    }

    private void CompleteInteraction(bool triggeredByDestruction)
    {
        if (enableDestruction)
        {
            if (hasCompletedInteraction)
            {
                return;
            }

            hasCompletedInteraction = true;
        }

        Vector3 worldPos = transform.position;

        if (grantResources && resourceRewardsSet != null && !resourceRewardsSet.IsEmpty && DynamicResourceManager.Instance != null)
        {
            DynamicResourceManager.Instance.GrantResources(resourceRewardsSet, worldPos, showResourceText);
        }

        TryGrantHealthReward();
        TryGrantManaReward();
        SpawnLootDrops(worldPos);

        if (spawnHazardOnOpen)
        {
            StartHazard();
        }

        if (pendingPortalAfterOpen)
        {
            pendingPortalAfterOpen = false;
            if (requireDoubleClickForPortal)
            {
                ArmPortalDoubleClick();
            }
            else
            {
                TryStartPortalRoutine();
            }
        }

        if (spawnOpenVfx && openVfxPrefab != null)
        {
            Vector3 spawnPosition = transform.position + openVfxOffset;
            GameObject vfx = Instantiate(openVfxPrefab, spawnPosition, Quaternion.identity);
            if (openVfxCleanup > 0f)
            {
                Destroy(vfx, openVfxCleanup);
            }
        }

        bool waitForHazard = spawnHazardOnOpen && hazardCoroutine != null;
        bool waitForPortal = enablePortal && (portalCoroutine != null || isTeleporting);

        if (!triggeredByDestruction && destroyAfterUse)
        {
            if (waitForHazard || waitForPortal)
            {
                StartCoroutine(DestroyAfterEffects());
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else if (triggeredByDestruction && destroyGameObjectAfterDestruction)
        {
            if (waitForHazard || waitForPortal)
            {
                StartCoroutine(DestroyAfterEffects(destructionCleanupDelay));
            }
            else if (destructionCleanupDelay > 0f)
            {
                Destroy(gameObject, destructionCleanupDelay);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void SpawnLootDrops(Vector3 interactionPoint)
    {
        if (!Application.isPlaying || !spawnLootOnOpen)
        {
            return;
        }

        if (lootDrops == null || lootDrops.Length == 0)
        {
            return;
        }

        if (lootPickupPrefab == null)
        {
            Debug.LogWarning("Loot drops are enabled but no loot pickup prefab has been assigned.", this);
            return;
        }

        PlayerInventory targetInventory = ResolvePlayerInventory();
        Transform currentPlayer = ResolvePlayer();
        Vector3 spawnOrigin = lootSpawnPoint != null ? lootSpawnPoint.position : interactionPoint;

        foreach (LootDropDefinition drop in lootDrops)
        {
            if (drop == null)
            {
                continue;
            }

            if (drop.Kind == LootDropDefinition.LootKind.Resource)
            {
                if (drop.ResourceType == null || drop.ResourceAmount <= 0)
                {
                    continue;
                }

                var set = new ResourceSet();
                set.Set(drop.ResourceType, drop.ResourceAmount);
                DynamicResourceManager.Instance?.GrantResources(set, spawnOrigin, showResourceText);
                continue;
            }

            // Default: gear item
            GearItem item = drop.Item;
            if (item == null)
            {
                continue;
            }

            int quantity = Mathf.Max(1, drop.Quantity);
            for (int i = 0; i < quantity; i++)
            {
                Vector2 scatter = Vector2.zero;
                if (lootScatterRadius != Vector2.zero)
                {
                    Vector2 randomPoint = UnityEngine.Random.insideUnitCircle;
                    scatter = new Vector2(randomPoint.x * lootScatterRadius.x, randomPoint.y * lootScatterRadius.y);
                }

                Vector3 finalPosition = spawnOrigin + new Vector3(scatter.x, scatter.y, 0f);
                LootPickup pickupInstance = Instantiate(lootPickupPrefab, spawnOrigin, Quaternion.identity);
                pickupInstance.Initialize(item, targetInventory, finalPosition, currentPlayer);
            }
        }
    }

    private void StartShakeRoutine()
    {
        StopShakeRoutine();
        shakeBaseLocalPosition = transform.localPosition;

        if (!gameObject.activeInHierarchy)
        {
            ApplyState(true);
            OnOpened();
            return;
        }

        shakeCoroutine = StartCoroutine(ShakeAndOpenRoutine());
    }

    private void StopShakeRoutine()
    {
        if (shakeCoroutine != null && Application.isPlaying)
        {
            StopCoroutine(shakeCoroutine);
        }

        shakeCoroutine = null;

        if (isShaking)
        {
            transform.localPosition = shakeBaseLocalPosition;
            isShaking = false;
        }
    }

    private IEnumerator ShakeAndOpenRoutine()
    {
        isShaking = true;

        float duration = Mathf.Max(0f, shakeDuration);
        float strength = Mathf.Max(0f, shakeStrength);
        float frequency = Mathf.Max(0.01f, shakeFrequency);

        if (duration <= 0f || strength <= 0f)
        {
            transform.localPosition = shakeBaseLocalPosition;
            isShaking = false;
            ApplyState(true);
            OnOpened();
            shakeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        float seedX = UnityEngine.Random.Range(0f, 1000f);
        float seedY = seedX + 100f;

        while (elapsed < duration)
        {
            float normalizedTime = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float curveMultiplier = shakeIntensityCurve != null ? Mathf.Max(0f, shakeIntensityCurve.Evaluate(normalizedTime)) : 1f;
            float amplitude = strength * curveMultiplier;
            float noiseTime = elapsed * frequency;
            float offsetX = (Mathf.PerlinNoise(seedX, noiseTime) * 2f - 1f) * amplitude;
            float offsetY = (Mathf.PerlinNoise(seedY, noiseTime) * 2f - 1f) * amplitude;
            Vector2 offset = new Vector2(offsetX, offsetY);
            transform.localPosition = shakeBaseLocalPosition + (Vector3)offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = shakeBaseLocalPosition;
        isShaking = false;
        shakeCoroutine = null;

        ApplyState(true);
        OnOpened();
    }

    private void StartHazard()
    {
        if (hazardCoroutine != null)
        {
            return;
        }

        hazardCoroutine = StartCoroutine(HazardRoutine());
    }

    private IEnumerator DestroyAfterEffects(float additionalDelay = 0f)
    {
        while (hazardCoroutine != null || (enablePortal && (portalCoroutine != null || isTeleporting)))
        {
            yield return null;
        }

        if (additionalDelay > 0f)
        {
            yield return new WaitForSeconds(additionalDelay);
        }

        Destroy(gameObject);
    }

    private bool TryConsumeResources()
    {
        if (!requireResources)
        {
            return true;
        }

        if (resourceCostSet == null || resourceCostSet.IsEmpty || DynamicResourceManager.Instance == null)
        {
            // No dynamic cost configured or manager missing; treat as no cost.
            return true;
        }

        return DynamicResourceManager.Instance.TrySpendResources(resourceCostSet, transform.position, showResourceText);
    }

    private void ApplyState(bool open)
    {
        bool wasOpen = isOpen;
        isOpen = open;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = isOpen ? openSprite : closedSprite;
        }

        bool shouldBlock = !isOpen;

        EnableBlockingCollider(shouldBlock);

        if (colliderObject != null && colliderObject != gameObject)
        {
            colliderObject.SetActive(shouldBlock);
        }

        if (Application.isPlaying)
        {
            if (isOpen && !wasOpen)
            {
                StartIdleVfxRoutine();
            }
            else if (!isOpen && wasOpen)
            {
                StopIdleVfxRoutine();
                DisarmPortalDoubleClick();
                SpawnCloseVfx();
            }
        }
    }

    private void RequestStateRefresh(bool targetOpen)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            QueueEditorStateRefresh(targetOpen);
            return;
        }
#endif

        runtimeTargetOpen = targetOpen;
        runtimeStateRefreshQueued = true;
    }

    private void ApplyQueuedRuntimeState()
    {
        if (!runtimeStateRefreshQueued || !Application.isPlaying)
        {
            return;
        }

        runtimeStateRefreshQueued = false;
        ApplyState(runtimeTargetOpen);
    }

#if UNITY_EDITOR
    private void QueueEditorStateRefresh(bool targetOpen)
    {
        editorTargetOpen = targetOpen;

        if (editorStateRefreshQueued)
        {
            return;
        }

        editorStateRefreshQueued = true;
        EditorApplication.delayCall += PerformEditorStateRefresh;
    }

    private void PerformEditorStateRefresh()
    {
        editorStateRefreshQueued = false;

        if (this == null)
        {
            return;
        }

        ApplyState(editorTargetOpen);
    }
#endif

    private void StartIdleVfxRoutine()
    {
        if (!spawnIdleVfx || idleVfxPrefab == null || !Application.isPlaying)
        {
            return;
        }

        StopIdleVfxRoutine(false);

        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (idleVfxDelay <= 0f)
        {
            SpawnIdleVfxInstance();
        }
        else
        {
            idleVfxCoroutine = StartCoroutine(SpawnIdleVfxAfterDelay());
        }
    }

    private IEnumerator SpawnIdleVfxAfterDelay()
    {
        yield return new WaitForSeconds(idleVfxDelay);
        SpawnIdleVfxInstance();
        idleVfxCoroutine = null;
    }

    private void SpawnIdleVfxInstance()
    {
        if (!spawnIdleVfx || idleVfxPrefab == null)
        {
            return;
        }

        DestroyIdleVfxInstance();

        Vector3 spawnPosition = transform.position + idleVfxOffset;
        idleVfxInstance = Instantiate(idleVfxPrefab, spawnPosition, Quaternion.identity);

        if (idleVfxLifetime > 0f)
        {
            Destroy(idleVfxInstance, idleVfxLifetime);
        }
    }

    private void StopIdleVfxRoutine()
    {
        StopIdleVfxRoutine(true);
    }

    private void StopIdleVfxRoutine(bool destroyInstance)
    {
        if (idleVfxCoroutine != null)
        {
            StopCoroutine(idleVfxCoroutine);
            idleVfxCoroutine = null;
        }

        if (destroyInstance)
        {
            DestroyIdleVfxInstance();
        }
    }

    private void DestroyIdleVfxInstance()
    {
        if (idleVfxInstance == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(idleVfxInstance);
        }
        else
        {
            DestroyImmediate(idleVfxInstance);
        }

        idleVfxInstance = null;
    }

    private void SpawnCloseVfx()
    {
        if (!spawnCloseVfx || closeVfxPrefab == null)
        {
            return;
        }

        Vector3 spawnPosition = transform.position + closeVfxOffset;
        GameObject vfx = Instantiate(closeVfxPrefab, spawnPosition, Quaternion.identity);
        if (closeVfxCleanup > 0f)
        {
            Destroy(vfx, closeVfxCleanup);
        }
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (colliderObject == null)
        {
            Transform colliderTransform = transform.Find("Collider");
            if (colliderTransform != null)
            {
                colliderObject = colliderTransform.gameObject;
            }
        }

        if (blockingCollider == null && colliderObject != null)
        {
            blockingCollider = colliderObject.GetComponent<Collider2D>();
            if (blockingCollider == null)
            {
                blockingCollider = colliderObject.GetComponent<Collider>();
            }
        }

        if (blockingCollider == null)
        {
            blockingCollider = GetComponent<Collider2D>();
            if (blockingCollider == null)
            {
                blockingCollider = GetComponent<Collider>();
            }
        }

        // Outline controller removed

        if (destructibleProp == null)
        {
            destructibleProp = GetComponent<DestructibleProp2D>();
        }
    }

    private void EnableBlockingCollider(bool enabled)
    {
        if (blockingCollider == null)
        {
            return;
        }

        if (blockingCollider is Behaviour behaviour)
        {
            behaviour.enabled = enabled;
            return;
        }

        if (blockingCollider is Collider collider)
        {
            collider.enabled = enabled;
        }
    }

    private void SubscribeToDestructible()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!enableDestruction)
        {
            return;
        }

        if (destructibleProp == null)
        {
            destructibleProp = GetComponent<DestructibleProp2D>();
        }

        if (destructibleProp != null)
        {
            destructibleProp.Destroyed -= HandleDestructibleDestroyed;
            destructibleProp.Destroyed += HandleDestructibleDestroyed;
            destructibleWarningLogged = false;
        }
        else
        {
            if (!destructibleWarningLogged)
            {
                Debug.LogWarning("Destruction is enabled but no DestructibleProp2D component was found.", this);
                destructibleWarningLogged = true;
            }
        }
    }

    private void UnsubscribeFromDestructible()
    {
        if (destructibleProp != null)
        {
            destructibleProp.Destroyed -= HandleDestructibleDestroyed;
        }
    }

    private void HandleDestructibleDestroyed()
    {
        if (!enableDestruction)
        {
            return;
        }

        isOpen = true;
        EnableBlockingCollider(false);
        if (colliderObject != null && colliderObject != gameObject)
        {
            colliderObject.SetActive(false);
        }

        SetPointerHover(false);
        UpdatePointerHover(null);
        cachedPointerInteractable = null;
        cachedPointerFrame = -1;
        ActiveInteractables.Remove(this);

        if (triggerOpenSequenceOnDestruction)
        {
            CompleteInteraction(true);
        }
        else
        {
            hasCompletedInteraction = true;
        }
    }

    private bool ContainsPointer(Vector3 worldPoint)
    {
        if (spriteRenderer == null)
        {
            return false;
        }

        worldPoint.z = transform.position.z;

        Bounds bounds = spriteRenderer.bounds;
        if (pointerBoundsPadding.sqrMagnitude > 0f)
        {
            Vector3 size = bounds.size;
            float shrinkX = Mathf.Max(0f, pointerBoundsPadding.x * 2f);
            float shrinkY = Mathf.Max(0f, pointerBoundsPadding.y * 2f);
            size.x = Mathf.Max(0f, size.x - shrinkX);
            size.y = Mathf.Max(0f, size.y - shrinkY);

            if (size.x <= 0f || size.y <= 0f)
            {
                return false;
            }

            bounds = new Bounds(bounds.center, size);
        }

        return bounds.Contains(worldPoint);
    }

    private bool IsPlayerInRange()
    {
        if (interactionRange <= 0f)
        {
            return true;
        }

        Transform currentPlayer = ResolvePlayer();
        if (currentPlayer == null)
        {
            return false;
        }

        float sqrRange = interactionRange * interactionRange;
        float sqrDistance = (currentPlayer.position - transform.position).sqrMagnitude;
        return sqrDistance <= sqrRange;
    }

    private Transform ResolvePlayer()
    {
        if (player != null)
        {
            CachePlayerHealth(player);
            CachePlayerMana(player);
            CachePlayerInventory(player);
            return player;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            CachePlayerHealth(player);
            CachePlayerMana(player);
            CachePlayerInventory(player);
        }

        return player;
    }

    private PlayerInventory ResolvePlayerInventory()
    {
        if (cachedPlayerInventory != null)
        {
            return cachedPlayerInventory;
        }

        Transform currentPlayer = ResolvePlayer();
        if (currentPlayer != null)
        {
            CachePlayerInventory(currentPlayer);
        }

        if (cachedPlayerInventory == null)
        {
            cachedPlayerInventory = FindFirstObjectByType<PlayerInventory>();
        }

        return cachedPlayerInventory;
    }

    private PlayerHealth ResolvePlayerHealth()
    {
        if (cachedPlayerHealth != null)
        {
            return cachedPlayerHealth;
        }

        Transform currentPlayer = ResolvePlayer();
        if (currentPlayer != null)
        {
            CachePlayerHealth(currentPlayer);
        }

        if (cachedPlayerHealth == null)
        {
            cachedPlayerHealth = PlayerHealth.Instance;
        }

        return cachedPlayerHealth;
    }

    private void CachePlayerHealth(Transform target)
    {
        if (target == null || cachedPlayerHealth != null)
        {
            return;
        }

        cachedPlayerHealth = target.GetComponent<PlayerHealth>();
    }

    private PlayerMana ResolvePlayerMana()
    {
        if (cachedPlayerMana != null)
        {
            return cachedPlayerMana;
        }

        Transform currentPlayer = ResolvePlayer();
        if (currentPlayer != null)
        {
            CachePlayerMana(currentPlayer);
        }

        if (cachedPlayerMana == null)
        {
            cachedPlayerMana = PlayerMana.Instance;
        }

        return cachedPlayerMana;
    }

    private void CachePlayerMana(Transform target)
    {
        if (target == null || cachedPlayerMana != null)
        {
            return;
        }

        cachedPlayerMana = target.GetComponent<PlayerMana>();
    }

    private void CachePlayerInventory(Transform target)
    {
        if (target == null || cachedPlayerInventory != null)
        {
            return;
        }

        cachedPlayerInventory = target.GetComponent<PlayerInventory>();
        if (cachedPlayerInventory == null)
        {
            cachedPlayerInventory = target.GetComponentInChildren<PlayerInventory>();
        }
    }

    private void TryGrantHealthReward()
    {
        if (!grantHealth)
        {
            return;
        }

        PlayerHealth playerHealth = ResolvePlayerHealth();
        if (playerHealth == null)
        {
            Debug.LogWarning("No PlayerHealth component found to grant health.", this);
            return;
        }

        bool atFull = playerHealth.currentHealth >= playerHealth.maxHealth;
        if (requireMissingHealth && atFull)
        {
            return;
        }

        float heal = flatHealAmount + percentOfMaxHealth * playerHealth.maxHealth;
        if (healVariance > 0f)
        {
            float varianceMultiplier = 1f + UnityEngine.Random.Range(-healVariance, healVariance);
            heal *= varianceMultiplier;
        }

        int finalHeal = Mathf.Max(1, Mathf.RoundToInt(heal));
        int before = playerHealth.currentHealth;
        int newHp = before + finalHeal;

        if (!allowOverheal)
        {
            newHp = Mathf.Min(newHp, playerHealth.maxHealth);
        }

        int actualHealed = Mathf.Max(0, newHp - before);
        playerHealth.currentHealth = newHp;

        if (actualHealed > 0)
        {
            if (CombatTextManager.Instance != null)
            {
                Vector3 fctPosition = playerHealth.transform.position + healTextOffset;
                CombatTextManager.Instance.SpawnHeal(actualHealed, fctPosition);
            }

            DamageFeedback.I?.PlayHeal();
        }
    }

    private void TryGrantManaReward()
    {
        if (!grantMana)
        {
            return;
        }

        PlayerMana playerMana = ResolvePlayerMana();
        if (playerMana == null)
        {
            Debug.LogWarning("No PlayerMana component found to grant mana.", this);
            return;
        }

        bool atFull = playerMana.CurrentMana >= playerMana.MaxMana - 0.0001f;
        if (requireMissingMana && atFull)
        {
            return;
        }

        float mana = flatManaAmount + percentOfMaxMana * playerMana.MaxMana;
        if (manaVariance > 0f)
        {
            float varianceMultiplier = 1f + UnityEngine.Random.Range(-manaVariance, manaVariance);
            mana *= varianceMultiplier;
        }

        mana = Mathf.Max(0f, mana);

        float before = playerMana.CurrentMana;
        float max = playerMana.MaxMana;
        float target = Mathf.Min(before + mana, max);
        float actualGranted = Mathf.Max(0f, target - before);

        if (actualGranted <= 0f)
        {
            return;
        }

        playerMana.Grant(actualGranted);

        if (CombatTextManager.Instance != null)
        {
            Vector3 fctPosition = playerMana.transform.position + manaTextOffset;
            CombatTextManager.Instance.SpawnMana(actualGranted, fctPosition);
        }
    }

    private static Interactable GetInteractableUnderPointer()
    {
        if (cachedPointerFrame == Time.frameCount)
        {
            return cachedPointerInteractable;
        }

        cachedPointerFrame = Time.frameCount;
        cachedPointerInteractable = null;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return null;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return null;
        }

        Vector3 worldPoint = camera.ScreenToWorldPoint(Input.mousePosition);

        Interactable best = null;
        float bestDistance = float.MaxValue;
        Vector2 pointer2D = new Vector2(worldPoint.x, worldPoint.y);

        foreach (Interactable interactable in ActiveInteractables)
        {
            if (interactable == null || interactable.spriteRenderer == null)
            {
                continue;
            }

            if (!interactable.ContainsPointer(worldPoint))
            {
                continue;
            }

            Vector2 interactablePosition2D = new Vector2(interactable.transform.position.x, interactable.transform.position.y);
            float distance = (pointer2D - interactablePosition2D).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = interactable;
            }
        }

        cachedPointerInteractable = best;
        UpdatePointerHover(best);
        return cachedPointerInteractable;
    }

    private void SetPointerHover(bool hovered)
    {
        if (isPointerHovered == hovered)
        {
            return;
        }

        isPointerHovered = hovered;
    }

    private void RefreshHoverOutline() { }

    private static void UpdatePointerHover(Interactable newHover)
    {
        if (hoveredInteractable == newHover)
        {
            return;
        }

        if (hoveredInteractable != null)
        {
            hoveredInteractable.SetPointerHover(false);
        }

        hoveredInteractable = newHover;

        if (hoveredInteractable != null)
        {
            hoveredInteractable.SetPointerHover(true);
        }
    }

    private IEnumerator HazardRoutine()
    {
        Vector2 center = transform.position;

        if (hazardVfx != null)
        {
            GameObject vfx = Instantiate(hazardVfx, center, Quaternion.identity);
            if (hazardVfxCleanup > 0f)
            {
                Destroy(vfx, hazardVfxCleanup);
            }
        }

        float endAt = Time.time + Mathf.Max(0.01f, hazardDuration);
        float tick = Mathf.Max(0.05f, hazardTickInterval);
        int damagePerTick = Mathf.Max(1, Mathf.RoundToInt(hazardDps * tick));

        if (hazardMask.value == 0)
        {
            hazardCoroutine = null;
            yield break;
        }

        const int Max = 64;
        Collider2D[] buffer = new Collider2D[Max];
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hazardMask);
        filter.SetDepth(float.NegativeInfinity, float.PositiveInfinity);

        while (Time.time < endAt)
        {
            int count = Physics2D.OverlapCircle(center, hazardRadius, filter, buffer);
            if (count > 0)
            {
                var already = new HashSet<Component>();
                for (int i = 0; i < count; i++)
                {
                    Collider2D collider = buffer[i];
                    if (collider == null)
                    {
                        continue;
                    }

                    var damageable = collider.GetComponentInParent<EnemyAI.IDamageable>();
                    if (damageable == null)
                    {
                        continue;
                    }

                    Component key = damageable as Component;
                    if (key != null && already.Contains(key))
                    {
                        continue;
                    }

                    Vector2 to = (Vector2)collider.bounds.center - center;
                    Vector2 push = to.sqrMagnitude < 0.0001f ? Vector2.right : to.normalized;

                    damageable.TakeDamage(damagePerTick, push);

                    if (showHazardCombatText && CombatTextManager.Instance != null)
                    {
                        Vector3 fctPos = (Vector3)collider.bounds.center + (Vector3)hazardFctOffset;
                        CombatTextManager.Instance.SpawnDamage(damagePerTick, fctPos, false);
                    }

                    if (key != null)
                    {
                        already.Add(key);
                    }
                }
            }

            yield return new WaitForSeconds(tick);
        }

        hazardCoroutine = null;
    }

    public void ConfigurePortalDestination(Transform destination, Vector3 offset, bool autoEnable = true)
    {
        portalDestination = destination;
        portalDestinationOffset = offset;

        if (autoEnable)
        {
            enablePortal = destination != null;
        }

        if (!enablePortal)
        {
            StopPortalRoutine();
            pendingPortalAfterOpen = false;
            DisarmPortalDoubleClick();
        }
    }

    // Dungeon utility: configure chest loot dynamically
    public void ConfigureLootDrops(LootDropDefinition[] drops, bool enable = true)
    {
        spawnLootOnOpen = enable;
        lootDrops = drops;
    }

    public void SetPortalEnabled(bool active)
    {
        if (enablePortal == active)
        {
            return;
        }

        enablePortal = active;

        if (!enablePortal)
        {
            StopPortalRoutine();
            pendingPortalAfterOpen = false;
            DisarmPortalDoubleClick();
        }
    }

    private void TryStartPortalRoutine()
    {
        if (!enablePortal || isTeleporting)
        {
            return;
        }

        if (portalDestination == null)
        {
            Debug.LogWarning("Portal interaction attempted without a destination.", this);
            return;
        }

        DisarmPortalDoubleClick();

        if (!gameObject.activeInHierarchy)
        {
            PerformImmediateTeleport();
            return;
        }

        if (portalCoroutine != null)
        {
            StopCoroutine(portalCoroutine);
        }

        portalCoroutine = StartCoroutine(PortalRoutine());
    }

    private void ArmPortalDoubleClick()
    {
        if (!requireDoubleClickForPortal)
        {
            return;
        }

        portalDoubleClickArmed = true;

        if (!Application.isPlaying || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (portalDoubleClickCoroutine != null)
        {
            StopCoroutine(portalDoubleClickCoroutine);
        }

        if (portalDoubleClickWindow <= 0f)
        {
            portalDoubleClickCoroutine = null;
            return;
        }

        portalDoubleClickCoroutine = StartCoroutine(PortalDoubleClickTimeout());
    }

    private void DisarmPortalDoubleClick()
    {
        portalDoubleClickArmed = false;

        if (portalDoubleClickCoroutine != null && Application.isPlaying)
        {
            StopCoroutine(portalDoubleClickCoroutine);
        }

        portalDoubleClickCoroutine = null;
    }

    private IEnumerator PortalDoubleClickTimeout()
    {
        yield return new WaitForSeconds(portalDoubleClickWindow);
        portalDoubleClickCoroutine = null;
        portalDoubleClickArmed = false;
    }

    private void StopPortalRoutine()
    {
        if (portalCoroutine != null && Application.isPlaying)
        {
            StopCoroutine(portalCoroutine);
        }

        portalCoroutine = null;

        if (isTeleporting)
        {
            RestorePortalSprites();
            RestoreBehavioursAfterPortal();
            isTeleporting = false;
        }

        pendingPortalAfterOpen = false;
        DisarmPortalDoubleClick();
    }

    private IEnumerator PortalRoutine()
    {
        isTeleporting = true;

        Transform currentPlayer = ResolvePlayer();
        if (currentPlayer == null)
        {
            isTeleporting = false;
            portalCoroutine = null;
            yield break;
        }

        CollectPlayerSpriteRenderers(currentPlayer);
        CachePortalColors();
        DisableBehavioursForPortal();

        if (portalActivationDelay > 0f)
        {
            yield return new WaitForSeconds(portalActivationDelay);
        }

        SpawnPortalVfx(portalDepartureVfx, currentPlayer.position + portalDepartureOffset);

        if (fadePlayerDuringTeleport && portalSpriteBuffer.Count > 0)
        {
            yield return FadePortalSprites(1f, 0f, portalFadeOutDuration);
        }
        else if (fadePlayerDuringTeleport && portalFadeOutDuration > 0f)
        {
            yield return new WaitForSeconds(portalFadeOutDuration);
        }

        if (portalTravelDelay > 0f)
        {
            yield return new WaitForSeconds(portalTravelDelay);
        }

        PerformTeleport(currentPlayer);

        SpawnPortalVfx(portalArrivalVfx, currentPlayer.position + portalArrivalVfxOffset);

        if (fadePlayerDuringTeleport && portalSpriteBuffer.Count > 0)
        {
            yield return FadePortalSprites(0f, 1f, portalFadeInDuration);
        }
        else if (fadePlayerDuringTeleport && portalFadeInDuration > 0f)
        {
            yield return new WaitForSeconds(portalFadeInDuration);
        }

        RestorePortalSprites();
        RestoreBehavioursAfterPortal();

        portalCoroutine = null;
        isTeleporting = false;
        PortalSequenceCompleted?.Invoke(this, currentPlayer);
    }

    private void PerformImmediateTeleport()
    {
        Transform currentPlayer = ResolvePlayer();
        if (currentPlayer == null)
        {
            return;
        }

        PerformTeleport(currentPlayer);
        PortalSequenceCompleted?.Invoke(this, currentPlayer);
    }

    private void PerformTeleport(Transform currentPlayer)
    {
        if (currentPlayer == null || portalDestination == null)
        {
            return;
        }

        PortalTeleporting?.Invoke(this, currentPlayer);

        Vector3 destination = portalDestination.position + portalDestinationOffset;
        currentPlayer.position = destination;

        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera.TryGetComponent(out SmoothCameraFollow cameraFollow) && cameraFollow.target == currentPlayer)
        {
            cameraFollow.SnapToTargetImmediately();
        }

        PortalTeleported?.Invoke(this, currentPlayer);
    }

    private void SpawnPortalVfx(GameObject prefab, Vector3 position)
    {
        if (prefab == null)
        {
            return;
        }

        GameObject instance = Instantiate(prefab, position, Quaternion.identity);
        if (portalVfxCleanup > 0f)
        {
            Destroy(instance, portalVfxCleanup);
        }
    }

    private void CollectPlayerSpriteRenderers(Transform currentPlayer)
    {
        portalSpriteBuffer.Clear();
        portalSpriteBuffer.AddRange(currentPlayer.GetComponentsInChildren<SpriteRenderer>());
    }

    private void CachePortalColors()
    {
        portalSpriteBaseColors.Clear();
        for (int i = 0; i < portalSpriteBuffer.Count; i++)
        {
            portalSpriteBaseColors.Add(portalSpriteBuffer[i].color);
        }
    }

    private IEnumerator FadePortalSprites(float fromMultiplier, float toMultiplier, float duration)
    {
        if (duration <= 0f)
        {
            ApplyPortalFade(toMultiplier);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);
            if (portalFadeCurve != null)
            {
                t = portalFadeCurve.Evaluate(t);
            }

            float value = Mathf.Lerp(fromMultiplier, toMultiplier, t);
            ApplyPortalFade(value);

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyPortalFade(toMultiplier);
    }

    private void ApplyPortalFade(float multiplier)
    {
        float clamped = Mathf.Clamp01(multiplier);
        for (int i = 0; i < portalSpriteBuffer.Count; i++)
        {
            SpriteRenderer renderer = portalSpriteBuffer[i];
            if (renderer == null)
            {
                continue;
            }

            Color baseColor = portalSpriteBaseColors[i];
            baseColor.a = portalSpriteBaseColors[i].a * clamped;
            renderer.color = baseColor;
        }
    }

    private void RestorePortalSprites()
    {
        for (int i = 0; i < portalSpriteBuffer.Count; i++)
        {
            SpriteRenderer renderer = portalSpriteBuffer[i];
            if (renderer == null)
            {
                continue;
            }

            renderer.color = portalSpriteBaseColors[i];
        }

        portalSpriteBuffer.Clear();
        portalSpriteBaseColors.Clear();
    }

    private void DisableBehavioursForPortal()
    {
        disabledDuringPortal.Clear();

        if (disableDuringPortal == null)
        {
            return;
        }

        foreach (Behaviour behaviour in disableDuringPortal)
        {
            if (behaviour == null || !behaviour.enabled)
            {
                continue;
            }

            behaviour.enabled = false;
            disabledDuringPortal.Add(behaviour);
        }
    }

    private void RestoreBehavioursAfterPortal()
    {
        for (int i = 0; i < disabledDuringPortal.Count; i++)
        {
            Behaviour behaviour = disabledDuringPortal[i];
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledDuringPortal.Clear();
    }
}
}

#if UNITY_EDITOR
namespace SmallScale.FantasyKingdomTileset
{
    [CustomEditor(typeof(Interactable))]
    public class InteractableInspectorInline : Editor
{
    private ResourceDatabase cachedDb;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        var interactable = (Interactable)target;
        if (interactable == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        var db = ResolveDatabase();
        if (db == null || db.Resources == null || db.Resources.Count == 0)
        {
            EditorGUILayout.HelpBox("No ResourceDatabase found. Add a DynamicResourceManager to the scene or create a ResourceDatabase asset.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Rewards UI
        if (GetBool("grantResources"))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Reward Drops", EditorStyles.boldLabel);
            EnsureResourceSet(interactable, "resourceRewardsSet");
            DrawResourceSet(interactable, "resourceRewardsSet", db);
        }

        // Costs UI
        if (GetBool("requireResources"))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Interaction Cost", EditorStyles.boldLabel);
            EnsureResourceSet(interactable, "resourceCostSet");
            DrawResourceSet(interactable, "resourceCostSet", db);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private ResourceDatabase ResolveDatabase()
    {
        if (cachedDb != null) return cachedDb;

        var dyn = UnityEngine.Object.FindFirstObjectByType<DynamicResourceManager>();
        if (dyn != null && dyn.Database != null)
        {
            cachedDb = dyn.Database;
            return cachedDb;
        }

        string[] guids = AssetDatabase.FindAssets("t:ResourceDatabase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedDb = AssetDatabase.LoadAssetAtPath<ResourceDatabase>(path);
        }
        return cachedDb;
    }

    private bool GetBool(string propertyName)
    {
        var prop = serializedObject.FindProperty(propertyName);
        return prop != null && prop.boolValue;
    }

    private static void EnsureResourceSet(Interactable interactable, string fieldName)
    {
        var field = typeof(Interactable).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;
        var set = (ResourceSet)field.GetValue(interactable);
        if (set == null)
        {
            set = new ResourceSet();
            Undo.RecordObject(interactable, "Init ResourceSet");
            field.SetValue(interactable, set);
            EditorUtility.SetDirty(interactable);
        }
    }

    private static void DrawResourceSet(Interactable interactable, string fieldName, ResourceDatabase db)
    {
        var field = typeof(Interactable).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;
        var set = (ResourceSet)field.GetValue(interactable);
        if (set == null) return;

        using (new EditorGUI.IndentLevelScope())
        {
            foreach (var def in db.Resources)
            {
                if (def == null) continue;
                int current = set.Get(def);
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.IntField(def.DisplayName, current);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(interactable, "Edit Resource Amount");
                    set.Set(def, Mathf.Max(0, next));
                    EditorUtility.SetDirty(interactable);
                }
            }
        }
    }
    }
}
#endif














