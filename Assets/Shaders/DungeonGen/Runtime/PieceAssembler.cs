using System.Collections.Generic;
using UnityEngine;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Layout;

namespace DungeonSystem.Runtime
{
    public class PieceAssembler
    {
        struct PieceBounds
        {
            public Vector3 size;
            public Vector3 center;
        }

        readonly RoomPiecePalette _palette;
        readonly DungeonConfig _config;
        readonly float _cellSize;
        readonly System.Random _rng;
        readonly Dictionary<int, PieceBounds> _cache = new();

        // New: PropPlacer for recipe-based decoration
        readonly PropPlacer _propPlacer;

        public PieceAssembler(RoomPiecePalette palette, DungeonConfig config, System.Random rng)
        {
            _palette = palette;
            _config = config;
            _cellSize = config.cellSize;
            _rng = rng;
            _propPlacer = new PropPlacer(config, palette, rng);
        }

        public void AssembleRoom(PlacedRoom placed, RoomInstance instance, FloorLayout layout)
        {
            Transform root = instance.transform;
            int w = placed.Width;
            int h = placed.Height;
            float sizeX = w * _cellSize;
            float sizeZ = h * _cellSize;
            float halfW = sizeX * 0.5f;
            float halfH = sizeZ * 0.5f;
            RoomType type = placed.Node.Type;

            float wallHeight = GetWallHeight(type);

            // Structural assembly (unchanged)
            FillFloor(root, sizeX, sizeZ, type);
            FillCeiling(root, sizeX, sizeZ, wallHeight, type);
            BuildRoomSockets(root, instance, w, h, halfW, halfH, type, wallHeight);
            BuildCornerPillars(root, w, h, halfW, halfH, wallHeight, type);
            BuildMapPillars(root, w, h, halfW, halfH, wallHeight, type);

            // ===== NEW: Recipe-based decoration via PropPlacer =====
            RoomRecipe recipe = _config.GetRecipe(type);
            if (recipe != null)
            {
                _propPlacer.DecoratRoom(placed, instance, recipe, layout);
            }
            else
            {
                // Legacy fallback: original hardcoded spawn points + props
                PlaceSpawnPoints(root, type, sizeX, sizeZ);
                DecorateProps(root, w, h, halfW, halfH, type, wallHeight);
            }
        }

        public void AssembleCorridorCell(Vector2Int cell, FloorLayout layout, float yOffset, Transform parent)
        {
            var cellGO = new GameObject($"CorridorCell_{cell.x}_{cell.y}");
            cellGO.transform.SetParent(parent);
            cellGO.transform.position = new Vector3(
                cell.x * _cellSize + _cellSize * 0.5f,
                yOffset,
                cell.y * _cellSize + _cellSize * 0.5f);

            RoomType type = RoomType.Corridor;
            float wallHeight = GetWallHeight(type);
            float halfCell = _cellSize * 0.5f;

            FillFloor(cellGO.transform, _cellSize, _cellSize, type);
            FillCeiling(cellGO.transform, _cellSize, _cellSize, wallHeight, type);

            bool[] hasWall = new bool[4];
            PieceEntry[] wallPieces = _palette.GetWallSegments(type);
            PieceEntry[] pillarPieces = _palette.GetPillars(type);
            PieceEntry[] torchPieces = _palette.GetTorches(type);
            PieceEntry[] wallProps = _palette.GetWallProps(type);

            for (int d = 0; d < 4; d++)
            {
                Direction dir = (Direction)d;
                Vector2Int neighbor = cell + GridUtils.GetOffset(dir);

                if (layout.OccupiedCells.Contains(neighbor)) continue;

                hasWall[d] = true;
                var wallParent = new GameObject($"Wall_{dir}");
                wallParent.transform.SetParent(cellGO.transform);
                wallParent.transform.localPosition = EdgeCenterPos(dir, 0f, halfCell, halfCell);
                FillWallEdge(wallParent.transform, dir, wallPieces, pillarPieces, torchPieces, wallProps, wallHeight);
            }

            BuildCorridorCornerPillars(cellGO.transform, hasWall, halfCell, wallHeight, pillarPieces);
        }

        // ======================== UNCHANGED STRUCTURAL METHODS ========================
        // All methods below are identical to the original PieceAssembler.
        // Only AssembleRoom above was modified to add the PropPlacer integration.

        float GetWallHeight(RoomType type)
        {
            PieceEntry[] walls = _palette.GetWallSegments(type);
            return (walls != null && walls.Length > 0) ? Measure(walls[0]).size.y : 3f;
        }

        float GetLevelY(int level, float wallHeight)
        {
            return _config.wallHeightOffset + level * (wallHeight - _config.pieceOverlap);
        }

        void FillFloor(Transform parent, float sizeX, float sizeZ, RoomType type)
        {
            PieceEntry[] tiles = _palette.GetFloorTiles(type);
            if (tiles == null || tiles.Length == 0) return;

            var floorRoot = new GameObject("Floor");
            floorRoot.transform.SetParent(parent);
            floorRoot.transform.localPosition = Vector3.zero;

            PieceBounds sample = Measure(WeightedPick(tiles));
            int countX = Mathf.Max(1, Mathf.CeilToInt(sizeX / sample.size.x));
            int countZ = Mathf.Max(1, Mathf.CeilToInt(sizeZ / sample.size.z));
            float slotX = sizeX / countX;
            float slotZ = sizeZ / countZ;

            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    PieceEntry entry = WeightedPick(tiles);
                    PieceBounds m = Measure(entry);

                    float cx = -sizeX * 0.5f + slotX * 0.5f + ix * slotX;
                    float cz = -sizeZ * 0.5f + slotZ * 0.5f + iz * slotZ;

                    Vector3 scale = new Vector3(slotX / Mathf.Max(0.01f, m.size.x - _config.pieceOverlap), 1f, slotZ / Mathf.Max(0.01f, m.size.z - _config.pieceOverlap));
                    Vector3 desiredCenter = new Vector3(cx, -m.size.y * 0.5f, cz);

                    var tile = Object.Instantiate(entry.prefab, floorRoot.transform);
                    tile.transform.localPosition = ComputePosition(desiredCenter, Quaternion.identity, scale, m);
                    tile.transform.localScale = scale;
                }
            }
        }

        void FillCeiling(Transform parent, float sizeX, float sizeZ, float wallHeight, RoomType type)
        {
            PieceEntry[] tiles = _palette.GetCeilingTiles(type);
            if (tiles == null || tiles.Length == 0) return;

            var ceilRoot = new GameObject("Ceiling");
            ceilRoot.transform.SetParent(parent);
            ceilRoot.transform.localPosition = Vector3.zero;

            PieceBounds sample = Measure(WeightedPick(tiles));
            int countX = Mathf.Max(1, Mathf.CeilToInt(sizeX / sample.size.x));
            int countZ = Mathf.Max(1, Mathf.CeilToInt(sizeZ / sample.size.z));
            float slotX = sizeX / countX;
            float slotZ = sizeZ / countZ;

            float baseY = GetLevelY(Mathf.Max(1, _config.roomHeightMultiplier), wallHeight);

            for (int ix = 0; ix < countX; ix++)
            {
                for (int iz = 0; iz < countZ; iz++)
                {
                    PieceEntry entry = WeightedPick(tiles);
                    PieceBounds m = Measure(entry);

                    float cx = -sizeX * 0.5f + slotX * 0.5f + ix * slotX;
                    float cz = -sizeZ * 0.5f + slotZ * 0.5f + iz * slotZ;

                    Vector3 scale = new Vector3(slotX / Mathf.Max(0.01f, m.size.x - _config.pieceOverlap), 1f, slotZ / Mathf.Max(0.01f, m.size.z - _config.pieceOverlap));
                    Vector3 desiredCenter = new Vector3(cx, baseY + m.size.y * 0.5f, cz);

                    var tile = Object.Instantiate(entry.prefab, ceilRoot.transform);
                    tile.transform.localPosition = ComputePosition(desiredCenter, Quaternion.identity, scale, m);
                    tile.transform.localScale = scale;
                }
            }
        }

        void BuildRoomSockets(Transform root, RoomInstance room, int w, int h, float halfW, float halfH, RoomType type, float wallHeight)
        {
            var socketsRoot = new GameObject("Sockets");
            socketsRoot.transform.SetParent(root);
            socketsRoot.transform.localPosition = Vector3.zero;

            PieceEntry[] wallPieces = _palette.GetWallSegments(type);
            PieceEntry[] doorPieces = _palette.GetDoorFrames(type);
            PieceEntry[] pillarPieces = _palette.GetPillars(type);
            PieceEntry[] torchPieces = _palette.GetTorches(type);
            PieceEntry[] wallProps = _palette.GetWallProps(type);

            for (int cx = 0; cx < w; cx++)
            {
                float cellX = -halfW + (cx + 0.5f) * _cellSize;
                BuildOneSocket(socketsRoot.transform, room, Direction.North, new Vector2Int(cx, h - 1), cellX, halfW, halfH, wallPieces, doorPieces, pillarPieces, torchPieces, wallProps, wallHeight);
                BuildOneSocket(socketsRoot.transform, room, Direction.South, new Vector2Int(cx, 0), cellX, halfW, halfH, wallPieces, doorPieces, pillarPieces, torchPieces, wallProps, wallHeight);
            }

            for (int cy = 0; cy < h; cy++)
            {
                float cellZ = -halfH + (cy + 0.5f) * _cellSize;
                BuildOneSocket(socketsRoot.transform, room, Direction.East, new Vector2Int(w - 1, cy), cellZ, halfW, halfH, wallPieces, doorPieces, pillarPieces, torchPieces, wallProps, wallHeight);
                BuildOneSocket(socketsRoot.transform, room, Direction.West, new Vector2Int(0, cy), cellZ, halfW, halfH, wallPieces, doorPieces, pillarPieces, torchPieces, wallProps, wallHeight);
            }
        }

        void BuildOneSocket(Transform parent, RoomInstance room, Direction dir, Vector2Int cellOffset, float cellAlongPos, float halfW, float halfH, PieceEntry[] wallPieces, PieceEntry[] doorPieces, PieceEntry[] pillarPieces, PieceEntry[] torchPieces, PieceEntry[] wallProps, float wallHeight)
        {
            var socketGO = new GameObject($"Socket_{dir}_{cellOffset.x}_{cellOffset.y}");
            socketGO.transform.SetParent(parent);
            socketGO.transform.localPosition = EdgeCenterPos(dir, cellAlongPos, halfW, halfH);

            var socket = socketGO.AddComponent<DoorSocket>();
            socket.socketDirection = dir;
            socket.cellOffset = cellOffset;

            var wallState = new GameObject("WallState");
            wallState.transform.SetParent(socketGO.transform);
            wallState.transform.localPosition = Vector3.zero;
            FillWallEdge(wallState.transform, dir, wallPieces, pillarPieces, torchPieces, wallProps, wallHeight);

            var openState = new GameObject("OpenState");
            openState.transform.SetParent(socketGO.transform);
            openState.transform.localPosition = Vector3.zero;
            BuildDoorway(openState.transform, dir, doorPieces, wallPieces, pillarPieces, wallHeight);
            openState.SetActive(false);

            socket.wallState = wallState;
            socket.openState = openState;

            if (_palette.doorLockedFrames != null && _palette.doorLockedFrames.Length > 0)
            {
                var lockedState = new GameObject("LockedState");
                lockedState.transform.SetParent(socketGO.transform);
                lockedState.transform.localPosition = Vector3.zero;
                BuildDoorway(lockedState.transform, dir, _palette.doorLockedFrames, wallPieces, pillarPieces, wallHeight);
                lockedState.SetActive(false);
                socket.lockedState = lockedState;
            }

            if (_palette.doorSecretFrames != null && _palette.doorSecretFrames.Length > 0)
            {
                var hiddenState = new GameObject("HiddenState");
                hiddenState.transform.SetParent(socketGO.transform);
                hiddenState.transform.localPosition = Vector3.zero;
                FillWallEdge(hiddenState.transform, dir, wallPieces, pillarPieces, torchPieces, wallProps, wallHeight);
                hiddenState.SetActive(false);
                socket.hiddenState = hiddenState;
            }

            room.sockets.Add(socket);
        }

        void FillWallEdge(Transform container, Direction dir, PieceEntry[] wallPieces, PieceEntry[] pillarPieces, PieceEntry[] torchPieces, PieceEntry[] wallProps, float wallHeight)
        {
            if (wallPieces == null || wallPieces.Length == 0) return;

            PieceBounds sampleM = Measure(WeightedPick(wallPieces));
            int count = Mathf.Max(1, Mathf.RoundToInt(_cellSize / sampleM.size.x));
            float segWidth = _cellSize / count;

            Quaternion rot = WallRotation(dir);
            Vector3 along = AlongDir(dir);
            Vector3 inward = InwardDir(dir);

            for (int yLevel = 0; yLevel < Mathf.Max(1, _config.roomHeightMultiplier); yLevel++)
            {
                float baseY = GetLevelY(yLevel, wallHeight);
                bool occupiedByPropOrTorch = false;

                for (int i = 0; i < count; i++)
                {
                    PieceEntry entry = WeightedPick(wallPieces);
                    PieceBounds m = Measure(entry);

                    float t = -_cellSize * 0.5f + segWidth * 0.5f + i * segWidth;
                    float scaleX = segWidth / Mathf.Max(0.01f, m.size.x - _config.pieceOverlap);
                    Vector3 scale = new Vector3(scaleX, 1f, 1f);

                    Vector3 desiredCenter = along * t + inward * (m.size.z * 0.5f) + Vector3.up * (baseY + m.size.y * 0.5f);

                    var go = Object.Instantiate(entry.prefab, container);
                    go.transform.localPosition = ComputePosition(desiredCenter, rot, scale, m);
                    go.transform.localRotation = rot;
                    go.transform.localScale = scale;

                    if (yLevel == 0 && i > 0 && i < count - 1)
                    {
                        if (!occupiedByPropOrTorch)
                        {
                            if (wallProps != null && wallProps.Length > 0 && _rng.NextDouble() < _config.wallPropProbability)
                            {
                                PlaceWallProp(container, wallProps, t, baseY, wallHeight, along, inward, m.size.z, rot);
                                occupiedByPropOrTorch = true;
                            }
                            else if (torchPieces != null && torchPieces.Length > 0 && _rng.NextDouble() < _config.torchProbability)
                            {
                                PlaceTorch(container, torchPieces, t, baseY, wallHeight, along, inward, m.size.z);
                                occupiedByPropOrTorch = true;
                            }
                        }
                        else
                        {
                            occupiedByPropOrTorch = false;
                        }
                    }

                    if (yLevel > 0 && pillarPieces != null && pillarPieces.Length > 0)
                        PlaceHorizontalBeam(container, t, baseY, segWidth, along, inward, m.size.z, rot, pillarPieces);

                    if (i < count - 1 && pillarPieces != null && pillarPieces.Length > 0)
                        PlaceJointPillar(container, t + segWidth * 0.5f, baseY, along, inward, m.size.z, wallHeight, pillarPieces);
                }
            }
        }

        void BuildDoorway(Transform container, Direction dir, PieceEntry[] doorPieces, PieceEntry[] wallPieces, PieceEntry[] pillarPieces, float wallHeight)
        {
            if (doorPieces == null || doorPieces.Length == 0) return;

            Quaternion rot = WallRotation(dir);
            Vector3 along = AlongDir(dir);
            Vector3 inward = InwardDir(dir);

            PieceEntry doorEntry = WeightedPick(doorPieces);
            PieceBounds doorM = Measure(doorEntry);

            float doorWidth = Mathf.Min(doorM.size.x, _cellSize * 0.8f);
            float halfDoor = doorWidth * 0.5f;
            Vector3 doorScale = new Vector3(doorWidth / Mathf.Max(0.01f, doorM.size.x - _config.pieceOverlap), 1f, 1f);

            float baseLevel0Y = GetLevelY(0, wallHeight);
            Vector3 doorCenter = inward * (doorM.size.z * 0.5f) + Vector3.up * (baseLevel0Y + doorM.size.y * 0.5f);

            var doorGO = Object.Instantiate(doorEntry.prefab, container);
            doorGO.transform.localPosition = ComputePosition(doorCenter, rot, doorScale, doorM);
            doorGO.transform.localRotation = rot;
            doorGO.transform.localScale = doorScale;

            float wallThickness = (wallPieces != null && wallPieces.Length > 0) ? Measure(wallPieces[0]).size.z : 0.5f;

            if (pillarPieces != null && pillarPieces.Length > 0)
            {
                PieceBounds pillarM = Measure(pillarPieces[0]);
                float pillarW = pillarM.size.x;
                PlaceJointPillar(container, -halfDoor - pillarW * 0.5f, baseLevel0Y, along, inward, wallThickness, wallHeight, pillarPieces);
                PlaceJointPillar(container, halfDoor + pillarW * 0.5f, baseLevel0Y, along, inward, wallThickness, wallHeight, pillarPieces);
            }

            if (wallPieces != null && wallPieces.Length > 0)
            {
                float sideLength = (_cellSize - doorWidth) * 0.5f;

                if (sideLength >= 0.05f)
                {
                    PieceBounds wallM = Measure(WeightedPick(wallPieces));
                    int sideCount = Mathf.Max(1, Mathf.RoundToInt(sideLength / wallM.size.x));
                    float sideSegWidth = sideLength / sideCount;

                    for (int side = 0; side < 2; side++)
                    {
                        float sideStart = side == 0 ? -_cellSize * 0.5f : halfDoor;

                        for (int i = 0; i < sideCount; i++)
                        {
                            PieceEntry entry = WeightedPick(wallPieces);
                            PieceBounds m = Measure(entry);

                            float t = sideStart + sideSegWidth * 0.5f + i * sideSegWidth;
                            float sx = sideSegWidth / Mathf.Max(0.01f, m.size.x - _config.pieceOverlap);
                            Vector3 scale = new Vector3(sx, 1f, 1f);

                            Vector3 desired = along * t + inward * (m.size.z * 0.5f) + Vector3.up * (baseLevel0Y + m.size.y * 0.5f);

                            var go = Object.Instantiate(entry.prefab, container);
                            go.transform.localPosition = ComputePosition(desired, rot, scale, m);
                            go.transform.localRotation = rot;
                            go.transform.localScale = scale;
                        }
                    }
                }

                for (int yLevel = 1; yLevel < Mathf.Max(1, _config.roomHeightMultiplier); yLevel++)
                {
                    PieceBounds wallM = Measure(WeightedPick(wallPieces));
                    int count = Mathf.Max(1, Mathf.RoundToInt(_cellSize / wallM.size.x));
                    float segWidth = _cellSize / count;
                    float baseY = GetLevelY(yLevel, wallHeight);

                    for (int i = 0; i < count; i++)
                    {
                        PieceEntry entry = WeightedPick(wallPieces);
                        PieceBounds m = Measure(entry);

                        float t = -_cellSize * 0.5f + segWidth * 0.5f + i * segWidth;
                        float scaleX = segWidth / Mathf.Max(0.01f, m.size.x - _config.pieceOverlap);
                        Vector3 scale = new Vector3(scaleX, 1f, 1f);

                        Vector3 desiredCenter = along * t + inward * (m.size.z * 0.5f) + Vector3.up * (baseY + m.size.y * 0.5f);

                        var go = Object.Instantiate(entry.prefab, container);
                        go.transform.localPosition = ComputePosition(desiredCenter, rot, scale, m);
                        go.transform.localRotation = rot;
                        go.transform.localScale = scale;

                        if (pillarPieces != null && pillarPieces.Length > 0)
                            PlaceHorizontalBeam(container, t, baseY, segWidth, along, inward, m.size.z, rot, pillarPieces);

                        if (i < count - 1 && pillarPieces != null && pillarPieces.Length > 0)
                            PlaceJointPillar(container, t + segWidth * 0.5f, baseY, along, inward, m.size.z, wallHeight, pillarPieces);
                    }
                }
            }
        }

        void PlaceTorch(Transform parent, PieceEntry[] torchPieces, float t, float wallBaseY, float wallHeight, Vector3 along, Vector3 inward, float wallThickness)
        {
            PieceEntry entry = WeightedPick(torchPieces);
            PieceBounds m = Measure(entry);

            float targetY = wallBaseY + wallHeight * _config.torchHeightRatio;
            Vector3 desiredCenter = along * t + inward * (wallThickness * 0.5f + _config.torchZOffset) + Vector3.up * targetY;
            Quaternion rot = Quaternion.LookRotation(inward) * Quaternion.Euler(_config.torchPitch, 0, 0);
            Vector3 scale = Vector3.one * _config.torchScale;

            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = ComputePosition(desiredCenter, rot, scale, m);
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
        }

        void PlaceWallProp(Transform parent, PieceEntry[] props, float t, float wallBaseY, float wallHeight, Vector3 along, Vector3 inward, float wallThickness, Quaternion baseRot)
        {
            PieceEntry entry = WeightedPick(props);
            PieceBounds m = Measure(entry);

            float desiredY;
            if (m.size.x >= m.size.y * 0.8f)
                desiredY = 0f + m.size.y * 0.5f - 0.02f;
            else
                desiredY = wallBaseY + wallHeight * 0.65f;

            Vector3 desiredCenter = along * t + inward * (wallThickness * 0.5f + m.size.z * 0.5f) + Vector3.up * desiredY;

            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = ComputePosition(desiredCenter, baseRot, Vector3.one, m);
            go.transform.localRotation = baseRot;
            go.transform.localScale = Vector3.one;
        }

        void PlaceJointPillar(Transform parent, float t, float baseY, Vector3 along, Vector3 inward, float wallThickness, float wallHeight, PieceEntry[] pillarPieces)
        {
            PieceEntry entry = WeightedPick(pillarPieces);
            PieceBounds m = Measure(entry);

            float uniformScale = wallHeight / Mathf.Max(0.01f, m.size.y - _config.pieceOverlap);
            Vector3 scale = Vector3.one * uniformScale;

            float insetDepth = Mathf.Max(m.size.z * uniformScale * 0.5f, wallThickness * 0.5f);
            Vector3 desiredCenter = along * t + inward * insetDepth + Vector3.up * (baseY + wallHeight * 0.5f);

            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = ComputePosition(desiredCenter, Quaternion.identity, scale, m);
            go.transform.localScale = scale;
        }

        void PlaceHorizontalBeam(Transform parent, float t, float baseY, float width, Vector3 along, Vector3 inward, float wallThickness, Quaternion wallRot, PieceEntry[] pillarPieces)
        {
            PieceEntry entry = WeightedPick(pillarPieces);
            PieceBounds m = Measure(entry);

            Quaternion rot = wallRot * Quaternion.Euler(0, 0, 90);

            float scaleY = width / Mathf.Max(0.01f, m.size.y - _config.pieceOverlap);
            float thicknessScale = 0.75f;
            Vector3 scale = new Vector3(thicknessScale, scaleY, thicknessScale);

            float insetDepth = Mathf.Max(m.size.x * thicknessScale * 0.5f, wallThickness * 0.5f);
            Vector3 desiredCenter = along * t + inward * insetDepth + Vector3.up * baseY;

            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = ComputePosition(desiredCenter, rot, scale, m);
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
        }

        void BuildCornerPillars(Transform root, int w, int h, float halfW, float halfH, float wallHeight, RoomType type)
        {
            PieceEntry[] pillars = _palette.GetPillars(type);
            if (pillars == null || pillars.Length == 0) return;

            PieceEntry[] walls = _palette.GetWallSegments(type);
            float wallThick = (walls != null && walls.Length > 0) ? Measure(walls[0]).size.z : 0.5f;

            var cornerRoot = new GameObject("CornerPillars");
            cornerRoot.transform.SetParent(root);
            cornerRoot.transform.localPosition = Vector3.zero;

            PlaceCornerPillar(cornerRoot.transform, -halfW, +halfH, wallThick, wallHeight, pillars, new Vector3(+1, 0, -1));
            PlaceCornerPillar(cornerRoot.transform, +halfW, +halfH, wallThick, wallHeight, pillars, new Vector3(-1, 0, -1));
            PlaceCornerPillar(cornerRoot.transform, -halfW, -halfH, wallThick, wallHeight, pillars, new Vector3(+1, 0, +1));
            PlaceCornerPillar(cornerRoot.transform, +halfW, -halfH, wallThick, wallHeight, pillars, new Vector3(-1, 0, +1));

            for (int cx = 1; cx < w; cx++)
            {
                float x = -halfW + cx * _cellSize;
                PlaceCornerPillar(cornerRoot.transform, x, +halfH, wallThick, wallHeight, pillars, new Vector3(0, 0, -1));
                PlaceCornerPillar(cornerRoot.transform, x, -halfH, wallThick, wallHeight, pillars, new Vector3(0, 0, +1));
            }

            for (int cy = 1; cy < h; cy++)
            {
                float z = -halfH + cy * _cellSize;
                PlaceCornerPillar(cornerRoot.transform, -halfW, z, wallThick, wallHeight, pillars, new Vector3(+1, 0, 0));
                PlaceCornerPillar(cornerRoot.transform, +halfW, z, wallThick, wallHeight, pillars, new Vector3(-1, 0, 0));
            }
        }

        void PlaceCornerPillar(Transform parent, float cornerX, float cornerZ, float wallThickness, float wallHeight, PieceEntry[] pillars, Vector3 insetDir)
        {
            for (int yLevel = 0; yLevel < Mathf.Max(1, _config.roomHeightMultiplier); yLevel++)
            {
                PieceEntry entry = WeightedPick(pillars);
                PieceBounds m = Measure(entry);

                float uniformScale = wallHeight / Mathf.Max(0.01f, m.size.y - _config.pieceOverlap);
                Vector3 scale = Vector3.one * uniformScale;

                float inset = wallThickness * 0.5f;
                float baseY = GetLevelY(yLevel, wallHeight);

                Vector3 desiredCenter = new Vector3(cornerX + insetDir.x * inset, baseY + wallHeight * 0.5f, cornerZ + insetDir.z * inset);

                var go = Object.Instantiate(entry.prefab, parent);
                go.transform.localPosition = ComputePosition(desiredCenter, Quaternion.identity, scale, m);
                go.transform.localScale = scale;
            }
        }

        void BuildMapPillars(Transform root, int w, int h, float halfW, float halfH, float wallHeight, RoomType type)
        {
            PieceEntry[] mPillars = _palette.GetMapPillars(type);
            if (mPillars == null || mPillars.Length == 0) return;
            if (w < 2 && h < 2) return;

            var pillarRoot = new GameObject("MapPillars");
            pillarRoot.transform.SetParent(root);
            pillarRoot.transform.localPosition = Vector3.zero;

            for (int cx = 1; cx < w; cx++)
                for (int cy = 1; cy < h; cy++)
                {
                    float x = -halfW + cx * _cellSize;
                    float z = -halfH + cy * _cellSize;
                    PlaceMapPillar(pillarRoot.transform, x, z, wallHeight, mPillars);
                }
        }

        void PlaceMapPillar(Transform parent, float x, float z, float wallHeight, PieceEntry[] mPillars)
        {
            for (int yLevel = 0; yLevel < Mathf.Max(1, _config.roomHeightMultiplier); yLevel++)
            {
                PieceEntry entry = WeightedPick(mPillars);
                PieceBounds m = Measure(entry);

                float uniformScale = wallHeight / Mathf.Max(0.01f, m.size.y - _config.pieceOverlap);
                Vector3 scale = Vector3.one * uniformScale;

                float baseY = GetLevelY(yLevel, wallHeight);
                Vector3 desiredCenter = new Vector3(x, baseY + wallHeight * 0.5f, z);

                var go = Object.Instantiate(entry.prefab, parent);
                go.transform.localPosition = ComputePosition(desiredCenter, Quaternion.identity, scale, m);
                go.transform.localScale = scale;
            }
        }

        void BuildCorridorCornerPillars(Transform parent, bool[] hasWall, float halfCell, float wallHeight, PieceEntry[] pillars)
        {
            if (pillars == null || pillars.Length == 0) return;

            PieceEntry[] walls = _palette.GetWallSegments(RoomType.Corridor);
            float wallThick = (walls != null && walls.Length > 0) ? Measure(walls[0]).size.z : 0.5f;

            bool n = hasWall[(int)Direction.North];
            bool e = hasWall[(int)Direction.East];
            bool s = hasWall[(int)Direction.South];
            bool w = hasWall[(int)Direction.West];

            if (n && e) PlaceCornerPillar(parent, +halfCell, +halfCell, wallThick, wallHeight, pillars, new Vector3(-1, 0, -1));
            if (n && w) PlaceCornerPillar(parent, -halfCell, +halfCell, wallThick, wallHeight, pillars, new Vector3(+1, 0, -1));
            if (s && e) PlaceCornerPillar(parent, +halfCell, -halfCell, wallThick, wallHeight, pillars, new Vector3(-1, 0, +1));
            if (s && w) PlaceCornerPillar(parent, -halfCell, -halfCell, wallThick, wallHeight, pillars, new Vector3(+1, 0, +1));
        }

        // ======================== LEGACY DECORATION (fallback when no recipe) ========================

        void DecorateProps(Transform root, int w, int h, float halfW, float halfH, RoomType type, float wallHeight)
        {
            var propRoot = new GameObject("Props");
            propRoot.transform.SetParent(root);
            propRoot.transform.localPosition = Vector3.zero;

            PieceEntry[] walls = _palette.GetWallSegments(type);
            float wallThick = (walls != null && walls.Length > 0) ? Measure(walls[0]).size.z : 0.5f;

            PieceEntry[] cProps = _palette.GetCornerProps(type);
            if (cProps != null && cProps.Length > 0 && _rng.NextDouble() < _config.cornerPropProbability)
            {
                PlaceCornerProp(propRoot.transform, -halfW, +halfH, wallThick, cProps, new Vector3(+1, 0, -1), Quaternion.Euler(0, 135, 0));
                PlaceCornerProp(propRoot.transform, +halfW, +halfH, wallThick, cProps, new Vector3(-1, 0, -1), Quaternion.Euler(0, -135, 0));
                PlaceCornerProp(propRoot.transform, -halfW, -halfH, wallThick, cProps, new Vector3(+1, 0, +1), Quaternion.Euler(0, 45, 0));
                PlaceCornerProp(propRoot.transform, +halfW, -halfH, wallThick, cProps, new Vector3(-1, 0, +1), Quaternion.Euler(0, -45, 0));
            }

            PieceEntry[] fProps = _palette.GetFloorProps(type);
            if (fProps != null && fProps.Length > 0)
            {
                int floorPropCount = _rng.Next(0, w * h + 1);
                for (int i = 0; i < floorPropCount; i++)
                {
                    if (_rng.NextDouble() < _config.floorPropProbability)
                    {
                        float rx = (float)(_rng.NextDouble() * (w * _cellSize * 0.7f) - (halfW * 0.7f));
                        float rz = (float)(_rng.NextDouble() * (h * _cellSize * 0.7f) - (halfH * 0.7f));
                        PlaceStandaloneProp(propRoot.transform, rx, rz, 0f, fProps);
                    }
                }
            }

            PieceEntry[] ceilProps = _palette.GetCeilingProps(type);
            if (ceilProps != null && ceilProps.Length > 0)
            {
                int ceilPropCount = _rng.Next(0, (w * h) / 2 + 1);
                for (int i = 0; i < ceilPropCount; i++)
                {
                    if (_rng.NextDouble() < _config.ceilingPropProbability)
                    {
                        float rx = (float)(_rng.NextDouble() * (w * _cellSize * 0.8f) - (halfW * 0.8f));
                        float rz = (float)(_rng.NextDouble() * (h * _cellSize * 0.8f) - (halfH * 0.8f));
                        PlaceStandaloneProp(propRoot.transform, rx, rz, GetLevelY(Mathf.Max(1, _config.roomHeightMultiplier), wallHeight), ceilProps, true);
                    }
                }
            }
        }

        void PlaceCornerProp(Transform parent, float cornerX, float cornerZ, float wallThickness, PieceEntry[] props, Vector3 insetDir, Quaternion rot)
        {
            PieceEntry entry = WeightedPick(props);
            PieceBounds m = Measure(entry);

            float radius = Mathf.Max(m.size.x, m.size.z) * 0.5f;
            float inset = Mathf.Max(radius, wallThickness);

            Vector3 desiredCenter = new Vector3(cornerX + insetDir.x * inset, m.size.y * 0.5f, cornerZ + insetDir.z * inset);

            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = ComputePosition(desiredCenter, rot, Vector3.one, m);
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one;
        }

        void PlaceStandaloneProp(Transform parent, float x, float z, float targetY, PieceEntry[] props, bool isCeiling = false)
        {
            PieceEntry entry = WeightedPick(props);
            PieceBounds m = Measure(entry);

            Quaternion rot = Quaternion.Euler(0, (float)_rng.NextDouble() * 360f, 0);
            float desiredY = isCeiling ? (targetY - m.size.y * 0.5f) : (targetY + m.size.y * 0.5f);
            Vector3 desiredCenter = new Vector3(x, desiredY, z);

            var go = Object.Instantiate(entry.prefab, parent);
            go.transform.localPosition = ComputePosition(desiredCenter, rot, Vector3.one, m);
            go.transform.localRotation = rot;
            go.transform.localScale = Vector3.one;
        }

        void PlaceSpawnPoints(Transform root, RoomType type, float sizeX, float sizeZ)
        {
            var spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(root);
            spawnRoot.transform.localPosition = Vector3.zero;

            float margin = Mathf.Min(2.0f, Mathf.Min(sizeX, sizeZ) * 0.15f);
            float sx = Mathf.Max(0.5f, sizeX * 0.5f - margin);
            float sz = Mathf.Max(0.5f, sizeZ * 0.5f - margin);

            foreach (var (pointType, localPos) in GetSpawnLayout(type, sx, sz))
            {
                var spGO = new GameObject($"Spawn_{pointType}");
                spGO.transform.SetParent(spawnRoot.transform);
                spGO.transform.localPosition = localPos;
                spGO.AddComponent<SpawnPoint>().pointType = pointType;
            }
        }

        List<(SpawnPointType, Vector3)> GetSpawnLayout(RoomType roomType, float sx, float sz)
        {
            var list = new List<(SpawnPointType, Vector3)>();
            switch (roomType)
            {
                case RoomType.Start:
                    list.Add((SpawnPointType.PlayerSpawn, Vector3.zero));
                    list.Add((SpawnPointType.Light, new Vector3(0, 0, sz * 0.6f)));
                    break;
                case RoomType.Combat:
                    list.Add((SpawnPointType.Enemy, new Vector3(-sx * 0.5f, 0, sz * 0.5f)));
                    list.Add((SpawnPointType.Enemy, new Vector3(sx * 0.5f, 0, -sz * 0.5f)));
                    list.Add((SpawnPointType.Enemy, new Vector3(sx * 0.5f, 0, sz * 0.5f)));
                    list.Add((SpawnPointType.Item, Vector3.zero));
                    break;
                case RoomType.Loot:
                    list.Add((SpawnPointType.Chest, Vector3.zero));
                    list.Add((SpawnPointType.Item, new Vector3(-sx * 0.4f, 0, 0)));
                    list.Add((SpawnPointType.Item, new Vector3(sx * 0.4f, 0, 0)));
                    break;
                case RoomType.Boss:
                    list.Add((SpawnPointType.BossSpawn, Vector3.zero));
                    list.Add((SpawnPointType.Light, new Vector3(-sx * 0.7f, 0, sz * 0.7f)));
                    list.Add((SpawnPointType.Light, new Vector3(sx * 0.7f, 0, sz * 0.7f)));
                    list.Add((SpawnPointType.Light, new Vector3(-sx * 0.7f, 0, -sz * 0.7f)));
                    list.Add((SpawnPointType.Light, new Vector3(sx * 0.7f, 0, -sz * 0.7f)));
                    break;
                case RoomType.MiniBoss:
                    list.Add((SpawnPointType.BossSpawn, Vector3.zero));
                    list.Add((SpawnPointType.Enemy, new Vector3(-sx * 0.6f, 0, 0)));
                    list.Add((SpawnPointType.Enemy, new Vector3(sx * 0.6f, 0, 0)));
                    break;
                case RoomType.Shop:
                    list.Add((SpawnPointType.NPC, new Vector3(0, 0, sz * 0.5f)));
                    list.Add((SpawnPointType.Item, new Vector3(-sx * 0.3f, 0, -sz * 0.3f)));
                    list.Add((SpawnPointType.Item, new Vector3(0, 0, -sz * 0.3f)));
                    list.Add((SpawnPointType.Item, new Vector3(sx * 0.3f, 0, -sz * 0.3f)));
                    break;
                case RoomType.SafeRoom:
                    list.Add((SpawnPointType.PlayerSpawn, Vector3.zero));
                    list.Add((SpawnPointType.Light, new Vector3(-sx * 0.5f, 0, 0)));
                    list.Add((SpawnPointType.Light, new Vector3(sx * 0.5f, 0, 0)));
                    break;
                case RoomType.Puzzle:
                    list.Add((SpawnPointType.PuzzleObject, Vector3.zero));
                    list.Add((SpawnPointType.Item, new Vector3(0, 0, -sz * 0.5f)));
                    list.Add((SpawnPointType.Light, new Vector3(0, 0, sz * 0.5f)));
                    break;
                case RoomType.Trap:
                    list.Add((SpawnPointType.Trap, new Vector3(-sx * 0.4f, 0, 0)));
                    list.Add((SpawnPointType.Trap, Vector3.zero));
                    list.Add((SpawnPointType.Trap, new Vector3(sx * 0.4f, 0, 0)));
                    list.Add((SpawnPointType.Item, new Vector3(0, 0, sz * 0.5f)));
                    break;
                case RoomType.SecretRoom:
                    list.Add((SpawnPointType.Chest, Vector3.zero));
                    list.Add((SpawnPointType.Light, new Vector3(0, 0, sz * 0.4f)));
                    break;
                case RoomType.StaircaseUp:
                case RoomType.StaircaseDown:
                    list.Add((SpawnPointType.Prop, Vector3.zero));
                    list.Add((SpawnPointType.Light, new Vector3(sx * 0.5f, 0, 0)));
                    break;
            }
            return list;
        }

        // ======================== SHARED UTILS ========================

        Vector3 ComputePosition(Vector3 desiredMeshCenter, Quaternion rot, Vector3 scale, PieceBounds m)
        {
            Vector3 scaledCenter = Vector3.Scale(m.center, scale);
            Vector3 rotatedScaledCenter = rot * scaledCenter;
            return desiredMeshCenter - rotatedScaledCenter;
        }

        Vector3 EdgeCenterPos(Direction dir, float cellAlongPos, float halfW, float halfH)
        {
            return dir switch
            {
                Direction.North => new Vector3(cellAlongPos, 0, halfH),
                Direction.South => new Vector3(cellAlongPos, 0, -halfH),
                Direction.East => new Vector3(halfW, 0, cellAlongPos),
                Direction.West => new Vector3(-halfW, 0, cellAlongPos),
                _ => Vector3.zero
            };
        }

        static Quaternion WallRotation(Direction dir) => dir switch
        {
            Direction.North => Quaternion.Euler(0, 180, 0),
            Direction.South => Quaternion.identity,
            Direction.East => Quaternion.Euler(0, 270, 0),
            Direction.West => Quaternion.Euler(0, 90, 0),
            _ => Quaternion.identity
        };

        static Vector3 AlongDir(Direction dir) => dir switch
        {
            Direction.North or Direction.South => Vector3.right,
            _ => Vector3.forward
        };

        static Vector3 InwardDir(Direction dir) => dir switch
        {
            Direction.North => Vector3.back,
            Direction.South => Vector3.forward,
            Direction.East => Vector3.left,
            Direction.West => Vector3.right,
            _ => Vector3.zero
        };

        PieceBounds Measure(PieceEntry entry)
        {
            if (entry == null || entry.prefab == null)
                return new PieceBounds { size = Vector3.one, center = Vector3.zero };

            PieceBounds raw = MeasurePrefab(entry.prefab);

            if (entry.widthOverride <= 0 && entry.heightOverride <= 0 && entry.depthOverride <= 0)
                return raw;

            return new PieceBounds
            {
                size = new Vector3(
                    entry.widthOverride > 0 ? entry.widthOverride : raw.size.x,
                    entry.heightOverride > 0 ? entry.heightOverride : raw.size.y,
                    entry.depthOverride > 0 ? entry.depthOverride : raw.size.z),
                center = raw.center
            };
        }

        PieceBounds MeasurePrefab(GameObject prefab)
        {
            int id = prefab.GetInstanceID();
            if (_cache.TryGetValue(id, out PieceBounds cached)) return cached;

            var instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool init = false;

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!init) { bounds = r.bounds; init = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!init)
                foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                {
                    if (!init) { bounds = col.bounds; init = true; }
                    else bounds.Encapsulate(col.bounds);
                }

            Object.DestroyImmediate(instance);

            Vector3 size = init ? bounds.size : Vector3.one;
            size.x = Mathf.Max(size.x, 0.01f);
            size.y = Mathf.Max(size.y, 0.01f);
            size.z = Mathf.Max(size.z, 0.01f);

            var result = new PieceBounds
            {
                size = size,
                center = init ? bounds.center : Vector3.zero
            };

            _cache[id] = result;
            return result;
        }

        PieceEntry WeightedPick(PieceEntry[] entries)
        {
            if (entries == null || entries.Length == 0) return null;
            if (entries.Length == 1) return entries[0];

            float total = 0f;
            foreach (var e in entries) total += Mathf.Max(e.spawnWeight, 0.01f);

            float roll = (float)(_rng.NextDouble() * total);
            float acc = 0f;

            foreach (var e in entries)
            {
                acc += Mathf.Max(e.spawnWeight, 0.01f);
                if (roll <= acc) return e;
            }

            return entries[^1];
        }
    }
}