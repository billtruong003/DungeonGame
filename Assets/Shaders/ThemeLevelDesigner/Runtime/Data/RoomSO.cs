using System.Collections.Generic;
using UnityEngine;

namespace ThemeLevelDesigner
{
    [CreateAssetMenu(fileName = "NewRoom", menuName = "Level Design/Room", order = 1)]
    public class RoomSO : ScriptableObject
    {
        public string roomName = "New Room";
        public RoomType roomType = RoomType.Combat;
        public Color roomColor = Color.white;

        [Range(1, 10)]
        public int difficulty = 1;

        [Header("Layout")]
        public List<RoomSectionData> sections = new();
        public List<ConnectionPoint> connectionPoints = new();

        [Header("Gameplay")]
        public List<SpawnPointData> enemySpawns = new();
        public List<SpawnPointData> lootSpawns = new();

        public Vector2Int GetBounds()
        {
            if (sections.Count == 0) return Vector2Int.one;
            int maxX = 0, maxY = 0;
            foreach (var s in sections)
            {
                var end = s.offset + s.size;
                if (end.x > maxX) maxX = end.x;
                if (end.y > maxY) maxY = end.y;
            }
            return new Vector2Int(maxX, maxY);
        }
    }

    [System.Serializable]
    public class RoomSectionData
    {
        public SectionEntry sectionRef;
        public ThemeSO themeRef;
        public Vector2Int offset;
        public Vector2Int size;
        public int rotationSteps;
    }

    [System.Serializable]
    public class ConnectionPoint
    {
        public Vector2Int position;
        public Direction direction;
        public bool isRequired;
    }

    [System.Serializable]
    public class SpawnPointData
    {
        public Vector2Int gridPos;
        public string spawnTag;
    }
}
