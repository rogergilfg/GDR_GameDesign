using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry listing all available resource types. Assign this to the
/// DynamicResourceManager so runtime systems can resolve resources consistently.
/// </summary>
[CreateAssetMenu(fileName = "ResourceDatabase", menuName = "Resources/Resource Database")]
[MovedFrom(true, null, null, "ResourceDatabase")]
public class ResourceDatabase : ScriptableObject
{
    [SerializeField]
    private List<ResourceTypeDef> resources = new List<ResourceTypeDef>();

    private readonly Dictionary<string, ResourceTypeDef> byId = new Dictionary<string, ResourceTypeDef>();

    public IReadOnlyList<ResourceTypeDef> Resources => resources;

    private void OnEnable()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        byId.Clear();
        if (resources == null) return;
        foreach (var def in resources)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.Id)) continue;
            if (!byId.ContainsKey(def.Id))
            {
                byId.Add(def.Id, def);
            }
        }
    }

    public bool TryGetById(string id, out ResourceTypeDef def)
    {
        if (string.IsNullOrWhiteSpace(id)) { def = null; return false; }
        return byId.TryGetValue(id, out def);
    }

    /// <summary>
    /// Attempts to find a resource type whose name matches the legacy enum name.
    /// </summary>
    // Legacy resolution removed
}


}




