using UnityEngine.Scripting.APIUpdating;
namespace SmallScale.FantasyKingdomTileset
{
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Common rarity levels that can be assigned to gear items.
/// </summary>
public enum GearRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Scriptable object representing a single equippable gear item.
/// </summary>
[CreateAssetMenu(fileName = "GearItem", menuName = "Gear/Gear Item")]
[MovedFrom(true, null, null, "GearItem")]
public class GearItem : ScriptableObject
{
    [SerializeField]
    [Tooltip("Unique identifier used to distinguish this gear item in save data or inventory systems.")]
    private string gearId = System.Guid.NewGuid().ToString();

    [SerializeField]
    [Tooltip("Display name presented to players when this gear item is shown in the UI.")]
    private string displayName = "New Gear";

    [SerializeField]
    [Tooltip("Type of slot that this gear item occupies when equipped.")]
    private GearType gearType = GearType.Weapon;

    [SerializeField]
    [Tooltip("Animator controller containing the animations for this gear item.")]
    private RuntimeAnimatorController animatorController;

    [SerializeField]
    [Tooltip("Optional icon displayed in equipment or inventory UI.")]
    private Sprite icon;

    [SerializeField]
    [Tooltip("Sprite displayed in the world when this item is spawned as loot.")]
    private Sprite worldIcon;

    [Header("World Icon Fallback")]
    [SerializeField, Range(0.01f, 2f)]
    [Tooltip("Scale used when falling back to the UI icon as world loot.")]
    private float uiIconWorldScale = 0.15f;

    [Header("Availability")] 
    [SerializeField]
    [Tooltip("If enabled, this item is eligible to be selected by random drop tables.")]
    private bool canAppearInRandomDrops = true;

    [SerializeField]
    [Tooltip("If enabled, this item can be crafted at crafting stations (implemented later).")]
    private bool isCraftable = false;

    [Header("Requirements")]
    [SerializeField, Min(0)]
    [Tooltip("Minimum player level required to equip this item. 0 = no requirement.")]
    private int requiredLevel = 0;

    [SerializeField]
    [Tooltip("Color tint applied to the gear's sprite when equipped.")]
    private Color spriteColor = Color.white;

    [SerializeField]
    [Tooltip("Rarity tier of this gear item. Controls the default color highlights.")]
    private GearRarity rarity = GearRarity.Common;

    [Header("Stats")]
    [SerializeField]
    [Tooltip("Amount of strength granted while this gear is equipped.")]
    private int strength;

    [SerializeField]
    [Tooltip("Amount of defense granted while this gear is equipped.")]
    private int defense;

    [SerializeField]
    [Tooltip("Additional health granted while this gear is equipped.")]
    private int health;

    [SerializeField]
    [Tooltip("Amount of knowledge granted while this gear is equipped.")]
    private int knowledge;

    [SerializeField]
    [Tooltip("Amount of intelligence granted while this gear is equipped.")]
    private int intelligence;

    [SerializeField]
    [Tooltip("Additional damage dealt to destructible tiles while this gear is equipped.")]
    private int tileDamageBonus;

    [SerializeField]
    [Tooltip("Damage dealt while this gear is equipped.")]
    private int damage;

    [SerializeField]
    [Tooltip("Flavor or informational text displayed in tooltips for this gear.")]
    [TextArea]
    private string info;

    [Header("Trading")]
    [SerializeField, Min(0)]
    [Tooltip("Default price players pay when purchasing this item from most traders. Traders can override by configuring a price above zero.")]
    private int defaultPlayerBuyPrice = 0;

    [SerializeField, Min(0)]
    [Tooltip("Default price players receive when selling this item to most traders. Traders can override by configuring a price above zero.")]
    private int defaultPlayerSellPrice = 0;

    [Header("Salvage (Dynamic)")]
    [SerializeField, HideInInspector]
    [Tooltip("Dynamic resources granted when this gear is salvaged.")]
    private ResourceSet salvageResourcesSet;

    /// <summary>
    /// Identifier used for persistence and lookup. Automatically generated if left empty.
    /// </summary>
    public string GearId => gearId;

    /// <summary>
    /// Human readable name of the gear item.
    /// </summary>
    public string DisplayName => displayName;

    /// <summary>
    /// Type of slot that the gear item occupies.
    /// </summary>
    public GearType GearType => gearType;

    /// <summary>
    /// Animator controller that should be applied to the gear slot when equipped.
    /// </summary>
    public RuntimeAnimatorController AnimatorController => animatorController;

    /// <summary>
    /// Optional icon for UI presentation.
    /// </summary>
    public Sprite Icon => icon;

    /// <summary>
    /// Sprite that should be shown when the gear is spawned as world loot.
    /// </summary>
    public Sprite WorldIcon => worldIcon;
    public float UiIconWorldScale => uiIconWorldScale;
    public bool CanAppearInRandomDrops => canAppearInRandomDrops;
    public bool IsCraftable => isCraftable;
    public int RequiredLevel => requiredLevel;

    /// <summary>
    /// Color applied to the gear's sprite renderer when equipped.
    /// </summary>
    public Color SpriteColor => spriteColor;

    /// <summary>
    /// Rarity tier assigned to this gear item.
    /// </summary>
    public GearRarity Rarity => rarity;

    /// <summary>
    /// Strength bonus provided by the gear.
    /// </summary>
    public int Strength => strength;

    /// <summary>
    /// Defense bonus provided by the gear.
    /// </summary>
    public int Defense => defense;

    /// <summary>
    /// Health bonus provided by the gear.
    /// </summary>
    public int Health => health;

    /// <summary>
    /// Knowledge bonus provided by the gear.
    /// </summary>
    public int Knowledge => knowledge;

    /// <summary>
    /// Intelligence bonus provided by the gear.
    /// </summary>
    public int Intelligence => intelligence;

    /// <summary>
    /// Additional tile damage applied when this gear is equipped.
    /// </summary>
    public int TileDamageBonus => Mathf.Max(0, tileDamageBonus);

    /// <summary>
    /// Damage dealt by the gear.
    /// </summary>
    public int Damage => damage;

    /// <summary>
    /// Additional descriptive information about the gear.
    /// </summary>
    public string Info => info;

    /// <summary>
    /// Resources granted when salvaging this gear item.
    /// </summary>
    public ResourceSet SalvageResourcesSet => salvageResourcesSet;

    /// <summary>
    /// Retrieves the color associated with the specified rarity.
    /// </summary>
    public static Color GetColorForRarity(GearRarity gearRarity)
    {
        switch (gearRarity)
        {
            case GearRarity.Uncommon:
                return new Color32(30, 171, 0, 255);
            case GearRarity.Rare:
                return new Color32(0, 112, 221, 255);
            case GearRarity.Epic:
                return new Color32(163, 53, 238, 255);
            case GearRarity.Legendary:
                return new Color32(255, 128, 0, 255);
            default:
                return new Color32(128, 128, 128, 255);
        }
    }

    /// <summary>
    /// Returns the relative drop weight used when rolling random loot based on rarity.
    /// </summary>
    public float RandomDropWeight => GetDropWeightForRarity(rarity);

    /// <summary>
    /// Provides the default drop weight for a given gear rarity. Higher values are more common.
    /// </summary>
    public static float GetDropWeightForRarity(GearRarity rarityLevel)
    {
        switch (rarityLevel)
        {
            case GearRarity.Common:    return 1.0f;
            case GearRarity.Uncommon:  return 0.65f;
            case GearRarity.Rare:      return 0.35f;
            case GearRarity.Epic:      return 0.18f;
            case GearRarity.Legendary: return 0.08f;
            default:                   return 1.0f;
        }
    }

    /// <summary>
    /// Returns the configured rarity color for this gear item.
    /// </summary>
    public Color RarityColor => GetColorForRarity(rarity);
    public int DefaultPlayerBuyPrice => Mathf.Max(0, defaultPlayerBuyPrice);
    public int DefaultPlayerSellPrice => Mathf.Max(0, defaultPlayerSellPrice);

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(gearId))
        {
            gearId = System.Guid.NewGuid().ToString();
        }
        if (defaultPlayerBuyPrice < 0)
        {
            defaultPlayerBuyPrice = 0;
        }
        if (defaultPlayerSellPrice < 0)
        {
            defaultPlayerSellPrice = 0;
        }
        if (tileDamageBonus < 0)
        {
            tileDamageBonus = 0;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(GearItem))]
public class GearItemInlineEditor : Editor
{
    private ResourceDatabase cachedDb;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        var gear = (GearItem)target;
        if (gear == null)
        {
            serializedObject.ApplyModifiedProperties();
            return;
        }

        var db = ResolveDatabase();
        if (db == null || db.Resources == null || db.Resources.Count == 0)
        {
            EditorGUILayout.HelpBox("No ResourceDatabase found. Add a DynamicResourceManager to the scene or create a ResourceDatabase asset.", MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Salvage Resources", EditorStyles.boldLabel);
        EnsureResourceSet(gear, "salvageResourcesSet");
        DrawResourceSet(gear, "salvageResourcesSet", db);

        serializedObject.ApplyModifiedProperties();
    }

    private ResourceDatabase ResolveDatabase()
    {
        if (cachedDb != null) return cachedDb;

        var dyn = UnityEngine.Object.FindFirstObjectByType<DynamicResourceManager>();
        if (dyn != null && dyn.Database != null)
        {
            cachedDb = dyn.Database;
            return cachedDb;
        }

        string[] guids = AssetDatabase.FindAssets("t:ResourceDatabase");
        if (guids != null && guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            cachedDb = AssetDatabase.LoadAssetAtPath<ResourceDatabase>(path);
        }
        return cachedDb;
    }

    private static void EnsureResourceSet(GearItem gear, string fieldName)
    {
        var field = typeof(GearItem).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;
        var set = (ResourceSet)field.GetValue(gear);
        if (set == null)
        {
            set = new ResourceSet();
            Undo.RecordObject(gear, "Init Salvage ResourceSet");
            field.SetValue(gear, set);
            EditorUtility.SetDirty(gear);
        }
    }

    private static void DrawResourceSet(GearItem gear, string fieldName, ResourceDatabase db)
    {
        var field = typeof(GearItem).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null) return;
        var set = (ResourceSet)field.GetValue(gear);
        if (set == null) return;

        using (new EditorGUI.IndentLevelScope())
        {
            foreach (var def in db.Resources)
            {
                if (def == null) continue;
                int current = set.Get(def);
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.IntField(def.DisplayName, current);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(gear, "Edit Salvage Resource Amount");
                    set.Set(def, Mathf.Max(0, next));
                    EditorUtility.SetDirty(gear);
                }
            }
        }
    }
}
#endif



}






