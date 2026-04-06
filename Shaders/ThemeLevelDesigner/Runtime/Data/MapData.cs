using System.Collections.Generic;
using UnityEngine;

namespace ThemeLevelDesigner
{
    [System.Serializable]
    public class PlacedSection
    {
        public string instanceId;
        public SectionEntry entry;
        public ThemeSO sourceTheme;
        public Vector2Int gridPos;
        public int rotationSteps; // 0,1,2,3 = 0°,90°,180°,270°
        public string roomGroupId;

        public Quaternion WorldRotation => Quaternion.Euler(0, rotationSteps * 90f, 0);

        public Vector2Int RotatedSize
        {
            get
            {
                if (rotationSteps % 2 == 0) return entry.gridSize;
                return new Vector2Int(entry.gridSize.y, entry.gridSize.x);
            }
        }
    }

    [CreateAssetMenu(fileName = "NewMap", menuName = "Level Design/Map Data", order = 2)]
    public class MapData : ScriptableObject
    {
        public float cellSize = 1f;
        public List<PlacedSection> placedSections = new();
        public List<RoomGroup> roomGroups = new();

        public bool CanPlace(Vector2Int pos, Vector2Int size, string excludeId = null)
        {
            foreach (var placed in placedSections)
            {
                if (excludeId != null && placed.instanceId == excludeId) continue;
                if (Overlaps(pos, size, placed.gridPos, placed.RotatedSize))
                    return false;
            }
            return true;
        }

        public PlacedSection GetAt(Vector2Int pos)
        {
            foreach (var placed in placedSections)
            {
                var s = placed.RotatedSize;
                if (pos.x >= placed.gridPos.x && pos.x < placed.gridPos.x + s.x &&
                    pos.y >= placed.gridPos.y && pos.y < placed.gridPos.y + s.y)
                    return placed;
            }
            return null;
        }

        public void Add(PlacedSection section)
        {
            if (string.IsNullOrEmpty(section.instanceId))
                section.instanceId = System.Guid.NewGuid().ToString("N")[..8];
            placedSections.Add(section);
        }

        public void Remove(PlacedSection section)
        {
            placedSections.Remove(section);
        }

        static bool Overlaps(Vector2Int posA, Vector2Int sizeA, Vector2Int posB, Vector2Int sizeB)
        {
            return posA.x < posB.x + sizeB.x && posA.x + sizeA.x > posB.x &&
                   posA.y < posB.y + sizeB.y && posA.y + sizeA.y > posB.y;
        }
    }

    [System.Serializable]
    public class RoomGroup
    {
        public string groupId;
        public string roomName = "Room";
        public Color roomColor = Color.white;
        public RoomType roomType = RoomType.Combat;
    }

    public enum RoomType
    {
        Start,
        Combat,
        Treasure,
        Shop,
        Boss,
        Corridor,
        Secret
    }
}
