using UnityEngine;

namespace StylizedTextureBaker
{
    public interface IStyleModule
    {
        string DisplayName { get; }
        StyleType Type { get; }
        bool Enabled { get; set; }
        int Order { get; set; }
        StyleBlendMode BlendMode { get; set; }
        float Opacity { get; set; }

        void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution
        );

        void DrawGUI();
        string Serialize();
        void Deserialize(string data);
    }
}
