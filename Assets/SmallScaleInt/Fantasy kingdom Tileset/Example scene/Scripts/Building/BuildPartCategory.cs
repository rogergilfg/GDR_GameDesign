using System;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Categories that group buildable parts displayed in the build menu.
/// </summary>
namespace SmallScale.FantasyKingdomTileset.Building
{
[Serializable]
[MovedFrom(true, null, null, "BuildPartCategory")]
public enum BuildPartCategory
{
    Ground = 0,
    Walls = 1,
    Roof = 2,
    Objects = 3,
    CraftingStations = 4,
}
}




