using System.Collections.Generic;
using UnityEngine;

namespace PrefabGallery
{
    [CreateAssetMenu(fileName = "New Theme", menuName = "Prefab Gallery/Theme")]
    public class GalleryTheme : ScriptableObject
    {
        public string themeName = "New Theme";
        public Color themeColor = new Color(1f, 0.55f, 0.2f);
        [TextArea(2, 3)]
        public string description = "";
        public List<GalleryCategory> categories = new List<GalleryCategory>();

        /// <summary>
        /// Get or create a category by name.
        /// </summary>
        public GalleryCategory GetOrCreateCategory(string catName)
        {
            foreach (var cat in categories)
            {
                if (cat.categoryName == catName)
                    return cat;
            }

            var newCat = new GalleryCategory
            {
                categoryName = catName,
                labelColor = GenerateCategoryColor(categories.Count)
            };
            categories.Add(newCat);
            return newCat;
        }

        public int TotalPrefabCount()
        {
            int count = 0;
            foreach (var cat in categories)
                count += cat.entries.Count;
            return count;
        }

        private static readonly Color[] PALETTE = new Color[]
        {
            new Color(0.45f, 0.78f, 1.0f),  // blue
            new Color(0.55f, 0.90f, 0.55f),  // green
            new Color(1.0f, 0.70f, 0.35f),   // orange
            new Color(0.90f, 0.50f, 0.85f),  // purple
            new Color(1.0f, 0.85f, 0.35f),   // yellow
            new Color(0.95f, 0.45f, 0.50f),  // red
            new Color(0.45f, 0.95f, 0.85f),  // teal
            new Color(0.75f, 0.65f, 1.0f),   // lavender
        };

        private static Color GenerateCategoryColor(int index)
        {
            return PALETTE[index % PALETTE.Length];
        }
    }
}
