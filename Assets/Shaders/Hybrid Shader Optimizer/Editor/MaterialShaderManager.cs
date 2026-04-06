/*
 * ============================================================================
 *  MATERIAL & SHADER MANAGER v2.0 - Unity Editor Tool
 * ============================================================================
 *  Đặt file này vào folder: Assets/Editor/MaterialShaderManagerV2.cs
 *  Mở tool: Window > Material & Shader Manager v2
 *  Shortcut: Ctrl+Shift+M
 * ============================================================================
 *  5 Panel:
 *    1. Dashboard         - Tổng quan + cảnh báo + thống kê
 *    2. Scene Explorer    - Quản lý Object-centric (tree view)
 *    3. Material Library  - Quản lý Material (list/grid/table)
 *    4. Shader Workshop   - Phân loại & chuyển đổi Shader
 *    5. Batch Operations  - Pipeline thao tác hàng loạt
 * ============================================================================
 */

using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class MaterialShaderManagerV2 : EditorWindow
{
    // ========================================================================
    //  ENUMS
    // ========================================================================

    public enum ShaderRenderCategory
    {
        Opaque,
        Cutout,
        Fade,
        Transparent,
        Additive,
        Multiply,
        UI,
        Unlit,
        Custom,
        Unknown
    }

    public enum PanelTab
    {
        Dashboard = 0,
        SceneExplorer = 1,
        MaterialLibrary = 2,
        ShaderWorkshop = 3,
        BatchOps = 4
    }

    public enum MaterialViewMode
    {
        List = 0,
        Grid = 1,
        Table = 2
    }

    public enum MaterialGroupBy
    {
        None = 0,
        Status = 1,
        ShaderCategory = 2,
        ShaderName = 3,
        AssetPath = 4
    }

    public enum SceneExplorerSort
    {
        ByName = 0,
        ByMaterialCount = 1,
        ByWarningCount = 2
    }

    public enum BatchStepType
    {
        Duplicate,
        ChangeShader,
        SetProperty,
        Rename,
        Replace,
        MoveToFolder
    }

    // ========================================================================
    //  DATA STRUCTURES
    // ========================================================================

    /// <summary>
    /// Thông tin 1 Material trong Scene
    /// </summary>
    [Serializable]
    public class MaterialInfo
    {
        public Material material;
        public string materialName;
        public string shaderName;
        public ShaderRenderCategory category;
        public List<RendererInfo> usedByRenderers = new List<RendererInfo>();
        public bool isDefault;
        public bool isSelected;
        public bool isDuplicated;
        public string assetPath;
        public int renderQueue;

        // Cached property info
        public bool hasMainTex;
        public bool hasNormalMap;
        public bool hasColor;
        public Color mainColor;
        public float metallic;
        public float smoothness;

        public int UsedByObjectCount => usedByRenderers.Select(r => r.gameObject).Distinct().Count();
    }

    /// <summary>
    /// Thông tin 1 Renderer trong Scene
    /// </summary>
    [Serializable]
    public class RendererInfo
    {
        public Renderer renderer;
        public GameObject gameObject;
        public string rendererType; // MeshRenderer, SkinnedMeshRenderer, etc.
        public List<MaterialInfo> materials = new List<MaterialInfo>();
    }

    /// <summary>
    /// Thông tin 1 GameObject có Renderer
    /// </summary>
    [Serializable]
    public class ObjectInfo
    {
        public GameObject gameObject;
        public string name;
        public string hierarchyPath;
        public List<RendererInfo> renderers = new List<RendererInfo>();
        public bool isExpanded;
        public bool isSelected;

        public int TotalMaterialCount => renderers.Sum(r => r.materials.Count);
        public int DefaultMaterialCount => renderers.Sum(r => r.materials.Count(m => m.isDefault));
        public int WarningCount => DefaultMaterialCount + renderers.Sum(r => r.materials.Count(m => m.shaderName == "Hidden/InternalErrorShader"));
        public bool HasWarnings => WarningCount > 0;

        public List<MaterialInfo> AllMaterials => renderers.SelectMany(r => r.materials).Distinct().ToList();
    }

    /// <summary>
    /// Action Log Entry
    /// </summary>
    [Serializable]
    public class ActionLogEntry
    {
        public string timestamp;
        public string description;
        public string details;
        public bool canUndo;

        public ActionLogEntry(string desc, string det = "")
        {
            timestamp = DateTime.Now.ToString("HH:mm:ss");
            description = desc;
            details = det;
            canUndo = true;
        }
    }

    /// <summary>
    /// Batch Pipeline Step
    /// </summary>
    [Serializable]
    public class BatchStep
    {
        public BatchStepType type;
        public bool isEnabled = true;

        // Duplicate params
        public string duplicatePath = "Assets/DuplicatedMaterials";
        public string namingPattern = "{name}_Copy";

        // Change Shader params
        public string targetShaderName = "Standard";
        public int targetShaderIndex = 0;
        public bool preserveProperties = true;

        // Set Property params
        public string propertyName = "_Color";
        public Color colorValue = Color.white;
        public float floatValue = 0f;

        // Rename params
        public string renamePrefix = "";
        public string renameSuffix = "";
        public string renameFindStr = "";
        public string renameReplaceStr = "";

        // Replace params
        public Material replacementMaterial;

        // Move params
        public string movePath = "Assets/Materials";

        public string GetDisplayName()
        {
            switch (type)
            {
                case BatchStepType.Duplicate: return "Duplicate";
                case BatchStepType.ChangeShader: return "Doi Shader → " + targetShaderName;
                case BatchStepType.SetProperty: return "Set " + propertyName;
                case BatchStepType.Rename: return "Rename";
                case BatchStepType.Replace: return "Replace";
                case BatchStepType.MoveToFolder: return "Move → " + movePath;
                default: return type.ToString();
            }
        }
    }

    /// <summary>
    /// Search result item
    /// </summary>
    public class SearchResult
    {
        public enum ResultType { Object, Material, Shader }
        public ResultType type;
        public string name;
        public object data; // ObjectInfo, MaterialInfo, or string (shader name)
    }

    /// <summary>
    /// Warning/Suggestion item for Dashboard
    /// </summary>
    public class WarningItem
    {
        public enum Severity { Warning, Suggestion, OK }
        public Severity severity;
        public string message;
        public int count;
        public Action onAction;
        public string actionLabel;
    }

    /// <summary>
    /// Settings (saved via EditorPrefs)
    /// </summary>
    [Serializable]
    public class ToolSettings
    {
        public string defaultDuplicatePath = "Assets/DuplicatedMaterials";
        public bool autoScanOnOpen = true;
        public bool autoScanOnSceneChange = true;
        public bool scanInactiveObjects = false;
        public int previewSize = 32;
        public MaterialViewMode defaultViewMode = MaterialViewMode.List;
        public bool includeSubScenes = false;
        public bool includePrefabInstances = false;
        public bool showInternalShaders = false;

        private const string PREFS_KEY = "MaterialShaderManagerV2_Settings";

        public void Save()
        {
            EditorPrefs.SetString(PREFS_KEY, JsonUtility.ToJson(this));
        }

        public static ToolSettings Load()
        {
            string json = EditorPrefs.GetString(PREFS_KEY, "");
            if (string.IsNullOrEmpty(json)) return new ToolSettings();
            try { return JsonUtility.FromJson<ToolSettings>(json); }
            catch { return new ToolSettings(); }
        }
    }

    // ========================================================================
    //  FIELDS - Data
    // ========================================================================

    private List<ObjectInfo> allObjects = new List<ObjectInfo>();
    private List<MaterialInfo> allMaterials = new List<MaterialInfo>();
    private Dictionary<ShaderRenderCategory, List<MaterialInfo>> categorizedMaterials
        = new Dictionary<ShaderRenderCategory, List<MaterialInfo>>();
    private Dictionary<string, List<MaterialInfo>> shaderGroups
        = new Dictionary<string, List<MaterialInfo>>();
    private List<ActionLogEntry> actionLog = new List<ActionLogEntry>();
    private List<WarningItem> warnings = new List<WarningItem>();
    private ToolSettings settings;

    // ========================================================================
    //  FIELDS - UI State
    // ========================================================================

    // Global
    private PanelTab currentPanel = PanelTab.Dashboard;
    private Vector2 mainScrollPos;
    private bool hasScannedOnce = false;
    private bool showSettings = false;

    // Search
    private string searchQuery = "";
    private List<SearchResult> searchResults = new List<SearchResult>();
    private bool showSearchResults = false;
    private Vector2 searchScrollPos;

    // Action Log
    private bool actionLogExpanded = false;
    private Vector2 actionLogScrollPos;

    // Panel 1: Dashboard
    private Vector2 dashboardScrollPos;

    // Panel 2: Scene Explorer
    private Vector2 sceneExplorerScrollPos;
    private Vector2 sceneDetailScrollPos;
    private int sceneFilterType = 0; // 0=All
    private int sceneFilterStatus = 0; // 0=All, 1=Has Default, 2=Has Missing
    private ShaderRenderCategory sceneFilterCategory = ShaderRenderCategory.Opaque;
    private bool sceneFilterCategoryEnabled = false;
    private SceneExplorerSort sceneSort = SceneExplorerSort.ByName;
    private ObjectInfo selectedObject = null;
    private readonly string[] rendererTypeFilters = new string[]
    {
        "All", "MeshRenderer", "SkinnedMeshRenderer", "SpriteRenderer",
        "ParticleSystemRenderer", "LineRenderer", "TrailRenderer", "UI (Canvas)"
    };
    private readonly string[] statusFilters = new string[]
    {
        "All", "Has Default Material", "Has Missing Shader"
    };

    // Panel 3: Material Library
    private MaterialViewMode matViewMode = MaterialViewMode.List;
    private MaterialGroupBy matGroupBy = MaterialGroupBy.Status;
    private int matFilterStatus = 0;
    private int matFilterCategory = 0; // 0=All
    private int matFilterShader = 0; // 0=All
    private string matSearchText = "";
    private Vector2 matListScrollPos;
    private Vector2 matDetailScrollPos;
    private MaterialInfo selectedMaterial = null;
    private bool matSelectAll = false;
    private Dictionary<string, bool> matGroupFoldouts = new Dictionary<string, bool>();

    // Panel 4: Shader Workshop
    private Vector2 shaderScrollPos;
    private ShaderRenderCategory? shaderMapFilter = null;
    private Dictionary<string, bool> shaderGroupFoldouts = new Dictionary<string, bool>();
    private int quickConvertSourceIdx = 0;
    private int quickConvertTargetIdx = 0;
    private string quickConvertCustomShader = "";
    private int quickConvertScope = 0; // 0=All, 1=Current category filter

    // Panel 5: Batch Operations
    private Vector2 batchScrollPos;
    private int batchSourceMode = 0; // 0=Selection, 1=Filter, 2=All
    private List<BatchStep> batchPipeline = new List<BatchStep>();
    private List<MaterialInfo> batchFilteredMaterials = new List<MaterialInfo>();
    private bool batchShowPreview = false;
    private int batchFilterStatus = 0;
    private int batchFilterCategory = 0;
    private int batchFilterShader = 0;
    private string batchSearchText = "";
    private bool batchSelectAll = false;

    // Popup state
    private bool showQuickConvertPopup = false;
    private bool showQuickDuplicatePopup = false;
    private MaterialInfo popupTargetMaterial = null;
    private int popupConvertShaderIdx = 0;
    private string popupConvertCustomShader = "";
    private bool popupConvertPreserveProps = true;
    private string popupDuplicateName = "";
    private string popupDuplicatePath = "";
    private bool popupDuplicateAutoAssign = true;

    // Common shader list
    private readonly string[] commonShaders = new string[]
    {
        "Standard",
        "Standard (Specular setup)",
        "Universal Render Pipeline/Lit",
        "Universal Render Pipeline/Simple Lit",
        "Universal Render Pipeline/Unlit",
        "Universal Render Pipeline/Complex Lit",
        "Unlit/Color",
        "Unlit/Texture",
        "Unlit/Transparent",
        "Unlit/Transparent Cutout",
        "Mobile/Diffuse",
        "Mobile/Bumped Diffuse",
        "Particles/Standard Surface",
        "Particles/Standard Unlit",
        "UI/Default",
        "Sprites/Default",
        "-- Nhap Shader Tuy Chinh --"
    };

    // ========================================================================
    //  FIELDS - Styles (lazy init)
    // ========================================================================

    private bool stylesInitialized = false;
    private GUIStyle headerStyle;
    private GUIStyle subHeaderStyle;
    private GUIStyle boxStyle;
    private GUIStyle statCardStyle;
    private GUIStyle statNumberStyle;
    private GUIStyle statLabelStyle;
    private GUIStyle warningBoxStyle;
    private GUIStyle searchFieldStyle;
    private GUIStyle searchResultStyle;
    private GUIStyle treeItemStyle;
    private GUIStyle treeItemSelectedStyle;
    private GUIStyle categoryBadgeStyle;
    private GUIStyle actionLogStyle;
    private GUIStyle linkLabelStyle;
    private GUIStyle miniTagStyle;
    private GUIStyle toolbarSearchStyle;
    private GUIStyle sectionHeaderStyle;
    private GUIStyle gridCardStyle;

    // Category colors
    private readonly Dictionary<ShaderRenderCategory, Color> categoryColors = new Dictionary<ShaderRenderCategory, Color>
    {
        { ShaderRenderCategory.Opaque,      new Color(0.85f, 0.85f, 0.85f) },
        { ShaderRenderCategory.Cutout,      new Color(1f, 0.95f, 0.8f) },
        { ShaderRenderCategory.Fade,        new Color(0.9f, 0.9f, 1f) },
        { ShaderRenderCategory.Transparent, new Color(0.8f, 0.9f, 1f) },
        { ShaderRenderCategory.Additive,    new Color(1f, 1f, 0.8f) },
        { ShaderRenderCategory.Multiply,    new Color(0.9f, 0.85f, 0.95f) },
        { ShaderRenderCategory.UI,          new Color(0.85f, 1f, 0.85f) },
        { ShaderRenderCategory.Unlit,       new Color(1f, 0.9f, 0.85f) },
        { ShaderRenderCategory.Custom,      new Color(0.95f, 0.85f, 0.85f) },
        { ShaderRenderCategory.Unknown,     Color.white }
    };

    private readonly Dictionary<ShaderRenderCategory, Color> categoryTextColors = new Dictionary<ShaderRenderCategory, Color>
    {
        { ShaderRenderCategory.Opaque,      new Color(0.2f, 0.54f, 0.3f) },
        { ShaderRenderCategory.Cutout,      new Color(0.3f, 0.61f, 0.9f) },
        { ShaderRenderCategory.Fade,        new Color(0.3f, 0.61f, 0.9f) },
        { ShaderRenderCategory.Transparent, new Color(0.3f, 0.61f, 0.9f) },
        { ShaderRenderCategory.Additive,    new Color(0.9f, 0.6f, 0.3f) },
        { ShaderRenderCategory.Multiply,    new Color(0.9f, 0.6f, 0.3f) },
        { ShaderRenderCategory.UI,          new Color(0.2f, 0.54f, 0.3f) },
        { ShaderRenderCategory.Unlit,       new Color(0.2f, 0.54f, 0.3f) },
        { ShaderRenderCategory.Custom,      new Color(0.9f, 0.6f, 0.3f) },
        { ShaderRenderCategory.Unknown,     new Color(0.6f, 0.6f, 0.6f) }
    };

    // ========================================================================
    //  MENU & WINDOW INIT
    // ========================================================================

    [MenuItem("Window/Material && Shader Manager v2 %#m")] // Ctrl+Shift+M
    public static void ShowWindow()
    {
        var window = GetWindow<MaterialShaderManagerV2>("Material & Shader Manager v2");
        window.minSize = new Vector2(650, 700);
        window.Show();
    }

    private void OnEnable()
    {
        settings = ToolSettings.Load();
        if (settings.autoScanOnOpen && !hasScannedOnce)
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    ScanScene();
                    hasScannedOnce = true;
                }
            };
        }
    }

    private void OnDisable()
    {
        settings?.Save();
    }

    // ========================================================================
    //  STYLES INITIALIZATION
    // ========================================================================

    private void InitStyles()
    {
        if (stylesInitialized) return;

        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft,
            padding = new RectOffset(8, 0, 6, 6)
        };

        subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            padding = new RectOffset(0, 0, 4, 4)
        };

        boxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(4, 4, 2, 2)
        };

        statCardStyle = new GUIStyle("box")
        {
            padding = new RectOffset(12, 12, 10, 10),
            margin = new RectOffset(4, 4, 4, 4),
            alignment = TextAnchor.MiddleCenter
        };

        statNumberStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 22,
            alignment = TextAnchor.MiddleCenter
        };

        statLabelStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };

        warningBoxStyle = new GUIStyle("box")
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin = new RectOffset(4, 4, 2, 2)
        };

        searchFieldStyle = new GUIStyle(EditorStyles.toolbarSearchField)
        {
            fixedHeight = 22,
            margin = new RectOffset(4, 4, 4, 4)
        };

        searchResultStyle = new GUIStyle("box")
        {
            padding = new RectOffset(8, 8, 4, 4),
            margin = new RectOffset(4, 4, 0, 0)
        };

        treeItemStyle = new GUIStyle(EditorStyles.label)
        {
            padding = new RectOffset(4, 4, 2, 2),
            margin = new RectOffset(0, 0, 0, 0)
        };

        treeItemSelectedStyle = new GUIStyle(treeItemStyle);
        treeItemSelectedStyle.normal.background = MakeTex(2, 2, new Color(0.24f, 0.49f, 0.91f, 0.3f));

        categoryBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(4, 4, 1, 1)
        };

        actionLogStyle = new GUIStyle("box")
        {
            padding = new RectOffset(8, 8, 4, 4),
            margin = new RectOffset(0, 0, 2, 0),
            fixedHeight = 0
        };

        linkLabelStyle = new GUIStyle(EditorStyles.linkLabel)
        {
            wordWrap = false
        };

        miniTagStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            padding = new RectOffset(4, 4, 1, 1),
            fontStyle = FontStyle.Bold,
            fontSize = 9
        };

        sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 11,
            padding = new RectOffset(4, 0, 6, 2)
        };

        gridCardStyle = new GUIStyle("box")
        {
            padding = new RectOffset(6, 6, 6, 6),
            margin = new RectOffset(3, 3, 3, 3),
            alignment = TextAnchor.UpperCenter
        };

        toolbarSearchStyle = new GUIStyle(EditorStyles.toolbarSearchField);

        stylesInitialized = true;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // ========================================================================
    //  MAIN GUI - Framework
    // ========================================================================

    private void OnGUI()
    {
        InitStyles();
        HandleKeyboardShortcuts();

        // ─── TOP BAR ───
        DrawTopBar();

        // ─── SEARCH BAR ───
        DrawSearchBar();

        // ─── TAB BAR ───
        DrawTabBar();

        // ─── MAIN CONTENT ───
        if (showSettings)
        {
            DrawSettingsPanel();
        }
        else
        {
            mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

            switch (currentPanel)
            {
                case PanelTab.Dashboard:       DrawDashboardPanel(); break;
                case PanelTab.SceneExplorer:    DrawSceneExplorerPanel(); break;
                case PanelTab.MaterialLibrary:  DrawMaterialLibraryPanel(); break;
                case PanelTab.ShaderWorkshop:   DrawShaderWorkshopPanel(); break;
                case PanelTab.BatchOps:         DrawBatchOpsPanel(); break;
            }

            EditorGUILayout.EndScrollView();
        }

        // ─── POPUPS (drawn on top) ───
        if (showQuickConvertPopup)  DrawQuickConvertPopup();
        if (showQuickDuplicatePopup) DrawQuickDuplicatePopup();

        // ─── ACTION LOG BAR ───
        DrawActionLogBar();
    }

    // ========================================================================
    //  TOP BAR
    // ========================================================================

    private void DrawTopBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Material & Shader Manager v2.0", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        // Settings button
        if (GUILayout.Button(new GUIContent("Settings", "Cai dat tool"),
            EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            showSettings = !showSettings;
        }

        // Help button
        if (GUILayout.Button(new GUIContent("?", "Huong dan su dung"),
            EditorStyles.toolbarButton, GUILayout.Width(24)))
        {
            ShowHelpDialog();
        }

        EditorGUILayout.EndHorizontal();
    }

    // ========================================================================
    //  SEARCH BAR
    // ========================================================================

    private void DrawSearchBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Scan button
        GUI.backgroundColor = hasScannedOnce ? Color.white : new Color(0.4f, 0.9f, 0.5f);
        if (GUILayout.Button("Quet Scene", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            ScanScene();
        }
        GUI.backgroundColor = Color.white;

        // Search field
        EditorGUI.BeginChangeCheck();
        GUI.SetNextControlName("GlobalSearch");
        searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);
        if (EditorGUI.EndChangeCheck())
        {
            if (!string.IsNullOrEmpty(searchQuery) && searchQuery.Length >= 2)
            {
                PerformSearch(searchQuery);
                showSearchResults = true;
            }
            else
            {
                showSearchResults = false;
                searchResults.Clear();
            }
        }

        // Clear search
        if (!string.IsNullOrEmpty(searchQuery))
        {
            if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
            {
                searchQuery = "";
                showSearchResults = false;
                searchResults.Clear();
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndHorizontal();

        // Search results dropdown
        if (showSearchResults && searchResults.Count > 0)
        {
            DrawSearchResults();
        }
    }

    private void DrawSearchResults()
    {
        Rect searchRect = GUILayoutUtility.GetLastRect();

        EditorGUILayout.BeginVertical(boxStyle);

        var objectResults = searchResults.Where(r => r.type == SearchResult.ResultType.Object).ToList();
        var materialResults = searchResults.Where(r => r.type == SearchResult.ResultType.Material).ToList();
        var shaderResults = searchResults.Where(r => r.type == SearchResult.ResultType.Shader).ToList();

        searchScrollPos = EditorGUILayout.BeginScrollView(searchScrollPos, GUILayout.MaxHeight(200));

        if (objectResults.Count > 0)
        {
            EditorGUILayout.LabelField($"Objects ({objectResults.Count})", EditorStyles.miniLabel);
            foreach (var r in objectResults.Take(5))
            {
                if (GUILayout.Button("  " + r.name, EditorStyles.linkLabel))
                {
                    selectedObject = r.data as ObjectInfo;
                    currentPanel = PanelTab.SceneExplorer;
                    showSearchResults = false;
                    if (selectedObject != null)
                    {
                        EditorGUIUtility.PingObject(selectedObject.gameObject);
                        Selection.activeGameObject = selectedObject.gameObject;
                    }
                }
            }
        }

        if (materialResults.Count > 0)
        {
            EditorGUILayout.LabelField($"Materials ({materialResults.Count})", EditorStyles.miniLabel);
            foreach (var r in materialResults.Take(5))
            {
                if (GUILayout.Button("  " + r.name, EditorStyles.linkLabel))
                {
                    selectedMaterial = r.data as MaterialInfo;
                    currentPanel = PanelTab.MaterialLibrary;
                    showSearchResults = false;
                    if (selectedMaterial?.material != null)
                        EditorGUIUtility.PingObject(selectedMaterial.material);
                }
            }
        }

        if (shaderResults.Count > 0)
        {
            EditorGUILayout.LabelField($"Shaders ({shaderResults.Count})", EditorStyles.miniLabel);
            foreach (var r in shaderResults.Take(5))
            {
                if (GUILayout.Button("  " + r.name, EditorStyles.linkLabel))
                {
                    currentPanel = PanelTab.ShaderWorkshop;
                    showSearchResults = false;
                }
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ========================================================================
    //  TAB BAR
    // ========================================================================

    private void DrawTabBar()
    {
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        string[] tabLabels = new string[]
        {
            "Dashboard",
            "Scene Explorer",
            "Material Library",
            "Shader Workshop",
            "Batch Ops"
        };

        int defaultCount = allMaterials.Count(m => m.isDefault);

        for (int i = 0; i < tabLabels.Length; i++)
        {
            string label = tabLabels[i];

            // Add badge count
            if (i == 0 && warnings.Count(w => w.severity == WarningItem.Severity.Warning) > 0)
                label += $" ({warnings.Count(w => w.severity == WarningItem.Severity.Warning)})";
            else if (i == 2 && allMaterials.Count > 0)
                label += $" ({allMaterials.Count})";

            bool isActive = (int)currentPanel == i;
            GUI.backgroundColor = isActive ? new Color(0.35f, 0.55f, 0.85f) : Color.white;

            if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Height(24)))
            {
                currentPanel = (PanelTab)i;
                showSettings = false;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Separator line
        Rect sep = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(sep, new Color(0.3f, 0.3f, 0.3f));

        EditorGUILayout.Space(2);
    }

    // ========================================================================
    //  ACTION LOG BAR
    // ========================================================================

    private void DrawActionLogBar()
    {
        EditorGUILayout.Space(2);
        Rect sep = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(sep, new Color(0.3f, 0.3f, 0.3f));

        EditorGUILayout.BeginVertical(actionLogStyle);

        EditorGUILayout.BeginHorizontal();

        // Latest action
        if (actionLog.Count > 0)
        {
            var latest = actionLog[actionLog.Count - 1];
            GUILayout.Label($"[{latest.timestamp}] {latest.description}", EditorStyles.miniLabel);
        }
        else
        {
            GUILayout.Label("Action Log: Chua co thao tac nao", EditorStyles.miniLabel);
        }

        GUILayout.FlexibleSpace();

        // Expand/collapse
        string expandLabel = actionLogExpanded ? "Thu gon" : $"Lich su ({actionLog.Count})";
        if (GUILayout.Button(expandLabel, EditorStyles.miniButton, GUILayout.Width(80)))
        {
            actionLogExpanded = !actionLogExpanded;
        }

        EditorGUILayout.EndHorizontal();

        // Expanded log
        if (actionLogExpanded && actionLog.Count > 0)
        {
            actionLogScrollPos = EditorGUILayout.BeginScrollView(actionLogScrollPos, GUILayout.MaxHeight(120));

            for (int i = actionLog.Count - 1; i >= Math.Max(0, actionLog.Count - 20); i--)
            {
                var entry = actionLog[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"[{entry.timestamp}]", EditorStyles.miniLabel, GUILayout.Width(60));
                GUILayout.Label(entry.description, EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(entry.details))
                {
                    if (GUILayout.Button("Chi tiet", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        EditorUtility.DisplayDialog("Chi tiet thao tac", entry.details, "OK");
                    }
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    // ========================================================================
    //  KEYBOARD SHORTCUTS
    // ========================================================================

    private void HandleKeyboardShortcuts()
    {
        Event e = Event.current;
        if (e == null || e.type != EventType.KeyDown) return;

        // Don't handle if typing in text field
        string focusedControl = GUI.GetNameOfFocusedControl();
        bool inTextField = !string.IsNullOrEmpty(focusedControl) &&
            (focusedControl.Contains("Search") || focusedControl.Contains("Text") || focusedControl.Contains("Field"));

        if (inTextField) return;

        if (e.keyCode == KeyCode.F5)
        {
            ScanScene();
            e.Use();
        }
        else if (e.control && e.keyCode == KeyCode.F)
        {
            GUI.FocusControl("GlobalSearch");
            e.Use();
        }
        else if (e.keyCode == KeyCode.Escape)
        {
            if (showSearchResults) { showSearchResults = false; e.Use(); }
            else if (showQuickConvertPopup) { showQuickConvertPopup = false; e.Use(); }
            else if (showQuickDuplicatePopup) { showQuickDuplicatePopup = false; e.Use(); }
            else if (showSettings) { showSettings = false; e.Use(); }
        }
        else if (!inTextField && e.keyCode >= KeyCode.Alpha1 && e.keyCode <= KeyCode.Alpha5)
        {
            currentPanel = (PanelTab)(e.keyCode - KeyCode.Alpha1);
            showSettings = false;
            e.Use();
        }
    }

    // ========================================================================
    //  PANEL 1: DASHBOARD
    // ========================================================================

    private void DrawDashboardPanel()
    {
        if (!hasScannedOnce || allMaterials.Count == 0)
        {
            DrawEmptyState("Chua co du lieu",
                "Nhan \"Quet Scene\" de bat dau phan tich materials va shaders trong scene hien tai.");
            return;
        }

        // ─── STAT CARDS ───
        EditorGUILayout.BeginHorizontal();

        // Objects card
        DrawStatCard(
            allObjects.Count.ToString(),
            "Objects voi Renderer",
            $"{allObjects.Count} objects, {allObjects.Sum(o => o.renderers.Count)} renderers",
            new Color(0.3f, 0.7f, 0.5f),
            () => { currentPanel = PanelTab.SceneExplorer; }
        );

        // Materials card
        int defaultCount = allMaterials.Count(m => m.isDefault);
        DrawStatCard(
            allMaterials.Count.ToString(),
            "Materials (unique)",
            $"{allMaterials.Count} unique, {defaultCount} default",
            new Color(0.3f, 0.6f, 1f),
            () => { currentPanel = PanelTab.MaterialLibrary; }
        );

        // Shaders card
        DrawStatCard(
            shaderGroups.Count.ToString(),
            "Shaders (unique)",
            $"{shaderGroups.Count} loai shader khac nhau",
            new Color(0.9f, 0.6f, 0.3f),
            () => { currentPanel = PanelTab.ShaderWorkshop; }
        );

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ─── WARNINGS & SUGGESTIONS ───
        DrawDashboardWarnings();

        EditorGUILayout.Space(6);

        // ─── TWO COLUMN: Shader Distribution + Top Materials ───
        EditorGUILayout.BeginHorizontal();

        // Left: Shader Distribution
        EditorGUILayout.BeginVertical(boxStyle, GUILayout.Width(position.width * 0.48f));
        EditorGUILayout.LabelField("Phan bo Shader", sectionHeaderStyle);
        EditorGUILayout.Space(4);
        DrawShaderDistributionChart();
        EditorGUILayout.EndVertical();

        // Right: Top Materials
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Top Materials (theo so object)", sectionHeaderStyle);
        EditorGUILayout.Space(4);
        DrawTopMaterialsList();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ─── RECENT ACTIONS ───
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Thao tac gan day", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        if (actionLog.Count == 0)
        {
            GUILayout.Label("Chua co thao tac nao.", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = actionLog.Count - 1; i >= Math.Max(0, actionLog.Count - 5); i--)
            {
                var entry = actionLog[i];
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"[{entry.timestamp}]", EditorStyles.miniLabel, GUILayout.Width(65));
                GUILayout.Label(entry.description, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawStatCard(string number, string label, string tooltip, Color accentColor, Action onClick)
    {
        EditorGUILayout.BeginVertical(statCardStyle, GUILayout.MinWidth(120), GUILayout.Height(70));

        Color prevColor = GUI.contentColor;
        GUI.contentColor = accentColor;
        EditorGUILayout.LabelField(new GUIContent(number, tooltip), statNumberStyle);
        GUI.contentColor = prevColor;

        EditorGUILayout.LabelField(new GUIContent(label, tooltip), statLabelStyle);

        // Make the whole card clickable
        Rect cardRect = GUILayoutUtility.GetLastRect();
        Rect fullCard = new Rect(cardRect.x, cardRect.y - 40, cardRect.width, 60);
        if (Event.current.type == EventType.MouseDown && fullCard.Contains(Event.current.mousePosition))
        {
            onClick?.Invoke();
            Event.current.Use();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawDashboardWarnings()
    {
        RefreshWarnings();

        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Canh bao & Goi y", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        if (warnings.Count == 0)
        {
            GUILayout.Label("Khong co canh bao nao.", EditorStyles.miniLabel);
        }
        else
        {
            foreach (var w in warnings)
            {
                EditorGUILayout.BeginHorizontal();

                string icon = w.severity == WarningItem.Severity.Warning ? "[!]" :
                              w.severity == WarningItem.Severity.Suggestion ? "[i]" : "[OK]";

                Color textCol = w.severity == WarningItem.Severity.Warning ? new Color(1f, 0.7f, 0.2f) :
                                w.severity == WarningItem.Severity.Suggestion ? new Color(0.4f, 0.7f, 1f) :
                                new Color(0.4f, 0.8f, 0.4f);

                var prevColor = GUI.contentColor;
                GUI.contentColor = textCol;
                GUILayout.Label($"{icon} {w.message}", EditorStyles.miniLabel);
                GUI.contentColor = prevColor;

                GUILayout.FlexibleSpace();

                if (w.onAction != null && !string.IsNullOrEmpty(w.actionLabel))
                {
                    if (GUILayout.Button(w.actionLabel, EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        w.onAction.Invoke();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawShaderDistributionChart()
    {
        if (categorizedMaterials == null || categorizedMaterials.Count == 0) return;

        int maxCount = categorizedMaterials.Values.Max(l => l.Count);
        if (maxCount == 0) maxCount = 1;

        float barMaxWidth = position.width * 0.28f;

        foreach (var kvp in categorizedMaterials.OrderByDescending(k => k.Value.Count))
        {
            if (kvp.Value.Count == 0) continue;

            EditorGUILayout.BeginHorizontal();

            // Category label
            GUILayout.Label($"{GetCategoryIcon(kvp.Key)} {kvp.Key}", EditorStyles.miniLabel, GUILayout.Width(85));

            // Bar
            float barWidth = (kvp.Value.Count / (float)maxCount) * barMaxWidth;
            Rect barRect = GUILayoutUtility.GetRect(barWidth, 14, GUILayout.Width(barWidth));

            Color barColor = categoryColors.ContainsKey(kvp.Key)
                ? categoryColors[kvp.Key] : Color.gray;
            barColor = Color.Lerp(barColor, categoryTextColors.ContainsKey(kvp.Key)
                ? categoryTextColors[kvp.Key] : Color.gray, 0.4f);
            EditorGUI.DrawRect(barRect, barColor);

            // Count
            GUILayout.Label(kvp.Value.Count.ToString(), EditorStyles.miniLabel, GUILayout.Width(30));

            // Clickable → navigate
            Rect rowRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && barRect.Contains(Event.current.mousePosition))
            {
                currentPanel = PanelTab.ShaderWorkshop;
                shaderMapFilter = kvp.Key;
                Event.current.Use();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawTopMaterialsList()
    {
        var topMats = allMaterials
            .OrderByDescending(m => m.UsedByObjectCount)
            .Take(5)
            .ToList();

        for (int i = 0; i < topMats.Count; i++)
        {
            var mat = topMats[i];
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label($"{i + 1}.", EditorStyles.miniLabel, GUILayout.Width(20));

            if (GUILayout.Button(mat.materialName, EditorStyles.linkLabel, GUILayout.Width(160)))
            {
                selectedMaterial = mat;
                currentPanel = PanelTab.MaterialLibrary;
                if (mat.material != null)
                    EditorGUIUtility.PingObject(mat.material);
            }

            GUILayout.Label($"({mat.UsedByObjectCount} obj)", EditorStyles.miniLabel);

            if (mat.isDefault)
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.7f, 0.2f);
                GUILayout.Label("[!] Default", EditorStyles.miniLabel, GUILayout.Width(65));
                GUI.contentColor = prev;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // ========================================================================
    //  PANEL 2: SCENE EXPLORER
    // ========================================================================

    private void DrawSceneExplorerPanel()
    {
        if (!hasScannedOnce || allObjects.Count == 0)
        {
            DrawEmptyState("Chua co du lieu",
                "Nhan \"Quet Scene\" de bat dau. Panel nay hien thi tat ca GameObject co Renderer trong scene.");
            return;
        }

        // ─── FILTER BAR ───
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Type:", EditorStyles.miniLabel, GUILayout.Width(35));
        sceneFilterType = EditorGUILayout.Popup(sceneFilterType, rendererTypeFilters,
            EditorStyles.toolbarPopup, GUILayout.Width(130));

        GUILayout.Label("Status:", EditorStyles.miniLabel, GUILayout.Width(42));
        sceneFilterStatus = EditorGUILayout.Popup(sceneFilterStatus, statusFilters,
            EditorStyles.toolbarPopup, GUILayout.Width(130));

        GUILayout.Label("Sort:", EditorStyles.miniLabel, GUILayout.Width(30));
        sceneSort = (SceneExplorerSort)EditorGUILayout.EnumPopup(sceneSort,
            EditorStyles.toolbarPopup, GUILayout.Width(110));

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Apply filters
        var filteredObjects = GetFilteredObjects();

        // ─── TREE VIEW ───
        sceneExplorerScrollPos = EditorGUILayout.BeginScrollView(sceneExplorerScrollPos,
            GUILayout.MinHeight(selectedObject != null ? 200 : 400));

        // Multi-select header
        EditorGUILayout.BeginHorizontal();
        int selectedCount = filteredObjects.Count(o => o.isSelected);
        GUILayout.Label($"  {filteredObjects.Count} objects | {selectedCount} selected", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();

        if (selectedCount > 0)
        {
            if (GUILayout.Button("Gui sang Batch Ops", EditorStyles.miniButton, GUILayout.Width(120)))
            {
                SendSelectedObjectsToBatchOps(filteredObjects.Where(o => o.isSelected).ToList());
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        foreach (var obj in filteredObjects)
        {
            DrawSceneExplorerObjectRow(obj);
        }

        EditorGUILayout.EndScrollView();

        // ─── DETAIL PANEL ───
        if (selectedObject != null)
        {
            Rect detailSep = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(detailSep, new Color(0.3f, 0.3f, 0.3f));

            DrawSceneExplorerDetail(selectedObject);
        }
    }

    private void DrawSceneExplorerObjectRow(ObjectInfo obj)
    {
        bool isSelected = obj == selectedObject;
        GUIStyle rowStyle = isSelected ? treeItemSelectedStyle : treeItemStyle;

        EditorGUILayout.BeginHorizontal(rowStyle);

        // Checkbox
        obj.isSelected = EditorGUILayout.Toggle(obj.isSelected, GUILayout.Width(18));

        // Expand/collapse arrow
        string arrow = obj.isExpanded ? "v" : ">";
        if (GUILayout.Button(arrow, EditorStyles.miniLabel, GUILayout.Width(14)))
        {
            obj.isExpanded = !obj.isExpanded;
        }

        // Object name (clickable for selection)
        if (GUILayout.Button(obj.name, EditorStyles.label, GUILayout.Width(200)))
        {
            selectedObject = (selectedObject == obj) ? null : obj;
            if (obj.gameObject != null)
            {
                EditorGUIUtility.PingObject(obj.gameObject);
                Selection.activeGameObject = obj.gameObject;
            }
        }

        GUILayout.FlexibleSpace();

        // Material count
        GUILayout.Label($"{obj.TotalMaterialCount} mat", EditorStyles.miniLabel, GUILayout.Width(40));

        // Warning indicator
        if (obj.HasWarnings)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.7f, 0.2f);
            GUILayout.Label($"[!] {obj.WarningCount}", EditorStyles.miniLabel, GUILayout.Width(35));
            GUI.contentColor = prev;
        }

        // Primary category badge
        var primaryCats = obj.AllMaterials.Select(m => m.category).Distinct().ToList();
        if (primaryCats.Count > 0)
        {
            var cat = primaryCats[0];
            var prev2 = GUI.contentColor;
            GUI.contentColor = categoryTextColors.ContainsKey(cat) ? categoryTextColors[cat] : Color.gray;
            GUILayout.Label($"{GetCategoryIcon(cat)}", EditorStyles.miniLabel, GUILayout.Width(25));
            GUI.contentColor = prev2;
        }

        EditorGUILayout.EndHorizontal();

        // ─── EXPANDED: Show renderers and materials ───
        if (obj.isExpanded)
        {
            foreach (var rend in obj.renderers)
            {
                // Renderer row
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(40);
                GUILayout.Label($"|-- [{rend.rendererType}]", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();

                // Material rows
                foreach (var mat in rend.materials)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(60);

                    GUILayout.Label("|-- ", EditorStyles.miniLabel, GUILayout.Width(25));

                    // Material name
                    if (GUILayout.Button(mat.materialName, EditorStyles.linkLabel, GUILayout.Width(150)))
                    {
                        if (mat.material != null)
                        {
                            EditorGUIUtility.PingObject(mat.material);
                            Selection.activeObject = mat.material;
                        }
                    }

                    // Shader name
                    GUILayout.Label(mat.shaderName, EditorStyles.miniLabel, GUILayout.Width(130));

                    // Category badge
                    DrawCategoryBadge(mat.category);

                    // Default warning
                    if (mat.isDefault)
                    {
                        var prev = GUI.contentColor;
                        GUI.contentColor = new Color(1f, 0.7f, 0.2f);
                        GUILayout.Label("[!]", EditorStyles.miniLabel, GUILayout.Width(20));
                        GUI.contentColor = prev;
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.Space(4);
        }
    }

    private void DrawSceneExplorerDetail(ObjectInfo obj)
    {
        EditorGUILayout.LabelField("Thong tin chi tiet", sectionHeaderStyle);

        sceneDetailScrollPos = EditorGUILayout.BeginScrollView(sceneDetailScrollPos, GUILayout.MaxHeight(250));

        EditorGUILayout.BeginVertical(boxStyle);

        // Object info
        EditorGUILayout.LabelField(obj.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Path: {obj.hierarchyPath}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Renderers: {obj.renderers.Count} | Materials: {obj.TotalMaterialCount}" +
            (obj.DefaultMaterialCount > 0 ? $" | Default: {obj.DefaultMaterialCount} [!]" : ""),
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        // Material cards
        foreach (var mat in obj.AllMaterials)
        {
            EditorGUILayout.BeginHorizontal(boxStyle);

            // Preview thumbnail
            if (mat.material != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(40, 40, GUILayout.Width(40));
                Texture preview = AssetPreview.GetAssetPreview(mat.material);
                if (preview == null) preview = AssetPreview.GetMiniThumbnail(mat.material);
                if (preview != null) EditorGUI.DrawPreviewTexture(previewRect, preview);
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(mat.materialName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Shader: {mat.shaderName} ({mat.category})", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Textures: Albedo {(mat.hasMainTex ? "[OK]" : "[X]")}  " +
                $"Normal {(mat.hasNormalMap ? "[OK]" : "[X]")}  |  Queue: {mat.renderQueue}",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            if (mat.isDefault)
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.7f, 0.2f);
                GUILayout.Label("[!] Default", EditorStyles.miniLabel, GUILayout.Width(65));
                GUI.contentColor = prev;
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        // Quick actions
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Quick Actions:", EditorStyles.miniLabel, GUILayout.Width(85));

        if (obj.DefaultMaterialCount > 0)
        {
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button("Duplicate Default Mats", EditorStyles.miniButton))
            {
                var defaults = obj.AllMaterials.Where(m => m.isDefault).ToList();
                DuplicateMaterials(defaults, settings.defaultDuplicatePath);
            }
            GUI.backgroundColor = Color.white;
        }

        if (GUILayout.Button("Doi Shader", EditorStyles.miniButton))
        {
            var mats = obj.AllMaterials.Where(m => !m.isDefault).ToList();
            if (mats.Count > 0)
            {
                foreach (var m in mats) m.isSelected = true;
                currentPanel = PanelTab.BatchOps;
                batchSourceMode = 0;
            }
        }

        if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
        {
            if (obj.gameObject != null)
            {
                EditorGUIUtility.PingObject(obj.gameObject);
                Selection.activeGameObject = obj.gameObject;
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    // ========================================================================
    //  PANEL 3: MATERIAL LIBRARY
    // ========================================================================

    private void DrawMaterialLibraryPanel()
    {
        if (!hasScannedOnce || allMaterials.Count == 0)
        {
            DrawEmptyState("Chua co du lieu",
                "Nhan \"Quet Scene\" de bat dau. Panel nay liet ke tat ca Material unique trong scene.");
            return;
        }

        // ─── VIEW MODE & GROUP BY ───
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("View:", EditorStyles.miniLabel, GUILayout.Width(32));
        matViewMode = (MaterialViewMode)EditorGUILayout.EnumPopup(matViewMode,
            EditorStyles.toolbarPopup, GUILayout.Width(60));

        GUILayout.Label("Group:", EditorStyles.miniLabel, GUILayout.Width(38));
        matGroupBy = (MaterialGroupBy)EditorGUILayout.EnumPopup(matGroupBy,
            EditorStyles.toolbarPopup, GUILayout.Width(90));

        GUILayout.Space(8);

        // Filters
        string[] statusOpts = { "All", "Default", "Custom", "Duplicated" };
        GUILayout.Label("Status:", EditorStyles.miniLabel, GUILayout.Width(40));
        matFilterStatus = EditorGUILayout.Popup(matFilterStatus, statusOpts,
            EditorStyles.toolbarPopup, GUILayout.Width(70));

        string[] catOpts = new string[] { "All" }
            .Concat(Enum.GetNames(typeof(ShaderRenderCategory))).ToArray();
        GUILayout.Label("Cat:", EditorStyles.miniLabel, GUILayout.Width(25));
        matFilterCategory = EditorGUILayout.Popup(matFilterCategory, catOpts,
            EditorStyles.toolbarPopup, GUILayout.Width(80));

        GUILayout.FlexibleSpace();

        // Search
        matSearchText = EditorGUILayout.TextField(matSearchText, EditorStyles.toolbarSearchField,
            GUILayout.Width(140));

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Apply filters
        var filteredMats = GetFilteredMaterials();

        // ─── SELECT ALL & COUNT ───
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        matSelectAll = EditorGUILayout.ToggleLeft($"Select All ({filteredMats.Count})", matSelectAll,
            EditorStyles.miniLabel, GUILayout.Width(130));
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var m in filteredMats) m.isSelected = matSelectAll;
        }

        GUILayout.FlexibleSpace();

        int selCount = filteredMats.Count(m => m.isSelected);
        if (selCount > 0)
        {
            GUILayout.Label($"Selected: {selCount}", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndHorizontal();

        // ─── MAIN CONTENT (split: list + detail) ───
        EditorGUILayout.BeginHorizontal();

        // Left: Material list
        float listWidth = selectedMaterial != null ? position.width * 0.6f : position.width - 20;

        EditorGUILayout.BeginVertical(GUILayout.Width(listWidth));

        matListScrollPos = EditorGUILayout.BeginScrollView(matListScrollPos);

        switch (matViewMode)
        {
            case MaterialViewMode.List:
                DrawMaterialListView(filteredMats);
                break;
            case MaterialViewMode.Grid:
                DrawMaterialGridView(filteredMats);
                break;
            case MaterialViewMode.Table:
                DrawMaterialTableView(filteredMats);
                break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Right: Detail sidebar
        if (selectedMaterial != null)
        {
            EditorGUILayout.BeginVertical(boxStyle, GUILayout.Width(position.width * 0.35f));
            DrawMaterialDetailSidebar(selectedMaterial);
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndHorizontal();

        // ─── SELECTION ACTIONS BAR ───
        if (selCount > 0)
        {
            DrawMaterialSelectionActionsBar(filteredMats.Where(m => m.isSelected).ToList());
        }
    }

    private void DrawMaterialListView(List<MaterialInfo> materials)
    {
        if (matGroupBy == MaterialGroupBy.None)
        {
            foreach (var mat in materials)
                DrawMaterialListRow(mat);
        }
        else
        {
            var groups = GroupMaterials(materials, matGroupBy);
            foreach (var group in groups)
            {
                string groupKey = group.Key;
                if (!matGroupFoldouts.ContainsKey(groupKey))
                    matGroupFoldouts[groupKey] = true;

                // Group header
                EditorGUILayout.BeginHorizontal(boxStyle);
                matGroupFoldouts[groupKey] = EditorGUILayout.Foldout(matGroupFoldouts[groupKey],
                    $"  {groupKey} ({group.Value.Count})", true, EditorStyles.foldoutHeader);
                EditorGUILayout.EndHorizontal();

                if (matGroupFoldouts[groupKey])
                {
                    foreach (var mat in group.Value)
                        DrawMaterialListRow(mat);
                }
            }
        }
    }

    private void DrawMaterialListRow(MaterialInfo mat)
    {
        bool isSelected = mat == selectedMaterial;
        GUIStyle rowStyle = isSelected ? treeItemSelectedStyle : treeItemStyle;

        EditorGUILayout.BeginHorizontal(rowStyle);

        // Checkbox
        mat.isSelected = EditorGUILayout.Toggle(mat.isSelected, GUILayout.Width(18));

        // Preview thumbnail
        if (mat.material != null)
        {
            Rect previewRect = GUILayoutUtility.GetRect(settings.previewSize, settings.previewSize,
                GUILayout.Width(settings.previewSize));
            Texture preview = AssetPreview.GetMiniThumbnail(mat.material);
            if (preview != null) EditorGUI.DrawPreviewTexture(previewRect, preview);
        }

        // Name & info
        EditorGUILayout.BeginVertical();

        // Material name (click to select)
        if (GUILayout.Button(mat.materialName, EditorStyles.linkLabel))
        {
            selectedMaterial = (selectedMaterial == mat) ? null : mat;
            if (mat.material != null) EditorGUIUtility.PingObject(mat.material);
        }

        // Second line: shader + usage
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Shader: {mat.shaderName}", EditorStyles.miniLabel, GUILayout.Width(180));

        DrawCategoryBadge(mat.category);

        GUILayout.Label($" | {mat.UsedByObjectCount} obj", EditorStyles.miniLabel, GUILayout.Width(50));

        if (mat.isDefault)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.7f, 0.2f);
            GUILayout.Label("[!] Default", EditorStyles.miniLabel, GUILayout.Width(65));
            GUI.contentColor = prev;
        }
        else if (mat.isDuplicated)
        {
            var prev = GUI.contentColor;
            GUI.contentColor = new Color(0.3f, 0.8f, 0.4f);
            GUILayout.Label("[OK] Dup", EditorStyles.miniLabel, GUILayout.Width(55));
            GUI.contentColor = prev;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        // Quick action buttons
        if (GUILayout.Button("S", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            OpenQuickConvertPopup(mat);
        }
        if (mat.isDefault && GUILayout.Button("D", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            OpenQuickDuplicatePopup(mat);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMaterialGridView(List<MaterialInfo> materials)
    {
        int gridColumns = Mathf.Max(1, (int)((selectedMaterial != null ? position.width * 0.58f : position.width - 30) / 100));

        int col = 0;
        EditorGUILayout.BeginHorizontal();

        foreach (var mat in materials)
        {
            if (col > 0 && col % gridColumns == 0)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }

            bool isSelected = mat == selectedMaterial;
            GUI.backgroundColor = isSelected ? new Color(0.35f, 0.55f, 0.85f) : Color.white;

            EditorGUILayout.BeginVertical(gridCardStyle, GUILayout.Width(90), GUILayout.Height(100));

            // Checkbox
            mat.isSelected = EditorGUILayout.Toggle(mat.isSelected, GUILayout.Width(18));

            // Preview
            if (mat.material != null)
            {
                Rect previewRect = GUILayoutUtility.GetRect(56, 56, GUILayout.Width(56));
                Texture preview = AssetPreview.GetAssetPreview(mat.material);
                if (preview == null) preview = AssetPreview.GetMiniThumbnail(mat.material);
                if (preview != null)
                    EditorGUI.DrawPreviewTexture(previewRect, preview);

                // Click to select
                if (Event.current.type == EventType.MouseDown && previewRect.Contains(Event.current.mousePosition))
                {
                    selectedMaterial = (selectedMaterial == mat) ? null : mat;
                    Event.current.Use();
                }
            }

            // Name
            string shortName = mat.materialName.Length > 12
                ? mat.materialName.Substring(0, 10) + ".."
                : mat.materialName;
            GUILayout.Label(shortName, EditorStyles.miniLabel);

            // Default badge
            if (mat.isDefault)
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.7f, 0.2f);
                GUILayout.Label("[!]", EditorStyles.miniLabel);
                GUI.contentColor = prev;
            }

            EditorGUILayout.EndVertical();

            GUI.backgroundColor = Color.white;
            col++;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMaterialTableView(List<MaterialInfo> materials)
    {
        // Table header
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("", GUILayout.Width(22)); // checkbox col
        GUILayout.Label("Name", EditorStyles.toolbarButton, GUILayout.Width(150));
        GUILayout.Label("Shader", EditorStyles.toolbarButton, GUILayout.Width(160));
        GUILayout.Label("Category", EditorStyles.toolbarButton, GUILayout.Width(80));
        GUILayout.Label("Objects", EditorStyles.toolbarButton, GUILayout.Width(50));
        GUILayout.Label("Queue", EditorStyles.toolbarButton, GUILayout.Width(45));
        GUILayout.Label("Status", EditorStyles.toolbarButton, GUILayout.Width(65));
        EditorGUILayout.EndHorizontal();

        foreach (var mat in materials)
        {
            bool isSelected = mat == selectedMaterial;
            GUIStyle rowStyle = isSelected ? treeItemSelectedStyle : treeItemStyle;

            EditorGUILayout.BeginHorizontal(rowStyle);

            mat.isSelected = EditorGUILayout.Toggle(mat.isSelected, GUILayout.Width(18));

            if (GUILayout.Button(mat.materialName, EditorStyles.linkLabel, GUILayout.Width(150)))
            {
                selectedMaterial = (selectedMaterial == mat) ? null : mat;
                if (mat.material != null) EditorGUIUtility.PingObject(mat.material);
            }

            GUILayout.Label(mat.shaderName, EditorStyles.miniLabel, GUILayout.Width(160));
            DrawCategoryBadge(mat.category, 80);
            GUILayout.Label(mat.UsedByObjectCount.ToString(), EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label(mat.renderQueue.ToString(), EditorStyles.miniLabel, GUILayout.Width(45));

            if (mat.isDefault)
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(1f, 0.7f, 0.2f);
                GUILayout.Label("[!] Default", EditorStyles.miniLabel, GUILayout.Width(65));
                GUI.contentColor = prev;
            }
            else
            {
                var prev = GUI.contentColor;
                GUI.contentColor = new Color(0.4f, 0.8f, 0.4f);
                GUILayout.Label("[OK]", EditorStyles.miniLabel, GUILayout.Width(65));
                GUI.contentColor = prev;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawMaterialDetailSidebar(MaterialInfo mat)
    {
        matDetailScrollPos = EditorGUILayout.BeginScrollView(matDetailScrollPos);

        // Close button
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            selectedMaterial = null;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
            return;
        }
        EditorGUILayout.EndHorizontal();

        // Large preview
        if (mat.material != null)
        {
            Rect previewRect = GUILayoutUtility.GetRect(100, 100);
            Texture preview = AssetPreview.GetAssetPreview(mat.material);
            if (preview == null) preview = AssetPreview.GetMiniThumbnail(mat.material);
            if (preview != null)
                EditorGUI.DrawPreviewTexture(previewRect, preview, null, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(mat.materialName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Path: {(string.IsNullOrEmpty(mat.assetPath) ? "(built-in)" : mat.assetPath)}",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"Shader: {mat.shaderName}", EditorStyles.miniLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Category:", EditorStyles.miniLabel, GUILayout.Width(60));
        DrawCategoryBadge(mat.category);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Render Queue: {mat.renderQueue}", EditorStyles.miniLabel);

        // Properties section
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Properties", sectionHeaderStyle);

        if (mat.hasColor)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("_Color:", EditorStyles.miniLabel, GUILayout.Width(60));
            Rect colorRect = GUILayoutUtility.GetRect(20, 14, GUILayout.Width(20));
            EditorGUI.DrawRect(colorRect, mat.mainColor);
            GUILayout.Label($"#{ColorUtility.ToHtmlStringRGB(mat.mainColor)}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.LabelField($"_Metallic: {mat.metallic:F2}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"_Smoothness: {mat.smoothness:F2}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"_MainTex: {(mat.hasMainTex ? "[OK]" : "[X] None")}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"_BumpMap: {(mat.hasNormalMap ? "[OK]" : "[X] None")}", EditorStyles.miniLabel);

        // Used by section
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Dung boi ({mat.UsedByObjectCount} objects)", sectionHeaderStyle);

        foreach (var rend in mat.usedByRenderers.Take(10))
        {
            if (rend.gameObject != null)
            {
                if (GUILayout.Button("  " + rend.gameObject.name, EditorStyles.linkLabel))
                {
                    var objInfo = allObjects.FirstOrDefault(o => o.gameObject == rend.gameObject);
                    if (objInfo != null)
                    {
                        selectedObject = objInfo;
                        currentPanel = PanelTab.SceneExplorer;
                    }
                    EditorGUIUtility.PingObject(rend.gameObject);
                }
            }
        }

        if (mat.usedByRenderers.Count > 10)
            GUILayout.Label($"  ... va {mat.usedByRenderers.Count - 10} renderers khac", EditorStyles.miniLabel);

        // Action buttons
        EditorGUILayout.Space(8);

        if (GUILayout.Button("Open in Inspector", EditorStyles.miniButton))
        {
            if (mat.material != null) Selection.activeObject = mat.material;
        }

        if (GUILayout.Button("Ping in Project", EditorStyles.miniButton))
        {
            if (mat.material != null) EditorGUIUtility.PingObject(mat.material);
        }

        if (GUILayout.Button("Select All Users", EditorStyles.miniButton))
        {
            var objects = mat.usedByRenderers.Select(r => r.gameObject).Where(g => g != null).Distinct().ToArray();
            Selection.objects = objects;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMaterialSelectionActionsBar(List<MaterialInfo> selectedMats)
    {
        EditorGUILayout.Space(2);
        Rect sep = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(sep, new Color(0.35f, 0.55f, 0.85f));

        EditorGUILayout.BeginHorizontal(boxStyle);

        GUILayout.Label($"{selectedMats.Count} selected", EditorStyles.miniLabel, GUILayout.Width(75));

        int defaultInSel = selectedMats.Count(m => m.isDefault);

        if (defaultInSel > 0)
        {
            GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
            if (GUILayout.Button($"Duplicate ({defaultInSel})", EditorStyles.miniButton))
            {
                DuplicateMaterials(selectedMats.Where(m => m.isDefault).ToList(), settings.defaultDuplicatePath);
            }
            GUI.backgroundColor = Color.white;
        }

        GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
        if (GUILayout.Button("Doi Shader", EditorStyles.miniButton))
        {
            currentPanel = PanelTab.BatchOps;
            batchSourceMode = 0;
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("Gui Batch Ops", EditorStyles.miniButton))
        {
            currentPanel = PanelTab.BatchOps;
            batchSourceMode = 0;
        }

        GUILayout.FlexibleSpace();

        // Deselect all
        if (GUILayout.Button("Bo chon", EditorStyles.miniButton, GUILayout.Width(55)))
        {
            foreach (var m in selectedMats) m.isSelected = false;
            matSelectAll = false;
        }

        EditorGUILayout.EndHorizontal();
    }

    // ========================================================================
    //  PANEL 4: SHADER WORKSHOP
    // ========================================================================

    private void DrawShaderWorkshopPanel()
    {
        if (!hasScannedOnce || allMaterials.Count == 0)
        {
            DrawEmptyState("Chua co du lieu",
                "Nhan \"Quet Scene\" de bat dau. Panel nay phan loai va chuyen doi Shader.");
            return;
        }

        // ─── SHADER MAP ───
        EditorGUILayout.LabelField("Shader Map", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        int mapCol = 0;
        foreach (ShaderRenderCategory cat in Enum.GetValues(typeof(ShaderRenderCategory)))
        {
            int count = categorizedMaterials.ContainsKey(cat) ? categorizedMaterials[cat].Count : 0;

            bool isActive = shaderMapFilter.HasValue && shaderMapFilter.Value == cat;
            Color bgColor = categoryColors.ContainsKey(cat) ? categoryColors[cat] : Color.white;
            if (isActive) bgColor = Color.Lerp(bgColor, new Color(0.35f, 0.55f, 0.85f), 0.5f);
            GUI.backgroundColor = bgColor;

            EditorGUILayout.BeginVertical("box", GUILayout.Width(60), GUILayout.Height(50));

            Color textCol = categoryTextColors.ContainsKey(cat) ? categoryTextColors[cat] : Color.gray;
            var prev = GUI.contentColor;
            GUI.contentColor = textCol;
            EditorGUILayout.LabelField(GetCategoryIcon(cat), new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            });
            EditorGUILayout.LabelField(count.ToString(), new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13
            });
            string shortName = cat.ToString().Length > 7 ? cat.ToString().Substring(0, 6) + "." : cat.ToString();
            EditorGUILayout.LabelField(shortName, new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9
            });
            GUI.contentColor = prev;

            EditorGUILayout.EndVertical();

            // Clickable
            Rect cardRect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
            {
                shaderMapFilter = (shaderMapFilter.HasValue && shaderMapFilter.Value == cat) ? (ShaderRenderCategory?)null : cat;
                Event.current.Use();
            }

            GUI.backgroundColor = Color.white;
            mapCol++;
            if (mapCol == 5)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                mapCol = 0;
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // ─── SHADER LIST ───
        string filterLabel = shaderMapFilter.HasValue
            ? $"Dang xem: {GetCategoryIcon(shaderMapFilter.Value)} {shaderMapFilter.Value}"
            : "Tat ca Shaders";

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(filterLabel, sectionHeaderStyle);
        GUILayout.FlexibleSpace();
        if (shaderMapFilter.HasValue)
        {
            if (GUILayout.Button("Xem All", EditorStyles.miniButton, GUILayout.Width(60)))
                shaderMapFilter = null;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        shaderScrollPos = EditorGUILayout.BeginScrollView(shaderScrollPos, GUILayout.MinHeight(200));

        var filteredShaderGroups = shaderGroups
            .Where(kvp => !shaderMapFilter.HasValue ||
                kvp.Value.Any(m => m.category == shaderMapFilter.Value))
            .OrderBy(kvp => kvp.Key);

        foreach (var kvp in filteredShaderGroups)
        {
            string shaderName = kvp.Key;
            var mats = shaderMapFilter.HasValue
                ? kvp.Value.Where(m => m.category == shaderMapFilter.Value).ToList()
                : kvp.Value;

            if (mats.Count == 0) continue;

            if (!shaderGroupFoldouts.ContainsKey(shaderName))
                shaderGroupFoldouts[shaderName] = false;

            EditorGUILayout.BeginVertical(boxStyle);

            shaderGroupFoldouts[shaderName] = EditorGUILayout.Foldout(shaderGroupFoldouts[shaderName],
                $"  {shaderName} ({mats.Count} materials)", true, EditorStyles.foldoutHeader);

            if (shaderGroupFoldouts[shaderName])
            {
                foreach (var mat in mats)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(16);

                    if (GUILayout.Button(mat.materialName, EditorStyles.linkLabel, GUILayout.Width(180)))
                    {
                        if (mat.material != null)
                        {
                            EditorGUIUtility.PingObject(mat.material);
                            Selection.activeObject = mat.material;
                        }
                    }

                    GUILayout.Label($"({mat.UsedByObjectCount} obj)", EditorStyles.miniLabel, GUILayout.Width(55));

                    if (mat.isDefault)
                    {
                        var prev = GUI.contentColor;
                        GUI.contentColor = new Color(1f, 0.7f, 0.2f);
                        GUILayout.Label("[!]", EditorStyles.miniLabel, GUILayout.Width(20));
                        GUI.contentColor = prev;
                    }

                    // Quick buttons
                    if (GUILayout.Button("S", EditorStyles.miniButton, GUILayout.Width(20)))
                        OpenQuickConvertPopup(mat);
                    if (mat.isDefault && GUILayout.Button("D", EditorStyles.miniButton, GUILayout.Width(20)))
                        OpenQuickDuplicatePopup(mat);

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);

        // ─── QUICK CONVERT SECTION ───
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Quick Convert (chuyen shader ca nhom)", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        // Source shader
        string[] shaderNames = shaderGroups.Keys.OrderBy(s => s).ToArray();
        if (shaderNames.Length > 0)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source:", EditorStyles.miniLabel, GUILayout.Width(50));
            if (quickConvertSourceIdx >= shaderNames.Length) quickConvertSourceIdx = 0;
            quickConvertSourceIdx = EditorGUILayout.Popup(quickConvertSourceIdx, shaderNames);
            EditorGUILayout.EndHorizontal();
        }

        // Target shader
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Target:", EditorStyles.miniLabel, GUILayout.Width(50));
        quickConvertTargetIdx = EditorGUILayout.Popup(quickConvertTargetIdx, commonShaders);
        EditorGUILayout.EndHorizontal();

        if (quickConvertTargetIdx == commonShaders.Length - 1)
        {
            quickConvertCustomShader = EditorGUILayout.TextField("Custom:", quickConvertCustomShader);
        }

        // Scope
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Scope:", EditorStyles.miniLabel, GUILayout.Width(50));
        quickConvertScope = GUILayout.Toggle(quickConvertScope == 0, "Tat ca", EditorStyles.miniButton) ? 0 : 1;
        quickConvertScope = GUILayout.Toggle(quickConvertScope == 1, "Chi category dang xem", EditorStyles.miniButton) ? 1 : 0;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Convert buttons
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        string targetName = quickConvertTargetIdx == commonShaders.Length - 1
            ? quickConvertCustomShader : commonShaders[quickConvertTargetIdx];

        if (shaderNames.Length > 0)
        {
            string sourceName = shaderNames[quickConvertSourceIdx];
            var matsToConvert = shaderGroups.ContainsKey(sourceName) ? shaderGroups[sourceName] : new List<MaterialInfo>();

            if (shaderMapFilter.HasValue && quickConvertScope == 1)
                matsToConvert = matsToConvert.Where(m => m.category == shaderMapFilter.Value).ToList();

            // Filter out defaults
            var convertable = matsToConvert.Where(m => !m.isDefault).ToList();

            GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
            GUI.enabled = convertable.Count > 0 && !string.IsNullOrEmpty(targetName);
            if (GUILayout.Button($"Convert Now ({convertable.Count} materials)", GUILayout.Height(26)))
            {
                if (EditorUtility.DisplayDialog("Xac nhan",
                    $"Chuyen {convertable.Count} material(s) tu \"{sourceName}\" sang \"{targetName}\"?\n\n" +
                    "Co the Undo (Ctrl+Z).", "Chuyen doi", "Huy"))
                {
                    ConvertShaders(convertable, targetName);
                }
            }
            GUI.enabled = true;
            GUI.backgroundColor = Color.white;

            if (matsToConvert.Count(m => m.isDefault) > 0)
            {
                EditorGUILayout.Space(4);
                GUILayout.Label($"[!] {matsToConvert.Count(m => m.isDefault)} default mat(s) se bi bo qua. Duplicate truoc.",
                    EditorStyles.miniLabel);
            }
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ========================================================================
    //  PANEL 5: BATCH OPERATIONS
    // ========================================================================

    private void DrawBatchOpsPanel()
    {
        if (!hasScannedOnce || allMaterials.Count == 0)
        {
            DrawEmptyState("Chua co du lieu",
                "Nhan \"Quet Scene\" de bat dau. Panel nay cho phep thuc hien nhieu thao tac lien tiep (pipeline).");
            return;
        }

        // ─── STEP 1: SOURCE ───
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Buoc 1: Chon Material nguon", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        batchSourceMode = GUILayout.Toggle(batchSourceMode == 0, "Tu Selection", EditorStyles.miniButton) ? 0 : batchSourceMode;
        batchSourceMode = GUILayout.Toggle(batchSourceMode == 1, "Tu Filter", EditorStyles.miniButton) ? 1 : batchSourceMode;
        batchSourceMode = GUILayout.Toggle(batchSourceMode == 2, "Tat ca", EditorStyles.miniButton) ? 2 : batchSourceMode;
        EditorGUILayout.EndHorizontal();

        List<MaterialInfo> sourceMats = new List<MaterialInfo>();

        switch (batchSourceMode)
        {
            case 0: // Selection
                sourceMats = allMaterials.Where(m => m.isSelected).ToList();
                GUILayout.Label($"  {sourceMats.Count} material(s) da chon tu cac panel khac", EditorStyles.miniLabel);
                break;

            case 1: // Filter
                DrawBatchFilterPanel();
                sourceMats = batchFilteredMaterials;
                break;

            case 2: // All
                sourceMats = allMaterials.ToList();
                GUILayout.Label($"  Tat ca {sourceMats.Count} material(s) trong scene", EditorStyles.miniLabel);
                break;
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ─── STEP 2: PIPELINE ───
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Buoc 2: Pipeline thao tac", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        // Pipeline steps
        if (batchPipeline.Count == 0)
        {
            GUILayout.Label("  Chua co buoc nao. Them buoc ben duoi.", EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < batchPipeline.Count; i++)
            {
                DrawBatchPipelineStep(i);
            }
        }

        EditorGUILayout.Space(4);

        // Add step buttons
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Them buoc:", EditorStyles.miniLabel, GUILayout.Width(70));

        if (GUILayout.Button("Duplicate", EditorStyles.miniButton))
            batchPipeline.Add(new BatchStep { type = BatchStepType.Duplicate, duplicatePath = settings.defaultDuplicatePath });

        if (GUILayout.Button("Doi Shader", EditorStyles.miniButton))
            batchPipeline.Add(new BatchStep { type = BatchStepType.ChangeShader });

        if (GUILayout.Button("Set Property", EditorStyles.miniButton))
            batchPipeline.Add(new BatchStep { type = BatchStepType.SetProperty });

        if (GUILayout.Button("Rename", EditorStyles.miniButton))
            batchPipeline.Add(new BatchStep { type = BatchStepType.Rename });

        if (GUILayout.Button("Move", EditorStyles.miniButton))
            batchPipeline.Add(new BatchStep { type = BatchStepType.MoveToFolder });

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        // ─── STEP 3: PREVIEW & EXECUTE ───
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField("Buoc 3: Xac nhan & Thuc hien", sectionHeaderStyle);
        EditorGUILayout.Space(2);

        // Preview table
        if (batchShowPreview && sourceMats.Count > 0)
        {
            DrawBatchPreviewTable(sourceMats);
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Preview", EditorStyles.miniButton, GUILayout.Width(80)))
        {
            batchShowPreview = !batchShowPreview;
        }

        GUI.backgroundColor = (sourceMats.Count > 0 && batchPipeline.Count > 0)
            ? new Color(0.3f, 0.8f, 0.5f) : Color.gray;
        GUI.enabled = sourceMats.Count > 0 && batchPipeline.Count > 0;

        if (GUILayout.Button($"Thuc hien Pipeline ({sourceMats.Count} materials)",
            GUILayout.Height(28), GUILayout.Width(300)))
        {
            if (EditorUtility.DisplayDialog("Xac nhan Pipeline",
                $"Thuc hien {batchPipeline.Count} buoc tren {sourceMats.Count} material(s)?\n\n" +
                "Co the Undo (Ctrl+Z).",
                "Thuc hien", "Huy"))
            {
                ExecuteBatchPipeline(sourceMats);
            }
        }

        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawBatchFilterPanel()
    {
        EditorGUILayout.BeginVertical("box");

        string[] statusOpts = { "All", "Default Only", "Custom Only" };
        string[] catOpts = new string[] { "All" }
            .Concat(Enum.GetNames(typeof(ShaderRenderCategory))).ToArray();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Status:", EditorStyles.miniLabel, GUILayout.Width(45));
        batchFilterStatus = EditorGUILayout.Popup(batchFilterStatus, statusOpts, GUILayout.Width(100));

        GUILayout.Label("Category:", EditorStyles.miniLabel, GUILayout.Width(60));
        batchFilterCategory = EditorGUILayout.Popup(batchFilterCategory, catOpts, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Search:", EditorStyles.miniLabel, GUILayout.Width(45));
        batchSearchText = EditorGUILayout.TextField(batchSearchText);
        EditorGUILayout.EndHorizontal();

        // Apply batch filter
        batchFilteredMaterials = allMaterials.Where(m =>
        {
            if (batchFilterStatus == 1 && !m.isDefault) return false;
            if (batchFilterStatus == 2 && m.isDefault) return false;
            if (batchFilterCategory > 0)
            {
                ShaderRenderCategory cat = (ShaderRenderCategory)(batchFilterCategory - 1);
                if (m.category != cat) return false;
            }
            if (!string.IsNullOrEmpty(batchSearchText) &&
                !m.materialName.ToLower().Contains(batchSearchText.ToLower()) &&
                !m.shaderName.ToLower().Contains(batchSearchText.ToLower()))
                return false;
            return true;
        }).ToList();

        // Select all
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        batchSelectAll = EditorGUILayout.ToggleLeft($"Select All ({batchFilteredMaterials.Count})", batchSelectAll,
            EditorStyles.miniLabel, GUILayout.Width(130));
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var m in batchFilteredMaterials) m.isSelected = batchSelectAll;
        }
        EditorGUILayout.EndHorizontal();

        // Mini list (scrollable, max 5 visible)
        int showCount = Math.Min(batchFilteredMaterials.Count, 8);
        if (showCount > 0)
        {
            for (int i = 0; i < showCount; i++)
            {
                var mat = batchFilteredMaterials[i];
                EditorGUILayout.BeginHorizontal();
                mat.isSelected = EditorGUILayout.Toggle(mat.isSelected, GUILayout.Width(18));
                GUILayout.Label(mat.materialName, EditorStyles.miniLabel, GUILayout.Width(160));
                GUILayout.Label(mat.shaderName, EditorStyles.miniLabel, GUILayout.Width(130));
                DrawCategoryBadge(mat.category, 60);
                EditorGUILayout.EndHorizontal();
            }

            if (batchFilteredMaterials.Count > showCount)
                GUILayout.Label($"  ... va {batchFilteredMaterials.Count - showCount} materials khac", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawBatchPipelineStep(int index)
    {
        var step = batchPipeline[index];

        GUI.backgroundColor = step.isEnabled ? new Color(0.9f, 0.95f, 1f) : new Color(0.85f, 0.85f, 0.85f);
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = Color.white;

        // Step header
        EditorGUILayout.BeginHorizontal();

        step.isEnabled = EditorGUILayout.Toggle(step.isEnabled, GUILayout.Width(18));
        GUILayout.Label($"Buoc {index + 1}: {step.type}", EditorStyles.boldLabel);

        GUILayout.FlexibleSpace();

        // Move up/down
        GUI.enabled = index > 0;
        if (GUILayout.Button("^", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            batchPipeline.RemoveAt(index);
            batchPipeline.Insert(index - 1, step);
        }
        GUI.enabled = index < batchPipeline.Count - 1;
        if (GUILayout.Button("v", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            batchPipeline.RemoveAt(index);
            batchPipeline.Insert(index + 1, step);
        }
        GUI.enabled = true;

        // Delete
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("X", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            batchPipeline.RemoveAt(index);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        // Step-specific config
        if (step.isEnabled)
        {
            EditorGUILayout.Space(2);

            switch (step.type)
            {
                case BatchStepType.Duplicate:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Luu vao:", EditorStyles.miniLabel, GUILayout.Width(50));
                    step.duplicatePath = EditorGUILayout.TextField(step.duplicatePath);
                    if (GUILayout.Button("..", EditorStyles.miniButton, GUILayout.Width(25)))
                    {
                        string path = EditorUtility.OpenFolderPanel("Chon folder", "Assets", "");
                        if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                            step.duplicatePath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Pattern:", EditorStyles.miniLabel, GUILayout.Width(50));
                    step.namingPattern = EditorGUILayout.TextField(step.namingPattern);
                    EditorGUILayout.EndHorizontal();
                    break;

                case BatchStepType.ChangeShader:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Shader:", EditorStyles.miniLabel, GUILayout.Width(50));
                    step.targetShaderIndex = EditorGUILayout.Popup(step.targetShaderIndex, commonShaders);
                    EditorGUILayout.EndHorizontal();

                    if (step.targetShaderIndex == commonShaders.Length - 1)
                    {
                        step.targetShaderName = EditorGUILayout.TextField("Custom:", step.targetShaderName);
                    }
                    else
                    {
                        step.targetShaderName = commonShaders[step.targetShaderIndex];
                    }

                    step.preserveProperties = EditorGUILayout.ToggleLeft("Giu lai properties", step.preserveProperties,
                        EditorStyles.miniLabel);
                    break;

                case BatchStepType.SetProperty:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Property:", EditorStyles.miniLabel, GUILayout.Width(55));
                    step.propertyName = EditorGUILayout.TextField(step.propertyName, GUILayout.Width(100));

                    if (step.propertyName.Contains("Color") || step.propertyName.Contains("color"))
                    {
                        step.colorValue = EditorGUILayout.ColorField(step.colorValue);
                    }
                    else
                    {
                        step.floatValue = EditorGUILayout.FloatField(step.floatValue);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;

                case BatchStepType.Rename:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Prefix:", EditorStyles.miniLabel, GUILayout.Width(45));
                    step.renamePrefix = EditorGUILayout.TextField(step.renamePrefix, GUILayout.Width(80));
                    GUILayout.Label("Suffix:", EditorStyles.miniLabel, GUILayout.Width(40));
                    step.renameSuffix = EditorGUILayout.TextField(step.renameSuffix, GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Find:", EditorStyles.miniLabel, GUILayout.Width(45));
                    step.renameFindStr = EditorGUILayout.TextField(step.renameFindStr, GUILayout.Width(100));
                    GUILayout.Label("Replace:", EditorStyles.miniLabel, GUILayout.Width(55));
                    step.renameReplaceStr = EditorGUILayout.TextField(step.renameReplaceStr, GUILayout.Width(100));
                    EditorGUILayout.EndHorizontal();
                    break;

                case BatchStepType.MoveToFolder:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("To:", EditorStyles.miniLabel, GUILayout.Width(20));
                    step.movePath = EditorGUILayout.TextField(step.movePath);
                    if (GUILayout.Button("..", EditorStyles.miniButton, GUILayout.Width(25)))
                    {
                        string path = EditorUtility.OpenFolderPanel("Chon folder", "Assets", "");
                        if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                            step.movePath = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    EditorGUILayout.EndHorizontal();
                    break;

                case BatchStepType.Replace:
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Thay bang:", EditorStyles.miniLabel, GUILayout.Width(65));
                    step.replacementMaterial = (Material)EditorGUILayout.ObjectField(step.replacementMaterial,
                        typeof(Material), false);
                    EditorGUILayout.EndHorizontal();
                    break;
            }
        }

        EditorGUILayout.EndVertical();

        // Arrow between steps
        if (index < batchPipeline.Count - 1)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(">>>", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawBatchPreviewTable(List<MaterialInfo> sourceMats)
    {
        EditorGUILayout.LabelField("Preview", sectionHeaderStyle);

        // Table header
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        GUILayout.Label("Material", EditorStyles.toolbarButton, GUILayout.Width(150));
        GUILayout.Label("Truoc", EditorStyles.toolbarButton, GUILayout.Width(150));
        GUILayout.Label("Sau", EditorStyles.toolbarButton, GUILayout.Width(150));
        EditorGUILayout.EndHorizontal();

        int previewCount = Math.Min(sourceMats.Count, 10);
        for (int i = 0; i < previewCount; i++)
        {
            var mat = sourceMats[i];
            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(mat.materialName, EditorStyles.miniLabel, GUILayout.Width(150));

            // Before state
            string before = $"{mat.shaderName}/{mat.category}";
            if (mat.isDefault) before += " [!]Default";
            GUILayout.Label(before, EditorStyles.miniLabel, GUILayout.Width(150));

            // After state (predicted)
            string after = PredictAfterState(mat);
            var prev = GUI.contentColor;
            GUI.contentColor = new Color(0.3f, 0.8f, 0.4f);
            GUILayout.Label(after, EditorStyles.miniLabel, GUILayout.Width(150));
            GUI.contentColor = prev;

            EditorGUILayout.EndHorizontal();
        }

        if (sourceMats.Count > previewCount)
            GUILayout.Label($"  ... va {sourceMats.Count - previewCount} materials khac", EditorStyles.miniLabel);
    }

    // ========================================================================
    //  POPUPS
    // ========================================================================

    private void DrawQuickConvertPopup()
    {
        if (popupTargetMaterial == null) { showQuickConvertPopup = false; return; }

        // Dim background
        Rect fullRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(fullRect, new Color(0, 0, 0, 0.3f));

        // Popup window
        float popW = 350, popH = 200;
        Rect popupRect = new Rect((position.width - popW) / 2, (position.height - popH) / 2, popW, popH);

        GUILayout.BeginArea(popupRect, "box");

        EditorGUILayout.LabelField("Doi Shader", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField($"Material: {popupTargetMaterial.materialName}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Shader hien tai: {popupTargetMaterial.shaderName} ({popupTargetMaterial.category})",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        popupConvertShaderIdx = EditorGUILayout.Popup("Shader moi:", popupConvertShaderIdx, commonShaders);

        if (popupConvertShaderIdx == commonShaders.Length - 1)
            popupConvertCustomShader = EditorGUILayout.TextField("Custom:", popupConvertCustomShader);

        popupConvertPreserveProps = EditorGUILayout.ToggleLeft("Giu lai properties", popupConvertPreserveProps);

        EditorGUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Huy"))
        {
            showQuickConvertPopup = false;
            popupTargetMaterial = null;
        }

        string target = popupConvertShaderIdx == commonShaders.Length - 1
            ? popupConvertCustomShader : commonShaders[popupConvertShaderIdx];

        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
        GUI.enabled = !string.IsNullOrEmpty(target);
        if (GUILayout.Button("Doi Shader"))
        {
            ConvertShaders(new List<MaterialInfo> { popupTargetMaterial }, target);
            showQuickConvertPopup = false;
            popupTargetMaterial = null;
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    private void DrawQuickDuplicatePopup()
    {
        if (popupTargetMaterial == null) { showQuickDuplicatePopup = false; return; }

        // Dim background
        Rect fullRect = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(fullRect, new Color(0, 0, 0, 0.3f));

        // Popup window
        float popW = 360, popH = 190;
        Rect popupRect = new Rect((position.width - popW) / 2, (position.height - popH) / 2, popW, popH);

        GUILayout.BeginArea(popupRect, "box");

        EditorGUILayout.LabelField("Duplicate Material", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField($"Material: {popupTargetMaterial.materialName}", EditorStyles.miniLabel);

        EditorGUILayout.Space(4);

        popupDuplicateName = EditorGUILayout.TextField("Ten moi:", popupDuplicateName);

        EditorGUILayout.BeginHorizontal();
        popupDuplicatePath = EditorGUILayout.TextField("Luu vao:", popupDuplicatePath);
        if (GUILayout.Button("..", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFolderPanel("Chon folder", "Assets", "");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                popupDuplicatePath = "Assets" + path.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        popupDuplicateAutoAssign = EditorGUILayout.ToggleLeft("Tu dong gan len objects", popupDuplicateAutoAssign);

        EditorGUILayout.Space(6);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Huy"))
        {
            showQuickDuplicatePopup = false;
            popupTargetMaterial = null;
        }

        GUI.backgroundColor = new Color(0.3f, 0.7f, 1f);
        if (GUILayout.Button("Duplicate"))
        {
            DuplicateMaterials(new List<MaterialInfo> { popupTargetMaterial }, popupDuplicatePath);
            showQuickDuplicatePopup = false;
            popupTargetMaterial = null;
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    // ========================================================================
    //  SETTINGS PANEL
    // ========================================================================

    private void DrawSettingsPanel()
    {
        EditorGUILayout.LabelField("Cai dat", headerStyle);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginVertical(boxStyle);

        // Default paths
        EditorGUILayout.LabelField("Duong dan mac dinh", sectionHeaderStyle);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Folder duplicate:", EditorStyles.miniLabel, GUILayout.Width(100));
        settings.defaultDuplicatePath = EditorGUILayout.TextField(settings.defaultDuplicatePath);
        if (GUILayout.Button("..", GUILayout.Width(25)))
        {
            string path = EditorUtility.OpenFolderPanel("Chon folder", "Assets", "");
            if (!string.IsNullOrEmpty(path) && path.StartsWith(Application.dataPath))
                settings.defaultDuplicatePath = "Assets" + path.Substring(Application.dataPath.Length);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Auto scan
        EditorGUILayout.LabelField("Auto Scan", sectionHeaderStyle);
        settings.autoScanOnOpen = EditorGUILayout.ToggleLeft("Tu dong quet khi mo tool", settings.autoScanOnOpen);
        settings.autoScanOnSceneChange = EditorGUILayout.ToggleLeft("Tu dong quet khi doi scene", settings.autoScanOnSceneChange);
        settings.scanInactiveObjects = EditorGUILayout.ToggleLeft("Quet ca inactive objects", settings.scanInactiveObjects);

        EditorGUILayout.Space(6);

        // Display
        EditorGUILayout.LabelField("Hien thi", sectionHeaderStyle);
        settings.previewSize = EditorGUILayout.IntSlider("Preview size:", settings.previewSize, 16, 64);
        settings.defaultViewMode = (MaterialViewMode)EditorGUILayout.EnumPopup("Default view:", settings.defaultViewMode);

        EditorGUILayout.Space(6);

        // Advanced
        EditorGUILayout.LabelField("Advanced", sectionHeaderStyle);
        settings.includeSubScenes = EditorGUILayout.ToggleLeft("Include sub-scenes", settings.includeSubScenes);
        settings.includePrefabInstances = EditorGUILayout.ToggleLeft("Include Prefab instances", settings.includePrefabInstances);
        settings.showInternalShaders = EditorGUILayout.ToggleLeft("Show internal Unity shaders", settings.showInternalShaders);

        EditorGUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset ve mac dinh", GUILayout.Width(140)))
        {
            settings = new ToolSettings();
        }
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Luu & Dong", GUILayout.Width(100)))
        {
            settings.Save();
            showSettings = false;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    // ========================================================================
    //  CORE LOGIC: SCAN SCENE
    // ========================================================================

    private void ScanScene()
    {
        allObjects.Clear();
        allMaterials.Clear();
        categorizedMaterials.Clear();
        shaderGroups.Clear();
        warnings.Clear();
        shaderGroupFoldouts.Clear();
        matGroupFoldouts.Clear();

        // Find all renderers
        Renderer[] allRenderers = settings.scanInactiveObjects
            ? Resources.FindObjectsOfTypeAll<Renderer>()
            : FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        // Filter to scene objects only (not assets)
        allRenderers = allRenderers.Where(r => r.gameObject != null &&
            !EditorUtility.IsPersistent(r.gameObject) &&
            r.gameObject.scene.isLoaded).ToArray();

        Dictionary<Material, MaterialInfo> materialMap = new Dictionary<Material, MaterialInfo>();
        Dictionary<GameObject, ObjectInfo> objectMap = new Dictionary<GameObject, ObjectInfo>();

        foreach (Renderer renderer in allRenderers)
        {
            GameObject go = renderer.gameObject;

            // Get or create ObjectInfo
            if (!objectMap.ContainsKey(go))
            {
                objectMap[go] = new ObjectInfo
                {
                    gameObject = go,
                    name = go.name,
                    hierarchyPath = GetHierarchyPath(go),
                    isExpanded = false,
                    isSelected = false
                };
            }

            ObjectInfo objInfo = objectMap[go];

            // Create RendererInfo
            RendererInfo rendInfo = new RendererInfo
            {
                renderer = renderer,
                gameObject = go,
                rendererType = renderer.GetType().Name
            };

            // Process materials
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat == null) continue;

                if (!materialMap.ContainsKey(mat))
                {
                    materialMap[mat] = CreateMaterialInfo(mat);
                }

                MaterialInfo matInfo = materialMap[mat];
                matInfo.usedByRenderers.Add(rendInfo);
                rendInfo.materials.Add(matInfo);
            }

            objInfo.renderers.Add(rendInfo);
        }

        allObjects = objectMap.Values.OrderBy(o => o.name).ToList();
        allMaterials = materialMap.Values.OrderBy(m => m.materialName).ToList();

        // Categorize
        foreach (ShaderRenderCategory cat in Enum.GetValues(typeof(ShaderRenderCategory)))
        {
            categorizedMaterials[cat] = allMaterials.Where(m => m.category == cat).ToList();
        }

        // Group by shader
        shaderGroups = allMaterials.GroupBy(m => m.shaderName)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Generate warnings
        RefreshWarnings();

        hasScannedOnce = true;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        AddActionLog($"Quet scene \"{sceneName}\"",
            $"{allObjects.Count} objects, {allMaterials.Count} materials, {shaderGroups.Count} shaders");

        Debug.Log($"[MaterialShaderManager v2] Quet xong: {allObjects.Count} objects, " +
            $"{allMaterials.Count} materials, {shaderGroups.Count} shaders, " +
            $"{allMaterials.Count(m => m.isDefault)} default materials.");

        Repaint();
    }

    private MaterialInfo CreateMaterialInfo(Material mat)
    {
        var info = new MaterialInfo
        {
            material = mat,
            materialName = mat.name,
            shaderName = mat.shader != null ? mat.shader.name : "None",
            category = ClassifyShader(mat),
            isDefault = IsDefaultMaterial(mat),
            isSelected = false,
            isDuplicated = false,
            assetPath = AssetDatabase.GetAssetPath(mat),
            renderQueue = mat.renderQueue,
            hasMainTex = mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null,
            hasNormalMap = mat.HasProperty("_BumpMap") && mat.GetTexture("_BumpMap") != null,
            hasColor = mat.HasProperty("_Color"),
            mainColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white,
            metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f,
            smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") :
                (mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0f)
        };
        return info;
    }

    // ========================================================================
    //  CORE LOGIC: CLASSIFY SHADER
    // ========================================================================

    private ShaderRenderCategory ClassifyShader(Material mat)
    {
        if (mat == null || mat.shader == null) return ShaderRenderCategory.Unknown;

        string shaderName = mat.shader.name.ToLower();

        // UI Shaders
        if (shaderName.Contains("ui/") || shaderName.Contains("sprites/"))
            return ShaderRenderCategory.UI;

        // Unlit Shaders
        if (shaderName.Contains("unlit"))
        {
            if (shaderName.Contains("transparent")) return ShaderRenderCategory.Transparent;
            if (shaderName.Contains("cutout")) return ShaderRenderCategory.Cutout;
            return ShaderRenderCategory.Unlit;
        }

        // Particle Shaders
        if (shaderName.Contains("particle"))
        {
            if (shaderName.Contains("additive") || shaderName.Contains("add"))
                return ShaderRenderCategory.Additive;
            if (shaderName.Contains("multiply"))
                return ShaderRenderCategory.Multiply;
            return ShaderRenderCategory.Transparent;
        }

        // Standard / URP Lit
        if (shaderName.Contains("standard") || shaderName.Contains("/lit") ||
            shaderName.Contains("simple lit") || shaderName.Contains("complex lit"))
        {
            if (mat.HasProperty("_Mode"))
            {
                int mode = (int)mat.GetFloat("_Mode");
                switch (mode)
                {
                    case 0: return ShaderRenderCategory.Opaque;
                    case 1: return ShaderRenderCategory.Cutout;
                    case 2: return ShaderRenderCategory.Fade;
                    case 3: return ShaderRenderCategory.Transparent;
                }
            }

            if (mat.HasProperty("_Surface"))
            {
                int surface = (int)mat.GetFloat("_Surface");
                if (surface == 1) return ShaderRenderCategory.Transparent;
                return ShaderRenderCategory.Opaque;
            }

            return ClassifyByRenderQueue(mat);
        }

        // Keywords in name
        if (shaderName.Contains("transparent") || shaderName.Contains("alpha"))
            return ShaderRenderCategory.Transparent;
        if (shaderName.Contains("cutout") || shaderName.Contains("alphatest"))
            return ShaderRenderCategory.Cutout;
        if (shaderName.Contains("fade"))
            return ShaderRenderCategory.Fade;
        if (shaderName.Contains("additive") || shaderName.Contains("add"))
            return ShaderRenderCategory.Additive;
        if (shaderName.Contains("multiply"))
            return ShaderRenderCategory.Multiply;

        return ClassifyByRenderQueue(mat);
    }

    private ShaderRenderCategory ClassifyByRenderQueue(Material mat)
    {
        int queue = mat.renderQueue;
        if (queue <= 2450) return ShaderRenderCategory.Opaque;
        if (queue <= 2500) return ShaderRenderCategory.Cutout;
        if (queue <= 4000) return ShaderRenderCategory.Transparent;
        return ShaderRenderCategory.Unknown;
    }

    // ========================================================================
    //  CORE LOGIC: IS DEFAULT MATERIAL
    // ========================================================================

    private bool IsDefaultMaterial(Material mat)
    {
        if (mat == null) return false;

        string assetPath = AssetDatabase.GetAssetPath(mat);

        if (string.IsNullOrEmpty(assetPath)) return true;
        if (assetPath.StartsWith("Resources/") || assetPath.StartsWith("Library/") ||
            assetPath.Contains("unity_builtin") || assetPath.Contains("unity default"))
            return true;
        if (assetPath.StartsWith("Packages/com.unity."))
            return true;

        return false;
    }

    // ========================================================================
    //  CORE LOGIC: DUPLICATE MATERIALS
    // ========================================================================

    private void DuplicateMaterials(List<MaterialInfo> materialsToClone, string savePath)
    {
        if (materialsToClone.Count == 0) return;

        if (!AssetDatabase.IsValidFolder(savePath))
            CreateFolderRecursive(savePath);

        Undo.SetCurrentGroupName("Duplicate Materials");
        int undoGroup = Undo.GetCurrentGroup();

        int successCount = 0;

        foreach (var info in materialsToClone)
        {
            Material newMat = new Material(info.material);
            newMat.name = info.material.name + "_Copy";

            string fileName = SanitizeFileName(newMat.name);
            string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{savePath}/{fileName}.mat");

            AssetDatabase.CreateAsset(newMat, fullPath);

            // Replace on all renderers
            foreach (var rendInfo in info.usedByRenderers)
            {
                if (rendInfo.renderer == null) continue;
                Undo.RecordObject(rendInfo.renderer, "Replace Material");

                Material[] mats = rendInfo.renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == info.material)
                        mats[i] = newMat;
                }
                rendInfo.renderer.sharedMaterials = mats;
            }

            info.isDuplicated = true;
            info.material = newMat;
            info.materialName = newMat.name;
            info.isDefault = false;
            info.assetPath = fullPath;
            successCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddActionLog($"Duplicate {successCount} material(s)",
            $"Luu vao {savePath}");

        EditorUtility.DisplayDialog("Hoan tat",
            $"Da duplicate thanh cong {successCount} material(s)\nvao {savePath}", "OK");

        ScanScene();
    }

    // ========================================================================
    //  CORE LOGIC: CONVERT SHADERS
    // ========================================================================

    private void ConvertShaders(List<MaterialInfo> materialsToConvert, string targetShaderName)
    {
        Shader targetShader = Shader.Find(targetShaderName);

        if (targetShader == null)
        {
            EditorUtility.DisplayDialog("Loi",
                $"Khong tim thay shader \"{targetShaderName}\".\nDam bao ten shader chinh xac.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Convert Shaders");
        int undoGroup = Undo.GetCurrentGroup();

        int successCount = 0;
        List<string> skipped = new List<string>();

        foreach (var info in materialsToConvert)
        {
            Material mat = info.material;

            if (info.isDefault)
            {
                skipped.Add(mat.name);
                continue;
            }

            Undo.RecordObject(mat, "Change Shader");

            // Save properties
            Color? mainColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : (Color?)null;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            Texture normalMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
            float? metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : (float?)null;
            float? smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") :
                (mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : (float?)null);

            // Change shader
            mat.shader = targetShader;

            // Restore properties
            if (mainColor.HasValue)
            {
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", mainColor.Value);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mainColor.Value);
            }
            if (mainTex != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTex);
            }
            if (normalMap != null && mat.HasProperty("_BumpMap"))
                mat.SetTexture("_BumpMap", normalMap);
            if (metallic.HasValue && mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", metallic.Value);
            if (smoothness.HasValue)
            {
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness.Value);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness.Value);
            }

            info.shaderName = targetShaderName;
            info.category = ClassifyShader(mat);
            EditorUtility.SetDirty(mat);
            successCount++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();

        string msg = $"Da chuyen doi {successCount} material(s) sang \"{targetShaderName}\"";
        if (skipped.Count > 0)
            msg += $"\nBo qua {skipped.Count} default material(s): {string.Join(", ", skipped.Take(3))}";

        AddActionLog($"Convert {successCount} mats → {targetShaderName}",
            $"Skipped: {skipped.Count} defaults");

        EditorUtility.DisplayDialog("Ket qua", msg, "OK");

        ScanScene();
    }

    // ========================================================================
    //  CORE LOGIC: BATCH PIPELINE EXECUTION
    // ========================================================================

    private void ExecuteBatchPipeline(List<MaterialInfo> sourceMats)
    {
        if (batchPipeline.Count == 0 || sourceMats.Count == 0) return;

        Undo.SetCurrentGroupName("Batch Pipeline");
        int undoGroup = Undo.GetCurrentGroup();

        List<MaterialInfo> workingMats = new List<MaterialInfo>(sourceMats);
        int totalOps = 0;

        foreach (var step in batchPipeline)
        {
            if (!step.isEnabled) continue;

            switch (step.type)
            {
                case BatchStepType.Duplicate:
                    var defaultsOnly = workingMats.Where(m => m.isDefault).ToList();
                    if (defaultsOnly.Count > 0)
                    {
                        if (!AssetDatabase.IsValidFolder(step.duplicatePath))
                            CreateFolderRecursive(step.duplicatePath);

                        foreach (var info in defaultsOnly)
                        {
                            Material newMat = new Material(info.material);
                            string newName = step.namingPattern.Replace("{name}", info.material.name);
                            newMat.name = newName;

                            string fileName = SanitizeFileName(newMat.name);
                            string fullPath = AssetDatabase.GenerateUniqueAssetPath($"{step.duplicatePath}/{fileName}.mat");
                            AssetDatabase.CreateAsset(newMat, fullPath);

                            foreach (var rendInfo in info.usedByRenderers)
                            {
                                if (rendInfo.renderer == null) continue;
                                Undo.RecordObject(rendInfo.renderer, "Replace Material");

                                Material[] mats = rendInfo.renderer.sharedMaterials;
                                for (int i = 0; i < mats.Length; i++)
                                {
                                    if (mats[i] == info.material) mats[i] = newMat;
                                }
                                rendInfo.renderer.sharedMaterials = mats;
                            }

                            info.isDuplicated = true;
                            info.material = newMat;
                            info.materialName = newMat.name;
                            info.isDefault = false;
                            info.assetPath = fullPath;
                            totalOps++;
                        }
                    }
                    break;

                case BatchStepType.ChangeShader:
                    Shader targetShader = Shader.Find(step.targetShaderName);
                    if (targetShader == null)
                    {
                        Debug.LogWarning($"[Batch] Shader '{step.targetShaderName}' not found, skipping step.");
                        continue;
                    }

                    foreach (var info in workingMats.Where(m => !m.isDefault))
                    {
                        Undo.RecordObject(info.material, "Change Shader");

                        if (step.preserveProperties)
                        {
                            Color? col = info.material.HasProperty("_Color") ? info.material.GetColor("_Color") : (Color?)null;
                            Texture tex = info.material.HasProperty("_MainTex") ? info.material.GetTexture("_MainTex") : null;

                            info.material.shader = targetShader;

                            if (col.HasValue)
                            {
                                if (info.material.HasProperty("_Color")) info.material.SetColor("_Color", col.Value);
                                if (info.material.HasProperty("_BaseColor")) info.material.SetColor("_BaseColor", col.Value);
                            }
                            if (tex != null)
                            {
                                if (info.material.HasProperty("_MainTex")) info.material.SetTexture("_MainTex", tex);
                                if (info.material.HasProperty("_BaseMap")) info.material.SetTexture("_BaseMap", tex);
                            }
                        }
                        else
                        {
                            info.material.shader = targetShader;
                        }

                        info.shaderName = step.targetShaderName;
                        info.category = ClassifyShader(info.material);
                        EditorUtility.SetDirty(info.material);
                        totalOps++;
                    }
                    break;

                case BatchStepType.SetProperty:
                    foreach (var info in workingMats.Where(m => !m.isDefault))
                    {
                        if (!info.material.HasProperty(step.propertyName)) continue;
                        Undo.RecordObject(info.material, "Set Property");

                        if (step.propertyName.Contains("Color") || step.propertyName.Contains("color"))
                            info.material.SetColor(step.propertyName, step.colorValue);
                        else
                            info.material.SetFloat(step.propertyName, step.floatValue);

                        EditorUtility.SetDirty(info.material);
                        totalOps++;
                    }
                    break;

                case BatchStepType.Rename:
                    foreach (var info in workingMats.Where(m => !m.isDefault && !string.IsNullOrEmpty(m.assetPath)))
                    {
                        string newName = info.materialName;

                        if (!string.IsNullOrEmpty(step.renameFindStr))
                            newName = newName.Replace(step.renameFindStr, step.renameReplaceStr);

                        newName = step.renamePrefix + newName + step.renameSuffix;

                        if (newName != info.materialName)
                        {
                            string error = AssetDatabase.RenameAsset(info.assetPath, newName);
                            if (string.IsNullOrEmpty(error))
                            {
                                info.material.name = newName;
                                info.materialName = newName;
                                totalOps++;
                            }
                        }
                    }
                    break;

                case BatchStepType.MoveToFolder:
                    if (!AssetDatabase.IsValidFolder(step.movePath))
                        CreateFolderRecursive(step.movePath);

                    foreach (var info in workingMats.Where(m => !m.isDefault && !string.IsNullOrEmpty(m.assetPath)))
                    {
                        string fileName = Path.GetFileName(info.assetPath);
                        string newPath = $"{step.movePath}/{fileName}";
                        string error = AssetDatabase.MoveAsset(info.assetPath, newPath);
                        if (string.IsNullOrEmpty(error))
                        {
                            info.assetPath = newPath;
                            totalOps++;
                        }
                    }
                    break;

                case BatchStepType.Replace:
                    if (step.replacementMaterial == null) continue;

                    foreach (var info in workingMats)
                    {
                        foreach (var rendInfo in info.usedByRenderers)
                        {
                            if (rendInfo.renderer == null) continue;
                            Undo.RecordObject(rendInfo.renderer, "Replace Material");

                            Material[] mats = rendInfo.renderer.sharedMaterials;
                            for (int i = 0; i < mats.Length; i++)
                            {
                                if (mats[i] == info.material) mats[i] = step.replacementMaterial;
                            }
                            rendInfo.renderer.sharedMaterials = mats;
                        }
                        totalOps++;
                    }
                    break;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string pipelineDesc = string.Join(" → ", batchPipeline.Where(s => s.isEnabled).Select(s => s.type.ToString()));
        AddActionLog($"Pipeline [{pipelineDesc}] tren {sourceMats.Count} mat(s)",
            $"Tong: {totalOps} thao tac thanh cong");

        EditorUtility.DisplayDialog("Hoan tat Pipeline",
            $"Da thuc hien {totalOps} thao tac tren {sourceMats.Count} material(s).\n" +
            $"Pipeline: {pipelineDesc}", "OK");

        ScanScene();
    }

    // ========================================================================
    //  HELPERS
    // ========================================================================

    private void DrawEmptyState(string title, string message)
    {
        EditorGUILayout.Space(40);
        EditorGUILayout.BeginVertical();

        var centeredStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14
        };
        EditorGUILayout.LabelField(title, centeredStyle);
        EditorGUILayout.Space(8);

        var msgStyle = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        EditorGUILayout.LabelField(message, msgStyle);

        EditorGUILayout.Space(16);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.5f);
        if (GUILayout.Button("Quet Scene", GUILayout.Width(150), GUILayout.Height(30)))
        {
            ScanScene();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void DrawCategoryBadge(ShaderRenderCategory cat, float width = 70)
    {
        Color textCol = categoryTextColors.ContainsKey(cat) ? categoryTextColors[cat] : Color.gray;
        var prev = GUI.contentColor;
        GUI.contentColor = textCol;
        GUILayout.Label($"{GetCategoryIcon(cat)} {cat}", EditorStyles.miniLabel, GUILayout.Width(width));
        GUI.contentColor = prev;
    }

    private string GetCategoryIcon(ShaderRenderCategory cat)
    {
        switch (cat)
        {
            case ShaderRenderCategory.Opaque:      return "[O]";
            case ShaderRenderCategory.Cutout:       return "[C]";
            case ShaderRenderCategory.Fade:         return "[F]";
            case ShaderRenderCategory.Transparent:  return "[T]";
            case ShaderRenderCategory.Additive:     return "[A]";
            case ShaderRenderCategory.Multiply:     return "[M]";
            case ShaderRenderCategory.UI:           return "[U]";
            case ShaderRenderCategory.Unlit:        return "[L]";
            case ShaderRenderCategory.Custom:       return "[?]";
            default:                                return "[?]";
        }
    }

    private string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return "/" + path;
    }

    private string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private void CreateFolderRecursive(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private void PerformSearch(string query)
    {
        searchResults.Clear();
        string q = query.ToLower();

        // Search objects
        foreach (var obj in allObjects)
        {
            if (obj.name.ToLower().Contains(q))
                searchResults.Add(new SearchResult { type = SearchResult.ResultType.Object, name = obj.name, data = obj });
        }

        // Search materials
        foreach (var mat in allMaterials)
        {
            if (mat.materialName.ToLower().Contains(q))
                searchResults.Add(new SearchResult { type = SearchResult.ResultType.Material, name = mat.materialName, data = mat });
        }

        // Search shaders
        foreach (var shader in shaderGroups.Keys)
        {
            if (shader.ToLower().Contains(q))
                searchResults.Add(new SearchResult { type = SearchResult.ResultType.Shader, name = shader, data = shader });
        }
    }

    private void RefreshWarnings()
    {
        warnings.Clear();

        int defaultCount = allMaterials.Count(m => m.isDefault);
        if (defaultCount > 0)
        {
            warnings.Add(new WarningItem
            {
                severity = WarningItem.Severity.Warning,
                message = $"{defaultCount} material default can duplicate",
                count = defaultCount,
                actionLabel = "Fix All",
                onAction = () =>
                {
                    foreach (var m in allMaterials.Where(m2 => m2.isDefault)) m.isSelected = true;
                    currentPanel = PanelTab.BatchOps;
                    batchSourceMode = 0;
                    if (batchPipeline.Count == 0)
                        batchPipeline.Add(new BatchStep { type = BatchStepType.Duplicate, duplicatePath = settings.defaultDuplicatePath });
                }
            });
        }

        int missingShaderCount = allMaterials.Count(m => m.shaderName == "Hidden/InternalErrorShader");
        if (missingShaderCount > 0)
        {
            warnings.Add(new WarningItem
            {
                severity = WarningItem.Severity.Warning,
                message = $"{missingShaderCount} material co shader bi loi",
                count = missingShaderCount,
                actionLabel = "Xem",
                onAction = () => { currentPanel = PanelTab.MaterialLibrary; matFilterStatus = 0; }
            });
        }

        // Find materials shared by many objects
        var highlyShared = allMaterials.Where(m => m.UsedByObjectCount > 10 && !m.isDefault).ToList();
        if (highlyShared.Count > 0)
        {
            warnings.Add(new WarningItem
            {
                severity = WarningItem.Severity.Suggestion,
                message = $"{highlyShared.Count} material dung chung boi >10 objects",
                count = highlyShared.Count,
                actionLabel = "Xem",
                onAction = () => { currentPanel = PanelTab.MaterialLibrary; }
            });
        }

        if (defaultCount == 0 && missingShaderCount == 0)
        {
            warnings.Add(new WarningItem
            {
                severity = WarningItem.Severity.OK,
                message = "Tat ca materials deu on!",
                count = 0,
                actionLabel = "",
                onAction = null
            });
        }
    }

    private void AddActionLog(string description, string details = "")
    {
        actionLog.Add(new ActionLogEntry(description, details));
        if (actionLog.Count > 100) actionLog.RemoveAt(0);
    }

    private List<ObjectInfo> GetFilteredObjects()
    {
        var filtered = allObjects.AsEnumerable();

        // Type filter
        if (sceneFilterType > 0)
        {
            string targetType = rendererTypeFilters[sceneFilterType];
            if (targetType == "UI (Canvas)")
            {
                filtered = filtered.Where(o => o.renderers.Any(r =>
                    r.rendererType == "CanvasRenderer" || r.rendererType.Contains("UI")));
            }
            else
            {
                filtered = filtered.Where(o => o.renderers.Any(r => r.rendererType == targetType));
            }
        }

        // Status filter
        if (sceneFilterStatus == 1) // Has Default
            filtered = filtered.Where(o => o.DefaultMaterialCount > 0);
        else if (sceneFilterStatus == 2) // Has Missing
            filtered = filtered.Where(o => o.AllMaterials.Any(m => m.shaderName == "Hidden/InternalErrorShader"));

        // Sort
        switch (sceneSort)
        {
            case SceneExplorerSort.ByName:
                filtered = filtered.OrderBy(o => o.name);
                break;
            case SceneExplorerSort.ByMaterialCount:
                filtered = filtered.OrderByDescending(o => o.TotalMaterialCount);
                break;
            case SceneExplorerSort.ByWarningCount:
                filtered = filtered.OrderByDescending(o => o.WarningCount);
                break;
        }

        return filtered.ToList();
    }

    private List<MaterialInfo> GetFilteredMaterials()
    {
        var filtered = allMaterials.AsEnumerable();

        // Status filter
        if (matFilterStatus == 1) filtered = filtered.Where(m => m.isDefault);
        else if (matFilterStatus == 2) filtered = filtered.Where(m => !m.isDefault);
        else if (matFilterStatus == 3) filtered = filtered.Where(m => m.isDuplicated);

        // Category filter
        if (matFilterCategory > 0)
        {
            ShaderRenderCategory cat = (ShaderRenderCategory)(matFilterCategory - 1);
            filtered = filtered.Where(m => m.category == cat);
        }

        // Search
        if (!string.IsNullOrEmpty(matSearchText))
        {
            string q = matSearchText.ToLower();
            filtered = filtered.Where(m =>
                m.materialName.ToLower().Contains(q) ||
                m.shaderName.ToLower().Contains(q));
        }

        return filtered.ToList();
    }

    private Dictionary<string, List<MaterialInfo>> GroupMaterials(List<MaterialInfo> mats, MaterialGroupBy groupBy)
    {
        switch (groupBy)
        {
            case MaterialGroupBy.Status:
                var statusGroups = new Dictionary<string, List<MaterialInfo>>();
                var defaults = mats.Where(m => m.isDefault).ToList();
                var customs = mats.Where(m => !m.isDefault).ToList();
                if (defaults.Count > 0) statusGroups["[!] DEFAULT MATERIALS"] = defaults;
                if (customs.Count > 0) statusGroups["[OK] CUSTOM MATERIALS"] = customs;
                return statusGroups;

            case MaterialGroupBy.ShaderCategory:
                return mats.GroupBy(m => m.category)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => $"{GetCategoryIcon(g.Key)} {g.Key}", g => g.ToList());

            case MaterialGroupBy.ShaderName:
                return mats.GroupBy(m => m.shaderName)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.ToList());

            case MaterialGroupBy.AssetPath:
                return mats.GroupBy(m =>
                {
                    if (string.IsNullOrEmpty(m.assetPath)) return "(built-in)";
                    string dir = Path.GetDirectoryName(m.assetPath);
                    return string.IsNullOrEmpty(dir) ? "(root)" : dir;
                })
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.ToList());

            default:
                return new Dictionary<string, List<MaterialInfo>> { { "All", mats } };
        }
    }

    private void OpenQuickConvertPopup(MaterialInfo mat)
    {
        popupTargetMaterial = mat;
        popupConvertShaderIdx = 0;
        popupConvertCustomShader = "";
        popupConvertPreserveProps = true;
        showQuickConvertPopup = true;
    }

    private void OpenQuickDuplicatePopup(MaterialInfo mat)
    {
        popupTargetMaterial = mat;
        popupDuplicateName = mat.materialName + "_Copy";
        popupDuplicatePath = settings.defaultDuplicatePath;
        popupDuplicateAutoAssign = true;
        showQuickDuplicatePopup = true;
    }

    private void SendSelectedObjectsToBatchOps(List<ObjectInfo> objects)
    {
        // Select all materials from selected objects
        foreach (var m in allMaterials) m.isSelected = false;

        foreach (var obj in objects)
        {
            foreach (var mat in obj.AllMaterials)
                mat.isSelected = true;
        }

        currentPanel = PanelTab.BatchOps;
        batchSourceMode = 0;
    }

    private string PredictAfterState(MaterialInfo mat)
    {
        string result = mat.shaderName + "/" + mat.category;

        foreach (var step in batchPipeline)
        {
            if (!step.isEnabled) continue;

            switch (step.type)
            {
                case BatchStepType.Duplicate:
                    if (mat.isDefault) result = "[Duplicated] " + result;
                    break;
                case BatchStepType.ChangeShader:
                    result = step.targetShaderName;
                    break;
                case BatchStepType.Rename:
                    // Show predicted name
                    break;
            }
        }

        return result;
    }

    private void ShowHelpDialog()
    {
        EditorUtility.DisplayDialog("Material & Shader Manager v2.0 - Huong dan",
            "PANELS:\n" +
            "1. Dashboard - Tong quan scene\n" +
            "2. Scene Explorer - Xem objects va materials\n" +
            "3. Material Library - Quan ly materials\n" +
            "4. Shader Workshop - Phan loai & doi shader\n" +
            "5. Batch Ops - Pipeline thao tac hang loat\n\n" +
            "SHORTCUTS:\n" +
            "F5 - Quet lai scene\n" +
            "Ctrl+F - Focus search\n" +
            "1-5 - Chuyen panel\n" +
            "Esc - Dong popup/settings\n" +
            "Ctrl+Shift+M - Mo tool\n\n" +
            "NUT NHANH:\n" +
            "S = Doi Shader (tren material)\n" +
            "D = Duplicate (tren default material)",
            "OK");
    }
}