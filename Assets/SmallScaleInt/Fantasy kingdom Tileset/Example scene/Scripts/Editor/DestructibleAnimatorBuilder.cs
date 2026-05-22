// Assets/Editor/DestructibleAnimatorBuilder.cs
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.EditorTools
{
public static class DestructibleAnimatorBuilder
{
    private const float FPS = 12f; // change if you want slower/faster destroy anim
    private const string DESTROY_TRIGGER = "Destroy";

    [MenuItem("Assets/Create/Destructible/Build Animator From Folder", true)]
    private static bool ValidateBuild()
    {
        var path = GetSelectedFolderPath();
        return !string.IsNullOrEmpty(path);
    }

    [MenuItem("Assets/Create/Destructible/Build Animator From Folder")]
    private static void Build()
    {
        string folder = GetSelectedFolderPath();
        if (string.IsNullOrEmpty(folder))
        {
            EditorUtility.DisplayDialog("No Folder Selected",
                "Select a folder (in the Project view) that contains the 16 sprites.", "OK");
            return;
        }

        // Collect sprites in this folder (non-recursive)
        var spriteGUIDs = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        var sprites = new List<Sprite>(spriteGUIDs.Length);

        foreach (var guid in spriteGUIDs)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);

            // Only accept assets that live directly in this folder (no subfolders)
            if (!IsDirectChildOf(path, folder))
                continue;

            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s != null) sprites.Add(s);
        }

        if (sprites.Count == 0)
        {
            EditorUtility.DisplayDialog("No sprites found",
                "The selected folder contains no Sprite assets.", "OK");
            return;
        }

        // Sort sprites (natural order by number in name if present, then name)
        sprites = sprites
            .OrderBy(s => ExtractFirstNumber(s.name))
            .ThenBy(s => s.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sprites.Count != 16)
        {
            if (!EditorUtility.DisplayDialog("Warning",
                $"Found {sprites.Count} sprites (expected 16). Proceed anyway?",
                "Proceed", "Cancel"))
                return;
        }

        // Build clips & controller names based on folder
        string folderName = System.IO.Path.GetFileName(folder.TrimEnd('/'));
        string idleClipPath    = System.IO.Path.Combine(folder, $"{folderName}_Idle.anim").Replace("\\", "/");
        string destroyClipPath = System.IO.Path.Combine(folder, $"{folderName}_Destroy.anim").Replace("\\", "/");
        string controllerPath  = System.IO.Path.Combine(folder, $"{folderName}_animator.controller").Replace("\\", "/");

        // Create clips
        var idleClip = CreateIdleClip(sprites.First());
        AssetDatabase.CreateAsset(idleClip, idleClipPath);

        var destroyClip = CreateDestroyClip(sprites, FPS);
        AssetDatabase.CreateAsset(destroyClip, destroyClipPath);

        // Create controller
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        ctrl.AddParameter(DESTROY_TRIGGER, AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = idleClip;
        sm.defaultState = idleState;

        var destroyState = sm.AddState("Destroy");
        destroyState.motion = destroyClip;

        // AnyState -> Destroy (trigger)
        var any = sm.AddAnyStateTransition(destroyState);
        any.hasExitTime = false;
        any.duration = 0f;
        any.canTransitionToSelf = false;
        any.AddCondition(AnimatorConditionMode.If, 0, DESTROY_TRIGGER);

        // Do NOT add a transition back; destroy clip is non-looping and clamps on the last frame.

        EditorUtility.SetDirty(idleClip);
        EditorUtility.SetDirty(destroyClip);
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Done",
            $"Created:\nâ€¢ {idleClipPath}\nâ€¢ {destroyClipPath}\nâ€¢ {controllerPath}\n\n" +
            "Hook this controller to your propâ€™s Animator. Trigger \"Destroy\" to play once and hold last frame.",
            "OK");
    }

    // ------- Clip creation -------

    private static AnimationClip CreateIdleClip(Sprite first)
    {
        var clip = new AnimationClip();
        clip.frameRate = FPS;

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new[] {
            new ObjectReferenceKeyframe { time = 0f, value = first }
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        // Loop on a single frame so it forever stays on the first sprite
        SetClipLoop(clip, true);
        return clip;
    }

    private static AnimationClip CreateDestroyClip(List<Sprite> sprites, float fps)
    {
        var clip = new AnimationClip();
        clip.frameRate = fps;

        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var keys = new ObjectReferenceKeyframe[sprites.Count];

        for (int i = 0; i < sprites.Count; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprites[i] };

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        // Do NOT loop; clamp forever so it holds the last frame (destroyed)
        SetClipLoop(clip, false);
        return clip;
    }

    private static void SetClipLoop(AnimationClip clip, bool loop)
    {
        var so = new SerializedObject(clip);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Also set wrapMode for preview friendliness
        clip.wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
        so.ApplyModifiedProperties();
    }

    // ------- Helpers -------

    private static string GetSelectedFolderPath()
    {
        // Prefer selection if it's a folder
        var obj = Selection.activeObject;
        if (obj != null)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (AssetDatabase.IsValidFolder(path)) return path;
        }
        // Fallback: prompt user
        string chosen = EditorUtility.OpenFolderPanel("Select sprites folder", "Assets", "");
        if (string.IsNullOrEmpty(chosen)) return null;

        // Convert absolute to relative "Assets/..."
        if (chosen.StartsWith(Application.dataPath))
            return "Assets" + chosen.Substring(Application.dataPath.Length).Replace("\\", "/");
        EditorUtility.DisplayDialog("Folder must be inside Assets",
            "Please pick a folder under your project's Assets.", "OK");
        return null;
    }

    private static bool IsDirectChildOf(string assetPath, string parentFolder)
    {
        parentFolder = parentFolder.TrimEnd('/');
        string dir = System.IO.Path.GetDirectoryName(assetPath).Replace("\\", "/");
        return string.Equals(dir, parentFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractFirstNumber(string s)
    {
        // Find first integer in the string for natural sorting; if none, return large value.
        int num = int.MaxValue;
        int start = -1;
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsDigit(s[i])) { start = i; break; }
        }
        if (start >= 0)
        {
            int end = start;
            while (end < s.Length && char.IsDigit(s[end])) end++;
            if (int.TryParse(s.Substring(start, end - start), out int val)) num = val;
        }
        return num;
    }
}
}
#endif





