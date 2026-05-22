using System.Collections.Generic;
using TMPro;
using SmallScale.FantasyKingdomTileset;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SkillSystem
{
    [DisallowMultipleComponent]
    public sealed class SkillWindowController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Key used to toggle the skill window.")]
        private KeyCode toggleKey = KeyCode.J;

        [SerializeField]
        [Tooltip("Root GameObject representing the overall skill UI window.")]
        private GameObject skillWindowRoot;

        [SerializeField]
        [Tooltip("Panel that contains the standard skill tree UI.")]
        private GameObject skillTreeWindowRoot;

        [SerializeField]
        [Tooltip("Panel that contains the class selection UI.")]
        private GameObject classSelectionWindowRoot;

        [SerializeField]
        [Tooltip("Skill manager that provides the available skill points.")]
        private SkillManager skillManager;

        [SerializeField]
        [Tooltip("Inventory UI that should be closed when the skill window opens.")]
        private InventoryUIController inventoryUI;

        [SerializeField]
        [Tooltip("Build menu that should be closed when the skill window opens.")]
        private BuildMenuController buildMenu;

        [SerializeField]
        [Tooltip("Spell book that should be closed when the skill window opens.")]
        private SpellBookController spellBook;

        [SerializeField]
        [Tooltip("Panels that represent individual skill trees.")]
        private SkillPanel[] skillPanels = System.Array.Empty<SkillPanel>();

        [SerializeField]
        [Tooltip("Skill trees that should be displayed in order.")]
        private SkillTreeDefinition[] defaultTrees = System.Array.Empty<SkillTreeDefinition>();

        [Header("Tree Selection")]
        [SerializeField]
        [Tooltip("Button prefab used when prompting the player to choose a skill tree.")]
        private SkillTreeSelectionButton classSelectionButtonPrefab;

        [SerializeField]
        [Tooltip("Container that holds dynamically created tree selection buttons.")]
        private Transform classSelectionButtonContainer;

        [SerializeField]
        [Tooltip("Text label describing how many trees remain to be selected.")]
        private TextMeshProUGUI classSelectionPromptLabel;

        [SerializeField]
        [Tooltip("Format string for the selection prompt (e.g. \"Choose your path ({0} remaining)\").")]
        private string classSelectionPromptFormat = "Choose your specialization ({0} remaining)";

        [Header("UI")]
        [SerializeField]
        [Tooltip("Label that displays the available skill points.")]
        private TextMeshProUGUI skillPointsLabel;

        [SerializeField]
        [Tooltip("Optional button that toggles the skill window when clicked.")]
        private Button toggleButton;

        [SerializeField]
        [Tooltip("Optional button that closes the skill window when clicked.")]
        private Button closeButton;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onWindowOpened;

        [SerializeField]
        private UnityEvent onWindowClosed;

        private bool isOpen;
        public bool IsOpen => isOpen;
        readonly List<SkillTreeDefinition> _selectedTreeBuffer = new();
        readonly List<SkillTreeSelectionButton> _activeSelectionButtons = new();
        bool _warnedMissingSelectionUI;

        void Awake()
        {
            if (!skillManager)
            {
                skillManager = FindFirstObjectByType<SkillManager>();
            }

            if (!inventoryUI)
            {
                inventoryUI = FindFirstObjectByType<InventoryUIController>();
            }

            if (!buildMenu)
            {
                buildMenu = FindFirstObjectByType<BuildMenuController>();
            }

            if (!spellBook)
            {
                spellBook = FindFirstObjectByType<SpellBookController>();
            }

            if (skillWindowRoot)
            {
                skillWindowRoot.SetActive(false);
            }

            if (skillTreeWindowRoot)
            {
                skillTreeWindowRoot.SetActive(false);
            }

            if (classSelectionWindowRoot)
            {
                classSelectionWindowRoot.SetActive(false);
            }

            UpdateSkillPointsDisplay();
        }

        void OnEnable()
        {
            if (skillManager != null)
            {
                skillManager.SkillPointsChanged += HandleSkillPointsChanged;
                skillManager.SelectedTreesChanged += HandleSelectedTreesChanged;
                skillManager.TreeSlotsChanged += HandleTreeSlotsChanged;
            }

            if (toggleButton)
            {
                toggleButton.onClick.AddListener(ToggleWindow);
            }

            if (closeButton)
            {
                closeButton.onClick.AddListener(CloseWindow);
            }
        }

        void OnDisable()
        {
            if (skillManager != null)
            {
                skillManager.SkillPointsChanged -= HandleSkillPointsChanged;
                skillManager.SelectedTreesChanged -= HandleSelectedTreesChanged;
                skillManager.TreeSlotsChanged -= HandleTreeSlotsChanged;
            }

            if (toggleButton)
            {
                toggleButton.onClick.RemoveListener(ToggleWindow);
            }

            if (closeButton)
            {
                closeButton.onClick.RemoveListener(CloseWindow);
            }
        }

        void Update()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                ToggleWindow();
            }

            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWindow();
            }
        }

        public void ToggleWindow()
        {
            SetWindowVisible(!isOpen);
        }

        public void OpenWindow()
        {
            SetWindowVisible(true);
        }

        public void CloseWindow()
        {
            SetWindowVisible(false);
        }

        void SetWindowVisible(bool visible)
        {
            if (isOpen == visible)
            {
                return;
            }

            if (visible)
            {
                if (inventoryUI != null && inventoryUI.IsInventoryVisible())
                {
                    inventoryUI.CloseInventory();
                }

                if (buildMenu && buildMenu.IsOpen)
                {
                    buildMenu.CloseMenu();
                }

                if (spellBook && spellBook.IsOpen)
                {
                    spellBook.CloseWindow();
                }
            }

            isOpen = visible;

            if (isOpen)
            {
                RefreshWindowContents();
                UpdateSkillPointsDisplay();
                onWindowOpened?.Invoke();
            }
            else
            {
                HideAllSubWindows();
                ClearSelectionButtons();
                // Hide tooltip when window closes
                if (AbilityTooltip.Instance != null)
                {
                    AbilityTooltip.Instance.Hide();
                }

                onWindowClosed?.Invoke();
            }
        }

        void RefreshWindowContents()
        {
            if (RequiresTreeSelection())
            {
                ShowClassSelectionWindow();
            }
            else
            {
                ShowSkillTreeWindow();
            }
        }

        void ShowClassSelectionWindow()
        {
            if (skillWindowRoot)
            {
                skillWindowRoot.SetActive(false);
            }

            if (skillTreeWindowRoot)
            {
                skillTreeWindowRoot.SetActive(false);
            }

            if (classSelectionWindowRoot)
            {
                classSelectionWindowRoot.SetActive(true);
            }

            BuildSelectionButtons();
            UpdateSelectionPrompt();
        }

        void ShowSkillTreeWindow()
        {
            if (classSelectionWindowRoot)
            {
                classSelectionWindowRoot.SetActive(false);
            }

            if (skillTreeWindowRoot)
            {
                skillTreeWindowRoot.SetActive(true);
            }

            if (skillWindowRoot)
            {
                skillWindowRoot.SetActive(true);
            }

            ClearSelectionButtons();
            PopulatePanels();
        }

        void HideAllSubWindows()
        {
            if (classSelectionWindowRoot)
            {
                classSelectionWindowRoot.SetActive(false);
            }

            if (skillTreeWindowRoot)
            {
                skillTreeWindowRoot.SetActive(false);
            }

            if (skillWindowRoot)
            {
                skillWindowRoot.SetActive(false);
            }
        }

        void PopulatePanels()
        {
            if (skillPanels == null)
            {
                return;
            }

            IReadOnlyList<SkillTreeDefinition> trees = GetTreesForDisplay();
            int treeCount = trees != null ? trees.Count : 0;
            int treeIndex = 0;

            for (int i = 0; i < skillPanels.Length; i++)
            {
                SkillPanel panel = skillPanels[i];
                if (!panel) continue;

                SkillTreeDefinition tree = null;
                while (treeIndex < treeCount && tree == null)
                {
                    tree = trees[treeIndex++];
                }

                if (tree)
                {
                    panel.gameObject.SetActive(true);
                    panel.ShowTree(tree);
                }
                else
                {
                    panel.gameObject.SetActive(false);
                }
            }
        }

        IReadOnlyList<SkillTreeDefinition> GetTreesForDisplay()
        {
            if (skillManager)
            {
                skillManager.GetSelectedTrees(_selectedTreeBuffer);
                if (_selectedTreeBuffer.Count > 0)
                {
                    return _selectedTreeBuffer;
                }
            }

            return defaultTrees;
        }

        bool RequiresTreeSelection()
        {
            if (!skillManager)
            {
                return false;
            }

            if (skillManager.RemainingTreeSelections <= 0)
            {
                return false;
            }

            if (!HasAvailableTreeChoices())
            {
                return false;
            }

            if (!classSelectionWindowRoot || !classSelectionButtonPrefab || !classSelectionButtonContainer)
            {
                if (!_warnedMissingSelectionUI)
                {
                    Debug.LogWarning("SkillWindowController is missing the tree selection prefab or container, cannot present the specialization choice UI.", this);
                    _warnedMissingSelectionUI = true;
                }
                return false;
            }

            return true;
        }

        bool HasAvailableTreeChoices()
        {
            if (!skillManager)
            {
                return false;
            }

            var trees = skillManager.AvailableTrees;
            if (trees == null)
            {
                return false;
            }

            for (int i = 0; i < trees.Count; i++)
            {
                SkillTreeDefinition tree = trees[i];
                if (tree != null && !skillManager.IsTreeSelected(tree))
                {
                    return true;
                }
            }

            return false;
        }

        void BuildSelectionButtons()
        {
            ClearSelectionButtons();
            if (!skillManager || !classSelectionButtonPrefab || !classSelectionButtonContainer)
            {
                return;
            }

            var trees = skillManager.AvailableTrees;
            if (trees == null)
            {
                return;
            }

            for (int i = 0; i < trees.Count; i++)
            {
                SkillTreeDefinition tree = trees[i];
                if (tree == null || skillManager.IsTreeSelected(tree))
                {
                    continue;
                }

                SkillTreeSelectionButton entry = Instantiate(classSelectionButtonPrefab, classSelectionButtonContainer);
                entry.Initialize(tree, HandleTreeSelectionClicked);
                entry.gameObject.name = $"Select_{tree.DisplayName}";
                _activeSelectionButtons.Add(entry);
            }

            UpdateSelectionPrompt();
        }

        void ClearSelectionButtons()
        {
            for (int i = 0; i < _activeSelectionButtons.Count; i++)
            {
                if (_activeSelectionButtons[i])
                {
                    Destroy(_activeSelectionButtons[i].gameObject);
                }
            }
            _activeSelectionButtons.Clear();
        }

        void UpdateSelectionPrompt()
        {
            if (!classSelectionPromptLabel)
            {
                return;
            }

            if (!skillManager)
            {
                classSelectionPromptLabel.text = string.Empty;
                return;
            }

            int remaining = skillManager.RemainingTreeSelections;
            if (remaining <= 0)
            {
                classSelectionPromptLabel.text = string.Empty;
                return;
            }

            string format = string.IsNullOrEmpty(classSelectionPromptFormat)
                ? "Choose a skill tree ({0} remaining)"
                : classSelectionPromptFormat;

            classSelectionPromptLabel.text = string.Format(format, remaining);
        }

        void HandleTreeSelectionClicked(SkillTreeDefinition tree)
        {
            if (!skillManager || !tree)
            {
                return;
            }

            if (!skillManager.TrySelectTree(tree))
            {
                return;
            }

            if (RequiresTreeSelection())
            {
                BuildSelectionButtons();
            }
            else
            {
                ShowSkillTreeWindow();
            }
        }

        void HandleSkillPointsChanged(int points)
        {
            UpdateSkillPointsDisplay();
        }

        void HandleSelectedTreesChanged()
        {
            if (isOpen)
            {
                RefreshWindowContents();
            }
        }

        void HandleTreeSlotsChanged(int slots)
        {
            if (isOpen)
            {
                RefreshWindowContents();
            }
        }

        void UpdateSkillPointsDisplay()
        {
            if (!skillPointsLabel) return;
            int points = skillManager != null ? skillManager.AvailableSkillPoints : 0;
            skillPointsLabel.text = points.ToString();
        }
    }
}






