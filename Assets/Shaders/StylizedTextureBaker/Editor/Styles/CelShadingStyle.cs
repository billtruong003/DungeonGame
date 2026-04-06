using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    [System.Serializable]
    public class CelShadingParameters
    {
        public int toneLevels = 3;
        public float smoothness = 0.02f;
        public bool bakeDirectionalLight;
        public Vector3 lightDirection = new Vector3(0f, 1f, 0.5f);
        public float lightInfluence = 0.5f;
        public float aoInfluence = 0.3f;
        public bool useCustomRamp;
        public Texture2D toneRampTexture;
    }

    public class CelShadingStyle : StyleModuleBase
    {
        public override string DisplayName => "Cel Shading";
        public override StyleType Type => StyleType.CelShading;

        public CelShadingParameters Parameters = new CelShadingParameters();

        private ComputeShader _shader;

        public override void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution)
        {
            if (_shader == null) _shader = LoadComputeShader("CelShade");
            if (_shader == null) return;

            int kernel = _shader.FindKernel("CelShade");

            _shader.SetInt("_Resolution", resolution);
            _shader.SetInt("_ToneLevels", Parameters.toneLevels);
            _shader.SetFloat("_Smoothness", Parameters.smoothness);
            _shader.SetInt("_BakeLight", Parameters.bakeDirectionalLight ? 1 : 0);
            _shader.SetVector("_LightDir", Parameters.lightDirection.normalized);
            _shader.SetFloat("_LightInfluence", Parameters.lightInfluence);
            _shader.SetFloat("_AOInfluence", Parameters.aoInfluence);
            _shader.SetInt("_UseCustomRamp", Parameters.useCustomRamp && Parameters.toneRampTexture != null ? 1 : 0);
            _shader.SetFloat("_Opacity", Opacity);

            _shader.SetTexture(kernel, "_SourceTex", sourceTexture);
            _shader.SetTexture(kernel, "_NormalMap", dataMaps.NormalMap);
            _shader.SetTexture(kernel, "_AOMap", dataMaps.AOMap);

            if (Parameters.useCustomRamp && Parameters.toneRampTexture != null)
                _shader.SetTexture(kernel, "_ToneRamp", Parameters.toneRampTexture);
            else
                _shader.SetTexture(kernel, "_ToneRamp", Texture2D.whiteTexture);

            _shader.SetTexture(kernel, "_Output", outputLayer);
            DispatchFullscreen(_shader, kernel, resolution);
        }

        public override void DrawGUI()
        {
            Parameters.toneLevels = IntSliderField("Tone Levels", Parameters.toneLevels, 2, 8);
            Parameters.smoothness = SliderField("Band Smoothness", Parameters.smoothness, 0f, 0.1f);
            Parameters.aoInfluence = SliderField("AO Influence", Parameters.aoInfluence, 0f, 1f);

            EditorGUILayout.Space(4);
            Parameters.bakeDirectionalLight = ToggleField("Bake Directional Light", Parameters.bakeDirectionalLight);

            if (Parameters.bakeDirectionalLight)
            {
                EditorGUI.indentLevel++;
                Parameters.lightDirection = EditorGUILayout.Vector3Field("Light Direction", Parameters.lightDirection);
                Parameters.lightInfluence = SliderField("Light Influence", Parameters.lightInfluence, 0f, 1f);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            Parameters.useCustomRamp = ToggleField("Custom Tone Ramp", Parameters.useCustomRamp);

            if (Parameters.useCustomRamp)
            {
                EditorGUI.indentLevel++;
                Parameters.toneRampTexture = ObjectField("Ramp Texture", Parameters.toneRampTexture);
                EditorGUI.indentLevel--;
            }
        }

        public override string Serialize()
        {
            return JsonUtility.ToJson(Parameters);
        }

        public override void Deserialize(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            Parameters = JsonUtility.FromJson<CelShadingParameters>(data);
        }
    }
}
