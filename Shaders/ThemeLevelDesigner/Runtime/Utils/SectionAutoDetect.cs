using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace ThemeLevelDesigner
{
    /// <summary>
    /// Auto-detects SectionEntry properties from a prefab:
    /// - Grid size from mesh bounds or naming convention
    /// - Tags from prefab name
    /// - Display name cleaned up from prefab name
    /// </summary>
    public static class SectionAutoDetect
    {
        // Known tag keywords to scan for in prefab names
        static readonly Dictionary<string, string[]> TagKeywords = new()
        {
            { "floor",     new[] { "floor", "ground", "tile", "terrain" } },
            { "wall",      new[] { "wall", "fence", "barrier" } },
            { "door",      new[] { "door", "gate", "entrance", "exit", "portal" } },
            { "prop",      new[] { "prop", "deco", "decor", "decoration", "furniture", "object" } },
            { "pillar",    new[] { "pillar", "column", "post" } },
            { "stair",     new[] { "stair", "step", "ladder", "ramp" } },
            { "trap",      new[] { "trap", "spike", "hazard", "danger" } },
            { "corner",    new[] { "corner", "bend", "turn" } },
            { "corridor",  new[] { "corridor", "hallway", "passage", "tunnel" } },
            { "ceiling",   new[] { "ceiling", "roof", "top" } },
            { "window",    new[] { "window" } },
            { "platform",  new[] { "platform", "bridge", "walkway" } },
            { "light",     new[] { "light", "lamp", "torch", "candle", "lantern" } },
            { "chest",     new[] { "chest", "loot", "treasure", "crate", "barrel" } },
            { "spawn",     new[] { "spawn", "start", "checkpoint" } },
        };

        /// <summary>
        /// Create a fully populated SectionEntry from a prefab.
        /// </summary>
        public static SectionEntry FromPrefab(GameObject prefab, float cellSize = 1f)
        {
            if (prefab == null) return null;

            var entry = new SectionEntry
            {
                prefab = prefab,
                id = GenerateId(prefab.name),
                displayName = CleanDisplayName(prefab.name),
                canRotate = true
            };

            // Try parse size from name first (e.g. "Floor_4x4", "Wall_1x3")
            var parsedSize = ParseSizeFromName(prefab.name);
            if (parsedSize.HasValue)
            {
                entry.gridSize = parsedSize.Value;
            }
            else
            {
                // Auto-detect from mesh bounds
                entry.gridSize = DetectGridSize(prefab, cellSize);
            }

            // Auto-detect tags from name
            entry.tags = DetectTags(prefab.name);

            return entry;
        }

        /// <summary>
        /// Try to parse grid size from naming patterns like:
        /// "Floor_Stone_4x4", "Wall_2x3", "Tile4x4", "Floor (4x4)"
        /// </summary>
        public static Vector2Int? ParseSizeFromName(string name)
        {
            // Patterns: 4x4, 4X4, 4×4
            var match = Regex.Match(name, @"(\d+)\s*[xX×]\s*(\d+)");
            if (match.Success)
            {
                int w = int.Parse(match.Groups[1].Value);
                int h = int.Parse(match.Groups[2].Value);
                if (w > 0 && w <= 64 && h > 0 && h <= 64)
                    return new Vector2Int(w, h);
            }
            return null;
        }

        /// <summary>
        /// Detect grid size from the prefab's combined renderer bounds.
        /// </summary>
        public static Vector2Int DetectGridSize(GameObject prefab, float cellSize = 1f)
        {
            var instance = Object.Instantiate(prefab);
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.hideFlags = HideFlags.HideAndDontSave;

            var renderers = instance.GetComponentsInChildren<Renderer>();
            Vector2Int result;

            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                // Round up to nearest cell
                int w = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cellSize));
                int h = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cellSize));
                result = new Vector2Int(w, h);
            }
            else
            {
                // Fallback: check colliders
                var colliders = instance.GetComponentsInChildren<Collider>();
                if (colliders.Length > 0)
                {
                    var bounds = colliders[0].bounds;
                    for (int i = 1; i < colliders.Length; i++)
                        bounds.Encapsulate(colliders[i].bounds);

                    int w = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / cellSize));
                    int h = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / cellSize));
                    result = new Vector2Int(w, h);
                }
                else
                {
                    result = Vector2Int.one;
                }
            }

            Object.DestroyImmediate(instance);
            return result;
        }

        /// <summary>
        /// Detect tags from prefab name by matching against known keywords.
        /// </summary>
        public static string[] DetectTags(string prefabName)
        {
            var nameLower = prefabName.ToLower();
            var found = new List<string>();

            foreach (var kvp in TagKeywords)
            {
                foreach (var keyword in kvp.Value)
                {
                    if (nameLower.Contains(keyword))
                    {
                        found.Add(kvp.Key);
                        break; // one match per tag category is enough
                    }
                }
            }

            // Fallback: if nothing matched, tag as "prop"
            if (found.Count == 0)
                found.Add("prop");

            return found.ToArray();
        }

        /// <summary>
        /// Clean up prefab name into a readable display name.
        /// "SM_Floor_Stone_4x4" → "Floor Stone"
        /// "Env_Wall_Brick_Damaged_02" → "Wall Brick Damaged 02"
        /// </summary>
        public static string CleanDisplayName(string prefabName)
        {
            var name = prefabName;

            // Remove common prefixes
            string[] prefixes = { "SM_", "SM.", "Env_", "Env.", "P_", "Prop_", "Geo_",
                                  "Mesh_", "MDL_", "PRF_", "PF_", "T_" };
            foreach (var prefix in prefixes)
            {
                if (name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                {
                    name = name[prefix.Length..];
                    break;
                }
            }

            // Remove size suffix like _4x4, (4x4)
            name = Regex.Replace(name, @"[_\s]*\(?\d+[xX×]\d+\)?$", "");

            // Replace underscores and dots with spaces
            name = name.Replace('_', ' ').Replace('.', ' ');

            // Remove duplicate spaces
            name = Regex.Replace(name, @"\s+", " ").Trim();

            return name;
        }

        /// <summary>
        /// Generate a snake_case id from prefab name.
        /// </summary>
        public static string GenerateId(string prefabName)
        {
            var id = prefabName.ToLower();
            id = Regex.Replace(id, @"[^a-z0-9_]", "_");
            id = Regex.Replace(id, @"_+", "_").Trim('_');
            return id;
        }

        /// <summary>
        /// Check if a prefab already exists in a theme's section list.
        /// </summary>
        public static bool ExistsInTheme(ThemeSO theme, GameObject prefab)
        {
            return theme.sections.Any(s => s.prefab == prefab);
        }
    }
}
