using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.Balance;

/// <summary>
/// Dynamic, data-driven resource manager. Works with ResourceSet and ResourceTypeDef.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "DynamicResourceManager")]
public class DynamicResourceManager : MonoBehaviour
{
    public static DynamicResourceManager Instance { get; private set; }

    [SerializeField]
    private ResourceDatabase database;


    [Header("Starting Resources (dynamic)")]
    [SerializeField]
    private ResourceSet startingResources;

    private readonly Dictionary<ResourceTypeDef, int> resources = new Dictionary<ResourceTypeDef, int>();
    private readonly HashSet<ResourceTypeDef> unlockedTypes = new HashSet<ResourceTypeDef>();

    public event Action<ResourceSet> OnResourcesUpdated;

    public ResourceDatabase Database => database;
    public IReadOnlyCollection<ResourceTypeDef> UnlockedTypes => unlockedTypes;
    public bool IsUnlocked(ResourceTypeDef type) => type != null && unlockedTypes.Contains(type);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildFromStartingResources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RebuildFromStartingResources()
    {
        resources.Clear();
        unlockedTypes.Clear();
        if (startingResources != null && startingResources.Amounts != null)
        {
            foreach (var amt in startingResources.Amounts)
            {
                if (amt.type == null || amt.amount <= 0) continue;
                resources[amt.type] = Mathf.Max(0, amt.amount);
                unlockedTypes.Add(amt.type);
            }
        }
        Notify();
    }

    public ResourceSet CurrentResources
    {
        get
        {
            var set = new ResourceSet();
            foreach (var kvp in resources)
            {
                set.Set(kvp.Key, kvp.Value);
            }
            return set;
        }
    }

    public int Get(ResourceTypeDef type)
    {
        return (type != null && resources.TryGetValue(type, out int val)) ? val : 0;
    }

    public bool HasResources(ResourceSet cost)
    {
        if (cost == null || cost.IsEmpty) return true;
        var list = cost.Amounts;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.type == null || a.amount <= 0) continue;
            int have = Get(a.type);
            if (have < a.amount) return false;
        }
        return true;
    }

    public bool TrySpendResources(ResourceSet cost, Vector3 worldPos, bool showFeedback = true)
    {
        if (cost == null || cost.IsEmpty) return true;
        if (!HasResources(cost)) return false;

        var list = cost.Amounts;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.type == null || a.amount <= 0) continue;
            resources[a.type] = Mathf.Max(0, Get(a.type) - a.amount);
            // Spending does not lock currencies again; once unlocked they remain visible
            if (showFeedback && CombatTextManager.Instance != null)
            {
                CombatTextManager.Instance.SpawnResourceGain($"-{a.amount} {a.type.DisplayName}", worldPos);
            }
        }

        Notify();
        return true;
    }

    public void GrantResources(ResourceSet grant, Vector3 worldPos, bool showFeedback = true, bool awardExperience = true)
    {
        if (grant == null || grant.IsEmpty) return;
        var list = grant.Amounts;
        int totalExperience = 0;
        float resourceMultiplier = GameBalanceManager.Instance != null ? GameBalanceManager.Instance.ResourceGainMultiplier : 1f;
        resourceMultiplier = Mathf.Max(0f, resourceMultiplier);

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.type == null || a.amount <= 0) continue;
            int adjustedAmount = Mathf.RoundToInt(a.amount * resourceMultiplier);
            if (resourceMultiplier > 0f && adjustedAmount <= 0 && a.amount > 0)
            {
                adjustedAmount = 1;
            }

            if (adjustedAmount <= 0)
            {
                continue;
            }

            resources[a.type] = Mathf.Max(0, Get(a.type) + adjustedAmount);
            unlockedTypes.Add(a.type);
            if (showFeedback && CombatTextManager.Instance != null)
            {
                CombatTextManager.Instance.SpawnResourceGain($"+{adjustedAmount} {a.type.DisplayName}", worldPos);
            }

            if (awardExperience && a.type != null)
            {
                int perUnit = a.type.ExperiencePerUnit;
                if (perUnit > 0 && adjustedAmount > 0)
                {
                    long contribution = (long)perUnit * adjustedAmount;
                    if (contribution > 0)
                    {
                        long newTotal = (long)totalExperience + contribution;
                        if (newTotal > int.MaxValue)
                        {
                            newTotal = int.MaxValue;
                        }

                        totalExperience = (int)newTotal;
                    }
                }
            }
        }

        if (awardExperience && totalExperience > 0)
        {
            if (GameBalanceManager.Instance != null)
            {
                totalExperience = GameBalanceManager.Instance.GetAdjustedResourceExperience(totalExperience);
            }

            if (totalExperience > 0)
            {
                PlayerExperience.GrantStatic(totalExperience, worldPos);
            }
        }

        Notify();
    }

    private void Notify()
    {
        OnResourcesUpdated?.Invoke(CurrentResources);
    }
}


}




