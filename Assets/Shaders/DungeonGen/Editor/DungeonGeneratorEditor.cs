#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DungeonSystem.Runtime;
using DungeonSystem.Core;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(DungeonGenerator))]
    public class DungeonGeneratorEditor : UnityEditor.Editor
    {
        private bool _showDebug = false;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var generator = (DungeonGenerator)target;

            GUILayout.Space(16);

            if (generator.config != null && generator.config.roomDatabase != null)
            {
                var db = generator.config.roomDatabase;
                if (db.allTemplates == null || db.allTemplates.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "RoomDatabase is empty! Click below to auto-generate default rooms for all types, " +
                        "or open DungeonSystem > Room Template Generator for more options.",
                        MessageType.Warning);

                    GUI.backgroundColor = new Color(0.9f, 0.5f, 0.1f);
                    if (GUILayout.Button("Auto-Generate Default Rooms into Database", GUILayout.Height(32)))
                    {
                        int count = RoomTemplateGenerator.GenerateAllDefaultsForDatabase(db);
                        EditorUtility.DisplayDialog("Done", $"Created {count} room templates.", "OK");
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.Space(8);
                }
            }

            GUI.backgroundColor = new Color(0.3f, 0.7f, 0.3f);
            if (GUILayout.Button("Generate Dungeon", GUILayout.Height(40)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Dungeon");
                generator.GenerateDungeon();
            }
            GUI.backgroundColor = Color.white;

            GUI.backgroundColor = new Color(0.7f, 0.3f, 0.3f);
            if (GUILayout.Button("Clear Dungeon", GUILayout.Height(28)))
            {
                Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Dungeon");
                generator.ClearDungeon();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            _showDebug = EditorGUILayout.Foldout(_showDebug, "Debug Info");
            if (_showDebug && generator.FloorResults != null)
            {
                EditorGUI.indentLevel++;
                foreach (var floor in generator.FloorResults)
                {
                    EditorGUILayout.LabelField($"Floor {floor.FloorIndex}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"  Nodes: {floor.Graph.Nodes.Count}");
                    EditorGUILayout.LabelField($"  Edges: {floor.Graph.Edges.Count}");
                    EditorGUILayout.LabelField($"  Corridors: {floor.Layout.Corridors.Count}");
                    EditorGUILayout.LabelField($"  Occupied Cells: {floor.Layout.OccupiedCells.Count}");

                    var typeCounts = new System.Collections.Generic.Dictionary<RoomType, int>();
                    foreach (var node in floor.Graph.Nodes)
                    {
                        typeCounts.TryGetValue(node.Type, out int c);
                        typeCounts[node.Type] = c + 1;
                    }
                    foreach (var kv in typeCounts)
                        EditorGUILayout.LabelField($"    {kv.Key}: {kv.Value}");

                    GUILayout.Space(4);
                }
                EditorGUI.indentLevel--;
            }
        }
    }
}
#endif
