using UnityEngine;

namespace StylizedTextureBaker
{
    public class EdgeDetector
    {
        private ComputeShader _shader;

        public EdgeFeatureData Detect(RenderTexture sourceTexture, MeshDataMaps dataMaps, BakeSettings settings)
        {
            LoadShader();

            int resolution = settings.ResolutionValue;
            var edgeData = new EdgeFeatureData(resolution);

            DetectTextureEdges(sourceTexture, edgeData.TextureEdge, settings);
            EnhanceGeometryEdges(dataMaps, edgeData.GeometryEdge, settings);
            CompositeEdges(edgeData, settings);

            return edgeData;
        }

        private void LoadShader()
        {
            if (_shader != null) return;
            _shader = ShaderLocator.Find("SobelEdge");
        }

        private void DetectTextureEdges(RenderTexture source, RenderTexture output, BakeSettings settings)
        {
            if (_shader == null) return;

            int kernel = _shader.FindKernel("SobelEdgeDetect");
            int resolution = settings.ResolutionValue;

            _shader.SetInt("_Resolution", resolution);
            _shader.SetFloat("_EdgeStrength", 1f);
            _shader.SetFloat("_EdgeSoftness", settings.edgeSoftness);
            _shader.SetTexture(kernel, "_SourceTex", source);
            _shader.SetTexture(kernel, "_TexEdgeOut", output);

            TextureUtility.DispatchCompute(_shader, kernel, resolution);
        }

        private void EnhanceGeometryEdges(MeshDataMaps dataMaps, RenderTexture output, BakeSettings settings)
        {
            if (_shader == null) return;

            int kernel = _shader.FindKernel("GeometryEdgeEnhance");
            int resolution = settings.ResolutionValue;

            _shader.SetInt("_Resolution", resolution);
            _shader.SetFloat("_CurvEdgeThreshold", settings.curvatureEdgeThreshold);
            _shader.SetFloat("_CurvatureEdgeWeight", settings.curvatureEdgeWeight);
            _shader.SetTexture(kernel, "_CurvatureMap", dataMaps.CurvatureMap);
            _shader.SetTexture(kernel, "_EdgeMask", dataMaps.EdgeMask);
            _shader.SetTexture(kernel, "_GeoEdgeOut", output);

            TextureUtility.DispatchCompute(_shader, kernel, resolution);
        }

        private void CompositeEdges(EdgeFeatureData edgeData, BakeSettings settings)
        {
            if (_shader == null) return;

            int kernel = _shader.FindKernel("CompositeEdges");
            int resolution = settings.ResolutionValue;

            _shader.SetInt("_Resolution", resolution);
            _shader.SetFloat("_GeoWeight", settings.geometryEdgeWeight);
            _shader.SetFloat("_TexWeight", settings.textureEdgeWeight);
            _shader.SetInt("_EdgeThicken", settings.edgeThickenPixels);
            _shader.SetFloat("_MinEdgeStrength", settings.minimumEdgeStrength);
            _shader.SetTexture(kernel, "_TexEdge", edgeData.TextureEdge);
            _shader.SetTexture(kernel, "_GeoEdge", edgeData.GeometryEdge);
            _shader.SetTexture(kernel, "_CompositeEdgeOut", edgeData.CompositeEdge);

            TextureUtility.DispatchCompute(_shader, kernel, resolution);
        }
    }
}
