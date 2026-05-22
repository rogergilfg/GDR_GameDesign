using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem
{
    public static class AbilityRankUtility
    {
        public static int ClampRank(int rank, int maxRank)
        {
            if (maxRank < 1) maxRank = 1;
            return Mathf.Clamp(rank, 1, maxRank);
        }
    }

    [Serializable]
    public struct RankedInt
    {
        [SerializeField] private int baseValue;
        [SerializeField] private List<int> rankValues;

        public RankedInt(int baseValue)
        {
            this.baseValue = baseValue;
            rankValues = new List<int>();
        }

        public static RankedInt FromBase(int value) => new RankedInt(value);

        public int BaseValue
        {
            get => baseValue;
            set => baseValue = value;
        }

        public List<int> RankValues => rankValues ?? (rankValues = new List<int>());

        public bool HasOverrides => rankValues != null && rankValues.Count > 0;

        public int GetValue(int rank)
        {
            if (rank <= 1 || rankValues == null || rankValues.Count == 0)
            {
                return baseValue;
            }

            int index = rank - 2;
            if (index >= 0 && index < rankValues.Count)
            {
                return rankValues[index];
            }

            return rankValues[rankValues.Count - 1];
        }
    }

    [Serializable]
    public struct RankedFloat
    {
        [SerializeField] private float baseValue;
        [SerializeField] private List<float> rankValues;

        public RankedFloat(float baseValue)
        {
            this.baseValue = baseValue;
            rankValues = new List<float>();
        }

        public static RankedFloat FromBase(float value) => new RankedFloat(value);

        public float BaseValue
        {
            get => baseValue;
            set => baseValue = value;
        }

        public List<float> RankValues => rankValues ?? (rankValues = new List<float>());

        public bool HasOverrides => rankValues != null && rankValues.Count > 0;

        public float GetValue(int rank)
        {
            if (rank <= 1 || rankValues == null || rankValues.Count == 0)
            {
                return baseValue;
            }

            int index = rank - 2;
            if (index >= 0 && index < rankValues.Count)
            {
                return rankValues[index];
            }

            return rankValues[rankValues.Count - 1];
        }
    }
}







