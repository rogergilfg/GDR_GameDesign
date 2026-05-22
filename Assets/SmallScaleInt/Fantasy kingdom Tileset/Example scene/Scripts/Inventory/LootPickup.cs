using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using SmallScale.FantasyKingdomTileset;
using UnityEngine.EventSystems;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

/// <summary>
/// Represents a collectable loot item that can be spawned into the world.
/// Handles drop animation, idle presentation and pickup logic that adds the
/// associated <see cref="GearItem"/> to the player's inventory.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "LootPickup")]
public class LootPickup : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Item")]
    [SerializeField]
    [Tooltip("Optional item assigned in the inspector for testing.")]
    private GearItem initialItem;

    [SerializeField]
    [Tooltip("When an item is assigned in the inspector, play the drop animation on enable.")]
    private bool animateOnEnableWithInitialItem = true;

    [Header("Rendering")]
    [SerializeField]
    [Tooltip("Renderer used to display the loot's sprite.")]
    private SpriteRenderer iconRenderer;

    [SerializeField]
    [Tooltip("Optional child object that renders a rarity beam underneath the loot.")]
    private Transform rarityBeamRoot;

    [SerializeField]
    [Tooltip("Sprite renderer used to tint the rarity beam.")]
    private SpriteRenderer rarityBeamRenderer;

    [Header("Icon Alignment")]
    [SerializeField]
    [Tooltip("When enabled, the loot icon snaps back to its initial rotation once the drop animation completes and stays aligned afterwards.")]
    private bool resetIconRotationAfterDrop = false;

    [SerializeField]
    [Tooltip("Optional glow sprite renderer that sits behind the icon.")]
    private SpriteRenderer iconGlowRenderer;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Alpha applied to the glow tint when visible.")]
    private float iconGlowAlpha = 0.7f;

    [SerializeField]
    [Tooltip("Trail renderer used during the gather animation to create a rarity-colored streak.")]
    private TrailRenderer gatherTrailRenderer;

    [SerializeField, Range(0f, 1f)]
    [Tooltip("Alpha applied to the gather trail colour.")]
    private float gatherTrailAlpha = 0.85f;

    [SerializeField]
    [Tooltip("Local-space offset applied to the rarity beam relative to the loot pickup transform.")]
    private Vector3 rarityBeamManualOffset = Vector3.zero;

    [Header("Rarity Beam Animation")]
    [SerializeField]
    [Tooltip("Delay in seconds before the rarity beam becomes visible once the loot settles.")]
    private float rarityBeamDelay = 0.35f;

    [SerializeField]
    [Tooltip("How much the rarity beam should scale uniformly on the X axis while pulsing.")]
    private float rarityBeamScaleAmplitude = 0.12f;

    [SerializeField]
    [Tooltip("Speed of the pulsing effect applied to the rarity beam's width.")]
    private float rarityBeamScaleFrequency = 2.1f;

    [SerializeField]
    [Tooltip("How much the rarity beam should extend and contract along its length.")]
    private float rarityBeamLengthAmplitude = 0.25f;

    [SerializeField]
    [Tooltip("Speed of the pulsing effect applied to the rarity beam's length.")]
    private float rarityBeamLengthFrequency = 2.7f;

    [Header("Drop Animation")]
    [SerializeField]
    [Tooltip("Range of time used for the drop animation.")]
    private Vector2 dropDurationRange = new Vector2(0.45f, 0.65f);

    [SerializeField]
    [Tooltip("Range for the peak height that the drop animation should reach.")]
    private Vector2 dropArcHeightRange = new Vector2(0.6f, 1.2f);

    [SerializeField]
    [Tooltip("Range for the rotation speed applied while the loot is falling.")]
    private Vector2 dropRotationSpeedRange = new Vector2(150f, 240f);

    [Header("Idle Animation")]
    [SerializeField]
    [Tooltip("Vertical bob amplitude once the drop animation finishes.")]
    private float idleBobAmplitude = 0.08f;

    [SerializeField]
    [Tooltip("Bob frequency applied once the drop animation finishes.")]
    private float idleBobFrequency = 2.5f;

    [SerializeField]
    [Tooltip("Rotation speed applied during idle.")]
    private float idleRotationSpeed = 40f;

    [SerializeField]
    [Tooltip("Enables floating/rotation idle animation once the loot has settled.")]
    private bool animateWhileIdle = true;

    [Header("Pickup Settings")]
    [SerializeField]
    [Tooltip("Distance at which the loot is automatically collected.")]
    private float autoPickupRange = 0.75f;

    [SerializeField]
    [Tooltip("Maximum distance at which the player can press the interact key to collect the loot.")]
    private float interactRange = 1.25f;

    [SerializeField]
    [Tooltip("Key used to manually pick up the loot when in range.")]
    private KeyCode interactKey = KeyCode.F;

    [Header("Gather Animation")]
    [SerializeField]
    [Tooltip("Duration of the gather animation that plays when the loot is collected.")]
    private float gatherDuration = 0.35f;

    [SerializeField]
    [Tooltip("Maximum height added to the gather animation's arc.")]
    private float gatherArcHeight = 0.6f;

    [SerializeField]
    [Tooltip("Rotation speed applied while the loot moves toward the player during the gather animation.")]
    private float gatherRotationSpeed = 540f;

    [SerializeField]
    [Tooltip("Offset applied to the player's position when targeting the gather animation endpoint.")]
    private Vector3 gatherTargetOffset = new Vector3(0f, 1f, 0f);

    [SerializeField]
    [Tooltip("Ease curve used to interpolate the gather animation movement.")]
    private AnimationCurve gatherEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private GearItem gearItem;
    private ResourceTypeDef resourceType;
    private int resourceAmount;
    private PlayerInventory inventory;
    private Transform playerTransform;
    private Vector3 dropStartPosition;
    private Vector3 dropEndPosition;
    private float dropDuration;
    private float dropElapsed;
    private float dropArcHeight;
    private float dropRotationSpeed;
    private int rotationDirection;
    private float idleTimeOffset;
    private bool isDropping;
    private bool isCollected;
    private bool hasRuntimeInitialization;
    private float nextInventorySearchTime;
    private float rarityBeamTimer;
    private bool rarityBeamActive;
    private Vector3 rarityBeamBaseScale = Vector3.one;
    private Quaternion rarityBeamBaseRotation = Quaternion.identity;
    private Vector3 rarityBeamBaseOffset = Vector3.zero;
    private Vector3 rarityBeamLocalOffset = Vector3.zero;
    private Quaternion initialIconRotation = Quaternion.identity;
    private bool lockIconRotation;
    private bool isGathering;
    private float gatherElapsed;
    private Vector3 gatherStartPosition;
    // Tracks whether loot pickup text has spawned to avoid duplicates (defensive; may be unused).
#pragma warning disable 0414
    private bool hasSpawnedLootText;
#pragma warning restore 0414

    private void Awake()
    {
        if (iconRenderer == null)
        {
            iconRenderer = GetComponent<SpriteRenderer>();
        }

        initialIconRotation = transform.rotation;

        if (rarityBeamRoot != null)
        {
            rarityBeamBaseScale = rarityBeamRoot.localScale;
            rarityBeamBaseRotation = rarityBeamRoot.rotation;
            rarityBeamBaseOffset = rarityBeamRoot.position - transform.position;
            rarityBeamLocalOffset = Quaternion.Inverse(transform.rotation) * rarityBeamBaseOffset;
            SetRarityBeamVisible(false);
        }

        if (rarityBeamRenderer == null && rarityBeamRoot != null)
        {
            rarityBeamRenderer = rarityBeamRoot.GetComponentInChildren<SpriteRenderer>();
        }

        ConfigureIconGlow();
        ConfigureGatherTrailInitialState();
    }

    private void OnEnable()
    {
        if (!hasRuntimeInitialization && initialItem != null)
        {
            Vector3 currentPosition = transform.position;
            Initialize(initialItem, null, currentPosition, playerTransform, animateOnEnableWithInitialItem);
        }
    }

    private void OnDisable()
    {
        InventoryUIController.TryClearTooltip(this);
    }

    private void Update()
    {
        if (!hasRuntimeInitialization)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (isGathering)
        {
            UpdateGatherAnimation(deltaTime);
            return;
        }

        if (isCollected)
        {
            return;
        }

        if (isDropping)
        {
            dropElapsed += deltaTime;
            float normalizedTime = Mathf.Clamp01(dropElapsed / Mathf.Max(0.0001f, dropDuration));
            Vector3 horizontalPosition = Vector3.Lerp(dropStartPosition, dropEndPosition, normalizedTime);
            float verticalOffset = 4f * dropArcHeight * normalizedTime * (1f - normalizedTime);
            transform.position = horizontalPosition + Vector3.up * verticalOffset;
            if (!lockIconRotation && !Mathf.Approximately(dropRotationSpeed, 0f))
            {
                ApplyIconRotation(dropRotationSpeed * rotationDirection * deltaTime);
            }
            MaintainChildAlignment();

            if (normalizedTime >= 1f)
            {
                isDropping = false;
                transform.position = dropEndPosition;
                dropStartPosition = dropEndPosition;
                rarityBeamTimer = 0f;
                rarityBeamActive = false;
                SetRarityBeamVisible(false);
                if (resetIconRotationAfterDrop)
                {
                    SetIconRotation(initialIconRotation);
                    lockIconRotation = true;
                }
                dropEndPosition = transform.position;
                dropStartPosition = transform.position;
                MaintainChildAlignment();
            }
        }
        else
        {
            if (animateWhileIdle)
            {
                float bobOffset = Mathf.Sin((Time.time + idleTimeOffset) * idleBobFrequency) * idleBobAmplitude;
                transform.position = dropEndPosition + Vector3.up * bobOffset;
                if (!lockIconRotation && !Mathf.Approximately(idleRotationSpeed, 0f))
                {
                    ApplyIconRotation(idleRotationSpeed * rotationDirection * deltaTime);
                }
            }
            else
            {
                transform.position = dropEndPosition;
            }
            UpdateRarityBeam(deltaTime);
            MaintainChildAlignment();
        }

        UpdatePickupState();
    }

    /// <summary>
    /// Configures the loot pickup with the specified <see cref="GearItem"/> and optionally the
    /// player inventory that should receive the item when collected.
    /// </summary>
    /// <param name="item">Gear item represented by this pickup.</param>
    /// <param name="targetInventory">Inventory that should receive the item when collected.</param>
    /// <param name="finalPosition">World position that the loot should settle at after the drop animation.</param>
    /// <param name="playerOverride">Optional player transform used for distance checks.</param>
    /// <param name="playDropAnimation">Determines whether the drop animation should play.</param>
    public void Initialize(GearItem item, PlayerInventory targetInventory, Vector3 finalPosition, Transform playerOverride = null, bool playDropAnimation = true)
    {
        gearItem = item;
        inventory = targetInventory;
        Transform resolvedPlayer = playerOverride != null ? playerOverride : targetInventory != null ? targetInventory.transform : playerTransform;
        playerTransform = resolvedPlayer;
        SetIconRotation(initialIconRotation);
        dropStartPosition = transform.position;
        dropEndPosition = finalPosition;
        dropDuration = Mathf.Max(0.05f, RandomRange(dropDurationRange, 0.55f));
        dropArcHeight = Mathf.Max(0.05f, RandomRange(dropArcHeightRange, 0.8f));
        dropRotationSpeed = Mathf.Abs(RandomRange(dropRotationSpeedRange, 180f));
        rotationDirection = Random.value > 0.5f ? 1 : -1;
        idleTimeOffset = Random.Range(0f, Mathf.PI * 2f);
        isDropping = playDropAnimation;
        lockIconRotation = resetIconRotationAfterDrop && !isDropping;
        dropElapsed = 0f;
        hasRuntimeInitialization = true;
        isCollected = false;
        isGathering = false;
        gatherElapsed = 0f;
        nextInventorySearchTime = 0f;
        hasSpawnedLootText = false;
        SetupIcon();
        if (!isDropping)
        {
            transform.position = dropEndPosition;
            dropStartPosition = dropEndPosition;
            MaintainChildAlignment();
        }
        else
        {
            transform.position = dropStartPosition;
            MaintainChildAlignment();
        }
    }

    private void UpdatePickupState()
    {
        if (isDropping || isGathering || (gearItem == null && resourceType == null))
        {
            return;
        }

        ResolveInventory();
        ResolvePlayerTransform();
        if (playerTransform == null)
        {
            return;
        }

        float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;
        float autoRange = Mathf.Max(0f, autoPickupRange);
        float interactRangeValue = Mathf.Max(autoRange, interactRange);
        bool withinInteractRange = interactRangeValue > 0f && sqrDistance <= interactRangeValue * interactRangeValue;

        if (autoRange > 0f && sqrDistance <= autoRange * autoRange)
        {
            TryCollect();
            return;
        }

        if (withinInteractRange && Input.GetKeyDown(interactKey))
        {
            TryCollect();
        }
    }

    public void InitializeResource(ResourceTypeDef type, int amount, Transform playerOverride = null, bool playDropAnimation = true)
    {
        gearItem = null;
        resourceType = type;
        resourceAmount = Mathf.Max(0, amount);
        inventory = null;
        Transform resolvedPlayer = playerOverride != null ? playerOverride : playerTransform;
        playerTransform = resolvedPlayer;
        SetIconRotation(initialIconRotation);
        dropStartPosition = transform.position;
        dropEndPosition = transform.position;
        dropDuration = Mathf.Max(0.05f, RandomRange(dropDurationRange, 0.55f));
        dropArcHeight = Mathf.Max(0.05f, RandomRange(dropArcHeightRange, 0.8f));
        dropRotationSpeed = Mathf.Abs(RandomRange(dropRotationSpeedRange, 180f));
        rotationDirection = Random.value > 0.5f ? 1 : -1;
        idleTimeOffset = Random.Range(0f, Mathf.PI * 2f);
        isDropping = playDropAnimation;
        lockIconRotation = resetIconRotationAfterDrop && !isDropping;
        dropElapsed = 0f;
        hasRuntimeInitialization = true;
        isCollected = false;
        isGathering = false;
        gatherElapsed = 0f;
        nextInventorySearchTime = 0f;
        hasSpawnedLootText = false;
        SetupIcon();
        if (!isDropping)
        {
            transform.position = dropEndPosition;
            dropStartPosition = dropEndPosition;
            MaintainChildAlignment();
        }
        else
        {
            transform.position = dropStartPosition;
            MaintainChildAlignment();
        }
    }

    private void TryCollect()
    {
        if (isGathering || isCollected || (gearItem == null && resourceType == null))
        {
            return;
        }

        ResolveInventory();
        if (playerTransform == null)
        {
            return;
        }

        InventoryUIController.TryClearTooltip(this);
        StartGatherAnimation();
    }

    private void ResolvePlayerTransform()
    {
        if (playerTransform != null)
        {
            return;
        }

        if (inventory != null)
        {
            playerTransform = inventory.transform;
            return;
        }

        if (PlayerStats.Instance != null)
        {
            playerTransform = PlayerStats.Instance.transform;
            return;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (Time.time < nextInventorySearchTime)
        {
            return;
        }

        var foundInventory = FindFirstObjectByType<PlayerInventory>();
        if (foundInventory != null)
        {
            inventory = foundInventory;
            playerTransform = foundInventory.transform;
        }

        nextInventorySearchTime = Time.time + 1f;
    }

    private void ResolveInventory()
    {
        if (inventory == null && playerTransform != null)
        {
            inventory = playerTransform.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = playerTransform.GetComponentInChildren<PlayerInventory>();
            }
        }

        if (inventory != null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (Time.time < nextInventorySearchTime)
        {
            return;
        }

        inventory = FindFirstObjectByType<PlayerInventory>();
        nextInventorySearchTime = Time.time + 1f;
    }

    private void SetupIcon()
    {
        if (iconRenderer == null)
        {
            return;
        }

        if (gearItem == null && resourceType == null)
        {
            iconRenderer.sprite = null;
            iconRenderer.color = Color.white;
            ConfigureRarityBeam();
            return;
        }

        if (gearItem != null)
        {
            bool hasWorldIcon = gearItem.WorldIcon != null;
            Sprite displaySprite = hasWorldIcon ? gearItem.WorldIcon : gearItem.Icon;
            iconRenderer.sprite = displaySprite;
            // If falling back to the regular UI icon, don't tint the world loot sprite.
            iconRenderer.color = hasWorldIcon ? gearItem.SpriteColor : Color.white;
        }
        else
        {
            iconRenderer.sprite = resourceType != null ? resourceType.Icon : null;
            iconRenderer.color = Color.white;
        }
        ConfigureRarityBeam();
        ConfigureIconGlow();
    }

    private void ConfigureRarityBeam()
    {
        rarityBeamTimer = 0f;
        rarityBeamActive = false;

        if (rarityBeamRenderer != null)
        {
            Color beamColor = Color.white;
            if (gearItem != null)
            {
                beamColor = gearItem.RarityColor;
            }
            else if (resourceType != null)
            {
                beamColor = resourceType.RarityColor;
            }
            rarityBeamRenderer.color = beamColor;
        }

        if (gearItem == null && resourceType == null)
        {
            SetRarityBeamVisible(false);
            return;
        }

        SetRarityBeamVisible(false);
    }

    private bool TryGetRarityColor(out Color color, out bool isCommon)
    {
        if (gearItem != null)
        {
            color = gearItem.RarityColor;
            isCommon = gearItem.Rarity == GearRarity.Common;
            return true;
        }

        if (resourceType != null)
        {
            color = resourceType.RarityColor;
            isCommon = resourceType.Rarity == GearRarity.Common;
            return true;
        }

        color = Color.white;
        isCommon = true;
        return false;
    }

    private void ConfigureIconGlow()
    {
        if (iconGlowRenderer == null)
        {
            return;
        }

        Sprite sourceSprite = iconRenderer != null ? iconRenderer.sprite : null;
        iconGlowRenderer.sprite = sourceSprite;

        if (!TryGetRarityColor(out Color glowColor, out bool isCommon) || isCommon || sourceSprite == null)
        {
            iconGlowRenderer.enabled = false;
            return;
        }

        float alpha = Mathf.Clamp01(iconGlowAlpha);
        glowColor.a = alpha;

        if (iconRenderer != null)
        {
            iconGlowRenderer.sortingLayerID = iconRenderer.sortingLayerID;
            iconGlowRenderer.sortingOrder = iconRenderer.sortingOrder - 1;
        }

        iconGlowRenderer.color = glowColor;
        iconGlowRenderer.enabled = true;
    }

    private void ConfigureGatherTrailInitialState()
    {
        if (gatherTrailRenderer == null)
        {
            return;
        }

        gatherTrailRenderer.emitting = false;
        gatherTrailRenderer.enabled = false;
        gatherTrailRenderer.Clear();
    }

    private void ActivateGatherTrail()
    {
        if (gatherTrailRenderer == null)
        {
            return;
        }

        if (!TryGetRarityColor(out Color trailColor, out bool isCommon) || isCommon)
        {
            gatherTrailRenderer.emitting = false;
            if (gatherTrailRenderer.enabled)
            {
                gatherTrailRenderer.Clear();
            }
            gatherTrailRenderer.enabled = false;
            return;
        }

        float alpha = Mathf.Clamp01(gatherTrailAlpha);
        trailColor.a = alpha;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });

        gatherTrailRenderer.colorGradient = gradient;
        gatherTrailRenderer.Clear();
        gatherTrailRenderer.enabled = true;
        gatherTrailRenderer.emitting = true;
    }

    private void DeactivateGatherTrail()
    {
        if (gatherTrailRenderer == null)
        {
            return;
        }

        gatherTrailRenderer.emitting = false;
        if (gatherTrailRenderer.enabled)
        {
            gatherTrailRenderer.Clear();
        }
        gatherTrailRenderer.enabled = false;
    }

    private void UpdateRarityBeam(float deltaTime)
    {
        if (rarityBeamRoot == null)
        {
            return;
        }

        rarityBeamTimer += deltaTime;

        if (!rarityBeamActive)
        {
            if (rarityBeamTimer < Mathf.Max(0f, rarityBeamDelay))
            {
                return;
            }

            rarityBeamActive = true;
            SetRarityBeamVisible(true);
        }

        float time = Time.time;
        float scalePulse = 1f + Mathf.Sin(time * rarityBeamScaleFrequency) * rarityBeamScaleAmplitude;
        float lengthPulse = 1f + Mathf.Sin(time * rarityBeamLengthFrequency + 1.1f) * rarityBeamLengthAmplitude;
        Vector3 targetScale = new Vector3(rarityBeamBaseScale.x * scalePulse, rarityBeamBaseScale.y * lengthPulse, rarityBeamBaseScale.z);
        rarityBeamRoot.localScale = targetScale;
    }

    private void SetRarityBeamVisible(bool visible)
    {
        if (rarityBeamRoot == null)
        {
            return;
        }

        rarityBeamRoot.position = transform.position + GetRarityBeamWorldOffset();
        rarityBeamRoot.rotation = rarityBeamBaseRotation;

        if (rarityBeamRoot.gameObject.activeSelf != visible)
        {
            rarityBeamRoot.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            rarityBeamRoot.localScale = rarityBeamBaseScale;
            rarityBeamRoot.rotation = rarityBeamBaseRotation;
            rarityBeamRoot.position = transform.position + GetRarityBeamWorldOffset();
        }
    }

    /// <inheritdoc />
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isCollected || (gearItem == null && resourceType == null))
        {
            return;
        }
        if (gearItem != null)
        {
            InventoryUIController.TryRequestTooltip(gearItem, this);
        }
        else if (resourceType != null)
        {
            InventoryUIController.TryRequestResourceTooltip(resourceType, this);
        }
    }

    /// <inheritdoc />
    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryUIController.TryClearTooltip(this);
    }

    /// <inheritdoc />
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isCollected || (gearItem == null && resourceType == null))
        {
            return;
        }

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (IsWithinManualPickupRange())
        {
            TryCollect();
        }
    }

    private void MaintainChildAlignment()
    {
        if (rarityBeamRoot != null)
        {
            rarityBeamRoot.rotation = rarityBeamBaseRotation;
            rarityBeamRoot.position = transform.position + GetRarityBeamWorldOffset();
        }
    }

    private Vector3 GetRarityBeamWorldOffset()
    {
        if (rarityBeamRoot == null)
        {
            return Vector3.zero;
        }

        return transform.rotation * (rarityBeamLocalOffset + rarityBeamManualOffset);
    }

    private Vector3 GetIconPivotWorldPosition()
    {
        return transform.position + GetRarityBeamWorldOffset();
    }

    private void ApplyIconRotation(float deltaAngle)
    {
        if (Mathf.Approximately(deltaAngle, 0f))
        {
            return;
        }

        Vector3 pivotBefore = GetIconPivotWorldPosition();
        transform.Rotate(0f, 0f, deltaAngle);
        Vector3 pivotAfter = GetIconPivotWorldPosition();
        transform.position += pivotBefore - pivotAfter;
    }

    private void SetIconRotation(Quaternion targetRotation)
    {
        Vector3 pivotBefore = GetIconPivotWorldPosition();
        transform.rotation = targetRotation;
        Vector3 pivotAfter = GetIconPivotWorldPosition();
        transform.position += pivotBefore - pivotAfter;
    }

    private void OnValidate()
    {
        if (rarityBeamRoot != null && rarityBeamRenderer == null)
        {
            rarityBeamRenderer = rarityBeamRoot.GetComponentInChildren<SpriteRenderer>();
        }

        if (rarityBeamRoot != null)
        {
            rarityBeamBaseScale = rarityBeamRoot.localScale;
            rarityBeamBaseRotation = rarityBeamRoot.rotation;
            rarityBeamBaseOffset = rarityBeamRoot.position - transform.position;
            rarityBeamLocalOffset = Quaternion.Inverse(transform.rotation) * rarityBeamBaseOffset;
        }

        initialIconRotation = transform.rotation;
        MaintainChildAlignment();
        ConfigureIconGlow();
        ConfigureGatherTrailInitialState();
    }

    private void StartGatherAnimation()
    {
        isDropping = false;
        isGathering = true;
        gatherElapsed = 0f;
        gatherStartPosition = transform.position;
        SetRarityBeamVisible(false);
        DisableColliders();
        ActivateGatherTrail();
        MaintainChildAlignment();
    }

    private void UpdateGatherAnimation(float deltaTime)
    {
        if (isCollected)
        {
            return;
        }

        ResolveInventory();
        if (playerTransform == null)
        {
            CompleteCollection();
            return;
        }

        gatherElapsed += deltaTime;
        float duration = Mathf.Max(0.0001f, gatherDuration);
        float normalizedTime = Mathf.Clamp01(gatherElapsed / duration);
        float easedTime = gatherEase != null && gatherEase.length > 0 ? Mathf.Clamp01(gatherEase.Evaluate(normalizedTime)) : normalizedTime;
        Vector3 targetPosition = playerTransform.position + gatherTargetOffset;
        Vector3 nextPosition = Vector3.Lerp(gatherStartPosition, targetPosition, easedTime);

        if (gatherArcHeight > 0f)
        {
            float arcOffset = Mathf.Sin(easedTime * Mathf.PI) * gatherArcHeight;
            nextPosition += Vector3.up * arcOffset;
        }

        transform.position = nextPosition;

        if (!lockIconRotation && !Mathf.Approximately(gatherRotationSpeed, 0f))
        {
            ApplyIconRotation(gatherRotationSpeed * deltaTime);
        }

        MaintainChildAlignment();

        if (normalizedTime >= 1f)
        {
            CompleteCollection();
        }
    }

    private void CompleteCollection()
    {
        if (isCollected)
        {
            return;
        }

        isGathering = false;
        isCollected = true;
        DeactivateGatherTrail();

        // Add to inventory (item) or grant to resources at the end of the gather animation.
        if (gearItem != null)
        {
            if (inventory != null)
            {
                inventory.Add(gearItem);
            }

            if (CombatTextManager.Instance != null)
            {
                Vector3 sourcePosition = playerTransform != null ? playerTransform.position : transform.position;
                Vector3 textPosition = sourcePosition + Vector3.up * 0.6f;
                CombatTextManager.Instance.SpawnLootPickup(gearItem.DisplayName, gearItem.RarityColor, textPosition);
            }
        }
        else if (resourceType != null && resourceAmount > 0)
        {
            if (DynamicResourceManager.Instance != null)
            {
                var grant = new ResourceSet();
                grant.Set(resourceType, resourceAmount);
                Vector3 sourcePosition = playerTransform != null ? playerTransform.position : transform.position;
                DynamicResourceManager.Instance.GrantResources(grant, sourcePosition, showFeedback: true);
            }
        }

        Destroy(gameObject);
    }

    private bool IsWithinManualPickupRange()
    {
        ResolveInventory();
        if (playerTransform == null)
        {
            return false;
        }

        float manualRange = Mathf.Max(Mathf.Max(0f, autoPickupRange), interactRange);
        if (manualRange <= 0f)
        {
            return false;
        }

        float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;
        return sqrDistance <= manualRange * manualRange;
    }

    private void DisableColliders()
    {
        if (TryGetComponent(out Collider2D collider2D))
        {
            collider2D.enabled = false;
        }

        if (TryGetComponent(out Collider collider3D))
        {
            collider3D.enabled = false;
        }
    }

    private static float RandomRange(Vector2 range, float fallback)
    {
        if (range.x == 0f && range.y == 0f)
        {
            return fallback;
        }

        float min = Mathf.Min(range.x, range.y);
        float max = Mathf.Max(range.x, range.y);
        if (Mathf.Approximately(min, max))
        {
            return min;
        }

        return Random.Range(min, max);
    }
}




}






