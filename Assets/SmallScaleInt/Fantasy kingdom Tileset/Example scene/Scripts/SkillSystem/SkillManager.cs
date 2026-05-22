using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.AbilitySystem;
using SmallScale.FantasyKingdomTileset;

namespace SkillSystem
{
    [DisallowMultipleComponent]
    public sealed class SkillManager : MonoBehaviour
    {
        public static SkillManager Instance { get; private set; }
        [SerializeField]
        [Tooltip("Experience component that drives leveling (defaults to PlayerExperience singleton).")]
        private PlayerExperience playerExperience;

        [SerializeField]
        private AbilityRunner abilityRunner;

        [SerializeField]
        [Tooltip("Ability slot manager used for auto-populating unlocked abilities.")]
        private AbilitySlotManager abilitySlotManager;

        [SerializeField]
        private List<SkillTreeDefinition> availableTrees = new List<SkillTreeDefinition>();

        [SerializeField]
        [Tooltip("Skill trees that start enabled for the player (consumes tree slots).")]
        private List<SkillTreeDefinition> startingSelectedTrees = new List<SkillTreeDefinition>();

        [Header("Tree Selection")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Number of tree slots immediately available to the player.")]
        private int startingTreeSlots = 1;

        [SerializeField]
        [Tooltip("Additional tree slots unlocked once the player reaches these levels.")]
        private List<int> additionalTreeSlotUnlockLevels = new List<int>();

        [SerializeField]
        [Tooltip("Skill points granted immediately when the manager initialises (e.g., starting points).")]
        private int startingSkillPoints = 0;

        [SerializeField]
        [Tooltip("Skill points granted each time the player gains a level.")]
        private int skillPointsPerLevel = 1;

        private readonly Dictionary<string, SkillTreeState> _treeStates = new Dictionary<string, SkillTreeState>();
        private readonly List<string> _selectedTreeIds = new List<string>();
        private int skillPoints;
        private int lastKnownLevel;
        int _unlockedTreeSlots;

        public event Action<SkillTreeDefinition, SkillNodeDefinition> SkillUnlocked;
        public event Action<SkillTreeDefinition, SkillNodeDefinition, int> SkillRankChanged;
        public event Action SkillDataChanged;
        public event Action<int> SkillPointsChanged;
        public event Action SelectedTreesChanged;
        public event Action<int> TreeSlotsChanged;

        void OnValidate()
        {
            if (availableTrees == null)
            {
                availableTrees = new List<SkillTreeDefinition>();
            }

            if (startingSelectedTrees == null)
            {
                startingSelectedTrees = new List<SkillTreeDefinition>();
            }

            if (additionalTreeSlotUnlockLevels == null)
            {
                additionalTreeSlotUnlockLevels = new List<int>();
            }
            else
            {
                additionalTreeSlotUnlockLevels.Sort();
            }

            startingTreeSlots = Mathf.Max(1, startingTreeSlots);
        }

        void Awake()
        {
            Instance = this;
            if (!abilityRunner)
            {
                abilityRunner = GetComponent<AbilityRunner>();
            }

            if (!abilitySlotManager)
            {
                abilitySlotManager = GetComponent<AbilitySlotManager>();
            }

            if (!playerExperience)
            {
                playerExperience = PlayerExperience.Instance;
            }

            InitializeStates();
            InitialiseSkillPoints();
            UpdateUnlockedTreeSlots(forceNotify: true);
            ApplyStartingSelections();
            SubscribeExperience();
            SyncAbilityRanksWithStates();
        }

        void OnEnable()
        {
            SubscribeExperience();
        }

        void OnDisable()
        {
            UnsubscribeExperience();
        }

        void InitializeStates()
        {
            if (availableTrees == null)
            {
                availableTrees = new List<SkillTreeDefinition>();
            }

            for (int i = 0; i < availableTrees.Count; i++)
            {
                SkillTreeDefinition tree = availableTrees[i];
                if (!tree) continue;

                string id = tree.TreeId;
                if (!_treeStates.ContainsKey(id))
                {
                    _treeStates.Add(id, new SkillTreeState(id));
                }
            }
        }

        void InitialiseSkillPoints()
        {
            skillPoints = Mathf.Max(0, startingSkillPoints);
            lastKnownLevel = CurrentLevel;
            SkillPointsChanged?.Invoke(skillPoints);
            SkillDataChanged?.Invoke();
        }

        void SubscribeExperience()
        {
            if (!playerExperience) return;

            playerExperience.LevelChanged -= HandleLevelChanged;
            playerExperience.LevelChanged += HandleLevelChanged;

            playerExperience.ExperienceChanged -= HandleExperienceChanged;
            playerExperience.ExperienceChanged += HandleExperienceChanged;
        }

        void UnsubscribeExperience()
        {
            if (!playerExperience) return;

            playerExperience.LevelChanged -= HandleLevelChanged;
            playerExperience.ExperienceChanged -= HandleExperienceChanged;
        }

        void HandleLevelChanged(int newLevel)
        {
            bool notified = false;
            if (skillPointsPerLevel > 0 && newLevel > lastKnownLevel)
            {
                int delta = Mathf.Max(1, newLevel - lastKnownLevel);
                GrantSkillPoints(skillPointsPerLevel * delta);
                notified = true;
            }

            lastKnownLevel = Mathf.Max(lastKnownLevel, newLevel);
            UpdateUnlockedTreeSlots();

            if (!notified)
            {
                SkillDataChanged?.Invoke();
            }
        }

        void HandleExperienceChanged(int level, int currentXp, int requiredXp)
        {
            SkillDataChanged?.Invoke();
        }

        public IReadOnlyList<SkillTreeDefinition> Trees => availableTrees;
        public IReadOnlyList<SkillTreeDefinition> AvailableTrees => availableTrees;
        public PlayerExperience Experience => playerExperience;
        public int CurrentLevel => playerExperience ? playerExperience.CurrentLevel : 0;
        public int AvailableSkillPoints => Mathf.Max(0, skillPoints);
        public AbilityRunner AbilityRunner => abilityRunner;
        public int UnlockedTreeSlots => _unlockedTreeSlots > 0 ? _unlockedTreeSlots : Mathf.Max(1, startingTreeSlots);
        public int SelectedTreeCount => _selectedTreeIds.Count;
        public int RemainingTreeSelections => Mathf.Max(0, UnlockedTreeSlots - SelectedTreeCount);

        public SkillTreeState GetState(SkillTreeDefinition tree)
        {
            if (!tree)
            {
                return null;
            }

            if (_treeStates.TryGetValue(tree.TreeId, out SkillTreeState state))
            {
                return state;
            }

            state = new SkillTreeState(tree.TreeId);
            _treeStates.Add(tree.TreeId, state);
            return state;
        }

        public bool IsUnlocked(SkillTreeDefinition tree, SkillNodeDefinition node)
        {
            var state = GetState(tree);
            if (state == null || node == null)
            {
                return false;
            }

            return state.IsUnlocked(node.NodeId);
        }

        public int GetRank(SkillTreeDefinition tree, SkillNodeDefinition node)
        {
            var state = GetState(tree);
            if (state == null || node == null)
            {
                return 0;
            }

            return state.GetRank(node.NodeId);
        }

        public int GetMaxRank(SkillTreeDefinition tree, SkillNodeDefinition node)
        {
            return ResolveMaxRank(node);
        }

        public int GetTotalPointsInvested(SkillTreeDefinition tree)
        {
            var state = GetState(tree);
            return state != null ? state.GetTotalPointsInvested() : 0;
        }

        public void GetSelectedTrees(List<SkillTreeDefinition> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();

            for (int i = 0; i < _selectedTreeIds.Count; i++)
            {
                var tree = ResolveTreeById(_selectedTreeIds[i]);
                if (tree)
                {
                    buffer.Add(tree);
                }
            }
        }

        public bool IsTreeSelected(SkillTreeDefinition tree)
        {
            if (!tree) return false;
            string id = tree.TreeId;
            for (int i = 0; i < _selectedTreeIds.Count; i++)
            {
                if (_selectedTreeIds[i] == id)
                {
                    return true;
                }
            }
            return false;
        }

        public bool CanSelectTree(SkillTreeDefinition tree)
        {
            if (tree == null) return false;
            if (SelectedTreeCount >= UnlockedTreeSlots) return false;
            if (!IsTreeAvailable(tree)) return false;
            return !IsTreeSelected(tree);
        }

        public bool TrySelectTree(SkillTreeDefinition tree)
        {
            return TrySelectTreeInternal(tree, suppressEvents: false);
        }

        public bool CanUnlock(SkillTreeDefinition tree, SkillNodeDefinition node)
        {
            if (tree == null || node == null)
            {
                return false;
            }

            SkillTreeState state = GetState(tree);
            if (state == null)
            {
                return false;
            }

            int maxRank = ResolveMaxRank(node);
            int currentRank = state.GetRank(node.NodeId);
            if (currentRank >= maxRank)
            {
                return false;
            }

            if (CurrentLevel < node.RequiredLevel)
            {
                return false;
            }

            // Check points invested requirement (only for first rank unlock)
            bool isFirstRank = currentRank == 0;
            if (isFirstRank && tree.RequirePointsInTree)
            {
                int pointsInvested = state.GetTotalPointsInvested();
                int requiredPoints = node.GetRequiredPointsInTree(tree);
                if (pointsInvested < requiredPoints)
                {
                    return false;
                }
            }

            if (isFirstRank)
            {
                var prerequisites = node.PrerequisiteNodeIds;
                if (prerequisites != null && prerequisites.Count > 0)
                {
                    for (int i = 0; i < prerequisites.Count; i++)
                    {
                        if (!state.IsUnlocked(prerequisites[i]))
                        {
                            return false;
                        }
                    }
                }
            }

            if (node.ConsumesSkillPoint && AvailableSkillPoints <= 0)
            {
                return false;
            }

            return true;
        }

        public bool TryUnlock(SkillTreeDefinition tree, SkillNodeDefinition node)
        {
            if (tree == null || node == null)
            {
                return false;
            }

            SkillTreeState state = GetState(tree);
            if (state == null)
            {
                return false;
            }

            int maxRank = ResolveMaxRank(node);
            int currentRank = state.GetRank(node.NodeId);
            if (currentRank >= maxRank)
            {
                return false;
            }

            if (!CanUnlock(tree, node))
            {
                return false;
            }

            if (node.ConsumesSkillPoint && !SpendSkillPoint())
            {
                return false;
            }

            int nextRank = Mathf.Clamp(currentRank + 1, 1, maxRank);
            state.SetRank(node.NodeId, nextRank);

            bool firstUnlock = currentRank == 0;
            ApplyNodeRankToAbility(node, nextRank);

            if (firstUnlock)
            {
                SkillUnlocked?.Invoke(tree, node);
            }

            SkillRankChanged?.Invoke(tree, node, nextRank);
            SkillDataChanged?.Invoke();
            return true;
        }

        public void GrantSkillPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            long next = (long)skillPoints + amount;
            skillPoints = (int)Mathf.Clamp(next, 0, int.MaxValue);
            SkillPointsChanged?.Invoke(skillPoints);
            SkillDataChanged?.Invoke();
        }

        bool SpendSkillPoint()
        {
            if (skillPoints <= 0)
            {
                return false;
            }

            skillPoints--;
            SkillPointsChanged?.Invoke(skillPoints);
            return true;
        }

        /// <summary>
        /// Resets all skill node ranks to 0 across all trees, removes all granted abilities,
        /// and refunds the spent skill points back to the player's available pool.
        /// </summary>
        public void ResetAllSkillsAndRefund()
        {
            // Build a unique set of trees to process:
            // - those configured on this manager
            // - plus any SkillTreeDefinition assets present in the project (covers UI-provided lists)
            var treesById = new Dictionary<string, SkillTreeDefinition>();
            if (availableTrees != null)
            {
                for (int i = 0; i < availableTrees.Count; i++)
                {
                    var t = availableTrees[i];
                    if (!t || string.IsNullOrEmpty(t.TreeId)) continue;
                    if (!treesById.ContainsKey(t.TreeId)) treesById.Add(t.TreeId, t);
                }
            }

            var allDefs = Resources.FindObjectsOfTypeAll<SkillTreeDefinition>();
            if (allDefs != null)
            {
                for (int i = 0; i < allDefs.Length; i++)
                {
                    var t = allDefs[i];
                    if (!t || string.IsNullOrEmpty(t.TreeId)) continue;
                    if (!treesById.ContainsKey(t.TreeId)) treesById.Add(t.TreeId, t);
                }
            }

            // 1) Calculate total spent points
            int refund = 0;
            foreach (var kv in treesById)
            {
                var tree = kv.Value;
                var state = GetState(tree);
                if (state != null) refund += Mathf.Max(0, state.GetTotalPointsInvested());
            }

            // 2) Remove all abilities granted by nodes and zero-out ranks
            foreach (var kv in treesById)
            {
                var tree = kv.Value;
                var nodes = tree.Nodes;
                if (nodes != null)
                {
                    for (int n = 0; n < nodes.Count; n++)
                    {
                        RemoveNodeAbilities(nodes[n]);
                    }
                }

                var state = GetState(tree);
                if (state != null)
                {
                    foreach (var s in state.EnumerateStates())
                    {
                        if (s != null) s.Rank = 0;
                    }
                }
            }

            // 3) Refund points
            if (refund > 0)
            {
                GrantSkillPoints(refund);
            }

            // 4) Notify and resync (ensures UI/slots reflect cleared state)
            SkillDataChanged?.Invoke();
            SkillPointsChanged?.Invoke(skillPoints);
            SyncAbilityRanksWithStates();
        }

        void ApplyNodeRankToAbility(SkillNodeDefinition node, int rank, bool autoAssignIfUnassigned = true)
        {
            if (node == null)
            {
                return;
            }

            int maxRank = Mathf.Max(1, ResolveMaxRank(node));
            rank = Mathf.Clamp(rank, 1, maxRank);

            AbilityDefinition targetAbility = node.GetAbilityForRank(rank);
            bool targetIsPassive = targetAbility != null && targetAbility.IsPassive;

            AbilityDefinition slotAbility = null;
            int slotIndex = -1;
            if (abilitySlotManager)
            {
                foreach (var candidate in node.EnumerateRankAbilities())
                {
                    if (!candidate) continue;
                    int index = abilitySlotManager.FindSlotContaining(candidate);
                    if (index >= 0)
                    {
                        slotAbility = candidate;
                        slotIndex = index;
                        break;
                    }
                }
            }

            foreach (var candidate in node.EnumerateRankAbilities())
            {
                if (!candidate) continue;
                if (candidate == targetAbility) continue;

                abilityRunner?.TryRemoveAbility(candidate);

                if (abilitySlotManager && candidate != slotAbility)
                {
                    abilitySlotManager.RemoveAbility(candidate);
                }
            }

            if (targetAbility)
            {
                abilityRunner?.TryAddAbility(targetAbility);

                if (abilitySlotManager)
                {
                    if (targetIsPassive)
                    {
                        if (slotIndex >= 0)
                        {
                            abilitySlotManager.ReplaceAbility(slotAbility, null);
                        }
                    }
                    else if (slotIndex >= 0 && slotAbility != targetAbility)
                    {
                        abilitySlotManager.ReplaceAbility(slotAbility, targetAbility);
                    }
                    else if (slotIndex < 0 && autoAssignIfUnassigned)
                    {
                        abilitySlotManager.TryAutoAssign(targetAbility);
                    }
                }
            }
            else if (slotIndex >= 0 && abilitySlotManager)
            {
                abilitySlotManager.ReplaceAbility(slotAbility, null);
            }
        }

        void RemoveNodeAbilities(SkillNodeDefinition node)
        {
            if (node == null)
            {
                return;
            }

            foreach (var ability in node.EnumerateRankAbilities())
            {
                if (!ability) continue;
                abilitySlotManager?.RemoveAbility(ability);
                abilityRunner?.TryRemoveAbility(ability);
            }
        }

        void SyncAbilityRanksWithStates()
        {
            for (int i = 0; i < availableTrees.Count; i++)
            {
                SkillTreeDefinition tree = availableTrees[i];
                if (!tree) continue;

                SkillTreeState state = GetState(tree);
                if (state == null) continue;

                var nodes = tree.Nodes;
                if (nodes == null) continue;

                for (int n = 0; n < nodes.Count; n++)
                {
                    SkillNodeDefinition node = nodes[n];
                    if (node == null) continue;

                    int rank = state.GetRank(node.NodeId);
                    if (rank > 0)
                    {
                        ApplyNodeRankToAbility(node, rank, autoAssignIfUnassigned: true);
                    }
                    else
                    {
                        RemoveNodeAbilities(node);
                    }
                }
            }
        }

        int ResolveMaxRank(SkillNodeDefinition node)
        {
            return node != null ? node.MaxRank : 1;
        }

        void ApplyStartingSelections()
        {
            if (startingSelectedTrees == null || startingSelectedTrees.Count == 0)
            {
                return;
            }

            bool addedAny = false;
            for (int i = 0; i < startingSelectedTrees.Count; i++)
            {
                var tree = startingSelectedTrees[i];
                if (tree && TrySelectTreeInternal(tree, suppressEvents: true))
                {
                    addedAny = true;
                }
            }

            if (addedAny)
            {
                SelectedTreesChanged?.Invoke();
                SkillDataChanged?.Invoke();
            }
        }

        bool TrySelectTreeInternal(SkillTreeDefinition tree, bool suppressEvents)
        {
            if (!CanSelectTree(tree))
            {
                return false;
            }

            _selectedTreeIds.Add(tree.TreeId);
            if (!suppressEvents)
            {
                SelectedTreesChanged?.Invoke();
                SkillDataChanged?.Invoke();
            }
            return true;
        }

        bool IsTreeAvailable(SkillTreeDefinition tree)
        {
            if (tree == null) return false;
            for (int i = 0; i < availableTrees.Count; i++)
            {
                if (availableTrees[i] == tree)
                {
                    return true;
                }
            }
            return false;
        }

        SkillTreeDefinition ResolveTreeById(string treeId)
        {
            if (string.IsNullOrEmpty(treeId)) return null;
            for (int i = 0; i < availableTrees.Count; i++)
            {
                SkillTreeDefinition tree = availableTrees[i];
                if (tree && tree.TreeId == treeId)
                {
                    return tree;
                }
            }
            return null;
        }

        void UpdateUnlockedTreeSlots(bool forceNotify = false)
        {
            int desired = CalculateUnlockedTreeSlots(CurrentLevel);
            if (forceNotify || desired != _unlockedTreeSlots)
            {
                _unlockedTreeSlots = desired;
                TreeSlotsChanged?.Invoke(_unlockedTreeSlots);
            }
        }

        int CalculateUnlockedTreeSlots(int level)
        {
            int slots = Mathf.Max(1, startingTreeSlots);
            if (additionalTreeSlotUnlockLevels != null)
            {
                for (int i = 0; i < additionalTreeSlotUnlockLevels.Count; i++)
                {
                    if (level >= additionalTreeSlotUnlockLevels[i])
                    {
                        slots++;
                    }
                }
            }

            return slots;
        }
    }
}





