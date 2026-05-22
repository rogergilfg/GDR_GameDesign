using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;
using TMPro;
using SmallScale.FantasyKingdomTileset.UI;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    [DisallowMultipleComponent]
    [MovedFrom("AbilitySystem")]
    public sealed class AbilityHotkeyManager : MonoBehaviour
    {
        [SerializeField]
        private AbilitySlotManager slotManager;

        [SerializeField]
        private KeyCode[] slotHotkeys = new KeyCode[10]
        {
            KeyCode.Alpha1,
            KeyCode.Alpha2,
            KeyCode.Alpha3,
            KeyCode.Alpha4,
            KeyCode.Alpha5,
            KeyCode.Alpha6,
            KeyCode.Alpha7,
            KeyCode.Alpha8,
            KeyCode.Alpha9,
            KeyCode.Alpha0
        };

        [SerializeField]
        private AbilitySlotView[] slotViews = System.Array.Empty<AbilitySlotView>();

        [SerializeField]
        private bool useAxisInput = true;

        [SerializeField]
        private bool fallbackToMouseDirection = true;

        [SerializeField]
        private bool triggerOnKeyHold = false;

        [Header("Companion Commands")]
        [SerializeField]
        private CompanionCommandPanelController companionCommandPanel;

        [SerializeField]
        private KeyCode companionAttackHotkey = KeyCode.None;

        [SerializeField]
        private KeyCode companionFollowHotkey = KeyCode.None;

        [SerializeField]
        private KeyCode companionStopHotkey = KeyCode.None;

        [SerializeField]
        private TextMeshProUGUI companionAttackHotkeyLabel;

        [SerializeField]
        private TextMeshProUGUI companionFollowHotkeyLabel;

        [SerializeField]
        private TextMeshProUGUI companionStopHotkeyLabel;

        private Vector2 lastNonZeroInput = Vector2.right;

        void Awake()
        {
            if (!slotManager)
            {
                slotManager = GetComponent<AbilitySlotManager>();
            }

            UpdateSlotViewLabels();
            UpdateCompanionHotkeyLabels();
        }

        void OnValidate()
        {
            UpdateSlotViewLabels();
            UpdateCompanionHotkeyLabels();
        }

        void Update()
        {
            if (!slotManager) return;

            AbilityRunner runner = slotManager.Runner;
            if (!runner) return;

            int count = Mathf.Min(slotManager.SlotCount, slotHotkeys.Length);
            for (int i = 0; i < count; i++)
            {
                AbilityDefinition ability = slotManager.GetAbility(i);
                if (!ability || ability.IsPassive) continue;

                KeyCode key = slotHotkeys[i];
                if (key == KeyCode.None) continue;

                bool pressed = triggerOnKeyHold ? Input.GetKey(key) : Input.GetKeyDown(key);
                if (!pressed) continue;

                AbilityActivationParameters parameters = new AbilityActivationParameters
                {
                    DesiredDirection = DetermineDirection(runner.transform),
                    Target = runner.CachedTarget
                };

                if (TryHandleStealthToggle(ability, runner.transform))
                {
                    continue;
                }

                runner.TryActivateAbility(ability, parameters);
            }

            HandleCompanionHotkeys();
        }

        Vector2 DetermineDirection(Transform owner)
        {
            Vector2 dir = Vector2.zero;

            if (useAxisInput)
            {
                dir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            }

            if (dir.sqrMagnitude < 0.0001f && fallbackToMouseDirection)
            {
                Camera camera = Camera.main;
                if (camera)
                {
                    Vector3 mouse = Input.mousePosition;
                    Vector3 world = camera.ScreenToWorldPoint(mouse);
                    Vector2 ownerPos = owner ? (Vector2)owner.position : Vector2.zero;
                    dir = world - (Vector3)ownerPos;
                }
            }

            if (dir.sqrMagnitude > 0.0001f)
            {
                lastNonZeroInput = dir.normalized;
                return lastNonZeroInput;
            }

            return lastNonZeroInput;
        }

        void UpdateSlotViewLabels()
        {
            if (slotViews == null) return;
            int count = Mathf.Min(slotViews.Length, slotHotkeys.Length);
            for (int i = 0; i < count; i++)
            {
                AbilitySlotView view = slotViews[i];
                if (!view) continue;

                string label = GetKeyLabel(slotHotkeys[i]);
                view.SetDisplayKey(label);
            }
        }

        void UpdateCompanionHotkeyLabels()
        {
            if (companionAttackHotkeyLabel)
            {
                companionAttackHotkeyLabel.text = GetKeyLabel(companionAttackHotkey);
            }

            if (companionFollowHotkeyLabel)
            {
                companionFollowHotkeyLabel.text = GetKeyLabel(companionFollowHotkey);
            }

            if (companionStopHotkeyLabel)
            {
                companionStopHotkeyLabel.text = GetKeyLabel(companionStopHotkey);
            }
        }

        string GetKeyLabel(KeyCode key)
        {
            if (key == KeyCode.None) return string.Empty;

            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            {
                int value = key - KeyCode.Alpha0;
                return value.ToString();
            }

            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9)
            {
                int value = key - KeyCode.Keypad0;
                return $"KP{value}";
            }

            return key.ToString();
        }

        void HandleCompanionHotkeys()
        {
            if (!companionCommandPanel) return;

            if (IsCompanionHotkeyPressed(companionAttackHotkey))
            {
                companionCommandPanel.TriggerAttackCommand();
            }

            if (IsCompanionHotkeyPressed(companionFollowHotkey))
            {
                companionCommandPanel.TriggerFollowCommand();
            }

            if (IsCompanionHotkeyPressed(companionStopHotkey))
            {
                companionCommandPanel.TriggerHoldCommand();
            }
        }

        bool IsCompanionHotkeyPressed(KeyCode key)
        {
            if (key == KeyCode.None) return false;
            return Input.GetKeyDown(key);
        }

        bool TryHandleStealthToggle(AbilityDefinition ability, Transform owner)
        {
            if (!ability || owner == null) return false;

            IReadOnlyList<AbilityStep> steps = ability.Steps;
            if (steps == null) return false;

            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] is InvisibilityStep invis && invis.RecastCancelsStealth)
                {
                    return InvisibilityStep.TryCancelStealth(owner);
                }
            }

            return false;
        }
    }
}





