using System.Collections.Generic;
using UnityEngine;

namespace PrefabGallery
{
    [System.Serializable]
    public class GalleryCategory
    {
        public string categoryName = "New Category";
        public Color labelColor = new Color(0.45f, 0.78f, 1f);
        public bool expanded = true;
        public List<PrefabEntry> entries = new List<PrefabEntry>();
    }
}
