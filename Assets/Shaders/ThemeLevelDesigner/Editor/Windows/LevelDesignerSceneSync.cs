using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace ThemeLevelDesigner.Editor
{
    /// <summary>
    /// Manages syncing the 2D map canvas to actual 3D prefabs in the Scene.
    /// Creates a "LevelPreview" root GameObject and instantiates all placed sections under it.
    /// Supports live sync (auto-update on changes) and manual sync.
    /// </summary>
    [InitializeOnLoad]
    public static class LevelDesignerSceneSync
    {
        const string RootName = "[LevelDesigner_Preview]";

        static MapData _syncedMap;
        static bool _autoSync;
        static bool _showGizmos = true;

        // Track what's currently in scene to avoid redundant rebuilds
        static readonly Dictionary<string, GameObject> _spawnedObjects = new();
        static int _lastSyncHash;

        static LevelDesignerSceneSync()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        // ==================== PUBLIC API ====================

        public static bool AutoSync
        {
            get => _autoSync;
            set => _autoSync = value;
        }

        public static bool ShowGizmos
        {
            get => _showGizmos;
            set
            {
                _showGizmos = value;
                SceneView.RepaintAll();
            }
        }

        /// <summary>
        /// Full sync: destroy everything and re-instantiate from map data.
        /// </summary>
        public static void SyncToScene(MapData map)
        {
            if (map == null) return;
            _syncedMap = map;

            ClearScene();

            var root = GetOrCreateRoot();
            Undo.RegisterCreatedObjectUndo(root, "Sync Level to Scene");

            int count = 0;
            foreach (var placed in map.placedSections)
            {
                if (placed.entry == null || placed.entry.prefab == null) continue;

                var worldPos = new Vector3(
                    placed.gridPos.x * map.cellSize,
                    0,
                    placed.gridPos.y * map.cellSize
                );

                var go = (GameObject)PrefabUtility.InstantiatePrefab(placed.entry.prefab, root.transform);
                go.transform.position = worldPos;
                go.transform.rotation = placed.WorldRotation;
                go.name = $"{placed.entry.displayName ?? placed.entry.id}_{placed.instanceId}";

                // Tag with instance id for tracking
                var tag = go.AddComponent<LevelPreviewTag>();
                tag.instanceId = placed.instanceId;
                tag.sectionId = placed.entry.id;

                _spawnedObjects[placed.instanceId] = go;
                count++;
            }

            _lastSyncHash = ComputeMapHash(map);

            // Focus scene view on the level
            if (count > 0)
            {
                var bounds = ComputeSceneBounds(map);
                foreach (var sv in SceneView.sceneViews)
                {
                    var sceneView = sv as SceneView;
                    if (sceneView != null)
                    {
                        sceneView.Frame(bounds, false);
                        break;
                    }
                }
            }

            Debug.Log($"[LevelDesigner] Synced {count} sections to Scene.");
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Incremental sync: only update what changed.
        /// Much faster than full rebuild for small edits.
        /// </summary>
        public static void IncrementalSync(MapData map)
        {
            if (map == null) return;
            _syncedMap = map;

            int newHash = ComputeMapHash(map);
            if (newHash == _lastSyncHash) return; // nothing changed

            var root = GetOrCreateRoot();

            // Find which sections are new, removed, or moved
            var currentIds = new HashSet<string>(map.placedSections
                .Where(p => p.entry?.prefab != null)
                .Select(p => p.instanceId));

            // Remove objects no longer in map
            var toRemove = _spawnedObjects.Keys.Where(id => !currentIds.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                if (_spawnedObjects.TryGetValue(id, out var go) && go != null)
                    Undo.DestroyObjectImmediate(go);
                _spawnedObjects.Remove(id);
            }

            // Add or update existing
            foreach (var placed in map.placedSections)
            {
                if (placed.entry == null || placed.entry.prefab == null) continue;

                var worldPos = new Vector3(
                    placed.gridPos.x * map.cellSize,
                    0,
                    placed.gridPos.y * map.cellSize
                );

                if (_spawnedObjects.TryGetValue(placed.instanceId, out var existing) && existing != null)
                {
                    // Update position/rotation if changed
                    if (existing.transform.position != worldPos || existing.transform.rotation != placed.WorldRotation)
                    {
                        Undo.RecordObject(existing.transform, "Move Section");
                        existing.transform.position = worldPos;
                        existing.transform.rotation = placed.WorldRotation;
                    }

                    // Check if prefab changed (replace happened)
                    var tag = existing.GetComponent<LevelPreviewTag>();
                    if (tag != null && tag.sectionId != placed.entry.id)
                    {
                        // Prefab changed — destroy old, create new
                        Undo.DestroyObjectImmediate(existing);
                        _spawnedObjects.Remove(placed.instanceId);
                        SpawnSection(root, placed, map.cellSize);
                    }
                }
                else
                {
                    // New section
                    SpawnSection(root, placed, map.cellSize);
                }
            }

            _lastSyncHash = newHash;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Clear all preview objects from the scene.
        /// </summary>
        public static void ClearScene()
        {
            var root = GameObject.Find(RootName);
            if (root != null)
                Undo.DestroyObjectImmediate(root);

            _spawnedObjects.Clear();
            _lastSyncHash = 0;
            _syncedMap = null;
            SceneView.RepaintAll();
        }

        /// <summary>
        /// Check if there's currently a preview in the scene.
        /// </summary>
        public static bool HasPreview => GameObject.Find(RootName) != null;

        // ==================== DUNGEON PREVIEW ====================

        static GeneratedDungeon _previewDungeon;

        public static void PreviewDungeon(GeneratedDungeon dungeon)
        {
            _previewDungeon = dungeon;
            _syncedMap = null;
            ClearScene();

            if (dungeon == null) return;

            var root = GetOrCreateRoot();
            int count = 0;

            foreach (var placedRoom in dungeon.rooms)
            {
                if (placedRoom.room == null) continue;

                foreach (var section in placedRoom.room.sections)
                {
                    if (section.sectionRef == null || section.sectionRef.prefab == null) continue;

                    var worldPos = new Vector3(
                        (placedRoom.worldGridPos.x + section.offset.x) * dungeon.config.cellSize,
                        0,
                        (placedRoom.worldGridPos.y + section.offset.y) * dungeon.config.cellSize
                    );
                    var rot = Quaternion.Euler(0, section.rotationSteps * 90f, 0);

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(section.sectionRef.prefab, root.transform);
                    go.transform.position = worldPos;
                    go.transform.rotation = rot;
                    go.name = $"Room{placedRoom.node.index}_{section.sectionRef.displayName}";
                    count++;
                }
            }

            Debug.Log($"[LevelDesigner] Dungeon preview: {count} sections from {dungeon.rooms.Count} rooms.");

            if (count > 0)
            {
                var bounds = new Bounds(root.transform.position, Vector3.zero);
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);

                foreach (var sv in SceneView.sceneViews)
                {
                    var sceneView = sv as SceneView;
                    if (sceneView != null)
                    {
                        sceneView.Frame(bounds, false);
                        break;
                    }
                }
            }

            SceneView.RepaintAll();
        }

        // ==================== SCENE GUI (gizmos) ====================

        static void OnSceneGUI(SceneView sv)
        {
            if (!_showGizmos) return;

            if (_syncedMap != null)
                DrawMapGizmos(_syncedMap);
            else if (_previewDungeon != null)
                DrawDungeonGizmos(_previewDungeon);
        }

        static void DrawMapGizmos(MapData map)
        {
            float cell = map.cellSize;

            foreach (var placed in map.placedSections)
            {
                if (placed.entry == null) continue;

                var pos = new Vector3(placed.gridPos.x * cell, 0, placed.gridPos.y * cell);
                var size = placed.RotatedSize;
                var center = pos + new Vector3(size.x * cell * 0.5f, 0.01f, size.y * cell * 0.5f);

                // Room group color overlay on ground
                Color color = Color.cyan;
                if (!string.IsNullOrEmpty(placed.roomGroupId))
                {
                    var group = map.roomGroups.Find(g => g.groupId == placed.roomGroupId);
                    if (group != null) color = group.roomColor;
                }
                else if (placed.sourceTheme != null)
                {
                    color = placed.sourceTheme.themeColor;
                }

                // Draw ground rect outline
                color.a = 0.5f;
                Handles.color = color;
                var p1 = pos;
                var p2 = pos + new Vector3(size.x * cell, 0, 0);
                var p3 = pos + new Vector3(size.x * cell, 0, size.y * cell);
                var p4 = pos + new Vector3(0, 0, size.y * cell);
                Handles.DrawAAPolyLine(3f, p1, p2, p3, p4, p1);

                // Label
                var style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 9,
                    normal = { textColor = color }
                };
                Handles.Label(center + Vector3.up * 0.1f, placed.entry.displayName ?? "?", style);
            }
        }

        static void DrawDungeonGizmos(GeneratedDungeon dungeon)
        {
            float cell = dungeon.config.cellSize;

            foreach (var room in dungeon.rooms)
            {
                var pos = new Vector3(room.worldGridPos.x * cell, 0, room.worldGridPos.y * cell);
                var center = pos + new Vector3(room.bounds.x * cell * 0.5f, 2f, room.bounds.y * cell * 0.5f);

                Color color = room.node.isCriticalPath ? Color.yellow : Color.cyan;
                if (room.node.requiredType == RoomType.Start) color = Color.green;
                if (room.node.requiredType == RoomType.Boss) color = Color.red;

                Handles.color = color;
                string label = room.room != null ? room.room.roomName : $"Room {room.node.index}";
                Handles.Label(center, $"{label}\nDiff: {room.difficulty:F1}");

                // Connections
                foreach (int conn in room.node.connections)
                {
                    if (conn <= room.node.index) continue;
                    var other = dungeon.rooms.Find(r => r.node.index == conn);
                    if (other == null) continue;

                    var otherPos = new Vector3(other.worldGridPos.x * cell, 0, other.worldGridPos.y * cell);
                    var otherCenter = otherPos + new Vector3(other.bounds.x * cell * 0.5f, 2f, other.bounds.y * cell * 0.5f);

                    Handles.color = new Color(1, 1, 1, 0.3f);
                    Handles.DrawDottedLine(center, otherCenter, 4f);
                }
            }
        }

        // ==================== HELPERS ====================

        static GameObject GetOrCreateRoot()
        {
            var root = GameObject.Find(RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                root.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(root, "Create Level Preview Root");
            }
            return root;
        }

        static void SpawnSection(GameObject root, PlacedSection placed, float cellSize)
        {
            var worldPos = new Vector3(
                placed.gridPos.x * cellSize,
                0,
                placed.gridPos.y * cellSize
            );

            var go = (GameObject)PrefabUtility.InstantiatePrefab(placed.entry.prefab, root.transform);
            go.transform.position = worldPos;
            go.transform.rotation = placed.WorldRotation;
            go.name = $"{placed.entry.displayName ?? placed.entry.id}_{placed.instanceId}";

            var tag = go.AddComponent<LevelPreviewTag>();
            tag.instanceId = placed.instanceId;
            tag.sectionId = placed.entry.id;

            _spawnedObjects[placed.instanceId] = go;
        }

        static int ComputeMapHash(MapData map)
        {
            int hash = map.placedSections.Count;
            foreach (var p in map.placedSections)
            {
                hash = hash * 31 + (p.instanceId?.GetHashCode() ?? 0);
                hash = hash * 31 + p.gridPos.GetHashCode();
                hash = hash * 31 + p.rotationSteps;
                hash = hash * 31 + (p.entry?.id?.GetHashCode() ?? 0);
            }
            return hash;
        }

        static Bounds ComputeSceneBounds(MapData map)
        {
            if (map.placedSections.Count == 0)
                return new Bounds(Vector3.zero, Vector3.one * 10);

            var first = map.placedSections[0];
            var min = new Vector3(first.gridPos.x * map.cellSize, 0, first.gridPos.y * map.cellSize);
            var max = min;

            foreach (var p in map.placedSections)
            {
                var pos = new Vector3(p.gridPos.x * map.cellSize, 0, p.gridPos.y * map.cellSize);
                var size = p.RotatedSize;
                var end = pos + new Vector3(size.x * map.cellSize, 2f, size.y * map.cellSize);

                min = Vector3.Min(min, pos);
                max = Vector3.Max(max, end);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    /// <summary>
    /// Tag component attached to preview objects to track which map section they represent.
    /// </summary>
    public class LevelPreviewTag : MonoBehaviour
    {
        [HideInInspector] public string instanceId;
        [HideInInspector] public string sectionId;
    }
}
