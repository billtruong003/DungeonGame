using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DungeonSystem.Core;

namespace DungeonSystem.Data
{
    [CreateAssetMenu(fileName = "RoomDatabase", menuName = "DungeonSystem/Room Database")]
    public class RoomDatabase : ScriptableObject
    {
        [Header("All room templates (auto-categorized by roomType)")]
        public List<RoomTemplate> allTemplates = new List<RoomTemplate>();

        private Dictionary<RoomType, List<RoomTemplate>> _cache;

        public Dictionary<RoomType, List<RoomTemplate>> GetCache()
        {
            if (_cache != null) return _cache;

            _cache = new Dictionary<RoomType, List<RoomTemplate>>();
            foreach (RoomType type in System.Enum.GetValues(typeof(RoomType)))
                _cache[type] = new List<RoomTemplate>();

            foreach (var t in allTemplates)
            {
                if (t == null) continue;
                _cache[t.roomType].Add(t);
            }
            return _cache;
        }

        public void InvalidateCache() => _cache = null;

        public RoomTemplate GetRandom(RoomType type, int floorIndex = 0, System.Random rng = null)
        {
            var cache = GetCache();
            if (!cache.TryGetValue(type, out var list) || list.Count == 0)
                return null;

            var valid = list.Where(t => t.IsValidForFloor(floorIndex)).ToList();
            if (valid.Count == 0) valid = list;

            return WeightedPick(valid, rng);
        }

        public List<RoomTemplate> GetAll(RoomType type, string tagFilter = null)
        {
            var cache = GetCache();
            if (!cache.TryGetValue(type, out var list)) return new List<RoomTemplate>();

            if (string.IsNullOrEmpty(tagFilter)) return new List<RoomTemplate>(list);

            return list.Where(t => t.tags != null && t.tags.Contains(tagFilter)).ToList();
        }

        private static RoomTemplate WeightedPick(List<RoomTemplate> candidates, System.Random rng)
        {
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0];

            float totalWeight = candidates.Sum(c => Mathf.Max(c.spawnWeight, 0.01f));
            float roll = rng != null ? (float)(rng.NextDouble() * totalWeight) : Random.Range(0f, totalWeight);

            float accumulated = 0f;
            foreach (var c in candidates)
            {
                accumulated += Mathf.Max(c.spawnWeight, 0.01f);
                if (roll <= accumulated) return c;
            }
            return candidates[^1];
        }

        private void OnValidate() => InvalidateCache();
    }
}
