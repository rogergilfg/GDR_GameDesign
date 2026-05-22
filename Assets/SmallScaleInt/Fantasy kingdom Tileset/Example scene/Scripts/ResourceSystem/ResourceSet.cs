using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serializable, extensible container mapping resource types to amounts.
/// </summary>
[Serializable]
[MovedFrom(true, null, null, "ResourceAmount")]
public struct ResourceAmount
{
    public ResourceTypeDef type;
    public int amount;
}

[Serializable]
[MovedFrom(true, null, null, "ResourceSet")]
public class ResourceSet
{
    [SerializeField]
    private List<ResourceAmount> amounts = new List<ResourceAmount>();

    public IReadOnlyList<ResourceAmount> Amounts => amounts;

    public bool IsEmpty
    {
        get
        {
            if (amounts == null || amounts.Count == 0) return true;
            for (int i = 0; i < amounts.Count; i++)
            {
                if (amounts[i].type != null && amounts[i].amount > 0) return false;
            }
            return true;
        }
    }

    public int Get(ResourceTypeDef type)
    {
        if (type == null || amounts == null) return 0;
        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i].type == type) return amounts[i].amount;
        }
        return 0;
    }

    public void Set(ResourceTypeDef type, int value)
    {
        if (type == null) return;
        value = Mathf.Max(0, value);
        if (amounts == null) amounts = new List<ResourceAmount>();
        for (int i = 0; i < amounts.Count; i++)
        {
            if (amounts[i].type == type)
            {
                var a = amounts[i];
                a.amount = value;
                amounts[i] = a;
                return;
            }
        }
        amounts.Add(new ResourceAmount { type = type, amount = value });
    }

    public void Add(ResourceTypeDef type, int delta)
    {
        if (type == null || delta == 0) return;
        int current = Get(type);
        Set(type, current + delta);
    }

    // Legacy conversion removed
}


}




