using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SkillSystem
{
    /// <summary>
    /// Displays all abilities the player has acquired from all sources (skill trees, starting abilities, etc.).
    /// Supports drag-and-drop to ability slots.
    /// </summary>
    public class SpellBookView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("The AbilityRunner component on the player that tracks all abilities.")]
        private AbilityRunner abilityRunner;

        [SerializeField]
        [Tooltip("The container where ability buttons will be spawned (usually a ScrollRect's content).")]
        private Transform contentContainer;

        [SerializeField]
        [Tooltip("Prefab for individual ability buttons in the spell book.")]
        private SpellBookAbilityButton abilityButtonPrefab;

        [Header("Separator (Optional)")]
        [SerializeField]
        [Tooltip("Optional prefab for a separator line between active and passive abilities.")]
        private GameObject separatorPrefab;

        [Header("Auto-Find Player")]
        [SerializeField]
        [Tooltip("If true, automatically finds the player's AbilityRunner on Start if not assigned.")]
        private bool autoFindPlayer = true;

        private readonly List<SpellBookAbilityButton> activeButtons = new List<SpellBookAbilityButton>();
        private GameObject activeSeparator;

        void Start()
        {
            if (autoFindPlayer && !abilityRunner)
            {
                // Find the player's AbilityRunner
                abilityRunner = FindPlayerAbilityRunner();
            }

            if (abilityRunner)
            {
                // Subscribe to ability events
                abilityRunner.AbilityGranted += OnAbilityGranted;
                abilityRunner.AbilityRemoved += OnAbilityRemoved;

                // Build initial list
                Rebuild();
            }
            else
            {
                Debug.LogWarning("SpellBookView: No AbilityRunner assigned and could not auto-find player.", this);
            }
        }

        void OnDestroy()
        {
            if (abilityRunner)
            {
                abilityRunner.AbilityGranted -= OnAbilityGranted;
                abilityRunner.AbilityRemoved -= OnAbilityRemoved;
            }
        }

        /// <summary>
        /// Finds the player's AbilityRunner component.
        /// </summary>
        AbilityRunner FindPlayerAbilityRunner()
        {
            // Try to find by layer
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer != -1)
            {
                var allRunners = FindObjectsByType<AbilityRunner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (var runner in allRunners)
                {
                    if (runner.gameObject.layer == playerLayer)
                    {
                        return runner;
                    }
                }
            }

            // Fallback: look for player-owned AbilityRunner
            var runners = FindObjectsByType<AbilityRunner>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var runner in runners)
            {
                if (runner.IsPlayerControlled)
                {
                    return runner;
                }
            }

            return null;
        }

        /// <summary>
        /// Clears and rebuilds the entire spell book from scratch.
        /// </summary>
        public void Rebuild()
        {
            if (!abilityRunner || !contentContainer || !abilityButtonPrefab)
            {
                return;
            }

            // Clear existing buttons
            ClearButtons();

            // Gather all abilities
            var allAbilities = new List<AbilityDefinition>();
            foreach (var ability in abilityRunner.EnumerateAbilities())
            {
                if (ability)
                {
                    allAbilities.Add(ability);
                }
            }

            // Sort: Active abilities first, then passive abilities
            allAbilities.Sort((a, b) =>
            {
                if (a.IsPassive == b.IsPassive)
                {
                    return string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
                }
                return a.IsPassive ? 1 : -1;
            });

            // Track if we need a separator
            bool hasActiveAbilities = false;
            bool separatorAdded = false;

            // Create buttons for each ability
            foreach (var ability in allAbilities)
            {
                // Add separator between active and passive
                if (!separatorAdded && ability.IsPassive && hasActiveAbilities && separatorPrefab)
                {
                    activeSeparator = Instantiate(separatorPrefab, contentContainer);
                    separatorAdded = true;
                }

                var button = Instantiate(abilityButtonPrefab, contentContainer);

                // For now, show rank 1 for all abilities
                // TODO: If you want to track ability ranks outside skill trees, implement rank tracking
                button.Setup(ability, rank: 1);
                activeButtons.Add(button);

                if (ability.IsPassive)
                {
                }
                else
                {
                    hasActiveAbilities = true;
                }
            }
        }

        /// <summary>
        /// Clears all ability buttons from the spell book.
        /// </summary>
        void ClearButtons()
        {
            foreach (var button in activeButtons)
            {
                if (button)
                {
                    Destroy(button.gameObject);
                }
            }
            activeButtons.Clear();

            if (activeSeparator)
            {
                Destroy(activeSeparator);
                activeSeparator = null;
            }
        }

        /// <summary>
        /// Called when a new ability is granted to the player.
        /// </summary>
        void OnAbilityGranted(AbilityDefinition ability)
        {
            // Rebuild the entire list to maintain sorting
            Rebuild();
        }

        /// <summary>
        /// Called when an ability is removed from the player.
        /// </summary>
        void OnAbilityRemoved(AbilityDefinition ability)
        {
            // Rebuild the entire list
            Rebuild();
        }

        /// <summary>
        /// Manually set the ability runner (useful for testing or custom setups).
        /// </summary>
        public void SetAbilityRunner(AbilityRunner runner)
        {
            if (abilityRunner)
            {
                abilityRunner.AbilityGranted -= OnAbilityGranted;
                abilityRunner.AbilityRemoved -= OnAbilityRemoved;
            }

            abilityRunner = runner;

            if (abilityRunner)
            {
                abilityRunner.AbilityGranted += OnAbilityGranted;
                abilityRunner.AbilityRemoved += OnAbilityRemoved;
                Rebuild();
            }
        }
    }
}






