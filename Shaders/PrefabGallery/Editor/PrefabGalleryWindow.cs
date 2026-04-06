#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace PrefabGallery.Editor
{
    public class PrefabGalleryWindow : EditorWindow
    {
        // ── Data ──────────────────────────────────────────
        private List<GalleryTheme> allThemes = new List<GalleryTheme>();
        private int selectedThemeIdx = -1;
        private int selectedCatIdx = -1;

        // ── UI State ──────────────────────────────────────
        private Vector2 sideScroll, gridScroll;
        private float thumbSize = 88f;
        private float placeScale = 1f;
        private string search = "";
        private bool showScanPanel;
        private bool scanRecursive = true;
        private string manualScanPath = "Assets/";
        private string previewRoot = "Assets/PrefabGallery/_Previews";
        private float sidebarWidth = 210f;
        private bool isResizingSidebar;

        // ── Drag ──────────────────────────────────────────
        private PrefabEntry dragEntry;

        // ── Rename ────────────────────────────────────────
        private int renamingCatIdx = -1;
        private string renameBuffer = "";

        // ── Add Category ──────────────────────────────────
        private bool addingCategory;
        private string newCatName = "";

        // ── Repaint Timer ─────────────────────────────────
        private double lastRepaint;

        // ══════════════════════════════════════════════════
        //  COLORS
        // ══════════════════════════════════════════════════

        private static readonly Color COL_BG          = new Color(0.118f, 0.118f, 0.141f);
        private static readonly Color COL_SIDEBAR     = new Color(0.137f, 0.141f, 0.169f);
        private static readonly Color COL_CARD        = new Color(0.200f, 0.204f, 0.243f);
        private static readonly Color COL_CARD_HOVER  = new Color(0.243f, 0.247f, 0.294f);
        private static readonly Color COL_ACCENT      = new Color(0.380f, 0.710f, 1.000f);
        private static readonly Color COL_GREEN       = new Color(0.400f, 0.850f, 0.560f);
        private static readonly Color COL_ORANGE      = new Color(1.000f, 0.680f, 0.320f);
        private static readonly Color COL_TEXT        = new Color(0.880f, 0.890f, 0.920f);
        private static readonly Color COL_TEXT_DIM    = new Color(0.520f, 0.540f, 0.600f);
        private static readonly Color COL_SEPARATOR   = new Color(0.220f, 0.228f, 0.271f);
        private static readonly Color COL_BTN_HOVER   = new Color(0.300f, 0.310f, 0.370f);

        // ══════════════════════════════════════════════════
        //  MENU
        // ══════════════════════════════════════════════════

        [MenuItem("Tools/Prefab Gallery %#g")]
        public static void Open()
        {
            var w = GetWindow<PrefabGalleryWindow>();
            w.titleContent = new GUIContent(" Prefab Gallery", EditorGUIUtility.IconContent("d_Prefab Icon").image);
            w.minSize = new Vector2(520, 320);
        }

        private void OnEnable()  => Refresh();
        private void OnFocus()   => Refresh();

        private void Refresh()
        {
            allThemes.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:GalleryTheme"))
            {
                var t = AssetDatabase.LoadAssetAtPath<GalleryTheme>(AssetDatabase.GUIDToAssetPath(guid));
                if (t) allThemes.Add(t);
            }
            allThemes.Sort((a, b) => string.Compare(a.themeName, b.themeName));
            if (selectedThemeIdx >= allThemes.Count) selectedThemeIdx = allThemes.Count - 1;
        }

        // ══════════════════════════════════════════════════
        //  MAIN GUI
        // ══════════════════════════════════════════════════

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), COL_BG);

            EditorGUILayout.BeginVertical();
            DrawToolbar();
            EditorGUILayout.BeginHorizontal();
            DrawSidebar();
            DrawResizeHandle();
            DrawMainContent();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            HandleDragToScene();

            if (EditorApplication.timeSinceStartup - lastRepaint > 0.08)
            {
                lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        // ══════════════════════════════════════════════════
        //  TOOLBAR
        // ══════════════════════════════════════════════════

        private void DrawToolbar()
        {
            Rect bar = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(bar, COL_SIDEBAR);

            // Title
            GUI.Label(R(bar, 12, 0, 160, 40), "Prefab Gallery",
                MakeStyle(15, COL_ACCENT, FontStyle.Bold, TextAnchor.MiddleLeft));

            // Right-aligned controls - calculate from right edge
            float rx = bar.width;

            // Search field
            rx -= 162;
            search = EditorGUI.TextField(R(bar, rx, 10, 150, 20), search, EditorStyles.toolbarSearchField);

            // Thumbnail size slider
            rx -= 112;
            GUI.Label(R(bar, rx, 12, 28, 16), "Grid", MakeStyle(9, COL_TEXT_DIM));
            thumbSize = GUI.HorizontalSlider(R(bar, rx + 30, 16, 68, 12), thumbSize, 52f, 152f);

            // Scale controls
            rx -= 148;
            GUI.Label(R(bar, rx, 12, 34, 16), "Scale", MakeStyle(9, COL_TEXT_DIM));
            placeScale = EditorGUI.FloatField(R(bar, rx + 36, 10, 34, 20), placeScale);
            placeScale = GUI.HorizontalSlider(R(bar, rx + 74, 16, 60, 12), placeScale, 0.1f, 5f);
            placeScale = Mathf.Clamp(placeScale, 0.01f, 100f);

            // Bottom separator
            EditorGUI.DrawRect(new Rect(bar.x, bar.yMax - 1, bar.width, 1), COL_SEPARATOR);
        }

        // ══════════════════════════════════════════════════
        //  SIDEBAR
        // ══════════════════════════════════════════════════

        private void DrawSidebar()
        {
            Rect sideArea = EditorGUILayout.BeginVertical(GUILayout.Width(sidebarWidth));
            EditorGUI.DrawRect(new Rect(sideArea.x, sideArea.y, sidebarWidth, position.height), COL_SIDEBAR);

            // Header
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            GUILayout.Label("THEMES", MakeStyle(10, COL_TEXT_DIM, FontStyle.Bold));
            GUILayout.FlexibleSpace();
            if (MiniBtn("+", 20, 16, COL_ACCENT))
                CreateTheme();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            // Theme list
            sideScroll = EditorGUILayout.BeginScrollView(sideScroll);

            for (int t = 0; t < allThemes.Count; t++)
            {
                if (allThemes[t] == null) continue;
                DrawThemeEntry(t);
            }

            if (allThemes.Count == 0)
            {
                GUILayout.Space(20);
                GUILayout.Label("No themes yet.\nClick + to create one.",
                    MakeStyle(11, COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleCenter));
            }

            EditorGUILayout.EndScrollView();

            DrawScanSection();
            EditorGUILayout.EndVertical();
        }

        private void DrawThemeEntry(int idx)
        {
            var theme = allThemes[idx];
            bool sel = idx == selectedThemeIdx;

            Rect row = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            bool hover = row.Contains(Event.current.mousePosition);

            if (sel)
                EditorGUI.DrawRect(row, Tint(theme.themeColor, 0.18f));
            else if (hover)
                EditorGUI.DrawRect(row, COL_CARD);

            // Color bar
            EditorGUI.DrawRect(new Rect(row.x, row.y + 5, 3, row.height - 10),
                sel ? theme.themeColor : COL_TEXT_DIM);

            // Name
            GUI.Label(new Rect(row.x + 14, row.y, row.width - 50, row.height),
                theme.themeName,
                MakeStyle(12, sel ? theme.themeColor : COL_TEXT,
                    sel ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft));

            // Badge
            int total = theme.TotalPrefabCount();
            if (total > 0)
            {
                string badge = total.ToString();
                float bw = Mathf.Max(badge.Length * 7 + 10, 20);
                Rect br = new Rect(row.xMax - bw - 8, row.y + 8, bw, 14);
                EditorGUI.DrawRect(br, Tint(theme.themeColor, 0.25f));
                GUI.Label(br, badge, MakeStyle(9, theme.themeColor, FontStyle.Normal, TextAnchor.MiddleCenter));
            }

            // Click
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                if (selectedThemeIdx == idx)
                    selectedThemeIdx = -1;
                else
                {
                    selectedThemeIdx = idx;
                    selectedCatIdx = theme.categories.Count > 0 ? 0 : -1;
                }
                renamingCatIdx = -1;
                addingCategory = false;
            }

            // Context menu
            if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                int ci = idx;
                menu.AddItem(new GUIContent("Rename Theme"), false, () => RenameTheme(ci));
                menu.AddItem(new GUIContent("Delete Theme"), false, () => DeleteTheme(ci));
                menu.ShowAsContext();
                Event.current.Use();
            }

            if (!sel) return;

            // Categories
            for (int c = 0; c < theme.categories.Count; c++)
                DrawCategoryEntry(theme, c);

            // Add category inline
            if (addingCategory)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(26);
                GUI.SetNextControlName("NewCatField");
                newCatName = EditorGUILayout.TextField(newCatName, GUILayout.Height(18));
                EditorGUI.FocusTextInControl("NewCatField");

                if (GUILayout.Button("✓", GUILayout.Width(22), GUILayout.Height(18)))
                    CommitNewCategory(theme);
                if (GUILayout.Button("✕", GUILayout.Width(22), GUILayout.Height(18)))
                    addingCategory = false;

                GUILayout.Space(8);
                EditorGUILayout.EndHorizontal();

                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Return) { CommitNewCategory(theme); Event.current.Use(); }
                    if (Event.current.keyCode == KeyCode.Escape) { addingCategory = false; Event.current.Use(); }
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(22);
                if (MiniBtn("+ Category", 80, 16, COL_TEXT_DIM))
                {
                    addingCategory = true;
                    newCatName = "";
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(4);
        }

        private void DrawCategoryEntry(GalleryTheme theme, int catIdx)
        {
            var cat = theme.categories[catIdx];
            bool sel = catIdx == selectedCatIdx;

            Rect row = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            bool hover = row.Contains(Event.current.mousePosition);

            if (sel)
                EditorGUI.DrawRect(row, Tint(COL_ACCENT, 0.12f));
            else if (hover)
                EditorGUI.DrawRect(row, Tint(COL_CARD, 0.7f));

            // Rename mode
            if (renamingCatIdx == catIdx)
            {
                GUI.SetNextControlName("RenameCatField");
                renameBuffer = EditorGUI.TextField(
                    new Rect(row.x + 30, row.y + 3, row.width - 70, 18), renameBuffer);
                EditorGUI.FocusTextInControl("RenameCatField");

                if (Event.current.type == EventType.KeyDown)
                {
                    if (Event.current.keyCode == KeyCode.Return)
                    {
                        if (!string.IsNullOrWhiteSpace(renameBuffer))
                        {
                            cat.categoryName = renameBuffer.Trim();
                            EditorUtility.SetDirty(theme);
                        }
                        renamingCatIdx = -1;
                        Event.current.Use();
                    }
                    if (Event.current.keyCode == KeyCode.Escape)
                    {
                        renamingCatIdx = -1;
                        Event.current.Use();
                    }
                }
                return;
            }

            // Dot
            EditorGUI.DrawRect(new Rect(row.x + 22, row.y + 9, 6, 6), sel ? cat.labelColor : COL_TEXT_DIM);

            // Name
            GUI.Label(new Rect(row.x + 34, row.y, row.width - 72, row.height),
                cat.categoryName,
                MakeStyle(11, sel ? COL_TEXT : COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleLeft));

            // Count
            if (cat.entries.Count > 0)
            {
                string cnt = cat.entries.Count.ToString();
                float cw = Mathf.Max(cnt.Length * 6 + 8, 16);
                Rect cr = new Rect(row.xMax - cw - 10, row.y + 6, cw, 12);
                EditorGUI.DrawRect(cr, Tint(cat.labelColor, 0.15f));
                GUI.Label(cr, cnt, MakeStyle(8, cat.labelColor, FontStyle.Normal, TextAnchor.MiddleCenter));
            }

            // Click
            if (GUI.Button(row, GUIContent.none, GUIStyle.none))
            {
                selectedCatIdx = catIdx;
                renamingCatIdx = -1;
            }

            // Context
            if (Event.current.type == EventType.ContextClick && row.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                int ci = catIdx;
                menu.AddItem(new GUIContent("Rename"), false, () =>
                {
                    renamingCatIdx = ci;
                    renameBuffer = cat.categoryName;
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Clear All Prefabs"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Clear",
                        $"Remove all {cat.entries.Count} prefabs from '{cat.categoryName}'?", "Clear", "Cancel"))
                    {
                        cat.entries.Clear();
                        EditorUtility.SetDirty(theme);
                    }
                });
                menu.AddItem(new GUIContent("Delete Category"), false, () =>
                {
                    if (EditorUtility.DisplayDialog("Delete",
                        $"Delete '{cat.categoryName}'?", "Delete", "Cancel"))
                    {
                        theme.categories.RemoveAt(ci);
                        if (selectedCatIdx >= theme.categories.Count)
                            selectedCatIdx = theme.categories.Count - 1;
                        EditorUtility.SetDirty(theme);
                    }
                });
                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        // ══════════════════════════════════════════════════
        //  SCAN SECTION
        // ══════════════════════════════════════════════════

        private void DrawScanSection()
        {
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), COL_SEPARATOR);

            GUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            showScanPanel = EditorGUILayout.Foldout(showScanPanel, "", true);
            GUILayout.Space(-14);
            GUILayout.Label("SCAN", MakeStyle(10, COL_TEXT_DIM, FontStyle.Bold));
            GUILayout.FlexibleSpace();
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            if (!showScanPanel) { GUILayout.Space(6); return; }

            GUILayout.Space(2);

            // Folder path
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            manualScanPath = EditorGUILayout.TextField(manualScanPath, GUILayout.Height(18));
            if (GUILayout.Button("…", GUILayout.Width(24), GUILayout.Height(18)))
            {
                string s = EditorUtility.OpenFolderPanel("Select Folder", "Assets", "");
                if (!string.IsNullOrEmpty(s) && s.StartsWith(Application.dataPath))
                    manualScanPath = "Assets" + s.Substring(Application.dataPath.Length);
            }
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);
            scanRecursive = EditorGUILayout.ToggleLeft("Recursive", scanRecursive, GUILayout.Width(80));
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            bool hasTheme = selectedThemeIdx >= 0 && selectedThemeIdx < allThemes.Count;
            bool hasCat = hasTheme && selectedCatIdx >= 0 &&
                          selectedCatIdx < allThemes[selectedThemeIdx].categories.Count;

            // Row 1: Scan to Cat + Smart Scan
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            GUI.enabled = hasCat;
            if (ColorButton("⟳ Scan → Category", COL_ACCENT))
                DoScanToCategory();
            GUI.enabled = true;

            GUILayout.Space(4);

            GUI.enabled = hasTheme;
            if (ColorButton("✦ Smart Scan", COL_GREEN))
                DoSmartScan();
            GUI.enabled = true;

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3);

            // Row 2: Scan Selected + Regen
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            GUI.enabled = hasCat;
            if (ColorButton("📂 Scan Selected", COL_ORANGE))
                DoScanSelectedFolder();
            GUI.enabled = true;

            GUILayout.Space(4);

            if (ColorButton("↻ Regen Previews", COL_BTN_HOVER))
                DoRegenPreviews();

            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
        }

        // ══════════════════════════════════════════════════
        //  RESIZE HANDLE
        // ══════════════════════════════════════════════════

        private void DrawResizeHandle()
        {
            Rect handle = GUILayoutUtility.GetRect(4, 4, GUILayout.ExpandHeight(true));
            handle.width = 4;
            EditorGUI.DrawRect(handle, COL_SEPARATOR);
            EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && handle.Contains(Event.current.mousePosition))
            {
                isResizingSidebar = true;
                Event.current.Use();
            }

            if (isResizingSidebar)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    sidebarWidth = Mathf.Clamp(Event.current.mousePosition.x, 160f, position.width * 0.45f);
                    Event.current.Use();
                    Repaint();
                }
                if (Event.current.type == EventType.MouseUp)
                {
                    isResizingSidebar = false;
                    Event.current.Use();
                }
            }
        }

        // ══════════════════════════════════════════════════
        //  MAIN CONTENT (GRID)
        // ══════════════════════════════════════════════════

        private void DrawMainContent()
        {
            Rect mainArea = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(new Rect(mainArea.x, mainArea.y, position.width - sidebarWidth, position.height), COL_BG);

            var cat = GetCurrentCategory();
            var theme = GetCurrentTheme();

            if (cat == null)
            {
                DrawEmptyState();
                EditorGUILayout.EndVertical();
                return;
            }

            // Header
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(12);

            Rect dotR = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
            dotR.y += 6;
            EditorGUI.DrawRect(dotR, cat.labelColor);

            GUILayout.Space(4);
            GUILayout.Label(cat.categoryName,
                MakeStyle(14, COL_TEXT, FontStyle.Bold, TextAnchor.MiddleLeft));
            GUILayout.FlexibleSpace();

            string info = $"{cat.entries.Count} prefab{(cat.entries.Count != 1 ? "s" : "")}";
            if (theme != null) info = $"{theme.themeName}  ›  {info}";
            GUILayout.Label(info, MakeStyle(10, COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleRight));
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            EditorGUI.DrawRect(GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true)), COL_SEPARATOR);
            GUILayout.Space(4);

            // Grid
            gridScroll = EditorGUILayout.BeginScrollView(gridScroll);

            float availW = position.width - sidebarWidth - 32;
            float cellW = thumbSize + 12;
            int cols = Mathf.Max(1, Mathf.FloorToInt(availW / cellW));

            var filtered = cat.entries
                .Where(e => e != null && e.prefab != null)
                .Where(e => string.IsNullOrEmpty(search) ||
                            e.name.ToLower().Contains(search.ToLower()))
                .ToList();

            if (filtered.Count == 0)
            {
                GUILayout.Space(40);
                GUILayout.Label(
                    string.IsNullOrEmpty(search)
                        ? "No prefabs in this category.\nUse Scan to add prefabs."
                        : "No matches found.",
                    MakeStyle(12, COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleCenter));
            }
            else
            {
                int col = 0;
                float totalGridW = cols * cellW;
                float offset = Mathf.Max(0, (availW - totalGridW) / 2f);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12 + offset);

                for (int i = 0; i < filtered.Count; i++)
                {
                    if (col >= cols)
                    {
                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(12 + offset);
                        col = 0;
                    }

                    DrawPrefabCard(filtered[i], cat);
                    col++;
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginVertical();

            GUILayout.Label("◇", MakeStyle(32, COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleCenter));
            GUILayout.Space(4);

            string msg;
            if (allThemes.Count == 0)
                msg = "Create a theme to get started";
            else if (selectedThemeIdx < 0)
                msg = "Select a theme from the sidebar";
            else
                msg = "Select or create a category";

            GUILayout.Label(msg, MakeStyle(13, COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleCenter));

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        // ══════════════════════════════════════════════════
        //  PREFAB CARD
        // ══════════════════════════════════════════════════

        private void DrawPrefabCard(PrefabEntry entry, GalleryCategory cat)
        {
            float cardW = thumbSize + 8;
            float cardH = thumbSize + 26;
            Rect total = GUILayoutUtility.GetRect(cardW, cardH);

            Rect card = new Rect(total.x + 2, total.y + 2, cardW - 4, cardH - 4);
            bool hover = card.Contains(Event.current.mousePosition);

            // Card background
            EditorGUI.DrawRect(card, hover ? COL_CARD_HOVER : COL_CARD);

            // Hover border
            if (hover)
            {
                float t = 1;
                EditorGUI.DrawRect(new Rect(card.x, card.y, card.width, t), COL_ACCENT);
                EditorGUI.DrawRect(new Rect(card.x, card.yMax - t, card.width, t), COL_ACCENT);
                EditorGUI.DrawRect(new Rect(card.x, card.y, t, card.height), COL_ACCENT);
                EditorGUI.DrawRect(new Rect(card.xMax - t, card.y, t, card.height), COL_ACCENT);
            }

            // Preview
            float pad = 4;
            Rect imgR = new Rect(card.x + pad, card.y + pad, card.width - pad * 2, card.height - 24 - pad);

            if (entry.preview != null)
                GUI.DrawTexture(imgR, entry.preview, ScaleMode.ScaleToFit);
            else
            {
                EditorGUI.DrawRect(imgR, new Color(0.10f, 0.10f, 0.13f));
                GUI.Label(imgR, "?", MakeStyle(18, COL_TEXT_DIM, FontStyle.Normal, TextAnchor.MiddleCenter));
            }

            // Name
            Rect nameR = new Rect(card.x + 3, card.yMax - 20, card.width - 6, 16);
            var nameStyle = MakeStyle(9, COL_TEXT, FontStyle.Normal, TextAnchor.MiddleCenter);
            nameStyle.wordWrap = true;
            nameStyle.clipping = TextClipping.Clip;
            GUI.Label(nameR, entry.name, nameStyle);

            // Tooltip
            if (hover)
                GUI.Label(card, new GUIContent("", $"{entry.name}\nScale: {placeScale:F2}x\nDrag → Scene"), GUIStyle.none);

            // Drag
            if (Event.current.type == EventType.MouseDrag && card.Contains(Event.current.mousePosition))
            {
                dragEntry = entry;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { entry.prefab };
                DragAndDrop.StartDrag(entry.name);
                Event.current.Use();
            }

            // Right-click
            if (Event.current.type == EventType.ContextClick && card.Contains(Event.current.mousePosition))
            {
                var menu = new GenericMenu();
                var e = entry;
                var c = cat;

                menu.AddItem(new GUIContent("Select in Project"), false, () =>
                {
                    Selection.activeObject = e.prefab;
                    EditorGUIUtility.PingObject(e.prefab);
                });
                menu.AddItem(new GUIContent("Place at Origin"), false, () => PlacePrefab(e, Vector3.zero));
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Regenerate Preview"), false, () =>
                {
                    EnsurePreviewFolder();
                    e.preview = PreviewGenerator.GeneratePreview(e.prefab,
                        $"{previewRoot}/{PreviewGenerator.SanitizeName(e.name)}_preview.png");
                    MarkDirty();
                });
                menu.AddItem(new GUIContent("Set Default Scale → Current"), false, () =>
                {
                    e.defaultScale = Vector3.one * placeScale;
                    MarkDirty();
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Remove from Gallery"), false, () =>
                {
                    c.entries.Remove(e);
                    MarkDirty();
                });

                menu.ShowAsContext();
                Event.current.Use();
            }
        }

        // ══════════════════════════════════════════════════
        //  DRAG TO SCENE
        // ══════════════════════════════════════════════════

        private void HandleDragToScene()
        {
            if (dragEntry == null) return;
            var captured = dragEntry;

            EditorApplication.delayCall += () =>
            {
                if (Selection.activeGameObject != null && captured != null &&
                    PrefabUtility.GetCorrespondingObjectFromSource(Selection.activeGameObject) == captured.prefab)
                {
                    Undo.RecordObject(Selection.activeGameObject.transform, "Gallery Scale");
                    Selection.activeGameObject.transform.localScale = captured.defaultScale * placeScale;
                }
            };
            dragEntry = null;
        }

        private void PlacePrefab(PrefabEntry entry, Vector3 pos)
        {
            if (entry?.prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
            go.transform.position = pos;
            go.transform.localScale = entry.defaultScale * placeScale;
            Undo.RegisterCreatedObjectUndo(go, $"Place {entry.name}");
            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        // ══════════════════════════════════════════════════
        //  SCAN ACTIONS
        // ══════════════════════════════════════════════════

        private void DoScanToCategory()
        {
            var cat = GetCurrentCategory();
            if (cat == null) return;
            EnsurePreviewFolder();
            int n = FolderScanner.ScanFlat(cat, manualScanPath, previewRoot, scanRecursive);
            MarkDirty();
            EditorUtility.DisplayDialog("Scan Complete", $"Added {n} prefabs to '{cat.categoryName}'.", "OK");
        }

        private void DoSmartScan()
        {
            var theme = GetCurrentTheme();
            if (theme == null) return;
            EnsurePreviewFolder();
            int n = FolderScanner.ScanSmart(theme, manualScanPath, previewRoot);
            MarkDirty();
            if (selectedCatIdx < 0 && theme.categories.Count > 0) selectedCatIdx = 0;
            EditorUtility.DisplayDialog("Smart Scan",
                $"Added {n} prefabs across {theme.categories.Count} categories.", "OK");
        }

        private void DoScanSelectedFolder()
        {
            string folder = FolderScanner.GetSelectedFolderPath();
            if (string.IsNullOrEmpty(folder))
            {
                EditorUtility.DisplayDialog("No Folder", "Select a folder in Project window first.", "OK");
                return;
            }
            var cat = GetCurrentCategory();
            if (cat == null) return;
            EnsurePreviewFolder();
            int n = FolderScanner.ScanFlat(cat, folder, previewRoot, scanRecursive);
            MarkDirty();
            EditorUtility.DisplayDialog("Scan Complete",
                $"Scanned '{Path.GetFileName(folder)}'\nAdded {n} prefabs to '{cat.categoryName}'.", "OK");
        }

        private void DoRegenPreviews()
        {
            var cat = GetCurrentCategory();
            if (cat == null) return;
            EnsurePreviewFolder();
            for (int i = 0; i < cat.entries.Count; i++)
            {
                var e = cat.entries[i];
                if (e?.prefab == null) continue;
                EditorUtility.DisplayProgressBar("Regenerating",
                    $"{e.name} ({i + 1}/{cat.entries.Count})", (float)i / cat.entries.Count);
                e.preview = PreviewGenerator.GeneratePreview(e.prefab,
                    $"{previewRoot}/{PreviewGenerator.SanitizeName(e.name)}_preview.png");
            }
            EditorUtility.ClearProgressBar();
            MarkDirty();
        }

        // ══════════════════════════════════════════════════
        //  THEME / CATEGORY ACTIONS
        // ══════════════════════════════════════════════════

        private void CreateTheme()
        {
            string dir = "Assets/PrefabGallery/Themes";
            EnsureFolder(dir);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{dir}/NewTheme.asset");
            var theme = ScriptableObject.CreateInstance<GalleryTheme>();
            theme.themeName = "New Theme";
            AssetDatabase.CreateAsset(theme, path);
            AssetDatabase.SaveAssets();
            Refresh();
            selectedThemeIdx = allThemes.IndexOf(theme);
            selectedCatIdx = -1;
            RenameTheme(selectedThemeIdx);
        }

        private void RenameTheme(int idx)
        {
            if (idx < 0 || idx >= allThemes.Count) return;
            string n = EditorInputDialog.Show("Rename Theme", "Enter name:", allThemes[idx].themeName);
            if (!string.IsNullOrWhiteSpace(n))
            {
                allThemes[idx].themeName = n.Trim();
                EditorUtility.SetDirty(allThemes[idx]);
                AssetDatabase.SaveAssets();
            }
        }

        private void DeleteTheme(int idx)
        {
            if (idx < 0 || idx >= allThemes.Count) return;
            if (!EditorUtility.DisplayDialog("Delete Theme",
                $"Delete '{allThemes[idx].themeName}'?", "Delete", "Cancel")) return;
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(allThemes[idx]));
            if (selectedThemeIdx == idx) { selectedThemeIdx = -1; selectedCatIdx = -1; }
            Refresh();
        }

        private void CommitNewCategory(GalleryTheme theme)
        {
            if (!string.IsNullOrWhiteSpace(newCatName))
            {
                theme.GetOrCreateCategory(newCatName.Trim());
                selectedCatIdx = theme.categories.Count - 1;
                EditorUtility.SetDirty(theme);
            }
            addingCategory = false;
            newCatName = "";
        }

        // ══════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════

        private GalleryTheme GetCurrentTheme()
        {
            if (selectedThemeIdx < 0 || selectedThemeIdx >= allThemes.Count) return null;
            return allThemes[selectedThemeIdx];
        }

        private GalleryCategory GetCurrentCategory()
        {
            var theme = GetCurrentTheme();
            if (theme == null || selectedCatIdx < 0 || selectedCatIdx >= theme.categories.Count) return null;
            return theme.categories[selectedCatIdx];
        }

        private void MarkDirty()
        {
            var theme = GetCurrentTheme();
            if (theme != null) { EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); }
        }

        private void EnsurePreviewFolder() => EnsureFolder(previewRoot);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parts = path.Split('/');
            string cur = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }

        private static Rect R(Rect p, float x, float y, float w, float h)
            => new Rect(p.x + x, p.y + y, w, h);

        private static Color Tint(Color c, float f)
            => new Color(c.r * f, c.g * f, c.b * f, 1f);

        private static GUIStyle MakeStyle(int size, Color color,
            FontStyle font = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontSize = size,
                normal = { textColor = color },
                fontStyle = font,
                alignment = anchor,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
        }

        private bool MiniBtn(string label, float w, float h, Color col)
        {
            var s = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 10,
                normal = { textColor = col },
                fixedHeight = h,
                alignment = TextAnchor.MiddleCenter
            };
            return GUILayout.Button(label, s, GUILayout.Width(w), GUILayout.Height(h));
        }

        private bool ColorButton(string label, Color color)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = color;
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                fixedHeight = 22,
                normal = { textColor = COL_TEXT },
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 2, 2)
            };
            bool r = GUILayout.Button(label, s, GUILayout.ExpandWidth(true), GUILayout.Height(22));
            GUI.backgroundColor = prev;
            return r;
        }
    }

    // ══════════════════════════════════════════════════
    //  INPUT DIALOG
    // ══════════════════════════════════════════════════

    public class EditorInputDialog : EditorWindow
    {
        private string value = "";
        private string message = "";
        private bool firstFrame = true;
        private static string result;

        public static string Show(string title, string msg, string defaultVal)
        {
            result = defaultVal;
            var w = GetWindow<EditorInputDialog>(true, title, true);
            w.message = msg;
            w.value = defaultVal;
            w.minSize = w.maxSize = new Vector2(320, 100);
            w.ShowModal();
            return result;
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label(message);
            GUILayout.Space(5);
            GUI.SetNextControlName("InputField");
            value = EditorGUILayout.TextField(value);
            if (firstFrame) { EditorGUI.FocusTextInControl("InputField"); firstFrame = false; }
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(80))) Close();
            if (GUILayout.Button("OK", GUILayout.Width(80)) ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            { result = value; Close(); }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
