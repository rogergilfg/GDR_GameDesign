using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;
using FantasyKingdoms.Minimap;
using SmallScaleInc.CharacterCreatorFantasy;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.EditorTools
{
    /// <summary>
    /// Prompts the user to auto-configure required project settings (layers + transparency sort) when the package is imported.
    /// </summary>
    [InitializeOnLoad]
    internal static class ExampleSceneSetupPrompt
    {
        const string PrefKeyBase = "SmallScaleFKT.SetupPromptCompleted";
        static readonly string[] RequiredLayers =
        {
            "Player",
            "Enemy",
            "World",
            "destructibleMask",
            "Occluder",
            "Neutral",
            "Props"
        };

        // Explicit ordering for custom layers (starting at slot 8)
        static readonly string[] OrderedLayers =
        {
            "Player",            // slot 6 (User Layer 6)
            "Enemy",             // slot 7 (User Layer 7)
            "World",             // slot 8
            "destructibleMask",  // slot 9
            "Occluder",          // slot 10
            "Neutral",           // slot 11
            "Props"              // slot 12
        };

        struct ComponentLayerRule
        {
            public Type ComponentType;
            public string LayerName;
            public bool IncludeChildren;

            public ComponentLayerRule(Type componentType, string layerName, bool includeChildren)
            {
                ComponentType = componentType;
                LayerName = layerName;
                IncludeChildren = includeChildren;
            }
        }

        struct NameLayerRule
        {
            public string ContainsLower;
            public string LayerName;
            public bool IncludeChildren;

            public NameLayerRule(string contains, string layerName, bool includeChildren)
            {
                ContainsLower = contains.ToLowerInvariant();
                LayerName = layerName;
                IncludeChildren = includeChildren;
            }
        }

        static readonly ComponentLayerRule[] ComponentRules =
        {
            new ComponentLayerRule(typeof(GenericTopDownController), "Player", true),
            new ComponentLayerRule(typeof(PlayerHealth), "Player", true),
            new ComponentLayerRule(typeof(EnemyAI), "Enemy", true),
            new ComponentLayerRule(typeof(TurretAI), "Enemy", true),
            new ComponentLayerRule(typeof(CompanionAI), "Neutral", true),
            new ComponentLayerRule(typeof(NeutralNpcAI), "Neutral", true),
            new ComponentLayerRule(typeof(DestructibleProp2D), "Props", false),
            new ComponentLayerRule(typeof(Tilemap), "World", false)
        };

        static readonly NameLayerRule[] NameRules =
        {
            new NameLayerRule("destructiblemask", "destructibleMask", true),
            new NameLayerRule("mask", "destructibleMask", true),
            new NameLayerRule("occluder", "Occluder", true),
            new NameLayerRule("props", "Props", true)
        };

        struct LayerMaskFieldRule
        {
            public string Keyword;
            public string[] Layers;

            public LayerMaskFieldRule(string keyword, params string[] layers)
            {
                Keyword = keyword.ToLowerInvariant();
                Layers = layers;
            }
        }

        static readonly LayerMaskFieldRule[] LayerMaskRules =
        {
            new LayerMaskFieldRule("enem", "Enemy"),
            new LayerMaskFieldRule("player", "Player"),
            new LayerMaskFieldRule("destructible", "destructibleMask"),
            new LayerMaskFieldRule("neutral", "Neutral"),
            new LayerMaskFieldRule("occluder", "Occluder"),
            new LayerMaskFieldRule("prop", "Props"),
            new LayerMaskFieldRule("world", "World"),
            new LayerMaskFieldRule("obstacle", "World")
        };

        static readonly string[] PackageRoots = { "Assets/SmallScaleInt" };
        static readonly string[] MinimapOverlayLayers = { "Enemy", "Props", "Neutral" };

        static ExampleSceneSetupPrompt()
        {
            EditorApplication.delayCall += () => TryPrompt();
        }

        [MenuItem("Tools/SmallScale/Run Example Scene Setup...", priority = 0)]
        static void RunManually()
        {
            TryPrompt(force: true);
        }

        static string GetPrefKey()
        {
            return $"{PrefKeyBase}_{Application.dataPath.GetHashCode()}";
        }

        static void TryPrompt(bool force = false)
        {
            string prefKey = GetPrefKey();
            if (!force && EditorPrefs.GetBool(prefKey, false))
                return;

            bool missingLayers = HasMissingLayers();
            bool wrongTransparency = HasWrongTransparencySettings();

            bool reapplyEverything = force && !missingLayers && !wrongTransparency;

            if (!missingLayers && !wrongTransparency && !force)
            {
                EditorPrefs.SetBool(prefKey, true);
                return;
            }

            if (reapplyEverything)
            {
                missingLayers = true;
                wrongTransparency = true;
            }

            string changesList = string.Empty;
            if (missingLayers)
                changesList += "• Add project layers: Player, Enemy, World, destructibleMask, Occluder, Neutral, Props.\n";
            if (wrongTransparency)
                changesList += "• Set Renderer2D Transparency Sort Mode to Custom Axis (0, 1, 0).\n";
            if (reapplyEverything)
                changesList += "• Re-apply layer assignments to all relevant scene objects.\n";

            string message = "To ensure the Fantasy Kingdom Tileset example scene renders correctly, a few project settings must be adjusted.\n\n"
                           + "Will you allow the package to apply the following changes?\n\n"
                           + changesList
                           + "\nIf you do not plan to open the example scene, feel free to choose \"No.\"";

            int choice = EditorUtility.DisplayDialogComplex(
                "Fantasy Kingdom Tileset Setup",
                message,
                "Apply Changes",
                "No",
                force ? "Cancel" : "Remind Me Later");

            switch (choice)
            {
                case 0:
                    if (missingLayers || reapplyEverything)
                        ApplyLayerSetup();
                    if (wrongTransparency || reapplyEverything)
                        ApplyTransparencySetup();
                    ApplyLayerFixesInProject();
                    EditorPrefs.SetBool(prefKey, true);
                    EditorUtility.DisplayDialog("Fantasy Kingdom Tileset", "Project settings and layers have been updated for the example scene.", "OK");
                    break;
                case 1:
                    EditorPrefs.SetBool(prefKey, true);
                    break;
                case 2:
                    if (force)
                        break;
                    // remind me later: do nothing
                    break;
            }
        }

        static bool HasMissingLayers()
        {
            var existing = new HashSet<string>(InternalEditorUtility.layers);
            foreach (var layer in RequiredLayers)
            {
                if (!existing.Contains(layer))
                    return true;
            }
            return false;
        }

        static void ApplyLayerSetup()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            // Ensure deterministic placement: lock specific indices to expected layer names.
            // Unity built-in layers occupy 0-5; user slots start at 6 (User Layer 6).
            EnsureLayerAtIndex(layersProp, 6, "Player");
            EnsureLayerAtIndex(layersProp, 7, "Enemy");
            EnsureLayerAtIndex(layersProp, 8, "World");
            EnsureLayerAtIndex(layersProp, 9, "destructibleMask");
            EnsureLayerAtIndex(layersProp, 10, "Occluder");
            EnsureLayerAtIndex(layersProp, 11, "Neutral");
            EnsureLayerAtIndex(layersProp, 12, "Props");

            foreach (string layer in RequiredLayers)
            {
                if (LayerExists(layersProp, layer))
                    continue;
                AssignLayer(layersProp, layer);
            }

            tagManager.ApplyModifiedProperties();
        }

        static bool LayerExists(SerializedProperty layersProp, string layer)
        {
            for (int i = 0; i < layersProp.arraySize; i++)
            {
                SerializedProperty element = layersProp.GetArrayElementAtIndex(i);
                if (element != null && element.stringValue == layer)
                    return true;
            }
            return false;
        }

        static void AssignLayer(SerializedProperty layersProp, string layer)
        {
            for (int i = 8; i < layersProp.arraySize; i++) // skip built-in layers
            {
                SerializedProperty element = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(element.stringValue))
                {
                    element.stringValue = layer;
                    return;
                }
            }

            Debug.LogWarning($"Fantasy Kingdom Tileset: Could not add layer \"{layer}\" because all custom layer slots are in use. Please add it manually via Project Settings > Tags and Layers.");
        }

        static void EnsureLayerAtIndex(SerializedProperty layersProp, int index, string layerName)
        {
            if (layersProp == null) return;
            if (index < 0 || index >= layersProp.arraySize) return;

            SerializedProperty element = layersProp.GetArrayElementAtIndex(index);
            if (element == null) return;

            if (element.stringValue != layerName)
            {
                element.stringValue = layerName;
            }
        }

        static bool HasWrongTransparencySettings()
        {
            if (GraphicsSettings.transparencySortMode != TransparencySortMode.CustomAxis)
                return true;

            Vector3 axis = GraphicsSettings.transparencySortAxis;
            if (!Approximately(axis, new Vector3(0f, 1f, 0f)))
                return true;

            return Renderer2DAssetsRequireUpdate();
        }

        static void ApplyTransparencySetup()
        {
            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);

            Object graphicsSettingsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            EditorUtility.SetDirty(graphicsSettingsAsset);
            AssetDatabase.SaveAssets();

            ApplyRenderer2DTransparencySettings();
        }

        static bool Renderer2DAssetsRequireUpdate()
        {
            string[] rendererGuids = AssetDatabase.FindAssets("t:Renderer2DData", PackageRoots);
            if (rendererGuids == null || rendererGuids.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < rendererGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(rendererGuids[i]);
                var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(path);
                if (rendererData == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(rendererData);
                SerializedProperty modeProp = so.FindProperty("m_TransparencySortMode");
                SerializedProperty axisProp = so.FindProperty("m_TransparencySortAxis");
                if (modeProp == null || axisProp == null)
                {
                    continue;
                }

                if (modeProp.intValue != (int)TransparencySortMode.CustomAxis ||
                    !Approximately(axisProp.vector3Value, new Vector3(0f, 1f, 0f)))
                {
                    return true;
                }
            }

            return false;
        }

        static void ApplyRenderer2DTransparencySettings()
        {
            string[] rendererGuids = AssetDatabase.FindAssets("t:Renderer2DData", PackageRoots);
            if (rendererGuids == null || rendererGuids.Length == 0)
            {
                return;
            }

            bool modifiedAny = false;
            Vector3 desiredAxis = new Vector3(0f, 1f, 0f);

            for (int i = 0; i < rendererGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(rendererGuids[i]);
                var rendererData = AssetDatabase.LoadAssetAtPath<Renderer2DData>(path);
                if (rendererData == null)
                {
                    continue;
                }

                SerializedObject so = new SerializedObject(rendererData);
                SerializedProperty modeProp = so.FindProperty("m_TransparencySortMode");
                SerializedProperty axisProp = so.FindProperty("m_TransparencySortAxis");
                if (modeProp == null || axisProp == null)
                {
                    continue;
                }

                bool changed = false;
                if (modeProp.intValue != (int)TransparencySortMode.CustomAxis)
                {
                    modeProp.intValue = (int)TransparencySortMode.CustomAxis;
                    changed = true;
                }

                if (!Approximately(axisProp.vector3Value, desiredAxis))
                {
                    axisProp.vector3Value = desiredAxis;
                    changed = true;
                }

                if (changed)
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rendererData);
                    modifiedAny = true;
                }
            }

            if (modifiedAny)
            {
                AssetDatabase.SaveAssets();
            }
        }

        static void ApplyLayerFixesInProject()
        {
            bool changedPrefabs = ApplyLayersToPrefabs();
            bool changedScenes = ApplyLayersToScenes();

            if (changedPrefabs || changedScenes)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        static bool ApplyLayersToPrefabs()
        {
            bool changedAny = false;
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", PackageRoots);
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab) continue;
                if (ApplyLayerRulesToGameObject(prefab))
                {
                    EditorUtility.SetDirty(prefab);
                    changedAny = true;
                }
            }
            return changedAny;
        }

        static bool ApplyLayersToScenes()
        {
            bool changedAny = false;
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", PackageRoots);
            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                bool sceneChanged = false;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (ApplyLayerRulesToGameObject(root))
                        sceneChanged = true;
                }

                if (sceneChanged)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changedAny = true;
                }

                EditorSceneManager.CloseScene(scene, true);
            }
            return changedAny;
        }

        static bool ApplyLayerRulesToGameObject(GameObject go)
        {
            if (!go)
                return false;

            bool changed = false;
            changed |= ApplyComponentRules(go);
            changed |= ApplyNameRules(go);
            changed |= ApplyLayerMaskDefaults(go);
            changed |= ApplyMinimapDefaults(go);

            foreach (Transform child in go.transform)
            {
                if (ApplyLayerRulesToGameObject(child.gameObject))
                    changed = true;
            }

            return changed;
        }

        static bool ApplyComponentRules(GameObject go)
        {
            bool changed = false;
            foreach (var rule in ComponentRules)
            {
                if (go.GetComponent(rule.ComponentType))
                {
                    if (SetLayer(go, rule.LayerName, rule.IncludeChildren))
                        changed = true;
                }
            }
            return changed;
        }

        static bool ApplyNameRules(GameObject go)
        {
            bool changed = false;
            string lower = go.name.ToLowerInvariant();
            foreach (var rule in NameRules)
            {
                if (lower.Contains(rule.ContainsLower))
                {
                    if (SetLayer(go, rule.LayerName, rule.IncludeChildren))
                        changed = true;
                }
            }
            return changed;
        }

        static bool SetLayer(GameObject go, string layerName, bool includeChildren)
        {
            int targetLayer = LayerMask.NameToLayer(layerName);
            if (targetLayer < 0)
                return false;

            bool changed = false;
            if (go.layer != targetLayer)
            {
                go.layer = targetLayer;
                changed = true;
            }

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                {
                    if (SetLayer(child.gameObject, layerName, true))
                        changed = true;
                }
            }

            return changed;
        }

        static bool ApplyLayerMaskDefaults(GameObject go)
        {
            bool changed = false;
            var behaviours = go.GetComponents<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                if (!behaviour)
                    continue;

                var serialized = new SerializedObject(behaviour);
                bool modified = ApplyLayerMaskDefaults(serialized);
                if (modified)
                {
                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(behaviour);
                    changed = true;
                }
            }
            return changed;
        }

        static bool ApplyMinimapDefaults(GameObject go)
        {
            var controller = go.GetComponent<MinimapController>();
            if (!controller)
            {
                return false;
            }

            bool changed = false;
            var serialized = new SerializedObject(controller);

            // Overlay layers: Enemy + Props + Neutral
            SerializedProperty overlayLayersProp = serialized.FindProperty("overlayLayers");
            int overlayMask = BuildMask(MinimapOverlayLayers);
            if (overlayLayersProp != null && overlayLayersProp.intValue != overlayMask)
            {
                overlayLayersProp.intValue = overlayMask;
                changed = true;
            }

            // Per-layer overrides: Enemy (red), Props/Neutral (blue)
            SerializedProperty overridesProp = serialized.FindProperty("overlayMarkerOverrides");
            if (overridesProp != null)
            {
                const int desiredCount = 2;
                if (overridesProp.arraySize != desiredCount)
                {
                    overridesProp.arraySize = desiredCount;
                    changed = true;
                }

                changed |= SetOverlayOverride(overridesProp.GetArrayElementAtIndex(0), "Enemy", BuildMask(new[] { "Enemy" }), new Color(1f, 0f, 0f, 1f), 2);
                changed |= SetOverlayOverride(overridesProp.GetArrayElementAtIndex(1), "Allies/Props", BuildMask(new[] { "Neutral", "Props" }), new Color(0f, 0.3243475f, 1f, 1f), 1);
            }

            if (changed)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }

            return changed;
        }

        static bool SetOverlayOverride(SerializedProperty element, string description, int layerMask, Color color, int markerSize)
        {
            if (element == null)
            {
                return false;
            }

            bool changed = false;

            SerializedProperty descProp = element.FindPropertyRelative("description");
            if (descProp != null && descProp.stringValue != description)
            {
                descProp.stringValue = description;
                changed = true;
            }

            SerializedProperty layersProp = element.FindPropertyRelative("layers");
            if (layersProp != null && layersProp.intValue != layerMask)
            {
                layersProp.intValue = layerMask;
                changed = true;
            }

            SerializedProperty colorProp = element.FindPropertyRelative("color");
            if (colorProp != null && colorProp.colorValue != color)
            {
                colorProp.colorValue = color;
                changed = true;
            }

            SerializedProperty sizeProp = element.FindPropertyRelative("markerSize");
            int clampedSize = Mathf.Clamp(markerSize, 1, 6);
            if (sizeProp != null && sizeProp.intValue != clampedSize)
            {
                sizeProp.intValue = clampedSize;
                changed = true;
            }

            return changed;
        }

        static bool ApplyLayerMaskDefaults(SerializedObject serialized)
        {
            bool changed = false;
            SerializedProperty iterator = serialized.GetIterator();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.propertyType != SerializedPropertyType.LayerMask)
                    continue;

                string name = iterator.name.ToLowerInvariant();
                foreach (var rule in LayerMaskRules)
                {
                    if (name.Contains(rule.Keyword))
                    {
                        int mask = BuildMask(rule.Layers);
                        if (mask != 0 && iterator.intValue != mask)
                        {
                            iterator.intValue = mask;
                            changed = true;
                        }
                        break;
                    }
                }
            }
            return changed;
        }

        static int BuildMask(IEnumerable<string> layers)
        {
            int mask = 0;
            foreach (var layer in layers)
            {
                int layerIndex = LayerMask.NameToLayer(layer);
                if (layerIndex >= 0)
                    mask |= (1 << layerIndex);
            }
            return mask;
        }

        static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x) &&
                   Mathf.Approximately(a.y, b.y) &&
                   Mathf.Approximately(a.z, b.z);
        }
    }
}




