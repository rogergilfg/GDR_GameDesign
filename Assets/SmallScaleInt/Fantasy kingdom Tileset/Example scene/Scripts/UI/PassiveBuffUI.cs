using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

/// <summary>
/// Controls a single passive buff icon in the passive buff panel.
/// Shows icon, stack count, countdown overlay, and tooltip on hover.
/// </summary>
[MovedFrom(true, null, null, "PassiveBuffUI")]
public class PassiveBuffUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image countdownOverlay;
    [SerializeField] private TextMeshProUGUI stackCountText;
    [SerializeField] private GameObject stackCountObject;
    [SerializeField] private Image buffFrame;

    [Header("Frame Colors")]
    [SerializeField] private Color debuffFrameColor = new Color(1f, 0f, 0f, 1f); // Red
    [SerializeField] private Color buffFrameColor = new Color(0f, 1f, 0f, 1f); // Green

    private AbilityDefinition _ability;
    private ProcTriggerModifier _procTrigger;
    private float _duration;
    private float _endTime;
    private bool _hasCountdown;
    private bool _isActive = true;

    public AbilityDefinition Ability => _ability;
    public bool IsActive => _isActive;

    void Reset()
    {
        // Auto-find references if not set
        if (!iconImage)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform) iconImage = iconTransform.GetComponent<Image>();
        }

        if (!countdownOverlay)
        {
            Transform overlayTransform = transform.Find("CountdownOverlay");
            if (overlayTransform) countdownOverlay = overlayTransform.GetComponent<Image>();
        }

        if (!stackCountText)
        {
            Transform stackTransform = transform.Find("StackCount");
            if (stackTransform) stackCountText = stackTransform.GetComponent<TextMeshProUGUI>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_ability != null && AbilityTooltip.Instance != null)
        {
            AbilityTooltip.Instance.Show(_ability);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (AbilityTooltip.Instance != null)
        {
            AbilityTooltip.Instance.Hide();
        }
    }

    void Update()
    {
        if (_hasCountdown && _isActive && _endTime > 0f)
        {
            float remaining = _endTime - Time.time;
            if (remaining <= 0f)
            {
                // Duration expired, mark as inactive so controller removes it
                UpdateCountdownFill(0f);
                _isActive = false;
            }
            else
            {
                float fillAmount = remaining / _duration;
                UpdateCountdownFill(fillAmount);
            }
        }
    }

    /// <summary>
    /// Initialize this buff UI for a permanent passive (no proc trigger).
    /// </summary>
    public void Initialize(AbilityDefinition ability)
    {
        _ability = ability;
        _procTrigger = null;
        _hasCountdown = false;
        _isActive = true;

        if (iconImage && ability.Icon)
        {
            iconImage.sprite = ability.Icon;
            iconImage.enabled = true;
        }

        // Hide countdown overlay for permanent passives
        if (countdownOverlay)
        {
            countdownOverlay.gameObject.SetActive(false);
        }

        // Hide stack count for permanent passives
        if (stackCountObject)
        {
            stackCountObject.SetActive(false);
        }
        else if (stackCountText)
        {
            stackCountText.gameObject.SetActive(false);
        }

        // Show frame with appropriate color
        SetFrameColor(ability.IsDebuff);
    }

    /// <summary>
    /// Initialize this buff UI for a proc-based passive with countdown and stacks.
    /// </summary>
    public void Initialize(AbilityDefinition ability, ProcTriggerModifier procTrigger, float duration, float endTime, int currentStacks)
    {
        _ability = ability;
        _procTrigger = procTrigger;
        _duration = duration;
        _endTime = endTime;
        _hasCountdown = duration > 0f;
        _isActive = true;

        if (iconImage && ability.Icon)
        {
            iconImage.sprite = ability.Icon;
            iconImage.enabled = true;
        }

        // Setup countdown overlay
        if (countdownOverlay)
        {
            countdownOverlay.gameObject.SetActive(_hasCountdown);
            if (_hasCountdown)
            {
                countdownOverlay.type = Image.Type.Filled;
                countdownOverlay.fillMethod = Image.FillMethod.Radial360;
                countdownOverlay.fillOrigin = (int)Image.Origin360.Top;
                UpdateCountdownFill(1f);
            }
        }

        // Setup stack count
        UpdateStackCount(currentStacks);

        // Show frame with appropriate color
        SetFrameColor(ability.IsDebuff);
    }

    /// <summary>
    /// Initialize this buff UI for a debuff (non-proc passive with duration).
    /// </summary>
    public void InitializeDebuff(AbilityDefinition ability, float duration, float endTime, bool isDebuff)
    {
        _ability = ability;
        _procTrigger = null;
        _duration = duration;
        _endTime = endTime;
        _hasCountdown = duration > 0f;
        _isActive = true;

        if (iconImage && ability.Icon)
        {
            iconImage.sprite = ability.Icon;
            iconImage.enabled = true;
        }

        // Setup countdown overlay
        if (countdownOverlay)
        {
            countdownOverlay.gameObject.SetActive(_hasCountdown);
            if (_hasCountdown)
            {
                countdownOverlay.type = Image.Type.Filled;
                countdownOverlay.fillMethod = Image.FillMethod.Radial360;
                countdownOverlay.fillOrigin = (int)Image.Origin360.Top;
                UpdateCountdownFill(1f);
            }
        }

        // Hide stack count for debuffs (debuffs don't use stacking system currently)
        if (stackCountObject)
        {
            stackCountObject.SetActive(false);
        }
        else if (stackCountText)
        {
            stackCountText.gameObject.SetActive(false);
        }

        // Show frame with appropriate color
        SetFrameColor(ability.IsDebuff);
    }

    /// <summary>
    /// Update the stack count display.
    /// </summary>
    public void UpdateStackCount(int stacks)
    {
        bool showStacks = _procTrigger != null && stacks > 1;

        if (stackCountObject)
        {
            stackCountObject.SetActive(showStacks);
        }

        if (stackCountText)
        {
            if (showStacks)
            {
                stackCountText.text = stacks.ToString();
                stackCountText.gameObject.SetActive(true);
            }
            else
            {
                stackCountText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Update the countdown fill amount (1 = full, 0 = empty).
    /// </summary>
    public void UpdateCountdownFill(float fillAmount)
    {
        if (countdownOverlay && _hasCountdown)
        {
            countdownOverlay.fillAmount = Mathf.Clamp01(fillAmount);
        }
    }

    /// <summary>
    /// Refresh the duration (when proc refreshes).
    /// </summary>
    public void RefreshDuration(float newEndTime)
    {
        _endTime = newEndTime;
        UpdateCountdownFill(1f);
    }

    /// <summary>
    /// Update the end time (for non-refreshing stacks).
    /// </summary>
    public void UpdateEndTime(float newEndTime)
    {
        // Use the latest end time
        if (newEndTime > _endTime)
        {
            _endTime = newEndTime;
        }
    }

    /// <summary>
    /// Mark this buff as inactive (will be removed).
    /// </summary>
    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// Set the frame color based on whether this is a debuff or buff.
    /// </summary>
    private void SetFrameColor(bool isDebuff)
    {
        if (buffFrame)
        {
            buffFrame.gameObject.SetActive(true);
            buffFrame.color = isDebuff ? debuffFrameColor : buffFrameColor;
        }
    }
}


}




