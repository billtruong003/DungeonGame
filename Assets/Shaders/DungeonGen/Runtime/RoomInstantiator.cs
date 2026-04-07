using System.Collections.Generic;
using UnityEngine;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Graph;
using DungeonSystem.Layout;

namespace DungeonSystem.Runtime
{
    public class RoomInstantiator
    {
        readonly DungeonConfig _config;
        readonly System.Random _rng;
        readonly PieceAssembler _assembler;

        const float WALL_HEIGHT = 3.0f;
        const float WALL_THICKNESS = 0.5f;
        const float FLOOR_THICKNESS = 0.1f;

        public RoomInstantiator(DungeonConfig config, System.Random rng)
        {
            _config = config;
            _rng = rng;

            if (config.piecePalette != null)
                _assembler = new PieceAssembler(config.piecePalette, config, rng);
        }

        public Dictionary<PlacedRoom, RoomInstance> Instantiate(
            FloorLayout layout, int floorIndex, Transform parent)
        {
            var instanceMap = new Dictionary<PlacedRoom, RoomInstance>();

            foreach (var placedRoom in layout.Rooms)
            {
                var instance = InstantiateRoom(placedRoom, floorIndex, parent, layout);
                if (instance != null)
                    instanceMap[placedRoom] = instance;
            }

            foreach (var corridor in layout.Corridors)
                InstantiateCorridor(corridor, layout, floorIndex, parent);

            ConfigureAllSockets(layout, instanceMap);

            return instanceMap;
        }

        RoomInstance InstantiateRoom(PlacedRoom placedRoom, int floorIndex, Transform parent, FloorLayout layout)
        {
            var node = placedRoom.Node;

            if (_assembler != null)
                return InstantiateFromPalette(placedRoom, floorIndex, parent, layout);

            RoomTemplate template = node.AssignedTemplate;
            if (template != null && template.prefab != null)
                return InstantiateFromTemplate(placedRoom, template, floorIndex, parent);

            return InstantiateFromPrimitives(placedRoom, floorIndex, parent);
        }

        RoomInstance InstantiateFromPalette(PlacedRoom placedRoom, int floorIndex, Transform parent, FloorLayout layout)
        {
            var node = placedRoom.Node;

            var go = new GameObject($"{node.Type}_{node.Id}_Assembled");
            go.transform.SetParent(parent);

            var room = go.AddComponent<RoomInstance>();
            room.roomType = node.Type;
            room.widthInCells = placedRoom.Width;
            room.heightInCells = placedRoom.Height;
            room.GraphNode = node;
            room.Initialize(placedRoom.GridPosition, node.Depth, _config.cellSize, floorIndex);

            _assembler.AssembleRoom(placedRoom, room, layout);
            room.CollectSockets();

            return room;
        }

        RoomInstance InstantiateFromTemplate(PlacedRoom placedRoom, RoomTemplate template, int floorIndex, Transform parent)
        {
            var node = placedRoom.Node;
            placedRoom.Template = template;

            var go = Object.Instantiate(template.prefab, parent);
            go.name = $"{node.Type}_{node.Id}_{template.displayName}";

            var room = go.GetComponent<RoomInstance>();
            if (room == null) room = go.AddComponent<RoomInstance>();

            room.roomType = node.Type;
            room.widthInCells = placedRoom.Width;
            room.heightInCells = placedRoom.Height;
            room.SourceTemplate = template;
            room.GraphNode = node;
            room.CollectSockets();
            room.Initialize(placedRoom.GridPosition, node.Depth, _config.cellSize, floorIndex);

            return room;
        }

        RoomInstance InstantiateFromPrimitives(PlacedRoom placedRoom, int floorIndex, Transform parent)
        {
            var node = placedRoom.Node;
            float cellSize = _config.cellSize;
            float sizeX = placedRoom.Width * cellSize;
            float sizeZ = placedRoom.Height * cellSize;

            var go = new GameObject($"{node.Type}_{node.Id}_Primitive");
            go.transform.SetParent(parent);

            var room = go.AddComponent<RoomInstance>();
            room.roomType = node.Type;
            room.widthInCells = placedRoom.Width;
            room.heightInCells = placedRoom.Height;
            room.GraphNode = node;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(go.transform);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(sizeX / 10f, 1f, sizeZ / 10f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = GetRoomMaterial(node.Type);
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
            var floorCol = floor.AddComponent<BoxCollider>();
            floorCol.center = Vector3.zero;
            floorCol.size = new Vector3(10f, FLOOR_THICKNESS, 10f);

            BuildPrimitiveSockets(go, room, placedRoom, cellSize);
            room.Initialize(placedRoom.GridPosition, node.Depth, cellSize, floorIndex);

            return room;
        }

        void BuildPrimitiveSockets(GameObject root, RoomInstance room, PlacedRoom placed, float cellSize)
        {
            float halfW = room.widthInCells * cellSize * 0.5f;
            float halfH = room.heightInCells * cellSize * 0.5f;
            float actualWallHeight = WALL_HEIGHT * Mathf.Max(1, _config.roomHeightMultiplier);
            float baseWallY = _config.wallHeightOffset + actualWallHeight * 0.5f;

            for (int x = 0; x < placed.Width; x++)
                for (int y = 0; y < placed.Height; y++)
                {
                    var co = new Vector2Int(x, y);
                    if (y == placed.Height - 1) BuildPrimitiveSocket(root, room, co, Direction.North, cellSize, halfW, halfH, actualWallHeight, baseWallY);
                    if (y == 0) BuildPrimitiveSocket(root, room, co, Direction.South, cellSize, halfW, halfH, actualWallHeight, baseWallY);
                    if (x == placed.Width - 1) BuildPrimitiveSocket(root, room, co, Direction.East, cellSize, halfW, halfH, actualWallHeight, baseWallY);
                    if (x == 0) BuildPrimitiveSocket(root, room, co, Direction.West, cellSize, halfW, halfH, actualWallHeight, baseWallY);
                }
        }

        void BuildPrimitiveSocket(GameObject root, RoomInstance room, Vector2Int cellOffset,
            Direction dir, float cellSize, float halfW, float halfH, float actualWallHeight, float baseWallY)
        {
            float cellX = -halfW + (cellOffset.x + 0.5f) * cellSize;
            float cellZ = -halfH + (cellOffset.y + 0.5f) * cellSize;

            Vector3 socketPos = dir switch
            {
                Direction.North => new Vector3(cellX, 0f, +halfH),
                Direction.South => new Vector3(cellX, 0f, -halfH),
                Direction.East => new Vector3(+halfW, 0f, cellZ),
                Direction.West => new Vector3(-halfW, 0f, cellZ),
                _ => Vector3.zero
            };

            var socketGO = new GameObject($"Socket_{dir}_{cellOffset.x}_{cellOffset.y}");
            socketGO.transform.SetParent(root.transform);
            socketGO.transform.localPosition = socketPos;

            var socket = socketGO.AddComponent<DoorSocket>();
            socket.socketDirection = dir;
            socket.cellOffset = cellOffset;

            bool isHorizontal = dir == Direction.North || dir == Direction.South;

            Vector3 wallOffset = dir switch
            {
                Direction.North => new Vector3(0f, baseWallY, -WALL_THICKNESS * 0.5f),
                Direction.South => new Vector3(0f, baseWallY, +WALL_THICKNESS * 0.5f),
                Direction.East => new Vector3(-WALL_THICKNESS * 0.5f, baseWallY, 0f),
                Direction.West => new Vector3(+WALL_THICKNESS * 0.5f, baseWallY, 0f),
                _ => Vector3.zero
            };

            Vector3 wallScale = isHorizontal
                ? new Vector3(cellSize, actualWallHeight, WALL_THICKNESS)
                : new Vector3(WALL_THICKNESS, actualWallHeight, cellSize);

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

        void InstantiateCorridor(CorridorSegment corridor, FloorLayout layout, int floorIndex, Transform parent)
        {
            if (corridor.Cells.Count == 0) return;

            float cellSize = _config.cellSize;
            float yOffset = floorIndex * _config.floorYSpacing;

            var corridorParent = new GameObject($"Corridor_{corridor.RoomA.Node.Id}_to_{corridor.RoomB.Node.Id}");
            corridorParent.transform.SetParent(parent);

            if (_assembler != null)
            {
                foreach (var cell in corridor.Cells)
                    _assembler.AssembleCorridorCell(cell, layout, yOffset, corridorParent.transform);
                return;
            }

            foreach (var cell in corridor.Cells)
                BuildPrimitiveCorridorCell(cell, layout, cellSize, yOffset, corridorParent.transform);
        }

        void BuildPrimitiveCorridorCell(Vector2Int cell, FloorLayout layout, float cellSize, float yOffset, Transform parent)
        {
            float halfCell = cellSize * 0.5f;

            var cellGO = new GameObject($"CorridorCell_{cell.x}_{cell.y}");
            cellGO.transform.SetParent(parent);
            cellGO.transform.position = new Vector3(
                cell.x * cellSize + halfCell, yOffset, cell.y * cellSize + halfCell);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.SetParent(cellGO.transform);
            floor.transform.localPosition = Vector3.zero;
            floor.transform.localScale = new Vector3(cellSize / 10f, 1f, cellSize / 10f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = GetCorridorMaterial();
            Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
            var floorCol = floor.AddComponent<BoxCollider>();
            floorCol.center = Vector3.zero;
            floorCol.size = new Vector3(10f, FLOOR_THICKNESS, 10f);

            float actualWallHeight = WALL_HEIGHT * Mathf.Max(1, _config.roomHeightMultiplier);
            float baseWallY = _config.wallHeightOffset + actualWallHeight * 0.5f;

            for (int d = 0; d < 4; d++)
            {
                Direction dir = (Direction)d;
                Vector2Int neighbor = cell + GridUtils.GetOffset(dir);
                if (layout.OccupiedCells.Contains(neighbor)) continue;

                bool isHorizontal = dir == Direction.North || dir == Direction.South;

                Vector3 wallPos = dir switch
                {
                    Direction.North => new Vector3(0, baseWallY, +halfCell - WALL_THICKNESS * 0.5f),
                    Direction.South => new Vector3(0, baseWallY, -halfCell + WALL_THICKNESS * 0.5f),
                    Direction.East => new Vector3(+halfCell - WALL_THICKNESS * 0.5f, baseWallY, 0),
                    Direction.West => new Vector3(-halfCell + WALL_THICKNESS * 0.5f, baseWallY, 0),
                    _ => Vector3.zero
                };

                Vector3 wallScale = isHorizontal
                    ? new Vector3(cellSize, actualWallHeight, WALL_THICKNESS)
                    : new Vector3(WALL_THICKNESS, actualWallHeight, cellSize);

                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = $"Wall_{dir}";
                wall.transform.SetParent(cellGO.transform);
                wall.transform.localPosition = wallPos;
                wall.transform.localScale = wallScale;
                wall.GetComponent<MeshRenderer>().sharedMaterial = GetWallMaterial();
            }
        }

        void ConfigureAllSockets(FloorLayout layout, Dictionary<PlacedRoom, RoomInstance> instanceMap)
        {
            foreach (var (placedRoom, roomInstance) in instanceMap)
            {
                roomInstance.WallAllSockets();

                foreach (var socket in roomInstance.sockets)
                {
                    Vector2Int socketGridCell = placedRoom.GridPosition + socket.cellOffset;
                    Vector2Int neighborCell = socketGridCell + GridUtils.GetOffset(socket.socketDirection);

                    var doorKey = new DoorConnection(socketGridCell, socket.socketDirection);

                    if (placedRoom.Connections.TryGetValue(doorKey, out var neighborRoom))
                    {
                        bool isSecret = false;
                        bool isLocked = false;

                        if (placedRoom.Node != null && neighborRoom.Node != null)
                            foreach (var edge in placedRoom.Node.Edges)
                                if (edge.GetOther(placedRoom.Node) == neighborRoom.Node)
                                {
                                    isSecret = edge.IsSecret;
                                    isLocked = edge.IsLocked;
                                    break;
                                }

                        socket.SetConnected(true, isLocked, isSecret);

                        if (instanceMap.TryGetValue(neighborRoom, out var neighborInstance))
                            socket.ConnectedRoom = neighborInstance;
                    }
                    else if (layout.OccupiedCells.Contains(neighborCell) && !layout.CellMap.ContainsKey(neighborCell))
                    {
                        socket.SetConnected(true);
                    }
                    else if (layout.CellMap.TryGetValue(neighborCell, out var adjacentRoom)
                        && adjacentRoom != placedRoom
                        && placedRoom.Node != null
                        && placedRoom.Node.IsConnectedTo(adjacentRoom.Node))
                    {
                        bool isSecret = false;
                        bool isLocked = false;

                        foreach (var edge in placedRoom.Node.Edges)
                            if (edge.GetOther(placedRoom.Node) == adjacentRoom.Node)
                            {
                                isSecret = edge.IsSecret;
                                isLocked = edge.IsLocked;
                                break;
                            }

                        socket.SetConnected(true, isLocked, isSecret);

                        if (instanceMap.TryGetValue(adjacentRoom, out var adjInstance))
                            socket.ConnectedRoom = adjInstance;
                    }
                }
            }
        }

        static Material _wallMat;
        static Material _corridorMat;
        static readonly Dictionary<RoomType, Material> _roomTypeMats = new();

        Material GetRoomMaterial(RoomType type)
        {
            if (_roomTypeMats.TryGetValue(type, out var cached) && cached != null) return cached;
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

        Material GetWallMaterial()
        {
            if (_wallMat != null) return _wallMat;
            _wallMat = CreateMaterial(new Color(0.45f, 0.35f, 0.3f));
            return _wallMat;
        }

        Material GetCorridorMaterial()
        {
            if (_corridorMat != null) return _corridorMat;
            _corridorMat = CreateMaterial(new Color(0.2f, 0.2f, 0.22f));
            return _corridorMat;
        }

        static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = color };
        }
    }
}