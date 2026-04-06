using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Core;
using DungeonSystem.Data;

namespace DungeonSystem.Runtime
{
    /// <summary>
    /// Runtime component attached to every instantiated room.
    /// Manages its sockets and exposes room metadata.
    /// </summary>
    public class RoomInstance : MonoBehaviour
    {
        [Header("Room Info (set by generator)")]
        public RoomType roomType;
        public int widthInCells = 1;
        public int heightInCells = 1;
        public int depth;                       // Graph depth (distance from start)
        public int floorIndex;

        [Header("Sockets")]
        public List<DoorSocket> sockets = new List<DoorSocket>();

        // Runtime data (not serialized)
        public Vector2Int GridPosition { get; private set; }
        public RoomTemplate SourceTemplate { get; set; }
        public Graph.GraphNode GraphNode { get; set; }

        /// <summary>
        /// Initialize position and metadata. Called by RoomInstantiator.
        /// </summary>
        public void Initialize(Vector2Int gridPosition, int depth, float cellSize, int floor)
        {
            GridPosition = gridPosition;
            this.depth = depth;
            this.floorIndex = floor;

            // Position: center of the multi-cell footprint
            float worldX = gridPosition.x * cellSize + (widthInCells * cellSize * 0.5f);
            float worldZ = gridPosition.y * cellSize + (heightInCells * cellSize * 0.5f);
            float worldY = floor * 20f; // Floor Y offset (configurable externally)

            transform.position = new Vector3(worldX, worldY, worldZ);
        }

        /// <summary>
        /// Auto-detect sockets from child DoorSocket components.
        /// Call after instantiation if sockets list is empty.
        /// </summary>
        public void CollectSockets()
        {
            sockets.Clear();
            sockets.AddRange(GetComponentsInChildren<DoorSocket>(true));
        }

        /// <summary>
        /// Find a socket at a given cell offset + direction.
        /// </summary>
        public DoorSocket GetSocket(Vector2Int cellOffset, Direction direction)
        {
            return sockets.FirstOrDefault(s =>
                s.socketDirection == direction && s.cellOffset == cellOffset);
        }

        /// <summary>
        /// Find any socket facing a given direction.
        /// </summary>
        public DoorSocket GetSocket(Direction direction)
        {
            return sockets.FirstOrDefault(s => s.socketDirection == direction);
        }

        /// <summary>
        /// Get all grid cells this room occupies.
        /// </summary>
        public List<Vector2Int> GetOccupiedCells()
        {
            return GridUtils.GetOccupiedCells(GridPosition, widthInCells, heightInCells);
        }

        /// <summary>
        /// Set all sockets to walled by default. Connections will open specific sockets.
        /// </summary>
        public void WallAllSockets()
        {
            foreach (var socket in sockets)
                socket.SetState(DoorState.Walled);
        }
    }
}
