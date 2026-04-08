#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Runtime;

namespace DungeonSystem.Editor
{
    /// <summary>
    /// Visual Recipe Builder — EditorWindow for creating and editing RoomRecipes.
    /// 
    /// Layout:
    ///   ┌──────────┬──────────────────┬──────────────┐
    ///   │  Palette  │   Room Preview   │   Recipe     │
    ///   │  Browser  │   (2D zones)     │   Entries    │
    ///   │           │                  │              │
    ///   └──────────┴──────────────────┴──────────────┘
    ///   │              Toolbar / Actions               │
    ///   └─────────────────────────────────────────────┘
    /// 
    /// Workflow:
    ///   1. Select or create a RoomRecipe
    ///   2. Assign a Palette for previewing available props
    ///   3. Click zones in the room preview (Center/Walls/Corners)
    ///   4. Pick tags from the palette browser panel
    ///   5. Props are matched visually with count indicators
    /// </summary>
    public class RecipeBuilderWindow : EditorWindow
    {
        // ── References ──
        RoomRecipe _recipe;
        RoomPiecePalette _palette;
        DungeonConfig _config;

        // ── UI State ──
        Vector2 _paletteScroll;
        Vector2 _recipeScroll;
        int _selectedPropIndex = -1;
        string _selectedZone = ""; // "center", "walls", "corners", "anywhere"
        string _paletteFilter = "";
        bool _showBatchPanel = false;
        int _previewRoomW = 1;
        int _previewRoomH = 1;

        // ── Cached data ──
        Dictionary<string, List<PieceEntry>> _taggedProps;
        bool _tagCacheDirty = true;

        // ── Layout constants ──
        const float PALETTE_WIDTH = 220f;
        const float RECIPE_WIDTH = 280f;
        const float TOOLBAR_HEIGHT = 80f;

        [MenuItem("DungeonSystem/Recipe Builder")]
        public static void ShowWindow()
        {
            var w = GetWindow<RecipeBuilderWindow>("Recipe Builder");
            w.minSize = new Vector2(800, 500);
        }

        void OnEnable()
        {
            AutoFindAssets();
        }

        void AutoFindAssets()
        {
            if (_palette == null)
            {
                var guids = AssetDatabase.FindAssets("t:RoomPiecePalette");
                if (guids.Length > 0)
                    _palette = AssetDatabase.LoadAssetAtPath<RoomPiecePalette>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (_config == null)
            {
                var guids = AssetDatabase.FindAssets("t:DungeonConfig");
                if (guids.Length > 0)
                    _config = AssetDatabase.LoadAssetAtPath<DungeonConfig>(
                        AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            _tagCacheDirty = true;
        }

        void OnSelectionChange()
        {
            if (Selection.activeObject is RoomRecipe r)
            {
                _recipe = r;
                _selectedPropIndex = -1;
                Repaint();
            }
        }

        void OnGUI()
        {
            if (_tagCacheDirty && _palette != null) RebuildTagCache();

            DrawTopBar();

            if (_recipe == null)
            {
                DrawNoRecipeView();
                return;
            }

            EditorGUILayout.BeginHorizontal();

            // Left panel: palette browser
            EditorGUILayout.BeginVertical(GUILayout.Width(PALETTE_WIDTH));
            DrawPalettePanel();
            EditorGUILayout.EndVertical();

            // Center panel: room preview
            DrawRoomPreview();

            // Right panel: recipe entries
            EditorGUILayout.BeginVertical(GUILayout.Width(RECIPE_WIDTH));
            DrawRecipePanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();

            // Bottom toolbar
            DrawBottomToolbar();
        }

        // ════════════════════════════════════════════════════════════════
        //  TOP BAR
        // ════════════════════════════════════════════════════════════════

        void DrawTopBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Recipe selector
            EditorGUI.BeginChangeCheck();
            _recipe = (RoomRecipe)EditorGUILayout.ObjectField(
                _recipe, typeof(RoomRecipe), false, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck()) _selectedPropIndex = -1;

            // Palette selector
            EditorGUI.BeginChangeCheck();
            _palette = (RoomPiecePalette)EditorGUILayout.ObjectField(
                _palette, typeof(RoomPiecePalette), false, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck()) _tagCacheDirty = true;

            GUILayout.FlexibleSpace();

            // Quick create
            if (GUILayout.Button("New Recipe", EditorStyles.toolbarButton, GUILayout.Width(80)))
                ShowNewRecipeMenu();

            if (GUILayout.Button("Batch ▾", EditorStyles.toolbarButton, GUILayout.Width(60)))
                _showBatchPanel = !_showBatchPanel;

            EditorGUILayout.EndHorizontal();

            if (_showBatchPanel)
                DrawBatchPanel();
        }

        void ShowNewRecipeMenu()
        {
            var menu = new GenericMenu();
            foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
            {
                if (type == RoomType.Corridor || type == RoomType.Junction) continue;
                var t = type; // capture
                menu.AddItem(new GUIContent($"Empty/{t}"), false, () => CreateNewRecipe(t, false));
                menu.AddItem(new GUIContent($"With Defaults/{t}"), false, () => CreateNewRecipe(t, true));
            }
            menu.ShowAsContext();
        }

        void CreateNewRecipe(RoomType type, bool withDefaults)
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Recipe", $"Recipe_{type}", "asset", "Choose location");
            if (string.IsNullOrEmpty(path)) return;

            RoomRecipe recipe;
            if (withDefaults)
            {
                recipe = RecipeAutoGenerator.GenerateSingle(type,
                    System.IO.Path.GetDirectoryName(path));
            }
            else
            {
                recipe = ScriptableObject.CreateInstance<RoomRecipe>();
                recipe.roomType = type;
                recipe.displayName = type.ToString();
                recipe.props = new List<RecipePropEntry>();
                recipe.spawnPoints = new List<RecipeSpawnEntry>();
                AssetDatabase.CreateAsset(recipe, path);
                AssetDatabase.SaveAssets();
            }

            _recipe = recipe;
            Selection.activeObject = recipe;
        }

        void DrawBatchPanel()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);

            _config = (DungeonConfig)EditorGUILayout.ObjectField(
                "DungeonConfig", _config, typeof(DungeonConfig), false);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
            if (GUILayout.Button("Generate ALL Default Recipes", GUILayout.Height(28)))
            {
                var recipes = RecipeAutoGenerator.GenerateAllDefaults();
                EditorUtility.DisplayDialog("Done",
                    $"Created/found {recipes.Count} recipes.", "OK");
            }

            GUI.enabled = _config != null;
            GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
            if (GUILayout.Button("Generate & Assign to Config", GUILayout.Height(28)))
            {
                int count = RecipeAutoGenerator.GenerateAndAssign(_config);
                EditorUtility.DisplayDialog("Done",
                    $"Added {count} new recipes to DungeonConfig.", "OK");
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ════════════════════════════════════════════════════════════════
        //  NO RECIPE VIEW
        // ════════════════════════════════════════════════════════════════

        void DrawNoRecipeView()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(GUILayout.Width(400));
            EditorGUILayout.LabelField("No Recipe Selected", new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 16, alignment = TextAnchor.MiddleCenter });
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                "Select a RoomRecipe asset in the Project window,\nor create a new one:",
                new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true });
            EditorGUILayout.Space(12);

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
            if (GUILayout.Button("Create New Recipe...", GUILayout.Height(32)))
                ShowNewRecipeMenu();

            EditorGUILayout.Space(4);
            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.9f);
            if (GUILayout.Button("Generate All Defaults", GUILayout.Height(28)))
            {
                var recipes = RecipeAutoGenerator.GenerateAllDefaults();
                if (recipes.Count > 0) _recipe = recipes[0];
                EditorUtility.DisplayDialog("Done", $"Created {recipes.Count} recipes.", "OK");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        // ════════════════════════════════════════════════════════════════
        //  PALETTE BROWSER (left panel)
        // ════════════════════════════════════════════════════════════════

        void DrawPalettePanel()
        {
            EditorGUILayout.LabelField("Palette Tags", EditorStyles.boldLabel);

            if (_palette == null)
            {
                EditorGUILayout.HelpBox("Assign a palette in the toolbar.", MessageType.Info);
                return;
            }

            _paletteFilter = EditorGUILayout.TextField(_paletteFilter, EditorStyles.toolbarSearchField);

            _paletteScroll = EditorGUILayout.BeginScrollView(_paletteScroll);

            if (_taggedProps != null)
            {
                foreach (var kv in _taggedProps.OrderByDescending(x => x.Value.Count))
                {
                    if (!string.IsNullOrEmpty(_paletteFilter) &&
                        !kv.Key.Contains(_paletteFilter.ToLower()))
                        continue;

                    bool isUsed = IsTagUsedInRecipe(kv.Key);

                    EditorGUILayout.BeginHorizontal();

                    // Tag button: click to add to selected entry or create new entry
                    Color c = isUsed
                        ? new Color(0.3f, 0.7f, 0.4f)
                        : new Color(0.85f, 0.85f, 0.85f);
                    GUI.backgroundColor = c;

                    if (GUILayout.Button($"{kv.Key} ({kv.Value.Count})",
                        EditorStyles.miniButton, GUILayout.Height(20)))
                    {
                        OnPaletteTagClicked(kv.Key);
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();

                    // Show prefab names under each tag when hovered/expanded
                    if (isUsed)
                    {
                        EditorGUI.indentLevel++;
                        foreach (var entry in kv.Value.Take(5))
                        {
                            string prefabName = entry.prefab != null ? entry.prefab.name : "(null)";
                            EditorGUILayout.LabelField($"  · {prefabName}", EditorStyles.miniLabel);
                        }
                        if (kv.Value.Count > 5)
                            EditorGUILayout.LabelField($"  ... +{kv.Value.Count - 5} more", EditorStyles.miniLabel);
                        EditorGUI.indentLevel--;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        void OnPaletteTagClicked(string tag)
        {
            if (_recipe == null) return;

            // If an entry is selected, add tag to it
            if (_selectedPropIndex >= 0 && _selectedPropIndex < _recipe.props.Count)
            {
                Undo.RecordObject(_recipe, "Add tag to entry");
                var entry = _recipe.props[_selectedPropIndex];
                var tags = new HashSet<string>(entry.requiredTags ?? new string[0]);
                if (tags.Contains(tag)) tags.Remove(tag);
                else tags.Add(tag);
                entry.requiredTags = tags.ToArray();
                EditorUtility.SetDirty(_recipe);
            }
            else
            {
                // Create new entry with this tag
                Undo.RecordObject(_recipe, "Add prop entry from palette");
                if (_recipe.props == null) _recipe.props = new List<RecipePropEntry>();
                _recipe.props.Add(new RecipePropEntry
                {
                    requiredTags = new[] { tag },
                    importance = PropImportance.Minor,
                    minCount = 0,
                    maxCount = 3,
                    chance = 0.5f,
                    preferWalls = false,
                    preferCenter = false,
                    preferCorners = false
                });
                _selectedPropIndex = _recipe.props.Count - 1;
                EditorUtility.SetDirty(_recipe);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  ROOM PREVIEW (center panel)
        // ════════════════════════════════════════════════════════════════

        void DrawRoomPreview()
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Room size selector
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Preview Size:", GUILayout.Width(80));
            _previewRoomW = EditorGUILayout.IntSlider(_previewRoomW, 1, 3, GUILayout.Width(120));
            EditorGUILayout.LabelField("×", GUILayout.Width(14));
            _previewRoomH = EditorGUILayout.IntSlider(_previewRoomH, 1, 3, GUILayout.Width(120));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Draw the 2D room visualization
            Rect drawArea = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (drawArea.width > 10 && drawArea.height > 10)
                DrawRoomGrid(drawArea);

            EditorGUILayout.EndVertical();
        }

        void DrawRoomGrid(Rect area)
        {
            // Background
            EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.18f));

            float padding = 30f;
            Rect inner = new Rect(area.x + padding, area.y + padding,
                area.width - padding * 2, area.height - padding * 2);

            float roomAspect = (float)_previewRoomW / _previewRoomH;
            float areaAspect = inner.width / inner.height;

            float roomW, roomH;
            if (roomAspect > areaAspect)
            {
                roomW = inner.width;
                roomH = roomW / roomAspect;
            }
            else
            {
                roomH = inner.height;
                roomW = roomH * roomAspect;
            }

            Rect roomRect = new Rect(
                inner.x + (inner.width - roomW) * 0.5f,
                inner.y + (inner.height - roomH) * 0.5f,
                roomW, roomH);

            // Room floor
            EditorGUI.DrawRect(roomRect, new Color(0.25f, 0.25f, 0.3f));

            // Zone rects
            float inset = 20f;
            float cornerSize = Mathf.Min(roomW, roomH) * 0.2f;

            // Center zone
            float cw = roomW * 0.35f;
            float ch = roomH * 0.35f;
            Rect centerRect = new Rect(roomRect.center.x - cw * 0.5f, roomRect.center.y - ch * 0.5f, cw, ch);

            // Draw zones with click handling
            DrawClickableZone(roomRect, "anywhere", new Color(0.3f, 0.3f, 0.35f, 0.0f), "Anywhere");
            DrawClickableZone(centerRect, "center", new Color(0.3f, 0.7f, 0.3f, 0.15f), "Center");

            // Wall zones
            Rect northWall = new Rect(roomRect.x + cornerSize, roomRect.y, roomRect.width - cornerSize * 2, inset);
            Rect southWall = new Rect(roomRect.x + cornerSize, roomRect.yMax - inset, roomRect.width - cornerSize * 2, inset);
            Rect eastWall = new Rect(roomRect.xMax - inset, roomRect.y + cornerSize, inset, roomRect.height - cornerSize * 2);
            Rect westWall = new Rect(roomRect.x, roomRect.y + cornerSize, inset, roomRect.height - cornerSize * 2);

            Color wallZoneColor = new Color(0.4f, 0.6f, 0.9f, 0.2f);
            DrawClickableZone(northWall, "walls", wallZoneColor, "");
            DrawClickableZone(southWall, "walls", wallZoneColor, "");
            DrawClickableZone(eastWall, "walls", wallZoneColor, "");
            DrawClickableZone(westWall, "walls", wallZoneColor, "");

            // Corner zones
            Color cornerColor = new Color(0.9f, 0.6f, 0.3f, 0.2f);
            Rect[] cornerRects = {
                new Rect(roomRect.x, roomRect.y, cornerSize, cornerSize),
                new Rect(roomRect.xMax - cornerSize, roomRect.y, cornerSize, cornerSize),
                new Rect(roomRect.x, roomRect.yMax - cornerSize, cornerSize, cornerSize),
                new Rect(roomRect.xMax - cornerSize, roomRect.yMax - cornerSize, cornerSize, cornerSize)
            };
            foreach (var cr in cornerRects)
                DrawClickableZone(cr, "corners", cornerColor, "");

            // Labels
            DrawZoneLabel(centerRect, "CENTER");
            DrawZoneLabel(new Rect(roomRect.x, roomRect.y - 18, roomRect.width, 18), "WALLS (edges)");
            DrawZoneLabel(cornerRects[0], "C");
            DrawZoneLabel(cornerRects[1], "C");
            DrawZoneLabel(cornerRects[2], "C");
            DrawZoneLabel(cornerRects[3], "C");

            // Room type label
            if (_recipe != null)
            {
                var typeStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = GetTypeColor(_recipe.roomType) },
                    fontSize = 12
                };
                GUI.Label(new Rect(roomRect.x, roomRect.yMax + 4, roomRect.width, 20),
                    $"{_recipe.roomType} — {_previewRoomW}×{_previewRoomH}", typeStyle);
            }

            // Draw prop entry indicators
            DrawPropIndicators(roomRect, centerRect, cornerRects);

            // Wall outline
            DrawRectOutline(roomRect, new Color(0.6f, 0.6f, 0.6f, 0.8f), 2f);
        }

        void DrawClickableZone(Rect rect, string zoneName, Color color, string label)
        {
            EditorGUI.DrawRect(rect, color);

            bool isSelected = _selectedZone == zoneName;
            if (isSelected)
            {
                Color highlight = new Color(1f, 1f, 0.3f, 0.15f);
                EditorGUI.DrawRect(rect, highlight);
                DrawRectOutline(rect, new Color(1f, 1f, 0.3f, 0.6f), 1f);
            }

            Event e = Event.current;
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                _selectedZone = isSelected ? "" : zoneName;

                // Auto-set placement hints on selected entry
                if (_selectedPropIndex >= 0 && _recipe != null &&
                    _selectedPropIndex < _recipe.props.Count)
                {
                    Undo.RecordObject(_recipe, "Set placement zone");
                    var entry = _recipe.props[_selectedPropIndex];
                    entry.preferCenter = zoneName == "center";
                    entry.preferWalls = zoneName == "walls";
                    entry.preferCorners = zoneName == "corners";
                    EditorUtility.SetDirty(_recipe);
                }

                e.Use();
                Repaint();
            }
        }

        void DrawPropIndicators(Rect roomRect, Rect centerRect, Rect[] cornerRects)
        {
            if (_recipe?.props == null) return;

            int centerCount = 0, wallCount = 0, cornerCount = 0, anyCount = 0;

            for (int i = 0; i < _recipe.props.Count; i++)
            {
                var entry = _recipe.props[i];
                if (entry.preferCenter) centerCount += entry.maxCount;
                else if (entry.preferWalls) wallCount += entry.maxCount;
                else if (entry.preferCorners) cornerCount += entry.maxCount;
                else anyCount += entry.maxCount;
            }

            var indicatorStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            if (centerCount > 0)
            {
                Rect badge = new Rect(centerRect.center.x - 12, centerRect.center.y + 8, 24, 16);
                EditorGUI.DrawRect(badge, new Color(0.2f, 0.6f, 0.2f, 0.8f));
                GUI.Label(badge, $"×{centerCount}", indicatorStyle);
            }
            if (wallCount > 0)
            {
                Rect badge = new Rect(roomRect.xMax - 40, roomRect.center.y - 8, 35, 16);
                EditorGUI.DrawRect(badge, new Color(0.3f, 0.5f, 0.8f, 0.8f));
                GUI.Label(badge, $"×{wallCount}", indicatorStyle);
            }
            if (cornerCount > 0)
            {
                Rect badge = new Rect(cornerRects[0].center.x - 10, cornerRects[0].center.y + 8, 24, 16);
                EditorGUI.DrawRect(badge, new Color(0.8f, 0.5f, 0.2f, 0.8f));
                GUI.Label(badge, $"×{cornerCount}", indicatorStyle);
            }
        }

        void DrawZoneLabel(Rect rect, string text)
        {
            var style = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 1f, 1f, 0.4f) },
                fontSize = 9
            };
            GUI.Label(rect, text, style);
        }

        void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        // ════════════════════════════════════════════════════════════════
        //  RECIPE ENTRIES (right panel)
        // ════════════════════════════════════════════════════════════════

        void DrawRecipePanel()
        {
            EditorGUILayout.LabelField("Recipe Entries", EditorStyles.boldLabel);

            if (_recipe == null) return;

            // Density controls
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _recipe.densityMultiplier = EditorGUILayout.Slider("Density", _recipe.densityMultiplier, 0.1f, 3f);
            _recipe.maxFillRatio = EditorGUILayout.Slider("Max Fill", _recipe.maxFillRatio, 0.1f, 0.9f);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);

            _recipeScroll = EditorGUILayout.BeginScrollView(_recipeScroll);

            // Prop entries
            if (_recipe.props != null)
            {
                EditorGUILayout.LabelField($"Props [{_recipe.props.Count}]", EditorStyles.miniBoldLabel);

                for (int i = 0; i < _recipe.props.Count; i++)
                {
                    var entry = _recipe.props[i];
                    bool isSelected = i == _selectedPropIndex;

                    // Entry box
                    Color bgColor = isSelected ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.22f, 0.22f, 0.22f);
                    var boxStyle = new GUIStyle("box") { padding = new RectOffset(4, 4, 2, 2) };

                    GUI.backgroundColor = bgColor;
                    EditorGUILayout.BeginVertical(boxStyle);
                    GUI.backgroundColor = Color.white;

                    // Header: importance + tags + actions
                    EditorGUILayout.BeginHorizontal();

                    // Click to select
                    Color impColor = entry.importance switch
                    {
                        PropImportance.Major => new Color(0.9f, 0.4f, 0.3f),
                        PropImportance.Minor => new Color(0.4f, 0.7f, 0.9f),
                        _ => new Color(0.6f, 0.6f, 0.6f)
                    };
                    GUI.backgroundColor = impColor;
                    if (GUILayout.Button(entry.importance.ToString().Substring(0, 3),
                        EditorStyles.miniButton, GUILayout.Width(35)))
                    {
                        _selectedPropIndex = isSelected ? -1 : i;
                    }
                    GUI.backgroundColor = Color.white;

                    // Tags
                    string tags = entry.requiredTags != null && entry.requiredTags.Length > 0
                        ? string.Join("+", entry.requiredTags) : "(empty)";
                    if (GUILayout.Button(tags, EditorStyles.miniLabel))
                        _selectedPropIndex = i;

                    // Count
                    EditorGUILayout.LabelField($"{entry.minCount}-{entry.maxCount}",
                        EditorStyles.miniLabel, GUILayout.Width(30));

                    // Zone indicator
                    string zone = entry.preferCenter ? "C" :
                        entry.preferWalls ? "W" :
                        entry.preferCorners ? "K" : "?";
                    EditorGUILayout.LabelField(zone, EditorStyles.miniLabel, GUILayout.Width(14));

                    // Match count from palette
                    if (_palette != null)
                    {
                        int matchCount = CountMatches(entry);
                        Color mc = matchCount > 0 ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.9f, 0.4f, 0.3f);
                        GUI.color = mc;
                        EditorGUILayout.LabelField($"[{matchCount}]", EditorStyles.miniLabel, GUILayout.Width(26));
                        GUI.color = Color.white;
                    }

                    // Delete
                    GUI.backgroundColor = new Color(0.7f, 0.25f, 0.25f);
                    if (GUILayout.Button("×", GUILayout.Width(18), GUILayout.Height(16)))
                    {
                        Undo.RecordObject(_recipe, "Remove prop entry");
                        _recipe.props.RemoveAt(i);
                        if (_selectedPropIndex >= _recipe.props.Count)
                            _selectedPropIndex = _recipe.props.Count - 1;
                        EditorUtility.SetDirty(_recipe);
                        GUI.backgroundColor = Color.white;
                        break;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();

                    // Detail row when selected
                    if (isSelected)
                    {
                        EditorGUI.indentLevel++;

                        entry.importance = (PropImportance)EditorGUILayout.EnumPopup("Importance", entry.importance);
                        entry.minCount = EditorGUILayout.IntField("Min", entry.minCount);
                        entry.maxCount = EditorGUILayout.IntField("Max", entry.maxCount);
                        entry.chance = EditorGUILayout.Slider("Chance", entry.chance, 0f, 1f);

                        EditorGUILayout.BeginHorizontal();
                        entry.preferCenter = GUILayout.Toggle(entry.preferCenter, "Center", "Button");
                        entry.preferWalls = GUILayout.Toggle(entry.preferWalls, "Walls", "Button");
                        entry.preferCorners = GUILayout.Toggle(entry.preferCorners, "Corners", "Button");
                        EditorGUILayout.EndHorizontal();

                        if (GUI.changed) EditorUtility.SetDirty(_recipe);
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.Space(8);

            // Spawn entries
            if (_recipe.spawnPoints != null && _recipe.spawnPoints.Count > 0)
            {
                EditorGUILayout.LabelField($"Spawns [{_recipe.spawnPoints.Count}]", EditorStyles.miniBoldLabel);

                for (int i = 0; i < _recipe.spawnPoints.Count; i++)
                {
                    var sp = _recipe.spawnPoints[i];
                    EditorGUILayout.BeginHorizontal();

                    Color spColor = GetSpawnColor(sp.pointType);
                    GUI.backgroundColor = spColor;
                    sp.pointType = (SpawnPointType)EditorGUILayout.EnumPopup(sp.pointType, GUILayout.Width(90));
                    GUI.backgroundColor = Color.white;

                    sp.count = EditorGUILayout.IntField(sp.count, GUILayout.Width(30));
                    sp.placement = (SpawnPlacement)EditorGUILayout.EnumPopup(sp.placement, GUILayout.Width(100));

                    GUI.backgroundColor = new Color(0.7f, 0.25f, 0.25f);
                    if (GUILayout.Button("×", GUILayout.Width(18)))
                    {
                        Undo.RecordObject(_recipe, "Remove spawn");
                        _recipe.spawnPoints.RemoveAt(i);
                        EditorUtility.SetDirty(_recipe);
                        GUI.backgroundColor = Color.white;
                        break;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndScrollView();

            // Add buttons
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
            if (GUILayout.Button("+ Prop", GUILayout.Height(22)))
            {
                Undo.RecordObject(_recipe, "Add prop entry");
                if (_recipe.props == null) _recipe.props = new List<RecipePropEntry>();
                _recipe.props.Add(new RecipePropEntry
                {
                    requiredTags = new string[0],
                    importance = PropImportance.Minor,
                    maxCount = 3,
                    chance = 0.5f
                });
                _selectedPropIndex = _recipe.props.Count - 1;
                EditorUtility.SetDirty(_recipe);
            }

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.8f);
            if (GUILayout.Button("+ Spawn", GUILayout.Height(22)))
            {
                Undo.RecordObject(_recipe, "Add spawn entry");
                if (_recipe.spawnPoints == null) _recipe.spawnPoints = new List<RecipeSpawnEntry>();
                _recipe.spawnPoints.Add(new RecipeSpawnEntry
                {
                    pointType = SpawnPointType.Enemy,
                    count = 1,
                    placement = SpawnPlacement.Random,
                    priority = 5
                });
                EditorUtility.SetDirty(_recipe);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
        }

        // ════════════════════════════════════════════════════════════════
        //  BOTTOM TOOLBAR
        // ════════════════════════════════════════════════════════════════

        void DrawBottomToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (_recipe != null)
            {
                EditorGUILayout.LabelField(
                    $"Editing: {_recipe.name}  |  Type: {_recipe.roomType}  |  " +
                    $"Props: {_recipe.props?.Count ?? 0}  Spawns: {_recipe.spawnPoints?.Count ?? 0}",
                    EditorStyles.miniLabel);
            }

            GUILayout.FlexibleSpace();

            if (_recipe != null)
            {
                if (GUILayout.Button("Select Asset", EditorStyles.toolbarButton))
                    Selection.activeObject = _recipe;

                if (GUILayout.Button("Reset to Default", EditorStyles.toolbarButton))
                {
                    if (EditorUtility.DisplayDialog("Reset?",
                        $"Reset recipe to auto-generated defaults for {_recipe.roomType}?",
                        "Reset", "Cancel"))
                    {
                        string dir = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(_recipe));
                        var temp = RecipeAutoGenerator.GenerateSingle(_recipe.roomType,
                            string.IsNullOrEmpty(dir) ? "Assets/Data/DungeonRecipes" : dir);
                        if (temp != null)
                        {
                            Undo.RecordObject(_recipe, "Reset recipe");
                            _recipe.props = temp.props;
                            _recipe.spawnPoints = temp.spawnPoints;
                            _recipe.densityMultiplier = temp.densityMultiplier;
                            _recipe.maxFillRatio = temp.maxFillRatio;
                            EditorUtility.SetDirty(_recipe);
                        }
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ════════════════════════════════════════════════════════════════
        //  TAG CACHE & HELPERS
        // ════════════════════════════════════════════════════════════════

        void RebuildTagCache()
        {
            _taggedProps = new Dictionary<string, List<PieceEntry>>();

            if (_palette == null) return;

            RoomType type = _recipe != null ? _recipe.roomType : RoomType.Combat;
            var allProps = _palette.GetAllProps(type);

            foreach (var prop in allProps)
            {
                if (prop?.placementProfile?.tags == null) continue;
                foreach (var tag in prop.placementProfile.tags)
                {
                    if (!_taggedProps.ContainsKey(tag))
                        _taggedProps[tag] = new List<PieceEntry>();
                    _taggedProps[tag].Add(prop);
                }
            }

            // Also add known tags that have 0 matches (so user can see what's missing)
            foreach (var tag in PropTags.All)
                if (!_taggedProps.ContainsKey(tag))
                    _taggedProps[tag] = new List<PieceEntry>();

            _tagCacheDirty = false;
        }

        bool IsTagUsedInRecipe(string tag)
        {
            if (_recipe?.props == null) return false;
            foreach (var entry in _recipe.props)
                if (entry.requiredTags != null)
                    foreach (var t in entry.requiredTags)
                        if (t == tag) return true;
            return false;
        }

        int CountMatches(RecipePropEntry entry)
        {
            if (_taggedProps == null || entry.requiredTags == null) return 0;
            int count = 0;
            foreach (var tag in entry.requiredTags)
                if (_taggedProps.TryGetValue(tag, out var list))
                    count += list.Count;
            return count;
        }

        static Color GetTypeColor(RoomType type) => type switch
        {
            RoomType.Start => new Color(0.3f, 0.9f, 0.3f),
            RoomType.Boss => new Color(0.9f, 0.2f, 0.2f),
            RoomType.MiniBoss => new Color(0.9f, 0.5f, 0.2f),
            RoomType.Combat => new Color(0.7f, 0.7f, 0.7f),
            RoomType.Loot => new Color(0.9f, 0.8f, 0.2f),
            RoomType.Puzzle => new Color(0.2f, 0.6f, 0.9f),
            RoomType.Shop => new Color(0.4f, 0.9f, 0.4f),
            RoomType.SafeRoom => new Color(0.4f, 0.7f, 0.9f),
            RoomType.SecretRoom => new Color(0.7f, 0.3f, 0.7f),
            RoomType.Trap => new Color(0.9f, 0.6f, 0.2f),
            _ => Color.white
        };

        static Color GetSpawnColor(SpawnPointType type) => type switch
        {
            SpawnPointType.Enemy => new Color(0.9f, 0.4f, 0.4f),
            SpawnPointType.BossSpawn => new Color(1f, 0.2f, 0.2f),
            SpawnPointType.Chest => new Color(0.9f, 0.8f, 0.2f),
            SpawnPointType.Item => new Color(0.9f, 0.7f, 0.3f),
            SpawnPointType.NPC => new Color(0.4f, 0.8f, 0.4f),
            SpawnPointType.PlayerSpawn => new Color(0.3f, 0.6f, 1f),
            _ => Color.white
        };
    }
}
#endif
