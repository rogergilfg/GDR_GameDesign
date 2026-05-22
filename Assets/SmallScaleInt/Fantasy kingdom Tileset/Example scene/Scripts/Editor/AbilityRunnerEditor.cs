using UnityEngine;
using UnityEditor;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SmallScale.FantasyKingdomTileset
{
using System.Collections.Generic;

[CustomEditor(typeof(AbilityRunner))]
public class AbilityRunnerEditor : Editor
{
    private bool showRuntimeAbilities = true;

    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        AbilityRunner runner = (AbilityRunner)target;

        // Only show runtime abilities section during play mode
        if (Application.isPlaying)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Runtime Debug Info", EditorStyles.boldLabel);

            // Runtime Granted Abilities section
            IReadOnlyList<AbilityDefinition> runtimeAbilities = runner.RuntimeGrantedAbilities;

            showRuntimeAbilities = EditorGUILayout.Foldout(showRuntimeAbilities,
                $"Runtime Granted Abilities ({runtimeAbilities.Count})", true);

            if (showRuntimeAbilities)
            {
                EditorGUI.indentLevel++;

                if (runtimeAbilities.Count == 0)
                {
                    EditorGUILayout.LabelField("No abilities granted yet", EditorStyles.miniLabel);
                }
                else
                {
                    for (int i = 0; i < runtimeAbilities.Count; i++)
                    {
                        var ability = runtimeAbilities[i];
                        if (ability != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"{i}:", GUILayout.Width(30));
                            EditorGUILayout.ObjectField(ability, typeof(AbilityDefinition), false);
                            EditorGUILayout.EndHorizontal();
                        }
                        else
                        {
                            EditorGUILayout.LabelField($"{i}: (null)", EditorStyles.miniLabel);
                        }
                    }
                }

                EditorGUI.indentLevel--;
            }

            // Force repaint during play mode to keep the list updated
            if (Application.isPlaying)
            {
                Repaint();
            }
        }
    }
}
}




