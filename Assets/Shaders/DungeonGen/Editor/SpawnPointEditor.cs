#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DungeonSystem.Runtime;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(SpawnPoint))]
    public class SpawnPointEditor : UnityEditor.Editor
    {
        static readonly float GIZMO_SIZE = 0.6f;

        [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
        public static void DrawSpawnGizmo(SpawnPoint sp, GizmoType gizmoType)
        {
            if (sp == null) return;
            bool selected = (gizmoType & GizmoType.InSelectionHierarchy) != 0;
            Color color = GetColor(sp.pointType);
            color.a = selected ? 1f : 0.6f;
            Gizmos.color = color;
            Vector3 pos = sp.transform.position;

            switch (sp.pointType)
            {
                case SpawnPointType.Enemy:
                case SpawnPointType.BossSpawn:
                    Gizmos.DrawWireSphere(pos + Vector3.up * 0.5f, GIZMO_SIZE);
                    Gizmos.DrawLine(pos, pos + Vector3.up);
                    break;
                case SpawnPointType.Chest:
                case SpawnPointType.Item:
                    Gizmos.DrawWireCube(pos + Vector3.up * 0.3f, Vector3.one * GIZMO_SIZE * 0.8f);
                    break;
                case SpawnPointType.Trap:
                    DrawDiamond(pos + Vector3.up * 0.3f, GIZMO_SIZE * 0.5f);
                    break;
                case SpawnPointType.Light:
                    Gizmos.DrawWireSphere(pos + Vector3.up * 1.5f, GIZMO_SIZE * 0.4f);
                    Gizmos.DrawLine(pos, pos + Vector3.up * 1.5f);
                    break;
                default:
                    Gizmos.DrawWireSphere(pos + Vector3.up * 0.5f, GIZMO_SIZE * 0.5f);
                    break;
            }

            if (selected)
            {
                var style = new GUIStyle { normal = { textColor = color }, alignment = TextAnchor.MiddleCenter, fontSize = 10, fontStyle = FontStyle.Bold };
                Handles.Label(pos + Vector3.up * 2f, sp.pointType.ToString(), style);
            }
        }

        static void DrawDiamond(Vector3 center, float size)
        {
            Vector3 top = center + Vector3.up * size, bottom = center + Vector3.down * size;
            Vector3 left = center + Vector3.left * size, right = center + Vector3.right * size;
            Vector3 front = center + Vector3.forward * size, back = center + Vector3.back * size;
            Gizmos.DrawLine(top, left); Gizmos.DrawLine(top, right);
            Gizmos.DrawLine(top, front); Gizmos.DrawLine(top, back);
            Gizmos.DrawLine(bottom, left); Gizmos.DrawLine(bottom, right);
            Gizmos.DrawLine(bottom, front); Gizmos.DrawLine(bottom, back);
        }

        static Color GetColor(SpawnPointType type)
        {
            return type switch
            {
                SpawnPointType.Enemy => new Color(0.9f, 0.2f, 0.2f),
                SpawnPointType.BossSpawn => new Color(1f, 0f, 0f),
                SpawnPointType.Item => new Color(1f, 0.85f, 0.2f),
                SpawnPointType.Chest => new Color(0.9f, 0.7f, 0.1f),
                SpawnPointType.Trap => new Color(1f, 0.5f, 0.1f),
                SpawnPointType.NPC => new Color(0.3f, 0.8f, 0.3f),
                SpawnPointType.PlayerSpawn => new Color(0.2f, 0.6f, 1f),
                SpawnPointType.Light => new Color(1f, 1f, 0.6f),
                SpawnPointType.Prop => new Color(0.6f, 0.6f, 0.6f),
                SpawnPointType.PuzzleObject => new Color(0.4f, 0.7f, 1f),
                _ => Color.white
            };
        }
    }
}
#endif
