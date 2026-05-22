using UnityEditor;
using UnityEngine;
using SmallScale.FantasyKingdomTileset.AbilitySystem;

namespace SkillSystem.Editor
{
    public sealed class SkillTreeEditorWindow : EditorWindow
    {
        const float DragHeaderHeight = 22f;
        const int MaxColumns = 3;

        static readonly Vector2 DefaultCellSize = new Vector2(160f, 160f);
        static readonly Vector2 DefaultSpacing = new Vector2(12f, 12f);
        static readonly Vector4 DefaultPadding = new Vector4(16f, 16f, 16f, 16f);

        [SerializeField] private SkillTreeDefinition tree;

        SerializedObject treeSO;
        SerializedProperty nodesProp;
        SerializedProperty displayNameProp;
        SerializedProperty descriptionProp;
        SerializedProperty overrideLayoutProp;
        SerializedProperty cellSizeProp;
        SerializedProperty spacingProp;
        SerializedProperty paddingProp;
        SerializedProperty requirePointsInTreeProp;
        SerializedProperty pointsPerRowProp;

        Vector2 pan;
        int selectedIndex = -1;
        int hoverIndex = -1;

        Vector2 nodeListScroll;

        GUIStyle nodeStyle;
        GUIStyle selectedNodeStyle;
        GUIStyle titleStyle;

        [MenuItem("Tools/SmallScale/Skill System/Skill Tree Editor")]
        public static void Open()
        {
            var window = GetWindow<SkillTreeEditorWindow>("Skill Tree Editor");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        void OnEnable()
        {
            InitStyles();
        }

        void InitStyles()
        {
            if (nodeStyle == null)
            {
                nodeStyle = new GUIStyle("window")
                {
                    alignment = TextAnchor.UpperCenter,
                    padding = new RectOffset(4, 4, 4, 4)
                };
            }

            if (selectedNodeStyle == null)
            {
                selectedNodeStyle = new GUIStyle(nodeStyle);
                selectedNodeStyle.normal.textColor = Color.cyan;
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 12
                };
            }
        }

        void OnGUI()
        {
            InitStyles();
            DrawToolbar();
            DrawCanvas();
            DrawInspector();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newTree = (SkillTreeDefinition)EditorGUILayout.ObjectField(tree, typeof(SkillTreeDefinition), false, GUILayout.Width(280f));
                if (newTree != tree)
                {
                    SetTree(newTree);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("Add Node", "Add a new node"), EditorStyles.toolbarButton))
                {
                    AddNodeAt(Vector2.zero, autoPlacement: true);
                }

                if (GUILayout.Button(new GUIContent("Delete Node", "Delete selected node"), EditorStyles.toolbarButton))
                {
                    DeleteSelectedNode();
                }
            }
        }

        void SetTree(SkillTreeDefinition newTree)
        {
            tree = newTree;
            selectedIndex = -1;
            hoverIndex = -1;

            if (tree)
            {
                treeSO = new SerializedObject(tree);
                nodesProp = treeSO.FindProperty("nodes");
                displayNameProp = treeSO.FindProperty("displayName");
                descriptionProp = treeSO.FindProperty("skillTreeDescription");
                overrideLayoutProp = treeSO.FindProperty("overrideLayout");
                cellSizeProp = treeSO.FindProperty("layoutCellSize");
                spacingProp = treeSO.FindProperty("layoutSpacing");
                paddingProp = treeSO.FindProperty("layoutPadding");
                requirePointsInTreeProp = treeSO.FindProperty("requirePointsInTree");
                pointsPerRowProp = treeSO.FindProperty("pointsPerRow");
            }
            else
            {
                treeSO = null;
                nodesProp = null;
                displayNameProp = null;
                descriptionProp = null;
                overrideLayoutProp = null;
                cellSizeProp = null;
                spacingProp = null;
                paddingProp = null;
                requirePointsInTreeProp = null;
                pointsPerRowProp = null;
            }

            Repaint();
        }

        void DrawCanvas()
        {
            Rect canvasRect = new Rect(0f, EditorStyles.toolbar.fixedHeight, position.width * 0.65f, position.height - EditorStyles.toolbar.fixedHeight);
            GUI.Box(canvasRect, GUIContent.none);

            if (!tree)
            {
                GUI.Label(canvasRect, "Assign a SkillTreeDefinition to begin", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            treeSO?.Update();

            Vector2 cellSize = ResolveCellSize();
            Vector2 spacing = ResolveSpacing();
            Vector4 padding = ResolvePadding();

            Event e = Event.current;
            if (canvasRect.Contains(e.mousePosition) && e.type == EventType.MouseDrag && e.button == 2)
            {
                pan += e.delta;
                Repaint();
            }

            hoverIndex = -1;

            DrawGrid(canvasRect, cellSize, spacing, padding);

            BeginWindows();
            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                Rect original = NodeRect(i, cellSize, spacing, padding);
                var style = (i == selectedIndex) ? selectedNodeStyle : nodeStyle;

                Rect updated = GUI.Window(i, original, DrawNodeWindow, GUIContent.none, style);

                if (updated.position != original.position)
                {
                    Vector2 editorPos = EditorPositionFromRect(updated, cellSize, spacing, padding);
                    SetNodeEditorPosition(i, Snap(editorPos));
                }

                if (updated.Contains(e.mousePosition))
                {
                    hoverIndex = i;
                }
            }
            EndWindows();

            HandleCanvasMouse(canvasRect);

            for (int i = 0; i < tree.Nodes.Count; i++)
            {
                DrawConnections(i, cellSize, spacing, padding);
            }
        }

        void DrawInspector()
        {
            Rect inspectorRect = new Rect(position.width * 0.65f + 4f, EditorStyles.toolbar.fixedHeight, position.width * 0.35f - 8f, position.height - EditorStyles.toolbar.fixedHeight);
            GUILayout.BeginArea(inspectorRect);

            if (!tree || treeSO == null)
            {
                GUILayout.Label("No tree selected.");
                GUILayout.EndArea();
                return;
            }

            treeSO.Update();

            GUILayout.Label("Skill Tree", titleStyle);
            EditorGUILayout.PropertyField(displayNameProp, new GUIContent("Tree Name"));
            EditorGUILayout.PropertyField(descriptionProp, new GUIContent("Description"));

            EditorGUILayout.Space(6f);
            GUILayout.Label("Layout", titleStyle);
            EditorGUILayout.PropertyField(overrideLayoutProp, new GUIContent("Override Layout"));
            if (overrideLayoutProp.boolValue)
            {
                EditorGUILayout.PropertyField(cellSizeProp, new GUIContent("Cell Size"));
                EditorGUILayout.PropertyField(spacingProp, new GUIContent("Spacing"));
                EditorGUILayout.PropertyField(paddingProp, new GUIContent("Padding (L,R,T,B)"));
            }
            else
            {
                EditorGUILayout.HelpBox("Using panel default layout.", MessageType.None);
            }

            EditorGUILayout.Space(6f);
            GUILayout.Label("Requirements", titleStyle);
            EditorGUILayout.PropertyField(requirePointsInTreeProp, new GUIContent("Require Points in Tree"));
            if (requirePointsInTreeProp.boolValue)
            {
                EditorGUILayout.PropertyField(pointsPerRowProp, new GUIContent("Points Per Row"));
                EditorGUILayout.HelpBox("Points required are calculated automatically based on row position:\nRow 0 = 0 points\nRow 1 = " + pointsPerRowProp.intValue + " points\nRow 2 = " + (pointsPerRowProp.intValue * 2) + " points\nRow 3 = " + (pointsPerRowProp.intValue * 3) + " points", MessageType.Info);
            }

            EditorGUILayout.Space(6f);
            GUILayout.Label("Nodes", titleStyle);
            DrawNodeList();

            EditorGUILayout.Space(6f);
            GUILayout.Label("Node Inspector", titleStyle);
            DrawNodeInspector();

            treeSO.ApplyModifiedProperties();
            GUILayout.EndArea();
        }

        void DrawNodeList()
        {
            nodeListScroll = EditorGUILayout.BeginScrollView(nodeListScroll, GUILayout.Height(180f));
            int count = nodesProp != null ? nodesProp.arraySize : 0;
            for (int i = 0; i < count; i++)
            {
                string title = GetNodeTitle(i);
                bool isSelected = (i == selectedIndex);
                if (GUILayout.Button($"{i + 1}. {title}", isSelected ? EditorStyles.toolbarButton : EditorStyles.miniButton))
                {
                    selectedIndex = i;
                    Repaint();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawNodeInspector()
        {
            if (selectedIndex < 0 || nodesProp == null || selectedIndex >= nodesProp.arraySize)
            {
                GUILayout.Label("Select a node to edit its properties.", EditorStyles.miniLabel);
                return;
            }

            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(selectedIndex);
            SerializedProperty displayNameProp = nodeProp.FindPropertyRelative("displayName");
            SerializedProperty grantedAbilityProp = nodeProp.FindPropertyRelative("grantedAbility");

            EditorGUILayout.PropertyField(displayNameProp);

            // Track ability changes to auto-populate name
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(grantedAbilityProp, new GUIContent("Rank 1 Ability"));
            if (EditorGUI.EndChangeCheck())
            {
                AutoPopulateFromAbility(displayNameProp, grantedAbilityProp);
            }

            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("additionalRankAbilities"), new GUIContent("Additional Rank Abilities"), true);

            SkillNodeDefinition runtimeNode = selectedIndex >= 0 && selectedIndex < tree.Nodes.Count ? tree.Nodes[selectedIndex] : null;

            EditorGUILayout.Space(4f);
            GUILayout.Label("Requirements", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("requiredLevel"));

            // Show calculated Required Points in Tree (read-only display)
            if (tree != null && tree.RequirePointsInTree && runtimeNode != null)
            {
                int calculatedPoints = runtimeNode.GetRequiredPointsInTree(tree);
                int row = Mathf.RoundToInt(runtimeNode.EditorPosition.y);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField(new GUIContent("Required Points (Row " + row + ")", "Automatically calculated based on row position and tree's Points Per Row setting"), calculatedPoints);
                }
            }

            EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("consumesSkillPoint"));
            if (runtimeNode != null)
            {
                EditorGUILayout.Space(6f);
                GUILayout.Label("Resolved Ranks", EditorStyles.boldLabel);
                int resolvedMaxRank = runtimeNode.MaxRank;
                EditorGUILayout.LabelField("Total Ranks", resolvedMaxRank.ToString());

                using (new EditorGUI.DisabledScope(true))
                {
                    for (int r = 1; r <= resolvedMaxRank; r++)
                    {
                        AbilityDefinition rankAbility = runtimeNode.GetAbilityForRank(r);
                        EditorGUILayout.ObjectField($"Rank {r} Ability", rankAbility, typeof(AbilityDefinition), false);
                    }
                }
            }

            SerializedProperty posProp = nodeProp.FindPropertyRelative("editorPosition");
            Vector2 newPos = EditorGUILayout.Vector2Field("Grid Position", posProp.vector2Value);
            newPos = Snap(newPos);
            if ((newPos - posProp.vector2Value).sqrMagnitude > 0.0001f)
            {
                posProp.vector2Value = newPos;
            }

            EditorGUILayout.Space(4f);
            GUILayout.Label("Prerequisites", EditorStyles.boldLabel);
            DrawPrerequisiteList(nodeProp.FindPropertyRelative("prerequisiteNodeIds"));

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("Delete Node"))
            {
                DeleteSelectedNode();
            }
        }

        void DrawPrerequisiteList(SerializedProperty prerequisites)
        {
            if (prerequisites == null) return;

            for (int i = 0; i < prerequisites.arraySize; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    SerializedProperty entry = prerequisites.GetArrayElementAtIndex(i);
                    EditorGUILayout.SelectableLabel(entry.stringValue, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                    if (GUILayout.Button("X", GUILayout.Width(24f)))
                    {
                        prerequisites.DeleteArrayElementAtIndex(i);
                        GUI.FocusControl(null);
                        break;
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Add Hover", GUILayout.Width(100f)))
                {
                    int candidate = hoverIndex >= 0 ? hoverIndex : -1;
                    if (candidate >= 0 && candidate != selectedIndex)
                    {
                        string prereqId = tree.Nodes[candidate].NodeId;
                        if (!ContainsString(prerequisites, prereqId))
                        {
                            prerequisites.InsertArrayElementAtIndex(prerequisites.arraySize);
                            prerequisites.GetArrayElementAtIndex(prerequisites.arraySize - 1).stringValue = prereqId;
                        }
                    }
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60f)))
                {
                    prerequisites.ClearArray();
                }
            }
        }

        bool ContainsString(SerializedProperty list, string value)
        {
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).stringValue == value)
                    return true;
            }
            return false;
        }

        void AutoPopulateFromAbility(SerializedProperty displayNameProp, SerializedProperty abilityProp)
        {
            AbilityDefinition ability = abilityProp.objectReferenceValue as AbilityDefinition;
            if (ability == null) return;

            // Auto-populate display name if it's empty or still the default "New Skill"
            string currentName = displayNameProp.stringValue;
            if (string.IsNullOrEmpty(currentName) || currentName == "New Skill")
            {
                displayNameProp.stringValue = ability.DisplayName;
            }
        }

        void DrawNodeWindow(int id)
        {
            var node = tree.Nodes[id];

            Rect dragRect = new Rect(0f, 0f, NodeWindowWidth, DragHeaderHeight);
            EditorGUI.DrawRect(dragRect, new Color(0f, 0f, 0f, 0.35f));
            GUI.Label(dragRect, GetNodeTitle(id), EditorStyles.whiteMiniLabel);
            EditorGUIUtility.AddCursorRect(dragRect, MouseCursor.MoveArrow);
            GUI.DragWindow(dragRect);

            Rect iconRect = new Rect(8f, DragHeaderHeight + 6f, NodeWindowWidth - 16f, NodeWindowWidth - 24f);
            Sprite iconSprite = node.Icon ? node.Icon : (node.GrantedAbility && node.GrantedAbility.Icon ? node.GrantedAbility.Icon : null);
            if (iconSprite != null)
            {
                DrawSprite(iconRect, iconSprite);
            }
            else
            {
                EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f, 0.35f));
                GUI.Label(iconRect, "No Icon", EditorStyles.centeredGreyMiniLabel);
            }

            Rect infoRect = new Rect(8f, DragHeaderHeight + iconRect.height + 4f, NodeWindowWidth - 16f, 18f);
            string requirements = $"Lvl {node.RequiredLevel}";
            if (tree != null && tree.RequirePointsInTree)
            {
                int calculatedPoints = node.GetRequiredPointsInTree(tree);
                if (calculatedPoints > 0)
                {
                    requirements += $" | {calculatedPoints}pts";
                }
            }
            GUI.Label(infoRect, requirements, EditorStyles.centeredGreyMiniLabel);
        }

        float NodeWindowWidth => Mathf.Max(112f, ResolveCellSize().x);

        void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;

            // Get the sprite's texture coordinates
            Rect spriteRect = sprite.textureRect;
            Texture2D tex = sprite.texture;

            // Calculate UV coordinates for the sprite within the texture
            Rect uvRect = new Rect(
                spriteRect.x / tex.width,
                spriteRect.y / tex.height,
                spriteRect.width / tex.width,
                spriteRect.height / tex.height
            );

            // Calculate aspect ratio to maintain sprite proportions
            float spriteAspect = spriteRect.width / spriteRect.height;
            float rectAspect = rect.width / rect.height;

            Rect drawRect = rect;
            if (spriteAspect > rectAspect)
            {
                // Sprite is wider - fit to width
                float height = rect.width / spriteAspect;
                drawRect.y += (rect.height - height) * 0.5f;
                drawRect.height = height;
            }
            else
            {
                // Sprite is taller - fit to height
                float width = rect.height * spriteAspect;
                drawRect.x += (rect.width - width) * 0.5f;
                drawRect.width = width;
            }

            // Draw the texture with the correct UV coordinates
            GUI.DrawTextureWithTexCoords(drawRect, tex, uvRect);
        }

        void HandleCanvasMouse(Rect canvasRect)
        {
            Event e = Event.current;
            if (!canvasRect.Contains(e.mousePosition)) return;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                selectedIndex = hoverIndex;
                Repaint();
            }
            else if (e.type == EventType.MouseDown && e.button == 1)
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Node"), false, () => AddNodeAt(MouseToTreePos(e.mousePosition), autoPlacement: false));
                if (hoverIndex >= 0)
                {
                    menu.AddItem(new GUIContent("Delete Node"), false, DeleteSelectedNode);
                    menu.AddSeparator("");
                    menu.AddItem(new GUIContent("Snap To Grid"), false, () => SnapNode(hoverIndex));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Delete Node"));
                }
                menu.ShowAsContext();
                e.Use();
            }
        }

        void DrawConnections(int nodeIndex, Vector2 cellSize, Vector2 spacing, Vector4 padding)
        {
            var node = tree.Nodes[nodeIndex];
            var prereqs = node.PrerequisiteNodeIds;
            if (prereqs == null) return;

            Vector2 from = NodeCenter(nodeIndex, cellSize, spacing, padding);
            Handles.color = new Color(1f, 1f, 1f, 0.25f);

            foreach (var pid in prereqs)
            {
                int idx = IndexOfNodeId(pid);
                if (idx < 0) continue;
                Vector2 to = NodeCenter(idx, cellSize, spacing, padding);
                Handles.DrawBezier(from, to, from + Vector2.left * 40f, to + Vector2.right * 40f, Color.white, null, 2f);
                Handles.DrawSolidDisc(to, Vector3.forward, 2f);
            }
        }

        int IndexOfNodeId(string id)
        {
            var list = tree.Nodes;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].NodeId == id)
                    return i;
            }
            return -1;
        }

        Rect NodeRect(int index, Vector2 cellSize, Vector2 spacing, Vector4 padding)
        {
            var node = tree.Nodes[index];
            Vector2 position = ToCanvasPosition(node.EditorPosition, cellSize, spacing, padding);
            return new Rect(position.x, position.y, NodeWindowWidth, cellSize.y);
        }

        Vector2 ToCanvasPosition(Vector2 gridPosition, Vector2 cellSize, Vector2 spacing, Vector4 padding)
        {
            int col = Mathf.Clamp(Mathf.RoundToInt(gridPosition.x), 0, MaxColumns - 1);
            int row = Mathf.Max(0, Mathf.RoundToInt(gridPosition.y));

            float x = padding.x + col * (cellSize.x + spacing.x) + pan.x;
            float y = padding.z + row * (cellSize.y + spacing.y) + pan.y;
            return new Vector2(x, y);
        }

        Vector2 EditorPositionFromRect(Rect rect, Vector2 cellSize, Vector2 spacing, Vector4 padding)
        {
            float x = (rect.x - pan.x - padding.x) / (cellSize.x + spacing.x);
            float y = (rect.y - pan.y - padding.z) / (cellSize.y + spacing.y);
            return new Vector2(x, y);
        }

        Vector2 NodeCenter(int index, Vector2 cellSize, Vector2 spacing, Vector4 padding)
        {
            Rect rect = NodeRect(index, cellSize, spacing, padding);
            return new Vector2(rect.x + rect.width * 0.5f, rect.y + rect.height * 0.5f);
        }

        void DrawGrid(Rect rect, Vector2 cellSize, Vector2 spacing, Vector4 padding)
        {
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.05f);

            float stepX = cellSize.x + spacing.x;
            float stepY = cellSize.y + spacing.y;

            float offsetX = (pan.x + padding.x) % stepX;
            for (int c = 0; c <= MaxColumns; c++)
            {
                float x = rect.x + padding.x + c * stepX + offsetX;
                Handles.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax));
            }

            int horizontalLines = Mathf.CeilToInt((rect.height + Mathf.Abs(pan.y)) / stepY) + 2;
            float offsetY = (pan.y + padding.z) % stepY;
            for (int r = 0; r < horizontalLines; r++)
            {
                float y = rect.y + padding.z + r * stepY + offsetY;
                Handles.DrawLine(new Vector2(rect.x, y), new Vector2(rect.xMax, y));
            }

            Handles.EndGUI();
        }

        string GetNodeTitle(int index)
        {
            if (tree == null) return $"Node {index + 1}";
            var nodes = tree.Nodes;
            if (index < 0 || index >= nodes.Count) return $"Node {index + 1}";
            var node = nodes[index];
            if (node == null) return $"Node {index + 1}";

            string name = node.DisplayName;
            if (string.IsNullOrEmpty(name) && node.GrantedAbility)
            {
                name = node.GrantedAbility.DisplayName;
            }

            return string.IsNullOrEmpty(name) ? $"Node {index + 1}" : name;
        }

        void AddNodeAt(Vector2 gridPos, bool autoPlacement)
        {
            if (!tree || nodesProp == null) return;
            treeSO.Update();
            nodesProp.InsertArrayElementAtIndex(nodesProp.arraySize);
            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(nodesProp.arraySize - 1);
            nodeProp.FindPropertyRelative("displayName").stringValue = "New Skill";
            nodeProp.FindPropertyRelative("grantedAbility").objectReferenceValue = null;
            nodeProp.FindPropertyRelative("requiredLevel").intValue = 1;
            nodeProp.FindPropertyRelative("consumesSkillPoint").boolValue = true;

            Vector2 placement = autoPlacement ? FindFreePosition(Vector2.zero) : Snap(gridPos);
            nodeProp.FindPropertyRelative("editorPosition").vector2Value = placement;
            nodeProp.FindPropertyRelative("prerequisiteNodeIds").ClearArray();

            treeSO.ApplyModifiedProperties();
            selectedIndex = nodesProp.arraySize - 1;
            Repaint();
        }

        Vector2 FindFreePosition(Vector2 desired)
        {
            Vector2 snappedDesired = Snap(desired);
            if (!IsOccupied(snappedDesired))
            {
                return snappedDesired;
            }

            int maxSearch = Mathf.Max(tree.Nodes.Count + MaxColumns * 4, MaxColumns * 8);
            for (int i = 0; i < maxSearch; i++)
            {
                int row = i / MaxColumns;
                int col = i % MaxColumns;
                Vector2 candidate = new Vector2(col, row);
                if (!IsOccupied(candidate))
                {
                    return candidate;
                }
            }

            return snappedDesired;
        }

        bool IsOccupied(Vector2 gridPos)
        {
            foreach (var node in tree.Nodes)
            {
                if (node == null) continue;
                if (Mathf.Approximately(node.EditorPosition.x, gridPos.x) && Mathf.Approximately(node.EditorPosition.y, gridPos.y))
                {
                    return true;
                }
            }
            return false;
        }

        void DeleteSelectedNode()
        {
            if (!tree || nodesProp == null || selectedIndex < 0 || selectedIndex >= nodesProp.arraySize) return;
            treeSO.Update();
            nodesProp.DeleteArrayElementAtIndex(selectedIndex);
            treeSO.ApplyModifiedProperties();
            selectedIndex = Mathf.Clamp(selectedIndex, 0, nodesProp.arraySize - 1);
            Repaint();
        }

        void SnapNode(int index)
        {
            if (!tree || nodesProp == null || index < 0 || index >= nodesProp.arraySize) return;
            treeSO.Update();
            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(index);
            SerializedProperty posProp = nodeProp.FindPropertyRelative("editorPosition");
            posProp.vector2Value = Snap(posProp.vector2Value);
            treeSO.ApplyModifiedProperties();
            Repaint();
        }

        void SetNodeEditorPosition(int index, Vector2 position)
        {
            if (!tree || nodesProp == null || index < 0 || index >= nodesProp.arraySize) return;
            treeSO.Update();
            SerializedProperty nodeProp = nodesProp.GetArrayElementAtIndex(index);
            SerializedProperty posProp = nodeProp.FindPropertyRelative("editorPosition");
            Vector2 snapped = Snap(position);
            if ((posProp.vector2Value - snapped).sqrMagnitude > 0.0001f)
            {
                posProp.vector2Value = snapped;
                treeSO.ApplyModifiedProperties();
                Repaint();
            }
        }

        Vector2 MouseToTreePos(Vector2 mousePosition)
        {
            Vector2 cellSize = ResolveCellSize();
            Vector2 spacing = ResolveSpacing();
            Vector4 padding = ResolvePadding();

            float x = (mousePosition.x - pan.x - padding.x) / (cellSize.x + spacing.x);
            float y = (mousePosition.y - pan.y - padding.z) / (cellSize.y + spacing.y);
            return Snap(new Vector2(x, y));
        }

        Vector2 Snap(Vector2 position)
        {
            float x = Mathf.Clamp(Mathf.Round(position.x), 0f, MaxColumns - 1);
            float y = Mathf.Max(0f, Mathf.Round(position.y));
            return new Vector2(x, y);
        }

        Vector2 ResolveCellSize()
        {
            Vector2 size = DefaultCellSize;
            if (tree && tree.OverrideLayout)
            {
                size = tree.LayoutCellSize;
            }
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        Vector2 ResolveSpacing()
        {
            Vector2 spacing = DefaultSpacing;
            if (tree && tree.OverrideLayout)
            {
                spacing = tree.LayoutSpacing;
            }
            return new Vector2(Mathf.Max(0f, spacing.x), Mathf.Max(0f, spacing.y));
        }

        Vector4 ResolvePadding()
        {
            Vector4 padding = DefaultPadding;
            if (tree && tree.OverrideLayout)
            {
                padding = tree.LayoutPadding;
            }
            return new Vector4(Mathf.Max(0f, padding.x), Mathf.Max(0f, padding.y), Mathf.Max(0f, padding.z), Mathf.Max(0f, padding.w));
        }
    }
}





