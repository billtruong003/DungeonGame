using System.Collections.Generic;
using UnityEngine;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Graph;
using DungeonSystem.Layout;

namespace DungeonSystem.Runtime
{
    /// <summary>
    /// Phase 3: Instantiate room prefabs from the solved layout.
    /// Assigns templates, positions rooms, configures sockets, spawns corridors.
    /// </summary>
    public class RoomInstantiator
    {
        private readonly DungeonConfig _config;
        private readonly System.Random _rng;

        public RoomInstantiator(DungeonConfig config, System.Random rng)
        {
            _config = config;
            _rng = rng;
        }

        /// <summary>
        /// Instantiate all rooms and corridors for a floor.
        /// Returns a map of PlacedRoom → instantiated RoomInstance.
        /// </summary>
        public Dictionary<PlacedRoom, RoomInstance> Instantiate(
            FloorLayout layout, int floorIndex, Transform parent)
        {
            var instanceMap = new Dictionary<PlacedRoom, RoomInstance>();

            // 1. Instantiate rooms
            foreach (var placedRoom in layout.Rooms)
            {
                var instance = InstantiateRoom(placedRoom, floorIndex, parent);
                if (instance != null)
                    instanceMap[placedRoom] = instance;
            }

            // 2. Instantiate corridors
            foreach (var corridor in layout.Corridors)
            {
                InstantiateCorridor(corridor, floorIndex, parent);
            }

            // 3. Configure sockets (doors/walls)
            ConfigureAllSockets(layout, instanceMap);

            return instanceMap;
        }

        // ======================== ROOM INSTANTIATION ========================

        private RoomInstance InstantiateRoom(PlacedRoom placedRoom, int floorIndex, Transform parent)
        {
            var node = placedRoom.Node;

            // Use the template that was assigned during AssignTemplateSizes (same size the layout used)
            RoomTemplate template = node.AssignedTemplate;

            if (template == null || template.prefab == null)
            {
                // Fallback: generate a primitive room
                return CreatePrimitiveRoom(placedRoom, floorIndex, parent);
            }

            placedRoom.Template = template;

            // Instantiate prefab
            var go = Object.Instantiate(template.prefab, parent);
            go.name = $"{node.Type}_{node.Id}_{template.displayName}";

            var roomInstance = go.GetComponent<RoomInstance>();
            if (roomInstance == null)
                roomInstance = go.AddComponent<RoomInstance>();

            // Configure
            roomInstance.roomType = node.Type;
            roomInstance.widthInCells = placedRoom.Width;
            roomInstance.heightInCells = placedRoom.Height;
            roomInstance.SourceTemplate = template;
            roomInstance.GraphNode = node;
            roomInstance.CollectSockets();
            roomInstance.Initialize(placedRoom.GridPosition, node.Depth, _config.cellSize, floorIndex);

            return roomInstance;
        }

        /// <summary>
        /// Create a simple primitive room when no template exists.
        /// Uses a Plane for the floor + wall segments.
        /// </summary>
        private RoomInstance CreatePrimitiveRoom(PlacedRoom placedRoom, int floorIndex, Transform parent)
        {
            var node = placedRoom.Node;
            float cellSize = _config.cellSize;
            float sizeX = placedRoom.Width * cellSize;
            float sizeZ = placedRoom.Height * cellSize;

            var go = new GameObject($"{node.Type}_{node.Id}_Primitive");
            go.transform.SetParent(parent);

            var roomInstance = go.AddComponent<RoomInstance>();
            roomInstance.roomType = node.Type;
            roomInstance.widthInCells = placedRoom.Width;
            roomInstance.heightInCells = placedRoom.Height;
            roomInstance.GraphNode = node;

            // --- FLOOR: Use a Plane (10x10 default scale, so we scale accordingly) ---
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(go.transform);
            floor.transform.localPosition = Vector3.zero;
            // Plane is 10x10 by default, so scale = desired size / 10
            floor.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);

            var floorRenderer = floor.GetComponent<MeshRenderer>();
            floorRenderer.sharedMaterial = GetRoomMaterial(node.Type);

            // Replace MeshCollider with thin BoxCollider
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
            var boxCol = floor.AddComponent<BoxCollider>();
            boxCol.center = Vector3.zero;
            boxCol.size = new Vector3(10f, FLOOR_THICKNESS, 10f);

            // --- SOCKETS: Create one per cell per edge ---
            CreatePrimitiveSockets(go, roomInstance, placedRoom, cellSize);

            roomInstance.Initialize(placedRoom.GridPosition, node.Depth, cellSize, floorIndex);

            return roomInstance;
        }

        private void CreatePrimitiveSockets(GameObject root, RoomInstance room, PlacedRoom placed, float cellSize)
        {
            float halfCellSize = cellSize * 0.5f;

            // For each cell, check each edge
            for (int x = 0; x < placed.Width; x++)
            {
                for (int y = 0; y < placed.Height; y++)
                {
                    Vector2Int cellOffset = new Vector2Int(x, y);

                    // Only create sockets on outer edges
                    if (y == placed.Height - 1) CreateSocket(root, room, cellOffset, Direction.North, cellSize);
                    if (y == 0) CreateSocket(root, room, cellOffset, Direction.South, cellSize);
                    if (x == placed.Width - 1) CreateSocket(root, room, cellOffset, Direction.East, cellSize);
                    if (x == 0) CreateSocket(root, room, cellOffset, Direction.West, cellSize);
                }
            }
        }

        private const float WALL_HEIGHT = 3.0f;
        private const float WALL_THICKNESS = 0.5f;
        private const float FLOOR_THICKNESS = 0.1f;

        private void CreateSocket(GameObject root, RoomInstance room, Vector2Int cellOffset, Direction dir, float cellSize)
        {
            var socketGO = new GameObject($"Socket_{dir}_{cellOffset.x}_{cellOffset.y}");
            socketGO.transform.SetParent(root.transform);

            float halfW = room.widthInCells * cellSize * 0.5f;
            float halfH = room.heightInCells * cellSize * 0.5f;
            float cellLocalX = -halfW + (cellOffset.x + 0.5f) * cellSize;
            float cellLocalZ = -halfH + (cellOffset.y + 0.5f) * cellSize;

            // Socket at room boundary edge
            Vector3 socketPos = dir switch
            {
                Direction.North => new Vector3(cellLocalX, 0f, +halfH),
                Direction.South => new Vector3(cellLocalX, 0f, -halfH),
                Direction.East => new Vector3(+halfW, 0f, cellLocalZ),
                Direction.West => new Vector3(-halfW, 0f, cellLocalZ),
                _ => Vector3.zero
            };
            socketGO.transform.localPosition = socketPos;

            var socket = socketGO.AddComponent<DoorSocket>();
            socket.socketDirection = dir;
            socket.cellOffset = cellOffset;

            // Wall: offset INWARD, sized to exactly one cell edge
            bool isHorizontal = dir == Direction.North || dir == Direction.South;
            Vector3 wallOffset = dir switch
            {
                Direction.North => new Vector3(0f, WALL_HEIGHT * 0.5f, -WALL_THICKNESS * 0.5f),
                Direction.South => new Vector3(0f, WALL_HEIGHT * 0.5f, +WALL_THICKNESS * 0.5f),
                Direction.East => new Vector3(-WALL_THICKNESS * 0.5f, WALL_HEIGHT * 0.5f, 0f),
                Direction.West => new Vector3(+WALL_THICKNESS * 0.5f, WALL_HEIGHT * 0.5f, 0f),
                _ => Vector3.zero
            };
            Vector3 wallScale = isHorizontal
                ? new Vector3(cellSize, WALL_HEIGHT, WALL_THICKNESS)
                : new Vector3(WALL_THICKNESS, WALL_HEIGHT, cellSize);

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallState";
            wall.transform.SetParent(socketGO.transform);
            wall.transform.localPosition = wallOffset;
            wall.transform.localScale = wallScale;
            wall.GetComponent<MeshRenderer>().sharedMaterial = GetWallMaterial();

            var openGO = new GameObject("OpenState");
            openGO.transform.SetParent(socketGO.transform);
            openGO.transform.localPosition = Vector3.zero;
            openGO.SetActive(false);

            socket.wallState = wall;
            socket.openState = openGO;
            room.sockets.Add(socket);
        }

        // ======================== CORRIDOR INSTANTIATION ========================

        private void InstantiateCorridor(CorridorSegment corridor, int floorIndex, Transform parent)
        {
            if (corridor.Cells.Count == 0) return;

            float cellSize = _config.cellSize;
            var corridorParent = new GameObject($"Corridor_{corridor.RoomA.Node.Id}_to_{corridor.RoomB.Node.Id}");
            corridorParent.transform.SetParent(parent);

            foreach (var cell in corridor.Cells)
            {
                // Each corridor cell is a small plane
                var cellGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
                cellGO.name = $"CorridorCell_{cell.x}_{cell.y}";
                cellGO.transform.SetParent(corridorParent.transform);

                float worldX = cell.x * cellSize + cellSize * 0.5f;
                float worldZ = cell.y * cellSize + cellSize * 0.5f;
                float worldY = floorIndex * _config.floorYSpacing;

                cellGO.transform.position = new Vector3(worldX, worldY, worldZ);
                cellGO.transform.localScale = new Vector3(cellSize / 10f, 1f, cellSize / 10f);

                cellGO.GetComponent<MeshRenderer>().sharedMaterial = GetCorridorMaterial();

                // Fix collider
                Object.DestroyImmediate(cellGO.GetComponent<MeshCollider>());
                var col = cellGO.AddComponent<BoxCollider>();
                col.center = Vector3.zero;
                col.size = new Vector3(10f, FLOOR_THICKNESS, 10f);
            }
        }

        // ======================== SOCKET CONFIGURATION ========================

        /// <summary>
        /// For every room, check each socket against the layout's connection data.
        /// Open sockets that have a neighbor, wall the rest.
        /// </summary>
        private void ConfigureAllSockets(FloorLayout layout, Dictionary<PlacedRoom, RoomInstance> instanceMap)
        {
            foreach (var (placedRoom, roomInstance) in instanceMap)
            {
                roomInstance.WallAllSockets();

                foreach (var socket in roomInstance.sockets)
                {
                    // Calculate the actual grid cell this socket represents
                    Vector2Int socketGridCell = placedRoom.GridPosition + socket.cellOffset;
                    Vector2Int neighborCell = socketGridCell + GridUtils.GetOffset(socket.socketDirection);

                    // Check if there's a connection to this neighbor
                    var doorKey = new DoorConnection(socketGridCell, socket.socketDirection);
                    if (placedRoom.Connections.TryGetValue(doorKey, out var neighborRoom))
                    {
                        // Find the corresponding edge in the graph
                        bool isSecret = false;
                        bool isLocked = false;

                        if (placedRoom.Node != null && neighborRoom.Node != null)
                        {
                            foreach (var edge in placedRoom.Node.Edges)
                            {
                                if (edge.GetOther(placedRoom.Node) == neighborRoom.Node)
                                {
                                    isSecret = edge.IsSecret;
                                    isLocked = edge.IsLocked;
                                    break;
                                }
                            }
                        }

                        socket.SetConnected(true, isLocked, isSecret);

                        // Link to neighbor room instance
                        if (instanceMap.TryGetValue(neighborRoom, out var neighborInstance))
                            socket.ConnectedRoom = neighborInstance;
                    }
                    else
                    {
                        // Check if neighbor cell is a corridor
                        if (layout.OccupiedCells.Contains(neighborCell) && !layout.CellMap.ContainsKey(neighborCell))
                        {
                            // It's a corridor cell — open the door
                            socket.SetConnected(true);
                        }
                    }
                }
            }
        }

        // ======================== MATERIALS (CACHED) ========================

        private static Material _roomFloorMat;
        private static Material _wallMat;
        private static Material _corridorMat;
        private static readonly Dictionary<RoomType, Material> _roomTypeMats = new Dictionary<RoomType, Material>();

        private Material GetRoomMaterial(RoomType type)
        {
            if (_roomTypeMats.TryGetValue(type, out var cached) && cached != null)
                return cached;

            Color color = type switch
            {
                RoomType.Start => new Color(0.2f, 0.5f, 0.2f),
                RoomType.Boss => new Color(0.6f, 0.1f, 0.1f),
                RoomType.MiniBoss => new Color(0.5f, 0.2f, 0.1f),
                RoomType.Combat => new Color(0.3f, 0.3f, 0.3f),
                RoomType.Loot => new Color(0.5f, 0.4f, 0.1f),
                RoomType.Puzzle => new Color(0.1f, 0.3f, 0.5f),
                RoomType.Shop => new Color(0.3f, 0.5f, 0.2f),
                RoomType.SafeRoom => new Color(0.2f, 0.4f, 0.5f),
                RoomType.SecretRoom => new Color(0.4f, 0.1f, 0.4f),
                RoomType.Trap => new Color(0.5f, 0.3f, 0.1f),
                RoomType.StaircaseUp => new Color(0.3f, 0.3f, 0.5f),
                RoomType.StaircaseDown => new Color(0.5f, 0.3f, 0.3f),
                _ => new Color(0.25f, 0.25f, 0.25f)
            };

            var mat = CreateMaterial(color);
            _roomTypeMats[type] = mat;
            return mat;
        }

        private Material GetWallMaterial()
        {
            if (_wallMat != null) return _wallMat;
            _wallMat = CreateMaterial(new Color(0.45f, 0.35f, 0.3f));
            return _wallMat;
        }

        private Material GetCorridorMaterial()
        {
            if (_corridorMat != null) return _corridorMat;
            _corridorMat = CreateMaterial(new Color(0.2f, 0.2f, 0.22f));
            return _corridorMat;
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader) { color = color };
            return mat;
        }
    }
}