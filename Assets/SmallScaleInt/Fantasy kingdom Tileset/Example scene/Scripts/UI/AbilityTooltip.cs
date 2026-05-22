using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

/// <summary>
/// Centralized tooltip system for displaying ability information.
/// Shows ability name, icon, and description when hovering over skill nodes or ability slots.
/// </summary>
[MovedFrom(true, null, null, "AbilityTooltip")]
public class AbilityTooltip : MonoBehaviour
{
    public static AbilityTooltip Instance { get; private set; }

    [Header("UI References")]
    [SerializeField]
    private GameObject tooltipPanel;

    [SerializeField]
    private TextMeshProUGUI nameText;

    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TextMeshProUGUI descriptionText;

    [SerializeField]
    private TextMeshProUGUI statsText;

    [SerializeField]
    private TextMeshProUGUI manaCostText;

    [Header("Settings")]
    [SerializeField]
    private float showDelay = 0.3f;

    [SerializeField]
    [Tooltip("If true, tooltip follows the mouse cursor. If false, tooltip stays at a fixed position.")]
    private bool followMouse = true;

    [SerializeField]
    [Tooltip("Offset from mouse position when followMouse is enabled (in screen space pixels). Use positive X/Y to move right/up.")]
    private Vector2 mouseOffset = new Vector2(15f, -15f);

    [SerializeField]
    [Tooltip("Fixed anchored position for the tooltip when followMouse is disabled.")]
    private Vector2 fixedPosition = new Vector2(100f, -100f);

    [Header("Passive Ability Style")]
    [SerializeField]
    [Tooltip("Color used for the 'Passive' text.")]
    private Color passiveColor = new Color(0.4f, 1f, 0.5f, 1f);

    [SerializeField]
    [Tooltip("Color used for the mana cost text.")]
    private Color manaCostColor = Color.white;

    private RectTransform tooltipRect;
    private Canvas parentCanvas;
    private CanvasScaler parentCanvasScaler;
    private float showTimer;
    private bool isShowing;
    private AbilityDefinition currentAbility;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas)
        {
            parentCanvasScaler = parentCanvas.GetComponentInParent<CanvasScaler>();
        }

        if (tooltipPanel)
        {
            tooltipPanel.SetActive(false);
        }
    }

    void OnDisable()
    {
        // Ensure tooltip is hidden when component is disabled
        Hide();
    }

    void Update()
    {
        if (isShowing && currentAbility != null)
        {
            showTimer += Time.unscaledDeltaTime;

            if (showTimer >= showDelay && !tooltipPanel.activeSelf)
            {
                tooltipPanel.SetActive(true);
            }

            if (followMouse)
            {
                UpdatePosition(Input.mousePosition);
            }
        }
    }

    /// <summary>
    /// Shows tooltip for the given ability at the mouse position.
    /// </summary>
    public void Show(AbilityDefinition ability)
    {
        if (ability == null)
        {
            Hide();
            return;
        }

        // If we're already showing the same ability, don't reset the timer or content
        bool isSameAbility = currentAbility == ability;

        currentAbility = ability;
        isShowing = true;

        // Only reset timer if this is a new ability
        if (!isSameAbility)
        {
            showTimer = 0f;
        }

        // Update content (always update in case ability data changed)
        bool isPassive = ability.IsPassive;

        if (nameText)
        {
            // Make title italic for passive abilities
            if (isPassive)
            {
                nameText.text = $"<i>{ability.DisplayName}</i>";
            }
            else
            {
                nameText.text = ability.DisplayName;
            }
        }

        if (iconImage)
        {
            iconImage.sprite = ability.Icon;
            iconImage.enabled = ability.Icon != null;
        }

        if (descriptionText)
        {
            descriptionText.text = string.IsNullOrWhiteSpace(ability.Description)
                ? "<i>No description available</i>"
                : ability.Description;
        }

        if (statsText)
        {
            statsText.text = BuildStatsText(ability);
        }

        if (manaCostText)
        {
            if (isPassive)
            {
                // Show "Passive" for passive abilities
                manaCostText.gameObject.SetActive(true);
                manaCostText.text = "Passive";
                manaCostText.color = passiveColor;
            }
            else if (ability.ManaCost > 0)
            {
                // Show mana cost for active abilities
                manaCostText.gameObject.SetActive(true);
                manaCostText.text = $"Mana: {ability.ManaCost}";
                manaCostText.color = manaCostColor; // Use configured mana cost color
            }
            else
            {
                // Hide for free abilities with no mana cost
                manaCostText.gameObject.SetActive(false);
            }
        }

        UpdatePosition(Input.mousePosition);
    }

    /// <summary>
    /// Hides the tooltip immediately.
    /// </summary>
    public void Hide()
    {
        isShowing = false;
        showTimer = 0f;
        currentAbility = null;

        if (tooltipPanel)
        {
            tooltipPanel.SetActive(false);
        }
    }

    void UpdatePosition(Vector3 mousePosition)
    {
        if (!tooltipRect) return;

        if (followMouse)
        {
            float scale = ResolveScaleFactor();
            Vector2 offset = new Vector2(mouseOffset.x * scale, mouseOffset.y * scale);
            Vector2 screenPoint = new Vector2(mousePosition.x, mousePosition.y) + offset;

            RectTransform parentRect = tooltipRect.parent as RectTransform;
            Camera canvasCamera = (parentCanvas && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? parentCanvas.worldCamera
                : null;

            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, canvasCamera, out Vector2 localPoint))
            {
                tooltipRect.anchoredPosition = localPoint;
            }
            else
            {
                tooltipRect.position = mousePosition + new Vector3(offset.x, offset.y, 0f);
            }
        }
        else
        {
            // Use fixed anchored position
            tooltipRect.anchoredPosition = fixedPosition;
        }
    }

    float ResolveScaleFactor()
    {
        if (!parentCanvas)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (!parentCanvasScaler && parentCanvas)
        {
            parentCanvasScaler = parentCanvas.GetComponentInParent<CanvasScaler>();
        }

        if (!parentCanvasScaler)
        {
            return parentCanvas ? Mathf.Max(0.0001f, parentCanvas.scaleFactor) : 1f;
        }

        switch (parentCanvasScaler.uiScaleMode)
        {
            case CanvasScaler.ScaleMode.ConstantPixelSize:
                return Mathf.Max(0.0001f, parentCanvasScaler.scaleFactor);

            case CanvasScaler.ScaleMode.ScaleWithScreenSize:
                {
                    Vector2 reference = parentCanvasScaler.referenceResolution;
                    if (reference.x <= 0f || reference.y <= 0f)
                        return 1f;

                    float widthRatio = Screen.width / reference.x;
                    float heightRatio = Screen.height / reference.y;

                    float logWidth = Mathf.Log(widthRatio, 2f);
                    float logHeight = Mathf.Log(heightRatio, 2f);
                    float scale = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, parentCanvasScaler.matchWidthOrHeight));
                    return Mathf.Max(0.0001f, scale);
                }

            case CanvasScaler.ScaleMode.ConstantPhysicalSize:
                return Mathf.Max(0.0001f, parentCanvasScaler.scaleFactor);

            default:
                return Mathf.Max(0.0001f, parentCanvasScaler.scaleFactor);
        }
    }

    string BuildStatsText(AbilityDefinition ability)
    {
        if (ability == null) return "";

        var stats = new System.Collections.Generic.List<string>();

        if (ability.ManaCost > 0)
        {
            stats.Add($"<color=#4AF>Mana: {ability.ManaCost}</color>");
        }

        if (ability.CooldownSeconds > 0)
        {
            stats.Add($"<color=#AAA>Cooldown: {ability.CooldownSeconds:0.#}s</color>");
        }

        if (ability.MaxCharges > 0)
        {
            stats.Add($"<color=#FA4>Charges: {ability.MaxCharges}</color>");
        }

        if (ability.IsPassive)
        {
            stats.Add("<color=#4F4>PASSIVE</color>");
        }

        return stats.Count > 0 ? string.Join(" | ", stats) : "";
    }
}



}





