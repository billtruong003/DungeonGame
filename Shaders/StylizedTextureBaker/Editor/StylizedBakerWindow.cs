using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    public class StylizedBakerWindow : EditorWindow
    {
        [MenuItem("Tools/Stylized Texture Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<StylizedBakerWindow>("Stylized Baker");
            window.minSize = new Vector2(420, 700);
        }

        private enum PreviewSource
        {
            SourceTexture,
            Curvature,
            Normal,
            AO,
            EdgeComposite,
            Position,
            DirectionalField,
            UVIslands,
            Stylized
        }

        private MeshFilter _targetMesh;
        private Texture2D _sourceTexture;
        private BakeSettings _settings;

        private BakerPipeline _pipeline;
        private PreviewRenderer _previewRenderer;

        private MeshDataMaps _dataMaps;
        private EdgeFeatureData _edgeData;
        private RenderTexture _linearSourceRT;
        private RenderTexture _srgbSourceRT;
        private RenderTexture _stylizedPreviewRT;
        private BakeResult _exportResult;

        private List<IStyleModule> _styleModules = new List<IStyleModule>();
        private StylePreset _activePreset;

        private Vector2 _scrollPosition;
        private float _bakeProgress;
        private string _bakeStatus = "";
        private bool _isBaking;

        private bool _showSourceSection = true;
        private bool _showDataMapSection = true;
        private bool _showStyleSection = true;
        private bool _showPreviewSection = true;
        private bool _showOutputSection = true;

        private PreviewSource _previewSource = PreviewSource.SourceTexture;
        private PreviewRenderer.ViewMode _viewMode = PreviewRenderer.ViewMode.Unlit;
        private int _soloLayerIndex = -1;
        private int _expandedLayerIndex = -1;
        private bool _dataMapsReady;
        private bool _stylizedDirty = true;

        private static readonly Color SectionBg = new Color(0.16f, 0.16f, 0.16f, 1f);
        private static readonly Color LayerBg = new Color(0.2f, 0.2f, 0.2f, 1f);
        private static readonly Color Accent = new Color(0.36f, 0.68f, 0.93f, 1f);
        private static readonly Color Danger = new Color(0.95f, 0.35f, 0.35f, 1f);
        private static readonly Color Success = new Color(0.35f, 0.85f, 0.45f, 1f);
        private static readonly Color Dim = new Color(0.55f, 0.55f, 0.55f, 1f);
        private static readonly Color PreviewBtn = new Color(0.45f, 0.75f, 0.95f, 1f);

        private readonly Dictionary<string, bool> _foldoutStates = new Dictionary<string, bool>();

        private void OnEnable()
        {
            _pipeline = new BakerPipeline();
            _previewRenderer = new PreviewRenderer();
            if (_settings == null) _settings = CreateInstance<BakeSettings>();
        }

        private void OnDisable()
        {
            CleanupAll();
            _previewRenderer?.Dispose();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawToolbar();
            EditorGUILayout.Space(2);
            DrawSourceSection();
            DrawDataMapSection();
            DrawPreviewSection();
            DrawStyleSection();
            DrawOutputSection();
            DrawProgressBar();

            EditorGUILayout.EndScrollView();

            if (Event.current.type == EventType.MouseDrag)
                Repaint();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = Accent },
                padding = new RectOffset(4, 0, 2, 0)
            };
            EditorGUILayout.LabelField("STYLIZED TEXTURE BAKER", titleStyle, GUILayout.Height(22));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset All", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                _styleModules.Clear();
                _soloLayerIndex = -1;
                _expandedLayerIndex = -1;
                CleanupAll();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSourceSection()
        {
            _showSourceSection = SectionHeader("SOURCE", _showSourceSection);
            if (!_showSourceSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var newMesh = (MeshFilter)EditorGUILayout.ObjectField("Mesh", _targetMesh, typeof(MeshFilter), true);
            if (newMesh != _targetMesh) { _targetMesh = newMesh; InvalidateAll(); }

            var newTex = (Texture2D)EditorGUILayout.ObjectField("Source Texture", _sourceTexture, typeof(Texture2D), false);
            if (newTex != _sourceTexture) { _sourceTexture = newTex; InvalidateAll(); }

            _settings.resolution = (BakeResolution)EditorGUILayout.EnumPopup("Resolution", _settings.resolution);

            _settings.useReducedPreview = EditorGUILayout.Toggle("Fast Preview", _settings.useReducedPreview);
            if (_settings.useReducedPreview)
            {
                EditorGUI.indentLevel++;
                _settings.previewResolution = (BakeResolution)EditorGUILayout.EnumPopup("Preview Res", _settings.previewResolution);
                EditorGUI.indentLevel--;
            }

            if (_targetMesh != null && _targetMesh.sharedMesh != null)
            {
                var m = _targetMesh.sharedMesh;
                EditorGUILayout.LabelField($"{m.vertexCount:N0} verts  •  {m.triangles.Length / 3:N0} tris", EditorStyles.miniLabel);
            }

            string meshError = BakeSettings.ValidateMesh(_targetMesh);
            if (meshError != null && _targetMesh != null)
                EditorGUILayout.HelpBox(meshError, MessageType.Warning);

            string texError = BakeSettings.ValidateTexture(_sourceTexture);
            if (texError != null && _sourceTexture != null)
                EditorGUILayout.HelpBox(texError, MessageType.Warning);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawDataMapSection()
        {
            _showDataMapSection = SectionHeader("DATA MAPS", _showDataMapSection);
            if (!_showDataMapSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            bool canBake = _targetMesh != null && _sourceTexture != null && !_isBaking;
            EditorGUI.BeginDisabledGroup(!canBake);

            var btnStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fixedHeight = 28 };
            if (GUILayout.Button(_dataMapsReady ? "Re-Bake Data Maps" : "Bake Data Maps", btnStyle))
                DoBakeDataMaps();

            EditorGUI.EndDisabledGroup();

            if (!_dataMapsReady)
                EditorGUILayout.HelpBox("Bake data maps first to enable preview and stylization.", MessageType.Info);

            EditorGUILayout.Space(4);

            Foldout("Curvature", () =>
            {
                _settings.curvatureSmoothIterations = EditorGUILayout.IntSlider("Smooth Iterations", _settings.curvatureSmoothIterations, 0, 10);
                _settings.curvatureScale = EditorGUILayout.Slider("Scale", _settings.curvatureScale, 0.01f, 5f);
                _settings.curvatureNormalizationPercentile = EditorGUILayout.Slider("Normalize Percentile", _settings.curvatureNormalizationPercentile, 0.8f, 1f);
            });

            Foldout("Mesh Edges (Dihedral)", () =>
            {
                _settings.dihedralAngleSoft = EditorGUILayout.Slider("Soft (°)", _settings.dihedralAngleSoft, 5f, 90f);
                _settings.dihedralAngleHard = EditorGUILayout.Slider("Hard (°)", _settings.dihedralAngleHard, 10f, 180f);
                if (_settings.dihedralAngleSoft > _settings.dihedralAngleHard)
                    _settings.dihedralAngleSoft = _settings.dihedralAngleHard;
            });

            Foldout("Edge Compositing", () =>
            {
                _settings.geometryEdgeWeight = EditorGUILayout.Slider("Geometry Weight", _settings.geometryEdgeWeight, 0f, 1f);
                _settings.textureEdgeWeight = EditorGUILayout.Slider("Texture Weight", _settings.textureEdgeWeight, 0f, 1f);
                _settings.curvatureEdgeThreshold = EditorGUILayout.Slider("Curvature Threshold", _settings.curvatureEdgeThreshold, 0f, 1f);
                _settings.curvatureEdgeWeight = EditorGUILayout.Slider("Curvature Weight", _settings.curvatureEdgeWeight, 0f, 1f);
                _settings.edgeThickenPixels = EditorGUILayout.IntSlider("Thicken (px)", _settings.edgeThickenPixels, 0, 8);
                _settings.minimumEdgeStrength = EditorGUILayout.Slider("Min Strength", _settings.minimumEdgeStrength, 0f, 1f);
                _settings.edgeSoftness = EditorGUILayout.Slider("Softness", _settings.edgeSoftness, 0.01f, 3f);
            });

            Foldout("Ambient Occlusion", () =>
            {
                _settings.aoRayCount = EditorGUILayout.IntSlider("Ray Count", _settings.aoRayCount, 8, 128);
                _settings.aoRadius = EditorGUILayout.Slider("Radius", _settings.aoRadius, 0.01f, 5f);
                _settings.aoIntensity = EditorGUILayout.Slider("Intensity", _settings.aoIntensity, 0f, 2f);
            });

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawPreviewSection()
        {
            _showPreviewSection = SectionHeader("3D PREVIEW", _showPreviewSection);
            if (!_showPreviewSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (!_dataMapsReady)
            {
                EditorGUILayout.LabelField("Bake data maps to enable preview.",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11, wordWrap = true },
                    GUILayout.Height(30));
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(4);
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Show:", GUILayout.Width(38));
            var labels = new[] { "Source", "Curv", "Norm", "AO", "Edge", "Pos", "Flow", "UV", "Styled" };
            int idx = (int)_previewSource;
            int newIdx = GUILayout.Toolbar(idx, labels);
            if (newIdx != idx)
            {
                _previewSource = (PreviewSource)newIdx;
                if (_previewSource == PreviewSource.Stylized && _stylizedDirty)
                    DoStylizedPreview();
                else
                    ApplyPreviewTexture();
            }
            EditorGUILayout.EndHorizontal();

            if (_previewSource == PreviewSource.Stylized)
            {
                EditorGUILayout.Space(2);
                bool canPreview = _styleModules.Count > 0;
                EditorGUI.BeginDisabledGroup(!canPreview);

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = PreviewBtn;
                var style = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fixedHeight = 26 };

                string label = _stylizedDirty ? "▶  Generate Preview" : "Refresh Preview";
                if (GUILayout.Button(label, style))
                    DoStylizedPreview();

                GUI.backgroundColor = prevBg;
                EditorGUI.EndDisabledGroup();

                if (!canPreview)
                    EditorGUILayout.HelpBox("Add at least one style layer first.", MessageType.Info);
            }

            EditorGUILayout.Space(4);

            float previewH = Mathf.Clamp(position.width * 0.55f, 180f, 380f);
            var previewRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(previewH), GUILayout.ExpandWidth(true));

            if (_previewRenderer.Render(previewRect))
                Repaint();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Light:", GUILayout.Width(38));

            if (GUILayout.Toggle(_viewMode == PreviewRenderer.ViewMode.Unlit, "Unlit", EditorStyles.miniButtonLeft))
                _viewMode = PreviewRenderer.ViewMode.Unlit;
            if (GUILayout.Toggle(_viewMode == PreviewRenderer.ViewMode.Lit, "Lit", EditorStyles.miniButtonRight))
                _viewMode = PreviewRenderer.ViewMode.Lit;

            _previewRenderer.Mode = _viewMode;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_previewSource != PreviewSource.Stylized && _previewSource != PreviewSource.SourceTexture)
            {
                var mapRT = GetDataMapRT(_previewSource);
                if (mapRT != null)
                {
                    EditorGUILayout.Space(4);
                    float thumbSize = Mathf.Min(position.width - 50, 170);
                    var thumbRect = GUILayoutUtility.GetRect(thumbSize, thumbSize, GUILayout.ExpandWidth(false));
                    thumbRect.x = (position.width - thumbSize) * 0.5f;
                    EditorGUI.DrawPreviewTexture(thumbRect, mapRT);
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawStyleSection()
        {
            _showStyleSection = SectionHeader("STYLE LAYERS", _showStyleSection);
            if (!_showStyleSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            _activePreset = (StylePreset)EditorGUILayout.ObjectField(_activePreset, typeof(StylePreset), false, GUILayout.Height(18));
            if (GUILayout.Button("Load", GUILayout.Width(44)) && _activePreset != null) LoadPreset(_activePreset);
            if (GUILayout.Button("Save", GUILayout.Width(44))) SavePreset();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            if (_styleModules.Count == 0)
            {
                EditorGUILayout.LabelField("No layers. Click '+ Add Layer' to begin.",
                    new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 11, wordWrap = true },
                    GUILayout.Height(30));
            }

            for (int i = 0; i < _styleModules.Count; i++)
            {
                if (DrawLayerHeader(i))
                {
                    GUIUtility.ExitGUI();
                    return;
                }

                if (_expandedLayerIndex == i)
                {
                    EditorGUI.indentLevel += 2;
                    _styleModules[i].DrawGUI();
                    EditorGUI.indentLevel -= 2;
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ Add Layer", GUILayout.Width(100), GUILayout.Height(22)))
                ShowAddMenu();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private bool DrawLayerHeader(int i)
        {
            var m = _styleModules[i];
            bool deleted = false;

            var rect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, LayerBg);

            float x = rect.x + 4, y = rect.y + 3;

            m.Enabled = EditorGUI.Toggle(new Rect(x, y, 16, 16), m.Enabled); x += 20;

            bool exp = _expandedLayerIndex == i;
            var arrowS = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Dim }, fontSize = 9 };
            if (GUI.Button(new Rect(x, y, 14, 16), exp ? "▼" : "▶", arrowS))
                _expandedLayerIndex = exp ? -1 : i;
            x += 16;

            var nameS = new GUIStyle(EditorStyles.label)
            { fontStyle = FontStyle.Bold, fontSize = 11, normal = { textColor = m.Enabled ? Color.white : Dim } };
            EditorGUI.LabelField(new Rect(x, y, 100, 18), m.DisplayName, nameS); x += 104;

            m.BlendMode = (StyleBlendMode)EditorGUI.EnumPopup(new Rect(x, y, 72, 16), m.BlendMode); x += 76;
            m.Opacity = GUI.HorizontalSlider(new Rect(x, y + 2, 60, 14), m.Opacity, 0f, 1f); x += 64;

            var pctS = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Dim }, alignment = TextAnchor.MiddleLeft };
            EditorGUI.LabelField(new Rect(x, y, 30, 16), $"{Mathf.RoundToInt(m.Opacity * 100)}%", pctS); x += 30;

            bool solo = _soloLayerIndex == i;
            var prev = GUI.color;
            GUI.color = solo ? Accent : Dim;
            if (GUI.Button(new Rect(x, y, 18, 16), "S", EditorStyles.miniButton))
            { _soloLayerIndex = solo ? -1 : i; _stylizedDirty = true; }
            GUI.color = prev; x += 20;

            EditorGUI.BeginDisabledGroup(i == 0);
            if (GUI.Button(new Rect(x, y, 18, 16), "▲", EditorStyles.miniButton)) SwapModules(i, i - 1);
            EditorGUI.EndDisabledGroup(); x += 20;

            EditorGUI.BeginDisabledGroup(i == _styleModules.Count - 1);
            if (GUI.Button(new Rect(x, y, 18, 16), "▼", EditorStyles.miniButton)) SwapModules(i, i + 1);
            EditorGUI.EndDisabledGroup(); x += 20;

            GUI.color = Danger;
            if (GUI.Button(new Rect(x, y, 18, 16), "✕", EditorStyles.miniButton))
            {
                _styleModules.RemoveAt(i);
                if (_soloLayerIndex == i) _soloLayerIndex = -1;
                else if (_soloLayerIndex > i) _soloLayerIndex--;
                if (_expandedLayerIndex == i) _expandedLayerIndex = -1;
                else if (_expandedLayerIndex > i) _expandedLayerIndex--;
                _stylizedDirty = true;
                deleted = true;
            }
            GUI.color = prev;

            return deleted;
        }

        private void DrawOutputSection()
        {
            _showOutputSection = SectionHeader("EXPORT", _showOutputSection);
            if (!_showOutputSection) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _settings.exportFormat = (ExportFormat)EditorGUILayout.EnumPopup("Format", _settings.exportFormat);
            _settings.paddingPixels = EditorGUILayout.IntSlider("Padding (px)", _settings.paddingPixels, 1, 32);
            _settings.seamBlendRadius = EditorGUILayout.IntSlider("Seam Blend", _settings.seamBlendRadius, 0, 4);

            EditorGUILayout.BeginHorizontal();
            _settings.outputFolder = EditorGUILayout.TextField("Output Folder", _settings.outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(28)))
            {
                string sel = EditorUtility.OpenFolderPanel("Output", "Assets", "");
                if (!string.IsNullOrEmpty(sel))
                {
                    int ai = sel.IndexOf("Assets");
                    if (ai >= 0) _settings.outputFolder = sel.Substring(ai);
                }
            }
            EditorGUILayout.EndHorizontal();

            _settings.exportOutlineMask = EditorGUILayout.Toggle("Export Outline Mask", _settings.exportOutlineMask);
            _settings.exportCompositeEdge = EditorGUILayout.Toggle("Export Composite Edge", _settings.exportCompositeEdge);
            _settings.exportDataMaps = EditorGUILayout.Toggle("Export Data Maps", _settings.exportDataMaps);

            EditorGUILayout.Space(8);

            bool canExport = _targetMesh != null && _sourceTexture != null && _styleModules.Count > 0 && !_isBaking;
            EditorGUI.BeginDisabledGroup(!canExport);

            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = canExport ? Success : Color.gray;
            var exportS = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, fixedHeight = 34 };

            if (GUILayout.Button("▶▶  FULL BAKE & EXPORT", exportS))
                DoFullBakeExport();

            GUI.backgroundColor = prevBg;
            EditorGUI.EndDisabledGroup();

            if (!canExport && !_isBaking)
            {
                string reason = _targetMesh == null ? "Assign a MeshFilter"
                    : _sourceTexture == null ? "Assign a Source Texture"
                    : _styleModules.Count == 0 ? "Add at least one Style Layer"
                    : "";
                if (!string.IsNullOrEmpty(reason))
                    EditorGUILayout.HelpBox(reason, MessageType.Info);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawProgressBar()
        {
            if (!_isBaking && string.IsNullOrEmpty(_bakeStatus)) return;
            EditorGUILayout.Space(2);
            var r = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(r, _bakeProgress, _bakeStatus);
            EditorGUILayout.Space(4);
        }

        private void DoBakeDataMaps()
        {
            string meshErr = BakeSettings.ValidateMesh(_targetMesh);
            string texErr = BakeSettings.ValidateTexture(_sourceTexture);

            if (meshErr != null) { Debug.LogError($"[StylizedBaker] {meshErr}"); return; }
            if (texErr != null) { Debug.LogError($"[StylizedBaker] {texErr}"); return; }

            SafeDisposeExportResult();
            CleanupDataMaps();
            ReleaseRT(ref _stylizedPreviewRT);
            _stylizedDirty = true;

            Mesh mesh = _targetMesh.sharedMesh;
            int res = _settings.ResolutionValue;

            _dataMaps = _pipeline.BakeDataMaps(mesh, _settings);
            _linearSourceRT = _pipeline.CreateLinearSourceRT(_sourceTexture, res);
            _srgbSourceRT = _pipeline.ColorSpace.CreateSRGBCopy(_linearSourceRT);
            _edgeData = _pipeline.DetectEdges(_linearSourceRT, _dataMaps, _settings);
            _dataMapsReady = true;

            _previewRenderer.SetMesh(mesh);
            _previewSource = PreviewSource.Curvature;
            ApplyPreviewTexture();
            _showPreviewSection = true;
            Repaint();
        }

        private void DoStylizedPreview()
        {
            if (!_dataMapsReady || _styleModules.Count == 0) return;

            ReleaseRT(ref _stylizedPreviewRT);

            int res = _settings.PreviewResolutionValue;
            var modules = GetActiveModules();

            _stylizedPreviewRT = _pipeline.StylizeToSRGB(
                _linearSourceRT, _dataMaps, _edgeData, modules, res);

            _stylizedDirty = _stylizedPreviewRT == null;
            _previewSource = PreviewSource.Stylized;
            ApplyPreviewTexture();
            Repaint();
        }

        private void DoFullBakeExport()
        {
            _isBaking = true;
            _bakeStatus = "Starting...";

            var modules = GetActiveModules();
            SafeDisposeExportResult();

            if (_dataMapsReady)
            {
                _exportResult = _pipeline.BakeFromCachedData(
                    _targetMesh.sharedMesh, _dataMaps, _edgeData,
                    _linearSourceRT, _settings, modules, OnProgress);
            }
            else
            {
                _exportResult = _pipeline.FullBake(
                    _targetMesh.sharedMesh, _sourceTexture,
                    _settings, modules, OnProgress);
            }

            if (_exportResult != null)
            {
                _pipeline.Export(_exportResult, _targetMesh.gameObject.name, _settings);
                _bakeStatus = "Export complete!";

                ReleaseRT(ref _stylizedPreviewRT);
                _stylizedPreviewRT = TextureUtility.CloneRT(_exportResult.StylizedColor);
                _stylizedDirty = false;
                _previewSource = PreviewSource.Stylized;
                _previewRenderer.SetMesh(_targetMesh.sharedMesh);
                ApplyPreviewTexture();
            }
            else
            {
                _bakeStatus = "Bake failed — check console.";
            }

            _isBaking = false;
            Repaint();
        }

        private void OnProgress(string status, float progress)
        {
            _bakeStatus = status;
            _bakeProgress = progress;
        }

        private void ApplyPreviewTexture()
        {
            RenderTexture tex = null;

            switch (_previewSource)
            {
                case PreviewSource.SourceTexture: tex = _srgbSourceRT; break;
                case PreviewSource.Stylized:      tex = _stylizedPreviewRT; break;
                default:                          tex = GetDataMapRT(_previewSource); break;
            }

            _previewRenderer.SetTexture(tex);
        }

        private RenderTexture GetDataMapRT(PreviewSource src)
        {
            if (_dataMaps == null) return null;
            switch (src)
            {
                case PreviewSource.Curvature:        return _dataMaps.CurvatureMap;
                case PreviewSource.Normal:           return _dataMaps.NormalMap;
                case PreviewSource.AO:               return _dataMaps.AOMap;
                case PreviewSource.EdgeComposite:    return _edgeData?.CompositeEdge;
                case PreviewSource.Position:         return _dataMaps.PositionMap;
                case PreviewSource.DirectionalField: return _dataMaps.DirectionalField;
                case PreviewSource.UVIslands:        return _dataMaps.UVIslandMask;
                default: return null;
            }
        }

        private List<IStyleModule> GetActiveModules()
        {
            if (_soloLayerIndex >= 0 && _soloLayerIndex < _styleModules.Count)
                return new List<IStyleModule> { _styleModules[_soloLayerIndex] };
            return _styleModules.Where(m => m.Enabled).ToList();
        }

        private bool SectionHeader(string title, bool expanded)
        {
            var r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, SectionBg);
            var s = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = Accent }, fontSize = 11, padding = new RectOffset(8, 0, 2, 0) };
            EditorGUI.LabelField(r, $"{(expanded ? "▼" : "▶")}  {title}", s);

            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            { expanded = !expanded; Event.current.Use(); Repaint(); }

            return expanded;
        }

        private void Foldout(string label, System.Action content)
        {
            if (!_foldoutStates.ContainsKey(label)) _foldoutStates[label] = false;
            _foldoutStates[label] = EditorGUILayout.Foldout(_foldoutStates[label], label, true);
            if (!_foldoutStates[label]) return;
            EditorGUI.indentLevel++;
            content();
            EditorGUI.indentLevel--;
        }

        private void ShowAddMenu()
        {
            var menu = new GenericMenu();
            foreach (StyleType type in System.Enum.GetValues(typeof(StyleType)))
            {
                var t = type;
                menu.AddItem(new GUIContent(t.ToString()), false, () =>
                {
                    var mod = StyleModuleFactory.Create(t);
                    mod.Order = _styleModules.Count;
                    _styleModules.Add(mod);
                    _expandedLayerIndex = _styleModules.Count - 1;
                    _stylizedDirty = true;
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }

        private void SwapModules(int a, int b)
        {
            var tmp = _styleModules[a];
            _styleModules[a] = _styleModules[b];
            _styleModules[b] = tmp;
            _styleModules[a].Order = a;
            _styleModules[b].Order = b;
            if (_soloLayerIndex == a) _soloLayerIndex = b;
            else if (_soloLayerIndex == b) _soloLayerIndex = a;
            if (_expandedLayerIndex == a) _expandedLayerIndex = b;
            else if (_expandedLayerIndex == b) _expandedLayerIndex = a;
            _stylizedDirty = true;
        }

        private void LoadPreset(StylePreset preset)
        {
            _styleModules.Clear();
            _soloLayerIndex = -1;
            _expandedLayerIndex = -1;
            _stylizedDirty = true;
            foreach (var d in preset.layers)
            {
                var mod = StyleModuleFactory.CreateFromData(d);
                if (mod != null) _styleModules.Add(mod);
            }
            Repaint();
        }

        private void SavePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Preset", "NewPreset", "asset", "Save style config");
            if (string.IsNullOrEmpty(path)) return;
            var preset = CreateInstance<StylePreset>();
            preset.presetName = System.IO.Path.GetFileNameWithoutExtension(path);
            foreach (var mod in _styleModules) preset.layers.Add(StyleModuleFactory.ToData(mod));
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            _activePreset = preset;
        }

        private void InvalidateAll()
        {
            SafeDisposeExportResult();
            CleanupDataMaps();
            ReleaseRT(ref _stylizedPreviewRT);
            _dataMapsReady = false;
            _stylizedDirty = true;
        }

        private void CleanupDataMaps()
        {
            _dataMaps?.Dispose(); _dataMaps = null;
            _edgeData?.Dispose(); _edgeData = null;
            ReleaseRT(ref _linearSourceRT);
            ReleaseRT(ref _srgbSourceRT);
            _dataMapsReady = false;
        }

        private void CleanupAll()
        {
            SafeDisposeExportResult();
            CleanupDataMaps();
            ReleaseRT(ref _stylizedPreviewRT);
            _bakeStatus = ""; _bakeProgress = 0f;
        }

        private void SafeDisposeExportResult()
        {
            if (_exportResult == null) return;

            if (_exportResult.DataMaps == _dataMaps)
                _exportResult.DataMaps = null;
            if (_exportResult.EdgeData == _edgeData)
                _exportResult.EdgeData = null;

            _exportResult.Dispose();
            _exportResult = null;
        }

        private void ReleaseRT(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            DestroyImmediate(rt);
            rt = null;
        }
    }
}
