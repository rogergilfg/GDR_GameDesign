using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using SmallScale.FantasyKingdomTileset;
using SmallScale.FantasyKingdomTileset.Building;
using Input = SmallScale.FantasyKingdomTileset.InputAdapter;

namespace SkillSystem
{
    /// <summary>
    /// Controls the spell book window visibility and integrates with other UI panels.
    /// Ensures only one panel (Spell Book, Inventory, Build Menu, or Skill Window) is open at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public class SpellBookController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField]
        [Tooltip("Key used to toggle the spell book.")]
        private KeyCode toggleKey = KeyCode.K;

        [SerializeField]
        [Tooltip("Root GameObject representing the spell book UI.")]
        private GameObject spellBookRoot;

        [Header("Other Panels to Close")]
        [SerializeField]
        [Tooltip("Inventory UI that should be closed when the spell book opens.")]
        private InventoryUIController inventoryUI;

        [SerializeField]
        [Tooltip("Build menu that should be closed when the spell book opens.")]
        private BuildMenuController buildMenu;

        [SerializeField]
        [Tooltip("Skill window that should be closed when the spell book opens.")]
        private SkillWindowController skillWindow;

        [Header("UI Buttons")]
        [SerializeField]
        [Tooltip("Optional button that toggles the spell book when clicked.")]
        private Button toggleButton;

        [SerializeField]
        [Tooltip("Optional button that closes the spell book when clicked.")]
        private Button closeButton;

        [Header("Events")]
        [SerializeField]
        private UnityEvent onWindowOpened;

        [SerializeField]
        private UnityEvent onWindowClosed;

        private bool isOpen;

        /// <summary>
        /// Returns true if the spell book is currently open.
        /// </summary>
        public bool IsOpen => isOpen;

        void Awake()
        {
            // Auto-find other panels if not assigned
            if (!inventoryUI)
            {
                inventoryUI = FindFirstObjectByType<InventoryUIController>();
            }

            if (!buildMenu)
            {
                buildMenu = FindFirstObjectByType<BuildMenuController>();
            }

            if (!skillWindow)
            {
                skillWindow = FindFirstObjectByType<SkillWindowController>();
            }

            // Start with spell book closed
            if (spellBookRoot)
            {
                spellBookRoot.SetActive(false);
            }
        }

        void OnEnable()
        {
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
            // Toggle with K key
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                ToggleWindow();
            }

            // Close with Escape when open
            if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseWindow();
            }
        }

        /// <summary>
        /// Toggles the spell book window between open and closed.
        /// </summary>
        public void ToggleWindow()
        {
            SetWindowVisible(!isOpen);
        }

        /// <summary>
        /// Opens the spell book window.
        /// </summary>
        public void OpenWindow()
        {
            SetWindowVisible(true);
        }

        /// <summary>
        /// Closes the spell book window.
        /// </summary>
        public void CloseWindow()
        {
            SetWindowVisible(false);
        }

        /// <summary>
        /// Sets the spell book window visibility.
        /// </summary>
        void SetWindowVisible(bool visible)
        {
            if (isOpen == visible)
            {
                return;
            }

            // When opening, close all other panels
            if (visible)
            {
                if (inventoryUI != null && inventoryUI.IsInventoryVisible())
                {
                    inventoryUI.CloseInventory();
                }

                if (buildMenu != null && buildMenu.IsOpen)
                {
                    buildMenu.CloseMenu();
                }

                if (skillWindow != null && skillWindow.IsOpen)
                {
                    skillWindow.CloseWindow();
                }
            }

            isOpen = visible;

            if (spellBookRoot)
            {
                spellBookRoot.SetActive(isOpen);
            }

            if (isOpen)
            {
                onWindowOpened?.Invoke();
            }
            else
            {
                // Hide tooltip when window closes
                if (AbilityTooltip.Instance != null)
                {
                    AbilityTooltip.Instance.Hide();
                }

                onWindowClosed?.Invoke();
            }
        }
    }
}







