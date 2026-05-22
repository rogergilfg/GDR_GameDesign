namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using SmallScale.FantasyKingdomTileset;

/// <summary>
/// Describes a single loot drop entry. Supports Gear items or dynamic Resources.
/// </summary>
[System.Serializable]
[MovedFrom(true, null, null, "LootDropDefinition")]
public class LootDropDefinition
{
        [MovedFrom(true, null, null, "LootKind")]
        public enum LootKind { Gear, Resource }

    [SerializeField]
    [Tooltip("Type of loot this entry represents.")]
    private LootKind kind = LootKind.Gear;

    [Header("Gear Item")]
    [SerializeField]
    [Tooltip("Item that should be spawned when the loot table is executed (Gear kind).")]
    private GearItem item;

    [SerializeField]
    [Min(1)]
    [Tooltip("Number of copies of the gear item to spawn (Gear kind).")]
    private int quantity = 1;

    [Header("Dynamic Resource")]
    [SerializeField]
    [Tooltip("Resource type to grant when this entry is processed (Resource kind).")]
    private ResourceTypeDef resourceType;

    [SerializeField]
    [Min(1)]
    [Tooltip("Amount of the resource to grant (Resource kind).")]
    private int resourceAmount = 1;

    public LootKind Kind => kind;

    // Gear
    public GearItem Item => item;
    public int Quantity => Mathf.Max(1, quantity);

    // Resource
    public ResourceTypeDef ResourceType => resourceType;
    public int ResourceAmount => Mathf.Max(1, resourceAmount);
}


}





