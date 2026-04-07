using UnityEngine;
using System.Collections.Generic;
using DungeonSystem.Core;

namespace DungeonSystem.Data
{
    /// <summary>
    /// Data-driven recipe that defines how a room type should be decorated.
    /// Replaces hardcoded GetSpawnLayout logic in PieceAssembler.
    /// 
    /// Design philosophy:
    ///   - Major items are placed first and MUST appear (bed in bedroom).
    ///   - Minor items fill remaining space with probability.
    ///   - Clutter items go on furniture via child anchors.
    ///   - Spawn points for gameplay entities (enemies, chests) are separate.
    /// </summary>
    [CreateAssetMenu(fileName = "RoomRecipe", menuName = "DungeonSystem/Room Recipe")]
    public class RoomRecipe : ScriptableObject
    {
        [Header("Identity")]
        public RoomType roomType;
        public string displayName;

        [Header("Prop Entries (placed in order: Major → Minor → Clutter)")]
        public List<RecipePropEntry> props = new List<RecipePropEntry>();

        [Header("Spawn Points")]
        public List<RecipeSpawnEntry> spawnPoints = new List<RecipeSpawnEntry>();

        [Header("Density")]
        [Tooltip("Overall density multiplier. 0.5 = half the props, 2.0 = double.")]
        [Range(0.1f, 3f)]
        public float densityMultiplier = 1f;

        [Tooltip("Max fill ratio of floor area before stopping placement (0-1).")]
        [Range(0.1f, 0.9f)]
        public float maxFillRatio = 0.6f;
    }

    [System.Serializable]
    public class RecipePropEntry
    {
        [Tooltip("Tag query: props with ANY of these tags can fill this slot. " +
                 "Matched against PropPlacementProfile.tags.")]
        public string[] requiredTags;

        [Tooltip("Importance tier determines placement order.")]
        public PropImportance importance = PropImportance.Minor;

        [Tooltip("Must at least this many be placed (0 = optional).")]
        public int minCount = 0;

        [Tooltip("Max instances of this entry (0 = unlimited by available space).")]
        public int maxCount = 3;

        [Tooltip("Probability of attempting to place each instance.")]
        [Range(0f, 1f)]
        public float chance = 0.5f;

        [Header("Placement Hints")]
        [Tooltip("Prefer center of room.")]
        public bool preferCenter = false;

        [Tooltip("Prefer against walls.")]
        public bool preferWalls = false;

        [Tooltip("Prefer corners.")]
        public bool preferCorners = false;
    }

    [System.Serializable]
    public class RecipeSpawnEntry
    {
        public Runtime.SpawnPointType pointType;
        public int count = 1;

        [Tooltip("Where in the room: Center, Corners, Edges, Random.")]
        public SpawnPlacement placement = SpawnPlacement.Random;

        [Tooltip("Priority for this spawn point (higher = more important).")]
        [Range(0, 10)]
        public int priority = 5;
    }

    public enum SpawnPlacement
    {
        Center,
        Corners,
        Edges,
        Random,
        NearEntrance,
        FarFromEntrance
    }
}