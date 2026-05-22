using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SmallScale.FantasyKingdomTileset;

namespace SkillSystem
{
    /// <summary>
    /// Represents a single ability entry in the spell book.
    /// Reuses the SkillNodeView prefab but configured for spell book display.
    /// Supports drag-and-drop to ability slots and shows ability info on hover.
    /// </summary>
    public class SpellBookAbilityButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References (from SkillNodeView prefab)")]
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TextMeshProUGUI titleLabel;

        [SerializeField]
        private TextMeshProUGUI rankLabel;

        [SerializeField]
        private GameObject unlockedMarker;

        [SerializeField]
        private GameObject lockedMarker;

        [SerializeField]
        private Image backgroundImage;

        [SerializeField]
        private Image unlockedOverlay;

        [Header("Ability Type Colors")]
        [SerializeField]
        [Tooltip("Color tint for active abilities.")]
        private Color activeAbilityColor = new Color(1f, 0.8f, 0.4f, 1f);

        [SerializeField]
        [Tooltip("Color tint for passive abilities.")]
        private Color passiveAbilityColor = new Color(0.6f, 0.8f, 1f, 1f);

        [SerializeField]
        [Tooltip("Color for cleared/empty slots.")]
        private Color defaultColor = new Color(0.8f, 0.8f, 0.8f, 1f);

        private AbilityDefinition ability;
        private int currentRank;
        private bool dragging;

        public AbilityDefinition Ability => ability;

        void Awake()
        {
            // Disable button component (we handle drag-and-drop directly)
            if (button)
            {
                button.interactable = false;
                button.enabled = false;
            }

            // Hide lock/unlock markers (not needed in spell book)
            if (unlockedMarker)
            {
                unlockedMarker.SetActive(false);
            }

            if (lockedMarker)
            {
                lockedMarker.SetActive(false);
            }
        }

        public void Setup(AbilityDefinition abilityDefinition, int rank = 1)
        {
            ability = abilityDefinition;
            currentRank = rank;

            if (!ability)
            {
                Clear();
                return;
            }

            // Set icon
            if (iconImage)
            {
                iconImage.sprite = ability.Icon;
                iconImage.enabled = ability.Icon != null;
            }

            // Set name
            if (titleLabel)
            {
                titleLabel.text = ability.DisplayName;
            }

            // Set rank
            if (rankLabel)
            {
                rankLabel.text = rank.ToString();
            }

            // Apply color based on ability type
            ApplyAbilityTypeColor();
        }

        private void ApplyAbilityTypeColor()
        {
            if (!ability) return;

            bool isPassive = ability.IsPassive;
            Color targetColor = isPassive ? passiveAbilityColor : activeAbilityColor;

            // Apply color to background
            if (backgroundImage)
            {
                backgroundImage.color = targetColor;
            }

            // Apply color to unlocked overlay
            if (unlockedOverlay)
            {
                unlockedOverlay.color = targetColor;
            }
        }

        public void Clear()
        {
            ability = null;
            currentRank = 0;

            if (iconImage)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (titleLabel)
            {
                titleLabel.text = string.Empty;
            }

            if (rankLabel)
            {
                rankLabel.text = string.Empty;
            }

            if (backgroundImage)
            {
                backgroundImage.color = defaultColor;
            }

            if (unlockedOverlay)
            {
                unlockedOverlay.color = defaultColor;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Don't allow dragging passive abilities (they can't be equipped to slots)
            if (!ability || ability.IsPassive) return;

            dragging = true;

            // Start drag using the drag-drop service
            AbilityDragDropService.BeginDrag(ability, ability.Icon);
            AbilityDragDropService.UpdatePosition(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;

            AbilityDragDropService.UpdatePosition(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;

            dragging = false;
            AbilityDragDropService.EndDrag();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!ability) return;

            // Show tooltip
            if (AbilityTooltip.Instance)
            {
                AbilityTooltip.Instance.Show(ability);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Hide tooltip
            if (AbilityTooltip.Instance)
            {
                AbilityTooltip.Instance.Hide();
            }
        }
    }
}







