using SkillSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Scripting.APIUpdating;
using SmallScale.FantasyKingdomTileset;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    using System.Collections.Generic;

    [MovedFrom("AbilitySystem")]
    public sealed class AbilitySlotView : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static Vector2 s_LastPointerDirection = Vector2.right;

        [SerializeField]
        private AbilitySlotManager slotManager;

        [SerializeField]
        private int slotIndex;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Image highlightImage;

        [SerializeField]
        private Image cooldownOverlay;

        [SerializeField]
        private TextMeshProUGUI keyLabel;

        [SerializeField]
        private TextMeshProUGUI chargeLabel;

        [SerializeField]
        private string displayKey;

        [SerializeField]
        private Sprite emptyIcon;

        bool dragging;

        void Reset()
        {
            // Auto-find references if not set
            if (!cooldownOverlay)
            {
                Transform overlayTransform = transform.Find("CooldownOverlay");
                if (overlayTransform) cooldownOverlay = overlayTransform.GetComponent<Image>();
            }
        }

        private void OnEnable()
        {
            if (!slotManager)
            {
                slotManager = GetComponentInParent<AbilitySlotManager>();
            }

            if (slotManager)
            {
                slotManager.SlotChanged += HandleSlotChanged;
            }

            UpdateLabel();
            Refresh();
            SetupCooldownOverlay();
        }

        private void OnDisable()
        {
            if (slotManager)
            {
                slotManager.SlotChanged -= HandleSlotChanged;
            }

            if (highlightImage)
            {
                highlightImage.enabled = false;
            }

            if (AbilityTooltip.Instance)
            {
                AbilityTooltip.Instance.Hide();
            }
        }

        void HandleSlotChanged(int index, AbilityDefinition ability)
        {
            if (index != slotIndex) return;
            Refresh();
        }

        void Refresh()
        {
            AbilityDefinition ability = slotManager ? slotManager.GetAbility(slotIndex) : null;
            if (iconImage)
            {
                if (ability && ability.Icon)
                {
                    iconImage.sprite = ability.Icon;
                    iconImage.enabled = true;
                }
                else if (emptyIcon)
                {
                    iconImage.sprite = emptyIcon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.enabled = false;
                }
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!slotManager || !AbilityDragDropService.HasPayload)
            {
                AbilityDragDropService.EndDrag();
                return;
            }

            bool handled = false;

            if (AbilityDragDropService.HasSourceSlot && AbilityDragDropService.SourceManager == slotManager)
            {
                int sourceIndex = AbilityDragDropService.SourceSlotIndex;
                if (sourceIndex == slotIndex)
                {
                    handled = true;
                }
                else if (sourceIndex >= 0)
                {
                    handled = slotManager.TrySwap(sourceIndex, slotIndex);
                }
            }

            AbilityDefinition ability = AbilityDragDropService.Ability;
            if (!handled && ability)
            {
                handled = slotManager.TryAssign(slotIndex, ability);

                if (handled && AbilityDragDropService.HasSourceSlot && AbilityDragDropService.SourceManager != null && AbilityDragDropService.SourceManager != slotManager)
                {
                    AbilityDragDropService.SourceManager.TryClear(AbilityDragDropService.SourceSlotIndex);
                }
            }

            AbilityDragDropService.EndDrag();
            if (handled)
            {
                Refresh();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (highlightImage)
            {
                highlightImage.enabled = true;
            }

            AbilityDefinition ability = slotManager ? slotManager.GetAbility(slotIndex) : null;
            if (ability && AbilityTooltip.Instance)
            {
                AbilityTooltip.Instance.Show(ability);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightImage)
            {
                highlightImage.enabled = false;
            }

            if (AbilityTooltip.Instance)
            {
                AbilityTooltip.Instance.Hide();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!slotManager) return;

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (slotManager.TryClear(slotIndex))
                {
                    Refresh();
                }
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                TryActivateAssignedAbility();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (dragging) return;
            AbilityDefinition ability = slotManager ? slotManager.GetAbility(slotIndex) : null;
            if (!ability) return;

            dragging = true;
            AbilityDragDropService.BeginDrag(ability, ability.Icon, slotManager, slotIndex);
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

        void TryActivateAssignedAbility()
        {
            if (!slotManager)
            {
                return;
            }

            AbilityDefinition ability = slotManager.GetAbility(slotIndex);
            if (!ability || ability.IsPassive)
            {
                return;
            }

            AbilityRunner runner = slotManager.Runner;
            if (!runner)
            {
                return;
            }

            if (TryHandleStealthToggle(ability, runner.transform))
            {
                return;
            }

            AbilityActivationParameters parameters = new AbilityActivationParameters
            {
                DesiredDirection = DeterminePointerDirection(runner.transform),
                Target = runner.CachedTarget
            };

            runner.TryActivateAbility(ability, parameters);
        }

        Vector2 DeterminePointerDirection(Transform owner)
        {
            Vector2 dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir = dir.normalized;
                s_LastPointerDirection = dir;
                return dir;
            }

            Camera camera = Camera.main;
            if (camera)
            {
                Vector3 mouse = Input.mousePosition;
                Vector3 world = camera.ScreenToWorldPoint(mouse);
                Vector2 ownerPos = owner ? (Vector2)owner.position : Vector2.zero;
                Vector2 delta = (Vector2)world - ownerPos;
                if (delta.sqrMagnitude > 0.0001f)
                {
                    dir = delta.normalized;
                    s_LastPointerDirection = dir;
                    return dir;
                }
            }

            return s_LastPointerDirection;
        }

        bool TryHandleStealthToggle(AbilityDefinition ability, Transform owner)
        {
            if (!ability || owner == null)
            {
                return false;
            }

            IReadOnlyList<AbilityStep> steps = ability.Steps;
            if (steps == null)
            {
                return false;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is InvisibilityStep invis && invis.RecastCancelsStealth)
                {
                    return InvisibilityStep.TryCancelStealth(owner);
                }
            }

            return false;
        }

        void UpdateLabel()
        {
            if (keyLabel)
            {
                keyLabel.text = displayKey;
            }
        }

        public void SetDisplayKey(string text)
        {
            displayKey = text;
            UpdateLabel();
        }

        void SetupCooldownOverlay()
        {
            if (cooldownOverlay)
            {
                cooldownOverlay.type = Image.Type.Filled;
                cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
                cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
                cooldownOverlay.fillAmount = 0f;
                cooldownOverlay.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            UpdateCooldownOverlay();
        }

        void UpdateCooldownOverlay()
        {
            if (!cooldownOverlay) return;

            AbilityDefinition ability = slotManager ? slotManager.GetAbility(slotIndex) : null;
            if (!ability || !slotManager || !slotManager.Runner)
            {
                if (cooldownOverlay.gameObject.activeSelf)
                {
                    cooldownOverlay.gameObject.SetActive(false);
                }
                if (chargeLabel)
                {
                    chargeLabel.gameObject.SetActive(false);
                }
                return;
            }

            var state = slotManager.Runner.GetState(ability);
            if (state == null)
            {
                if (cooldownOverlay.gameObject.activeSelf)
                {
                    cooldownOverlay.gameObject.SetActive(false);
                }
                if (chargeLabel)
                {
                    chargeLabel.gameObject.SetActive(false);
                }
                return;
            }

            // Update charge display
            UpdateChargeDisplay(state);

            // Check if ability is on cooldown using new charge system
            float fillAmount = state.GetCooldownFillAmount();
            if (fillAmount > 0f)
            {
                cooldownOverlay.fillAmount = fillAmount;

                if (!cooldownOverlay.gameObject.activeSelf)
                {
                    cooldownOverlay.gameObject.SetActive(true);
                }
            }
            else
            {
                // Not on cooldown or all charges ready
                if (cooldownOverlay.gameObject.activeSelf)
                {
                    cooldownOverlay.gameObject.SetActive(false);
                }
            }
        }

        void UpdateChargeDisplay(AbilityRuntimeState state)
        {
            if (!chargeLabel) return;

            int maxCharges = state.GetMaxCharges();
            if (maxCharges > 0)
            {
                // Show charges: "current/max"
                int currentCharges = state.ChargesRemaining;
                chargeLabel.text = $"{currentCharges}/{maxCharges}";
                chargeLabel.gameObject.SetActive(true);
            }
            else
            {
                // No charge system, hide label
                chargeLabel.gameObject.SetActive(false);
            }
        }
    }
}








