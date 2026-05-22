using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.Building
{
/// <summary>
/// Centralises build-part unlocking based on resources the player has discovered.
/// Tiles register themselves with this service so UI and placement logic can query availability.
/// </summary>
[MovedFrom(true, null, null, "BuildUnlockService")]
public static class BuildUnlockService
{
    private static readonly HashSet<DestructibleTileData> registeredDefinitions = new HashSet<DestructibleTileData>();
    private static readonly HashSet<DestructibleTileData> unlockedDefinitions = new HashSet<DestructibleTileData>();
    private static readonly Dictionary<ResourceTypeDef, List<DestructibleTileData>> waitingByResource = new Dictionary<ResourceTypeDef, List<DestructibleTileData>>();

    private static ResourceSet lastKnownResources;

    /// <summary>
    /// Raised each time a tile is unlocked for the first time.
    /// </summary>
    public static event Action<DestructibleTileData> OnDefinitionUnlocked;

    /// <summary>
    /// Raised whenever the unlock state changes (one or more tiles unlocked).
    /// </summary>
    public static event Action OnUnlocksChanged;

    /// <summary>
    /// Registers a tile definition so it can participate in the unlock system.
    /// Safe to call multiple times; subsequent calls are ignored.
    /// </summary>
    /// <param name="definition">Tile definition to register.</param>
    public static void RegisterDefinition(DestructibleTileData definition)
    {
        if (definition == null || registeredDefinitions.Contains(definition))
        {
            return;
        }

        if (!definition.IsBuildable)
        {
            return;
        }

        registeredDefinitions.Add(definition);

        if (!definition.RequiresUnlock)
        {
            UnlockDefinition(definition);
            return;
        }

        ResourceTypeDef resource = definition.UnlockResourceRequirement;
        if (resource == null)
        {
            UnlockDefinition(definition);
            return;
        }

        if (!waitingByResource.TryGetValue(resource, out List<DestructibleTileData> list))
        {
            list = new List<DestructibleTileData>();
            waitingByResource.Add(resource, list);
        }

        if (!list.Contains(definition))
        {
            list.Add(definition);
        }

        TryUnlockImmediately(definition, resource);
    }

    /// <summary>
    /// Registers a sequence of tile definitions.
    /// </summary>
    /// <param name="definitions">Definitions to register.</param>
    public static void RegisterDefinitions(IEnumerable<DestructibleTileData> definitions)
    {
        if (definitions == null)
        {
            return;
        }

        foreach (DestructibleTileData def in definitions)
        {
            RegisterDefinition(def);
        }
    }

    /// <summary>
    /// Returns true when the provided definition is currently unlocked.
    /// Tiles without an unlock requirement are always considered unlocked.
    /// </summary>
    /// <param name="definition">Definition to test.</param>
    public static bool IsUnlocked(DestructibleTileData definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (!definition.RequiresUnlock)
        {
            return true;
        }

        return unlockedDefinitions.Contains(definition);
    }

    /// <summary>
    /// Returns a short UI hint describing how to unlock the provided definition.
    /// </summary>
    /// <param name="definition">Definition to describe.</param>
    public static string GetUnlockHint(DestructibleTileData definition)
    {
        ResourceTypeDef resource = definition != null ? definition.UnlockResourceRequirement : null;
        if (resource == null)
        {
            return string.Empty;
        }

        return $"Unlock: Acquire {resource.DisplayName}";
    }

    /// <summary>
    /// Updates unlock states based on the latest resource snapshot.
    /// Should be called whenever the player's resources change.
    /// </summary>
    /// <param name="resources">Current resources held by the player.</param>
    public static void RefreshUnlocks(ResourceSet resources)
    {
        if (resources == null || resources.Amounts == null)
        {
            return;
        }

        lastKnownResources = resources;

        bool changed = false;
        var amounts = resources.Amounts;
        for (int i = 0; i < amounts.Count; i++)
        {
            ResourceTypeDef resource = amounts[i].type;
            int quantity = amounts[i].amount;
            if (resource == null || quantity <= 0)
            {
                continue;
            }

            if (!waitingByResource.TryGetValue(resource, out List<DestructibleTileData> list) || list.Count == 0)
            {
                continue;
            }

            for (int j = list.Count - 1; j >= 0; j--)
            {
                DestructibleTileData definition = list[j];
                if (definition == null)
                {
                    list.RemoveAt(j);
                    continue;
                }

                if (UnlockDefinition(definition))
                {
                    changed = true;
                }

                list.RemoveAt(j);
            }

            if (list.Count == 0)
            {
                waitingByResource.Remove(resource);
            }
        }

        if (changed)
        {
            OnUnlocksChanged?.Invoke();
        }
    }

    private static bool UnlockDefinition(DestructibleTileData definition)
    {
        if (definition == null)
        {
            return false;
        }

        if (unlockedDefinitions.Contains(definition))
        {
            return false;
        }

        unlockedDefinitions.Add(definition);
        OnDefinitionUnlocked?.Invoke(definition);
        return true;
    }

    private static void TryUnlockImmediately(DestructibleTileData definition, ResourceTypeDef resource)
    {
        if (definition == null || resource == null)
        {
            return;
        }

        bool unlockedNow = false;

        DynamicResourceManager manager = DynamicResourceManager.Instance;
        if (manager != null && manager.Get(resource) > 0)
        {
            unlockedNow = UnlockDefinition(definition);
        }
        else if (lastKnownResources != null && lastKnownResources.Get(resource) > 0)
        {
            unlockedNow = UnlockDefinition(definition);
        }

        if (!unlockedNow)
        {
            return;
        }

        if (waitingByResource.TryGetValue(resource, out List<DestructibleTileData> list))
        {
            list.Remove(definition);
            if (list.Count == 0)
            {
                waitingByResource.Remove(resource);
            }
        }

        OnUnlocksChanged?.Invoke();
    }
}
}





