using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    [System.Serializable]
    public class WeatheringParameters
    {
        public Color wearColor = new Color(0.85f, 0.82f, 0.78f, 1f);
        public float wearAmount = 0.5f;
        public float curvatureThreshold = 0.3f;
        public Color grimeColor = new Color(0.2f, 0.18f, 0.15f, 1f);
        public float grimeAmount = 0.4f;
        public float noiseScale = 5f;
        public float noiseBreakup = 0.6f;
        public Texture2D noiseTexture;
    }

    public class WeatheringStyle : StyleModuleBase
    {
        public override string DisplayName => "Weathering";
        public override StyleType Type => StyleType.Weathering;

        public WeatheringParameters Parameters = new WeatheringParameters();

        private ComputeShader _shader;

        public override void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution)
        {
            if (_shader == null) _shader = LoadComputeShader("WeatheringGen");
            if (_shader == null) return;

            int kernel = _shader.FindKernel("GenerateWeathering");

            _shader.SetInt("_Resolution", resolution);
            _shader.SetVector("_WearColor", Parameters.wearColor);
            _shader.SetFloat("_WearAmount", Parameters.wearAmount);
            _shader.SetFloat("_CurvThreshold", Parameters.curvatureThreshold);
            _shader.SetVector("_GrimeColor", Parameters.grimeColor);
            _shader.SetFloat("_GrimeAmount", Parameters.grimeAmount);
            _shader.SetFloat("_NoiseScale", Parameters.noiseScale);
            _shader.SetFloat("_NoiseBreakup", Parameters.noiseBreakup);
            _shader.SetFloat("_Opacity", Opacity);

            _shader.SetTexture(kernel, "_SourceTex", sourceTexture);
            _shader.SetTexture(kernel, "_CurvatureMap", dataMaps.CurvatureMap);
            _shader.SetTexture(kernel, "_AOMap", dataMaps.AOMap);

            Texture2D noise = Parameters.noiseTexture != null ? Parameters.noiseTexture : NoiseGenerator.GetDeterministic();
            _shader.SetTexture(kernel, "_NoiseTex", noise);

            _shader.SetTexture(kernel, "_Output", outputLayer);
            DispatchFullscreen(_shader, kernel, resolution);
        }

        public override void DrawGUI()
        {
            EditorGUILayout.LabelField("Edge Wear", EditorStyles.boldLabel);
            Parameters.wearColor = ColorField("Wear Color", Parameters.wearColor);
            Parameters.wearAmount = SliderField("Wear Amount", Parameters.wearAmount, 0f, 1f);
            Parameters.curvatureThreshold = SliderField("Curvature Threshold", Parameters.curvatureThreshold, 0.01f, 1f);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Cavity Grime", EditorStyles.boldLabel);
            Parameters.grimeColor = ColorField("Grime Color", Parameters.grimeColor);
            Parameters.grimeAmount = SliderField("Grime Amount", Parameters.grimeAmount, 0f, 1f);

            EditorGUILayout.Space(6);
            Parameters.noiseScale = SliderField("Noise Scale", Parameters.noiseScale, 0.5f, 20f);
            Parameters.noiseBreakup = SliderField("Breakup", Parameters.noiseBreakup, 0f, 1f);
            Parameters.noiseTexture = ObjectField("Noise Override", Parameters.noiseTexture);
        }

        public override string Serialize() { return JsonUtility.ToJson(Parameters); }
        public override void Deserialize(string data) { if (!string.IsNullOrEmpty(data)) Parameters = JsonUtility.FromJson<WeatheringParameters>(data); }
    }
}
