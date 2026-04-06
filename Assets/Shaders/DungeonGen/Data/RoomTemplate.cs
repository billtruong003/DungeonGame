using UnityEngine;
using System.Collections.Generic;
using DungeonSystem.Core;

namespace DungeonSystem.Data
{
    /// <summary>
    /// Defines a single room template. Each template is a prefab + metadata.
    /// The prefab uses a plane for the floor and has DoorSocket children.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomTemplate", menuName = "DungeonSystem/Room Template")]
    public class RoomTemplate : ScriptableObject
    {
        [Header("Identity")]
        public string templateId;
        public string displayName;
        [TextArea(2, 4)]
        public string description;

        [Header("Classification")]
        public RoomType roomType = RoomType.Combat;
        public string[] tags;                       // e.g. "crypt", "sewer", "fire"
        [Range(0f, 1f)]
        public float spawnWeight = 1f;              // Higher = more likely to be picked

        [Header("Dimensions (in grid cells)")]
        [Min(1)] public int widthInCells = 1;
        [Min(1)] public int heightInCells = 1;

        [Header("Prefab")]
        public GameObject prefab;
        public Texture2D preview;

        [Header("Socket Configuration")]
        [Tooltip("Which edges of this room CAN have doors. Auto-detected from prefab if empty.")]
        public List<SocketDefinition> sockets = new List<SocketDefinition>();

        [Header("Difficulty")]
        [Range(0, 10)]
        public int difficultyTier = 1;
        public int minFloorAppearance = 0;          // Don't appear before floor N
        public int maxFloorAppearance = 99;         // Don't appear after floor N

        /// <summary>
        /// Can this template appear on the given floor?
        /// </summary>
        public bool IsValidForFloor(int floorIndex)
        {
            return floorIndex >= minFloorAppearance && floorIndex <= maxFloorAppearance;
        }

        /// <summary>
        /// Returns the grid footprint size, optionally rotated by 90° steps.
        /// </summary>
        public Vector2Int GetRotatedSize(int rotationSteps)
        {
            bool swapped = (rotationSteps % 2) != 0;
            return swapped ? new Vector2Int(heightInCells, widthInCells) : new Vector2Int(widthInCells, heightInCells);
        }
    }

    [System.Serializable]
    public class SocketDefinition
    {
        public Direction direction;
        public Vector2Int cellOffset;               // Which cell of the room this socket belongs to
        [Tooltip("If false, this edge is always a wall (no door possible).")]
        public bool canConnect = true;
    }
}
