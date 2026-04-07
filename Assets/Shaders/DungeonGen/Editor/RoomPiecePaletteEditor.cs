#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Data;

namespace DungeonSystem.Editor
{
    [CustomEditor(typeof(RoomPiecePalette))]
    public class RoomPiecePaletteEditor : UnityEditor.Editor
    {
        bool _showFloor = true;
        bool _showWalls = true;
        bool _showDoors = true;
        bool _showStructural = true;
        bool _showDeco = true;
        bool _showOverrides = true;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var palette = (RoomPiecePalette)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Room Piece Palette", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            _showFloor = DrawPieceSection("Floor & Ceiling Tiles", _showFloor,
                serializedObject.FindProperty("floorTiles"), palette.floorTiles, v => palette.floorTiles = v);
            DrawPieceSection("Ceiling Tiles", _showFloor,
                serializedObject.FindProperty("ceilingTiles"), palette.ceilingTiles, v => palette.ceilingTiles = v);

            _showWalls = DrawPieceSection("Wall Segments", _showWalls,
                serializedObject.FindProperty("wallSegments"), palette.wallSegments, v => palette.wallSegments = v);

            _showDoors = DrawPieceSection("Door Frames", _showDoors,
                serializedObject.FindProperty("doorFrames"), palette.doorFrames, v => palette.doorFrames = v);
            DrawPieceSection("Door Locked", _showDoors,
                serializedObject.FindProperty("doorLockedFrames"), palette.doorLockedFrames, v => palette.doorLockedFrames = v);
            DrawPieceSection("Door Secret", _showDoors,
                serializedObject.FindProperty("doorSecretFrames"), palette.doorSecretFrames, v => palette.doorSecretFrames = v);

            _showStructural = DrawPieceSection("Corner / Joint Pillars", _showStructural,
                serializedObject.FindProperty("pillars"), palette.pillars, v => palette.pillars = v);
            DrawPieceSection("Map Pillars (internal grid)", _showStructural,
                serializedObject.FindProperty("mapPillars"), palette.mapPillars, v => palette.mapPillars = v);

            _showDeco = DrawPieceSection("Torches", _showDeco,
                serializedObject.FindProperty("torches"), palette.torches, v => palette.torches = v);
            DrawPieceSection("Wall Props", _showDeco,
                serializedObject.FindProperty("wallProps"), palette.wallProps, v => palette.wallProps = v);
            DrawPieceSection("Corner Props", _showDeco,
                serializedObject.FindProperty("cornerProps"), palette.cornerProps, v => palette.cornerProps = v);
            DrawPieceSection("Floor Props", _showDeco,
                serializedObject.FindProperty("floorProps"), palette.floorProps, v => palette.floorProps = v);
            DrawPieceSection("Ceiling Props", _showDeco,
                serializedObject.FindProperty("ceilingProps"), palette.ceilingProps, v => palette.ceilingProps = v);

            EditorGUILayout.Space(8);
            _showOverrides = EditorGUILayout.Foldout(_showOverrides, "Room Type Overrides", true, EditorStyles.foldoutHeader);
            if (_showOverrides)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("overrides"), true);

            EditorGUILayout.Space(12);
            DrawUtilityButtons(palette);

            serializedObject.ApplyModifiedProperties();
        }

        bool DrawPieceSection(string label, bool foldout, SerializedProperty arrayProp,
            PieceEntry[] currentArray, System.Action<PieceEntry[]> setter)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            foldout = EditorGUILayout.Foldout(foldout, $"{label}[{(currentArray != null ? currentArray.Length : 0)}]", true, EditorStyles.foldoutHeader);

            int selectedPrefabCount = GetSelectedPrefabs().Count;
            GUI.enabled = selectedPrefabCount > 0;
            GUI.backgroundColor = selectedPrefabCount > 0 ? new Color(0.3f, 0.7f, 0.4f) : Color.gray;

            if (GUILayout.Button($"+ Add Selected ({selectedPrefabCount})", GUILayout.Width(140), GUILayout.Height(20)))
            {
                Undo.RecordObject(target, $"Add pieces to {label}");
                var newEntries = CreateEntriesFromSelection();
                setter(MergeArrays(currentArray, newEntries));
                EditorUtility.SetDirty(target);
            }

            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (foldout)
            {
                EditorGUI.indentLevel++;

                if (currentArray != null && currentArray.Length > 0)
                {
                    for (int i = 0; i < currentArray.Length; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        DrawPieceEntryCompact(arrayProp, i, currentArray[i]);

                        GUI.backgroundColor = new Color(0.8f, 0.3f, 0.3f);
                        if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
                        {
                            Undo.RecordObject(target, "Remove piece");
                            var list = new List<PieceEntry>(currentArray);
                            list.RemoveAt(i);
                            setter(list.ToArray());
                            EditorUtility.SetDirty(target);
                            break;
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Empty — select prefabs in Project and click 'Add Selected'", EditorStyles.miniLabel);
                }

                if (currentArray != null && currentArray.Length > 0)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Re-measure All Bounds", GUILayout.Width(160)))
                    {
                        Undo.RecordObject(target, "Re-measure bounds");
                        foreach (var entry in currentArray)
                            if (entry.prefab != null) ApplyMeasuredBounds(entry);
                        EditorUtility.SetDirty(target);
                    }
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            return foldout;
        }

        void DrawPieceEntryCompact(SerializedProperty arrayProp, int index, PieceEntry entry)
        {
            if (entry == null) return;
            string sizeLabel = $"{entry.widthOverride:F1}x{entry.heightOverride:F1}x{entry.depthOverride:F1}";
            var element = arrayProp.GetArrayElementAtIndex(index);
            EditorGUILayout.PropertyField(element.FindPropertyRelative("prefab"), GUIContent.none, GUILayout.MinWidth(120));
            EditorGUILayout.LabelField(sizeLabel, EditorStyles.miniLabel, GUILayout.Width(90));
            EditorGUILayout.PropertyField(element.FindPropertyRelative("spawnWeight"), GUIContent.none, GUILayout.Width(50));
        }

        void DrawUtilityButtons(RoomPiecePalette palette)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Re-measure ALL Bounds"))
            {
                Undo.RecordObject(target, "Re-measure all");
                RemeasureAll(palette.floorTiles);
                RemeasureAll(palette.wallSegments);
                RemeasureAll(palette.doorFrames);
                RemeasureAll(palette.doorLockedFrames);
                RemeasureAll(palette.doorSecretFrames);
                RemeasureAll(palette.pillars);
                RemeasureAll(palette.mapPillars);
                RemeasureAll(palette.ceilingTiles);
                RemeasureAll(palette.torches);
                RemeasureAll(palette.wallProps);
                RemeasureAll(palette.cornerProps);
                RemeasureAll(palette.floorProps);
                RemeasureAll(palette.ceilingProps);
                EditorUtility.SetDirty(target);
            }

            if (GUILayout.Button("Clear All Empty Slots"))
            {
                Undo.RecordObject(target, "Clear empty slots");
                palette.floorTiles = RemoveNulls(palette.floorTiles);
                palette.wallSegments = RemoveNulls(palette.wallSegments);
                palette.doorFrames = RemoveNulls(palette.doorFrames);
                palette.doorLockedFrames = RemoveNulls(palette.doorLockedFrames);
                palette.doorSecretFrames = RemoveNulls(palette.doorSecretFrames);
                palette.pillars = RemoveNulls(palette.pillars);
                palette.mapPillars = RemoveNulls(palette.mapPillars);
                palette.ceilingTiles = RemoveNulls(palette.ceilingTiles);
                palette.torches = RemoveNulls(palette.torches);
                palette.wallProps = RemoveNulls(palette.wallProps);
                palette.cornerProps = RemoveNulls(palette.cornerProps);
                palette.floorProps = RemoveNulls(palette.floorProps);
                palette.ceilingProps = RemoveNulls(palette.ceilingProps);
                EditorUtility.SetDirty(target);
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        List<GameObject> GetSelectedPrefabs()
        {
            return Selection.gameObjects
                .Where(go => PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.NotAPrefab || AssetDatabase.Contains(go))
                .Where(go => go != null).ToList();
        }

        PieceEntry[] CreateEntriesFromSelection()
        {
            var prefabs = GetSelectedPrefabs();
            var entries = new PieceEntry[prefabs.Count];
            for (int i = 0; i < prefabs.Count; i++)
            {
                entries[i] = new PieceEntry { prefab = prefabs[i], spawnWeight = 1f };
                ApplyMeasuredBounds(entries[i]);
            }
            return entries;
        }

        void ApplyMeasuredBounds(PieceEntry entry)
        {
            if (entry.prefab == null) return;
            Vector3 size = MeasurePrefabSize(entry.prefab);
            entry.widthOverride = size.x;
            entry.heightOverride = size.y;
            entry.depthOverride = size.z;
        }

        Vector3 MeasurePrefabSize(GameObject prefab)
        {
            var instance = Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool init = false;

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (!init) { bounds = r.bounds; init = true; }
                else bounds.Encapsulate(r.bounds);
            }

            if (!init)
                foreach (var col in instance.GetComponentsInChildren<Collider>(true))
                {
                    if (!init) { bounds = col.bounds; init = true; }
                    else bounds.Encapsulate(col.bounds);
                }

            DestroyImmediate(instance);
            if (!init) return Vector3.one;

            Vector3 result = bounds.size;
            result.x = Mathf.Max(result.x, 0.01f);
            result.y = Mathf.Max(result.y, 0.01f);
            result.z = Mathf.Max(result.z, 0.01f);
            return result;
        }

        PieceEntry[] MergeArrays(PieceEntry[] existing, PieceEntry[] additions)
        {
            if (existing == null || existing.Length == 0) return additions;
            if (additions == null || additions.Length == 0) return existing;
            var merged = new List<PieceEntry>(existing);
            foreach (var add in additions)
                if (!merged.Any(e => e.prefab == add.prefab)) merged.Add(add);
            return merged.ToArray();
        }

        void RemeasureAll(PieceEntry[] entries)
        {
            if (entries == null) return;
            foreach (var e in entries)
                if (e != null && e.prefab != null) ApplyMeasuredBounds(e);
        }

        PieceEntry[] RemoveNulls(PieceEntry[] entries)
        {
            if (entries == null) return entries;
            return entries.Where(e => e != null && e.prefab != null).ToArray();
        }
    }
}
#endif