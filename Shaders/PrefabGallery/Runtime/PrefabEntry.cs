using UnityEngine;

namespace PrefabGallery
{
    [System.Serializable]
    public class PrefabEntry
    {
        public string name;
        public GameObject prefab;
        public Texture2D preview;
        public Vector3 defaultScale = Vector3.one;
        public string assetPath;
        [HideInInspector] public string guid;
    }
}
