using UnityEditor;
using UnityEngine;
using DungeonGeneration;

namespace SmallScale.FantasyKingdomTileset.EditorTools
{
[CustomEditor(typeof(DungeonGenerator))]
public class DungeonGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Runtime Utilities", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Regenerate", GUILayout.Height(24)))
            {
                var gen = (DungeonGenerator)target;
                if (gen != null)
                {
                    // Full reset to ensure enemies/prefabs and tiles are cleared
                    gen.ClearExistingDungeon();
                    gen.GenerateDungeon(null, true);
                }
            }

            if (GUILayout.Button("Regenerate (Random Seed)", GUILayout.Height(24)))
            {
                var gen = (DungeonGenerator)target;
                if (gen != null)
                {
                    int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
                    gen.ClearExistingDungeon();
                    gen.GenerateDungeon(seed, true);
                }
            }
        }
        EditorGUI.EndDisabledGroup();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to enable runtime regeneration.", MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
}





