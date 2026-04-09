using UnityEngine;
using System.Collections.Generic;
using DungeonSystem.Core;

namespace DungeonSystem.Data
{
    [CreateAssetMenu(fileName = "DungeonConfig", menuName = "DungeonSystem/Dungeon Config")]
    public class DungeonConfig : ScriptableObject
    {
        [Header("Database")]
        public RoomDatabase roomDatabase;
        public RoomPiecePalette piecePalette;

        [Header("Room Recipes (decoration per room type)")]
        [Tooltip("Data-driven decoration recipes. If a recipe exists for a room type, " +
                 "PropPlacer uses it instead of the legacy hardcoded decoration.")]
        public List<RoomRecipe> roomRecipes = new List<RoomRecipe>();

        [Header("Grid")]
        public float cellSize = 16f;

        [Header("Room Geometry")]
        [Min(1)] public int roomHeightMultiplier = 1;
        public float wallHeightOffset = 0f;
        public float pieceOverlap = 0.05f;

        [Header("Torches")]
        [Range(0f, 1f)] public float torchProbability = 0.25f;
        [Range(0f, 1f)] public float torchHeightRatio = 0.75f;
        public float torchScale = 1.5f;
        public float torchPitch = 45f;
        public float torchZOffset = -0.1f;

        [Header("Baseboards (floor-wall trim)")]
        [Tooltip("Enable baseboard placement along the base of walls.")]
        public bool enableBaseboards = true;
        [Tooltip("Enable horizontal trim between wall tiers (roomHeightMultiplier >= 2). " +
                 "Uses baseboard pieces if available, otherwise rotates pillar pieces.")]
        public bool enableHorizontalBeams = true;
        [Tooltip("Y offset from floor level. 0 = sitting on the floor.")]
        public float baseboardYOffset = 0f;
        [Tooltip("How far inward from the wall surface the baseboard sits. " +
                 "Positive = away from wall, toward room center.")]
        public float baseboardInwardOffset = 0f;

        [Header("Props & Decor")]
        [Range(0f, 1f)] public float cornerPropProbability = 0.5f;
        [Range(0f, 1f)] public float wallPropProbability = 0.3f;
        [Range(0f, 1f)] public float floorPropProbability = 0.4f;
        [Range(0f, 1f)] public float ceilingPropProbability = 0.2f;

        [Header("Floors")]
        [Range(1, 20)] public int totalFloors = 3;
        public float floorYSpacing = 20f;
        [Header("Room Count Per Floor")]
        [Range(3, 100)] public int minRoomsPerFloor = 12;
        [Range(3, 100)] public int maxRoomsPerFloor = 20;

        [Header("Generation Strategy")]
        public GenerationStrategy strategy = GenerationStrategy.BranchingTree;
        [Range(0.3f, 0.9f)] public float mainPathRatio = 0.5f;
        [Range(0f, 0.5f)] public float cycleProbability = 0.15f;
        [Range(1, 10)] public int maxBranchDepth = 4;

        [Header("Room Type Distribution")][Range(0f, 1f)] public float combatRatio = 0.50f; [Range(0f, 1f)] public float lootRatio = 0.20f; [Range(0f, 1f)] public float puzzleRatio = 0.10f; [Range(0f, 1f)] public float trapRatio = 0.05f; [Range(0f, 1f)] public float safeRoomRatio = 0.05f; [Range(0f, 1f)] public float shopRatio = 0.05f; [Range(0f, 1f)] public float secretRatio = 0.05f;

        [Header("Special Rooms")]
        public bool guaranteeShop = true;
        public bool guaranteeSafeRoom = true;
        public bool placeMiniBoss = true;

        [Header("Difficulty Scaling")]
        [Range(1f, 3f)] public float difficultyMultiplierPerFloor = 1.2f;

        [Header("Corridor")][Range(1, 15)] public int maxCorridorLength = 8; [Range(0f, 1f)] public float corridorTurnChance = 0.3f; [Header("Seed")]
        public bool useFixedSeed = false;
        public int fixedSeed = 12345;

        // Runtime cache for recipe lookup
        private Dictionary<RoomType, RoomRecipe> _recipeCache;

        /// <summary>
        /// Get the decoration recipe for a room type, or null if none defined.
        /// </summary>
        public RoomRecipe GetRecipe(RoomType type)
        {
            if (_recipeCache == null)
            {
                _recipeCache = new Dictionary<RoomType, RoomRecipe>();
                if (roomRecipes != null)
                    foreach (var r in roomRecipes)
                        if (r != null && !_recipeCache.ContainsKey(r.roomType))
                            _recipeCache[r.roomType] = r;
            }
            return _recipeCache.TryGetValue(type, out var recipe) ? recipe : null;
        }

        private void OnValidate() => _recipeCache = null;

        public RoomTypeDistribution GetDistribution(int floorIndex)
        {
            float total = combatRatio + lootRatio + puzzleRatio + trapRatio + safeRoomRatio + shopRatio + secretRatio;
            if (total <= 0f) total = 1f;
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
            float total = combat + loot + puzzle + trap + safeRoom + shop + secret;
            float roll = (float)(rng.NextDouble() * total);

            float acc = 0f;
            acc += combat; if (roll < acc) return RoomType.Combat;
            acc += loot; if (roll < acc) return RoomType.Loot;
            acc += puzzle; if (roll < acc) return RoomType.Puzzle;
            acc += trap; if (roll < acc) return RoomType.Trap;
            acc += safeRoom; if (roll < acc) return RoomType.SafeRoom;
            acc += shop; if (roll < acc) return RoomType.Shop;
            acc += secret; if (roll < acc) return RoomType.SecretRoom;
            return RoomType.Combat;
        }
    }
}
