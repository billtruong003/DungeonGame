using UnityEngine;

namespace ThemeLevelDesigner
{
    [System.Serializable]
    public class SectionEntry
    {
        public string id;
        public string displayName;
        public GameObject prefab;
        public Texture2D preview;
        public Vector2Int gridSize = new Vector2Int(1, 1);
        public string[] tags = { "floor" };
        public bool canRotate = true;
        public SnapPoint[] snapPoints;
    }

    [System.Serializable]
    public class SnapPoint
    {
        public Vector2Int localGridPos;
        public Direction direction;
    }

    public enum Direction
    {
        North,
        South,
        East,
        West
    }
}
