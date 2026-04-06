using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThemeLevelDesigner
{
    [CreateAssetMenu(fileName = "NewTheme", menuName = "Level Design/Theme", order = 0)]
    public class ThemeSO : ScriptableObject
    {
        [Header("Theme Info")]
        public string themeName = "New Theme";
        public Color themeColor = Color.cyan;
        public Sprite themeIcon;

        [Header("Sections")]
        public List<SectionEntry> sections = new();

        [Header("Environment (Optional)")]
        public VolumeProfile postProcessProfile;
        public Material skyboxOverride;
        public Color ambientColor = new Color(0.2f, 0.2f, 0.25f);
        public Color fogColor = new Color(0.1f, 0.1f, 0.15f);
        [Range(0f, 0.1f)] public float fogDensity = 0.02f;

        [Header("Audio (Optional)")]
        public AudioClip ambientLoop;
        public AudioClip footstepClip;

        /// <summary>Get sections filtered by tag.</summary>
        public List<SectionEntry> GetByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag) || tag == "All")
                return sections;

            var result = new List<SectionEntry>();
            foreach (var s in sections)
            {
                if (s.tags == null) continue;
                foreach (var t in s.tags)
                {
                    if (t == tag)
                    {
                        result.Add(s);
                        break;
                    }
                }
            }
            return result;
        }

        /// <summary>Collect all unique tags across sections.</summary>
        public List<string> GetAllTags()
        {
            var tags = new HashSet<string>();
            foreach (var s in sections)
            {
                if (s.tags == null) continue;
                foreach (var t in s.tags)
                    tags.Add(t);
            }
            var list = new List<string>(tags);
            list.Sort();
            list.Insert(0, "All");
            return list;
        }
    }
}
