using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SkillSystem
{
    [CreateAssetMenu(menuName = "Skill System/Skill Tree", fileName = "SkillTree")]
    public sealed class SkillTreeDefinition : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Optional identifier used when referencing this tree in saves or UI.")]
        private string treeId = Guid.NewGuid().ToString();

        [SerializeField]
        [Tooltip("Display name presented to the player.")]
        private string displayName = "New Skill Tree";

        [SerializeField, TextArea(2, 4)]
        [Tooltip("Short description shown when the player chooses a skill tree.")]
        private string skillTreeDescription = string.Empty;

        [SerializeField]
        [Tooltip("Icon used in the skill panel.")]
        private Sprite icon;

        [Header("Layout")]
        [SerializeField]
        [Tooltip("If enabled, this tree uses the custom layout settings below when rendered.")]
        private bool overrideLayout = false;

        [SerializeField]
        [Tooltip("Size of each cell in the skill panel grid (width, height).")]
        private Vector2 layoutCellSize = new Vector2(160f, 160f);

        [SerializeField]
        [Tooltip("Spacing between cells (horizontal, vertical).")]
        private Vector2 layoutSpacing = new Vector2(12f, 12f);

        [SerializeField]
        [Tooltip("Padding around the content area (left, right, top, bottom).")]
        private Vector4 layoutPadding = new Vector4(16f, 16f, 16f, 16f);

        [Header("Point Investment Requirements")]
        [SerializeField]
        [Tooltip("When enabled, nodes require a certain number of points invested in the tree before they can be unlocked.")]
        private bool requirePointsInTree = false;

        [SerializeField]
        [Min(1)]
        [Tooltip("Number of points required to unlock each additional row. Row 0 = 0 points, Row 1 = pointsPerRow, Row 2 = pointsPerRow * 2, etc.")]
        private int pointsPerRow = 5;

        [SerializeField]
        [Tooltip("Ordered collection of nodes that belong to this tree.")]
        private List<SkillNodeDefinition> nodes = new List<SkillNodeDefinition>();

        public string TreeId => string.IsNullOrEmpty(treeId) ? (treeId = Guid.NewGuid().ToString()) : treeId;
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }

                return nodes != null && nodes.Count > 0 && nodes[0] != null
                    ? nodes[0].DisplayName
                    : "Skill Tree";
            }
        }
        public string Description => skillTreeDescription;
        public Sprite Icon => icon;
        public bool OverrideLayout => overrideLayout;
        public Vector2 LayoutCellSize => layoutCellSize;
        public Vector2 LayoutSpacing => layoutSpacing;
        public Vector4 LayoutPadding => layoutPadding;
        public bool RequirePointsInTree => requirePointsInTree;
        public int PointsPerRow => Mathf.Max(1, pointsPerRow);
        public IReadOnlyList<SkillNodeDefinition> Nodes => nodes;

        /// <summary>
        /// Calculates the required points for a node based on its row position.
        /// Row 0 = 0 points, Row 1 = pointsPerRow, Row 2 = pointsPerRow * 2, etc.
        /// </summary>
        public int CalculateRequiredPointsForRow(int row)
        {
            if (!requirePointsInTree || row <= 0)
            {
                return 0;
            }
            return row * PointsPerRow;
        }

        public void GetLayout(out Vector2 cellSize, out Vector2 spacing, out Vector4 padding)
        {
            cellSize = layoutCellSize;
            spacing = layoutSpacing;
            padding = layoutPadding;
        }

        public SkillNodeDefinition GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || nodes == null)
            {
                return null;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].NodeId == nodeId)
                {
                    return nodes[i];
                }
            }

            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (nodes == null)
            {
                nodes = new List<SkillNodeDefinition>();
            }

            layoutCellSize.x = Mathf.Max(1f, layoutCellSize.x);
            layoutCellSize.y = Mathf.Max(1f, layoutCellSize.y);
            layoutSpacing.x = Mathf.Max(0f, layoutSpacing.x);
            layoutSpacing.y = Mathf.Max(0f, layoutSpacing.y);
            layoutPadding.x = Mathf.Max(0f, layoutPadding.x);
            layoutPadding.y = Mathf.Max(0f, layoutPadding.y);
            layoutPadding.z = Mathf.Max(0f, layoutPadding.z);
            layoutPadding.w = Mathf.Max(0f, layoutPadding.w);

            pointsPerRow = Mathf.Max(1, pointsPerRow);

            HashSet<string> existingIds = new HashSet<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                SkillNodeDefinition node = nodes[i];
                if (node == null)
                {
                    nodes[i] = new SkillNodeDefinition();
                    node = nodes[i];
                }

                node.Validate();

                if (!existingIds.Add(node.NodeId))
                {
                    node.ForceNewGuid();
                    existingIds.Add(node.NodeId);
                }
            }

            if (string.IsNullOrEmpty(treeId))
            {
                treeId = Guid.NewGuid().ToString();
            }
        }
#endif
    }

    [Serializable]
    public sealed class SkillNodeDefinition
    {
        [SerializeField]
        private string nodeId = Guid.NewGuid().ToString();

        [SerializeField]
        [Tooltip("Display name for this skill node.")]
        private string displayName = "New Skill";

        [SerializeField]
        [Tooltip("Ability granted when the node is unlocked (optional).")]
        private AbilityDefinition grantedAbility;

        [SerializeField]
        [Tooltip("Higher rank abilities unlocked by this node (Rank 2 = element 0, Rank 3 = element 1, etc.).")]
        private List<AbilityDefinition> additionalRankAbilities = new List<AbilityDefinition>();

        [SerializeField]
        [Tooltip("Minimum player level required before this node can be unlocked.")]
        private int requiredLevel = 1;

        [SerializeField]
        [Tooltip("Number of points that must be invested in the tree before this node becomes available (only used if tree has RequirePointsInTree enabled).")]
        private int requiredPointsInTree = 0;

        [SerializeField]
        [Tooltip("List of node ids that must be unlocked before this node becomes available.")]
        private List<string> prerequisiteNodeIds = new List<string>();

        [SerializeField]
        [Tooltip("Editor/UI position used by the skill panel for layout.")]
        private Vector2 editorPosition = Vector2.zero;

        [SerializeField]
        [Tooltip("If false, unlocking this node will not spend a skill point.")]
        private bool consumesSkillPoint = true;

        public string NodeId => string.IsNullOrEmpty(nodeId) ? (nodeId = Guid.NewGuid().ToString()) : nodeId;
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName))
                {
                    return displayName;
                }

                var ability = GrantedAbility;
                return ability ? ability.DisplayName : "New Skill";
            }
        }
        public Sprite Icon
        {
            get
            {
                var ability = GrantedAbility;
                return ability ? ability.Icon : null;
            }
        }
        public AbilityDefinition GrantedAbility
        {
            get
            {
                if (grantedAbility) return grantedAbility;
                if (additionalRankAbilities != null)
                {
                    for (int i = 0; i < additionalRankAbilities.Count; i++)
                    {
                        if (additionalRankAbilities[i]) return additionalRankAbilities[i];
                    }
                }
                return null;
            }
        }
        public IReadOnlyList<AbilityDefinition> AdditionalRankAbilities => additionalRankAbilities;
        public int MaxRank => Mathf.Max(1, GetAbilityRankCount());
        public int RequiredLevel => Mathf.Max(1, requiredLevel);
        public int RequiredPointsInTree => Mathf.Max(0, requiredPointsInTree);
        public IReadOnlyList<string> PrerequisiteNodeIds => prerequisiteNodeIds;
        public Vector2 EditorPosition => editorPosition;
        public bool ConsumesSkillPoint => consumesSkillPoint;

        /// <summary>
        /// Gets the required points for this node based on the tree's settings and this node's row position.
        /// </summary>
        public int GetRequiredPointsInTree(SkillTreeDefinition tree)
        {
            if (tree == null || !tree.RequirePointsInTree)
            {
                return 0;
            }
            int row = Mathf.RoundToInt(editorPosition.y);
            return tree.CalculateRequiredPointsForRow(row);
        }

        public AbilityDefinition GetAbilityForRank(int rank)
        {
            if (rank <= 0) return null;
            if (rank == 1)
            {
                if (grantedAbility)
                {
                    return grantedAbility;
                }

                if (additionalRankAbilities != null)
                {
                    for (int i = 0; i < additionalRankAbilities.Count; i++)
                    {
                        if (additionalRankAbilities[i])
                        {
                            return additionalRankAbilities[i];
                        }
                    }
                }

                return null;
            }

            if (additionalRankAbilities == null)
            {
                return null;
            }

            int index = rank - 2;
            if (index >= 0 && index < additionalRankAbilities.Count)
            {
                return additionalRankAbilities[index];
            }

            return null;
        }

        public IEnumerable<AbilityDefinition> EnumerateRankAbilities()
        {
            if (grantedAbility)
            {
                yield return grantedAbility;
            }

            if (additionalRankAbilities != null)
            {
                for (int i = 0; i < additionalRankAbilities.Count; i++)
                {
                    var ability = additionalRankAbilities[i];
                    if (ability)
                    {
                        yield return ability;
                    }
                }
            }
        }

        int GetAbilityRankCount()
        {
            int count = 0;
            if (grantedAbility) count++;
            if (additionalRankAbilities != null)
            {
                for (int i = 0; i < additionalRankAbilities.Count; i++)
                {
                    if (additionalRankAbilities[i]) count++;
                }
            }
            return count;
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                nodeId = Guid.NewGuid().ToString();
            }

            requiredLevel = Mathf.Max(1, requiredLevel);
            requiredPointsInTree = Mathf.Max(0, requiredPointsInTree);

            if (prerequisiteNodeIds == null)
            {
                prerequisiteNodeIds = new List<string>();
            }

            if (additionalRankAbilities == null)
            {
                additionalRankAbilities = new List<AbilityDefinition>();
            }
        }

        public void ForceNewGuid()
        {
            nodeId = Guid.NewGuid().ToString();
        }
    }
}





