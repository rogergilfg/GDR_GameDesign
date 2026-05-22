using SmallScale.FantasyKingdomTileset.AbilitySystem;
using UnityEditor;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem.Editor
{
    using SmallScale.FantasyKingdomTileset.AbilitySystem;
    
    [CustomPropertyDrawer(typeof(RankedInt))]
    public class RankedIntDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty list = property.FindPropertyRelative("rankValues");
            int extraCount = list != null ? list.arraySize : 0;
            return EditorGUIUtility.singleLineHeight * (2 + extraCount);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty baseProp = property.FindPropertyRelative("baseValue");
            SerializedProperty listProp = property.FindPropertyRelative("rankValues");

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, baseProp, new GUIContent(label.text + " (Rank 1)"));

            if (listProp != null)
            {
                for (int i = 0; i < listProp.arraySize; i++)
                {
                    line.y += EditorGUIUtility.singleLineHeight;
                    SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                    EditorGUI.PropertyField(line, element, new GUIContent($"Rank {i + 2}"));
                }

                line.y += EditorGUIUtility.singleLineHeight;
                if (GUI.Button(line, "+ Rank"))
                {
                    listProp.arraySize += 1;
                }
            }

            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(RankedFloat))]
    public class RankedFloatDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            SerializedProperty list = property.FindPropertyRelative("rankValues");
            int extraCount = list != null ? list.arraySize : 0;
            return EditorGUIUtility.singleLineHeight * (2 + extraCount);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            SerializedProperty baseProp = property.FindPropertyRelative("baseValue");
            SerializedProperty listProp = property.FindPropertyRelative("rankValues");

            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, baseProp, new GUIContent(label.text + " (Rank 1)"));

            if (listProp != null)
            {
                for (int i = 0; i < listProp.arraySize; i++)
                {
                    line.y += EditorGUIUtility.singleLineHeight;
                    SerializedProperty element = listProp.GetArrayElementAtIndex(i);
                    EditorGUI.PropertyField(line, element, new GUIContent($"Rank {i + 2}"));
                }

                line.y += EditorGUIUtility.singleLineHeight;
                if (GUI.Button(line, "+ Rank"))
                {
                    listProp.arraySize += 1;
                }
            }

            EditorGUI.EndProperty();
        }
    }
}







