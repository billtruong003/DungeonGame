#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Runtime;

namespace DungeonSystem.Editor
{
    /// <summary>
    /// Generates room prefabs with correct geometry for the dungeon system.
    /// 
    /// === COORDINATE SYSTEM ===
    /// 
    /// Grid space:
    ///   - Each cell is cellSize × cellSize world units (default 16×16).
    ///   - A room at grid position (gx, gy) with size (W, H) cells occupies
    ///     cells (gx, gy) through (gx+W-1, gy+H-1).
    /// 
    /// Room local space:
    ///   - Origin (0, 0, 0) is at the CENTER of the room's footprint.
    ///   - Floor extends from (-halfW, 0, -halfH) to (+halfW, 0, +halfH)
    ///     where halfW = W * cellSize / 2, halfH = H * cellSize / 2.
    ///   - Y-up: floor at y=0, walls rise in +Y.
    /// 
    /// Wall placement:
    ///   - Walls sit INSIDE the room boundary (inset by wallThickness / 2).
    ///   - This prevents overlap with adjacent rooms' walls.
    ///   - Wall width = cellSize (covers one full cell edge, no gaps).
    ///   - Wall height = WALL_HEIGHT (configurable, default 3.0).
    ///   - Wall thickness = WALL_THICKNESS (configurable, default 0.5).
    /// 
    /// Socket position:
    ///   - Socket GameObjects are placed at the cell's edge center on the room boundary.
    ///   - The wall child is offset INWARD by wallThickness / 2.
    /// </summary>
    public class RoomTemplateGenerator : EditorWindow
    {
        // ==================== CONSTANTS ====================
        private const float WALL_HEIGHT = 3.0f;
        private const float WALL_THICKNESS = 0.5f;
        private const float FLOOR_THICKNESS = 0.1f;

        // ==================== SINGLE ROOM FIELDS ====================
        private string _roomName = "NewRoom";
        private RoomType _roomType = RoomType.Combat;
        private float _cellSize = 16f;
        private int _widthInCells = 1;
        private int _heightInCells = 1;
        private bool _createTemplate = true;
        private string _savePath = "Assets/Prefabs/DungeonRooms";

        // ==================== BATCH FIELDS ====================
        private RoomDatabase _targetDatabase;
        private int _variantsPerType = 3;
        private bool _includeLargeVariants = true;
        private bool _showBatchSection = true;
        private bool _showSingleSection = false;

        [MenuItem("DungeonSystem/Room Template Generator")]
        public static void ShowWindow()
        {
            GetWindow<RoomTemplateGenerator>("Room Generator");
        }

        // =============================================================
        // GUI
        // =============================================================

        private void OnGUI()
        {
            GUILayout.Label("Room Template Generator", EditorStyles.boldLabel);
            GUILayout.Space(4);

            _savePath = EditorGUILayout.TextField("Save Path", _savePath);
            _cellSize = EditorGUILayout.FloatField("Cell Size", _cellSize);

            GUILayout.Space(4);
            EditorGUILayout.HelpBox(
                $"Wall Height: {WALL_HEIGHT}m  |  Wall Thickness: {WALL_THICKNESS}m  |  Floor Thickness: {FLOOR_THICKNESS}m\n" +
                "Walls are placed INSIDE room boundary to prevent overlap with neighbors.",
                MessageType.None);

            GUILayout.Space(12);

            // === BATCH ===
            _showBatchSection = EditorGUILayout.Foldout(_showBatchSection, "Batch Generate All Room Types", true, EditorStyles.foldoutHeader);
            if (_showBatchSection)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "Auto-generate default prefabs + templates for EVERY room type.\n" +
                    "If a RoomDatabase is assigned, templates are auto-added to it.\n" +
                    "Existing files with the same name will be skipped.",
                    MessageType.Info);

                _targetDatabase = (RoomDatabase)EditorGUILayout.ObjectField(
                    "Target RoomDatabase", _targetDatabase, typeof(RoomDatabase), false);
                _variantsPerType = EditorGUILayout.IntSlider("Variants Per Type", _variantsPerType, 1, 5);
                _includeLargeVariants = EditorGUILayout.Toggle("Include Large Variants (2x2, etc)", _includeLargeVariants);

                GUILayout.Space(8);
                GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
                if (GUILayout.Button("Generate ALL Default Rooms", GUILayout.Height(36)))
                {
                    int count = GenerateAllDefaults();
                    EditorUtility.DisplayDialog("Done",
                        $"Created {count} room templates." +
                        (_targetDatabase != null ? "\nTemplates added to RoomDatabase." : ""),
                        "OK");
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(12);

            // === SINGLE ===
            _showSingleSection = EditorGUILayout.Foldout(_showSingleSection, "Generate Single Room", true, EditorStyles.foldoutHeader);
            if (_showSingleSection)
            {
                EditorGUI.indentLevel++;
                _roomName = EditorGUILayout.TextField("Room Name", _roomName);
                _roomType = (RoomType)EditorGUILayout.EnumPopup("Room Type", _roomType);
                _widthInCells = EditorGUILayout.IntSlider("Width (Cells)", _widthInCells, 1, 4);
                _heightInCells = EditorGUILayout.IntSlider("Height (Cells)", _heightInCells, 1, 4);
                _createTemplate = EditorGUILayout.Toggle("Create Template SO", _createTemplate);

                GUILayout.Space(8);
                GUI.backgroundColor = new Color(0.3f, 0.7f, 0.4f);
                if (GUILayout.Button("Generate Room Prefab", GUILayout.Height(36)))
                {
                    GenerateSingleRoom(_roomType, _roomName, _widthInCells, _heightInCells, _createTemplate);
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.indentLevel--;
            }
        }

        // =============================================================
        // BATCH GENERATION
        // =============================================================

        private int GenerateAllDefaults()
        {
            EnsureFolder(_savePath);
            var roomSpecs = BuildDefaultSpecs();
            var createdTemplates = new List<RoomTemplate>();

            foreach (var spec in roomSpecs)
            {
                var template = GenerateSingleRoom(spec.type, spec.name, spec.width, spec.height, createSO: true);
                if (template != null)
                    createdTemplates.Add(template);
            }

            if (_targetDatabase != null && createdTemplates.Count > 0)
            {
                Undo.RecordObject(_targetDatabase, "Auto-populate RoomDatabase");
                foreach (var t in createdTemplates)
                {
                    if (!_targetDatabase.allTemplates.Contains(t))
                        _targetDatabase.allTemplates.Add(t);
                }
                _targetDatabase.InvalidateCache();
                EditorUtility.SetDirty(_targetDatabase);
                AssetDatabase.SaveAssets();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return createdTemplates.Count;
        }

        private List<RoomSpec> BuildDefaultSpecs()
        {
            var specs = new List<RoomSpec>();
            var typeConfigs = new Dictionary<RoomType, RoomTypeConfig>
            {
                { RoomType.Start,          new RoomTypeConfig("Entrance",    new[] { (1,1) }) },
                { RoomType.Combat,         new RoomTypeConfig("Arena",       new[] { (1,1), (2,1), (1,2), (2,2) }) },
                { RoomType.Loot,           new RoomTypeConfig("Treasury",    new[] { (1,1), (2,1) }) },
                { RoomType.Puzzle,         new RoomTypeConfig("Enigma",      new[] { (1,1), (2,2) }) },
                { RoomType.Boss,           new RoomTypeConfig("BossLair",    new[] { (2,2), (3,2) }) },
                { RoomType.MiniBoss,       new RoomTypeConfig("EliteArena",  new[] { (2,1), (2,2) }) },
                { RoomType.StaircaseUp,    new RoomTypeConfig("StairsUp",    new[] { (1,1) }) },
                { RoomType.StaircaseDown,  new RoomTypeConfig("StairsDown",  new[] { (1,1) }) },
                { RoomType.Corridor,       new RoomTypeConfig("Hallway",     new[] { (1,1) }) },
                { RoomType.Junction,       new RoomTypeConfig("Crossroad",   new[] { (1,1) }) },
                { RoomType.SecretRoom,     new RoomTypeConfig("Hidden",      new[] { (1,1) }) },
                { RoomType.SafeRoom,       new RoomTypeConfig("Sanctuary",   new[] { (1,1), (2,1) }) },
                { RoomType.Shop,           new RoomTypeConfig("Market",      new[] { (1,1), (2,1) }) },
                { RoomType.Trap,           new RoomTypeConfig("TrapRoom",    new[] { (1,1), (2,1) }) },
            };

            foreach (var kv in typeConfigs)
            {
                var config = kv.Value;
                foreach (var (w, h) in config.sizes)
                {
                    if (!_includeLargeVariants && (w > 1 || h > 1)) continue;

                    int count = (w == 1 && h == 1) ? _variantsPerType : Mathf.Max(1, _variantsPerType / 2);
                    for (int v = 0; v < count; v++)
                    {
                        string suffix = count > 1 ? $"_v{v + 1}" : "";
                        string sizeSuffix = (w > 1 || h > 1) ? $"_{w}x{h}" : "";
                        specs.Add(new RoomSpec
                        {
                            type = kv.Key,
                            name = $"{config.baseName}{sizeSuffix}{suffix}",
                            width = w,
                            height = h
                        });
                    }
                }
            }
            return specs;
        }

        // =============================================================
        // SINGLE ROOM GENERATION
        // =============================================================

        private RoomTemplate GenerateSingleRoom(RoomType type, string roomName, int w, int h, bool createSO)
        {
            EnsureFolder(_savePath);

            string prefabName = $"{type}_{roomName}_{w}x{h}";
            string prefabPath = $"{_savePath}/{prefabName}.prefab";

            // Skip if exists
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existingPrefab != null)
            {
                string soPath = $"{_savePath}/{prefabName}_Template.asset";
                return AssetDatabase.LoadAssetAtPath<RoomTemplate>(soPath);
            }

            // Build hierarchy
            GameObject root = new GameObject(prefabName);
            var roomInstance = root.AddComponent<RoomInstance>();
            roomInstance.roomType = type;
            roomInstance.widthInCells = w;
            roomInstance.heightInCells = h;

            BuildFloor(root, w, h, type);
            BuildWallSockets(root, roomInstance, w, h);
            BuildDecoration(root, type, w, h);

            // Save
            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            Debug.Log($"[RoomGen] Prefab: {prefabPath}");

            RoomTemplate template = null;
            if (createSO && prefabAsset != null)
                template = CreateTemplateSO(prefabAsset, prefabName, type, roomName, w, h);

            return template;
        }

        // =============================================================
        // FLOOR CONSTRUCTION
        // =============================================================
        //
        // Unity Plane primitive:
        //   - Default size: 10 × 10 world units (vertices from -5 to +5 on X and Z)
        //   - To make it W*cellSize × H*cellSize, scale by (W*cellSize/10, 1, H*cellSize/10)
        //   - Has a MeshCollider by default — we replace it with a thin BoxCollider
        //
        // Example: 2×1 room, cellSize=16
        //   sizeX = 32, sizeZ = 16
        //   Plane scale = (3.2, 1, 1.6)
        //   Floor covers local (-16, 0, -8) to (16, 0, 8)

        private void BuildFloor(GameObject root, int w, int h, RoomType type)
        {
            float sizeX = w * _cellSize;
            float sizeZ = h * _cellSize;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(root.transform);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);

            // Replace MeshCollider with a thin BoxCollider for accurate physics
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
            var boxCol = floor.AddComponent<BoxCollider>();
            // BoxCollider on a scaled Plane: the Plane mesh goes from -5 to +5,
            // so in unscaled local space, center=(0,0,0), size=(10, 0, 10).
            // We set a thin box covering the same area.
            boxCol.center = Vector3.zero;
            boxCol.size = new Vector3(10f, FLOOR_THICKNESS / floor.transform.localScale.y, 10f);

            floor.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial($"Floor_{type}", GetFloorColor(type));
        }

        // =============================================================
        // WALL / SOCKET CONSTRUCTION
        // =============================================================
        //
        // For a room with W×H cells:
        //   halfW = W * cellSize / 2
        //   halfH = H * cellSize / 2
        //
        // Cell (cx, cy) has its center in local space at:
        //   cellLocalX = -halfW + (cx + 0.5) * cellSize
        //   cellLocalZ = -halfH + (cy + 0.5) * cellSize
        //
        // Sockets exist only on OUTER edges:
        //   - North edge: cells where cy == H-1, socket faces +Z
        //   - South edge: cells where cy == 0,   socket faces -Z
        //   - East edge:  cells where cx == W-1, socket faces +X
        //   - West edge:  cells where cx == 0,   socket faces -X
        //
        // Socket GO position = cell center projected to the room boundary:
        //   North: (cellLocalX, 0, +halfH)
        //   South: (cellLocalX, 0, -halfH)
        //   East:  (+halfW, 0, cellLocalZ)
        //   West:  (-halfW, 0, cellLocalZ)
        //
        // Wall (child of socket) is offset INWARD by WALL_THICKNESS/2:
        //   North wall: localPos = (0, WALL_HEIGHT/2, -WALL_THICKNESS/2)
        //   South wall: localPos = (0, WALL_HEIGHT/2, +WALL_THICKNESS/2)
        //   East  wall: localPos = (-WALL_THICKNESS/2, WALL_HEIGHT/2, 0)
        //   West  wall: localPos = (+WALL_THICKNESS/2, WALL_HEIGHT/2, 0)
        //
        // Wall scale:
        //   Horizontal (N/S): (cellSize, WALL_HEIGHT, WALL_THICKNESS)
        //   Vertical   (E/W): (WALL_THICKNESS, WALL_HEIGHT, cellSize)
        //
        // This ensures:
        //   - Walls never protrude outside the room's grid footprint
        //   - Adjacent rooms' walls don't overlap
        //   - Each wall covers exactly one cell's edge width

        private void BuildWallSockets(GameObject root, RoomInstance room, int w, int h)
        {
            var socketContainer = new GameObject("Sockets");
            socketContainer.transform.SetParent(root.transform);
            socketContainer.transform.localPosition = Vector3.zero;

            float halfW = w * _cellSize * 0.5f;
            float halfH = h * _cellSize * 0.5f;
            var wallMat = GetOrCreateMaterial("WallMat", new Color(0.5f, 0.4f, 0.35f));

            // North & South edges
            for (int cx = 0; cx < w; cx++)
            {
                float cellLocalX = -halfW + (cx + 0.5f) * _cellSize;

                // North socket: top row, facing +Z
                BuildSocket(socketContainer, room, wallMat,
                    dir: Direction.North,
                    cellOffset: new Vector2Int(cx, h - 1),
                    socketLocalPos: new Vector3(cellLocalX, 0f, +halfH),
                    wallLocalOffset: new Vector3(0f, WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f),
                    wallScale: new Vector3(_cellSize, WALL_HEIGHT, WALL_THICKNESS));

                // South socket: bottom row, facing -Z
                BuildSocket(socketContainer, room, wallMat,
                    dir: Direction.South,
                    cellOffset: new Vector2Int(cx, 0),
                    socketLocalPos: new Vector3(cellLocalX, 0f, -halfH),
                    wallLocalOffset: new Vector3(0f, WALL_HEIGHT * 0.5f, +WALL_THICKNESS * 0.5f),
                    wallScale: new Vector3(_cellSize, WALL_HEIGHT, WALL_THICKNESS));
            }

            // East & West edges
            for (int cy = 0; cy < h; cy++)
            {
                float cellLocalZ = -halfH + (cy + 0.5f) * _cellSize;

                // East socket: rightmost column, facing +X
                BuildSocket(socketContainer, room, wallMat,
                    dir: Direction.East,
                    cellOffset: new Vector2Int(w - 1, cy),
                    socketLocalPos: new Vector3(+halfW, 0f, cellLocalZ),
                    wallLocalOffset: new Vector3(-WALL_THICKNESS * 0.5f, WALL_HEIGHT * 0.5f, 0f),
                    wallScale: new Vector3(WALL_THICKNESS, WALL_HEIGHT, _cellSize));

                // West socket: leftmost column, facing -X
                BuildSocket(socketContainer, room, wallMat,
                    dir: Direction.West,
                    cellOffset: new Vector2Int(0, cy),
                    socketLocalPos: new Vector3(-halfW, 0f, cellLocalZ),
                    wallLocalOffset: new Vector3(+WALL_THICKNESS * 0.5f, WALL_HEIGHT * 0.5f, 0f),
                    wallScale: new Vector3(WALL_THICKNESS, WALL_HEIGHT, _cellSize));
            }
        }

        /// <summary>
        /// Build one socket with its wall and open state.
        /// </summary>
        private void BuildSocket(
            GameObject parent, RoomInstance room, Material wallMat,
            Direction dir, Vector2Int cellOffset,
            Vector3 socketLocalPos, Vector3 wallLocalOffset, Vector3 wallScale)
        {
            // --- Socket GameObject ---
            var socketGO = new GameObject($"Socket_{dir}_{cellOffset.x}_{cellOffset.y}");
            socketGO.transform.SetParent(parent.transform);
            socketGO.transform.localPosition = socketLocalPos;

            var socket = socketGO.AddComponent<DoorSocket>();
            socket.socketDirection = dir;
            socket.cellOffset = cellOffset;

            // --- Open state (empty marker, activates when door is open) ---
            var openGO = new GameObject("OpenState");
            openGO.transform.SetParent(socketGO.transform);
            openGO.transform.localPosition = Vector3.zero;
            openGO.SetActive(false);

            // --- Wall state (Cube, visible when door is closed / walled) ---
            var wallGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallGO.name = "WallState";
            wallGO.transform.SetParent(socketGO.transform);
            wallGO.transform.localPosition = wallLocalOffset;
            wallGO.transform.localScale = wallScale;
            wallGO.GetComponent<MeshRenderer>().sharedMaterial = wallMat;

            // BoxCollider is auto-created by CreatePrimitive and matches the scale. ✓

            socket.openState = openGO;
            socket.wallState = wallGO;
            room.sockets.Add(socket);
        }

        // =============================================================
        // ROOM DECORATION (placeholder visuals per type)
        // =============================================================

        private void BuildDecoration(GameObject root, RoomType type, int w, int h)
        {
            var deco = new GameObject("Decorations");
            deco.transform.SetParent(root.transform);
            deco.transform.localPosition = Vector3.zero;

            // All decorations stay well within room boundaries
            float inset = WALL_THICKNESS + 0.5f; // keep away from walls
            float maxExtentX = (w * _cellSize * 0.5f) - inset;
            float maxExtentZ = (h * _cellSize * 0.5f) - inset;

            switch (type)
            {
                case RoomType.Start:
                    AddPrimitive(deco, PrimitiveType.Cylinder, "StartMarker",
                        Vector3.up * 1f, new Vector3(0.6f, 1f, 0.6f),
                        new Color(0.2f, 0.8f, 0.2f));
                    break;

                case RoomType.Boss:
                    // Platform
                    float platR = Mathf.Min(maxExtentX, maxExtentZ) * 0.7f;
                    AddPrimitive(deco, PrimitiveType.Cylinder, "BossArena",
                        new Vector3(0, 0.1f, 0), new Vector3(platR, 0.1f, platR),
                        new Color(0.7f, 0.1f, 0.1f));
                    // Corner pillars
                    float px = maxExtentX * 0.7f, pz = maxExtentZ * 0.7f;
                    foreach (var (nx, nz) in new[] { (px, pz), (-px, pz), (px, -pz), (-px, -pz) })
                        AddPrimitive(deco, PrimitiveType.Cylinder, "Pillar",
                            new Vector3(nx, 1.2f, nz), new Vector3(0.4f, 1.2f, 0.4f),
                            new Color(0.5f, 0.1f, 0.1f));
                    break;

                case RoomType.MiniBoss:
                    float r = Mathf.Min(maxExtentX, maxExtentZ) * 0.5f;
                    AddPrimitive(deco, PrimitiveType.Cylinder, "ElitePlatform",
                        new Vector3(0, 0.1f, 0), new Vector3(r, 0.1f, r),
                        new Color(0.6f, 0.3f, 0.1f));
                    break;

                case RoomType.Loot:
                    AddPrimitive(deco, PrimitiveType.Cube, "Chest",
                        new Vector3(0, 0.4f, 0), new Vector3(1.2f, 0.8f, 0.8f),
                        new Color(0.7f, 0.6f, 0.1f));
                    break;

                case RoomType.Puzzle:
                    AddPrimitive(deco, PrimitiveType.Sphere, "PuzzleOrb",
                        new Vector3(0, 2f, 0), Vector3.one * 1.2f,
                        new Color(0.1f, 0.4f, 0.8f));
                    break;

                case RoomType.Shop:
                    AddPrimitive(deco, PrimitiveType.Cube, "Counter",
                        new Vector3(0, 0.5f, maxExtentZ * 0.5f), new Vector3(Mathf.Min(3f, maxExtentX), 1f, 0.6f),
                        new Color(0.4f, 0.3f, 0.15f));
                    break;

                case RoomType.SafeRoom:
                    AddPrimitive(deco, PrimitiveType.Sphere, "Campfire",
                        new Vector3(0, 0.3f, 0), Vector3.one * 0.7f,
                        new Color(1f, 0.6f, 0.1f));
                    break;

                case RoomType.Trap:
                    for (int i = -1; i <= 1; i++)
                    {
                        float sx = Mathf.Clamp(i * 2f, -maxExtentX, maxExtentX);
                        AddPrimitive(deco, PrimitiveType.Cube, $"Spike_{i}",
                            new Vector3(sx, 0.15f, 0), new Vector3(0.3f, 0.3f, 0.3f),
                            new Color(0.6f, 0.6f, 0.6f));
                    }
                    break;

                case RoomType.SecretRoom:
                    AddPrimitive(deco, PrimitiveType.Sphere, "Gem",
                        new Vector3(0, 1.5f, 0), Vector3.one * 0.5f,
                        new Color(0.6f, 0.1f, 0.8f));
                    break;

                case RoomType.StaircaseUp:
                    BuildStaircase(deco, goingUp: true, maxExtentZ);
                    break;

                case RoomType.StaircaseDown:
                    BuildStaircase(deco, goingUp: false, maxExtentZ);
                    break;
            }
        }

        private void BuildStaircase(GameObject parent, bool goingUp, float maxExtentZ)
        {
            int steps = 4;
            float stepH = 0.5f;
            float stepD = Mathf.Min(1.5f, maxExtentZ * 2f / steps);
            float startZ = -steps * stepD * 0.5f;
            Color c = goingUp ? new Color(0.3f, 0.3f, 0.5f) : new Color(0.5f, 0.3f, 0.3f);
            var mat = GetOrCreateMaterial(goingUp ? "StepUp" : "StepDown", c);

            for (int i = 0; i < steps; i++)
            {
                float y = goingUp ? i * stepH : (steps - 1 - i) * stepH;
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step_{i}";
                step.transform.SetParent(parent.transform);
                step.transform.localPosition = new Vector3(0, y + stepH * 0.5f, startZ + i * stepD);
                step.transform.localScale = new Vector3(Mathf.Min(_cellSize * 0.4f, 5f), stepH, stepD);
                step.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        private GameObject AddPrimitive(GameObject parent, PrimitiveType prim, string name,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(prim);
            go.name = name;
            go.transform.SetParent(parent.transform);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<MeshRenderer>().sharedMaterial = GetOrCreateMaterial(name + "Mat", color);
            // Remove collider from decorations — they're visual placeholders only
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            return go;
        }

        // =============================================================
        // TEMPLATE SO
        // =============================================================

        private RoomTemplate CreateTemplateSO(GameObject prefab, string prefabName,
            RoomType type, string displayName, int w, int h)
        {
            string soPath = $"{_savePath}/{prefabName}_Template.asset";
            var existing = AssetDatabase.LoadAssetAtPath<RoomTemplate>(soPath);
            if (existing != null) return existing;

            var template = ScriptableObject.CreateInstance<RoomTemplate>();
            template.templateId = prefabName;
            template.displayName = displayName;
            template.roomType = type;
            template.widthInCells = w;
            template.heightInCells = h;
            template.prefab = prefab;
            template.spawnWeight = 1f;
            template.difficultyTier = type switch
            {
                RoomType.Boss => 10, RoomType.MiniBoss => 7,
                RoomType.Trap => 5, RoomType.Combat => 3, _ => 1
            };

            var roomInst = prefab.GetComponent<RoomInstance>();
            if (roomInst != null)
            {
                foreach (var socket in roomInst.sockets)
                {
                    template.sockets.Add(new SocketDefinition
                    {
                        direction = socket.socketDirection,
                        cellOffset = socket.cellOffset,
                        canConnect = true
                    });
                }
            }

            AssetDatabase.CreateAsset(template, soPath);
            return template;
        }

        // =============================================================
        // STATIC API
        // =============================================================

        public static int GenerateAllDefaultsForDatabase(
            RoomDatabase database,
            string savePath = "Assets/Prefabs/DungeonRooms",
            float cellSize = 16f,
            int variantsPerType = 2)
        {
            var window = CreateInstance<RoomTemplateGenerator>();
            window._savePath = savePath;
            window._cellSize = cellSize;
            window._targetDatabase = database;
            window._variantsPerType = variantsPerType;
            window._includeLargeVariants = true;
            int count = window.GenerateAllDefaults();
            DestroyImmediate(window);
            return count;
        }

        // =============================================================
        // UTILITIES
        // =============================================================

        private static Color GetFloorColor(RoomType type)
        {
            return type switch
            {
                RoomType.Start         => new Color(0.25f, 0.40f, 0.25f),
                RoomType.Boss          => new Color(0.35f, 0.15f, 0.15f),
                RoomType.MiniBoss      => new Color(0.35f, 0.25f, 0.15f),
                RoomType.Combat        => new Color(0.28f, 0.28f, 0.28f),
                RoomType.Loot          => new Color(0.35f, 0.32f, 0.18f),
                RoomType.Puzzle        => new Color(0.18f, 0.25f, 0.35f),
                RoomType.Shop          => new Color(0.25f, 0.35f, 0.22f),
                RoomType.SafeRoom      => new Color(0.22f, 0.32f, 0.38f),
                RoomType.SecretRoom    => new Color(0.30f, 0.18f, 0.30f),
                RoomType.Trap          => new Color(0.35f, 0.28f, 0.18f),
                RoomType.Corridor      => new Color(0.22f, 0.22f, 0.22f),
                RoomType.Junction      => new Color(0.24f, 0.24f, 0.24f),
                RoomType.StaircaseUp   => new Color(0.25f, 0.25f, 0.35f),
                RoomType.StaircaseDown => new Color(0.35f, 0.25f, 0.25f),
                _ => new Color(0.25f, 0.25f, 0.25f)
            };
        }

        private Material GetOrCreateMaterial(string matName, Color color)
        {
            string matDir = $"{_savePath}/Materials";
            EnsureFolder(matDir);
            string path = $"{matDir}/{matName}.mat";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath,
                assetPath.Replace("Assets/", "").Replace("Assets\\", ""));
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }
        }

        private struct RoomSpec
        {
            public RoomType type;
            public string name;
            public int width, height;
        }

        private class RoomTypeConfig
        {
            public string baseName;
            public (int w, int h)[] sizes;
            public RoomTypeConfig(string n, (int, int)[] s) { baseName = n; sizes = s; }
        }
    }
}
#endif
