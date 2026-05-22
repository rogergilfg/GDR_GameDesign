using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace SmallScale.FantasyKingdomTileset.Building
{
/// <summary>
/// Stores all buildable parts that should appear inside the build menu.
/// </summary>
[CreateAssetMenu(menuName = "Building/Build Catalogue", fileName = "BuildCatalogue")]
[MovedFrom(true, null, null, "BuildCatalogue")]
public sealed class BuildCatalogue : ScriptableObject
{
    [SerializeField]
    [Tooltip("Collection of buildable parts exposed in the build menu.")]
    private List<DestructibleTileData> parts = new List<DestructibleTileData>();

    /// <summary>
    /// Gets a read-only list of every part inside the catalogue.
    /// </summary>
    public IReadOnlyList<DestructibleTileData> Parts => parts;

    private void OnEnable()
    {
        BuildUnlockService.RegisterDefinitions(EnumerateBuildableParts());
    }

    /// <summary>
    /// Retrieves all parts belonging to the provided category.
    /// </summary>
    /// <param name="category">The category to filter by.</param>
    /// <param name="results">Optional buffer to store the matching parts.</param>
    /// <returns>A read-only list of parts matching the category.</returns>
    public IReadOnlyList<DestructibleTileData> GetParts(BuildPartCategory category, List<DestructibleTileData> results = null)
    {
        if (results == null)
        {
            results = new List<DestructibleTileData>();
        }
        else
        {
            results.Clear();
        }

        for (int i = 0; i < parts.Count; i++)
        {
            DestructibleTileData definition = parts[i];
            if (definition != null && definition.IsBuildable && definition.Category == category)
            {
                results.Add(definition);
            }
        }

        return results;
    }

    public IEnumerable<DestructibleTileData> EnumerateBuildableParts()
    {
        for (int i = 0; i < parts.Count; i++)
        {
            var definition = parts[i];
            if (definition != null && definition.IsBuildable)
                yield return definition;
        }
    }
}
}







