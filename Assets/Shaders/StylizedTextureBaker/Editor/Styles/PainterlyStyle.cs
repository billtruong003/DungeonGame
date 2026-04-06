using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    [System.Serializable]
    public class PainterlyParameters
    {
        public float strokeLength = 8f;
        public float strokeWidth = 3f;
        public float flowStrength = 1f;
        public float colorJitter = 0.05f;
        public float saturationBoost = 1.1f;
        public Texture2D noiseTexture;
    }

    public class PainterlyStyle : StyleModuleBase
    {
        public override string DisplayName => "Painterly";
        public override StyleType Type => StyleType.Painterly;

        public PainterlyParameters Parameters = new PainterlyParameters();

        private ComputeShader _shader;

        public override void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution)
        {
            if (_shader == null) _shader = LoadComputeShader("PainterlyWarp");
            if (_shader == null) return;

            int kernel = _shader.FindKernel("PainterlyWarp");

            _shader.SetInt("_Resolution", resolution);
            _shader.SetFloat("_StrokeLength", Parameters.strokeLength);
            _shader.SetFloat("_StrokeWidth", Parameters.strokeWidth);
            _shader.SetFloat("_FlowStrength", Parameters.flowStrength);
            _shader.SetFloat("_ColorJitter", Parameters.colorJitter);
            _shader.SetFloat("_SaturationBoost", Parameters.saturationBoost);
            _shader.SetFloat("_Opacity", Opacity);

            _shader.SetTexture(kernel, "_SourceTex", sourceTexture);
            _shader.SetTexture(kernel, "_DirectionalField", dataMaps.DirectionalField);

            Texture2D noise = Parameters.noiseTexture != null ? Parameters.noiseTexture : NoiseGenerator.GetDeterministic();
            _shader.SetTexture(kernel, "_NoiseTex", noise);

            _shader.SetTexture(kernel, "_Output", outputLayer);
            DispatchFullscreen(_shader, kernel, resolution);
        }

        public override void DrawGUI()
        {
            Parameters.strokeLength = SliderField("Stroke Length", Parameters.strokeLength, 1f, 30f);
            Parameters.strokeWidth = SliderField("Stroke Width", Parameters.strokeWidth, 0.5f, 10f);
            Parameters.flowStrength = SliderField("Flow Strength", Parameters.flowStrength, 0f, 3f);

            EditorGUILayout.Space(4);
            Parameters.colorJitter = SliderField("Color Jitter", Parameters.colorJitter, 0f, 0.2f);
            Parameters.saturationBoost = SliderField("Saturation Boost", Parameters.saturationBoost, 0.5f, 2f);

            EditorGUILayout.Space(4);
            Parameters.noiseTexture = ObjectField("Noise Override", Parameters.noiseTexture);
        }

        public override string Serialize() { return JsonUtility.ToJson(Parameters); }
        public override void Deserialize(string data) { if (!string.IsNullOrEmpty(data)) Parameters = JsonUtility.FromJson<PainterlyParameters>(data); }
    }
}
