#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using DungeonSystem.Core;
using DungeonSystem.Data;
using DungeonSystem.Runtime;

namespace DungeonSystem.Editor
{
    /// <summary>
    /// One-click generation of RoomRecipe assets for every room type.
    /// Converts the hardcoded spawn layouts and decoration logic from PieceAssembler
    /// into data-driven RoomRecipe ScriptableObjects.
    /// 
    /// Accessible from:
    ///   - Menu: DungeonSystem > Generate Default Recipes
    ///   - RecipeBuilderWindow toolbar
    ///   - DungeonConfig inspector
    /// </summary>
    public static class RecipeAutoGenerator
    {
        private const string DEFAULT_SAVE_PATH = "Assets/Data/DungeonRecipes";

        /// <summary>
        /// Generate default recipes for all room types. Returns the created recipes.
        /// Skips types that already have a recipe at the save path.
        /// </summary>
        public static List<RoomRecipe> GenerateAllDefaults(
            string savePath = DEFAULT_SAVE_PATH,
            bool overwriteExisting = false)
        {
            EnsureFolder(savePath);
            var created = new List<RoomRecipe>();

            foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
            {
                if (type == RoomType.Corridor || type == RoomType.Junction)
                    continue; // corridors don't use recipes

                string assetPath = $"{savePath}/Recipe_{type}.asset";

                if (!overwriteExisting)
                {
                    var existing = AssetDatabase.LoadAssetAtPath<RoomRecipe>(assetPath);
                    if (existing != null)
                    {
                        created.Add(existing);
                        continue;
                    }
                }

                var recipe = BuildRecipe(type);
                AssetDatabase.CreateAsset(recipe, assetPath);
                created.Add(recipe);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return created;
        }

        /// <summary>
        /// Generate a single recipe for the given room type.
        /// </summary>
        public static RoomRecipe GenerateSingle(RoomType type, string savePath = DEFAULT_SAVE_PATH)
        {
            EnsureFolder(savePath);
            string assetPath = $"{savePath}/Recipe_{type}.asset";

            var recipe = BuildRecipe(type);
            AssetDatabase.CreateAsset(recipe, assetPath);
            AssetDatabase.SaveAssets();
            return recipe;
        }

        /// <summary>
        /// Generate and auto-assign all recipes to a DungeonConfig.
        /// </summary>
        public static int GenerateAndAssign(DungeonConfig config, string savePath = DEFAULT_SAVE_PATH)
        {
            var recipes = GenerateAllDefaults(savePath);

            Undo.RecordObject(config, "Auto-assign recipes");
            if (config.roomRecipes == null)
                config.roomRecipes = new List<RoomRecipe>();

            int added = 0;
            foreach (var recipe in recipes)
            {
                bool alreadyAssigned = false;
                foreach (var existing in config.roomRecipes)
                    if (existing != null && existing.roomType == recipe.roomType)
                    { alreadyAssigned = true; break; }

                if (!alreadyAssigned)
                {
                    config.roomRecipes.Add(recipe);
                    added++;
                }
            }

            EditorUtility.SetDirty(config);
            return added;
        }

        // ════════════════════════════════════════════════════════════════
        //  RECIPE BLUEPRINTS — one method per room type
        // ════════════════════════════════════════════════════════════════

        static RoomRecipe BuildRecipe(RoomType type)
        {
            var recipe = ScriptableObject.CreateInstance<RoomRecipe>();
            recipe.roomType = type;
            recipe.displayName = $"{type} (Auto)";
            recipe.props = new List<RecipePropEntry>();
            recipe.spawnPoints = new List<RecipeSpawnEntry>();

            switch (type)
            {
                case RoomType.Start:      BuildStartRecipe(recipe);      break;
                case RoomType.Combat:     BuildCombatRecipe(recipe);     break;
                case RoomType.Loot:       BuildLootRecipe(recipe);       break;
                case RoomType.Boss:       BuildBossRecipe(recipe);       break;
                case RoomType.MiniBoss:   BuildMiniBossRecipe(recipe);   break;
                case RoomType.Shop:       BuildShopRecipe(recipe);       break;
                case RoomType.SafeRoom:   BuildSafeRoomRecipe(recipe);   break;
                case RoomType.Puzzle:     BuildPuzzleRecipe(recipe);     break;
                case RoomType.Trap:       BuildTrapRecipe(recipe);       break;
                case RoomType.SecretRoom: BuildSecretRecipe(recipe);     break;
                case RoomType.StaircaseUp:
                case RoomType.StaircaseDown:
                    BuildStaircaseRecipe(recipe); break;
                default:
                    BuildGenericRecipe(recipe); break;
            }

            return recipe;
        }

        static void BuildStartRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.6f;
            r.maxFillRatio = 0.3f;

            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Major,
                min: 2, max: 4, chance: 0.9f, preferWalls: true);
            AddProp(r, new[] { PropTags.FloorDecor }, PropImportance.Minor,
                min: 0, max: 1, chance: 0.4f, preferCenter: true);

            AddSpawn(r, SpawnPointType.PlayerSpawn, 1, SpawnPlacement.Center, priority: 10);
            AddSpawn(r, SpawnPointType.Light, 1, SpawnPlacement.FarFromEntrance);
        }

        static void BuildCombatRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.8f;
            r.maxFillRatio = 0.4f;

            AddProp(r, new[] { PropTags.Storage }, PropImportance.Minor,
                min: 0, max: 3, chance: 0.4f, preferWalls: true);
            AddProp(r, new[] { PropTags.Pillar }, PropImportance.Minor,
                min: 0, max: 2, chance: 0.3f);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 1, max: 4, chance: 0.7f, preferWalls: true);
            AddProp(r, new[] { PropTags.Bones }, PropImportance.Clutter,
                min: 0, max: 2, chance: 0.3f);

            AddSpawn(r, SpawnPointType.Enemy, 3, SpawnPlacement.Corners, priority: 8);
            AddSpawn(r, SpawnPointType.Item, 1, SpawnPlacement.Center, priority: 3);
        }

        static void BuildLootRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 1.2f;
            r.maxFillRatio = 0.6f;

            AddProp(r, new[] { PropTags.Storage }, PropImportance.Major,
                min: 2, max: 5, chance: 0.8f, preferWalls: true);
            AddProp(r, new[] { PropTags.Shelf }, PropImportance.Major,
                min: 1, max: 3, chance: 0.6f, preferWalls: true);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 2, max: 4, chance: 0.8f, preferWalls: true);
            AddProp(r, new[] { PropTags.Potion, PropTags.Book }, PropImportance.Clutter,
                min: 0, max: 4, chance: 0.5f);

            AddSpawn(r, SpawnPointType.Chest, 1, SpawnPlacement.Center, priority: 10);
            AddSpawn(r, SpawnPointType.Item, 2, SpawnPlacement.Edges, priority: 5);
        }

        static void BuildBossRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.5f;
            r.maxFillRatio = 0.25f;

            AddProp(r, new[] { PropTags.Pillar, PropTags.Statue }, PropImportance.Major,
                min: 4, max: 4, chance: 1f, preferCorners: true);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Major,
                min: 4, max: 6, chance: 1f, preferWalls: true);
            AddProp(r, new[] { PropTags.FloorDecor }, PropImportance.Minor,
                min: 0, max: 1, chance: 0.5f, preferCenter: true);
            AddProp(r, new[] { PropTags.Bones }, PropImportance.Clutter,
                min: 0, max: 3, chance: 0.3f);

            AddSpawn(r, SpawnPointType.BossSpawn, 1, SpawnPlacement.Center, priority: 10);
            AddSpawn(r, SpawnPointType.Light, 4, SpawnPlacement.Corners, priority: 5);
        }

        static void BuildMiniBossRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.6f;
            r.maxFillRatio = 0.3f;

            AddProp(r, new[] { PropTags.Pillar }, PropImportance.Major,
                min: 2, max: 4, chance: 0.8f, preferCorners: true);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 2, max: 4, chance: 0.8f, preferWalls: true);

            AddSpawn(r, SpawnPointType.BossSpawn, 1, SpawnPlacement.Center, priority: 10);
            AddSpawn(r, SpawnPointType.Enemy, 2, SpawnPlacement.Edges, priority: 6);
        }

        static void BuildShopRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 1.3f;
            r.maxFillRatio = 0.6f;

            AddProp(r, new[] { PropTags.Table }, PropImportance.Major,
                min: 1, max: 2, chance: 1f, preferWalls: true);
            AddProp(r, new[] { PropTags.Shelf }, PropImportance.Major,
                min: 2, max: 4, chance: 0.8f, preferWalls: true);
            AddProp(r, new[] { PropTags.Goods }, PropImportance.Minor,
                min: 2, max: 5, chance: 0.7f);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 2, max: 3, chance: 0.9f, preferWalls: true);
            AddProp(r, new[] { PropTags.Sign }, PropImportance.Minor,
                min: 0, max: 1, chance: 0.4f, preferWalls: true);

            AddSpawn(r, SpawnPointType.NPC, 1, SpawnPlacement.FarFromEntrance, priority: 10);
            AddSpawn(r, SpawnPointType.Item, 3, SpawnPlacement.Edges, priority: 5);
        }

        static void BuildSafeRoomRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 1.0f;
            r.maxFillRatio = 0.5f;

            AddProp(r, new[] { PropTags.Campfire }, PropImportance.Major,
                min: 1, max: 1, chance: 1f, preferCenter: true);
            AddProp(r, new[] { PropTags.Seating }, PropImportance.Minor,
                min: 1, max: 3, chance: 0.6f);
            AddProp(r, new[] { PropTags.Bed }, PropImportance.Minor,
                min: 0, max: 1, chance: 0.4f, preferCorners: true);
            AddProp(r, new[] { PropTags.Storage }, PropImportance.Minor,
                min: 0, max: 2, chance: 0.4f, preferWalls: true);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 1, max: 2, chance: 0.8f, preferWalls: true);

            AddSpawn(r, SpawnPointType.PlayerSpawn, 1, SpawnPlacement.Center, priority: 8);
            AddSpawn(r, SpawnPointType.Light, 2, SpawnPlacement.Edges, priority: 5);
        }

        static void BuildPuzzleRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.7f;
            r.maxFillRatio = 0.35f;

            AddProp(r, new[] { PropTags.Mechanism }, PropImportance.Major,
                min: 1, max: 2, chance: 0.8f, preferWalls: true);
            AddProp(r, new[] { PropTags.Pillar, PropTags.Statue }, PropImportance.Minor,
                min: 0, max: 2, chance: 0.5f);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 2, max: 4, chance: 0.9f, preferWalls: true);
            AddProp(r, new[] { PropTags.FloorDecor }, PropImportance.Minor,
                min: 0, max: 2, chance: 0.4f);

            AddSpawn(r, SpawnPointType.PuzzleObject, 1, SpawnPlacement.Center, priority: 10);
            AddSpawn(r, SpawnPointType.Item, 1, SpawnPlacement.FarFromEntrance, priority: 4);
            AddSpawn(r, SpawnPointType.Light, 1, SpawnPlacement.NearEntrance, priority: 3);
        }

        static void BuildTrapRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.8f;
            r.maxFillRatio = 0.4f;

            AddProp(r, new[] { PropTags.Trap }, PropImportance.Major,
                min: 2, max: 4, chance: 0.9f);
            AddProp(r, new[] { PropTags.Bones }, PropImportance.Minor,
                min: 0, max: 2, chance: 0.4f);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 1, max: 2, chance: 0.5f, preferWalls: true);

            AddSpawn(r, SpawnPointType.Trap, 3, SpawnPlacement.Edges, priority: 9);
            AddSpawn(r, SpawnPointType.Item, 1, SpawnPlacement.FarFromEntrance, priority: 5);
        }

        static void BuildSecretRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 1.0f;
            r.maxFillRatio = 0.5f;

            AddProp(r, new[] { PropTags.Storage }, PropImportance.Major,
                min: 1, max: 2, chance: 0.9f, preferWalls: true);
            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 1, max: 2, chance: 0.7f, preferWalls: true);
            AddProp(r, new[] { PropTags.Potion, PropTags.Book }, PropImportance.Clutter,
                min: 0, max: 3, chance: 0.5f);

            AddSpawn(r, SpawnPointType.Chest, 1, SpawnPlacement.Center, priority: 10);
            AddSpawn(r, SpawnPointType.Light, 1, SpawnPlacement.NearEntrance, priority: 3);
        }

        static void BuildStaircaseRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.4f;
            r.maxFillRatio = 0.2f;

            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 1, max: 2, chance: 0.8f, preferWalls: true);

            AddSpawn(r, SpawnPointType.Prop, 1, SpawnPlacement.Center, priority: 5);
            AddSpawn(r, SpawnPointType.Light, 1, SpawnPlacement.Edges, priority: 3);
        }

        static void BuildGenericRecipe(RoomRecipe r)
        {
            r.densityMultiplier = 0.5f;
            r.maxFillRatio = 0.3f;

            AddProp(r, new[] { PropTags.Lighting }, PropImportance.Minor,
                min: 1, max: 2, chance: 0.7f, preferWalls: true);
        }

        // ════════════════════════════════════════════════════════════════
        //  BUILDER HELPERS
        // ════════════════════════════════════════════════════════════════

        static void AddProp(RoomRecipe r, string[] tags, PropImportance importance,
            int min, int max, float chance,
            bool preferCenter = false, bool preferWalls = false, bool preferCorners = false)
        {
            r.props.Add(new RecipePropEntry
            {
                requiredTags = tags,
                importance = importance,
                minCount = min,
                maxCount = max,
                chance = chance,
                preferCenter = preferCenter,
                preferWalls = preferWalls,
                preferCorners = preferCorners
            });
        }

        static void AddSpawn(RoomRecipe r, SpawnPointType type, int count,
            SpawnPlacement placement, int priority = 5)
        {
            r.spawnPoints.Add(new RecipeSpawnEntry
            {
                pointType = type,
                count = count,
                placement = placement,
                priority = priority
            });
        }

        static void EnsureFolder(string assetPath)
        {
            string fullPath = Path.Combine(
                Application.dataPath,
                assetPath.Replace("Assets/", "").Replace("Assets\\", ""));
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                AssetDatabase.Refresh();
            }
        }

        // ════════════════════════════════════════════════════════════════
        //  MENU ITEM
        // ════════════════════════════════════════════════════════════════

        [MenuItem("DungeonSystem/Generate Default Recipes")]
        public static void MenuGenerateAll()
        {
            var recipes = GenerateAllDefaults();
            EditorUtility.DisplayDialog("Recipe Generator",
                $"Created/found {recipes.Count} room recipes in:\n{DEFAULT_SAVE_PATH}",
                "OK");
        }
    }
}
#endif
