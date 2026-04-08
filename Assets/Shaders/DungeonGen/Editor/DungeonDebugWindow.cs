#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using DungeonSystem.Core;
using DungeonSystem.Runtime;
using DungeonSystem.Graph;
using DungeonSystem.Layout;

namespace DungeonSystem.Editor
{
    public class DungeonDebugWindow : EditorWindow
    {
        private static bool _showGraph = true;
        private static bool _showGrid = true;
        private static bool _showLabels = true;
        private static int _visibleFloor = 0;

        [MenuItem("DungeonSystem/Debug Visualizer")]
        public static void ShowWindow() => GetWindow<DungeonDebugWindow>("Dungeon Debug");

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

        private void OnGUI()
        {
            GUILayout.Label("Dungeon Debug Visualizer", EditorStyles.boldLabel);
            _showGraph = EditorGUILayout.Toggle("Show Graph Edges", _showGraph);
            _showGrid = EditorGUILayout.Toggle("Show Grid Cells", _showGrid);
            _showLabels = EditorGUILayout.Toggle("Show Labels", _showLabels);
            _visibleFloor = EditorGUILayout.IntField("Visible Floor", _visibleFloor);
            if (GUILayout.Button("Repaint Scene")) SceneView.RepaintAll();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            var generator = Object.FindObjectOfType<DungeonGenerator>();
            if (generator == null || generator.FloorResults == null) return;

            foreach (var floor in generator.FloorResults)
            {
                if (floor.FloorIndex != _visibleFloor) continue;
                float cellSize = generator.config != null ? generator.config.cellSize : 16f;
                float yOffset = floor.FloorIndex * (generator.config != null ? generator.config.floorYSpacing : 20f);

                if (_showGrid) DrawGridCells(floor.Layout, cellSize, yOffset);
                if (_showGraph) DrawGraphEdges(floor, cellSize, yOffset);
                if (_showLabels) DrawRoomLabels(floor, cellSize, yOffset);
            }
        }

        private static void DrawGridCells(FloorLayout layout, float cellSize, float yOffset)
        {
            foreach (var cell in layout.OccupiedCells)
            {
                Vector3 worldPos = new Vector3(cell.x * cellSize + cellSize * 0.5f, yOffset + 0.05f, cell.y * cellSize + cellSize * 0.5f);
                bool isRoom = layout.CellMap.ContainsKey(cell);
                Color color = isRoom ? new Color(0.3f, 0.6f, 0.3f, 0.15f) : new Color(0.5f, 0.5f, 0.2f, 0.1f);
                Handles.color = color;
                Handles.DrawSolidRectangleWithOutline(
                    new Vector3[] {
                        worldPos + new Vector3(-cellSize * 0.45f, 0, -cellSize * 0.45f),
                        worldPos + new Vector3( cellSize * 0.45f, 0, -cellSize * 0.45f),
                        worldPos + new Vector3( cellSize * 0.45f, 0,  cellSize * 0.45f),
                        worldPos + new Vector3(-cellSize * 0.45f, 0,  cellSize * 0.45f)
                    }, color, new Color(1, 1, 1, 0.1f));
            }
        }

        private static void DrawGraphEdges(FloorResult floor, float cellSize, float yOffset)
        {
            foreach (var edge in floor.Graph.Edges)
            {
                var roomA = floor.Layout.Rooms.FirstOrDefault(r => r.Node == edge.A);
                var roomB = floor.Layout.Rooms.FirstOrDefault(r => r.Node == edge.B);
                if (roomA == null || roomB == null) continue;
                Vector3 posA = GridToWorld(roomA.GridPosition, roomA.Width, roomA.Height, cellSize, yOffset);
                Vector3 posB = GridToWorld(roomB.GridPosition, roomB.Width, roomB.Height, cellSize, yOffset);

                if (edge.IsShortcut) { Handles.color = new Color(1f, 0.5f, 0f, 0.7f); Handles.DrawDottedLine(posA, posB, 4f); }
                else if (edge.IsSecret) { Handles.color = new Color(0.7f, 0f, 0.7f, 0.5f); Handles.DrawDottedLine(posA, posB, 2f); }
                else { Handles.color = new Color(0.4f, 0.8f, 1f, 0.6f); Handles.DrawLine(posA, posB); }
            }
        }

        private static void DrawRoomLabels(FloorResult floor, float cellSize, float yOffset)
        {
            var style = new GUIStyle { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
            foreach (var room in floor.Layout.Rooms)
            {
                Vector3 pos = GridToWorld(room.GridPosition, room.Width, room.Height, cellSize, yOffset);
                pos.y += 2f;
                string label = $"{room.Node.Type}\n#{room.Node.Id}";
                if (room.Node.IsMainPath) label += "\n[MAIN]";
                if (room.Node.IsDeadEnd) label += "\n[DEAD END]";
                style.normal.textColor = GetRoomTypeColor(room.Node.Type);
                Handles.Label(pos, label, style);
            }
        }

        private static Vector3 GridToWorld(Vector2Int gridPos, int w, int h, float cellSize, float yOffset)
        {
            return new Vector3(gridPos.x * cellSize + w * cellSize * 0.5f, yOffset + 0.1f, gridPos.y * cellSize + h * cellSize * 0.5f);
        }

        private static Color GetRoomTypeColor(RoomType type)
        {
            return type switch
            {
                RoomType.Start => Color.green, RoomType.Boss => Color.red,
                RoomType.MiniBoss => new Color(1f, 0.5f, 0f), RoomType.Loot => Color.yellow,
                RoomType.Puzzle => Color.cyan, RoomType.Shop => new Color(0.5f, 1f, 0.5f),
                RoomType.SafeRoom => new Color(0.5f, 0.8f, 1f), RoomType.SecretRoom => new Color(0.8f, 0.3f, 0.8f),
                RoomType.Trap => new Color(1f, 0.6f, 0.2f), RoomType.Corridor => new Color(0.6f, 0.6f, 0.6f),
                _ => Color.white
            };
        }
    }
}
#endif
