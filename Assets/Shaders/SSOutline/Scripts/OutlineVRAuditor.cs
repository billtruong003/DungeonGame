#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BillDev.SSOutline
{
    public sealed class OutlineVRAuditor : EditorWindow
    {
        private Vector2 _scroll;
        private List<AuditEntry> _results = new();
        private int _passCount;
        private int _warnCount;
        private int _failCount;
        private GUIStyle _headerStyle;
        private GUIStyle _passStyle;
        private GUIStyle _warnStyle;
        private GUIStyle _failStyle;
        private GUIStyle _infoStyle;
        private bool _stylesReady;

        private enum Severity { Pass, Warn, Fail }

        private struct AuditEntry
        {
            public string category;
            public string message;
            public string fix;
            public Severity severity;
            public Action autoFix;
        }

        [MenuItem("Tools/BillDev/Outline VR Auditor")]
        public static void Open()
        {
            var window = GetWindow<OutlineVRAuditor>("Outline VR Audit");
            window.minSize = new Vector2(520, 400);
            window.RunAudit();
        }

        private void BuildStyles()
        {
            if (_stylesReady) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

            _passStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(0.3f, 0.85f, 0.4f) } };
            _warnStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.8f, 0.2f) } };
            _failStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = new Color(1f, 0.35f, 0.3f) } };
            _infoStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel) { padding = new RectOffset(20, 4, 0, 4) };

            _stylesReady = true;
        }

        private void OnGUI()
        {
            BuildStyles();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("SSOutline VR Build Audit", _headerStyle);
            EditorGUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Run Audit", GUILayout.Height(28)))
                    RunAudit();

                GUILayout.FlexibleSpace();

                var summary = $"Pass: {_passCount}  Warn: {_warnCount}  Fail: {_failCount}";
                EditorGUILayout.LabelField(summary, EditorStyles.boldLabel, GUILayout.Width(240));
            }

            EditorGUILayout.Space(6);
            var rect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(rect, new Color(0.35f, 0.75f, 0.45f));
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            string lastCategory = null;
            foreach (var entry in _results)
            {
                if (entry.category != lastCategory)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField(entry.category, EditorStyles.boldLabel);
                    lastCategory = entry.category;
                }

                var icon = entry.severity switch
                {
                    Severity.Pass => "\u2714",
                    Severity.Warn => "\u26A0",
                    Severity.Fail => "\u2716",
                    _ => ""
                };

                var style = entry.severity switch
                {
                    Severity.Pass => _passStyle,
                    Severity.Warn => _warnStyle,
                    Severity.Fail => _failStyle,
                    _ => _passStyle
                };

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"  {icon} {entry.message}", style);

                    if (entry.autoFix != null && entry.severity != Severity.Pass)
                    {
                        if (GUILayout.Button("Fix", GUILayout.Width(40), GUILayout.Height(18)))
                        {
                            entry.autoFix();
                            RunAudit();
                        }
                    }
                }

                if (entry.severity != Severity.Pass && !string.IsNullOrEmpty(entry.fix))
                    EditorGUILayout.LabelField(entry.fix, _infoStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunAudit()
        {
            _results.Clear();
            _passCount = _warnCount = _failCount = 0;

            AuditURPAsset();
            AuditRendererFeature();
            AuditShaderReferences();
            AuditShaderVariantStripping();
            AuditVolumeProfile();
            AuditXRSettings();
            AuditGraphicsAPI();
            AuditQualityLevels();

            foreach (var r in _results)
            {
                switch (r.severity)
                {
                    case Severity.Pass: _passCount++; break;
                    case Severity.Warn: _warnCount++; break;
                    case Severity.Fail: _failCount++; break;
                }
            }

            Repaint();
        }

        private void Add(string cat, string msg, Severity sev, string fix = null, Action autoFix = null)
        {
            _results.Add(new AuditEntry
            {
                category = cat,
                message = msg,
                severity = sev,
                fix = fix,
                autoFix = autoFix
            });
        }

        private void AuditURPAsset()
        {
            const string cat = "URP Pipeline Asset";

            var rpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rpAsset == null)
            {
                Add(cat, "No URP Asset assigned in Graphics Settings", Severity.Fail,
                    "Project Settings > Graphics > Scriptable Render Pipeline Settings > assign URP Asset");
                return;
            }

            Add(cat, $"URP Asset found: {rpAsset.name}", Severity.Pass);

            var so = new SerializedObject(rpAsset);

            var depthProp = so.FindProperty("m_RequireDepthTexture");
            if (depthProp != null)
            {
                if (depthProp.boolValue)
                    Add(cat, "Depth Texture enabled", Severity.Pass);
                else
                    Add(cat, "Depth Texture disabled", Severity.Fail,
                        "URP Asset > enable Depth Texture for depth-based edge detection",
                        () => { depthProp.boolValue = true; so.ApplyModifiedProperties(); });
            }

            var opaqueNormalsProp = so.FindProperty("m_RequireOpaqueNormalsTexture");
            if (opaqueNormalsProp != null)
            {
                if (opaqueNormalsProp.boolValue)
                    Add(cat, "Opaque Normals Texture enabled", Severity.Pass);
                else
                    Add(cat, "Opaque Normals Texture disabled", Severity.Warn,
                        "URP Asset > enable Opaque Normals Texture for normal-based edge detection");
            }
            else
            {
                CheckDepthNormalsFallback(rpAsset, cat);
            }

            var srpBatcher = so.FindProperty("m_UseSRPBatcher");
            if (srpBatcher != null && srpBatcher.boolValue)
                Add(cat, "SRP Batcher enabled", Severity.Pass);
            else if (srpBatcher != null)
                Add(cat, "SRP Batcher disabled", Severity.Warn,
                    "URP Asset > enable SRP Batcher for better VR performance");
        }

        private void CheckDepthNormalsFallback(UniversalRenderPipelineAsset rpAsset, string cat)
        {
            bool hasDepthNormals = false;
            var renderers = GetAllRendererData(rpAsset);
            foreach (var rd in renderers)
            {
                if (rd == null) continue;
                var so = new SerializedObject(rd);
                var iter = so.GetIterator();
                while (iter.NextVisible(true))
                {
                    if (iter.name.Contains("DepthNormal", StringComparison.OrdinalIgnoreCase))
                    {
                        hasDepthNormals = true;
                        break;
                    }
                }
            }

            if (!hasDepthNormals)
                Add(cat, "Could not verify Normals Texture setting", Severity.Warn,
                    "Ensure DepthNormals pass or Opaque Normals is enabled for normal-based edge detection");
        }

        private void AuditRendererFeature()
        {
            const string cat = "Outline Renderer Feature";

            var rpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (rpAsset == null) return;

            var renderers = GetAllRendererData(rpAsset);
            bool foundFeature = false;
            bool shadersMissing = false;

            foreach (var rd in renderers)
            {
                if (rd == null) continue;

                var features = GetRendererFeatures(rd);
                foreach (var feature in features)
                {
                    if (feature == null) continue;
                    if (feature.GetType() != typeof(OutlineFeature)) continue;

                    foundFeature = true;
                    Add(cat, $"OutlineFeature found on renderer: {rd.name}", Severity.Pass);

                    if (!feature.isActive)
                        Add(cat, $"OutlineFeature is DISABLED on {rd.name}", Severity.Fail,
                            "Enable the feature in the Renderer Asset inspector");

                    var featureSO = new SerializedObject(feature);
                    var outlineShaderProp = featureSO.FindProperty("outlineShader");
                    var maskShaderProp = featureSO.FindProperty("maskShader");

                    if (outlineShaderProp != null && outlineShaderProp.objectReferenceValue == null)
                    {
                        shadersMissing = true;
                        var outlineShader = Shader.Find("Hidden/BillDev/SSOutline");
                        Add(cat, "Outline Shader not assigned", Severity.Fail,
                            "Drag Hidden/BillDev/SSOutline shader into the Outline Shader slot on OutlineFeature",
                            outlineShader != null ? () =>
                            {
                                outlineShaderProp.objectReferenceValue = outlineShader;
                                featureSO.ApplyModifiedProperties();
                            } : null);
                    }

                    if (maskShaderProp != null && maskShaderProp.objectReferenceValue == null)
                    {
                        shadersMissing = true;
                        var maskShader = Shader.Find("Hidden/BillDev/SelectionMask");
                        Add(cat, "Mask Shader not assigned", Severity.Fail,
                            "Drag Hidden/BillDev/SelectionMask shader into the Mask Shader slot on OutlineFeature",
                            maskShader != null ? () =>
                            {
                                maskShaderProp.objectReferenceValue = maskShader;
                                featureSO.ApplyModifiedProperties();
                            } : null);
                    }

                    if (!shadersMissing && outlineShaderProp?.objectReferenceValue != null && maskShaderProp?.objectReferenceValue != null)
                        Add(cat, "Both shaders assigned (will survive build stripping)", Severity.Pass);
                }
            }

            if (!foundFeature)
                Add(cat, "OutlineFeature not found on any renderer", Severity.Fail,
                    "Add OutlineFeature to your URP Renderer Asset > Renderer Features");
        }

        private void AuditShaderReferences()
        {
            const string cat = "Shader Availability";

            var shaderChecks = new[]
            {
                ("Hidden/BillDev/SSOutline", "Outline composite shader"),
                ("Hidden/BillDev/SelectionMask", "Selection mask shader"),
                ("BillDev/InvisibleOccluder", "Invisible occluder shader"),
            };

            foreach (var (shaderName, label) in shaderChecks)
            {
                var shader = Shader.Find(shaderName);
                if (shader != null)
                    Add(cat, $"{label} found: {shaderName}", Severity.Pass);
                else
                    Add(cat, $"{label} missing: {shaderName}", Severity.Fail,
                        $"Ensure {shaderName} exists in the project and is not excluded from build");
            }

            var alwaysIncluded = GetAlwaysIncludedShaders();
            bool hasOutline = alwaysIncluded.Any(s => s != null && s.name == "Hidden/BillDev/SSOutline");
            bool hasMask = alwaysIncluded.Any(s => s != null && s.name == "Hidden/BillDev/SelectionMask");

            if (hasOutline && hasMask)
                Add(cat, "Outline shaders in Always Included list", Severity.Pass);
            else
                Add(cat, "Outline shaders not in Always Included list", Severity.Warn,
                    "If shaders are assigned via SerializeField on the feature this is fine. Otherwise add them to Project Settings > Graphics > Always Included Shaders");
        }

        private void AuditShaderVariantStripping()
        {
            const string cat = "Shader Variant Stripping";

            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset"));
            if (so == null) return;

            var stripProp = so.FindProperty("m_InstancingStripping");
            if (stripProp != null && stripProp.intValue == 2)
                Add(cat, "Instancing variants set to Strip All", Severity.Fail,
                    "Project Settings > Graphics > Shader Stripping > Instancing Variants > keep as Strip Unused");
            else
                Add(cat, "Instancing variants not stripped aggressively", Severity.Pass);
        }

        private void AuditVolumeProfile()
        {
            const string cat = "Volume & Profile";

            var volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);
            bool foundOutlineVolume = false;
            bool outlineActive = false;

            foreach (var vol in volumes)
            {
                if (vol.profile == null) continue;

                foreach (var component in vol.profile.components)
                {
                    if (component is not OutlineVolume outline) continue;

                    foundOutlineVolume = true;

                    if (outline.isActive.value)
                    {
                        outlineActive = true;
                        Add(cat, $"OutlineVolume active on \"{vol.gameObject.name}\"", Severity.Pass);

                        if (outline.mode.value == OutlineVolume.OutlineMode.FullScreen)
                        {
                            if (!outline.useDepth.value && !outline.useNormals.value)
                                Add(cat, "FullScreen mode but both Depth and Normals disabled", Severity.Fail,
                                    "Enable at least one edge detection method (useDepth or useNormals)");
                        }

                        if (outline.mode.value == OutlineVolume.OutlineMode.SelectionOnly ||
                            outline.mode.value == OutlineVolume.OutlineMode.Mixed)
                        {
                            if (outline.selectionLayer.value == 0)
                                Add(cat, "Selection/Mixed mode but Selection Layer is empty", Severity.Fail,
                                    "Assign at least one layer to Selection Layer");
                        }

                        if (outline.outlineColor.value.a < 0.01f)
                            Add(cat, "Outline color alpha is near zero", Severity.Warn,
                                "Outline will be invisible. Increase alpha of outline color");

                        if (outline.outlineIntensity.value < 0.01f)
                            Add(cat, "Outline intensity is near zero", Severity.Warn,
                                "Outline will be invisible. Increase outline intensity");

                        if (outline.debugMode.value != OutlineVolume.DebugView.None)
                            Add(cat, $"Debug mode active: {outline.debugMode.value}", Severity.Warn,
                                "Disable debug mode before building for VR");
                    }
                    else
                    {
                        Add(cat, $"OutlineVolume found but isActive=false on \"{vol.gameObject.name}\"", Severity.Warn,
                            "Enable isActive on the OutlineVolume component");
                    }
                }
            }

            if (!foundOutlineVolume)
                Add(cat, "No OutlineVolume component found in any Volume in scene", Severity.Fail,
                    "Add a Volume with OutlineVolume override to your scene");

            if (foundOutlineVolume && !outlineActive)
                Add(cat, "OutlineVolume exists but none are active", Severity.Fail,
                    "Set isActive = true on at least one OutlineVolume");
        }

        private void AuditXRSettings()
        {
            const string cat = "XR / VR Settings";

            bool xrManagementInstalled = false;
            string renderMode = "Unknown";

#if XR_MANAGEMENT_4_0_OR_NEWER || true
            try
            {
                var xrGenSettings = AssetDatabase.FindAssets("t:XRGeneralSettings");
                if (xrGenSettings.Length > 0)
                {
                    xrManagementInstalled = true;
                    Add(cat, "XR Management package detected", Severity.Pass);
                }
                else
                {
                    Add(cat, "XR Management not found", Severity.Warn,
                        "Install XR Plugin Management if targeting VR");
                }
            }
            catch
            {
                Add(cat, "Could not verify XR Management", Severity.Warn);
            }
#endif

            var xrSettingsAssets = AssetDatabase.FindAssets("t:ScriptableObject XR");
            foreach (var guid in xrSettingsAssets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (obj == null) continue;

                var type = obj.GetType();
                if (!type.Name.Contains("OculusSettings") && !type.Name.Contains("MetaXR") && !type.Name.Contains("OpenXR"))
                    continue;

                var so = new SerializedObject(obj);
                var iter = so.GetIterator();
                while (iter.NextVisible(true))
                {
                    if (iter.name.Contains("RenderMode", StringComparison.OrdinalIgnoreCase) ||
                        iter.name.Contains("m_StereoRenderingMode", StringComparison.OrdinalIgnoreCase))
                    {
                        renderMode = iter.intValue switch
                        {
                            0 => "Multi Pass",
                            1 => "Single Pass Instanced",
                            2 => "Multiview",
                            _ => $"Value={iter.intValue}"
                        };
                        break;
                    }
                }

                Add(cat, $"XR Plugin: {type.Name}, Render Mode: {renderMode}", Severity.Pass);

                if (renderMode == "Multi Pass")
                    Add(cat, "Multi Pass mode detected", Severity.Warn,
                        "Single Pass Instanced is recommended for VR performance. Outline works with both.");
            }

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            if (buildTarget == BuildTarget.Android)
                Add(cat, "Build target: Android (Meta Quest compatible)", Severity.Pass);
            else
                Add(cat, $"Build target: {buildTarget}", Severity.Warn,
                    "Switch to Android for Meta Quest builds");
        }

        private void AuditGraphicsAPI()
        {
            const string cat = "Graphics API";

            var target = EditorUserBuildSettings.activeBuildTarget;
            var apis = PlayerSettings.GetGraphicsAPIs(target);

            foreach (var api in apis)
            {
                if (api == GraphicsDeviceType.Vulkan || api == GraphicsDeviceType.OpenGLES3)
                    Add(cat, $"{api} present for {target}", Severity.Pass);
                else if (api == GraphicsDeviceType.OpenGLES2)
                    Add(cat, $"{api} present - not supported for VR outlines", Severity.Fail,
                        "Remove OpenGLES2 from Graphics APIs");
                else
                    Add(cat, $"{api} present for {target}", Severity.Pass);
            }

            if (PlayerSettings.GetUseDefaultGraphicsAPIs(target))
                Add(cat, "Auto Graphics API enabled", Severity.Warn,
                    "Consider manually setting Vulkan for Meta Quest for predictable behavior");
        }

        private void AuditQualityLevels()
        {
            const string cat = "Quality Levels";

            var qualityCount = QualitySettings.names.Length;
            for (int i = 0; i < qualityCount; i++)
            {
                var rp = QualitySettings.GetRenderPipelineAssetAt(i);
                if (rp is UniversalRenderPipelineAsset urpAsset)
                {
                    var renderers = GetAllRendererData(urpAsset);
                    bool hasFeature = renderers.Any(rd =>
                    {
                        if (rd == null) return false;
                        return GetRendererFeatures(rd).Any(f => f != null && f.GetType() == typeof(OutlineFeature));
                    });

                    if (hasFeature)
                        Add(cat, $"Quality \"{QualitySettings.names[i]}\" has OutlineFeature", Severity.Pass);
                    else
                        Add(cat, $"Quality \"{QualitySettings.names[i]}\" missing OutlineFeature", Severity.Warn,
                            "This quality level won't render outlines. Add OutlineFeature to its renderer.");
                }
                else if (rp == null)
                {
                    var defaultRp = GraphicsSettings.currentRenderPipeline;
                    if (defaultRp is UniversalRenderPipelineAsset)
                        Add(cat, $"Quality \"{QualitySettings.names[i]}\" uses default pipeline", Severity.Pass);
                }
            }
        }

        private static ScriptableRendererData[] GetAllRendererData(UniversalRenderPipelineAsset asset)
        {
            var result = new List<ScriptableRendererData>();

            var so = new SerializedObject(asset);
            var rendererDataList = so.FindProperty("m_RendererDataList");

            if (rendererDataList != null && rendererDataList.isArray)
            {
                for (int i = 0; i < rendererDataList.arraySize; i++)
                {
                    var element = rendererDataList.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue is ScriptableRendererData rd)
                        result.Add(rd);
                }
            }

            return result.ToArray();
        }

        private static List<ScriptableRendererFeature> GetRendererFeatures(ScriptableRendererData rendererData)
        {
            var result = new List<ScriptableRendererFeature>();
            var so = new SerializedObject(rendererData);
            var featuresProp = so.FindProperty("m_RendererFeatures");

            if (featuresProp != null && featuresProp.isArray)
            {
                for (int i = 0; i < featuresProp.arraySize; i++)
                {
                    var element = featuresProp.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue is ScriptableRendererFeature feature)
                        result.Add(feature);
                }
            }

            return result;
        }

        private static List<Shader> GetAlwaysIncludedShaders()
        {
            var result = new List<Shader>();
            var graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
            if (graphicsSettings == null) return result;

            var so = new SerializedObject(graphicsSettings);
            var shadersProp = so.FindProperty("m_AlwaysIncludedShaders");

            if (shadersProp != null && shadersProp.isArray)
            {
                for (int i = 0; i < shadersProp.arraySize; i++)
                {
                    var element = shadersProp.GetArrayElementAtIndex(i);
                    if (element.objectReferenceValue is Shader shader)
                        result.Add(shader);
                }
            }

            return result;
        }
    }
}
#endif
