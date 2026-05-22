using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SmallScale.FantasyKingdomTileset.AbilitySystem.Editor
{
    [CustomEditor(typeof(AbilityDefinition))]
    public sealed class AbilityDefinitionEditor : UnityEditor.Editor
    {
        SerializedProperty _requirementsProp;
        SerializedProperty _stepsProp;
        SerializedProperty _passiveModifiersProp;
        SerializedProperty _isPassiveProp;
        SerializedProperty _isDebuffProp;

        ReorderableList _requirementsList;
        ReorderableList _stepsList;
        ReorderableList _passiveModifiersList;

        static readonly List<Type> RequirementTypes;
        static readonly List<Type> StepTypes;
        static readonly List<Type> PassiveModifierTypes;
        static readonly Dictionary<Type, string> DescriptionCache = new();
        static readonly List<(AbilityActorKindMask mask, string label)> OwnerLabelCache = new()
        {
            (AbilityActorKindMask.Player, "Player"),
            (AbilityActorKindMask.Enemy, "Enemy"),
            (AbilityActorKindMask.Neutral, "Neutral"),
            (AbilityActorKindMask.Unknown, "Unknown")
        };

        static AbilityDefinitionEditor()
        {
            RequirementTypes = TypeCache.GetTypesDerivedFrom<AbilityRequirement>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();

            StepTypes = TypeCache.GetTypesDerivedFrom<AbilityStep>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();

            PassiveModifierTypes = TypeCache.GetTypesDerivedFrom<PassiveAbilityModifier>()
                .Where(t => !t.IsAbstract && !t.IsGenericType && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToList();
        }

        void OnEnable()
        {
            _requirementsProp = serializedObject.FindProperty("activationRequirements");
            _stepsProp = serializedObject.FindProperty("steps");
            _passiveModifiersProp = serializedObject.FindProperty("passiveModifiers");
            _isPassiveProp = serializedObject.FindProperty("isPassive");
            _isDebuffProp = serializedObject.FindProperty("isDebuff");

            _requirementsList = CreateList(_requirementsProp, "Activation Requirements", typeof(AbilityRequirement), RequirementTypes);
            _stepsList = CreateList(_stepsProp, "Steps", typeof(AbilityStep), StepTypes);
            _passiveModifiersList = CreateList(_passiveModifiersProp, "Passive Modifiers", typeof(PassiveAbilityModifier), PassiveModifierTypes);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool isPassive = false;
            if (_isPassiveProp != null)
            {
                EditorGUILayout.PropertyField(_isPassiveProp);
                isPassive = _isPassiveProp.boolValue;
            }

            if (isPassive && _isDebuffProp != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_isDebuffProp);
                EditorGUI.indentLevel--;
            }
            else if (!isPassive && _isDebuffProp != null && _isDebuffProp.boolValue)
            {
                _isDebuffProp.boolValue = false;
            }

            DrawPropertiesExcluding(serializedObject, "activationRequirements", "steps", "passiveModifiers", "isPassive", "isDebuff");

            EditorGUILayout.Space();

            if (!isPassive)
            {
                _requirementsList?.DoLayoutList();
                EditorGUILayout.Space();
                _stepsList?.DoLayoutList();
            }
            else
            {
                EditorGUILayout.HelpBox("Passive abilities ignore activation requirements and steps. Configure their behaviour through Passive Modifiers.", MessageType.Info);
            }

            EditorGUILayout.Space();
            _passiveModifiersList?.DoLayoutList();

            serializedObject.ApplyModifiedProperties();
        }

        ReorderableList CreateList(SerializedProperty property, string header, Type baseType, List<Type> concreteTypes)
        {
            if (property == null)
            {
                return null;
            }

            var list = new ReorderableList(serializedObject, property, true, true, true, true)
            {
                drawHeaderCallback = rect => EditorGUI.LabelField(rect, header),
                drawElementCallback = (rect, index, active, focused) =>
                {
                    if (index < 0 || index >= property.arraySize) return;
                    var element = property.GetArrayElementAtIndex(index);
                    string label = GetElementLabel(element, index);
                    float propertyHeight = EditorGUI.GetPropertyHeight(element, new GUIContent(label), true);
                    Rect propertyRect = new Rect(rect.x, rect.y, rect.width, propertyHeight);
                    EditorGUI.PropertyField(propertyRect, element, new GUIContent(label), true);

                    if (element.isExpanded)
                    {
                        string description = GetDescriptionForElement(element);
                        if (!string.IsNullOrEmpty(description))
                        {
                            float spacing = EditorGUIUtility.standardVerticalSpacing;
                            var helpContent = new GUIContent(description);
                            float helpHeight = EditorStyles.helpBox.CalcHeight(helpContent, rect.width);
                            Rect helpRect = EditorGUI.IndentedRect(new Rect(rect.x, propertyRect.yMax + spacing, rect.width, helpHeight));
                            EditorGUI.HelpBox(helpRect, description, MessageType.None);
                        }
                    }
                },
                elementHeightCallback = index =>
                {
                    if (index < 0 || index >= property.arraySize) return EditorGUIUtility.singleLineHeight;
                    var element = property.GetArrayElementAtIndex(index);
                    float height = EditorGUI.GetPropertyHeight(element, GUIContent.none, true);
                    if (element.isExpanded)
                    {
                        string description = GetDescriptionForElement(element);
                        if (!string.IsNullOrEmpty(description))
                        {
                            float width = Mathf.Max(10f, EditorGUIUtility.currentViewWidth - 40f);
                            height += EditorGUIUtility.standardVerticalSpacing + EditorStyles.helpBox.CalcHeight(new GUIContent(description), width);
                        }
                    }

                    return height + EditorGUIUtility.standardVerticalSpacing;
                }
            };

            list.onAddDropdownCallback = (rect, l) => ShowAddMenu(property, concreteTypes, baseType);
            list.onRemoveCallback = l =>
            {
                if (l.index >= 0 && l.index < property.arraySize)
                {
                    property.DeleteArrayElementAtIndex(l.index);
                    serializedObject.ApplyModifiedProperties();
                }
            };

            return list;
        }

        void ShowAddMenu(SerializedProperty listProperty, List<Type> concreteTypes, Type baseType)
        {
            var menu = new GenericMenu();

            if (concreteTypes.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent($"No classes found for {baseType.Name}"));
            }
            else
            {
                foreach (var type in concreteTypes)
                {
                    string label = Nicify(type);
                    menu.AddItem(new GUIContent(label), false, () => AddManagedReference(listProperty, type));
                }
            }

            menu.ShowAsContext();
        }

        void AddManagedReference(SerializedProperty listProperty, Type type)
        {
            serializedObject.Update();
            int index = listProperty.arraySize;
            listProperty.InsertArrayElementAtIndex(index);
            var element = listProperty.GetArrayElementAtIndex(index);
            element.managedReferenceValue = Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        static string Nicify(Type type)
        {
            string name = type.Name;
            if (name.EndsWith("Requirement")) name = name[..^11];
            if (name.EndsWith("Step")) name = name[..^4];
            if (name.EndsWith("Modifier")) name = name[..^8];
            return ObjectNames.NicifyVariableName(name);
        }

        static string GetElementLabel(SerializedProperty element, int index)
        {
            string typeName = element != null ? element.managedReferenceFullTypename : string.Empty;
            if (string.IsNullOrEmpty(typeName))
            {
                return $"[{index}] (Unassigned)";
            }

            int spaceIndex = typeName.IndexOf(' ');
            if (spaceIndex >= 0 && spaceIndex < typeName.Length - 1)
            {
                typeName = typeName[(spaceIndex + 1)..];
            }

            int lastDot = typeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < typeName.Length - 1)
            {
                typeName = typeName[(lastDot + 1)..];
            }

            if (typeName.Contains("AbilitySystem."))
            {
                typeName = typeName.Replace("AbilitySystem.", string.Empty);
            }

            if (typeName.EndsWith("Requirement")) typeName = typeName[..^11];
            if (typeName.EndsWith("Step")) typeName = typeName[..^4];
            if (typeName.EndsWith("Modifier")) typeName = typeName[..^8];

            typeName = ObjectNames.NicifyVariableName(typeName);
            string ownerSummary = GetOwnerSummary(element);
            return string.IsNullOrEmpty(ownerSummary)
                ? $"{index + 1}. {typeName}"
                : $"{index + 1}. {typeName} [{ownerSummary}]";
        }

        static string GetDescriptionForElement(SerializedProperty element)
        {
            if (element == null) return string.Empty;

            object instance = element.managedReferenceValue;
            Type type = instance?.GetType();

            if (type == null && !string.IsNullOrEmpty(element.managedReferenceFullTypename))
            {
                type = GetTypeFromManagedReference(element.managedReferenceFullTypename);
            }

            if (type == null) return string.Empty;

            if (!DescriptionCache.TryGetValue(type, out string description))
            {
                var abilityComponentAttr = type.GetCustomAttribute<AbilityComponentDescriptionAttribute>();
                var passiveModifierAttr = type.GetCustomAttribute<PassiveModifierDescriptionAttribute>();
                description = abilityComponentAttr?.Summary ?? passiveModifierAttr?.Description ?? string.Empty;
                DescriptionCache[type] = description;
            }

            return description;
        }

        static Type GetTypeFromManagedReference(string fullTypename)
        {
            if (string.IsNullOrEmpty(fullTypename)) return null;
            int spaceIndex = fullTypename.IndexOf(' ');
            if (spaceIndex < 0 || spaceIndex >= fullTypename.Length - 1) return null;

            string assemblyName = fullTypename[..spaceIndex];
            string className = fullTypename[(spaceIndex + 1)..];

            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.Ordinal));

                return assembly?.GetType(className);
            }
            catch
            {
                return null;
            }
        }

        static string GetOwnerSummary(SerializedProperty element)
        {
            if (element == null) return string.Empty;

            object instance = element.managedReferenceValue;
            AbilityActorKindMask mask = AbilityActorKindMask.All;

            switch (instance)
            {
                case AbilityRequirement requirement:
                    mask = requirement.OwnerFilter == AbilityActorKindMask.None ? AbilityActorKindMask.All : requirement.OwnerFilter;
                    break;
                case AbilityStep step:
                    mask = step.OwnerFilter == AbilityActorKindMask.None ? AbilityActorKindMask.All : step.OwnerFilter;
                    break;
                default:
                {
                    // attempt to resolve type if managed reference exists but instance not yet created
                    Type type = GetTypeFromManagedReference(element.managedReferenceFullTypename);
                    if (type != null)
                    {
                        if (typeof(AbilityRequirement).IsAssignableFrom(type))
                        {
                            mask = AbilityActorKindMask.All;
                        }
                        else if (typeof(AbilityStep).IsAssignableFrom(type))
                        {
                            mask = AbilityActorKindMask.All;
                        }
                    }
                    break;
                }
            }

            if (mask == AbilityActorKindMask.All)
            {
                return "All";
            }

            var labels = new List<string>(3);
            foreach (var entry in OwnerLabelCache)
            {
                if ((mask & entry.mask) != 0)
                {
                    labels.Add(entry.label);
                }
            }

            return labels.Count == 0 ? "All" : string.Join(", ", labels);
        }
    }
}





