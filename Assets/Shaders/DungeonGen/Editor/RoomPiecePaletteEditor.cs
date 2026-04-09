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

            DrawPieceSection("Baseboards (floor-wall trim)", _showFloor,
                serializedObject.FindProperty("baseboards"), palette.baseboards, v => palette.baseboards = v);

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

                        // Expandable bounds editor row
                        DrawBoundsEditor(arrayProp, i, currentArray[i]);
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

        // Track which entries have bounds editor expanded (keyed by prefab instance ID + array index)
        static HashSet<int> _expandedBoundsEntries = new HashSet<int>();

        static int GetEntryKey(PieceEntry entry, int index)
        {
            int prefabId = entry?.prefab != null ? entry.prefab.GetInstanceID() : 0;
            return prefabId ^ (index * 397);
        }

        void DrawPieceEntryCompact(SerializedProperty arrayProp, int index, PieceEntry entry)
        {
            if (entry == null) return;
            var element = arrayProp.GetArrayElementAtIndex(index);

            // Prefab field
            EditorGUILayout.PropertyField(element.FindPropertyRelative("prefab"), GUIContent.none, GUILayout.MinWidth(120));

            // Size button — click to toggle bounds editor
            int key = GetEntryKey(entry, index);
            bool isExpanded = _expandedBoundsEntries.Contains(key);
            bool hasManualBounds = entry.widthOverride > 0 || entry.heightOverride > 0 || entry.depthOverride > 0;

            string sizeLabel = hasManualBounds
                ? $"{entry.widthOverride:F1}×{entry.heightOverride:F1}×{entry.depthOverride:F1}"
                : "auto";

            // Color: green if manual bounds set, gray if auto
            GUI.backgroundColor = isExpanded ? new Color(0.4f, 0.7f, 0.9f)
                : hasManualBounds ? new Color(0.5f, 0.8f, 0.5f)
                : new Color(0.7f, 0.7f, 0.7f);

            if (GUILayout.Button(new GUIContent(sizeLabel, "Click to edit bounds manually"),
                EditorStyles.miniButton, GUILayout.Width(90), GUILayout.Height(18)))
            {
                if (isExpanded) _expandedBoundsEntries.Remove(key);
                else _expandedBoundsEntries.Add(key);
            }
            GUI.backgroundColor = Color.white;

            // Weight
            EditorGUILayout.PropertyField(element.FindPropertyRelative("spawnWeight"), GUIContent.none, GUILayout.Width(50));

            // Horizontal toggle
            var horizProp = element.FindPropertyRelative("isPreRotatedHorizontal");
            if (horizProp != null)
            {
                horizProp.boolValue = GUILayout.Toggle(horizProp.boolValue,
                    new GUIContent("H", "Pre-rotated horizontal: piece is already lying flat, no 90° rotation needed"),
                    "Button", GUILayout.Width(22), GUILayout.Height(18));
            }
        }

        /// <summary>
        /// Draw the expanded bounds editor row below a piece entry.
        /// Returns true if the entry was modified.
        /// </summary>
        bool DrawBoundsEditor(SerializedProperty arrayProp, int index, PieceEntry entry)
        {
            int key = GetEntryKey(entry, index);
            if (!_expandedBoundsEntries.Contains(key)) return false;

            var element = arrayProp.GetArrayElementAtIndex(index);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20); // indent

            // W / H / D fields
            var wProp = element.FindPropertyRelative("widthOverride");
            var hProp = element.FindPropertyRelative("heightOverride");
            var dProp = element.FindPropertyRelative("depthOverride");

            GUILayout.Label("W", EditorStyles.miniLabel, GUILayout.Width(14));
            wProp.floatValue = EditorGUILayout.FloatField(wProp.floatValue, GUILayout.Width(45));
            GUILayout.Label("H", EditorStyles.miniLabel, GUILayout.Width(14));
            hProp.floatValue = EditorGUILayout.FloatField(hProp.floatValue, GUILayout.Width(45));
            GUILayout.Label("D", EditorStyles.miniLabel, GUILayout.Width(14));
            dProp.floatValue = EditorGUILayout.FloatField(dProp.floatValue, GUILayout.Width(45));

            GUILayout.Space(4);

            // Auto-measure button
            GUI.backgroundColor = new Color(0.4f, 0.7f, 0.9f);
            if (GUILayout.Button(new GUIContent("M", "Auto-measure from mesh bounds"),
                GUILayout.Width(22), GUILayout.Height(16)))
            {
                Undo.RecordObject(target, "Auto-measure bounds");
                if (entry.prefab != null) ApplyMeasuredBounds(entry);
                EditorUtility.SetDirty(target);
            }

            // Clear overrides (set to 0 = use auto at runtime)
            GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
            if (GUILayout.Button(new GUIContent("C", "Clear overrides — use auto-detected bounds at runtime"),
                GUILayout.Width(22), GUILayout.Height(16)))
            {
                Undo.RecordObject(target, "Clear bounds override");
                entry.widthOverride = 0;
                entry.heightOverride = 0;
                entry.depthOverride = 0;
                EditorUtility.SetDirty(target);
            }
            GUI.backgroundColor = Color.white;

            // Show measured vs override comparison
            if (entry.prefab != null)
            {
                Vector3 measured = MeasurePrefabSize(entry.prefab);
                bool differs = (entry.widthOverride > 0 && Mathf.Abs(entry.widthOverride - measured.x) > 0.01f)
                    || (entry.heightOverride > 0 && Mathf.Abs(entry.heightOverride - measured.y) > 0.01f)
                    || (entry.depthOverride > 0 && Mathf.Abs(entry.depthOverride - measured.z) > 0.01f);

                if (differs)
                {
                    GUI.color = new Color(1f, 0.8f, 0.4f);
                    GUILayout.Label($"mesh:{measured.x:F1}×{measured.y:F1}×{measured.z:F1}",
                        EditorStyles.miniLabel, GUILayout.Width(110));
                    GUI.color = Color.white;
                }
            }

            EditorGUILayout.EndHorizontal();
            return true;
        }

        void DrawUtilityButtons(RoomPiecePalette palette)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Re-measure ALL Bounds"))
            {
                Undo.RecordObject(target, "Re-measure all");
                RemeasureAll(palette.floorTiles); RemeasureAll(palette.wallSegments);
                RemeasureAll(palette.doorFrames); RemeasureAll(palette.doorLockedFrames);
                RemeasureAll(palette.doorSecretFrames); RemeasureAll(palette.pillars);
                RemeasureAll(palette.mapPillars); RemeasureAll(palette.ceilingTiles);
                RemeasureAll(palette.baseboards);
                RemeasureAll(palette.torches); RemeasureAll(palette.wallProps);
                RemeasureAll(palette.cornerProps); RemeasureAll(palette.floorProps);
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
                palette.baseboards = RemoveNulls(palette.baseboards);
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
