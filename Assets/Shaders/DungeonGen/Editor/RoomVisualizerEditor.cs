#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DungeonSystem.Runtime;
using DungeonSystem.Core;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(RoomInstance))]
    public class RoomVisualizerEditor : UnityEditor.Editor
    {
        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
        public static void DrawRoomGizmos(RoomInstance room, GizmoType gizmoType)
        {
            if (room == null) return;
            bool isSelected = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
            float cellSize = 16f;
            var gen = room.GetComponentInParent<DungeonGenerator>();
            if (gen != null && gen.config != null) cellSize = gen.config.cellSize;
            DrawRoomBounds(room, isSelected, cellSize);
            DrawSockets(room, isSelected);
        }

        private static void DrawRoomBounds(RoomInstance room, bool isSelected, float cellSize)
        {
            Color typeColor = GetTypeColor(room.roomType);
            Gizmos.color = isSelected
                ? new Color(typeColor.r, typeColor.g, typeColor.b, 0.4f)
                : new Color(typeColor.r, typeColor.g, typeColor.b, 0.15f);
            Vector3 size = new Vector3(room.widthInCells * cellSize, 1f, room.heightInCells * cellSize);
            Gizmos.DrawWireCube(room.transform.position, size);
            if (isSelected)
            {
                Gizmos.color = new Color(typeColor.r, typeColor.g, typeColor.b, 0.08f);
                Gizmos.DrawCube(room.transform.position, size);
            }
        }

        private static void DrawSockets(RoomInstance room, bool isSelected)
        {
            if (room.sockets == null) return;
            foreach (var socket in room.sockets)
            {
                if (socket == null) continue;
                Vector3 pos = socket.transform.position;
                Color socketColor = socket.CurrentState switch
                {
                    DoorState.Open => Color.green, DoorState.Walled => new Color(0.5f, 0.5f, 0.5f),
                    DoorState.Locked => Color.yellow, DoorState.Hidden => new Color(0.6f, 0.2f, 0.6f),
                    _ => Color.white
                };
                Gizmos.color = isSelected ? socketColor : new Color(socketColor.r, socketColor.g, socketColor.b, 0.4f);
                Gizmos.DrawSphere(pos, 0.5f);
                if (isSelected)
                {
                    Vector3 dir = socket.socketDirection switch
                    {
                        Direction.North => Vector3.forward, Direction.East => Vector3.right,
                        Direction.South => Vector3.back, Direction.West => Vector3.left, _ => Vector3.zero
                    };
                    Gizmos.DrawLine(pos, pos + dir * 1.5f);
                }
            }
        }

        private static Color GetTypeColor(RoomType type)
        {
            return type switch
            {
                RoomType.Start => new Color(0.2f, 0.8f, 0.2f), RoomType.Boss => new Color(0.8f, 0.1f, 0.1f),
                RoomType.MiniBoss => new Color(0.8f, 0.4f, 0.1f), RoomType.Combat => new Color(0.5f, 0.5f, 0.5f),
                RoomType.Loot => new Color(0.8f, 0.7f, 0.1f), RoomType.Puzzle => new Color(0.1f, 0.5f, 0.8f),
                RoomType.Shop => new Color(0.3f, 0.7f, 0.3f), RoomType.SafeRoom => new Color(0.3f, 0.6f, 0.8f),
                RoomType.SecretRoom => new Color(0.6f, 0.1f, 0.6f), RoomType.Trap => new Color(0.7f, 0.4f, 0.1f),
                RoomType.Corridor => new Color(0.4f, 0.4f, 0.4f), _ => Color.gray
            };
        }
    }
}
#endif
