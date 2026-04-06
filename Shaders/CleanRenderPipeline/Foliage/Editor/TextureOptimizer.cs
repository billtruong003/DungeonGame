using UnityEngine;
using UnityEditor;
using UnityEditor.Presets;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EditorTools
{
    public class TextureOptimizer : EditorWindow
    {
        // ─── Data ──────────────────────────────────────────────────────
        private class TexInfo
        {
            public string Path;
            public TextureImporter Importer;
            public TextureImporterType TexType;
            public int OrigMaxSize, RecMaxSize;
            public bool OrigMip, RecMip;
            public bool OrigCrunch, RecCrunch;
            public int OrigCrunchQ, RecCrunchQ;
            public TextureImporterCompression OrigComp, RecComp;
            public int ActualW, ActualH;
            public bool NeedsResize, NeedsMip, NeedsComp, NeedsCrunch;
            public bool NeedsAndroidOverride, NeedsASTCChange;
            public TextureImporterFormat CurAndroidFmt, RecAndroidFmt;
            public bool HasAndroidOverride, HasHighRes;
            public bool IsSelected = true;
            public string RuleName;
            public bool HasIssue => NeedsResize || NeedsMip || NeedsComp || NeedsCrunch || NeedsAndroidOverride || NeedsASTCChange;
        }

        private class FolderNode
        {
            public string Name, FullPath;
            public bool Expanded, Selected = true;
            public List<FolderNode> Children = new List<FolderNode>();
            public List<string> Textures = new List<string>();
            public int Count { get { int c = Textures.Count; foreach (var ch in Children) c += ch.Count; return c; } }
            public long Size
            {
                get
                {
                    long s = 0;
                    foreach (var t in Textures) { var fi = new FileInfo(t); if (fi.Exists) s += fi.Length; }
                    foreach (var ch in Children) s += ch.Size; return s;
                }
            }
            public void SetSel(bool v) { Selected = v; foreach (var ch in Children) ch.SetSel(v); }
            public List<string> GetSel()
            {
                var r = new List<string>(); if (Selected) r.AddRange(Textures);
                foreach (var ch in Children) r.AddRange(ch.GetSel()); return r;
            }
        }

        private class AuditEntry
        {
            public string Path, TexType, Compression, AndroidFormat, Issues, AppliedRule;
            public int MaxSize, ActualW, ActualH;
            public bool HasAndroidOverride, IsCompliant;
        }

        // ─── State ─────────────────────────────────────────────────────
        private TextureImportRuleset _ruleset;
        private string _searchFolder = "Assets";
        private List<TexInfo> _textures = new List<TexInfo>();
        private List<string> _unusedTextures = new List<string>();
        private List<AuditEntry> _auditEntries = new List<AuditEntry>();
        private FolderNode _unusedTree;
        private Vector2 _scrCfg, _scrAudit, _scrOpt, _scrUnused;
        private bool _scanned, _audited, _selAll = true;
        private int _totalCount, _issueCount;
        private long _estSaved;
        private int _auditTotal, _auditOK, _auditFail, _auditNoOverride, _auditNoAstc, _auditOversize;
        private bool _auditOnlyIssues = true;

        // Styles
        private GUIStyle _sH, _sS, _sSt, _tBad, _tGood, _tInfo, _tWarn, _sFolder, _sFile;
        private bool _stylesOk;
        private int _tab;
        private readonly string[] _tabs = { "Ruleset & Config", "Audit Report", "Scan & Optimize", "Unused Textures" };

        private static readonly int[] POT = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };
        private static readonly string[] KNOWN_DIRS = {
            "Assets/Models/Cosmetics/", "Assets/Models/FruitCannon/Textures/",
            "Assets/Models/Halloween/Textures/", "Assets/Models/New/Buildings/Materials/",
            "Assets/0Grabbables/", "Assets/PolygonBattleRoyale/Textures/", "Assets/Models/CamoMonkey/",
        };
        private static System.Reflection.MethodInfo _getWH;
        private static bool _getWH_cached;

        [MenuItem("Tools/Texture Optimizer %#t")]
        public static void Open() { var w = GetWindow<TextureOptimizer>("Texture Optimizer"); w.minSize = new Vector2(740, 580); }
        private void OnEnable() { _ruleset = TextureImportRuleset.FindInProject(); }

        private void OnGUI()
        {
            InitStyles();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("TEXTURE OPTIMIZER — Quest / ASTC", _sH);
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("TEABAG-38 · Phase 3.3", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(4);
            _tab = GUILayout.Toolbar(_tab, _tabs, GUILayout.Height(28));
            EditorGUILayout.Space(4);
            switch (_tab) { case 0: DrawConfig(); break; case 1: DrawAudit(); break; case 2: DrawOptimize(); break; case 3: DrawUnused(); break; }
        }

        // ────────────────────────────────────────────────────────────────
        //  Tab 0 – Config
        // ────────────────────────────────────────────────────────────────
        private void DrawConfig()
        {
            _scrCfg = EditorGUILayout.BeginScrollView(_scrCfg);

            // ── Ruleset ──
            Hdr("Texture Import Ruleset");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "ScriptableObject chứa rules cho từng texture type.\n" +
                "Commit vào git, cả team dùng chung.\n" +
                "Tool chỉ apply khi bấm nút — không tự động sửa khi import.",
                MessageType.Info);

            _ruleset = (TextureImportRuleset)EditorGUILayout.ObjectField("Ruleset", _ruleset, typeof(TextureImportRuleset), false);

            if (_ruleset != null)
            {
                if (GUILayout.Button("Select in Inspector")) Selection.activeObject = _ruleset;
                EditorGUILayout.Space(4);
                EditorGUI.BeginDisabledGroup(true);
                foreach (var r in _ruleset.Rules)
                    EditorGUILayout.LabelField($"  {r.TextureType}: max={r.MaxSize}, mip={r.MipMaps}, crunch={r.Crunch}(Q{r.CrunchQuality}), ASTC={r.AndroidASTCFormat}", EditorStyles.miniLabel);
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                EditorGUILayout.HelpBox("Chưa có ruleset.", MessageType.Warning);
                BtnColor(new Color(0.3f, 1f, 0.5f));
                if (GUILayout.Button("Create Default Ruleset", GUILayout.Height(26))) CreateDefaultRuleset();
                BtnColor(Color.white);
            }
            EditorGUILayout.EndVertical();

            // ── Preset Manager ──
            EditorGUILayout.Space(6);
            Hdr("Preset Manager (Future Imports)");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.HelpBox(
                "Preset Manager apply settings cho texture MỚI import — không đụng existing files.\n" +
                "Tạo preset bên dưới, rồi đăng ký trong Project Settings → Preset Manager.\n\n" +
                "Mỗi texture type cần 1 preset riêng với filter phù hợp:\n" +
                "  • Default preset, filter: (trống) — catch-all\n" +
                "  • NormalMap preset, filter: _Normal hoặc _N\n" +
                "  • Sprite preset, filter: Assets/UI/",
                MessageType.Info);

            if (_ruleset != null)
            {
                BtnColor(new Color(0.3f, 0.8f, 1f));
                if (GUILayout.Button("Generate .preset Files From Ruleset", GUILayout.Height(28)))
                    GeneratePresetFiles();
                BtnColor(Color.white);
            }

            if (GUILayout.Button("Open Preset Manager"))
                SettingsService.OpenProjectSettings("Project/Preset Manager");

            // Show existing presets status
            bool hasDefaults = CheckPresetManagerHasTextureDefaults();
            EditorGUILayout.LabelField(
                hasDefaults
                    ? "✓ TextureImporter default preset(s) registered in Preset Manager"
                    : "⚠ No TextureImporter defaults in Preset Manager yet",
                _sSt);

            EditorGUILayout.EndVertical();

            // ── Tool Settings ──
            EditorGUILayout.Space(6);
            Hdr("Tool Settings");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _searchFolder = EditorGUILayout.TextField("Search Folder", _searchFolder);
            EditorGUILayout.EndVertical();

            // ── Known Dirs ──
            EditorGUILayout.Space(6);
            Hdr("Known Texture Directories");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var d in KNOWN_DIRS)
            {
                bool ex = AssetDatabase.IsValidFolder(d.TrimEnd('/'));
                EditorGUILayout.LabelField($"  {(ex ? "✓" : "✗")}  {d}", ex ? EditorStyles.label : EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────────
        //  Tab 1 – Audit
        // ────────────────────────────────────────────────────────────────
        private void DrawAudit()
        {
            if (!CheckRuleset()) return;

            EditorGUILayout.BeginHorizontal();
            BtnColor(new Color(0.3f, 0.8f, 1f));
            if (GUILayout.Button("Run Full Audit", GUILayout.Height(30))) RunAudit();
            BtnColor(Color.white);
            EditorGUI.BeginDisabledGroup(!_audited);
            if (GUILayout.Button("Export CSV", GUILayout.Height(30))) ExportAuditCSV();
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!_audited) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Total: {_auditTotal}  |  OK: {_auditOK}  |  Fail: {_auditFail}", _sSt);
            EditorGUILayout.LabelField($"  No Android override: {_auditNoOverride}  |  Non-ASTC: {_auditNoAstc}  |  Oversize: {_auditOversize}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            _auditOnlyIssues = EditorGUILayout.ToggleLeft("Show only non-compliant", _auditOnlyIssues, EditorStyles.boldLabel);

            _scrAudit = EditorGUILayout.BeginScrollView(_scrAudit);
            foreach (var e in _auditEntries)
            {
                if (_auditOnlyIssues && e.IsCompliant) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                if (GUILayout.Button(e.Path, EditorStyles.label))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(e.Path));
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(12);
                GUILayout.Label(e.TexType, _tInfo);
                GUILayout.Label($"{e.ActualW}×{e.ActualH}", EditorStyles.miniLabel);
                GUILayout.Label($"max={e.MaxSize}", EditorStyles.miniLabel);
                GUILayout.Label(e.HasAndroidOverride ? $"Android:{e.AndroidFormat}" : "Android:NONE", e.HasAndroidOverride ? _tInfo : _tWarn);
                GUILayout.Label(e.IsCompliant ? "✓" : e.Issues, e.IsCompliant ? _tGood : _tBad);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────────
        //  Tab 2 – Scan & Optimize
        // ────────────────────────────────────────────────────────────────
        private void DrawOptimize()
        {
            if (!CheckRuleset()) return;

            EditorGUILayout.BeginHorizontal();
            BtnColor(new Color(0.3f, 0.8f, 1f));
            if (GUILayout.Button("Scan", GUILayout.Height(30))) ScanTextures();
            BtnColor(Color.white);
            EditorGUI.BeginDisabledGroup(!_scanned || _issueCount == 0);
            BtnColor(new Color(0.3f, 1f, 0.5f));
            if (GUILayout.Button("Apply Selected", GUILayout.Height(30))) ApplyOptimizations();
            BtnColor(Color.white);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            if (!_scanned) return;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Total: {_totalCount}  |  Issues: {_issueCount}  |  Est. savings: ~{Fmt(_estSaved)}", _sSt);
            EditorGUILayout.EndVertical();

            bool a = EditorGUILayout.ToggleLeft("Select / Deselect All", _selAll, EditorStyles.boldLabel);
            if (a != _selAll) { _selAll = a; foreach (var t in _textures.Where(x => x.HasIssue)) t.IsSelected = _selAll; }

            _scrOpt = EditorGUILayout.BeginScrollView(_scrOpt);
            foreach (var t in _textures)
            {
                if (!t.HasIssue) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                t.IsSelected = EditorGUILayout.Toggle(t.IsSelected, GUILayout.Width(20));
                if (GUILayout.Button(t.Path, EditorStyles.label))
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(t.Path));
                GUILayout.Label(t.TexType.ToString(), _tInfo);
                if (t.HasHighRes) GUILayout.Label("HighRes", _tInfo);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(26);
                if (t.NeedsResize)
                {
                    string lbl = $"Size {t.OrigMaxSize}→{t.RecMaxSize}";
                    if (t.RecMaxSize < _ruleset.GetRuleForType(t.TexType).MaxSize)
                        lbl += $" (fit {t.ActualW}×{t.ActualH})";
                    GUILayout.Label(lbl, _tBad);
                }
                if (t.NeedsMip) GUILayout.Label($"Mip {t.OrigMip}→{t.RecMip}", _tBad);
                if (t.NeedsComp) GUILayout.Label($"{t.OrigComp}→{t.RecComp}", _tBad);
                if (t.NeedsCrunch)
                {
                    string lbl = !t.OrigCrunch ? $"Crunch OFF→ON(Q{t.RecCrunchQ})"
                        : t.RecCrunch ? $"CrunchQ {t.OrigCrunchQ}→{t.RecCrunchQ}" : "Crunch ON→OFF";
                    GUILayout.Label(lbl, _tBad);
                }
                if (t.NeedsAndroidOverride) GUILayout.Label("Android MISSING", _tWarn);
                if (t.NeedsASTCChange) GUILayout.Label($"Android {t.CurAndroidFmt}→{t.RecAndroidFmt}", _tBad);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        // ────────────────────────────────────────────────────────────────
        //  Tab 3 – Unused
        // ────────────────────────────────────────────────────────────────
        private void DrawUnused()
        {
            BtnColor(new Color(1f, 0.8f, 0.3f));
            if (GUILayout.Button("Find Unused Textures", GUILayout.Height(30)))
            { FindUnused(); _unusedTree = BuildTree(_unusedTextures); }
            BtnColor(Color.white);

            if (_unusedTree == null || _unusedTree.Count == 0)
            { EditorGUILayout.HelpBox("No unused textures found.", MessageType.Info); return; }

            var sel = _unusedTree.GetSel();
            long selSz = 0; foreach (var p in sel) { var fi = new FileInfo(p); if (fi.Exists) selSz += fi.Length; }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Unused: {_unusedTree.Count} ({Fmt(_unusedTree.Size)})  |  Selected: {sel.Count} ({Fmt(selSz)})", _sSt);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select All")) _unusedTree.SetSel(true);
            if (GUILayout.Button("Deselect All")) _unusedTree.SetSel(false);
            if (GUILayout.Button("Expand All")) SetExp(_unusedTree, true);
            if (GUILayout.Button("Collapse All")) SetExp(_unusedTree, false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export CSV")) ExportUnusedCSV(sel);
            BtnColor(new Color(1f, 0.35f, 0.35f));
            EditorGUI.BeginDisabledGroup(sel.Count == 0);
            if (GUILayout.Button($"Move to Trash ({sel.Count})"))
            {
                if (EditorUtility.DisplayDialog("Confirm", $"Trash {sel.Count} textures ({Fmt(selSz)})?", "Yes", "Cancel"))
                { foreach (var p in sel) AssetDatabase.MoveAssetToTrash(p); AssetDatabase.Refresh(); FindUnused(); _unusedTree = BuildTree(_unusedTextures); }
            }
            EditorGUI.EndDisabledGroup();
            BtnColor(Color.white);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            _scrUnused = EditorGUILayout.BeginScrollView(_scrUnused);
            foreach (var ch in _unusedTree.Children) DrawFolder(ch, 0);
            foreach (var tx in _unusedTree.Textures) DrawLeaf(tx, 0);
            EditorGUILayout.EndScrollView();
        }

        private void DrawFolder(FolderNode n, int d)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(d * 18);
            bool s = EditorGUILayout.Toggle(n.Selected, GUILayout.Width(18));
            if (s != n.Selected) n.SetSel(s);
            n.Expanded = EditorGUILayout.Foldout(n.Expanded, "", true);
            GUILayout.Label($"■ {n.Name}/", _sFolder, GUILayout.ExpandWidth(false));
            GUILayout.Label($" ({n.Count}, {Fmt(n.Size)})", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (!n.Expanded) return;
            foreach (var ch in n.Children) DrawFolder(ch, d + 1);
            foreach (var tx in n.Textures) DrawLeaf(tx, d + 1);
        }

        private void DrawLeaf(string path, int d)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(d * 18 + 22);
            var fi = new FileInfo(path);
            if (GUILayout.Button($"  {Path.GetFileName(path)}  ({(fi.Exists ? Fmt(fi.Length) : "?")})", _sFile))
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ================================================================
        //  Preset Manager Helpers
        // ================================================================

        /// <summary>
        /// Generate .preset files from ruleset rules.
        /// Creates one .preset per texture type at Assets/Settings/Presets/.
        /// User then registers them in Project Settings → Preset Manager manually.
        /// </summary>
        private void GeneratePresetFiles()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
            if (!AssetDatabase.IsValidFolder("Assets/Settings/Presets"))
                AssetDatabase.CreateFolder("Assets/Settings", "Presets");

            // Create temp texture to get a real TextureImporter
            string tmpPath = "Assets/Settings/Presets/_tmp_preset_gen_.png";
            var tmpTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            File.WriteAllBytes(tmpPath, tmpTex.EncodeToPNG());
            Object.DestroyImmediate(tmpTex);
            AssetDatabase.ImportAsset(tmpPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(tmpPath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.DeleteAsset(tmpPath);
                Debug.LogError("[TextureOptimizer] Failed to create temp importer.");
                return;
            }

            int created = 0;
            foreach (var rule in _ruleset.Rules)
            {
                // Configure importer to match rule
                importer.textureType         = rule.TextureType;
                importer.maxTextureSize      = rule.MaxSize;
                importer.mipmapEnabled       = rule.MipMaps;
                importer.textureCompression  = rule.Compression;
                importer.crunchedCompression = rule.Crunch;
                importer.compressionQuality  = rule.CrunchQuality;
                importer.filterMode          = rule.FilterMode;

                // Set Android override
                if (_ruleset.OverrideAndroid)
                {
                    var android = importer.GetPlatformTextureSettings("Android");
                    android.overridden     = true;
                    android.format         = rule.AndroidASTCFormat;
                    android.maxTextureSize = rule.EffectiveAndroidMaxSize;
                    importer.SetPlatformTextureSettings(android);
                }

                var preset = new Preset(importer);
                string presetPath = $"Assets/Settings/Presets/Quest_{rule.TextureType}.preset";

                // Overwrite if exists
                if (File.Exists(presetPath))
                    AssetDatabase.DeleteAsset(presetPath);

                AssetDatabase.CreateAsset(preset, presetPath);
                created++;
            }

            AssetDatabase.DeleteAsset(tmpPath);
            AssetDatabase.Refresh();

            Debug.Log($"[TextureOptimizer] Generated {created} .preset files in Assets/Settings/Presets/.\n" +
                      "Next: go to Project Settings → Preset Manager → add each preset as a default for TextureImporter with appropriate filter.");

            // Ping the folder
            var folder = AssetDatabase.LoadAssetAtPath<Object>("Assets/Settings/Presets");
            if (folder != null) EditorGUIUtility.PingObject(folder);
        }

        private static bool CheckPresetManagerHasTextureDefaults()
        {
            string[] guids = AssetDatabase.FindAssets("t:Preset");
            foreach (var guid in guids)
            {
                var p = AssetDatabase.LoadAssetAtPath<Preset>(AssetDatabase.GUIDToAssetPath(guid));
                if (p != null && p.GetPresetType().GetManagedTypeName() == "UnityEditor.TextureImporter")
                {
                    var defaults = Preset.GetDefaultPresetsForType(p.GetPresetType());
                    return defaults != null && defaults.Length > 0;
                }
            }
            return false;
        }

        // ================================================================
        //  Core — Audit
        // ================================================================
        private void RunAudit()
        {
            _auditEntries.Clear();
            _auditTotal = _auditOK = _auditFail = _auditNoOverride = _auditNoAstc = _auditOversize = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _searchFolder });
            _auditTotal = guids.Length;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.StartsWith("Assets")) continue;
                    if (_ruleset.SkipEditorFolders && path.Contains("/Editor/")) continue;

                    if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Audit", $"{i}/{guids.Length}", (float)i / guids.Length))
                        break;

                    try
                    {
                        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (imp == null) continue;

                        var rule = _ruleset.GetRuleForType(imp.textureType);
                        bool hr = HasLabel(path, _ruleset.HighResLabel);
                        int maxSize = hr ? _ruleset.HighResMaxSize : rule.MaxSize;
                        var fo = _ruleset.GetFolderOverride(path);
                        if (fo != null && !hr) maxSize = fo.MaxSize;

                        GetTexSize(imp, out int w, out int h);

                        string i1 = null, i2 = null;
                        if (imp.maxTextureSize > maxSize) { i1 = $"MaxSize>{maxSize}"; _auditOversize++; }

                        var an = imp.GetPlatformTextureSettings("Android");
                        string af = "NONE";
                        if (!an.overridden)
                        { if (_ruleset.OverrideAndroid) { i2 = "No Android override"; _auditNoOverride++; } }
                        else
                        {
                            af = an.format.ToString();
                            if (_ruleset.OverrideAndroid && !IsASTC(an.format)) { i2 = $"Non-ASTC:{af}"; _auditNoAstc++; }
                        }

                        string iss = i1 != null && i2 != null ? $"{i1} | {i2}" : i1 ?? i2 ?? "";
                        bool ok = iss.Length == 0;
                        if (ok) _auditOK++; else _auditFail++;

                        _auditEntries.Add(new AuditEntry
                        {
                            Path = path, TexType = imp.textureType.ToString(), MaxSize = imp.maxTextureSize,
                            Compression = imp.textureCompression.ToString(), AndroidFormat = af,
                            HasAndroidOverride = an.overridden, ActualW = w, ActualH = h,
                            IsCompliant = ok, Issues = iss, AppliedRule = rule.TextureType.ToString(),
                        });
                    }
                    catch (System.Exception ex) { Debug.LogWarning($"[Audit] Skip {path}: {ex.Message}"); }
                    if (i > 0 && i % 200 == 0) EditorUtility.UnloadUnusedAssetsImmediate();
                }
            }
            finally { EditorUtility.ClearProgressBar(); EditorUtility.UnloadUnusedAssetsImmediate(); }
            _audited = true;
        }

        // ================================================================
        //  Core — Scan
        // ================================================================
        private void ScanTextures()
        {
            _textures.Clear(); _issueCount = 0; _estSaved = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { _searchFolder });
            _totalCount = guids.Length;

            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.StartsWith("Assets")) continue;
                    if (_ruleset.SkipEditorFolders && path.Contains("/Editor/")) continue;

                    if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Scan", $"{i}/{guids.Length}", (float)i / guids.Length))
                        break;

                    try
                    {
                        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (imp == null) continue;

                        var rule = _ruleset.GetRuleForType(imp.textureType);
                        bool hr = HasLabel(path, _ruleset.HighResLabel);
                        int maxSize = hr ? _ruleset.HighResMaxSize : rule.MaxSize;
                        var fo = _ruleset.GetFolderOverride(path);
                        if (fo != null && !hr) maxSize = fo.MaxSize;

                        GetTexSize(imp, out int w, out int h);
                        int maxDim = Mathf.Max(w, h);
                        int rec = maxSize;
                        if (_ruleset.AutoFitSize && maxDim > 0 && maxDim < maxSize)
                            rec = SmallestPOT(maxDim, maxSize);

                        bool nResize = imp.maxTextureSize > rec;
                        bool nMip = imp.mipmapEnabled != rule.MipMaps;
                        bool nComp = imp.textureCompression != rule.Compression;
                        bool nCrunch = rule.Crunch != imp.crunchedCompression || (rule.Crunch && imp.compressionQuality != rule.CrunchQuality);

                        var an = imp.GetPlatformTextureSettings("Android");
                        bool nOverride = _ruleset.OverrideAndroid && !an.overridden;

                        TextureImporterFormat recAstc = rule.AndroidASTCFormat;
                        if (fo != null && fo.AndroidASTCFormat != TextureImporterFormat.Automatic) recAstc = fo.AndroidASTCFormat;
                        if (hr) recAstc = TextureImporterFormat.ASTC_4x4;

                        bool nAstc = false;
                        var curFmt = TextureImporterFormat.Automatic;
                        if (_ruleset.OverrideAndroid && an.overridden)
                        {
                            curFmt = an.format;
                            nAstc = an.format != recAstc;
                            int effMax = hr ? _ruleset.HighResMaxSize : rule.EffectiveAndroidMaxSize;
                            if (fo != null && !hr) effMax = Mathf.Min(effMax, fo.MaxSize);
                            if (an.maxTextureSize > Mathf.Min(effMax, rec)) nAstc = true;
                        }

                        var info = new TexInfo
                        {
                            Path = path, Importer = imp, TexType = imp.textureType,
                            OrigMaxSize = imp.maxTextureSize, RecMaxSize = rec,
                            OrigMip = imp.mipmapEnabled, RecMip = rule.MipMaps,
                            OrigCrunch = imp.crunchedCompression, RecCrunch = rule.Crunch,
                            OrigCrunchQ = imp.compressionQuality, RecCrunchQ = rule.CrunchQuality,
                            OrigComp = imp.textureCompression, RecComp = rule.Compression,
                            ActualW = w, ActualH = h,
                            NeedsResize = nResize, NeedsMip = nMip, NeedsComp = nComp, NeedsCrunch = nCrunch,
                            NeedsAndroidOverride = nOverride, NeedsASTCChange = nAstc,
                            CurAndroidFmt = curFmt, RecAndroidFmt = recAstc,
                            HasAndroidOverride = an.overridden, HasHighRes = hr, RuleName = rule.TextureType.ToString(),
                        };

                        if (nResize) { float r = (float)rec / info.OrigMaxSize; var fi = new FileInfo(path); if (fi.Exists) _estSaved += (long)(fi.Length * (1f - r * r)); }
                        if (info.HasIssue) _issueCount++;
                        _textures.Add(info);
                    }
                    catch (System.Exception ex) { Debug.LogWarning($"[Scan] Skip {path}: {ex.Message}"); }
                    if (i > 0 && i % 200 == 0) EditorUtility.UnloadUnusedAssetsImmediate();
                }
            }
            finally { EditorUtility.ClearProgressBar(); EditorUtility.UnloadUnusedAssetsImmediate(); }
            _scanned = true;
        }

        // ================================================================
        //  Core — Apply
        // ================================================================
        private void ApplyOptimizations()
        {
            var sel = _textures.Where(t => t.HasIssue && t.IsSelected).ToList();
            if (sel.Count == 0) return;
            int applied = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < sel.Count; i++)
                {
                    var t = sel[i];
                    if (i % 20 == 0 && EditorUtility.DisplayCancelableProgressBar("Applying", $"{i}/{sel.Count}", (float)i / sel.Count))
                        break;
                    try
                    {
                        if (t.NeedsResize) t.Importer.maxTextureSize = t.RecMaxSize;
                        if (t.NeedsMip) t.Importer.mipmapEnabled = t.RecMip;
                        if (t.NeedsComp) t.Importer.textureCompression = t.RecComp;
                        if (t.NeedsCrunch) { t.Importer.crunchedCompression = t.RecCrunch; if (t.RecCrunch) t.Importer.compressionQuality = t.RecCrunchQ; }

                        if (_ruleset.OverrideAndroid && (t.NeedsAndroidOverride || t.NeedsASTCChange))
                        {
                            var an = t.Importer.GetPlatformTextureSettings("Android");
                            an.overridden = true;
                            an.format = t.RecAndroidFmt;
                            an.maxTextureSize = Mathf.Min(t.HasHighRes ? _ruleset.HighResMaxSize : _ruleset.GetRuleForType(t.TexType).EffectiveAndroidMaxSize, t.RecMaxSize);
                            t.Importer.SetPlatformTextureSettings(an);
                        }

                        t.Importer.SaveAndReimport();
                        applied++;
                    }
                    catch (System.Exception ex) { Debug.LogWarning($"[Apply] {t.Path}: {ex.Message}"); }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); EditorUtility.ClearProgressBar(); }
            Debug.Log($"[TextureOptimizer] Applied {applied}/{sel.Count}.");
            ScanTextures();
        }

        // ================================================================
        //  Core — Unused
        // ================================================================
        private void FindUnused()
        {
            _unusedTextures.Clear();
            string[] tg = AssetDatabase.FindAssets("t:Texture2D", new[] { _searchFolder });
            var allTex = new HashSet<string>();
            foreach (var g in tg) { string p = AssetDatabase.GUIDToAssetPath(g); if (p.StartsWith("Assets") && !p.Contains("/Editor/")) allTex.Add(p); }

            var refs = new HashSet<string>();
            string[] ag = AssetDatabase.FindAssets("", new[] { _searchFolder });
            try
            {
                for (int i = 0; i < ag.Length; i++)
                {
                    string ap = AssetDatabase.GUIDToAssetPath(ag[i]);
                    if (allTex.Contains(ap) || AssetDatabase.IsValidFolder(ap)) continue;
                    if (i % 100 == 0 && EditorUtility.DisplayCancelableProgressBar("Unused", $"{i}/{ag.Length}", (float)i / ag.Length)) break;
                    try { foreach (var dep in AssetDatabase.GetDependencies(ap, false)) if (allTex.Contains(dep)) refs.Add(dep); } catch { }
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            foreach (var sc in EditorBuildSettings.scenes)
            { if (!sc.enabled) continue; try { foreach (var dep in AssetDatabase.GetDependencies(sc.path, true)) if (allTex.Contains(dep)) refs.Add(dep); } catch { } }

            foreach (var rt in allTex.Where(p => p.Contains("/Resources/") || p.Contains("/StreamingAssets/") || p.Contains("/Addressables/"))) refs.Add(rt);
            _unusedTextures = allTex.Where(p => !refs.Contains(p)).OrderBy(p => p).ToList();
        }

        // ================================================================
        //  Helpers
        // ================================================================
        private bool CheckRuleset()
        {
            if (_ruleset != null) return true;
            EditorGUILayout.HelpBox("Assign ruleset in Ruleset & Config tab.", MessageType.Warning);
            return false;
        }

        private void CreateDefaultRuleset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            string path = "Assets/Settings/TextureImportRuleset.asset";
            if (File.Exists(path)) { _ruleset = AssetDatabase.LoadAssetAtPath<TextureImportRuleset>(path); Selection.activeObject = _ruleset; return; }
            var asset = CreateInstance<TextureImportRuleset>();
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.Refresh();
            _ruleset = asset; Selection.activeObject = _ruleset; EditorGUIUtility.PingObject(_ruleset);
        }

        private static void GetTexSize(TextureImporter imp, out int w, out int h)
        {
            w = h = 0;
            if (!_getWH_cached) { _getWH = typeof(TextureImporter).GetMethod("GetWidthAndHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); _getWH_cached = true; }
            if (_getWH != null) { try { object[] a = { 0, 0 }; _getWH.Invoke(imp, a); w = (int)a[0]; h = (int)a[1]; if (w > 0 && h > 0) return; } catch { } }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(imp.assetPath);
            if (tex != null) { w = tex.width; h = tex.height; Resources.UnloadAsset(tex); }
        }

        private static bool HasLabel(string path, string label)
        {
            if (string.IsNullOrEmpty(label)) return false;
            var obj = AssetDatabase.LoadMainAssetAtPath(path);
            if (obj == null) return false;
            var labels = AssetDatabase.GetLabels(obj);
            bool found = labels != null && labels.Any(l => string.Equals(l, label, System.StringComparison.OrdinalIgnoreCase));
            if (!(obj is GameObject)) Resources.UnloadAsset(obj);
            return found;
        }

        private static bool IsASTC(TextureImporterFormat f) =>
            f == TextureImporterFormat.ASTC_4x4 || f == TextureImporterFormat.ASTC_5x5 ||
            f == TextureImporterFormat.ASTC_6x6 || f == TextureImporterFormat.ASTC_8x8 ||
            f == TextureImporterFormat.ASTC_10x10 || f == TextureImporterFormat.ASTC_12x12;

        private static int SmallestPOT(int dim, int ceil) { foreach (int p in POT) if (p >= dim) return Mathf.Min(p, ceil); return ceil; }
        private static string Fmt(long b) { if (b < 1024) return $"{b} B"; if (b < 1024 * 1024) return $"{b / 1024f:F1} KB"; return $"{b / (1024f * 1024f):F1} MB"; }

        private FolderNode BuildTree(List<string> paths)
        {
            var root = new FolderNode { Name = "Assets", FullPath = "Assets", Expanded = true };
            foreach (var path in paths)
            {
                string[] parts = path.Split('/'); var cur = root;
                for (int i = 1; i < parts.Length - 1; i++)
                { var ch = cur.Children.FirstOrDefault(c => c.Name == parts[i]); if (ch == null) { ch = new FolderNode { Name = parts[i], FullPath = string.Join("/", parts, 0, i + 1) }; cur.Children.Add(ch); } cur = ch; }
                cur.Textures.Add(path);
            }
            SortTree(root); return root;
        }
        private void SortTree(FolderNode n) { n.Children.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal)); n.Textures.Sort(); foreach (var c in n.Children) SortTree(c); }
        private void SetExp(FolderNode n, bool v) { n.Expanded = v; foreach (var c in n.Children) SetExp(c, v); }

        private void ExportAuditCSV()
        {
            string sp = EditorUtility.SaveFilePanel("Save Audit", "", "texture_audit", "csv");
            if (string.IsNullOrEmpty(sp)) return;
            var l = new List<string> { "Path,Type,ActualW,ActualH,MaxSize,Compression,AndroidOverride,AndroidFormat,Rule,Compliant,Issues" };
            foreach (var e in _auditEntries) l.Add($"\"{e.Path}\",{e.TexType},{e.ActualW},{e.ActualH},{e.MaxSize},\"{e.Compression}\",{e.HasAndroidOverride},\"{e.AndroidFormat}\",{e.AppliedRule},{e.IsCompliant},\"{e.Issues}\"");
            File.WriteAllLines(sp, l);
        }
        private void ExportUnusedCSV(List<string> paths)
        {
            string sp = EditorUtility.SaveFilePanel("Save Unused", "", "unused_textures", "csv");
            if (string.IsNullOrEmpty(sp)) return;
            var l = new List<string> { "Path,FileSize(KB)" };
            foreach (var p in paths) { var fi = new FileInfo(p); l.Add($"\"{p}\",{(fi.Exists ? fi.Length / 1024 : 0)}"); }
            File.WriteAllLines(sp, l);
        }

        private void Hdr(string t) { EditorGUILayout.Space(2); EditorGUILayout.LabelField(t, _sS); }
        private static void BtnColor(Color c) { GUI.backgroundColor = c; }

        private void InitStyles()
        {
            if (_stylesOk) return;
            _sH = new GUIStyle(EditorStyles.boldLabel) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            _sS = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            _sSt = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, richText = true };
            _tBad = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white, background = MkTex(new Color(0.9f, 0.3f, 0.2f, 0.85f)) }, padding = new RectOffset(6, 6, 2, 2), margin = new RectOffset(2, 2, 2, 2), fontStyle = FontStyle.Bold, fontSize = 10 };
            _tGood = new GUIStyle(_tBad) { normal = { textColor = Color.white, background = MkTex(new Color(0.2f, 0.7f, 0.3f, 0.85f)) } };
            _tInfo = new GUIStyle(_tBad) { normal = { textColor = Color.white, background = MkTex(new Color(0.4f, 0.55f, 0.75f, 0.85f)) } };
            _tWarn = new GUIStyle(_tBad) { normal = { textColor = Color.white, background = MkTex(new Color(0.95f, 0.7f, 0.2f, 0.9f)) } };
            _sFolder = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
            _sFile = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, richText = true };
            _stylesOk = true;
        }
        private static Texture2D MkTex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
    }
}