using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Graph;

namespace DungeonSystem.Layout
{
    public class LayoutSolver
    {
        private readonly DungeonConfig _config;
        private readonly System.Random _rng;

        public LayoutSolver(DungeonConfig config, System.Random rng)
        {
            _config = config;
            _rng = rng;
        }

        public FloorLayout Solve(DungeonGraph graph, int floorIndex)
        {
            var layout = new FloorLayout();
            var mainPath = graph.GetMainPath();
            PlaceMainPath(layout, mainPath);
            PlaceBranches(layout, graph, mainPath);
            CreateCorridors(layout, graph);
            return layout;
        }

        private void PlaceMainPath(FloorLayout layout, List<GraphNode> mainPath)
        {
            if (mainPath.Count == 0) return;
            layout.AddRoom(mainPath[0], Vector2Int.zero, mainPath[0].TemplateWidth, mainPath[0].TemplateHeight);

            Direction prevDir = Direction.None;
            for (int i = 1; i < mainPath.Count; i++)
            {
                var node = mainPath[i];
                var prevRoom = layout.Rooms[^1];
                Vector2Int placed = PlaceAdjacentToRoom(layout, prevRoom, node, ref prevDir);

                if (placed == InvalidPos)
                    placed = FindNearestFreePosition(layout, prevRoom.GridPosition, node.TemplateWidth, node.TemplateHeight);

                layout.AddRoom(node, placed, node.TemplateWidth, node.TemplateHeight);
            }
        }

        private Vector2Int PlaceAdjacentToRoom(FloorLayout layout, PlacedRoom anchor, GraphNode node, ref Direction prevDir)
        {
            int w = node.TemplateWidth;
            int h = node.TemplateHeight;

            var directions = new List<Direction>(GridUtils.CardinalDirections);

            if (prevDir != Direction.None && _rng.NextDouble() < 0.4)
            {
                directions.Remove(prevDir);
                directions.Insert(0, prevDir);
            }
            else
            {
                Shuffle(directions);
            }

            int[] gaps = { 0, 1, 2 };

            foreach (var dir in directions)
            {
                foreach (int gap in gaps)
                {
                    Vector2Int offset = GridUtils.GetOffset(dir) * (GetRoomExtent(anchor, dir) + gap + GetRoomApproachExtent(w, h, dir));
                    Vector2Int candidate = anchor.GridPosition + offset;

                    if (layout.CanPlaceRect(candidate, w, h, padding: 0))
                    {
                        prevDir = dir;
                        return candidate;
                    }
                }
            }
            return InvalidPos;
        }

        private int GetRoomExtent(PlacedRoom room, Direction dir)
        {
            return dir switch
            {
                Direction.North => room.Height,
                Direction.South => 1,
                Direction.East => room.Width,
                Direction.West => 1,
                _ => 1
            };
        }

        private int GetRoomApproachExtent(int w, int h, Direction dir)
        {
            return dir switch
            {
                Direction.North => 0,
                Direction.South => h - 1,
                Direction.East => 0,
                Direction.West => w - 1,
                _ => 0
            };
        }

        private void PlaceBranches(FloorLayout layout, DungeonGraph graph, List<GraphNode> mainPath)
        {
            var placed = new HashSet<int>();
            foreach (var node in layout.Rooms)
                placed.Add(node.Node.Id);

            var queue = new Queue<GraphNode>();
            foreach (var mp in mainPath)
                queue.Enqueue(mp);

            Direction dummyDir = Direction.None;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var currentRoom = layout.Rooms.FirstOrDefault(r => r.Node == current);
                if (currentRoom == null) continue;

                foreach (var neighbor in current.GetNeighbors())
                {
                    if (placed.Contains(neighbor.Id)) continue;

                    Vector2Int pos = PlaceAdjacentToRoom(layout, currentRoom, neighbor, ref dummyDir);
                    if (pos == InvalidPos)
                        pos = FindNearestFreePosition(layout, currentRoom.GridPosition, neighbor.TemplateWidth, neighbor.TemplateHeight);

                    layout.AddRoom(neighbor, pos, neighbor.TemplateWidth, neighbor.TemplateHeight);
                    placed.Add(neighbor.Id);
                    queue.Enqueue(neighbor);
                }
            }
        }

        private void CreateCorridors(FloorLayout layout, DungeonGraph graph)
        {
            foreach (var edge in graph.Edges)
            {
                var roomA = layout.Rooms.FirstOrDefault(r => r.Node == edge.A);
                var roomB = layout.Rooms.FirstOrDefault(r => r.Node == edge.B);
                if (roomA == null || roomB == null) continue;

                if (AreRoomsAdjacent(roomA, roomB))
                {
                    RegisterDirectConnection(layout, roomA, roomB);
                    continue;
                }

                var corridor = PathfindCorridor(layout, roomA, roomB, edge);
                if (corridor != null)
                {
                    layout.Corridors.Add(corridor);
                    foreach (var cell in corridor.Cells)
                        layout.AddCorridorCell(cell, corridor);
                }
            }
        }

        private bool AreRoomsAdjacent(PlacedRoom a, PlacedRoom b)
        {
            var cellsA = a.GetCells();
            var cellsB = new HashSet<Vector2Int>(b.GetCells());

            foreach (var cell in cellsA)
                foreach (var dir in GridUtils.CardinalDirections)
                    if (cellsB.Contains(cell + GridUtils.GetOffset(dir)))
                        return true;
            return false;
        }

        private void RegisterDirectConnection(FloorLayout layout, PlacedRoom a, PlacedRoom b)
        {
            var cellsA = a.GetCells();
            var cellsB = new HashSet<Vector2Int>(b.GetCells());

            foreach (var cellA in cellsA)
                foreach (var dir in GridUtils.CardinalDirections)
                {
                    Vector2Int neighbor = cellA + GridUtils.GetOffset(dir);
                    if (cellsB.Contains(neighbor))
                    {
                        a.Connections[new DoorConnection(cellA, dir)] = b;
                        b.Connections[new DoorConnection(neighbor, GridUtils.GetOpposite(dir))] = a;
                        return;
                    }
                }
        }

        private CorridorSegment PathfindCorridor(FloorLayout layout, PlacedRoom roomA, PlacedRoom roomB, GraphEdge edge)
        {
            var edgeCellsA = GetRoomEdgeCells(roomA);
            var edgeCellsB = GetRoomEdgeCells(roomB);

            Vector2Int bestStartAdj = default, bestEndAdj = default;
            Direction bestStartDir = Direction.None, bestEndDir = Direction.None;
            int bestDist = int.MaxValue;

            foreach (var (cellA, dirA) in edgeCellsA)
            {
                Vector2Int adjA = cellA + GridUtils.GetOffset(dirA);
                if (layout.OccupiedCells.Contains(adjA) && layout.GetRoomAt(adjA) != roomB) continue;

                foreach (var (cellB, dirB) in edgeCellsB)
                {
                    Vector2Int adjB = cellB + GridUtils.GetOffset(dirB);
                    if (layout.OccupiedCells.Contains(adjB) && layout.GetRoomAt(adjB) != roomA) continue;

                    int dist = GridUtils.ManhattanDistance(adjA, adjB);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestStartAdj = adjA;
                        bestEndAdj = adjB;
                        bestStartDir = dirA;
                        bestEndDir = dirB;
                    }
                }
            }

            if (bestDist == int.MaxValue) return null;
            if (bestDist > _config.maxCorridorLength * 2) return null;

            var cells = TraceLShapePath(bestStartAdj, bestEndAdj, layout);

            var segment = new CorridorSegment { SourceEdge = edge, RoomA = roomA, RoomB = roomB };
            segment.Cells.AddRange(cells);

            Vector2Int doorCellA = bestStartAdj - GridUtils.GetOffset(bestStartDir);
            Vector2Int doorCellB = bestEndAdj - GridUtils.GetOffset(bestEndDir);

            roomA.Connections[new DoorConnection(doorCellA, bestStartDir)] = roomB;
            roomB.Connections[new DoorConnection(doorCellB, bestEndDir)] = roomA;

            return segment;
        }

        private List<(Vector2Int cell, Direction outDir)> GetRoomEdgeCells(PlacedRoom room)
        {
            var result = new List<(Vector2Int, Direction)>();
            var cells = new HashSet<Vector2Int>(room.GetCells());

            foreach (var cell in cells)
                foreach (var dir in GridUtils.CardinalDirections)
                {
                    Vector2Int neighbor = cell + GridUtils.GetOffset(dir);
                    if (!cells.Contains(neighbor))
                        result.Add((cell, dir));
                }
            return result;
        }

        private List<Vector2Int> TraceLShapePath(Vector2Int start, Vector2Int end, FloorLayout layout)
        {
            var cells = new List<Vector2Int>();
            Vector2Int current = start;
            bool xFirst = _rng.NextDouble() > 0.5;

            if (xFirst)
            {
                current = TraceAxis(current, end.x, true, cells, layout);
                TraceAxis(current, end.y, false, cells, layout);
            }
            else
            {
                current = TraceAxis(current, end.y, false, cells, layout);
                TraceAxis(current, end.x, true, cells, layout);
            }
            return cells;
        }

        private Vector2Int TraceAxis(Vector2Int current, int target, bool isX, List<Vector2Int> cells, FloorLayout layout)
        {
            while ((isX ? current.x : current.y) != target)
            {
                int val = isX ? current.x : current.y;
                int step = Math.Sign(target - val);
                current = isX
                    ? new Vector2Int(current.x + step, current.y)
                    : new Vector2Int(current.x, current.y + step);

                if (!layout.OccupiedCells.Contains(current))
                    cells.Add(current);
            }
            return current;
        }

        private static readonly Vector2Int InvalidPos = new Vector2Int(int.MinValue, int.MinValue);

        private Vector2Int FindNearestFreePosition(FloorLayout layout, Vector2Int near, int w, int h)
        {
            for (int radius = 1; radius < 50; radius++)
                for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
                        var candidate = new Vector2Int(near.x + x, near.y + y);
                        if (layout.CanPlaceRect(candidate, w, h, padding: 0))
                            return candidate;
                    }
            return new Vector2Int(near.x + 10, near.y + 10);
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
