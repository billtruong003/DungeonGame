using System.Collections.Generic;
using UnityEngine;

namespace ThemeLevelDesigner
{
    /// <summary>
    /// Instantiates a MapData or GeneratedDungeon into the scene at runtime.
    /// </summary>
    public class DungeonInstantiator : MonoBehaviour
    {
        [Header("Source")]
        public MapData mapData;
        public DungeonConfigSO dungeonConfig;

        [Header("Settings")]
        public bool instantiateOnStart = true;
        public bool useMapData = true;
        public Transform parentTransform;

        GeneratedDungeon _generatedDungeon;
        readonly List<GameObject> _spawnedObjects = new();

        void Start()
        {
            if (instantiateOnStart)
            {
                if (useMapData && mapData != null)
                    InstantiateMap(mapData);
                else if (dungeonConfig != null)
                    InstantiateGenerated(dungeonConfig);
            }
        }

        public void InstantiateMap(MapData data)
        {
            Clear();
            var parent = parentTransform != null ? parentTransform : transform;

            foreach (var placed in data.placedSections)
            {
                if (placed.entry == null || placed.entry.prefab == null) continue;

                var worldPos = new Vector3(
                    placed.gridPos.x * data.cellSize,
                    0,
                    placed.gridPos.y * data.cellSize
                );

                var go = Instantiate(placed.entry.prefab, worldPos, placed.WorldRotation, parent);
                go.name = $"{placed.entry.displayName}_{placed.instanceId}";
                _spawnedObjects.Add(go);
            }
        }

        public GeneratedDungeon InstantiateGenerated(DungeonConfigSO config)
        {
            Clear();
            _generatedDungeon = DungeonGenerator.Generate(config);
            var parent = parentTransform != null ? parentTransform : transform;

            foreach (var placedRoom in _generatedDungeon.rooms)
            {
                if (placedRoom.room == null) continue;

                foreach (var section in placedRoom.room.sections)
                {
                    if (section.sectionRef == null || section.sectionRef.prefab == null) continue;

                    var worldPos = new Vector3(
                        (placedRoom.worldGridPos.x + section.offset.x) * config.cellSize,
                        0,
                        (placedRoom.worldGridPos.y + section.offset.y) * config.cellSize
                    );
                    var rot = Quaternion.Euler(0, section.rotationSteps * 90f, 0);

                    var go = Instantiate(section.sectionRef.prefab, worldPos, rot, parent);
                    go.name = $"Room{placedRoom.node.index}_{section.sectionRef.displayName}";
                    _spawnedObjects.Add(go);
                }
            }

            return _generatedDungeon;
        }

        public void Clear()
        {
            foreach (var go in _spawnedObjects)
            {
                if (go != null)
                {
                    if (Application.isPlaying)
                        Destroy(go);
                    else
                        DestroyImmediate(go);
                }
            }
            _spawnedObjects.Clear();
        }
    }
}
