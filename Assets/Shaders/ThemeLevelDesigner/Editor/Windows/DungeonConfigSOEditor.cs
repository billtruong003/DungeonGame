using UnityEditor;
using UnityEngine;

namespace ThemeLevelDesigner.Editor
{
    [CustomEditor(typeof(DungeonConfigSO))]
    public class DungeonConfigSOEditor : UnityEditor.Editor
    {
        GeneratedDungeon _lastGenerated;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var config = (DungeonConfigSO)target;

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generation", EditorStyles.boldLabel);

            if (GUILayout.Button("Preview in Scene View (Prefabs)"))
            {
                _lastGenerated = DungeonGenerator.Generate(config);
                LevelDesignerSceneSync.PreviewDungeon(_lastGenerated);
                Debug.Log($"[DungeonGen] Generated {_lastGenerated.rooms.Count} rooms (seed: {_lastGenerated.seed})");
            }

            if (GUILayout.Button("Clear Scene Preview"))
            {
                LevelDesignerSceneSync.ClearScene();
                _lastGenerated = null;
            }

            if (_lastGenerated != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"Last generated: {_lastGenerated.rooms.Count} rooms, seed {_lastGenerated.seed}",
                    MessageType.Info);

                foreach (var room in _lastGenerated.rooms)
                {
                    string roomName = room.room != null ? room.room.roomName : "Empty";
                    string type = room.node.requiredType.HasValue ? $" [{room.node.requiredType}]" : "";
                    string path = room.node.isCriticalPath ? " (critical)" : "";
                    EditorGUILayout.LabelField($"  #{room.node.index}: {roomName}{type}{path} @ {room.worldGridPos} diff={room.difficulty:F1}");
                }
            }
        }
    }

    [CustomEditor(typeof(DungeonInstantiator))]
    public class DungeonInstantiatorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var inst = (DungeonInstantiator)target;

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Instantiate Now (Editor)"))
            {
                if (inst.useMapData && inst.mapData != null)
                    inst.InstantiateMap(inst.mapData);
                else if (inst.dungeonConfig != null)
                    inst.InstantiateGenerated(inst.dungeonConfig);
            }

            if (GUILayout.Button("Clear"))
            {
                inst.Clear();
            }
        }
    }
}
