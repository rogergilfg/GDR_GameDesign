using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SmallScale.FantasyKingdomTileset;

namespace SkillSystem
{
    public sealed class SkillNodeView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TextMeshProUGUI titleLabel;

        [SerializeField]
        private TextMeshProUGUI descriptionLabel;

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

        private SkillManager manager;
        private SkillTreeDefinition tree;
        private SkillNodeDefinition node;
        private int currentRank;
        private int currentMaxRank = 1;
        private bool isUnlocked;
        private bool suppressNextClick;
        private bool dragging;
        private bool isHovering;
        private int lastTooltipRank = -1;

        public RectTransform RectTransform => transform as RectTransform;
        public SkillNodeDefinition NodeDefinition => node;
        public AbilityDefinition GrantedAbility => GetActiveAbility();

        void Awake()
        {
            if (button)
            {
                button.interactable = false;
            }
        }

        public void Bind(SkillManager skillManager, SkillTreeDefinition definition, SkillNodeDefinition nodeDefinition)
        {
            manager = skillManager;
            tree = definition;
            node = nodeDefinition;

            if (iconImage)
            {
                iconImage.sprite = node.Icon;
                iconImage.enabled = node.Icon != null;
            }

            if (titleLabel)
            {
                titleLabel.text = node.DisplayName;
            }

            if (descriptionLabel)
            {
                // Get description from the first ability
                AbilityDefinition firstAbility = node.GetAbilityForRank(1);
                descriptionLabel.text = firstAbility != null ? firstAbility.Description : string.Empty;
            }

            // Apply color based on ability type
            ApplyAbilityTypeColor();
        }

        private void ApplyAbilityTypeColor()
        {
            if (node == null) return;

            // Check if this is a passive ability
            AbilityDefinition ability = node.GetAbilityForRank(1);
            bool isPassive = ability != null && ability.IsPassive;

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

        public void Refresh(int rank, int maxRank, bool canInvest, bool meetsLevel, bool prerequisitesMet)
        {
            currentMaxRank = Mathf.Max(1, maxRank);
            currentRank = Mathf.Clamp(rank, 0, currentMaxRank);
            isUnlocked = currentRank > 0;

            if (lockedMarker)
            {
                bool showLocked = currentRank <= 0 && !canInvest;
                lockedMarker.SetActive(showLocked);
            }

            if (unlockedMarker)
            {
                bool fullyUnlocked = currentRank >= currentMaxRank && currentMaxRank > 0;
                unlockedMarker.SetActive(fullyUnlocked);
            }

            if (button)
            {
                button.interactable = canInvest;
            }

            if (backgroundImage)
            {
                Color color = backgroundImage.color;
                if (isUnlocked)
                {
                    color.a = 1f;
                }
                else if (canInvest)
                {
                    // Slightly brighter when available to unlock
                    color.a = 0.9f;
                }
                else if (!meetsLevel || !prerequisitesMet)
                {
                    color.a = 0.4f;
                }
                else
                {
                    color.a = 0.6f;
                }
                backgroundImage.color = color;
            }

            if (rankLabel)
            {
                rankLabel.gameObject.SetActive(true);
                rankLabel.text = $"{Mathf.Clamp(currentRank, 0, currentMaxRank)}/{currentMaxRank}";
            }

            if (descriptionLabel)
            {
                // Get description from the ability for the current rank (or rank 1 if not unlocked yet)
                int rankToShow = currentRank > 0 ? currentRank : 1;
                AbilityDefinition ability = node != null ? node.GetAbilityForRank(rankToShow) : null;
                descriptionLabel.text = ability != null ? ability.Description : string.Empty;
            }

            // If we're currently hovering, refresh the tooltip with the updated rank
            if (isHovering)
            {
                RefreshTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (suppressNextClick)
            {
                suppressNextClick = false;
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!manager || tree == null || node == null)
            {
                return;
            }

            manager.TryUnlock(tree, node);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!CanBeginDrag())
            {
                return;
            }

            dragging = true;
            suppressNextClick = true;

            AbilityDefinition ability = GetActiveAbility();
            Sprite icon = node.Icon;
            AbilityDragDropService.BeginDrag(ability, icon);
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

        bool CanBeginDrag()
        {
            return manager && tree != null && node != null && isUnlocked && GetActiveAbility();
        }

        AbilityDefinition GetActiveAbility()
        {
            if (node == null)
            {
                return null;
            }

            if (currentRank > 0)
            {
                AbilityDefinition ability = node.GetAbilityForRank(currentRank);
                if (ability && !ability.IsPassive) return ability;
            }

            AbilityDefinition fallback = node.GetAbilityForRank(1);
            return fallback != null && !fallback.IsPassive ? fallback : null;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (node == null) return;

            isHovering = true;
            RefreshTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovering = false;
            lastTooltipRank = -1; // Reset so tooltip refreshes next time we hover

            if (AbilityTooltip.Instance != null)
            {
                AbilityTooltip.Instance.Hide();
            }
        }

        void RefreshTooltip()
        {
            if (node == null || !isHovering) return;

            // Determine which rank to show:
            // - If fully maxed out, show the max rank (what you currently have)
            // - Otherwise, show the next available rank (what you can upgrade to)
            int rankToShow;
            if (currentRank >= currentMaxRank)
            {
                // Fully upgraded - show the max rank
                rankToShow = currentMaxRank;
            }
            else
            {
                // Not fully upgraded - show the next rank you can get
                rankToShow = currentRank + 1;
            }

            // Only update tooltip if the rank to show has changed
            if (rankToShow != lastTooltipRank)
            {
                lastTooltipRank = rankToShow;

                AbilityDefinition abilityToShow = node.GetAbilityForRank(rankToShow);

                if (abilityToShow != null && AbilityTooltip.Instance != null)
                {
                    AbilityTooltip.Instance.Show(abilityToShow);
                }
            }
        }
    }
}





