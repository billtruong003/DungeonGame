using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    [System.Serializable]
    public class HatchingParameters
    {
        public float hatchDensity = 20f;
        public float hatchThickness = 0.4f;
        public bool followSurfaceFlow = true;
        public float baseAngle = 45f;
        public float crossAngle = -45f;
        public bool enableCrossHatch = true;
        public float darkThreshold = 0.4f;
        public Color hatchColor = Color.black;
        public float pressureVariation = 0.3f;
        public Texture2D noiseTexture;
    }

    public class HatchingStyle : StyleModuleBase
    {
        public override string DisplayName => "Hatching";
        public override StyleType Type => StyleType.Hatching;

        public HatchingParameters Parameters = new HatchingParameters();

        private ComputeShader _shader;

        public override void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution)
        {
            if (_shader == null) _shader = LoadComputeShader("HatchingGen");
            if (_shader == null) return;

            int kernel = _shader.FindKernel("GenerateHatching");

            _shader.SetInt("_Resolution", resolution);
            _shader.SetFloat("_HatchDensity", Parameters.hatchDensity);
            _shader.SetFloat("_HatchThickness", Parameters.hatchThickness);
            _shader.SetInt("_FollowSurface", Parameters.followSurfaceFlow ? 1 : 0);
            _shader.SetFloat("_BaseAngle", Parameters.baseAngle);
            _shader.SetFloat("_CrossAngle", Parameters.crossAngle);
            _shader.SetInt("_EnableCross", Parameters.enableCrossHatch ? 1 : 0);
            _shader.SetFloat("_DarkThreshold", Parameters.darkThreshold);
            _shader.SetVector("_HatchColor", Parameters.hatchColor);
            _shader.SetFloat("_PressureVariation", Parameters.pressureVariation);
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
            Parameters.hatchDensity = SliderField("Density", Parameters.hatchDensity, 5f, 80f);
            Parameters.hatchThickness = SliderField("Line Thickness", Parameters.hatchThickness, 0.1f, 1f);
            Parameters.hatchColor = ColorField("Ink Color", Parameters.hatchColor);
            Parameters.pressureVariation = SliderField("Pressure Variation", Parameters.pressureVariation, 0f, 1f);

            EditorGUILayout.Space(4);
            Parameters.followSurfaceFlow = ToggleField("Follow Surface Flow", Parameters.followSurfaceFlow);

            if (!Parameters.followSurfaceFlow)
            {
                EditorGUI.indentLevel++;
                Parameters.baseAngle = SliderField("Base Angle", Parameters.baseAngle, -180f, 180f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            Parameters.enableCrossHatch = ToggleField("Cross-Hatch", Parameters.enableCrossHatch);

            if (Parameters.enableCrossHatch)
            {
                EditorGUI.indentLevel++;
                Parameters.crossAngle = SliderField("Cross Angle", Parameters.crossAngle, -180f, 180f);
                Parameters.darkThreshold = SliderField("Dark Threshold", Parameters.darkThreshold, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            Parameters.noiseTexture = ObjectField("Noise Override", Parameters.noiseTexture);
        }

        public override string Serialize() { return JsonUtility.ToJson(Parameters); }
        public override void Deserialize(string data) { if (!string.IsNullOrEmpty(data)) Parameters = JsonUtility.FromJson<HatchingParameters>(data); }
    }
}
