using UnityEngine;
using DungeonSystem.Core;

namespace DungeonSystem.Data
{
    [CreateAssetMenu(fileName = "DungeonConfig", menuName = "DungeonSystem/Dungeon Config")]
    public class DungeonConfig : ScriptableObject
    {
        [Header("Database")]
        public RoomDatabase roomDatabase;

        [Header("Grid")]
        [Tooltip("World units per grid cell.")]
        public float cellSize = 16f;

        [Header("Floors")]
        [Range(1, 20)]
        public int totalFloors = 3;
        public float floorYSpacing = 20f;

        [Header("Room Count Per Floor")]
        [Range(3, 100)]
        public int minRoomsPerFloor = 12;
        [Range(3, 100)]
        public int maxRoomsPerFloor = 20;

        [Header("Generation Strategy")]
        public GenerationStrategy strategy = GenerationStrategy.BranchingTree;

        [Tooltip("Main path length as fraction of total room count.")]
        [Range(0.3f, 0.9f)]
        public float mainPathRatio = 0.5f;

        [Tooltip("Probability of creating a shortcut loop between branches.")]
        [Range(0f, 0.5f)]
        public float cycleProbability = 0.15f;

        [Tooltip("Max branch depth (rooms) from main path.")]
        [Range(1, 10)]
        public int maxBranchDepth = 4;

        [Header("Room Type Distribution")]
        [Range(0f, 1f)] public float combatRatio = 0.50f;
        [Range(0f, 1f)] public float lootRatio = 0.20f;
        [Range(0f, 1f)] public float puzzleRatio = 0.10f;
        [Range(0f, 1f)] public float trapRatio = 0.05f;
        [Range(0f, 1f)] public float safeRoomRatio = 0.05f;
        [Range(0f, 1f)] public float shopRatio = 0.05f;
        [Range(0f, 1f)] public float secretRatio = 0.05f;

        [Header("Special Rooms")]
        public bool guaranteeShop = true;
        public bool guaranteeSafeRoom = true;
        [Tooltip("Place a mini-boss roughly halfway through the main path.")]
        public bool placeMiniBoss = true;

        [Header("Difficulty Scaling")]
        [Range(1f, 3f)]
        public float difficultyMultiplierPerFloor = 1.2f;

        [Header("Corridor")]
        [Tooltip("Max corridor length before it looks weird.")]
        [Range(1, 15)]
        public int maxCorridorLength = 8;
        [Tooltip("Chance to make a corridor turn instead of going straight.")]
        [Range(0f, 1f)]
        public float corridorTurnChance = 0.3f;

        [Header("Seed")]
        public bool useFixedSeed = false;
        public int fixedSeed = 12345;

        /// <summary>
        /// Normalize ratios so they sum to 1.
        /// </summary>
        public RoomTypeDistribution GetDistribution(int floorIndex)
        {
            float total = combatRatio + lootRatio + puzzleRatio + trapRatio + safeRoomRatio + shopRatio + secretRatio;
            if (total <= 0f) total = 1f;

            // Scale combat up on deeper floors
            float floorMult = 1f + (floorIndex * 0.1f);

            return new RoomTypeDistribution
            {
                combat = (combatRatio * floorMult) / total,
                loot = lootRatio / total,
                puzzle = puzzleRatio / total,
                trap = (trapRatio * floorMult) / total,
                safeRoom = safeRoomRatio / total,
                shop = shopRatio / total,
                secret = secretRatio / total
            };
        }
    }

    [System.Serializable]
    public struct RoomTypeDistribution
    {
        public float combat, loot, puzzle, trap, safeRoom, shop, secret;

        public RoomType Sample(System.Random rng)
        {
            // Re-normalize
            float total = combat + loot + puzzle + trap + safeRoom + shop + secret;
            float roll = (float)(rng.NextDouble() * total);

            float acc = 0f;
            acc += combat; if (roll < acc) return RoomType.Combat;
            acc += loot;   if (roll < acc) return RoomType.Loot;
            acc += puzzle; if (roll < acc) return RoomType.Puzzle;
            acc += trap;   if (roll < acc) return RoomType.Trap;
            acc += safeRoom; if (roll < acc) return RoomType.SafeRoom;
            acc += shop;   if (roll < acc) return RoomType.Shop;
            acc += secret; if (roll < acc) return RoomType.SecretRoom;
            return RoomType.Combat;
        }
    }
}
