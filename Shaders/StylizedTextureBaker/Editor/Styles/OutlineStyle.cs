using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    [System.Serializable]
    public class OutlineParameters
    {
        public float baseThickness = 2f;
        public float thicknessVariation = 1.5f;
        public Color strokeColor = Color.black;
        public Texture2D brushTexture;
        public bool useBrush;
        public float brushScale = 1f;
        public float brushRotationJitter = 15f;
        public float edgeThreshold = 0.25f;
    }

    public class OutlineStyle : StyleModuleBase
    {
        public override string DisplayName => "Outline";
        public override StyleType Type => StyleType.Outline;

        public OutlineParameters Parameters = new OutlineParameters();

        private ComputeShader _jfaShader;
        private ComputeShader _outlineShader;

        public override void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution)
        {
            if (_jfaShader == null) _jfaShader = LoadComputeShader("JumpFlood");
            if (_outlineShader == null) _outlineShader = LoadComputeShader("OutlineGen");
            if (_jfaShader == null || _outlineShader == null) return;

            var distanceField = TextureUtility.CreateMaskRT(resolution);
            var strengthField = TextureUtility.CreateMaskRT(resolution);

            ComputeJFA(edgeData.CompositeEdge, distanceField, strengthField, resolution);
            RenderStroke(distanceField, strengthField, dataMaps, outputLayer, resolution);

            distanceField.Release();
            Object.DestroyImmediate(distanceField);
            strengthField.Release();
            Object.DestroyImmediate(strengthField);
        }

        private void ComputeJFA(RenderTexture edgeSource, RenderTexture distOut, RenderTexture strengthOut, int resolution)
        {
            var coordA = TextureUtility.CreateRT(resolution, RenderTextureFormat.RGFloat);
            var coordB = TextureUtility.CreateRT(resolution, RenderTextureFormat.RGFloat);

            int seedKernel = _jfaShader.FindKernel("Seed");
            _jfaShader.SetInt("_Resolution", resolution);
            _jfaShader.SetFloat("_EdgeThreshold", Parameters.edgeThreshold);
            _jfaShader.SetTexture(seedKernel, "_EdgeSource", edgeSource);
            _jfaShader.SetTexture(seedKernel, "_CoordWrite", coordA);
            DispatchFullscreen(_jfaShader, seedKernel, resolution);

            int propKernel = _jfaShader.FindKernel("Propagate");
            bool readFromA = true;

            int maxStep = Mathf.NextPowerOfTwo(resolution) / 2;
            for (int step = maxStep; step >= 1; step /= 2)
            {
                var readTex = readFromA ? coordA : coordB;
                var writeTex = readFromA ? coordB : coordA;

                _jfaShader.SetInt("_Resolution", resolution);
                _jfaShader.SetInt("_StepSize", step);
                _jfaShader.SetTexture(propKernel, "_CoordRead", readTex);
                _jfaShader.SetTexture(propKernel, "_CoordWrite", writeTex);
                DispatchFullscreen(_jfaShader, propKernel, resolution);

                readFromA = !readFromA;
            }

            int distKernel = _jfaShader.FindKernel("BuildDistanceField");
            var finalCoord = readFromA ? coordA : coordB;

            _jfaShader.SetInt("_Resolution", resolution);
            _jfaShader.SetTexture(distKernel, "_CoordRead", finalCoord);
            _jfaShader.SetTexture(distKernel, "_EdgeSource", edgeSource);
            _jfaShader.SetTexture(distKernel, "_DistanceOut", distOut);
            _jfaShader.SetTexture(distKernel, "_StrengthOut", strengthOut);
            DispatchFullscreen(_jfaShader, distKernel, resolution);

            coordA.Release();
            Object.DestroyImmediate(coordA);
            coordB.Release();
            Object.DestroyImmediate(coordB);
        }

        private void RenderStroke(RenderTexture distanceField, RenderTexture strengthField, MeshDataMaps dataMaps, RenderTexture output, int resolution)
        {
            int kernel = _outlineShader.FindKernel("GenerateOutline");
            bool brushActive = Parameters.useBrush && Parameters.brushTexture != null;

            _outlineShader.SetInt("_Resolution", resolution);
            _outlineShader.SetFloat("_BaseThickness", Parameters.baseThickness);
            _outlineShader.SetFloat("_ThicknessVariation", Parameters.thicknessVariation);
            _outlineShader.SetVector("_StrokeColor", Parameters.strokeColor);
            _outlineShader.SetFloat("_Opacity", Opacity);
            _outlineShader.SetInt("_UseBrush", brushActive ? 1 : 0);
            _outlineShader.SetFloat("_BrushScale", Parameters.brushScale);
            _outlineShader.SetFloat("_BrushRotationJitter", Parameters.brushRotationJitter);

            _outlineShader.SetTexture(kernel, "_DistanceField", distanceField);
            _outlineShader.SetTexture(kernel, "_EdgeStrength", strengthField);
            _outlineShader.SetTexture(kernel, "_CurvatureMap", dataMaps.CurvatureMap);
            _outlineShader.SetTexture(kernel, "_DirectionalField", dataMaps.DirectionalField);
            BindTexture(_outlineShader, kernel, "_BrushTex", brushActive ? Parameters.brushTexture : null);
            _outlineShader.SetTexture(kernel, "_Output", output);

            DispatchFullscreen(_outlineShader, kernel, resolution);
        }

        public override void DrawGUI()
        {
            Parameters.baseThickness = SliderField("Base Thickness", Parameters.baseThickness, 0.5f, 20f);
            Parameters.thicknessVariation = SliderField("Curvature Variation", Parameters.thicknessVariation, 0f, 5f);
            Parameters.strokeColor = ColorField("Stroke Color", Parameters.strokeColor);
            Parameters.edgeThreshold = SliderField("Edge Threshold", Parameters.edgeThreshold, 0.05f, 0.9f);

            EditorGUILayout.Space(4);
            Parameters.useBrush = ToggleField("Use Brush Texture", Parameters.useBrush);

            if (Parameters.useBrush)
            {
                EditorGUI.indentLevel++;
                Parameters.brushTexture = ObjectField("Brush", Parameters.brushTexture);
                Parameters.brushScale = SliderField("Brush Scale", Parameters.brushScale, 0.1f, 5f);
                Parameters.brushRotationJitter = SliderField("Rotation Jitter", Parameters.brushRotationJitter, 0f, 45f);
                EditorGUI.indentLevel--;
            }
        }

        public override string Serialize() { return JsonUtility.ToJson(Parameters); }
        public override void Deserialize(string data) { if (!string.IsNullOrEmpty(data)) Parameters = JsonUtility.FromJson<OutlineParameters>(data); }
    }
}
