namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;

/// <summary>
/// Central list of gear items used by random drop systems.
/// </summary>
[CreateAssetMenu(fileName = "GearItemDatabase", menuName = "Gear/Gear Item Database")]
[MovedFrom(true, null, null, "GearItemDatabase")]
public class GearItemDatabase : ScriptableObject
{
    [SerializeField]
    [Tooltip("All gear items that can be referenced by drop tables or rewards.")]
    private List<GearItem> items = new List<GearItem>();

    /// <summary>
    /// Items contained in this database.
    /// </summary>
    public IReadOnlyList<GearItem> Items => items;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (items == null) return;
        items.RemoveAll(item => item == null);
    }
#endif
}



}






