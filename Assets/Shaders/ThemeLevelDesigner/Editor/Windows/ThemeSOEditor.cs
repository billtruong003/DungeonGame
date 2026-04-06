using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ThemeLevelDesigner.Editor
{
    [CustomEditor(typeof(ThemeSO))]
    public class ThemeSOEditor : UnityEditor.Editor
    {
        bool _showGallery = true;
        bool _showAdvanced;
        float _cellSizeOverride = 1f;
        Vector2 _galleryScroll;

        void OnEnable()
        {
            // Auto-load any saved preview textures from disk
            var theme = (ThemeSO)target;
            if (theme == null) return;

            foreach (var section in theme.sections)
            {
                if (section.preview == null && section.prefab != null)
                {
                    var loaded = PreviewUtility.LoadExistingPreview(section.prefab);
                    if (loaded != null) section.preview = loaded;
                }
            }
        }

        public override void OnInspectorGUI()
        {
            var theme = (ThemeSO)target;

            // ======= HEADER =======
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Theme: {theme.themeName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{theme.sections.Count} sections", EditorStyles.miniLabel, GUILayout.Width(80));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            DrawDefaultInspector();

            EditorGUILayout.Space(10);

            // ======= DROP ZONE: Drag prefabs here =======
            DrawDropZone(theme);

            EditorGUILayout.Space(6);

            // ======= QUICK ACTIONS =======
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Folder...", GUILayout.Height(28)))
                ScanFolder(theme);
            if (GUILayout.Button("Rescan All Sizes", GUILayout.Height(28)))
                RescanAllSizes(theme);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate Previews", GUILayout.Height(28)))
                GenerateAllPreviews(theme);
            if (GUILayout.Button("Re-detect Tags", GUILayout.Height(28)))
                RedetectAllTags(theme);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Remove Missing", GUILayout.Height(24)))
                RemoveMissing(theme);
            if (GUILayout.Button("Sort A-Z", GUILayout.Height(24)))
                SortSections(theme);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Open in Level Designer", GUILayout.Height(24)))
                LevelDesignerWindow.ShowWindow();

            // Advanced settings
            _showAdvanced = EditorGUILayout.Foldout(_showAdvanced, "Advanced Settings");
            if (_showAdvanced)
            {
                EditorGUI.indentLevel++;
                _cellSizeOverride = EditorGUILayout.FloatField("Cell Size for Detection", _cellSizeOverride);
                EditorGUILayout.HelpBox(
                    "Naming convention: include size in prefab name like 'Floor_Stone_4x4'.\n" +
                    "Auto-tag keywords: floor, wall, door, prop, pillar, stair, trap, corner, corridor, ceiling, light, chest, spawn.\n" +
                    "Prefixes auto-removed: SM_, Env_, P_, Prop_, Geo_, Mesh_, MDL_, PRF_, PF_, T_",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }

            // ======= GALLERY =======
            EditorGUILayout.Space(10);
            _showGallery = EditorGUILayout.Foldout(_showGallery, $"Section Gallery ({theme.sections.Count})");
            if (_showGallery)
                DrawGallery(theme);
        }

        // ==================== DROP ZONE ====================

        void DrawDropZone(ThemeSO theme)
        {
            var dropRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));

            var prevColor = GUI.backgroundColor;
            bool isDragging = Event.current.type == EventType.DragUpdated || Event.current.type == EventType.DragPerform;
            bool isHovering = isDragging && dropRect.Contains(Event.current.mousePosition);

            GUI.backgroundColor = isHovering ? new Color(0.3f, 0.8f, 0.4f) : new Color(0.25f, 0.25f, 0.3f);
            GUI.Box(dropRect, "", EditorStyles.helpBox);
            GUI.backgroundColor = prevColor;

            var labelStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = isHovering ? Color.white : new Color(0.6f, 0.6f, 0.65f) }
            };
            GUI.Label(dropRect, isHovering
                ? "DROP TO ADD"
                : "Drag Prefabs or Folders Here to Add Sections", labelStyle);

            if (!isDragging) return;

            if (Event.current.type == EventType.DragUpdated && dropRect.Contains(Event.current.mousePosition))
            {
                bool hasPrefabs = DragAndDrop.objectReferences.Any(o => o is GameObject && PrefabUtility.IsPartOfPrefabAsset(o));
                bool hasFolders = DragAndDrop.paths.Any(p => AssetDatabase.IsValidFolder(p));
                if (hasPrefabs || hasFolders)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    Event.current.Use();
                }
            }
            else if (Event.current.type == EventType.DragPerform && dropRect.Contains(Event.current.mousePosition))
            {
                DragAndDrop.AcceptDrag();
                Event.current.Use();

                var prefabs = DragAndDrop.objectReferences
                    .OfType<GameObject>()
                    .Where(go => PrefabUtility.IsPartOfPrefabAsset(go))
                    .ToList();

                // Also handle folder drops
                foreach (var path in DragAndDrop.paths)
                {
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        var folderPrefabs = FindPrefabsInFolder(path, true);
                        prefabs.AddRange(folderPrefabs);
                    }
                }

                prefabs = prefabs.Distinct().ToList();
                if (prefabs.Count > 0)
                    AddPrefabsToTheme(theme, prefabs);
            }
        }

        // ==================== FOLDER SCAN ====================

        void ScanFolder(ThemeSO theme)
        {
            var folder = EditorUtility.OpenFolderPanel("Select Prefab Folder", "Assets", "");
            if (string.IsNullOrEmpty(folder)) return;

            var projectPath = Application.dataPath;
            if (folder.StartsWith(projectPath))
                folder = "Assets" + folder[projectPath.Length..];
            else
            {
                EditorUtility.DisplayDialog("Error", "Please select a folder inside the Assets directory.", "OK");
                return;
            }

            bool recursive = EditorUtility.DisplayDialog("Scan Subfolders?",
                "Also scan subfolders for prefabs?", "Yes, scan all", "This folder only");

            var prefabs = FindPrefabsInFolder(folder, recursive);
            if (prefabs.Count == 0)
            {
                EditorUtility.DisplayDialog("No Prefabs", "No prefabs found in the selected folder.", "OK");
                return;
            }

            AddPrefabsToTheme(theme, prefabs);
        }

        static List<GameObject> FindPrefabsInFolder(string folder, bool recursive)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            var results = new List<GameObject>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!recursive)
                {
                    var dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
                    if (dir != folder) continue;
                }
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go != null) results.Add(go);
            }

            return results;
        }

        // ==================== CORE: Add Prefabs ====================

        void AddPrefabsToTheme(ThemeSO theme, List<GameObject> prefabs)
        {
            Undo.RecordObject(theme, "Add Sections from Prefabs");

            int added = 0, skipped = 0;

            EditorUtility.DisplayProgressBar("Adding Sections", "Processing...", 0);

            for (int i = 0; i < prefabs.Count; i++)
            {
                var prefab = prefabs[i];
                EditorUtility.DisplayProgressBar("Adding Sections",
                    $"{prefab.name} ({i + 1}/{prefabs.Count})",
                    (float)i / prefabs.Count);

                if (SectionAutoDetect.ExistsInTheme(theme, prefab))
                {
                    skipped++;
                    continue;
                }

                var entry = SectionAutoDetect.FromPrefab(prefab, _cellSizeOverride);
                if (entry == null) continue;

                var preview = PreviewUtility.GeneratePreview(prefab);
                if (preview != null) entry.preview = preview;

                theme.sections.Add(entry);
                added++;
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(theme);

            Debug.Log($"[ThemeSO] Added {added} sections, skipped {skipped} duplicates to '{theme.themeName}'.");

            if (added > 0 || skipped > 0)
                EditorUtility.DisplayDialog("Done",
                    $"Added {added} sections.\nSkipped {skipped} duplicates.", "OK");
        }

        // ==================== BULK ACTIONS ====================

        void GenerateAllPreviews(ThemeSO theme)
        {
            Undo.RecordObject(theme, "Generate Previews");
            int count = 0;

            EditorUtility.DisplayProgressBar("Generating Previews", "", 0);
            for (int i = 0; i < theme.sections.Count; i++)
            {
                var section = theme.sections[i];
                if (section.prefab == null) continue;

                EditorUtility.DisplayProgressBar("Generating Previews",
                    section.displayName ?? section.prefab.name,
                    (float)i / theme.sections.Count);

                var tex = PreviewUtility.GeneratePreview(section.prefab);
                if (tex != null)
                {
                    section.preview = tex;
                    count++;
                }
            }

            EditorUtility.ClearProgressBar();
            EditorUtility.SetDirty(theme);
            Debug.Log($"[ThemeSO] Generated {count} previews for '{theme.themeName}'.");
        }

        void RescanAllSizes(ThemeSO theme)
        {
            Undo.RecordObject(theme, "Rescan Sizes");
            int count = 0;

            foreach (var section in theme.sections)
            {
                if (section.prefab == null) continue;
                var parsedSize = SectionAutoDetect.ParseSizeFromName(section.prefab.name);
                section.gridSize = parsedSize ?? SectionAutoDetect.DetectGridSize(section.prefab, _cellSizeOverride);
                count++;
            }

            EditorUtility.SetDirty(theme);
            Debug.Log($"[ThemeSO] Rescanned sizes for {count} sections.");
        }

        void RedetectAllTags(ThemeSO theme)
        {
            Undo.RecordObject(theme, "Re-detect Tags");

            foreach (var section in theme.sections)
            {
                if (section.prefab == null) continue;
                section.tags = SectionAutoDetect.DetectTags(section.prefab.name);
                section.displayName = SectionAutoDetect.CleanDisplayName(section.prefab.name);
            }

            EditorUtility.SetDirty(theme);
            Debug.Log($"[ThemeSO] Re-detected tags for {theme.sections.Count} sections.");
        }

        void RemoveMissing(ThemeSO theme)
        {
            Undo.RecordObject(theme, "Remove Missing");
            int removed = theme.sections.RemoveAll(s => s.prefab == null);
            EditorUtility.SetDirty(theme);
            if (removed > 0) Debug.Log($"[ThemeSO] Removed {removed} entries with missing prefabs.");
        }

        void SortSections(ThemeSO theme)
        {
            Undo.RecordObject(theme, "Sort Sections");
            theme.sections.Sort((a, b) =>
                string.Compare(a.displayName ?? "", b.displayName ?? "", System.StringComparison.OrdinalIgnoreCase));
            EditorUtility.SetDirty(theme);
        }

        // ==================== GALLERY ====================

        void DrawGallery(ThemeSO theme)
        {
            if (theme.sections.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No sections yet. Drag prefabs into the drop zone above, or use 'Scan Folder'.",
                    MessageType.Info);
                return;
            }

            int columns = Mathf.Max(1, (int)(EditorGUIUtility.currentViewWidth / 105));
            int col = 0;
            int removeIndex = -1;

            _galleryScroll = EditorGUILayout.BeginScrollView(_galleryScroll, GUILayout.MaxHeight(400));

            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < theme.sections.Count; i++)
            {
                var section = theme.sections[i];
                if (col >= columns)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal();
                    col = 0;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(96));

                // Preview
                Texture2D tex = section.preview;
                if (tex == null && section.prefab != null)
                    tex = AssetPreview.GetAssetPreview(section.prefab);

                if (tex != null)
                    GUILayout.Label(tex, GUILayout.Width(88), GUILayout.Height(72));
                else
                    GUILayout.Box(section.prefab != null ? "..." : "X", GUILayout.Width(88), GUILayout.Height(72));

                GUILayout.Label(section.displayName ?? "?", EditorStyles.miniLabel, GUILayout.Width(88));

                string tags = section.tags != null ? string.Join(",", section.tags) : "";
                GUILayout.Label($"{section.gridSize.x}x{section.gridSize.y} [{tags}]",
                    EditorStyles.miniLabel, GUILayout.Width(88));

                if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(88), GUILayout.Height(16)))
                    removeIndex = i;

                EditorGUILayout.EndVertical();
                col++;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();

            if (removeIndex >= 0)
            {
                Undo.RecordObject(theme, "Remove Section");
                theme.sections.RemoveAt(removeIndex);
                EditorUtility.SetDirty(theme);
            }
        }
    }
}
