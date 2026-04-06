using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CleanRenderPipeline.InvertHull.Editor
{
    /// <summary>
    /// SmoothNormalBaker v2
    /// 
    /// Key features:
    ///   - DEDUP: Groups renderers by shared mesh. Bakes ONCE per unique mesh,
    ///     then assigns the baked result to all renderers sharing that mesh.
    ///     500 trees using 1 mesh → 1 bake, 500 assignments.
    ///   - IGNORE INACTIVE: Option to skip inactive GameObjects.
    ///     Inactive objects won't get baked meshes, so when dragged to
    ///     inactive they revert to original (no outline smooth normal).
    ///   - SCAN PREVIEW: Shows grouped list of unique meshes + how many
    ///     renderers share each one before baking.
    /// </summary>
    public class SmoothNormalBaker : EditorWindow
    {
        // ================================================================
        // Scan result: one entry per unique source mesh
        // ================================================================
        private class MeshGroup
        {
            public Mesh sourceMesh;
            public string meshName;
            public string assetPath;       // Where the source mesh lives
            public int vertexCount;
            public List<RendererRef> renderers = new List<RendererRef>();
            public bool selected = true;
            public bool alreadyBaked;      // tangents look like they have smooth normals
        }

        private class RendererRef
        {
            public Renderer renderer;
            public MeshFilter meshFilter;              // null if SkinnedMeshRenderer
            public SkinnedMeshRenderer skinnedRenderer; // null if MeshFilter
            public string objectPath;
            public bool isActive;
        }

        // ================================================================
        // State
        // ================================================================
        private List<MeshGroup> _groups = new List<MeshGroup>();
        private bool _hasScanned;
        private Vector2 _scroll;

        // Settings
        private float _weldThreshold = 0.0001f;
        private bool _saveAsNewAsset = true;
        private string _savePath = "Assets/BakedMeshes";
        private bool _ignoreInactive = true;
        private bool _autoEnableKeyword = true;

        // Foldout per group
        private HashSet<int> _expandedGroups = new HashSet<int>();

        [MenuItem("Tools/CleanRender/Bake Smooth Normals")]
        public static void ShowWindow()
        {
            var w = GetWindow<SmoothNormalBaker>("Smooth Normal Baker");
            w.minSize = new Vector2(480, 500);
        }

        // ================================================================
        // GUI
        // ================================================================
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawHeader();
            DrawSettings();
            DrawScanButton();

            if (_hasScanned)
            {
                DrawScanStats();
                DrawSelectionBar();
                DrawGroupList();
                DrawActionButtons();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Smooth Normal Baker", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Bake smooth normals v\u00e0o tangent channel cho outline m\u01b0\u1ee3t.\n\n" +
                "Scan Scene \u2192 nh\u00f3m theo mesh chung \u2192 bake 1 l\u1ea7n/mesh \u2192 g\u00e1n cho t\u1ea5t c\u1ea3.\n" +
                "500 c\u00e2y d\u00f9ng 1 mesh = 1 l\u1ea7n bake, 500 l\u1ea7n assign.",
                MessageType.Info);
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Settings", EditorStyles.miniBoldLabel);

            _weldThreshold = EditorGUILayout.FloatField(
                new GUIContent("Weld Threshold",
                    "Vertex g\u1ea7n h\u01a1n kho\u1ea3ng n\u00e0y \u0111\u01b0\u1ee3c coi l\u00e0 c\u00f9ng v\u1ecb tr\u00ed."),
                _weldThreshold);
            _weldThreshold = Mathf.Max(0.000001f, _weldThreshold);

            _ignoreInactive = EditorGUILayout.Toggle(
                new GUIContent("Ignore Inactive Objects",
                    "Khi b\u1eadt: object inactive s\u1ebd kh\u00f4ng \u0111\u01b0\u1ee3c bake.\n" +
                    "K\u00e9o object v\u00e0o inactive = tr\u1edf v\u1ec1 mesh g\u1ed1c (kh\u00f4ng c\u00f3 smooth normal)."),
                _ignoreInactive);

            _saveAsNewAsset = EditorGUILayout.Toggle(
                new GUIContent("Save as New Asset",
                    "T\u1ea1o mesh m\u1edbi (non-destructive). T\u1eaft = s\u1eeda tangent in-place."),
                _saveAsNewAsset);

            if (_saveAsNewAsset)
            {
                EditorGUILayout.BeginHorizontal();
                _savePath = EditorGUILayout.TextField("Save Path", _savePath);
                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string p = EditorUtility.OpenFolderPanel("Save Baked Meshes", "Assets", "BakedMeshes");
                    if (!string.IsNullOrEmpty(p) && p.StartsWith(Application.dataPath))
                        _savePath = "Assets" + p.Substring(Application.dataPath.Length);
                }
                EditorGUILayout.EndHorizontal();
            }

            _autoEnableKeyword = EditorGUILayout.Toggle(
                new GUIContent("Auto Enable Keyword",
                    "T\u1ef1 \u0111\u1ed9ng b\u1eadt _OUTLINE_SMOOTH_NORMAL tr\u00ean material sau khi bake."),
                _autoEnableKeyword);
        }

        private void DrawScanButton()
        {
            EditorGUILayout.Space(8);
            GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
            if (GUILayout.Button("\u26A1 Scan Scene Meshes", GUILayout.Height(30)))
                PerformScan();
            GUI.backgroundColor = Color.white;
        }

        private void DrawScanStats()
        {
            int uniqueMeshes = _groups.Count;
            int totalRenderers = _groups.Sum(g => g.renderers.Count);
            int selectedMeshes = _groups.Count(g => g.selected);
            int alreadyBaked = _groups.Count(g => g.alreadyBaked);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label($"Unique meshes: {uniqueMeshes}", EditorStyles.miniLabel);
            GUILayout.Label($"Renderers: {totalRenderers}", EditorStyles.miniLabel);
            GUILayout.Label($"Selected: {selectedMeshes}", EditorStyles.miniLabel);
            GUILayout.Label($"Already baked: {alreadyBaked}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSelectionBar()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
                foreach (var g in _groups) g.selected = true;
            if (GUILayout.Button("None", EditorStyles.miniButtonMid, GUILayout.Width(45)))
                foreach (var g in _groups) g.selected = false;
            if (GUILayout.Button("Not Baked", EditorStyles.miniButtonRight, GUILayout.Width(75)))
            {
                foreach (var g in _groups) g.selected = !g.alreadyBaked;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroupList()
        {
            EditorGUILayout.Space(4);

            float listH = Mathf.Clamp(_groups.Count * 30f + 10f, 80f, 300f);
            var listScroll = EditorGUILayout.BeginScrollView(
                Vector2.zero, GUILayout.Height(listH));

            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i];
                DrawGroupEntry(g, i);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGroupEntry(MeshGroup g, int index)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            // Checkbox
            g.selected = EditorGUILayout.Toggle(g.selected, GUILayout.Width(16));

            // Status
            string icon = g.alreadyBaked ? "\u2714" : "\u25CB";
            EditorGUILayout.LabelField(icon, GUILayout.Width(18));

            // Mesh name (clickable to ping)
            string label = $"{g.meshName}  ({g.vertexCount} verts)";
            if (GUILayout.Button(label, EditorStyles.linkLabel, GUILayout.MinWidth(150)))
            {
                if (g.sourceMesh != null)
                    EditorGUIUtility.PingObject(g.sourceMesh);
            }

            // Renderer count
            int activeCount = g.renderers.Count(r => r.isActive);
            int inactiveCount = g.renderers.Count - activeCount;
            string countLabel = inactiveCount > 0
                ? $"{activeCount} active + {inactiveCount} inactive"
                : $"{g.renderers.Count} renderers";
            EditorGUILayout.LabelField(countLabel, EditorStyles.miniLabel, GUILayout.Width(140));

            // Expand toggle to show renderer list
            bool expanded = _expandedGroups.Contains(index);
            if (GUILayout.Button(expanded ? "\u25BC" : "\u25B6", EditorStyles.miniButton, GUILayout.Width(22)))
            {
                if (expanded) _expandedGroups.Remove(index);
                else _expandedGroups.Add(index);
            }

            EditorGUILayout.EndHorizontal();

            // Expanded: show renderer list
            if (_expandedGroups.Contains(index))
            {
                EditorGUI.indentLevel += 2;
                foreach (var r in g.renderers)
                {
                    EditorGUILayout.BeginHorizontal();
                    string activeIcon = r.isActive ? "" : " [inactive]";
                    if (GUILayout.Button($"{r.objectPath}{activeIcon}", EditorStyles.miniLabel))
                    {
                        if (r.renderer != null)
                        {
                            EditorGUIUtility.PingObject(r.renderer.gameObject);
                            Selection.activeGameObject = r.renderer.gameObject;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel -= 2;
            }
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.Space(6);

            int selectedCount = _groups.Count(g => g.selected);
            int totalRenderers = _groups.Where(g => g.selected).Sum(g => g.renderers.Count);

            GUI.backgroundColor = selectedCount > 0 ? new Color(0.35f, 0.75f, 1f) : Color.gray;
            EditorGUI.BeginDisabledGroup(selectedCount == 0);

            string btnLabel = $"Bake ({selectedCount} meshes \u2192 {totalRenderers} renderers)";
            if (GUILayout.Button(btnLabel, GUILayout.Height(32)))
            {
                PerformBake();
            }

            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            // Material keyword buttons
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable Smooth Normal Keyword (All InvertHull materials)"))
                ToggleKeywordAllScene(true);
            if (GUILayout.Button("Disable", GUILayout.Width(65)))
                ToggleKeywordAllScene(false);
            EditorGUILayout.EndHorizontal();
        }

        // ================================================================
        // Scan: find all renderers, group by shared mesh
        // ================================================================
        private void PerformScan()
        {
            _groups.Clear();
            _expandedGroups.Clear();

            var allRenderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Group by source mesh instance ID
            var meshToGroup = new Dictionary<int, MeshGroup>();

            foreach (var renderer in allRenderers)
            {
                if (renderer == null) continue;

                // Check if it uses an InvertHull shader
                bool usesIH = false;
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader.name.Contains("InvertHull"))
                    { usesIH = true; break; }
                }
                if (!usesIH) continue;

                // Get mesh
                Mesh mesh = null;
                MeshFilter mf = renderer.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = renderer as SkinnedMeshRenderer;
                if (mf != null) mesh = mf.sharedMesh;
                else if (smr != null) mesh = smr.sharedMesh;
                if (mesh == null) continue;

                int meshID = mesh.GetInstanceID();

                if (!meshToGroup.TryGetValue(meshID, out var group))
                {
                    group = new MeshGroup
                    {
                        sourceMesh = mesh,
                        meshName = mesh.name,
                        assetPath = AssetDatabase.GetAssetPath(mesh),
                        vertexCount = mesh.vertexCount,
                        alreadyBaked = HasSmoothNormalsInTangent(mesh)
                    };
                    meshToGroup[meshID] = group;
                }

                bool isActive = renderer.gameObject.activeInHierarchy;

                group.renderers.Add(new RendererRef
                {
                    renderer = renderer,
                    meshFilter = mf,
                    skinnedRenderer = smr,
                    objectPath = HierarchyPath(renderer.gameObject),
                    isActive = isActive
                });
            }

            _groups = meshToGroup.Values
                .OrderByDescending(g => g.renderers.Count)
                .ToList();

            _hasScanned = true;
            Repaint();

            int total = _groups.Sum(g => g.renderers.Count);
            Debug.Log($"[SmoothNormalBaker] Scanned: {_groups.Count} unique meshes, {total} renderers.");
        }

        /// <summary>
        /// Heuristic check: if tangent data exists and looks like normals
        /// (normalized vectors with w close to 1), likely already baked.
        /// </summary>
        private static bool HasSmoothNormalsInTangent(Mesh mesh)
        {
            var tangents = mesh.tangents;
            if (tangents == null || tangents.Length == 0) return false;

            var normals = mesh.normals;
            if (normals == null || normals.Length == 0) return false;

            // Sample a few tangents: if they differ from normals and are normalized,
            // probably smooth normals. If they match normals exactly → not baked.
            // If they look like standard tangent (perpendicular to normal) → not baked.
            int sampleCount = Mathf.Min(20, tangents.Length);
            int step = Mathf.Max(1, tangents.Length / sampleCount);
            int matchCount = 0;

            for (int i = 0; i < tangents.Length && matchCount < sampleCount; i += step)
            {
                Vector3 t = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                Vector3 n = normals[i];

                float lenSq = t.sqrMagnitude;
                if (lenSq < 0.5f || lenSq > 1.5f) return false; // not normalized

                float dot = Vector3.Dot(t.normalized, n.normalized);
                // Standard tangent: perpendicular to normal (dot ≈ 0)
                // Smooth normal baked: roughly parallel to normal but averaged (dot > 0.3 typically)
                // If most samples have |dot| > 0.3, likely baked
                if (Mathf.Abs(dot) > 0.3f) matchCount++;
            }

            return matchCount >= sampleCount * 0.6f;
        }

        // ================================================================
        // Bake: one bake per unique mesh, assign to all renderers
        // ================================================================
        private void PerformBake()
        {
            var toBake = _groups.Where(g => g.selected).ToList();
            if (toBake.Count == 0) return;

            Undo.SetCurrentGroupName("Bake Smooth Normals");
            int undoGroup = Undo.GetCurrentGroup();

            if (_saveAsNewAsset && !System.IO.Directory.Exists(_savePath))
                System.IO.Directory.CreateDirectory(_savePath);

            int bakedMeshes = 0;
            int assignedRenderers = 0;

            foreach (var group in toBake)
            {
                if (group.sourceMesh == null) continue;

                // --- BAKE ONCE for this mesh ---
                Mesh bakedMesh = BakeSmoothNormalsToTangent(group.sourceMesh, _weldThreshold);
                if (bakedMesh == null) continue;

                if (_saveAsNewAsset)
                {
                    string assetName = $"{group.sourceMesh.name}_SN";
                    string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{_savePath}/{assetName}.asset");
                    AssetDatabase.CreateAsset(bakedMesh, fullPath);
                }

                bakedMeshes++;

                // --- ASSIGN to all renderers sharing this mesh ---
                foreach (var rr in group.renderers)
                {
                    if (rr.renderer == null) continue;

                    // Skip inactive if option is on
                    if (_ignoreInactive && !rr.isActive) continue;

                    if (_saveAsNewAsset)
                    {
                        // Assign baked mesh copy
                        if (rr.meshFilter != null)
                        {
                            Undo.RecordObject(rr.meshFilter, "Assign baked mesh");
                            rr.meshFilter.sharedMesh = bakedMesh;
                        }
                        else if (rr.skinnedRenderer != null)
                        {
                            Undo.RecordObject(rr.skinnedRenderer, "Assign baked mesh");
                            rr.skinnedRenderer.sharedMesh = bakedMesh;
                        }
                    }
                    else
                    {
                        // In-place: just copy tangents to source (affects all users)
                        Undo.RecordObject(group.sourceMesh, "Bake tangents in-place");
                        group.sourceMesh.tangents = bakedMesh.tangents;
                        EditorUtility.SetDirty(group.sourceMesh);
                        // Only need to do this once for in-place
                        break;
                    }

                    // Auto enable keyword on materials
                    if (_autoEnableKeyword)
                    {
                        foreach (var mat in rr.renderer.sharedMaterials)
                        {
                            if (mat == null || !mat.shader.name.Contains("InvertHull")) continue;
                            Undo.RecordObject(mat, "Enable smooth normal");
                            mat.EnableKeyword("_OUTLINE_SMOOTH_NORMAL");
                            mat.SetFloat("_OutlineSmoothNormal", 1f);
                            EditorUtility.SetDirty(mat);
                        }
                    }

                    assignedRenderers++;
                }

                group.alreadyBaked = true;
            }

            Undo.CollapseUndoOperations(undoGroup);

            if (_saveAsNewAsset)
                AssetDatabase.SaveAssets();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[SmoothNormalBaker] Baked {bakedMeshes} unique meshes, assigned to {assignedRenderers} renderers.");

            EditorUtility.DisplayDialog("Bake Complete",
                $"Baked: {bakedMeshes} unique meshes\n" +
                $"Assigned: {assignedRenderers} renderers\n" +
                (_ignoreInactive ? $"(Inactive objects skipped)" : ""),
                "OK");
        }

        // ================================================================
        // Core algorithm: average normals at same position → tangent.xyz
        // ================================================================
        public static Mesh BakeSmoothNormalsToTangent(Mesh source, float weldThreshold)
        {
            if (source == null) return null;

            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            if (vertices == null || normals == null || vertices.Length == 0 || normals.Length == 0)
                return null;

            int vertCount = vertices.Length;
            float invThreshold = 1f / Mathf.Max(weldThreshold, 0.000001f);

            // Spatial hash: group vertices by quantized position
            var spatialHash = new Dictionary<long, List<int>>();
            for (int i = 0; i < vertCount; i++)
            {
                long key = SpatialKey(vertices[i], invThreshold);
                if (!spatialHash.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    spatialHash[key] = list;
                }
                list.Add(i);
            }

            // Compute averaged smooth normals
            Vector3[] smoothNormals = new Vector3[vertCount];
            foreach (var kvp in spatialHash)
            {
                var group = kvp.Value;
                Vector3 avg = Vector3.zero;
                for (int i = 0; i < group.Count; i++)
                    avg += normals[group[i]];
                avg = avg.normalized;
                if (avg.sqrMagnitude < 0.01f)
                    avg = normals[group[0]].normalized;
                for (int i = 0; i < group.Count; i++)
                    smoothNormals[group[i]] = avg;
            }

            // Write to tangent channel
            Vector4[] existingTangents = source.tangents;
            Vector4[] newTangents = new Vector4[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 sn = smoothNormals[i];
                float w = (existingTangents != null && existingTangents.Length > i)
                    ? existingTangents[i].w : 1f;
                newTangents[i] = new Vector4(sn.x, sn.y, sn.z, w);
            }

            Mesh result = Object.Instantiate(source);
            result.name = source.name + "_SmoothNormals";
            result.tangents = newTangents;
            return result;
        }

        private static long SpatialKey(Vector3 pos, float invThreshold)
        {
            int x = Mathf.RoundToInt(pos.x * invThreshold);
            int y = Mathf.RoundToInt(pos.y * invThreshold);
            int z = Mathf.RoundToInt(pos.z * invThreshold);
            unchecked
            {
                long key = (long)(x & 0x1FFFFF);
                key |= (long)(y & 0x1FFFFF) << 21;
                key |= (long)(z & 0x1FFFFF) << 42;
                return key;
            }
        }

        private static string HierarchyPath(GameObject go)
        {
            string p = go.name;
            var t = go.transform.parent;
            while (t != null) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }

        // ================================================================
        // Batch keyword toggle for all InvertHull materials in scene
        // ================================================================
        private void ToggleKeywordAllScene(bool enable)
        {
            var allRenderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;
            var processed = new HashSet<int>();

            foreach (var r in allRenderers)
            {
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null || !mat.shader.name.Contains("InvertHull")) continue;
                    int id = mat.GetInstanceID();
                    if (processed.Contains(id)) continue;
                    processed.Add(id);

                    Undo.RecordObject(mat, "Toggle smooth normals");
                    if (enable)
                    {
                        mat.EnableKeyword("_OUTLINE_SMOOTH_NORMAL");
                        mat.SetFloat("_OutlineSmoothNormal", 1f);
                    }
                    else
                    {
                        mat.DisableKeyword("_OUTLINE_SMOOTH_NORMAL");
                        mat.SetFloat("_OutlineSmoothNormal", 0f);
                    }
                    EditorUtility.SetDirty(mat);
                    changed++;
                }
            }
            Debug.Log($"[SmoothNormal] {(enable ? "Enabled" : "Disabled")} on {changed} materials scene-wide.");
        }
    }
}
