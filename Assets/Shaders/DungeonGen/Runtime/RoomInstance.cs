using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Core;
using DungeonSystem.Data;

namespace DungeonSystem.Runtime
{
    public class RoomInstance : MonoBehaviour
    {
        [Header("Room Info (set by generator)")]
        public RoomType roomType;
        public int widthInCells = 1;
        public int heightInCells = 1;
        public int depth;
        public int floorIndex;

        [Header("Sockets")]
        public List<DoorSocket> sockets = new List<DoorSocket>();

        public Vector2Int GridPosition { get; private set; }
        public RoomTemplate SourceTemplate { get; set; }
        public Graph.GraphNode GraphNode { get; set; }

        public void Initialize(Vector2Int gridPosition, int depth, float cellSize, int floor)
        {
            GridPosition = gridPosition;
            this.depth = depth;
            this.floorIndex = floor;

            float worldX = gridPosition.x * cellSize + (widthInCells * cellSize * 0.5f);
            float worldZ = gridPosition.y * cellSize + (heightInCells * cellSize * 0.5f);
            float worldY = floor * 20f;

            transform.position = new Vector3(worldX, worldY, worldZ);
        }

        public void CollectSockets()
        {
            sockets.Clear();
            sockets.AddRange(GetComponentsInChildren<DoorSocket>(true));
        }

        public DoorSocket GetSocket(Vector2Int cellOffset, Direction direction)
        {
            return sockets.FirstOrDefault(s =>
                s.socketDirection == direction && s.cellOffset == cellOffset);
        }

        public DoorSocket GetSocket(Direction direction)
        {
            return sockets.FirstOrDefault(s => s.socketDirection == direction);
        }

        public List<Vector2Int> GetOccupiedCells()
        {
            return GridUtils.GetOccupiedCells(GridPosition, widthInCells, heightInCells);
        }

        public void WallAllSockets()
        {
            foreach (var socket in sockets)
                socket.SetState(DoorState.Walled);
        }
    }
}
