#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Tilemaps;
using SmallScale.FantasyKingdomTileset.Building;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.EditorTools
{
public class DestructibleTileBatchAuthorWindow : EditorWindow
{
    const string kLastProfileKey = "SS_DTBAP_LastProfilePath";

    SerializedObject _so;                     // serialized view of the profile
    Vector2 _scroll;
    [SerializeField] DestructibleTileBatchProfile profile;  // current profile asset

    ReorderableList _rulesList;               // drag-reorderable UI for rules
    int _selectedIndex = -1;                  // selected rule index (-1 = default rule)
    Vector2 _leftScroll, _rightScroll;        // independent scrolls for list and properties
    float _leftPaneWidth = 320f;              // fixed width for rules list

    [MenuItem("Tools/SmallScale/Destruction/Batch Author Tiles...")]
    public static void Open()
    {
        var w = GetWindow<DestructibleTileBatchAuthorWindow>("Destructible Tiles Author");
        w.minSize = new Vector2(640, 520);
        w.Show();
    }

    void OnEnable()
    {
        // Try auto-load last profile
        if (!profile)
        {
            var lastPath = EditorPrefs.GetString(kLastProfileKey, "");
            if (!string.IsNullOrEmpty(lastPath))
                profile = AssetDatabase.LoadAssetAtPath<DestructibleTileBatchProfile>(lastPath);
        }

        // If still none, create a temporary in-memory instance so UI works immediately
        if (!profile)
        {
            profile = CreateInstance<DestructibleTileBatchProfile>();
            if (profile.rules.Count == 0)
            {
                profile.rules.Add(new DestructibleTileBatchProfile.Rule { prefix = "Flora", enabled = true });
                profile.rules.Add(new DestructibleTileBatchProfile.Rule { prefix = "Stone", enabled = true });
            }
        }

        _so = new SerializedObject(profile);
        SetupRulesList();
    }

    void OnGUI()
    {
        // Profile selector + save/load row
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        var newProfile = (DestructibleTileBatchProfile)EditorGUILayout.ObjectField("Profile", profile, typeof(DestructibleTileBatchProfile), false);
        if (newProfile != profile && newProfile != null)
        {
            profile = newProfile;
            _so = new SerializedObject(profile);
            EditorPrefs.SetString(kLastProfileKey, AssetDatabase.GetAssetPath(profile));
            SetupRulesList();
        }

        if (GUILayout.Button("New", GUILayout.Width(60)))
            CreateNewProfile();

        if (GUILayout.Button("Save", GUILayout.Width(60)))
            SaveProfile(false);

        if (GUILayout.Button("Save As...", GUILayout.Width(80)))
            SaveProfile(true);

        if (GUILayout.Button("Load...", GUILayout.Width(70)))
            LoadProfile();
        EditorGUILayout.EndHorizontal();

        if (!_so?.targetObject)
        {
            EditorGUILayout.HelpBox("No profile loaded.", MessageType.Warning);
            return;
        }

        _so.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Folders & Database", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.sourceFolder)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.outputFolder)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.database)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.resourceDatabase)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.defaultLootPickupPrefab)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.defaultGearDatabase)));

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.overwriteExisting)));
        EditorGUILayout.PropertyField(_so.FindProperty(nameof(DestructibleTileBatchProfile.dryRun)));

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();

        // Left: rules list
        EditorGUILayout.BeginVertical(GUILayout.Width(_leftPaneWidth));
        _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll);
        if (_rulesList == null) SetupRulesList();
        _rulesList?.DoLayoutList();

        // Default rule as its own selectable item
        bool isDefaultSelected = (_selectedIndex < 0);
        var defaultStyle = isDefaultSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton;
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Toggle(isDefaultSelected, "Default Rule", defaultStyle))
            {
                _selectedIndex = -1;
            }
            GUILayout.FlexibleSpace();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Right: properties of selected item
        EditorGUILayout.BeginVertical();
        _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);
        var rulesProp = _so.FindProperty(nameof(DestructibleTileBatchProfile.rules));
        if (_selectedIndex >= 0 && _selectedIndex < rulesProp.arraySize)
        {
            var selectedProp = rulesProp.GetArrayElementAtIndex(_selectedIndex);
            EditorGUILayout.LabelField($"Rule: {selectedProp.FindPropertyRelative("prefix").stringValue}", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DrawRuleFields(selectedProp, _selectedIndex);
            EditorGUILayout.EndVertical();
        }
        else
        {
            var defProp = _so.FindProperty(nameof(DestructibleTileBatchProfile.defaultRule));
            EditorGUILayout.LabelField("Default Rule", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            DrawRuleFields(defProp, -1);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(!CanRun()))
        {
            if (GUILayout.Button("Generate / Update Destructible Tile Data", GUILayout.Height(36)))
                Run();
        }

        if (_so.ApplyModifiedProperties())
        {
            if (AssetDatabase.Contains(profile))
                EditorUtility.SetDirty(profile);
        }
    }

    void SetupRulesList()
    {
        if (profile == null)
        {
            _rulesList = null;
            return;
        }

        if (_so == null)
        {
            _so = new SerializedObject(profile);
        }

        var rulesProp = _so.FindProperty(nameof(DestructibleTileBatchProfile.rules));
        _rulesList = new ReorderableList(_so, rulesProp, draggable: true, displayHeader: true, displayAddButton: true, displayRemoveButton: true);

        _rulesList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Prefix Rules (drag to reorder) â€” longest prefix wins; ties: later wins");
        };

        _rulesList.drawElementCallback = (rect, index, active, focused) =>
        {
            var elem = rulesProp.GetArrayElementAtIndex(index);
            float pad = 4f;
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;

            // enabled toggle
            var rEnabled = new Rect(rect.x + pad, rect.y, 18f, rect.height);
            var enabledProp = elem.FindPropertyRelative("enabled");
            enabledProp.boolValue = EditorGUI.Toggle(rEnabled, GUIContent.none, enabledProp.boolValue);

            // prefix field
            var rPrefix = new Rect(rEnabled.xMax + 6f, rect.y, rect.width - (rEnabled.xMax - rect.x) - 12f, rect.height);
            EditorGUI.PropertyField(rPrefix, elem.FindPropertyRelative("prefix"), GUIContent.none);
        };

        _rulesList.elementHeightCallback = index => EditorGUIUtility.singleLineHeight + 6f;

        _rulesList.onAddCallback = list =>
        {
            rulesProp.InsertArrayElementAtIndex(rulesProp.arraySize);
            var elem = rulesProp.GetArrayElementAtIndex(rulesProp.arraySize - 1);
            elem.FindPropertyRelative("enabled").boolValue = true;
            elem.FindPropertyRelative("prefix").stringValue = "New";
            _selectedIndex = rulesProp.arraySize - 1;
        };

        _rulesList.onSelectCallback = list =>
        {
            _selectedIndex = list.index;
        };
    }

    // no-op now; kept for potential future bulk expand/collapse if we reintroduce foldouts
    void SetAllRuleFoldouts(bool expanded) { }

    void DrawRuleFields(SerializedProperty rule, int ruleIndex)
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxHP"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("clearTile"));
        if (!rule.FindPropertyRelative("clearTile").boolValue)
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("destroyedTile"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("destroyVfxPrefab"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("destroyVfxCleanup"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("swapDelay"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("placeDestroyedOnBrokenMap"));

        EditorGUILayout.Space(2);
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("flashOnHit"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("flashColor"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("flashHold"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("flashFade"));

        // staged and impact VFX
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("stagedVfxPrefab"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("stagedVfxCleanup"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("impactVfxPrefab"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("impactVfxCleanup"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Gear Drops", EditorStyles.boldLabel);
        var enableGearProp = rule.FindPropertyRelative("enableGearDrops");
        EditorGUILayout.PropertyField(enableGearProp, new GUIContent("Enable Random Drops"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("guaranteedGearDrops"), true);
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("aliasTileNames"), true);
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("lootScatter"));
        if (enableGearProp.boolValue)
        {
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("gearDropChance"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("gearDropChainMultiplier"));
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("gearDropMaxCount"));
            var localPoolProp = rule.FindPropertyRelative("useLocalGearPool");
            EditorGUILayout.PropertyField(localPoolProp, new GUIContent("Use Local Gear Pool"));
            if (localPoolProp.boolValue)
            {
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("localRandomGearDrops"), true);
            }
            else
            {
                EditorGUILayout.HelpBox("Random drops will pull from the profile's default Gear Item Database.", MessageType.None);
            }
        }

        DrawResourceDropsUI(ruleIndex);

        EditorGUILayout.Space(4);
        // Crafting first so category can auto-sync
        EditorGUILayout.LabelField("Crafting Station", EditorStyles.boldLabel);
        var isCraftProp = rule.FindPropertyRelative("isCraftingStation");
        var catProp = rule.FindPropertyRelative("buildCategory");
        EditorGUILayout.PropertyField(isCraftProp);
        if (isCraftProp.boolValue)
        {
            // Force category to CraftingStations and show as disabled
            catProp.enumValueIndex = (int)BuildPartCategory.CraftingStations;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(catProp);
            }
            EditorGUILayout.PropertyField(rule.FindPropertyRelative("stationType"));
        }
        else
        {
            // Normal category selection when not a crafting station
            EditorGUILayout.PropertyField(catProp);
        }
        // Remaining build settings
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("isBuildable"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("partIdPrefix"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("displayNameOverride"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("buildIcon"));
        EditorGUILayout.PropertyField(rule.FindPropertyRelative("buildInfoText"));
        DrawUnlockResourceField(rule, ruleIndex);
        EditorGUI.indentLevel--;
    }

    void DrawUnlockResourceField(SerializedProperty ruleProp, int ruleIndex)
    {
        var unlockProp = ruleProp.FindPropertyRelative("unlockResource");
        if (unlockProp == null)
        {
            return;
        }

        if (profile == null)
        {
            EditorGUILayout.PropertyField(unlockProp);
            return;
        }

        var db = profile.resourceDatabase;
        if (db == null || db.Resources == null || db.Resources.Count == 0)
        {
            EditorGUILayout.PropertyField(unlockProp);
            return;
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("Unlock Requirement", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.Space(1);
            ResourceTypeDef current = unlockProp.objectReferenceValue as ResourceTypeDef;

            int optionCount = db.Resources.Count + 1;
            string[] options = new string[optionCount];
            options[0] = "Unlocked by default";
            int selectedIndex = 0;

            for (int i = 0; i < db.Resources.Count; i++)
            {
                ResourceTypeDef res = db.Resources[i];
                options[i + 1] = res != null ? res.DisplayName : "<missing>";
                if (res != null && res == current)
                {
                    selectedIndex = i + 1;
                }
            }

            int nextIndex = EditorGUILayout.Popup("Required Resource", selectedIndex, options);
            ResourceTypeDef next = nextIndex > 0 && nextIndex - 1 < db.Resources.Count ? db.Resources[nextIndex - 1] : null;
            if (next != current)
            {
                unlockProp.objectReferenceValue = next;
            }
        }
    }

    void DrawResourceDropsUI(int ruleIndex)
    {
        if (profile == null)
            return;

        DestructibleTileBatchProfile.Rule ruleObj = null;
        if (ruleIndex >= 0)
        {
            if (profile.rules == null || ruleIndex >= profile.rules.Count) return;
            ruleObj = profile.rules[ruleIndex];
        }
        else
        {
            ruleObj = profile.defaultRule;
        }
        var db = profile.resourceDatabase;

        EditorGUILayout.LabelField("Resource Drops", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            if (db == null || db.Resources == null || db.Resources.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign a ResourceDatabase to list available resources.", MessageType.Info);
                return;
            }

            if (ruleObj.resourceDrops == null)
            {
                ruleObj.resourceDrops = new ResourceSet();
            }

            foreach (var def in db.Resources)
            {
                if (def == null) continue;
                int current = ruleObj.resourceDrops.Get(def);
                int next = EditorGUILayout.IntField(def.DisplayName, current);
                next = Mathf.Max(0, next);
                if (next != current)
                {
                    ruleObj.resourceDrops.Set(def, next);
                }
            }
        }

        // Ensure changes persist
        EditorUtility.SetDirty(profile);
    }

    bool CanRun()
    {
        return profile
            && profile.sourceFolder
            && profile.outputFolder
            && profile.database;
    }

    void CreateNewProfile()
    {
        var path = EditorUtility.SaveFilePanelInProject("Create Batch Profile", "DestructibleTileBatchProfile", "asset", "Choose a location to save the profile.");
        if (string.IsNullOrEmpty(path)) return;
        var asset = CreateInstance<DestructibleTileBatchProfile>();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        profile = asset;
        _so = new SerializedObject(profile);
        EditorPrefs.SetString(kLastProfileKey, path);
        SetupRulesList();
    }

    void SaveProfile(bool saveAs)
    {
        if (!profile) return;

        string currentPath = AssetDatabase.GetAssetPath(profile);
        if (string.IsNullOrEmpty(currentPath) || saveAs)
        {
            var newPath = EditorUtility.SaveFilePanelInProject("Save Profile As", "DestructibleTileBatchProfile", "asset", "Choose a location to save the profile.");
            if (string.IsNullOrEmpty(newPath)) return;

            if (!AssetDatabase.Contains(profile))
                AssetDatabase.CreateAsset(profile, newPath);
            else
                AssetDatabase.MoveAsset(currentPath, newPath);

            currentPath = newPath;
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        EditorPrefs.SetString(kLastProfileKey, currentPath);
    }

    void LoadProfile()
    {
        var path = EditorUtility.OpenFilePanel("Load Profile", Application.dataPath, "asset");
        if (string.IsNullOrEmpty(path)) return;

        // Convert absolute path to relative project path
        if (path.StartsWith(Application.dataPath))
            path = "Assets" + path.Substring(Application.dataPath.Length);

        var loaded = AssetDatabase.LoadAssetAtPath<DestructibleTileBatchProfile>(path);
        if (loaded)
        {
            profile = loaded;
            _so = new SerializedObject(profile);
            EditorPrefs.SetString(kLastProfileKey, path);
            SetupRulesList();
        }
        else
        {
            EditorUtility.DisplayDialog("Not a Profile", "Selected asset is not a DestructibleTileBatchProfile.", "OK");
        }
    }

    void Run()
    {
        if (!CanRun())
        {
            EditorUtility.DisplayDialog("Missing Data", "Assign Source Folder, Output Folder and Database.", "OK");
            return;
        }

        string srcPath = AssetDatabase.GetAssetPath(profile.sourceFolder);
        string outPath = AssetDatabase.GetAssetPath(profile.outputFolder);
        if (string.IsNullOrEmpty(srcPath) || string.IsNullOrEmpty(outPath))
        {
            EditorUtility.DisplayDialog("Invalid Paths", "Folders must be inside the project.", "OK");
            return;
        }

        // Collect tiles in source folder
        List<TileBase> tiles = new List<TileBase>();
        foreach (string guid in AssetDatabase.FindAssets("t:TileBase", new[] { srcPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile) tiles.Add(tile);
        }

        // Build variant grouping (by base name)
        var variantsByTile = BuildVariantLookup(tiles);

        // Ensure DB list exists
        var db = profile.database;
        if (db.entries == null) db.entries = new List<DestructibleTileData>();

        // Map existing db entries by source tile
        var byTile = new Dictionary<TileBase, DestructibleTileData>();
        foreach (var e in db.entries)
        {
            if (e != null && e.sourceTile != null && !byTile.ContainsKey(e.sourceTile))
                byTile.Add(e.sourceTile, e);
        }

        int created = 0, updated = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            TileBase tile = tiles[i];
            if (tile == null) continue;

            // Pick first matching rule (top to bottom)
            var rule = GetMatchingRule(tile.name);
            if (rule == null || !rule.enabled) continue;

            bool isNew = !byTile.TryGetValue(tile, out DestructibleTileData data) || data == null;
            if (isNew)
            {
                string assetName = Sanitize(tile.name) + ".asset";
                string assetPath = Path.Combine(outPath, assetName).Replace('\\', '/');
                data = AssetDatabase.LoadAssetAtPath<DestructibleTileData>(assetPath);
                if (!data)
                {
                    data = CreateInstance<DestructibleTileData>();
                    AssetDatabase.CreateAsset(data, assetPath);
                    created++;
                }
                else
                {
                    updated++;
                }
            }
            else
            {
                updated++;
            }

            // Copy common fields
            data.sourceTile = tile;
            data.maxHP = Mathf.Max(1, rule.maxHP);
            data.clearTile = rule.clearTile;
            data.destroyedTile = rule.destroyedTile;
            data.destroyVfxPrefab = rule.destroyVfxPrefab;
            data.destroyVfxCleanup = Mathf.Max(0f, rule.destroyVfxCleanup);
            data.swapDelay = Mathf.Max(0f, rule.swapDelay);
            data.placeDestroyedOnBrokenMap = rule.placeDestroyedOnBrokenMap;
            data.flashOnHit = rule.flashOnHit;
            data.flashColor = rule.flashColor;
            data.flashHold = Mathf.Max(0f, rule.flashHold);
            data.flashFade = Mathf.Max(0f, rule.flashFade);
            data.stagedVfxPrefab = rule.stagedVfxPrefab;
            data.stagedVfxCleanup = Mathf.Max(0f, rule.stagedVfxCleanup);
            data.impactVfxPrefab = rule.impactVfxPrefab;
            data.impactVfxCleanup = Mathf.Max(0f, rule.impactVfxCleanup);
            data.enableGearDrops = rule.enableGearDrops;
            data.gearDropChance = Mathf.Clamp01(rule.gearDropChance);
            data.gearDropChainMultiplier = Mathf.Clamp01(rule.gearDropChainMultiplier);
            data.gearDropMaxCount = Mathf.Clamp(rule.gearDropMaxCount, 1, 3);
            data.useLocalGearPool = rule.useLocalGearPool;
            data.lootPickupPrefab = profile.defaultLootPickupPrefab;
            data.gearDropDatabase = profile.defaultGearDatabase;
            data.lootScatter = rule.lootScatter;
            data.guaranteedGearDrops = rule.guaranteedGearDrops != null ? new List<GearItem>(rule.guaranteedGearDrops) : new List<GearItem>();
            data.localRandomGearDrops = (rule.useLocalGearPool && rule.localRandomGearDrops != null)
                ? new List<GearItem>(rule.localRandomGearDrops)
                : new List<GearItem>();

            // Keep previous assigned resource drops unless rule specifies new and previous non-empty
            ResourceSet assignedDrops = rule.resourceDrops;
            if ((assignedDrops == null || assignedDrops.IsEmpty) && !isNew)
            {
                assignedDrops = data.resourceDropsSet;
            }
            data.resourceDropsSet = assignedDrops;

            // Crafting
            data.isCraftingStation = rule.isCraftingStation;
            data.craftingStationType = rule.stationType;

            // Build metadata
            string partId = tile != null ? tile.name : string.Empty;
            if (!string.IsNullOrWhiteSpace(rule.partIdPrefix))
                partId = rule.partIdPrefix + "_" + partId;
            string displayName = !string.IsNullOrWhiteSpace(rule.displayNameOverride) ? rule.displayNameOverride : partId;
            Sprite icon = ExtractSprite(tile) ?? rule.buildIcon;
            string infoText = rule.buildInfoText;
            data.SetBuildMetadata(partId, displayName, (rule.isCraftingStation ? BuildPartCategory.CraftingStations : rule.buildCategory), icon, infoText, assignedDrops);
            data.SetUnlockResource(rule.unlockResource);
            data.SetBuildable(rule.isBuildable);

            // Variants
            if (variantsByTile.TryGetValue(tile, out TileBase[] variants) && variants != null && variants.Length > 0)
            {
                data.SetVariants(variants);
            }
            else
            {
                data.SetVariants(new[] { tile });
            }

            EditorUtility.SetDirty(data);

            // Link into DB
            if (byTile.TryGetValue(tile, out var existing))
            {
                int idx = db.entries.IndexOf(existing);
                if (idx >= 0) db.entries[idx] = data;
            }
            else
            {
                db.entries.Add(data);
                byTile[tile] = data;
            }
        }

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Batch Authoring", $"Created: {created}\nUpdated: {updated}\nTotal DB Entries: {db.entries.Count}", "OK");
    }

    DestructibleTileBatchProfile.Rule GetMatchingRule(string tileName)
    {
        if (string.IsNullOrEmpty(tileName)) return profile.defaultRule;

        DestructibleTileBatchProfile.Rule best = null;
        int bestLen = -1;
        int bestIndex = -1;

        string lowerName = tileName.ToLowerInvariant();

        for (int i = 0; i < profile.rules.Count; i++)
        {
            var r = profile.rules[i];
            if (r == null || !r.enabled) continue;

            bool aliasMatch = false;
            if (r.aliasTileNames != null)
            {
                for (int a = 0; a < r.aliasTileNames.Count; a++)
                {
                    string alias = r.aliasTileNames[a];
                    if (string.IsNullOrWhiteSpace(alias)) continue;
                    if (lowerName == alias.Trim().ToLowerInvariant())
                    {
                        aliasMatch = true;
                        break;
                    }
                }
            }

            if (aliasMatch)
            {
                int len = tileName.Length;
                if (len > bestLen || (len == bestLen && i > bestIndex))
                {
                    best = r;
                    bestLen = len;
                    bestIndex = i;
                }
                continue;
            }

            string p = r.prefix;
            if (string.IsNullOrWhiteSpace(p)) continue;

            if (tileName.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                int len = p.Length;
                if (len > bestLen || (len == bestLen && i > bestIndex))
                {
                    best = r;
                    bestLen = len;
                    bestIndex = i;
                }
            }
        }

        if (best != null)
            return best;

        // Check default rule aliases
        if (profile.defaultRule != null && profile.defaultRule.aliasTileNames != null)
        {
            for (int i = 0; i < profile.defaultRule.aliasTileNames.Count; i++)
            {
                string alias = profile.defaultRule.aliasTileNames[i];
                if (string.IsNullOrWhiteSpace(alias)) continue;
                if (lowerName == alias.Trim().ToLowerInvariant())
                    return profile.defaultRule;
            }
        }

        return profile.defaultRule;
    }

    static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrEmpty(name) ? "Unnamed" : name;
    }

    static Sprite ExtractSprite(TileBase tile)
    {
        if (tile == null) return null;
        if (tile is Tile t) return t.sprite;
        return null;
    }

    static Dictionary<TileBase, TileBase[]> BuildVariantLookup(IReadOnlyList<TileBase> tiles)
    {
        var groups = new Dictionary<string, List<TileBase>>();
        var result = new Dictionary<TileBase, TileBase[]>();

        for (int i = 0; i < tiles.Count; i++)
        {
            TileBase tile = tiles[i];
            if (!tile) continue;

            string groupName = ExtractBaseTileName(tile.name);
            if (!groups.TryGetValue(groupName, out List<TileBase> list))
            {
                list = new List<TileBase>();
                groups.Add(groupName, list);
            }

            if (!list.Contains(tile))
            {
                list.Add(tile);
            }
        }

        foreach (KeyValuePair<string, List<TileBase>> kvp in groups)
        {
            TileBase[] sorted = SortVariants(kvp.Value);
            for (int i = 0; i < sorted.Length; i++)
            {
                TileBase tile = sorted[i];
                if (tile != null)
                {
                    result[tile] = sorted;
                }
            }
        }

        return result;
    }

    static TileBase[] SortVariants(List<TileBase> variants)
    {
        variants.Sort((a, b) =>
        {
            int orderA = GetDirectionOrder(a != null ? a.name : null);
            int orderB = GetDirectionOrder(b != null ? b.name : null);
            int compare = orderA.CompareTo(orderB);
            if (compare != 0)
            {
                return compare;
            }

            string nameA = a != null ? a.name : string.Empty;
            string nameB = b != null ? b.name : string.Empty;
            return string.CompareOrdinal(nameA, nameB);
        });

        return variants.ToArray();
    }

    static int GetDirectionOrder(string tileName)
    {
        if (TryGetDirectionSuffix(tileName, out char suffix))
        {
            switch (suffix)
            {
                case 'N': return 0;
                case 'E': return 1;
                case 'S': return 2;
                case 'W': return 3;
            }
        }

        return -1;
    }

    static bool TryGetDirectionSuffix(string tileName, out char suffix)
    {
        suffix = default;
        if (string.IsNullOrEmpty(tileName) || tileName.Length < 2)
        {
            return false;
        }

        char possibleDirection = tileName[tileName.Length - 1];
        char underscore = tileName[tileName.Length - 2];
        if (underscore != '_')
        {
            return false;
        }

        possibleDirection = char.ToUpperInvariant(possibleDirection);
        if (possibleDirection == 'N' || possibleDirection == 'E' || possibleDirection == 'S' || possibleDirection == 'W')
        {
            suffix = possibleDirection;
            return true;
        }

        return false;
    }

    static string ExtractBaseTileName(string tileName)
    {
        if (string.IsNullOrWhiteSpace(tileName))
        {
            return string.Empty;
        }

        tileName = tileName.Trim();
        if (TryGetDirectionSuffix(tileName, out _))
        {
            return tileName.Substring(0, tileName.Length - 2);
        }

        return tileName;
    }
}
}
#endif






