using System.Collections.Generic;
using UnityEngine;

namespace StylizedTextureBaker
{
    [CreateAssetMenu(fileName = "StylePreset", menuName = "Stylized Baker/Style Preset")]
    public class StylePreset : ScriptableObject
    {
        public string presetName = "New Preset";
        public List<StyleLayerData> layers = new List<StyleLayerData>();
    }

    [System.Serializable]
    public class StyleLayerData
    {
        public StyleType type;
        public bool enabled = true;
        public int order;
        public StyleBlendMode blendMode = StyleBlendMode.Normal;
        [Range(0f, 1f)]
        public float opacity = 1f;
        public string serializedParameters;
    }

    public enum StyleType
    {
        Outline,
        CelShading,
        Hatching,
        Painterly,
        Weathering
    }
}
