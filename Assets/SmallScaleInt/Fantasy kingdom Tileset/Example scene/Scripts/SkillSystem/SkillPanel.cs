using System.Collections.Generic;
using UnityEngine;

namespace SkillSystem
{
    public sealed class SkillPanel : MonoBehaviour
    {
        const int MaxColumns = 3;

        [SerializeField]
        private SkillManager skillManager;

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private SkillNodeView nodeViewPrefab;

        [SerializeField]
        private TMPro.TextMeshProUGUI skillPanelName;

        [Header("Layout Defaults")]
        [SerializeField]
        private Vector2 cellSize = new Vector2(160f, 160f);

        [SerializeField]
        private Vector2 cellSpacing = new Vector2(12f, 12f);

        [SerializeField]
        private Vector4 contentPadding = new Vector4(16f, 16f, 16f, 16f);

        private readonly Dictionary<string, SkillNodeView> _activeViews = new Dictionary<string, SkillNodeView>();
        private SkillTreeDefinition _currentTree;

        void OnEnable()
        {
            if (skillManager)
            {
                skillManager.SkillDataChanged += Refresh;
            }

            if (_currentTree)
            {
                Refresh();
            }
        }

        void OnDisable()
        {
            if (skillManager)
            {
                skillManager.SkillDataChanged -= Refresh;
            }
        }

        public void ShowTree(SkillTreeDefinition tree)
        {
            _currentTree = tree;

            // Update the panel name text
            if (skillPanelName != null && tree != null)
            {
                skillPanelName.text = tree.DisplayName;
            }

            Rebuild();
            Refresh();
        }

        public void Clear()
        {
            foreach (var kvp in _activeViews)
            {
                if (kvp.Value)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }

            _activeViews.Clear();
        }

        void EnsureContentRootAnchors()
        {
            if (!contentRoot) return;
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(0f, 1f);
            contentRoot.pivot = new Vector2(0f, 1f);
        }

        void Rebuild()
        {
            Clear();

            if (!_currentTree || !nodeViewPrefab || !contentRoot)
            {
                return;
            }

            var nodes = _currentTree.Nodes;
            if (nodes == null)
            {
                return;
            }

            EnsureContentRootAnchors();

            Vector2 resolvedCellSize = cellSize;
            Vector2 resolvedSpacing = cellSpacing;
            Vector4 resolvedPadding = contentPadding;

            if (_currentTree.OverrideLayout)
            {
                _currentTree.GetLayout(out resolvedCellSize, out resolvedSpacing, out resolvedPadding);
            }

            float cellWidth = Mathf.Max(1f, resolvedCellSize.x);
            float cellHeight = Mathf.Max(1f, resolvedCellSize.y);
            float spacingX = Mathf.Max(0f, resolvedSpacing.x);
            float spacingY = Mathf.Max(0f, resolvedSpacing.y);

            float paddingLeft = Mathf.Max(0f, resolvedPadding.x);
            float paddingRight = Mathf.Max(0f, resolvedPadding.y);
            float paddingTop = Mathf.Max(0f, resolvedPadding.z);
            float paddingBottom = Mathf.Max(0f, resolvedPadding.w);

            int maxCol = 0;
            int maxRow = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                SkillNodeDefinition node = nodes[i];
                if (node == null) continue;

                SkillNodeView view = Instantiate(nodeViewPrefab, contentRoot);
                view.name = $"SkillNode_{node.DisplayName}";
                view.Bind(skillManager, _currentTree, node);

                RectTransform rect = view.RectTransform;
                if (rect)
                {
                    int col = Mathf.Clamp(Mathf.RoundToInt(node.EditorPosition.x), 0, MaxColumns - 1);
                    int row = Mathf.Max(0, Mathf.RoundToInt(node.EditorPosition.y));

                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(paddingLeft + col * (cellWidth + spacingX), -(paddingTop + row * (cellHeight + spacingY)));

                    maxCol = Mathf.Max(maxCol, col);
                    maxRow = Mathf.Max(maxRow, row);
                }

                _activeViews[node.NodeId] = view;
            }

            int totalColumns = Mathf.Max(1, maxCol + 1);
            int totalRows = Mathf.Max(1, maxRow + 1);

            float width = paddingLeft + paddingRight + totalColumns * cellWidth + Mathf.Max(0, totalColumns - 1) * spacingX;
            float height = paddingTop + paddingBottom + totalRows * cellHeight + Mathf.Max(0, totalRows - 1) * spacingY;

            contentRoot.sizeDelta = new Vector2(width, height);
            contentRoot.anchoredPosition = Vector2.zero;
        }

        void Refresh()
        {
            if (!_currentTree || skillManager == null)
            {
                return;
            }

            SkillTreeState state = skillManager.GetState(_currentTree);
            if (state == null)
            {
                return;
            }

            var nodes = _currentTree.Nodes;
            if (nodes == null)
            {
                return;
            }

            int playerLevel = skillManager.CurrentLevel;

            for (int i = 0; i < nodes.Count; i++)
            {
                SkillNodeDefinition node = nodes[i];
                if (node == null) continue;

                if (!_activeViews.TryGetValue(node.NodeId, out SkillNodeView view) || !view)
                {
                    continue;
                }

                int rank = state.GetRank(node.NodeId);
                int maxRank = skillManager.GetMaxRank(_currentTree, node);
                bool canInvest = skillManager.CanUnlock(_currentTree, node);
                bool meetsLevel = playerLevel >= node.RequiredLevel;
                bool prerequisitesMet = true;

                var prerequisites = node.PrerequisiteNodeIds;
                if (prerequisites != null && prerequisites.Count > 0)
                {
                    for (int p = 0; p < prerequisites.Count; p++)
                    {
                        if (!state.IsUnlocked(prerequisites[p]))
                        {
                            prerequisitesMet = false;
                            break;
                        }
                    }
                }

                view.Refresh(rank, maxRank, canInvest, meetsLevel, prerequisitesMet);
            }
        }
    }
}





