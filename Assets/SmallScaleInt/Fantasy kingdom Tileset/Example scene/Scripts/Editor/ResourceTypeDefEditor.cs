using UnityEditor;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset
{
    [CustomEditor(typeof(ResourceTypeDef))]
    public class ResourceTypeDefEditor : UnityEditor.Editor
    {
        SerializedProperty craftingCostProp;
        SerializedProperty isCraftableProp;

        static ResourceDatabase sharedDatabase;
        ResourceDatabase overrideDatabase;

        void OnEnable()
        {
            craftingCostProp = serializedObject.FindProperty("craftingCost");
            isCraftableProp = serializedObject.FindProperty("isCraftable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawPropertiesExcluding(serializedObject, "craftingCost");

            DrawCraftingCostSection();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawCraftingCostSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Crafting Cost", EditorStyles.boldLabel);

            if (craftingCostProp == null)
            {
                EditorGUILayout.HelpBox("Crafting cost property missing.", MessageType.Warning);
                return;
            }

            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox("Select a single resource type to edit crafting costs.", MessageType.Info);
                EditorGUILayout.PropertyField(craftingCostProp, true);
                return;
            }

            ResourceDatabase database = DrawDatabaseField();
            if (database == null || database.Resources == null || database.Resources.Count == 0)
            {
                EditorGUILayout.HelpBox("Assign a ResourceDatabase so all resources can be listed.", MessageType.Info);
                EditorGUILayout.PropertyField(craftingCostProp, true);
                return;
            }

            SerializedProperty amountsProp = craftingCostProp.FindPropertyRelative("amounts");
            if (amountsProp == null)
            {
                EditorGUILayout.PropertyField(craftingCostProp, true);
                return;
            }

            using (new EditorGUI.DisabledScope(!isCraftableProp.boolValue))
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var def in database.Resources)
                {
                    if (def == null) continue;
                    int currentAmount = GetAmount(amountsProp, def);
                    EditorGUI.BeginChangeCheck();
                    int nextAmount = EditorGUILayout.IntField(def.DisplayName, currentAmount);
                    nextAmount = Mathf.Max(0, nextAmount);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SetAmount(amountsProp, def, nextAmount);
                    }
                }
            }
        }

        ResourceDatabase DrawDatabaseField()
        {
            ResourceDatabase current = overrideDatabase != null ? overrideDatabase : sharedDatabase;
            EditorGUI.BeginChangeCheck();
            ResourceDatabase next = (ResourceDatabase)EditorGUILayout.ObjectField("Resource Database", current, typeof(ResourceDatabase), false);
            if (EditorGUI.EndChangeCheck())
            {
                overrideDatabase = next;
                sharedDatabase = next;
                current = next;
            }

            if (current == null)
            {
                current = AutoLocateDatabase();
                if (current != null)
                {
                    sharedDatabase = current;
                }
            }

            return current;
        }

        static ResourceDatabase AutoLocateDatabase()
        {
#if UNITY_EDITOR
            string[] guids = AssetDatabase.FindAssets("t:ResourceDatabase");
            if (guids != null && guids.Length > 0)
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var db = AssetDatabase.LoadAssetAtPath<ResourceDatabase>(path);
                    if (db != null)
                    {
                        return db;
                    }
                }
            }
#endif
            return null;
        }

        static int GetAmount(SerializedProperty amountsProp, ResourceTypeDef type)
        {
            if (type == null) return 0;
            for (int i = 0; i < amountsProp.arraySize; i++)
            {
                SerializedProperty element = amountsProp.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = element.FindPropertyRelative("type");
                if (typeProp != null && typeProp.objectReferenceValue == type)
                {
                    SerializedProperty amountProp = element.FindPropertyRelative("amount");
                    return amountProp != null ? amountProp.intValue : 0;
                }
            }
            return 0;
        }

        static void SetAmount(SerializedProperty amountsProp, ResourceTypeDef type, int value)
        {
            if (type == null) return;
            for (int i = 0; i < amountsProp.arraySize; i++)
            {
                SerializedProperty element = amountsProp.GetArrayElementAtIndex(i);
                SerializedProperty typeProp = element.FindPropertyRelative("type");
                if (typeProp != null && typeProp.objectReferenceValue == type)
                {
                    if (value <= 0)
                    {
                        amountsProp.DeleteArrayElementAtIndex(i);
                    }
                    else
                    {
                        SerializedProperty amountProp = element.FindPropertyRelative("amount");
                        if (amountProp != null)
                        {
                            amountProp.intValue = value;
                        }
                    }
                    return;
                }
            }

            if (value <= 0)
            {
                return;
            }

            int newIndex = amountsProp.arraySize;
            amountsProp.InsertArrayElementAtIndex(newIndex);
            SerializedProperty newElement = amountsProp.GetArrayElementAtIndex(newIndex);
            SerializedProperty newTypeProp = newElement.FindPropertyRelative("type");
            SerializedProperty newAmountProp = newElement.FindPropertyRelative("amount");
            if (newTypeProp != null) newTypeProp.objectReferenceValue = type;
            if (newAmountProp != null) newAmountProp.intValue = value;
        }
    }
}




