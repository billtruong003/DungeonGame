using System.Collections.Generic;
using UnityEngine;
using DungeonSystem.Core;
using DungeonSystem.Graph;
using System;

namespace DungeonSystem.Layout
{
    /// <summary>
    /// Represents the spatial arrangement of one floor.
    /// Maps grid cells to placed rooms and corridors.
    /// </summary>
    public class FloorLayout
    {
        /// <summary> Cell → which placed room owns it. </summary>
        public Dictionary<Vector2Int, PlacedRoom> CellMap { get; } = new Dictionary<Vector2Int, PlacedRoom>();

        /// <summary> All placed rooms. </summary>
        public List<PlacedRoom> Rooms { get; } = new List<PlacedRoom>();

        /// <summary> Corridor segments between rooms. </summary>
        public List<CorridorSegment> Corridors { get; } = new List<CorridorSegment>();

        /// <summary> All occupied cells (rooms + corridors). </summary>
        public HashSet<Vector2Int> OccupiedCells { get; } = new HashSet<Vector2Int>();

        public bool IsCellFree(Vector2Int cell) => !OccupiedCells.Contains(cell);

        public bool CanPlaceRect(Vector2Int origin, int w, int h, int padding = 0)
        {
            return !GridUtils.RectOverlaps(origin, w, h, OccupiedCells, padding);
        }

        public PlacedRoom AddRoom(GraphNode node, Vector2Int gridPos, int w, int h)
        {
            var room = new PlacedRoom
            {
                Node = node,
                GridPosition = gridPos,
                Width = w,
                Height = h
            };

            Rooms.Add(room);

            var cells = GridUtils.GetOccupiedCells(gridPos, w, h);
            foreach (var cell in cells)
            {
                CellMap[cell] = room;
                OccupiedCells.Add(cell);
            }

            return room;
        }

        public void AddCorridorCell(Vector2Int cell, CorridorSegment segment)
        {
            OccupiedCells.Add(cell);
            if (!CellMap.ContainsKey(cell))
            {
                // Corridor cells don't belong to a room
            }
        }

        /// <summary>
        /// Find which placed room a grid cell belongs to.
        /// </summary>
        public PlacedRoom GetRoomAt(Vector2Int cell)
        {
            return CellMap.TryGetValue(cell, out var room) ? room : null;
        }
    }

    /// <summary>
    /// A room placed at a specific grid position.
    /// </summary>
    public class PlacedRoom
    {
        public GraphNode Node { get; set; }
        public Vector2Int GridPosition { get; set; }
        public int Width { get; set; } = 1;
        public int Height { get; set; } = 1;
        public int RotationSteps { get; set; } = 0;
        public Data.RoomTemplate Template { get; set; }  // Assigned during instantiation

        /// <summary>
        /// All grid cells this room covers.
        /// </summary>
        public List<Vector2Int> GetCells() => GridUtils.GetOccupiedCells(GridPosition, Width, Height);

        /// <summary>
        /// Connections: direction → neighbor PlacedRoom.
        /// Populated after layout is complete.
        /// </summary>
        public Dictionary<DoorConnection, PlacedRoom> Connections { get; } = new Dictionary<DoorConnection, PlacedRoom>();
    }

    /// <summary>
    /// Describes a door connection at a specific cell edge.
    /// </summary>
    public struct DoorConnection : System.IEquatable<DoorConnection>
    {
        public Vector2Int Cell;       // The cell that has this door
        public Direction Direction;   // Which edge of the cell

        public DoorConnection(Vector2Int cell, Direction dir) { Cell = cell; Direction = dir; }

        public bool Equals(DoorConnection other) => Cell == other.Cell && Direction == other.Direction;
        public override bool Equals(object obj) => obj is DoorConnection dc && Equals(dc);
        public override int GetHashCode() => HashCode.Combine(Cell, Direction);
    }

    /// <summary>
    /// A corridor connecting two rooms, stored as a sequence of cells.
    /// </summary>
    public class CorridorSegment
    {
        public GraphEdge SourceEdge { get; set; }
        public PlacedRoom RoomA { get; set; }
        public PlacedRoom RoomB { get; set; }
        public List<Vector2Int> Cells { get; } = new List<Vector2Int>();
    }
}
