#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Runtime;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(RoomRecipe))]
    public class RoomRecipeEditor : UnityEditor.Editor
    {
        // ── Serialized properties ──
        SerializedProperty _roomType;
        SerializedProperty _displayName;
        SerializedProperty _props;
        SerializedProperty _spawnPoints;
        SerializedProperty _densityMultiplier;
        SerializedProperty _maxFillRatio;

        // ── State ──
        RoomPiecePalette _previewPalette;
        bool _showTagPicker = false;
        int _tagPickerForIndex = -1;
        bool _showValidation = true;
        bool _showPropsSection = true;
        bool _showSpawnSection = true;
        bool _showQuickAdd = false;
        Vector2 _propsScroll;

        // ── Cached palette analysis ──
        Dictionary<string, int> _tagMatchCounts;
        List<string> _orphanTags;
        bool _analysisDirty = true;

        void OnEnable()
        {
            _roomType = serializedObject.FindProperty("roomType");
            _displayName = serializedObject.FindProperty("displayName");
            _props = serializedObject.FindProperty("props");
            _spawnPoints = serializedObject.FindProperty("spawnPoints");
            _densityMultiplier = serializedObject.FindProperty("densityMultiplier");
            _maxFillRatio = serializedObject.FindProperty("maxFillRatio");
            _analysisDirty = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var recipe = (RoomRecipe)target;

            DrawHeader(recipe);
            EditorGUILayout.Space(4);
            DrawPaletteSelector();
            EditorGUILayout.Space(4);
            DrawValidation(recipe);
            EditorGUILayout.Space(8);
            DrawDensitySection();
            EditorGUILayout.Space(8);
            DrawPropsSection(recipe);
            EditorGUILayout.Space(8);
            DrawSpawnPointsSection(recipe);
            EditorGUILayout.Space(12);
            DrawFooterActions(recipe);

            serializedObject.ApplyModifiedProperties();
        }

        // ════════════════════════════════════════════════════════════════
        //  HEADER
        // ════════════════════════════════════════════════════════════════

        void DrawHeader(RoomRecipe recipe)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            Color typeColor = GetTypeColor(recipe.roomType);
            GUI.color = typeColor;
            EditorGUILayout.LabelField($"◆ Room Recipe: {recipe.roomType}", headerStyle);
            GUI.color = Color.white;

            EditorGUILayout.PropertyField(_roomType);
            EditorGUILayout.PropertyField(_displayName);

            // Summary line
            int propCount = recipe.props?.Count ?? 0;
            int spawnCount = recipe.spawnPoints?.Count ?? 0;
            EditorGUILayout.LabelField(
                $"{propCount} prop entries  ·  {spawnCount} spawn entries  ·  density {recipe.densityMultiplier:F1}x",
                EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        // ════════════════════════════════════════════════════════════════
        //  PALETTE SELECTOR (for preview/validation)
        // ════════════════════════════════════════════════════════════════

        void DrawPaletteSelector()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _previewPalette = (RoomPiecePalette)EditorGUILayout.ObjectField(
                "Preview Palette", _previewPalette, typeof(RoomPiecePalette), false);
            if (EditorGUI.EndChangeCheck()) _analysisDirty = true;

            if (_previewPalette == null)
            {
                // Try auto-find palette
                if (GUILayout.Button("Find", GUILayout.Width(50)))
                {
                    var guids = AssetDatabase.FindAssets("t:RoomPiecePalette");
                    if (guids.Length > 0)
                    {
                        _previewPalette = AssetDatabase.LoadAssetAtPath<RoomPiecePalette>(
                            AssetDatabase.GUIDToAssetPath(guids[0]));
                        _analysisDirty = true;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_previewPalette == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a RoomPiecePalette above to see tag match counts and validation.",
                    MessageType.Info);
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  VALIDATION
        // ════════════════════════════════════════════════════════════════

        void DrawValidation(RoomRecipe recipe)
        {
            if (_previewPalette == null) return;
            if (_analysisDirty) RefreshAnalysis(recipe);

            _showValidation = EditorGUILayout.Foldout(_showValidation, "Validation", true, EditorStyles.foldoutHeader);
            if (!_showValidation) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (_orphanTags != null && _orphanTags.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"⚠ {_orphanTags.Count} tag(s) in recipe have NO matching props in palette:\n" +
                    string.Join(", ", _orphanTags),
                    MessageType.Warning);
            }
            else if (recipe.props != null && recipe.props.Count > 0)
            {
                EditorGUILayout.HelpBox("✓ All tags match at least one prop in the palette.", MessageType.Info);
            }

            // Show tag match summary
            if (_tagMatchCounts != null && _tagMatchCounts.Count > 0)
            {
                EditorGUILayout.LabelField("Tag Coverage:", EditorStyles.miniLabel);
                foreach (var kv in _tagMatchCounts.OrderByDescending(x => x.Value))
                {
                    Color c = kv.Value == 0 ? new Color(1f, 0.5f, 0.3f) : new Color(0.5f, 0.8f, 0.5f);
                    GUI.color = c;
                    EditorGUILayout.LabelField($"  {kv.Key}", $"{kv.Value} props", EditorStyles.miniLabel);
                }
                GUI.color = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        void RefreshAnalysis(RoomRecipe recipe)
        {
            _tagMatchCounts = new Dictionary<string, int>();
            _orphanTags = new List<string>();

            if (_previewPalette == null || recipe.props == null)
            {
                _analysisDirty = false;
                return;
            }

            var allPaletteProps = _previewPalette.GetAllProps(recipe.roomType);

            foreach (var entry in recipe.props)
            {
                if (entry.requiredTags == null) continue;
                foreach (var tag in entry.requiredTags)
                {
                    if (_tagMatchCounts.ContainsKey(tag)) continue;

                    int count = 0;
                    foreach (var pp in allPaletteProps)
                    {
                        if (pp?.placementProfile?.tags == null) continue;
                        foreach (var pt in pp.placementProfile.tags)
                            if (pt == tag) { count++; break; }
                    }

                    _tagMatchCounts[tag] = count;
                    if (count == 0) _orphanTags.Add(tag);
                }
            }

            _analysisDirty = false;
        }

        // ════════════════════════════════════════════════════════════════
        //  DENSITY SECTION
        // ════════════════════════════════════════════════════════════════

        void DrawDensitySection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_densityMultiplier, new GUIContent("Multiplier"));
            EditorGUILayout.PropertyField(_maxFillRatio, new GUIContent("Max Fill Ratio"));
            EditorGUILayout.EndVertical();
        }

        // ════════════════════════════════════════════════════════════════
        //  PROPS SECTION
        // ════════════════════════════════════════════════════════════════

        void DrawPropsSection(RoomRecipe recipe)
        {
            _showPropsSection = EditorGUILayout.Foldout(_showPropsSection,
                $"Prop Entries [{recipe.props?.Count ?? 0}]", true, EditorStyles.foldoutHeader);
            if (!_showPropsSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (recipe.props == null || recipe.props.Count == 0)
            {
                EditorGUILayout.HelpBox("No prop entries. Use Quick Add below or add manually.", MessageType.None);
            }
            else
            {
                for (int i = 0; i < recipe.props.Count; i++)
                {
                    DrawPropEntry(recipe, i);
                    if (i < recipe.props.Count - 1)
                        DrawThinSeparator();
                }
            }

            EditorGUILayout.Space(4);
            DrawPropActions(recipe);

            EditorGUILayout.EndVertical();
        }

        void DrawPropEntry(RoomRecipe recipe, int index)
        {
            var entry = recipe.props[index];
            var prop = _props.GetArrayElementAtIndex(index);

            // ── Header row: importance badge + tags + delete ──
            EditorGUILayout.BeginHorizontal();

            // Importance badge
            Color badgeColor = entry.importance switch
            {
                PropImportance.Major => new Color(0.9f, 0.4f, 0.3f),
                PropImportance.Minor => new Color(0.4f, 0.7f, 0.9f),
                PropImportance.Clutter => new Color(0.6f, 0.6f, 0.6f),
                _ => Color.white
            };

            GUI.backgroundColor = badgeColor;
            EditorGUILayout.PropertyField(
                prop.FindPropertyRelative("importance"), GUIContent.none, GUILayout.Width(70));
            GUI.backgroundColor = Color.white;

            // Tags display
            string tagDisplay = entry.requiredTags != null && entry.requiredTags.Length > 0
                ? string.Join(" + ", entry.requiredTags)
                : "(no tags)";

            // Match count
            int matchCount = GetMatchCount(entry);
            string matchLabel = _previewPalette != null ? $"[{matchCount} match]" : "";
            Color matchColor = matchCount == 0 && _previewPalette != null
                ? new Color(1f, 0.5f, 0.3f)
                : new Color(0.5f, 0.8f, 0.5f);

            EditorGUILayout.LabelField(tagDisplay, EditorStyles.miniLabel);

            if (_previewPalette != null)
            {
                GUI.color = matchColor;
                EditorGUILayout.LabelField(matchLabel, EditorStyles.miniLabel, GUILayout.Width(70));
                GUI.color = Color.white;
            }

            // Tag picker button
            if (GUILayout.Button("Tags", GUILayout.Width(40)))
            {
                _showTagPicker = !_showTagPicker || _tagPickerForIndex != index;
                _tagPickerForIndex = index;
            }

            // Delete button
            GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                Undo.RecordObject(target, "Remove recipe entry");
                recipe.props.RemoveAt(index);
                _analysisDirty = true;
                EditorUtility.SetDirty(target);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // ── Tag picker dropdown ──
            if (_showTagPicker && _tagPickerForIndex == index)
                DrawTagPicker(recipe, index);

            // ── Detail row: count, chance, placement hints ──
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("minCount"),
                new GUIContent("Min"), GUILayout.Width(100));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("maxCount"),
                new GUIContent("Max"), GUILayout.Width(100));
            EditorGUILayout.PropertyField(prop.FindPropertyRelative("chance"),
                GUIContent.none, GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();

            // Placement hints as compact toggles
            EditorGUILayout.BeginHorizontal();
            var pCenter = prop.FindPropertyRelative("preferCenter");
            var pWalls = prop.FindPropertyRelative("preferWalls");
            var pCorners = prop.FindPropertyRelative("preferCorners");
            pCenter.boolValue = GUILayout.Toggle(pCenter.boolValue, "Center", "Button", GUILayout.Width(60));
            pWalls.boolValue = GUILayout.Toggle(pWalls.boolValue, "Walls", "Button", GUILayout.Width(55));
            pCorners.boolValue = GUILayout.Toggle(pCorners.boolValue, "Corners", "Button", GUILayout.Width(65));
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        void DrawTagPicker(RoomRecipe recipe, int entryIndex)
        {
            var entry = recipe.props[entryIndex];
            var currentTags = new HashSet<string>(entry.requiredTags ?? new string[0]);

            EditorGUILayout.BeginVertical(new GUIStyle("box") { padding = new RectOffset(8, 8, 4, 4) });
            EditorGUILayout.LabelField("Pick Tags (click to toggle):", EditorStyles.miniLabel);

            int columns = 5;
            int tagIndex = 0;
            while (tagIndex < PropTags.All.Length)
            {
                EditorGUILayout.BeginHorizontal();
                for (int col = 0; col < columns && tagIndex < PropTags.All.Length; col++, tagIndex++)
                {
                    string tag = PropTags.All[tagIndex];
                    bool isActive = currentTags.Contains(tag);

                    GUI.backgroundColor = isActive ? new Color(0.3f, 0.7f, 0.4f) : Color.white;
                    if (GUILayout.Button(tag, EditorStyles.miniButton, GUILayout.MinWidth(65)))
                    {
                        Undo.RecordObject(target, "Toggle recipe tag");
                        if (isActive) currentTags.Remove(tag);
                        else currentTags.Add(tag);

                        recipe.props[entryIndex].requiredTags = currentTags.ToArray();
                        _analysisDirty = true;
                        EditorUtility.SetDirty(target);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Close", EditorStyles.miniButton, GUILayout.Width(50)))
                _showTagPicker = false;

            EditorGUILayout.EndVertical();
        }

        void DrawPropActions(RoomRecipe recipe)
        {
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
            if (GUILayout.Button("+ Add Prop Entry", GUILayout.Height(24)))
            {
                Undo.RecordObject(target, "Add recipe prop entry");
                if (recipe.props == null) recipe.props = new List<RecipePropEntry>();
                recipe.props.Add(new RecipePropEntry
                {
                    requiredTags = new string[0],
                    importance = PropImportance.Minor,
                    minCount = 0,
                    maxCount = 3,
                    chance = 0.5f
                });
                _analysisDirty = true;
                EditorUtility.SetDirty(target);
            }
            GUI.backgroundColor = Color.white;

            // Quick-add templates
            _showQuickAdd = GUILayout.Toggle(_showQuickAdd, "Quick Add ▾",
                "Button", GUILayout.Width(90), GUILayout.Height(24));

            EditorGUILayout.EndHorizontal();

            if (_showQuickAdd)
                DrawQuickAddMenu(recipe);
        }

        void DrawQuickAddMenu(RoomRecipe recipe)
        {
            EditorGUILayout.BeginVertical(new GUIStyle("box") { padding = new RectOffset(4, 4, 4, 4) });
            EditorGUILayout.LabelField("Quick Add Preset:", EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (QuickAddBtn("Lighting (wall)"))
                QuickAdd(recipe, new[] { PropTags.Lighting }, PropImportance.Minor, 2, 4, 0.8f, preferWalls: true);
            if (QuickAddBtn("Storage (wall)"))
                QuickAdd(recipe, new[] { PropTags.Storage }, PropImportance.Minor, 1, 3, 0.5f, preferWalls: true);
            if (QuickAddBtn("Pillars (corner)"))
                QuickAdd(recipe, new[] { PropTags.Pillar }, PropImportance.Major, 2, 4, 0.8f, preferCorners: true);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (QuickAddBtn("Seating"))
                QuickAdd(recipe, new[] { PropTags.Seating }, PropImportance.Minor, 1, 3, 0.5f);
            if (QuickAddBtn("Campfire (center)"))
                QuickAdd(recipe, new[] { PropTags.Campfire }, PropImportance.Major, 1, 1, 1f, preferCenter: true);
            if (QuickAddBtn("Clutter"))
                QuickAdd(recipe, new[] { PropTags.Book, PropTags.Dish, PropTags.Potion },
                    PropImportance.Clutter, 0, 4, 0.4f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        bool QuickAddBtn(string label)
        {
            GUI.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
            bool clicked = GUILayout.Button(label, EditorStyles.miniButton);
            GUI.backgroundColor = Color.white;
            return clicked;
        }

        void QuickAdd(RoomRecipe recipe, string[] tags, PropImportance imp,
            int min, int max, float chance,
            bool preferCenter = false, bool preferWalls = false, bool preferCorners = false)
        {
            Undo.RecordObject(target, "Quick add prop entry");
            if (recipe.props == null) recipe.props = new List<RecipePropEntry>();
            recipe.props.Add(new RecipePropEntry
            {
                requiredTags = tags,
                importance = imp,
                minCount = min,
                maxCount = max,
                chance = chance,
                preferCenter = preferCenter,
                preferWalls = preferWalls,
                preferCorners = preferCorners
            });
            _analysisDirty = true;
            EditorUtility.SetDirty(target);
        }

        // ════════════════════════════════════════════════════════════════
        //  SPAWN POINTS SECTION
        // ════════════════════════════════════════════════════════════════

        void DrawSpawnPointsSection(RoomRecipe recipe)
        {
            _showSpawnSection = EditorGUILayout.Foldout(_showSpawnSection,
                $"Spawn Points [{recipe.spawnPoints?.Count ?? 0}]", true, EditorStyles.foldoutHeader);
            if (!_showSpawnSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (recipe.spawnPoints != null)
            {
                for (int i = 0; i < recipe.spawnPoints.Count; i++)
                {
                    var sp = _spawnPoints.GetArrayElementAtIndex(i);
                    EditorGUILayout.BeginHorizontal();

                    Color spColor = GetSpawnColor(recipe.spawnPoints[i].pointType);
                    GUI.backgroundColor = spColor;
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("pointType"),
                        GUIContent.none, GUILayout.Width(100));
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("count"),
                        new GUIContent("×"), GUILayout.Width(60));
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("placement"),
                        GUIContent.none, GUILayout.Width(110));
                    EditorGUILayout.PropertyField(sp.FindPropertyRelative("priority"),
                        GUIContent.none, GUILayout.Width(40));

                    GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                    if (GUILayout.Button("✕", GUILayout.Width(22)))
                    {
                        Undo.RecordObject(target, "Remove spawn entry");
                        recipe.spawnPoints.RemoveAt(i);
                        EditorUtility.SetDirty(target);
                        GUI.backgroundColor = Color.white;
                        break;
                    }
                    GUI.backgroundColor = Color.white;

                    EditorGUILayout.EndHorizontal();
                }
            }

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.8f);
            if (GUILayout.Button("+ Add Spawn Point", GUILayout.Height(22)))
            {
                Undo.RecordObject(target, "Add spawn entry");
                if (recipe.spawnPoints == null) recipe.spawnPoints = new List<RecipeSpawnEntry>();
                recipe.spawnPoints.Add(new RecipeSpawnEntry
                {
                    pointType = SpawnPointType.Enemy,
                    count = 1,
                    placement = SpawnPlacement.Random,
                    priority = 5
                });
                EditorUtility.SetDirty(target);
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        // ════════════════════════════════════════════════════════════════
        //  FOOTER ACTIONS
        // ════════════════════════════════════════════════════════════════

        void DrawFooterActions(RoomRecipe recipe)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = new Color(0.8f, 0.5f, 0.2f);
            if (GUILayout.Button("Reset to Default", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Reset Recipe",
                    $"Reset this recipe to auto-generated defaults for {recipe.roomType}?",
                    "Reset", "Cancel"))
                {
                    Undo.RecordObject(target, "Reset recipe to default");
                    var temp = RecipeAutoGenerator.GenerateSingle(recipe.roomType,
                        System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(target)));
                    if (temp != null)
                    {
                        recipe.props = temp.props;
                        recipe.spawnPoints = temp.spawnPoints;
                        recipe.densityMultiplier = temp.densityMultiplier;
                        recipe.maxFillRatio = temp.maxFillRatio;
                        recipe.displayName = temp.displayName;
                        _analysisDirty = true;
                        EditorUtility.SetDirty(target);
                    }
                }
            }

            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.3f);
            if (GUILayout.Button("Duplicate Recipe", GUILayout.Height(26)))
            {
                string path = AssetDatabase.GetAssetPath(target);
                string dir = System.IO.Path.GetDirectoryName(path);
                string newPath = AssetDatabase.GenerateUniqueAssetPath(
                    $"{dir}/Recipe_{recipe.roomType}_copy.asset");
                AssetDatabase.CopyAsset(path, newPath);
                AssetDatabase.Refresh();
                var copy = AssetDatabase.LoadAssetAtPath<RoomRecipe>(newPath);
                if (copy != null) Selection.activeObject = copy;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // ════════════════════════════════════════════════════════════════
        //  UTILITY
        // ════════════════════════════════════════════════════════════════

        int GetMatchCount(RecipePropEntry entry)
        {
            if (_previewPalette == null || entry.requiredTags == null) return 0;
            int total = 0;
            foreach (var tag in entry.requiredTags)
                if (_tagMatchCounts != null && _tagMatchCounts.TryGetValue(tag, out int c))
                    total += c;
            return total;
        }

        void DrawThinSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
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
            SpawnPointType.Trap => new Color(0.9f, 0.5f, 0.2f),
            SpawnPointType.NPC => new Color(0.4f, 0.8f, 0.4f),
            SpawnPointType.PlayerSpawn => new Color(0.3f, 0.6f, 1f),
            SpawnPointType.Light => new Color(1f, 1f, 0.6f),
            SpawnPointType.PuzzleObject => new Color(0.5f, 0.7f, 1f),
            _ => Color.white
        };
    }
}
#endif
