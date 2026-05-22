using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using SmallScale.FantasyKingdomTileset;

namespace SmallScale.FantasyKingdomTileset.EditorTools
{
[CustomEditor(typeof(TileColliderAutoPlacer))]
public class TileColliderAutoPlacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("Populate Colliders"))
        {
            foreach (Object targetObject in targets)
            {
                if (targetObject is TileColliderAutoPlacer autoPlacer)
                {
                    RecordUndo(autoPlacer);
                    autoPlacer.PopulateColliders();
                }
            }
        }
    }

    private static void RecordUndo(TileColliderAutoPlacer autoPlacer)
    {
        if (autoPlacer == null)
        {
            return;
        }

        Tilemap colliderTilemap = autoPlacer.ColliderTilemap;
        if (colliderTilemap != null)
        {
            Undo.RegisterCompleteObjectUndo(colliderTilemap, "Populate Colliders");
        }
        else
        {
            Undo.RegisterCompleteObjectUndo(autoPlacer, "Populate Colliders");
        }
    }
}
}





