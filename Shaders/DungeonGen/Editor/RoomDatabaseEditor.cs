#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using DungeonSystem.Data;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(RoomDatabase))]
    public class RoomDatabaseEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var db = (RoomDatabase)target;

            GUILayout.Space(12);

            // Show summary
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            var cache = db.GetCache();
            foreach (var kv in cache)
            {
                if (kv.Value.Count > 0)
                    EditorGUILayout.LabelField($"  {kv.Key}", $"{kv.Value.Count} templates");
            }

            GUILayout.Space(8);

            // Auto-generate button (prominent when database is empty)
            bool isEmpty = db.allTemplates == null || db.allTemplates.Count == 0;

            if (isEmpty)
            {
                EditorGUILayout.HelpBox(
                    "Database is empty! Click below to auto-generate default room templates for all room types.",
                    MessageType.Warning);
            }

            GUI.backgroundColor = isEmpty ? new Color(0.9f, 0.5f, 0.1f) : new Color(0.3f, 0.6f, 0.8f);
            string btnLabel = isEmpty
                ? "Auto-Generate ALL Default Room Templates"
                : "Re-generate Missing Room Templates";

            if (GUILayout.Button(btnLabel, GUILayout.Height(isEmpty ? 40 : 28)))
            {
                int count = RoomTemplateGenerator.GenerateAllDefaultsForDatabase(db);
                EditorUtility.DisplayDialog("Auto-Generate Complete",
                    $"Created/found {count} room templates and added them to this database.", "OK");
            }
            GUI.backgroundColor = Color.white;

            // Clean nulls button
            if (!isEmpty && GUILayout.Button("Remove Null Entries"))
            {
                Undo.RecordObject(db, "Clean nulls");
                db.allTemplates.RemoveAll(t => t == null);
                db.InvalidateCache();
                EditorUtility.SetDirty(db);
            }
        }
    }
}
#endif
