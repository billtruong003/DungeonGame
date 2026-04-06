using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

namespace ThemeLevelDesigner.Editor
{
    public class LevelDesignerWindow : EditorWindow
    {
        // Data
        ThemeSO _currentTheme;
        MapData _currentMap;
        string _currentTag = "All";
        string _searchText = "";

        // UI Refs
        DropdownField _themeDropdown;
        DropdownField _tagDropdown;
        ToolbarSearchField _searchField;
        VisualElement _paletteGrid;
        MapCanvasElement _canvas;
        VisualElement _inspectorPanel;
        Label _statusBar;

        // State
        PlacedSection _selectedSection;
        SectionEntry _draggedEntry;
        ToolbarToggle _autoSyncToggle;

        [MenuItem("Tools/Level Designer %#L")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<LevelDesignerWindow>();
            wnd.titleContent = new GUIContent("Level Designer", EditorGUIUtility.IconContent("d_Terrain Icon").image);
            wnd.minSize = new Vector2(900, 550);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // Load stylesheet
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                FindAssetPath<StyleSheet>("LevelDesignerStyles"));
            if (uss != null) root.styleSheets.Add(uss);

            // ===== TOOLBAR =====
            var toolbar = new Toolbar();
            toolbar.style.paddingLeft = 4;
            toolbar.style.paddingRight = 4;

            // Theme dropdown
            toolbar.Add(new Label("Theme:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = 4 } });
            _themeDropdown = new DropdownField { style = { width = 150 } };
            _themeDropdown.RegisterValueChangedCallback(OnThemeChanged);
            toolbar.Add(_themeDropdown);

            // Tag filter
            toolbar.Add(new Label("  Filter:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = 4 } });
            _tagDropdown = new DropdownField { style = { width = 100 } };
            _tagDropdown.RegisterValueChangedCallback(evt => { _currentTag = evt.newValue; RefreshPalette(); });
            toolbar.Add(_tagDropdown);

            // Search
            toolbar.Add(new ToolbarSpacer { style = { width = 8 } });
            _searchField = new ToolbarSearchField();
            _searchField.style.width = 180;
            _searchField.RegisterValueChangedCallback(evt => { _searchText = evt.newValue; RefreshPalette(); });
            toolbar.Add(_searchField);

            // Spacer + buttons
            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);
            var newMapBtn = new ToolbarButton(OnNewMap) { text = "New Map" };
            var loadMapBtn = new ToolbarButton(OnLoadMap) { text = "Load Map" };
            var saveMapBtn = new ToolbarButton(OnSaveMap) { text = "Save Map" };
            var genPreviewBtn = new ToolbarButton(OnGeneratePreviews) { text = "Gen Previews" };
            toolbar.Add(newMapBtn);
            toolbar.Add(loadMapBtn);
            toolbar.Add(saveMapBtn);
            toolbar.Add(genPreviewBtn);

            // Scene sync buttons
            toolbar.Add(new ToolbarSpacer { style = { width = 8 } });
            var syncBtn = new ToolbarButton(OnSyncToScene) { text = "▶ Sync to Scene" };
            syncBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
            syncBtn.style.color = Color.white;
            toolbar.Add(syncBtn);

            _autoSyncToggle = new ToolbarToggle { text = "Auto", value = false };
            _autoSyncToggle.RegisterValueChangedCallback(evt =>
            {
                LevelDesignerSceneSync.AutoSync = evt.newValue;
                if (evt.newValue && _currentMap != null)
                    LevelDesignerSceneSync.IncrementalSync(_currentMap);
            });
            toolbar.Add(_autoSyncToggle);

            var clearSceneBtn = new ToolbarButton(() => LevelDesignerSceneSync.ClearScene()) { text = "Clear Scene" };
            toolbar.Add(clearSceneBtn);

            root.Add(toolbar);

            // ===== MAIN AREA (3 columns) =====
            var mainArea = new VisualElement();
            mainArea.style.flexDirection = FlexDirection.Row;
            mainArea.style.flexGrow = 1;

            // -- LEFT: Palette --
            var palettePanel = new VisualElement();
            palettePanel.style.width = 220;
            palettePanel.style.minWidth = 180;
            palettePanel.style.borderRightWidth = 1;
            palettePanel.style.borderRightColor = new Color(0.2f, 0.2f, 0.2f);
            palettePanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);

            var palLabel = new Label("SECTIONS") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginLeft = 8, marginTop = 4 } };
            palettePanel.Add(palLabel);

            var paletteScroll = new ScrollView(ScrollViewMode.Vertical);
            paletteScroll.style.flexGrow = 1;
            _paletteGrid = new VisualElement();
            _paletteGrid.style.flexDirection = FlexDirection.Row;
            _paletteGrid.style.flexWrap = Wrap.Wrap;
            _paletteGrid.style.paddingLeft = 4;
            _paletteGrid.style.paddingRight = 4;
            _paletteGrid.style.paddingTop = 4;
            paletteScroll.Add(_paletteGrid);
            palettePanel.Add(paletteScroll);

            mainArea.Add(palettePanel);

            // -- CENTER: Canvas --
            _canvas = new MapCanvasElement(this);
            _canvas.style.flexGrow = 1;
            _canvas.style.backgroundColor = new Color(0.14f, 0.14f, 0.16f);
            mainArea.Add(_canvas);

            // -- RIGHT: Inspector --
            _inspectorPanel = new VisualElement();
            _inspectorPanel.style.width = 240;
            _inspectorPanel.style.minWidth = 200;
            _inspectorPanel.style.borderLeftWidth = 1;
            _inspectorPanel.style.borderLeftColor = new Color(0.2f, 0.2f, 0.2f);
            _inspectorPanel.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f);
            _inspectorPanel.style.paddingLeft = 8;
            _inspectorPanel.style.paddingRight = 8;
            _inspectorPanel.style.paddingTop = 8;

            var inspLabel = new Label("INSPECTOR") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } };
            _inspectorPanel.Add(inspLabel);
            _inspectorPanel.Add(new Label("Select a section on the map.") { name = "insp-placeholder" });

            mainArea.Add(_inspectorPanel);
            root.Add(mainArea);

            // ===== STATUS BAR =====
            _statusBar = new Label("Ready");
            _statusBar.style.height = 22;
            _statusBar.style.paddingLeft = 8;
            _statusBar.style.unityTextAlign = TextAnchor.MiddleLeft;
            _statusBar.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            _statusBar.style.fontSize = 11;
            root.Add(_statusBar);

            // Init
            RefreshThemeDropdown();
        }

        // ==================== THEME ====================

        void RefreshThemeDropdown()
        {
            var guids = AssetDatabase.FindAssets("t:ThemeSO");
            var themes = guids
                .Select(g => AssetDatabase.LoadAssetAtPath<ThemeSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null)
                .ToList();

            _themeDropdown.choices = themes.Select(t => t.themeName).ToList();
            if (themes.Count > 0)
            {
                _currentTheme = themes[0];
                _themeDropdown.SetValueWithoutNotify(_currentTheme.themeName);
                RefreshTagDropdown();
                RefreshPalette();
            }
        }

        void OnThemeChanged(ChangeEvent<string> evt)
        {
            var guids = AssetDatabase.FindAssets("t:ThemeSO");
            foreach (var g in guids)
            {
                var theme = AssetDatabase.LoadAssetAtPath<ThemeSO>(AssetDatabase.GUIDToAssetPath(g));
                if (theme != null && theme.themeName == evt.newValue)
                {
                    _currentTheme = theme;
                    break;
                }
            }
            RefreshTagDropdown();
            RefreshPalette();
        }

        void RefreshTagDropdown()
        {
            if (_currentTheme == null) return;
            var tags = _currentTheme.GetAllTags();
            _tagDropdown.choices = tags;
            _tagDropdown.SetValueWithoutNotify("All");
            _currentTag = "All";
        }

        // ==================== PALETTE ====================

        void RefreshPalette()
        {
            _paletteGrid.Clear();
            if (_currentTheme == null) return;

            var filtered = _currentTheme.GetByTag(_currentTag);
            if (!string.IsNullOrEmpty(_searchText))
            {
                filtered = filtered.Where(s =>
                    s.displayName != null &&
                    s.displayName.ToLower().Contains(_searchText.ToLower())
                ).ToList();
            }

            foreach (var section in filtered)
            {
                var item = CreatePaletteItem(section);
                _paletteGrid.Add(item);
            }

            UpdateStatus();
        }

        VisualElement CreatePaletteItem(SectionEntry section)
        {
            var container = new VisualElement();
            container.style.width = 96;
            container.style.height = 110;
            container.style.marginBottom = 4;
            container.style.marginRight = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
            container.style.overflow = Overflow.Hidden;

            // Border color from theme
            if (_currentTheme != null)
            {
                container.style.borderBottomWidth = 2;
                container.style.borderBottomColor = _currentTheme.themeColor;
            }

            // Preview image
            var img = new Image();
            img.style.width = 92;
            img.style.height = 80;
            img.style.marginLeft = 2;
            img.style.marginTop = 2;

            if (section.preview != null)
                img.image = section.preview;
            else if (section.prefab != null)
                img.image = AssetPreview.GetAssetPreview(section.prefab);

            container.Add(img);

            // Size badge
            var badge = new Label($"{section.gridSize.x}x{section.gridSize.y}");
            badge.style.position = Position.Absolute;
            badge.style.right = 4;
            badge.style.top = 2;
            badge.style.fontSize = 9;
            badge.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            badge.style.color = Color.white;
            badge.style.paddingLeft = 3;
            badge.style.paddingRight = 3;
            badge.style.borderBottomLeftRadius = 2;
            badge.style.borderBottomRightRadius = 2;
            badge.style.borderTopLeftRadius = 2;
            badge.style.borderTopRightRadius = 2;
            container.Add(badge);

            // Name
            var nameLabel = new Label(section.displayName ?? section.id ?? "???");
            nameLabel.style.fontSize = 10;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            nameLabel.style.height = 18;
            container.Add(nameLabel);

            // Tooltip
            container.tooltip = $"{section.displayName}\nSize: {section.gridSize.x}x{section.gridSize.y}\nTags: {string.Join(", ", section.tags ?? new string[0])}";

            // Hover effect
            container.RegisterCallback<MouseEnterEvent>(evt =>
                container.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f));
            container.RegisterCallback<MouseLeaveEvent>(evt =>
                container.style.backgroundColor = new Color(0.22f, 0.22f, 0.24f));

            // Drag start
            container.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    _draggedEntry = section;
                    DragAndDrop.PrepareStartDrag();
                    DragAndDrop.SetGenericData("SectionEntry", section);
                    DragAndDrop.objectReferences = new Object[0];
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    DragAndDrop.StartDrag(section.displayName ?? "Section");
                    evt.StopPropagation();
                }
            });

            return container;
        }

        // ==================== CANVAS CALLBACKS ====================

        internal MapData CurrentMap => _currentMap;
        internal ThemeSO CurrentTheme => _currentTheme;

        internal void OnSectionPlaced(Vector2Int gridPos, SectionEntry entry)
        {
            if (_currentMap == null) OnNewMap();

            var placed = new PlacedSection
            {
                entry = entry,
                sourceTheme = _currentTheme,
                gridPos = gridPos,
                rotationSteps = 0
            };

            if (_currentMap.CanPlace(gridPos, entry.gridSize))
            {
                Undo.RecordObject(_currentMap, "Place Section");
                _currentMap.Add(placed);
                EditorUtility.SetDirty(_currentMap);
                _canvas.MarkDirtyRepaint();
                UpdateStatus();
                TriggerAutoSync();
            }
        }

        internal void OnSectionSelected(PlacedSection section)
        {
            _selectedSection = section;
            RefreshInspector();
        }

        internal void OnSectionDeselected()
        {
            _selectedSection = null;
            RefreshInspector();
        }

        // ==================== INSPECTOR ====================

        void RefreshInspector()
        {
            _inspectorPanel.Clear();
            _inspectorPanel.Add(new Label("INSPECTOR") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } });

            if (_selectedSection == null)
            {
                _inspectorPanel.Add(new Label("Select a section on the map."));
                return;
            }

            var s = _selectedSection;

            _inspectorPanel.Add(new Label($"Name: {s.entry?.displayName ?? "?"}"));
            _inspectorPanel.Add(new Label($"ID: {s.instanceId}"));
            _inspectorPanel.Add(new Label($"Grid Pos: {s.gridPos}"));
            _inspectorPanel.Add(new Label($"Size: {s.RotatedSize}"));
            _inspectorPanel.Add(new Label($"Theme: {s.sourceTheme?.themeName ?? "?"}"));

            // Rotation
            var rotField = new SliderInt("Rotation", 0, 3) { value = s.rotationSteps };
            rotField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(_currentMap, "Rotate Section");
                s.rotationSteps = evt.newValue;
                EditorUtility.SetDirty(_currentMap);
                _canvas.MarkDirtyRepaint();
                SceneView.RepaintAll();
            });
            _inspectorPanel.Add(rotField);

            // Room group
            if (!string.IsNullOrEmpty(s.roomGroupId))
            {
                var group = _currentMap.roomGroups.Find(g => g.groupId == s.roomGroupId);
                if (group != null)
                    _inspectorPanel.Add(new Label($"Room: {group.roomName} ({group.roomType})"));
            }

            // Replace button
            _inspectorPanel.Add(new VisualElement { style = { height = 12 } });
            var replaceBtn = new Button(() => ShowReplacePopup(s)) { text = "Replace Section..." };
            _inspectorPanel.Add(replaceBtn);

            // Delete button
            var deleteBtn = new Button(() =>
            {
                Undo.RecordObject(_currentMap, "Delete Section");
                _currentMap.Remove(s);
                EditorUtility.SetDirty(_currentMap);
                _selectedSection = null;
                _canvas.MarkDirtyRepaint();
                RefreshInspector();
                UpdateStatus();
                TriggerAutoSync();
            })
            { text = "Delete" };
            deleteBtn.style.backgroundColor = new Color(0.5f, 0.15f, 0.15f);
            deleteBtn.style.marginTop = 4;
            _inspectorPanel.Add(deleteBtn);

            // 3D Preview
            _inspectorPanel.Add(new VisualElement { style = { height = 12 } });
            if (s.entry?.prefab != null)
            {
                var previewTex = AssetPreview.GetAssetPreview(s.entry.prefab);
                if (previewTex != null)
                {
                    var previewImg = new Image { image = previewTex };
                    previewImg.style.width = 200;
                    previewImg.style.height = 200;
                    _inspectorPanel.Add(previewImg);
                }
            }
        }

        void ShowReplacePopup(PlacedSection section)
        {
            if (_currentTheme == null) return;

            var compatible = _currentTheme.sections
                .Where(s => s.gridSize == section.entry.gridSize)
                .ToList();

            var menu = new GenericMenu();
            foreach (var candidate in compatible)
            {
                var c = candidate;
                menu.AddItem(new GUIContent(c.displayName ?? c.id), false, () =>
                {
                    Undo.RecordObject(_currentMap, "Replace Section");
                    section.entry = c;
                    section.sourceTheme = _currentTheme;
                    EditorUtility.SetDirty(_currentMap);
                    _canvas.MarkDirtyRepaint();
                    RefreshInspector();
                    TriggerAutoSync();
                    SceneView.RepaintAll();
                });
            }

            if (compatible.Count == 0)
                menu.AddDisabledItem(new GUIContent("No compatible sections found"));

            menu.ShowAsContext();
        }

        // ==================== MAP MANAGEMENT ====================

        void OnNewMap()
        {
            _currentMap = CreateInstance<MapData>();
            _currentMap.name = "UnsavedMap";
            _selectedSection = null;
            _canvas.MarkDirtyRepaint();
            RefreshInspector();
            UpdateStatus();
        }

        void OnLoadMap()
        {
            var path = EditorUtility.OpenFilePanel("Load Map", "Assets", "asset");
            if (string.IsNullOrEmpty(path)) return;
            path = FileUtil.GetProjectRelativePath(path);
            if (string.IsNullOrEmpty(path)) return;

            var loaded = AssetDatabase.LoadAssetAtPath<MapData>(path);
            if (loaded != null)
            {
                _currentMap = loaded;
                _selectedSection = null;
                _canvas.MarkDirtyRepaint();
                RefreshInspector();
                UpdateStatus();
            }
        }

        void OnSaveMap()
        {
            if (_currentMap == null) return;

            if (!AssetDatabase.Contains(_currentMap))
            {
                var path = EditorUtility.SaveFilePanelInProject("Save Map", "NewMap", "asset", "Save map data");
                if (string.IsNullOrEmpty(path)) return;
                AssetDatabase.CreateAsset(_currentMap, path);
            }
            EditorUtility.SetDirty(_currentMap);
            AssetDatabase.SaveAssets();
            UpdateStatus();
        }

        void OnGeneratePreviews()
        {
            if (_currentTheme == null) return;

            Undo.RecordObject(_currentTheme, "Generate Previews");
            int count = 0;
            foreach (var section in _currentTheme.sections)
            {
                if (section.prefab == null) continue;
                var tex = PreviewUtility.GeneratePreview(section.prefab);
                if (tex != null)
                {
                    section.preview = tex;
                    count++;
                }
            }
            EditorUtility.SetDirty(_currentTheme);
            RefreshPalette();
            Debug.Log($"[LevelDesigner] Generated {count} previews for theme '{_currentTheme.themeName}'.");
        }

        // ==================== ROOM GROUPING ====================

        internal void GroupAsRoom(List<PlacedSection> sections)
        {
            if (_currentMap == null || sections.Count == 0) return;

            Undo.RecordObject(_currentMap, "Group as Room");
            var group = new RoomGroup
            {
                groupId = System.Guid.NewGuid().ToString("N")[..8],
                roomName = $"Room_{_currentMap.roomGroups.Count + 1}",
                roomColor = Random.ColorHSV(0, 1, 0.4f, 0.7f, 0.8f, 1f)
            };
            _currentMap.roomGroups.Add(group);

            foreach (var s in sections)
                s.roomGroupId = group.groupId;

            EditorUtility.SetDirty(_currentMap);
            _canvas.MarkDirtyRepaint();
        }

        // ==================== UTILS ====================

        void OnSyncToScene()
        {
            if (_currentMap == null)
            {
                Debug.LogWarning("[LevelDesigner] No map to sync. Create or load a map first.");
                return;
            }
            LevelDesignerSceneSync.SyncToScene(_currentMap);
            UpdateStatus();
        }

        void TriggerAutoSync()
        {
            if (LevelDesignerSceneSync.AutoSync && _currentMap != null)
                LevelDesignerSceneSync.IncrementalSync(_currentMap);
        }

        void UpdateStatus()
        {
            int count = _currentMap != null ? _currentMap.placedSections.Count : 0;
            int rooms = _currentMap != null ? _currentMap.roomGroups.Count : 0;
            string themeName = _currentTheme != null ? _currentTheme.themeName : "None";
            string syncState = LevelDesignerSceneSync.HasPreview ? " | Scene: SYNCED" : "";
            _statusBar.text = $"  Sections: {count}  |  Rooms: {rooms}  |  Theme: {themeName}  |  Map: {(_currentMap != null ? _currentMap.name : "None")}{syncState}";
        }

        static string FindAssetPath<T>(string name) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
            return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
        }
    }
}
