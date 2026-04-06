using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ShaderOptimization.Models;
using ShaderOptimization.Core;

namespace ShaderOptimization.UI
{
    public class ShaderOptimizationSuite : EditorWindow
    {
        private List<ShaderAnalysisResult> shaderResults = new List<ShaderAnalysisResult>();
        private List<DefaultAsset> customScanRoots = new List<DefaultAsset>();
        private Vector2 mainScroll;
        private int selectedTab = 0;
        private readonly string[] tabs = { "Analysis Results", "Settings", "Auto-Stripper Configuration" };
        private string searchQuery = "";
        private ShaderFilter currentFilter = ShaderFilter.All;[MenuItem("Tools/Optimization/Shader Optimization Suite")]
        public static void ShowWindow()
        {
            GetWindow<ShaderOptimizationSuite>("Shader Optimization");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            selectedTab = GUILayout.Toolbar(selectedTab, tabs);
            EditorGUILayout.Space();

            switch (selectedTab)
            {
                case 0: DrawAnalysisTab(); break;
                case 1: DrawSettingsTab(); break;
                case 2: DrawAutoStripperTab(); break;
            }
        }

        private void DrawAnalysisTab()
        {
            if (GUILayout.Button("Run Comprehensive Analysis", GUILayout.Height(40)))
            {
                RunAnalysis();
            }

            if (shaderResults.Count == 0) return;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            currentFilter = (ShaderFilter)EditorGUILayout.EnumPopup(currentFilter, EditorStyles.toolbarPopup, GUILayout.Width(150));
            searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("Delete ALL Unused", EditorStyles.toolbarButton, GUILayout.Width(130)))
            {
                DeleteAllUnused();
            }

            if (GUILayout.Button("Mark Filtered to Strip", EditorStyles.toolbarButton, GUILayout.Width(140)))
            {
                MarkFilteredForStripping(true);
            }

            if (GUILayout.Button("Unmark Filtered", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                MarkFilteredForStripping(false);
            }

            EditorGUILayout.EndHorizontal();

            mainScroll = EditorGUILayout.BeginScrollView(mainScroll);
            var filtered = shaderResults.Where(FilterPredicate).ToList();
            foreach (var result in filtered)
            {
                DrawShaderResult(result);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSettingsTab()
        {
            EditorGUILayout.LabelField("Custom Scan Roots", EditorStyles.boldLabel);

            for (int i = 0; i < customScanRoots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                customScanRoots[i] = (DefaultAsset)EditorGUILayout.ObjectField(customScanRoots[i], typeof(DefaultAsset), false);
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    customScanRoots.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Custom Root", GUILayout.Width(150)))
            {
                customScanRoots.Add(null);
            }
        }

        private void DrawAutoStripperTab()
        {
            var config = ShaderOptimizationConfig.Instance;

            config.EnableAutoStripping = EditorGUILayout.Toggle("Enable Auto-Stripping", config.EnableAutoStripping);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shaders to Strip (Exact Name)", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear All Shaders", GUILayout.Width(150)))
            {
                config.ShadersToStrip.Clear();
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < config.ShadersToStrip.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                config.ShadersToStrip[i] = EditorGUILayout.TextField(config.ShadersToStrip[i]);
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    config.ShadersToStrip.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Shader to Strip", GUILayout.Width(150)))
            {
                config.ShadersToStrip.Add("");
            }

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Variants/Keywords to Strip (e.g. FOG_LINEAR)", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear All Keywords", GUILayout.Width(150)))
            {
                config.VariantsToStrip.Clear();
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < config.VariantsToStrip.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                config.VariantsToStrip[i] = EditorGUILayout.TextField(config.VariantsToStrip[i]);
                if (GUILayout.Button("X", GUILayout.Width(30)))
                {
                    config.VariantsToStrip.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Add Keyword to Strip", GUILayout.Width(150)))
            {
                config.VariantsToStrip.Add("");
            }

            if (GUI.changed)
            {
                config.SaveConfig();
            }
        }

        private void DrawShaderResult(ShaderAnalysisResult result)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            result.IsExpanded = EditorGUILayout.Foldout(result.IsExpanded, result.Name, true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            if (result.IsUnused) GUI.color = Color.red;
            else if (result.IsUsedOnlyOutsideScenes) GUI.color = Color.yellow;
            else GUI.color = Color.green;

            GUILayout.Label(GetStatusString(result), EditorStyles.boldLabel);
            GUI.color = Color.white;

            EditorGUILayout.EndHorizontal();

            if (result.IsExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField("Path", result.Path);
                EditorGUILayout.LabelField("Size", FormatSize(result.FileSize));
                EditorGUILayout.LabelField("In Build Scenes", result.InBuildScenes.ToString());
                EditorGUILayout.LabelField("In Dynamic Roots", result.InDynamicRoots.ToString());
                EditorGUILayout.LabelField("Always Included", result.IsAlwaysIncluded.ToString());

                if (result.ReferencingMaterials.Count > 0)
                {
                    EditorGUILayout.LabelField("Referenced By Materials:", EditorStyles.boldLabel);
                    foreach (var mat in result.ReferencingMaterials)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(mat);
                        if (GUILayout.Button("Select", GUILayout.Width(60)))
                        {
                            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Material>(mat);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select Shader", GUILayout.Width(120)))
                {
                    Selection.activeObject = result.Shader;
                }

                if (result.IsUnused && !result.IsAlwaysIncluded && !result.IsBuiltIn)
                {
                    GUI.color = Color.red;
                    if (GUILayout.Button("Delete Asset", GUILayout.Width(100)))
                    {
                        AssetDatabase.DeleteAsset(result.Path);
                        shaderResults.Remove(result);
                        GUIUtility.ExitGUI();
                    }
                    GUI.color = Color.white;
                }

                if (result.IsAlwaysIncluded)
                {
                    if (GUILayout.Button("Remove from Always Included", GUILayout.Width(200)))
                    {
                        RemoveFromAlwaysIncluded(result.Shader);
                        result.IsAlwaysIncluded = false;
                    }
                }

                var config = ShaderOptimizationConfig.Instance;
                if (!config.ShadersToStrip.Contains(result.Name))
                {
                    if (GUILayout.Button("Mark for Auto-Stripping", GUILayout.Width(180)))
                    {
                        config.ShadersToStrip.Add(result.Name);
                        config.SaveConfig();
                    }
                }
                else
                {
                    if (GUILayout.Button("Unmark Auto-Stripping", GUILayout.Width(180)))
                    {
                        config.ShadersToStrip.Remove(result.Name);
                        config.SaveConfig();
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndVertical();
        }

        private bool FilterPredicate(ShaderAnalysisResult r)
        {
            if (!string.IsNullOrEmpty(searchQuery) && r.Name.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            return currentFilter switch
            {
                ShaderFilter.Unused => r.IsUnused,
                ShaderFilter.UsedOutsideScenes => r.IsUsedOnlyOutsideScenes,
                ShaderFilter.UsedInScenes => r.InBuildScenes,
                ShaderFilter.AlwaysIncluded => r.IsAlwaysIncluded,
                _ => true
            };
        }

        private void MarkFilteredForStripping(bool mark)
        {
            var config = ShaderOptimizationConfig.Instance;
            var filtered = shaderResults.Where(FilterPredicate).ToList();
            bool changed = false;

            foreach (var r in filtered)
            {
                if (string.IsNullOrEmpty(r.Name)) continue;

                if (mark)
                {
                    if (!config.ShadersToStrip.Contains(r.Name))
                    {
                        config.ShadersToStrip.Add(r.Name);
                        changed = true;
                    }
                }
                else
                {
                    if (config.ShadersToStrip.Contains(r.Name))
                    {
                        config.ShadersToStrip.Remove(r.Name);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                config.SaveConfig();
                Repaint();
            }
        }

        private void RunAnalysis()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Shader Analysis", "Finding Shaders...", 0.1f);

                var allShaders = AssetDatabase.FindAssets("t:Shader")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<Shader>)
                    .Where(s => s != null)
                    .Distinct()
                    .ToList();

                EditorUtility.DisplayProgressBar("Shader Analysis", "Finding Materials...", 0.2f);
                var allMaterials = AssetDatabase.FindAssets("t:Material")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToList();

                EditorUtility.DisplayProgressBar("Shader Analysis", "Collecting Build Scene Dependencies...", 0.3f);
                var buildScenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                var buildDependencies = AssetDatabase.GetDependencies(buildScenes, true);

                EditorUtility.DisplayProgressBar("Shader Analysis", "Collecting Resources Dependencies...", 0.4f);
                var resourceFolders = AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/") && AssetDatabase.IsValidFolder(p)).ToArray();
                var resourceAssets = AssetDatabase.FindAssets("", resourceFolders).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                var resourceDependencies = AssetDatabase.GetDependencies(resourceAssets, true);

                EditorUtility.DisplayProgressBar("Shader Analysis", "Collecting Dynamic Dependencies...", 0.5f);
                var addressableDependencies = GetAddressablesDependencies();
                var bundleDependencies = GetAssetBundleDependencies();

                var customAssets = customScanRoots.Where(r => r != null).Select(AssetDatabase.GetAssetPath)
                    .SelectMany(p => AssetDatabase.IsValidFolder(p) ? AssetDatabase.FindAssets("", new[] { p }).Select(AssetDatabase.GUIDToAssetPath) : new[] { p })
                    .ToArray();
                var customDependencies = AssetDatabase.GetDependencies(customAssets, true);

                var allDynamicDependencies = resourceDependencies
                    .Concat(addressableDependencies)
                    .Concat(bundleDependencies)
                    .Concat(customDependencies)
                    .Distinct()
                    .ToArray();

                EditorUtility.DisplayProgressBar("Shader Analysis", "Analyzing Always Included Shaders...", 0.6f);
                var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset").FirstOrDefault();
                var alwaysIncludedShaders = new HashSet<Shader>();
                if (graphicsSettings != null)
                {
                    var serializedGraphics = new SerializedObject(graphicsSettings);
                    var alwaysIncludedProperty = serializedGraphics.FindProperty("m_AlwaysIncludedShaders");
                    for (int i = 0; i < alwaysIncludedProperty.arraySize; i++)
                    {
                        var shaderRef = alwaysIncludedProperty.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                        if (shaderRef != null) alwaysIncludedShaders.Add(shaderRef);
                    }
                }

                EditorUtility.DisplayProgressBar("Shader Analysis", "Mapping Materials to Shaders...", 0.7f);
                var materialToShader = new Dictionary<string, Shader>();
                foreach (var matPath in allMaterials)
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (mat != null && mat.shader != null)
                    {
                        materialToShader[matPath] = mat.shader;
                    }
                }

                EditorUtility.DisplayProgressBar("Shader Analysis", "Categorizing Shaders...", 0.8f);
                var buildShaders = new HashSet<Shader>(buildDependencies.Where(p => p.EndsWith(".shader")).Select(AssetDatabase.LoadAssetAtPath<Shader>).Where(s => s != null));
                var dynamicShaders = new HashSet<Shader>(allDynamicDependencies.Where(p => p.EndsWith(".shader")).Select(AssetDatabase.LoadAssetAtPath<Shader>).Where(s => s != null));

                foreach (var matPath in buildDependencies.Where(p => p.EndsWith(".mat")))
                {
                    if (materialToShader.TryGetValue(matPath, out var shader)) buildShaders.Add(shader);
                }

                foreach (var matPath in allDynamicDependencies.Where(p => p.EndsWith(".mat")))
                {
                    if (materialToShader.TryGetValue(matPath, out var shader)) dynamicShaders.Add(shader);
                }

                EditorUtility.DisplayProgressBar("Shader Analysis", "Finalizing Results...", 0.9f);
                shaderResults.Clear();

                foreach (var shader in allShaders)
                {
                    var path = AssetDatabase.GetAssetPath(shader);
                    var fileInfo = new FileInfo(path);

                    var result = new ShaderAnalysisResult
                    {
                        Shader = shader,
                        Name = shader.name,
                        Path = path,
                        InBuildScenes = buildShaders.Contains(shader),
                        InDynamicRoots = dynamicShaders.Contains(shader),
                        IsAlwaysIncluded = alwaysIncludedShaders.Contains(shader),
                        FileSize = fileInfo.Exists ? fileInfo.Length : 0
                    };

                    foreach (var kvp in materialToShader)
                    {
                        if (kvp.Value == shader)
                        {
                            if (buildDependencies.Contains(kvp.Key) || allDynamicDependencies.Contains(kvp.Key) || result.IsUnused)
                            {
                                result.ReferencingMaterials.Add(kvp.Key);
                            }
                        }
                    }

                    shaderResults.Add(result);
                }

                shaderResults = shaderResults.OrderByDescending(r => r.FileSize).ToList();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private string[] GetAddressablesDependencies()
        {
            if (!AssetDatabase.IsValidFolder("Assets/AddressableAssetsData/AssetGroups")) return new string[0];

            var groupAssets = AssetDatabase.FindAssets("", new[] { "Assets/AddressableAssetsData/AssetGroups" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();

            return AssetDatabase.GetDependencies(groupAssets, true);
        }

        private string[] GetAssetBundleDependencies()
        {
            var bundleNames = AssetDatabase.GetAllAssetBundleNames();
            if (bundleNames == null || bundleNames.Length == 0) return new string[0];

            var bundleAssets = new List<string>();
            foreach (var bundle in bundleNames)
            {
                bundleAssets.AddRange(AssetDatabase.GetAssetPathsFromAssetBundle(bundle));
            }

            return AssetDatabase.GetDependencies(bundleAssets.ToArray(), true);
        }

        private void RemoveFromAlwaysIncluded(Shader shader)
        {
            var graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
            var serializedObject = new SerializedObject(graphicsSettings);
            var arrayProp = serializedObject.FindProperty("m_AlwaysIncludedShaders");

            for (int i = arrayProp.arraySize - 1; i >= 0; i--)
            {
                if (arrayProp.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    arrayProp.DeleteArrayElementAtIndex(i);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DeleteAllUnused()
        {
            if (EditorUtility.DisplayDialog("Confirm Delete", "Permanently delete all unreferenced shaders?", "Yes", "Cancel"))
            {
                var unused = shaderResults.Where(r => r.IsUnused && !r.IsAlwaysIncluded && !r.IsBuiltIn).ToList();
                foreach (var u in unused)
                {
                    AssetDatabase.DeleteAsset(u.Path);
                }
                RunAnalysis();
            }
        }

        private string GetStatusString(ShaderAnalysisResult result)
        {
            if (result.IsUnused) return "Unused (Safe to Strip/Delete)";
            if (result.IsUsedOnlyOutsideScenes) return "Used ONLY Outside Scenes (Dynamic/Always Included)";
            if (result.InBuildScenes) return "Used In Build Scenes";
            return "Unknown";
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double len = bytes;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}