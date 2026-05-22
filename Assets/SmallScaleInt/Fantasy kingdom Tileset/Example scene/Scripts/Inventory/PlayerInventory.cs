using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using System;
using System.Collections.Generic;
using UnityEngine;
using SmallScale.FantasyKingdomTileset;

/// <summary>
/// Stores the collection of gear items owned by the player. The inventory keeps
/// track of unequipped gear and notifies listeners whenever its contents change
/// so that UI systems can stay in sync.
/// </summary>
[DisallowMultipleComponent]
[MovedFrom(true, null, null, "PlayerInventory")]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Gear items that should be available to the player when the game starts.")]
    private List<GearItem> startingGear = new List<GearItem>();

    private readonly List<GearItem> items = new List<GearItem>();

    /// <summary>
    /// Invoked whenever the contents of the inventory change.
    /// </summary>
    public event Action InventoryChanged;

    /// <summary>
    /// Provides read only access to the items currently stored in the inventory.
    /// </summary>
    public IReadOnlyList<GearItem> Items => items;

    private void Awake()
    {
        RebuildFromStartingGear();
    }

    private void OnEnable()
    {
        NotifyInventoryChanged();
    }

    /// <summary>
    /// Removes all items currently stored and repopulates the inventory with the starting gear list.
    /// </summary>
    public void RebuildFromStartingGear()
    {
        items.Clear();

        foreach (GearItem gearItem in startingGear)
        {
            if (gearItem == null)
            {
                continue;
            }

            items.Add(gearItem);
        }

        NotifyInventoryChanged();
    }

    /// <summary>
    /// Determines whether the supplied gear item is currently present in the inventory.
    /// </summary>
    public bool Contains(GearItem gearItem)
    {
        return gearItem != null && items.Contains(gearItem);
    }

    /// <summary>
    /// Adds the supplied gear item to the inventory.
    /// </summary>
    public bool Add(GearItem gearItem)
    {
        if (gearItem == null)
        {
            return false;
        }

        items.Add(gearItem);
        NotifyInventoryChanged();
        return true;
    }

    /// <summary>
    /// Removes the supplied gear item from the inventory.
    /// </summary>
    public bool Remove(GearItem gearItem)
    {
        if (gearItem == null)
        {
            return false;
        }

        if (!items.Remove(gearItem))
        {
            return false;
        }

        NotifyInventoryChanged();
        return true;
    }

    private void NotifyInventoryChanged()
    {
        InventoryChanged?.Invoke();
    }
}




}





