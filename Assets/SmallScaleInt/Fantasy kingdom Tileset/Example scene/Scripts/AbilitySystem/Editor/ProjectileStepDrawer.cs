using UnityEditor;
using UnityEngine;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem.Editor
{
    [CustomPropertyDrawer(typeof(ProjectileStep))]
    public sealed class ProjectileStepDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            height += EditorGUIUtility.standardVerticalSpacing;
            height += GetPropertyHeight(property, "mode");

            height += GetPropertyHeight(property, "useMouseAimForPlayers");
            height += GetPropertyHeight(property, "fallbackDirection");
            height += GetPropertyHeight(property, "muzzleLocalOffset");

            var mode = GetMode(property);
            bool usesProjectile = mode != ProjectileStep.ProjectileMode.MultiShotBeam;
            if (usesProjectile)
            {
                height += GetPropertyHeight(property, "projectile");
                height += GetPropertyHeight(property, "impactDamage");
            }

            switch (mode)
            {
                case ProjectileStep.ProjectileMode.AreaExplosion:
                    height += GetPropertyHeight(property, "areaSettings");
                    break;
                case ProjectileStep.ProjectileMode.AreaDamageOverTime:
                    height += GetPropertyHeight(property, "areaDotSettings");
                    break;
                case ProjectileStep.ProjectileMode.ChainShot:
                    height += GetPropertyHeight(property, "chainSettings");
                    break;
                case ProjectileStep.ProjectileMode.MultiShotCone:
                    height += GetPropertyHeight(property, "coneSettings");
                    break;
                case ProjectileStep.ProjectileMode.MultiShotBeam:
                    height += GetPropertyHeight(property, "beamSettings");
                    break;
            }

            height += GetPropertyHeight(property, "spawnMuzzleVfx");
            height += GetPropertyHeight(property, "muzzleVfxPrefab");
            height += GetPropertyHeight(property, "muzzleVfxLifetime");
            height += GetPropertyHeight(property, "muzzleVfxScale");

            if (usesProjectile)
            {
                height += GetPropertyHeight(property, "hitVfxPrefab");
                height += GetPropertyHeight(property, "hitVfxLifetime");
                height += GetPropertyHeight(property, "hitVfxScale");
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;
            Rect contentRect = new Rect(position.x, foldoutRect.yMax + EditorGUIUtility.standardVerticalSpacing, position.width, EditorGUIUtility.singleLineHeight);

            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("mode"));
            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("useMouseAimForPlayers"));
            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("fallbackDirection"));
            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("muzzleLocalOffset"));

            var mode = GetMode(property);
            bool usesProjectile = mode != ProjectileStep.ProjectileMode.MultiShotBeam;
            if (usesProjectile)
            {
                contentRect = DrawProperty(contentRect, property.FindPropertyRelative("projectile"));
                contentRect = DrawProperty(contentRect, property.FindPropertyRelative("impactDamage"));
            }

            switch (mode)
            {
                case ProjectileStep.ProjectileMode.AreaExplosion:
                    contentRect = DrawProperty(contentRect, property.FindPropertyRelative("areaSettings"));
                    break;
                case ProjectileStep.ProjectileMode.AreaDamageOverTime:
                    contentRect = DrawProperty(contentRect, property.FindPropertyRelative("areaDotSettings"));
                    break;
                case ProjectileStep.ProjectileMode.ChainShot:
                    contentRect = DrawProperty(contentRect, property.FindPropertyRelative("chainSettings"));
                    break;
                case ProjectileStep.ProjectileMode.MultiShotCone:
                    contentRect = DrawProperty(contentRect, property.FindPropertyRelative("coneSettings"));
                    break;
                case ProjectileStep.ProjectileMode.MultiShotBeam:
                    contentRect = DrawProperty(contentRect, property.FindPropertyRelative("beamSettings"));
                    break;
            }

            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("spawnMuzzleVfx"));
            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("muzzleVfxPrefab"));
            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("muzzleVfxLifetime"));
            contentRect = DrawProperty(contentRect, property.FindPropertyRelative("muzzleVfxScale"));

            if (usesProjectile)
            {
                contentRect = DrawProperty(contentRect, property.FindPropertyRelative("hitVfxPrefab"));
                contentRect = DrawProperty(contentRect, property.FindPropertyRelative("hitVfxLifetime"));
                contentRect = DrawProperty(contentRect, property.FindPropertyRelative("hitVfxScale"));
            }

            EditorGUI.indentLevel--;
        }

        static ProjectileStep.ProjectileMode GetMode(SerializedProperty property)
        {
            var modeProp = property.FindPropertyRelative("mode");
            return (ProjectileStep.ProjectileMode)(modeProp?.enumValueIndex ?? 0);
        }

        static float GetPropertyHeight(SerializedProperty parent, string relativeName)
        {
            var prop = parent.FindPropertyRelative(relativeName);
            if (prop == null)
            {
                return 0f;
            }

            return EditorGUI.GetPropertyHeight(prop, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        static Rect DrawProperty(Rect rect, SerializedProperty property)
        {
            if (property == null)
            {
                return rect;
            }

            float height = EditorGUI.GetPropertyHeight(property, true);
            Rect drawRect = new Rect(rect.x, rect.y, rect.width, height);
            EditorGUI.PropertyField(drawRect, property, true);
            rect.y += height + EditorGUIUtility.standardVerticalSpacing;
            return rect;
        }
    }
}



