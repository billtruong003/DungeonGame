using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

namespace CleanRenderPipeline.InvertHull.Editor
{
    /// <summary>
    /// InvertHullOutlineSetup - Full scene-wide outline management window
    /// 
    /// 3 tabs:
    ///   [Global Settings] - Create/edit OutlineGlobalSettings, setup OutlineManager
    ///   [Scene Scanner]   - Scan, select, batch convert shaders + add overrides
    ///   [Live Control]    - Runtime sliders to tweak all outlines in play mode
    /// </summary>
    public class InvertHullOutlineSetup : EditorWindow
    {
        // ================================================================
        // Shader mapping
        // ================================================================
        private static readonly Dictionary<string, string> ShaderMapping = new Dictionary<string, string>
        {
            { "CleanRender/ToonLit",     "CleanRender/ToonLit InvertHull" },
            { "CleanRender/ToonMetal",   "CleanRender/ToonMetal InvertHull" },
            { "CleanRender/ToonCrystal", "CleanRender/ToonCrystal InvertHull" },
        };

        private static Dictionary<string, string> _reverseMapping;
        private static Dictionary<string, string> ReverseMapping =>
            _reverseMapping ?? (_reverseMapping = ShaderMapping.ToDictionary(k => k.Value, k => k.Key));

        // ================================================================
        // Scan entry
        // ================================================================
        private class ScanEntry
        {
            public Renderer renderer;
            public GameObject gameObject;
            public string objectPath;
            public string shaderName;
            public Material[] convertibleMats;
            public bool selected;
            public bool hasOverride;
            public bool alreadyConverted;
        }

        // ================================================================
        // State
        // ================================================================
        private int _tab;
        private Vector2 _mainScroll, _listScroll;

        // Global settings tab
        private OutlineGlobalSettings _settings;
        private SerializedObject _settingsSO;

        // Scanner tab
        private List<ScanEntry> _scanResults = new List<ScanEntry>();
        private bool _hasScanned;
        private string _searchFilter = "";
        private bool _showConvertible = true, _showConverted = true;
        private Dictionary<string, bool> _shaderFilters = new Dictionary<string, bool>();
        private float _defaultOutlineWidth = 1.5f;
        private Color _defaultOutlineColor = Color.black;
        private bool _autoAddOverride = true;

        // Live control
        private float _liveWidth;
        private Color _liveColor;
        private float _liveFadeStart, _liveFadeEnd;

        [MenuItem("Tools/CleanRender/Outline Manager")]
        public static void ShowWindow()
        {
            var w = GetWindow<InvertHullOutlineSetup>("Outline Manager");
            w.minSize = new Vector2(520, 550);
        }

        private void OnEnable()
        {
            EditorSceneManager.sceneOpened += (s, m) => { _scanResults.Clear(); _hasScanned = false; };
            FindSettingsAsset();
        }

        private void FindSettingsAsset()
        {
            if (_settings != null) return;
            var guids = AssetDatabase.FindAssets("t:OutlineGlobalSettings");
            if (guids.Length > 0)
                _settings = AssetDatabase.LoadAssetAtPath<OutlineGlobalSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // ================================================================
        // GUI
        // ================================================================
        private void OnGUI()
        {
            _mainScroll = EditorGUILayout.BeginScrollView(_mainScroll);

            EditorGUILayout.Space(4);
            _tab = GUILayout.Toolbar(_tab, new[] { "Global Settings", "Scene Scanner", "Live Control" }, GUILayout.Height(28));
            EditorGUILayout.Space(6);

            switch (_tab)
            {
                case 0: DrawGlobalSettingsTab(); break;
                case 1: DrawScannerTab(); break;
                case 2: DrawLiveControlTab(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ================================================================
        // TAB 0: Global Settings
        // ================================================================
        private void DrawGlobalSettingsTab()
        {
            EditorGUILayout.LabelField("Outline Global Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "M\u1ed9t asset duy nh\u1ea5t \u0111i\u1ec1u khi\u1ec3n to\u00e0n b\u1ed9 outline trong scene.\n" +
                "T\u1ea1o asset \u2192 g\u1eafn v\u00e0o OutlineManager \u2192 done.",
                MessageType.Info);

            EditorGUILayout.Space(5);

            // Settings asset field
            _settings = (OutlineGlobalSettings)EditorGUILayout.ObjectField(
                "Settings Asset", _settings, typeof(OutlineGlobalSettings), false);

            if (_settings == null)
            {
                EditorGUILayout.Space(5);
                GUI.backgroundColor = new Color(0.4f, 0.85f, 0.4f);
                if (GUILayout.Button("Create Outline Settings Asset", GUILayout.Height(30)))
                {
                    CreateSettingsAsset();
                }
                GUI.backgroundColor = Color.white;
                return;
            }

            // Inline editor for the settings
            EditorGUILayout.Space(5);
            _settingsSO = new SerializedObject(_settings);
            _settingsSO.Update();

            DrawSettingsInline();

            _settingsSO.ApplyModifiedProperties();

            // OutlineManager setup
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            DrawManagerSetup();

            // Smooth Normal Baker shortcut
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Smooth Normals", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Hard-edge mesh c\u1ea7n bake smooth normal v\u00e0o tangent channel\n" +
                "\u0111\u1ec3 outline kh\u00f4ng b\u1ecb g\u00e3y t\u1ea1i c\u1ea1nh c\u1ee9ng.",
                MessageType.None);
            if (GUILayout.Button("Open Smooth Normal Baker"))
            {
                SmoothNormalBaker.ShowWindow();
            }
        }

        private void DrawSettingsInline()
        {
            EditorGUILayout.LabelField("Master", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("outlineEnabled"));
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("globalWidth"));
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("globalColor"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Distance Fade", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("fadeStartDistance"));
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("fadeEndDistance"));
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("alwaysUseFade"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Camera Optimization", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("frustumCullMargin"));
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("updateInterval"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Platform", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("desktopMode"));
            EditorGUILayout.PropertyField(_settingsSO.FindProperty("vrNearClip"));
        }

        private void DrawManagerSetup()
        {
            var manager = FindFirstObjectByType<OutlineManager>();

            if (manager != null)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"\u2714 OutlineManager on: {manager.gameObject.name}");
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                    Selection.activeGameObject = manager.gameObject;
                EditorGUILayout.EndHorizontal();

                if (manager.settings != _settings)
                {
                    EditorGUILayout.HelpBox("Manager's settings asset doesn't match! Click to update.", MessageType.Warning);
                    if (GUILayout.Button("Assign Current Settings to Manager"))
                    {
                        Undo.RecordObject(manager, "Assign settings");
                        manager.settings = _settings;
                        EditorUtility.SetDirty(manager);
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("\u2716 No OutlineManager in scene.", MessageType.Warning);

                GUI.backgroundColor = new Color(0.35f, 0.7f, 1f);
                if (GUILayout.Button("Add OutlineManager to Main Camera", GUILayout.Height(28)))
                {
                    var cam = Camera.main;
                    if (cam == null)
                    {
                        EditorUtility.DisplayDialog("No Camera", "Tag a camera as MainCamera.", "OK");
                        return;
                    }
                    var mgr = Undo.AddComponent<OutlineManager>(cam.gameObject);
                    mgr.settings = _settings;
                    EditorUtility.SetDirty(mgr);
                }
                GUI.backgroundColor = Color.white;
            }
        }

        private void CreateSettingsAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Outline Settings", "OutlineSettings", "asset",
                "Choose where to save the OutlineGlobalSettings asset");
            if (string.IsNullOrEmpty(path)) return;

            var asset = ScriptableObject.CreateInstance<OutlineGlobalSettings>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _settings = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // ================================================================
        // TAB 1: Scene Scanner
        // ================================================================
        private void DrawScannerTab()
        {
            // Scan button
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button("\u26A1 Scan Scene", GUILayout.Height(30)))
                PerformScan();
            GUI.backgroundColor = Color.white;
            if (_hasScanned && GUILayout.Button("\u21BB", GUILayout.Width(36), GUILayout.Height(30)))
                PerformScan(true);
            EditorGUILayout.EndHorizontal();

            if (_hasScanned)
            {
                DrawScanStats();
                EditorGUILayout.Space(3);

                // Default settings for conversion
                EditorGUILayout.LabelField("Conversion Defaults", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                _defaultOutlineWidth = EditorGUILayout.Slider("Width (px)", _defaultOutlineWidth, 0.1f, 10f);
                _defaultOutlineColor = EditorGUILayout.ColorField(_defaultOutlineColor, GUILayout.Width(60));
                EditorGUILayout.EndHorizontal();
                _autoAddOverride = EditorGUILayout.Toggle("Auto add OutlineOverride", _autoAddOverride);

                EditorGUILayout.Space(3);
                DrawSelectionBar();
                DrawFilterBar();
                DrawActionButtons();
                DrawEntryList();
            }
            else
            {
                EditorGUILayout.HelpBox("Nh\u1ea5n Scan Scene \u0111\u1ec3 qu\u00e9t to\u00e0n b\u1ed9 renderer trong scene.", MessageType.None);
            }
        }

        private void DrawScanStats()
        {
            int total = _scanResults.Count;
            int convertible = _scanResults.Count(e => !e.alreadyConverted);
            int done = _scanResults.Count(e => e.alreadyConverted);
            int selected = _scanResults.Count(e => e.selected);
            int withOv = _scanResults.Count(e => e.hasOverride);

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"Total: {total}", EditorStyles.miniLabel);
            GUILayout.Label($"Convertible: {convertible}", EditorStyles.miniLabel);
            GUILayout.Label($"Done: {done}", EditorStyles.miniLabel);
            GUILayout.Label($"Selected: {selected}", EditorStyles.miniLabel);
            GUILayout.Label($"Override: {withOv}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionBar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                foreach (var e in Filtered()) e.selected = true;
            if (GUILayout.Button("None", EditorStyles.miniButtonMid, GUILayout.Width(45)))
                foreach (var e in Filtered()) e.selected = false;
            if (GUILayout.Button("Convertible", EditorStyles.miniButtonMid, GUILayout.Width(80)))
            {
                foreach (var e in _scanResults) e.selected = false;
                foreach (var e in Filtered().Where(e => !e.alreadyConverted)) e.selected = true;
            }
            if (GUILayout.Button("No Override", EditorStyles.miniButtonRight, GUILayout.Width(80)))
            {
                foreach (var e in _scanResults) e.selected = false;
                foreach (var e in Filtered().Where(e => !e.hasOverride)) e.selected = true;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // Shader type toggles
            var types = _scanResults.Select(e => e.shaderName).Distinct().ToList();
            if (types.Count > 1)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Shader:", GUILayout.Width(48));
                foreach (var t in types)
                {
                    if (!_shaderFilters.ContainsKey(t)) _shaderFilters[t] = true;
                    string label = t.Replace("CleanRender/", "").Replace(" InvertHull", " [IH]");
                    _shaderFilters[t] = GUILayout.Toggle(_shaderFilters[t], label, EditorStyles.miniButton, GUILayout.MinWidth(50));
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("\uD83D\uDD0D", GUILayout.Width(18));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            if (GUILayout.Button("\u2716", GUILayout.Width(20))) _searchFilter = "";
            _showConvertible = GUILayout.Toggle(_showConvertible, "Base", EditorStyles.miniButtonLeft, GUILayout.Width(45));
            _showConverted = GUILayout.Toggle(_showConverted, "IH", EditorStyles.miniButtonRight, GUILayout.Width(35));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons()
        {
            int selConvert = _scanResults.Count(e => e.selected && !e.alreadyConverted);
            int selOverride = _scanResults.Count(e => e.selected && !e.hasOverride);
            int selRevert = _scanResults.Count(e => e.selected && e.alreadyConverted);

            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = selConvert > 0 ? new Color(0.35f, 0.65f, 1f) : Color.gray;
            EditorGUI.BeginDisabledGroup(selConvert == 0);
            if (GUILayout.Button($"\u2192 Convert ({selConvert})", GUILayout.Height(26)))
                ConvertSelected();
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = selOverride > 0 ? new Color(0.4f, 0.85f, 0.4f) : Color.gray;
            EditorGUI.BeginDisabledGroup(selOverride == 0);
            if (GUILayout.Button($"+ Override ({selOverride})", GUILayout.Height(26)))
                AddOverridesToSelected();
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = selRevert > 0 ? new Color(1f, 0.65f, 0.35f) : Color.gray;
            EditorGUI.BeginDisabledGroup(selRevert == 0);
            if (GUILayout.Button($"\u2190 Revert ({selRevert})", GUILayout.Height(26)))
                RevertSelected();
            EditorGUI.EndDisabledGroup();

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        private void DrawEntryList()
        {
            var filtered = Filtered();
            float h = Mathf.Clamp(filtered.Count * 24f + 8f, 60f, 320f);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.Height(h));

            if (filtered.Count == 0)
                EditorGUILayout.LabelField("No results.", EditorStyles.centeredGreyMiniLabel);

            for (int i = 0; i < filtered.Count; i++)
            {
                var e = filtered[i];
                if (e.renderer == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

                e.selected = EditorGUILayout.Toggle(e.selected, GUILayout.Width(16));

                // Status
                string icon = e.alreadyConverted ? (e.hasOverride ? "\u2714\u2714" : "\u2714") : "\u25CB";
                EditorGUILayout.LabelField(icon, GUILayout.Width(20));

                // Name (clickable)
                if (GUILayout.Button(e.gameObject.name, EditorStyles.linkLabel, GUILayout.MinWidth(90)))
                {
                    EditorGUIUtility.PingObject(e.gameObject);
                    Selection.activeGameObject = e.gameObject;
                }

                // Shader
                string sh = e.shaderName.Replace("CleanRender/", "");
                if (sh.Length > 20) sh = sh.Substring(0, 17) + "...";
                EditorGUILayout.LabelField(sh, EditorStyles.miniLabel, GUILayout.Width(125));

                // Parent path
                if (e.objectPath.Contains("/"))
                {
                    string parent = e.objectPath.Substring(0, e.objectPath.LastIndexOf('/'));
                    if (parent.Length > 30) parent = "..." + parent.Substring(parent.Length - 27);
                    EditorGUILayout.LabelField(new GUIContent(parent, e.objectPath), EditorStyles.miniLabel, GUILayout.MinWidth(50));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ================================================================
        // TAB 2: Live Control
        // ================================================================
        private void DrawLiveControlTab()
        {
            EditorGUILayout.LabelField("Live Control", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Khi \u0111ang Play mode ho\u1eb7c c\u00f3 OutlineManager active,\n" +
                "c\u00e1c slider d\u01b0\u1edbi \u0111\u00e2y thay \u0111\u1ed5i outline realtime.",
                MessageType.Info);

            var manager = FindFirstObjectByType<OutlineManager>();
            if (manager == null || manager.settings == null)
            {
                EditorGUILayout.HelpBox("Need OutlineManager + settings in scene. Go to Global Settings tab.", MessageType.Warning);
                return;
            }

            var s = manager.settings;

            EditorGUILayout.Space(5);

            // Master toggle
            bool wasEnabled = s.outlineEnabled;
            s.outlineEnabled = EditorGUILayout.Toggle("Outlines Enabled", s.outlineEnabled);

            EditorGUILayout.Space(3);

            // Width slider
            float newWidth = EditorGUILayout.Slider("Global Width (px)", s.globalWidth, 0f, 10f);
            if (!Mathf.Approximately(newWidth, s.globalWidth))
            {
                Undo.RecordObject(s, "Change outline width");
                s.globalWidth = newWidth;
            }

            // Color
            Color newColor = EditorGUILayout.ColorField("Global Color", s.globalColor);
            if (newColor != s.globalColor)
            {
                Undo.RecordObject(s, "Change outline color");
                s.globalColor = newColor;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Distance Fade", EditorStyles.miniBoldLabel);

            float newFadeStart = EditorGUILayout.FloatField("Fade Start", s.fadeStartDistance);
            float newFadeEnd = EditorGUILayout.FloatField("Fade End", s.fadeEndDistance);
            if (!Mathf.Approximately(newFadeStart, s.fadeStartDistance) ||
                !Mathf.Approximately(newFadeEnd, s.fadeEndDistance))
            {
                Undo.RecordObject(s, "Change fade distances");
                s.fadeStartDistance = newFadeStart;
                s.fadeEndDistance = newFadeEnd;
            }

            EditorGUILayout.Space(5);

            // Quick buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All OFF", GUILayout.Height(28)))
            {
                s.outlineEnabled = false;
                s.Apply(UnityEngine.XR.XRSettings.isDeviceActive);
            }
            if (GUILayout.Button("All ON", GUILayout.Height(28)))
            {
                s.outlineEnabled = true;
                s.Apply(UnityEngine.XR.XRSettings.isDeviceActive);
            }
            if (GUILayout.Button("Width 0.5", GUILayout.Height(28)))
            {
                s.globalWidth = 0.5f;
                s.Apply(UnityEngine.XR.XRSettings.isDeviceActive);
            }
            if (GUILayout.Button("Width 2.0", GUILayout.Height(28)))
            {
                s.globalWidth = 2.0f;
                s.Apply(UnityEngine.XR.XRSettings.isDeviceActive);
            }
            EditorGUILayout.EndHorizontal();

            // Apply changes in editor (even outside play mode for preview)
            if (GUI.changed)
            {
                s.Apply(UnityEngine.XR.XRSettings.isDeviceActive);
                EditorUtility.SetDirty(s);
            }
        }

        // ================================================================
        // Scan
        // ================================================================
        private void PerformScan(bool preserveSel = false)
        {
            Dictionary<int, bool> prevSel = null;
            if (preserveSel)
            {
                prevSel = new Dictionary<int, bool>();
                foreach (var e in _scanResults)
                    if (e.renderer != null) prevSel[e.renderer.GetInstanceID()] = e.selected;
            }

            _scanResults.Clear();
            _shaderFilters.Clear();

            var baseShaders = new HashSet<string>(ShaderMapping.Keys);
            var ihShaders = new HashSet<string>(ShaderMapping.Values);
            var allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                var convertible = new List<Material>();
                string primaryShader = null;
                bool isIH = false;

                foreach (var m in mats)
                {
                    if (m == null) continue;
                    string sn = m.shader.name;
                    if (baseShaders.Contains(sn))
                    {
                        convertible.Add(m);
                        if (primaryShader == null) primaryShader = sn;
                    }
                    else if (ihShaders.Contains(sn))
                    {
                        convertible.Add(m);
                        if (primaryShader == null) { primaryShader = sn; isIH = true; }
                    }
                }
                if (convertible.Count == 0) continue;

                var entry = new ScanEntry
                {
                    renderer = r,
                    gameObject = r.gameObject,
                    objectPath = HierarchyPath(r.gameObject),
                    shaderName = primaryShader,
                    convertibleMats = convertible.ToArray(),
                    selected = false,
                    hasOverride = r.GetComponent<OutlineOverride>() != null,
                    alreadyConverted = isIH
                };

                if (prevSel != null && prevSel.TryGetValue(r.GetInstanceID(), out bool ws))
                    entry.selected = ws;

                _scanResults.Add(entry);
            }

            _scanResults.Sort((a, b) =>
            {
                if (a.alreadyConverted != b.alreadyConverted) return a.alreadyConverted ? 1 : -1;
                return string.Compare(a.objectPath, b.objectPath, System.StringComparison.Ordinal);
            });

            _hasScanned = true;
            Repaint();
        }

        private static string HierarchyPath(GameObject go)
        {
            string p = go.name;
            var t = go.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }

        private List<ScanEntry> Filtered()
        {
            return _scanResults.Where(e =>
            {
                if (e.renderer == null) return false;
                if (e.alreadyConverted && !_showConverted) return false;
                if (!e.alreadyConverted && !_showConvertible) return false;
                if (_shaderFilters.TryGetValue(e.shaderName, out bool ok) && !ok) return false;
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    string lf = _searchFilter.ToLowerInvariant();
                    if (!e.objectPath.ToLowerInvariant().Contains(lf) &&
                        !e.shaderName.ToLowerInvariant().Contains(lf)) return false;
                }
                return true;
            }).ToList();
        }

        // ================================================================
        // Actions
        // ================================================================
        private void ConvertSelected()
        {
            var targets = _scanResults.Where(e => e.selected && !e.alreadyConverted).ToList();
            if (targets.Count == 0) return;

            Undo.SetCurrentGroupName("Outline Convert");
            int ug = Undo.GetCurrentGroup();
            int matCount = 0, ovCount = 0;

            foreach (var e in targets)
            {
                foreach (var m in e.convertibleMats)
                {
                    if (m == null) continue;
                    if (ShaderMapping.TryGetValue(m.shader.name, out string target))
                    {
                        var s = Shader.Find(target);
                        if (s == null) continue;
                        Undo.RecordObject(m, "Convert");
                        m.shader = s;
                        m.SetColor("_OutlineColor", _defaultOutlineColor);
                        m.SetFloat("_OutlineWidth", _defaultOutlineWidth);
                        m.EnableKeyword("_INVERT_HULL_ON");
                        EditorUtility.SetDirty(m);
                        matCount++;
                    }
                }

                if (_autoAddOverride && !e.hasOverride)
                {
                    var ov = Undo.AddComponent<OutlineOverride>(e.gameObject);
                    ov.widthOverride = _defaultOutlineWidth;
                    ovCount++;
                }

                e.alreadyConverted = true;
                e.hasOverride = e.renderer.GetComponent<OutlineOverride>() != null;
                if (e.convertibleMats.Length > 0 && e.convertibleMats[0] != null)
                    e.shaderName = e.convertibleMats[0].shader.name;
            }

            Undo.CollapseUndoOperations(ug);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Outline] Converted {matCount} materials, added {ovCount} overrides.");
        }

        private void RevertSelected()
        {
            var targets = _scanResults.Where(e => e.selected && e.alreadyConverted).ToList();
            if (targets.Count == 0) return;

            Undo.SetCurrentGroupName("Outline Revert");
            int ug = Undo.GetCurrentGroup();
            int c = 0;

            foreach (var e in targets)
            {
                foreach (var m in e.convertibleMats)
                {
                    if (m == null) continue;
                    if (ReverseMapping.TryGetValue(m.shader.name, out string baseName))
                    {
                        var s = Shader.Find(baseName);
                        if (s == null) continue;
                        Undo.RecordObject(m, "Revert");
                        m.shader = s;
                        EditorUtility.SetDirty(m);
                        c++;
                    }
                }
                e.alreadyConverted = false;
                if (e.convertibleMats.Length > 0 && e.convertibleMats[0] != null)
                    e.shaderName = e.convertibleMats[0].shader.name;
            }

            Undo.CollapseUndoOperations(ug);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Outline] Reverted {c} materials.");
        }

        private void AddOverridesToSelected()
        {
            var targets = _scanResults.Where(e => e.selected && !e.hasOverride).ToList();
            if (targets.Count == 0) return;

            Undo.SetCurrentGroupName("Add Overrides");
            int ug = Undo.GetCurrentGroup();
            int c = 0;

            foreach (var e in targets)
            {
                if (e.renderer == null) continue;
                var ov = Undo.AddComponent<OutlineOverride>(e.gameObject);
                ov.widthOverride = _defaultOutlineWidth;
                e.hasOverride = true;
                c++;
            }

            Undo.CollapseUndoOperations(ug);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Outline] Added {c} OutlineOverride components.");
        }
    }
}
