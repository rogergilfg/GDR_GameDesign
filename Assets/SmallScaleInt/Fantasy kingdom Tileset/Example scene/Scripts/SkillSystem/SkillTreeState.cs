using System;
using System.Collections.Generic;
using UnityEngine;

namespace SkillSystem
{
    [Serializable]
    public sealed class SkillTreeState
    {
        [UnityEngine.SerializeField]
        private string treeId;

        [UnityEngine.SerializeField]
        private List<SkillNodeState> nodeStates = new List<SkillNodeState>();

        private readonly Dictionary<string, SkillNodeState> _cache = new Dictionary<string, SkillNodeState>();

        public SkillTreeState()
        {
        }

        public SkillTreeState(string id)
        {
            treeId = id;
        }

        public string TreeId => treeId;

        public bool IsUnlocked(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return false;
            }

            return TryGetState(nodeId, out SkillNodeState state) && state != null && state.Rank > 0;
        }

        public void SetUnlocked(string nodeId, bool value)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            SkillNodeState state = GetOrCreateState(nodeId);
            if (value)
            {
                state.Rank = Mathf.Max(1, state.Rank);
            }
            else
            {
                state.Rank = 0;
            }
        }

        public bool TryGetState(string nodeId, out SkillNodeState state)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                state = null;
                return false;
            }

            if (_cache.TryGetValue(nodeId, out state))
            {
                return state != null;
            }

            for (int i = 0; i < nodeStates.Count; i++)
            {
                SkillNodeState entry = nodeStates[i];
                if (entry != null && entry.NodeId == nodeId)
                {
                    _cache[nodeId] = entry;
                    state = entry;
                    return true;
                }
            }

            state = null;
            return false;
        }

        public IEnumerable<SkillNodeState> EnumerateStates()
        {
            return nodeStates;
        }

        public int GetRank(string nodeId)
        {
            if (TryGetState(nodeId, out SkillNodeState state) && state != null)
            {
                return state.Rank;
            }

            return 0;
        }

        public void SetRank(string nodeId, int rank)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                return;
            }

            SkillNodeState state = GetOrCreateState(nodeId);
            state.Rank = rank;
        }

        /// <summary>
        /// Calculates the total number of skill points invested in this tree.
        /// This counts each rank of each unlocked node.
        /// </summary>
        public int GetTotalPointsInvested()
        {
            int total = 0;
            for (int i = 0; i < nodeStates.Count; i++)
            {
                if (nodeStates[i] != null)
                {
                    total += nodeStates[i].Rank;
                }
            }
            return total;
        }

        SkillNodeState GetOrCreateState(string nodeId)
        {
            if (TryGetState(nodeId, out SkillNodeState state) && state != null)
            {
                return state;
            }

            state = new SkillNodeState(nodeId);
            nodeStates.Add(state);
            _cache[nodeId] = state;
            return state;
        }
    }

    [Serializable]
    public sealed class SkillNodeState
    {
        [UnityEngine.SerializeField]
        private string nodeId;

        [UnityEngine.SerializeField]
        private bool unlocked;

        [UnityEngine.SerializeField]
        private int rank;

        public SkillNodeState()
        {
        }

        public SkillNodeState(string id)
        {
            nodeId = id;
        }

        public string NodeId => nodeId;
        public bool Unlocked
        {
            get => Rank > 0;
            set => Rank = value ? Mathf.Max(1, Rank) : 0;
        }

        public int Rank
        {
            get
            {
                if (rank <= 0 && unlocked)
                {
                    return 1;
                }

                return Mathf.Max(0, rank);
            }
            set
            {
                int clamped = Mathf.Max(0, value);
                rank = clamped;
                unlocked = clamped > 0;
            }
        }
    }
}





