using System;
using System.Collections.Generic;
using UnityEngine;

namespace StylizedTextureBaker
{
    public class BakerPipeline
    {
        private readonly MeshDataExtractor _dataExtractor = new MeshDataExtractor();
        private readonly EdgeDetector _edgeDetector = new EdgeDetector();
        private readonly StylizationEngine _stylizationEngine = new StylizationEngine();
        private readonly UVCompositor _uvCompositor = new UVCompositor();
        private readonly TextureExporter _exporter = new TextureExporter();
        private readonly ColorSpaceHelper _colorSpace = new ColorSpaceHelper();

        public ColorSpaceHelper ColorSpace => _colorSpace;

        public MeshDataMaps BakeDataMaps(Mesh mesh, BakeSettings settings)
        {
            return _dataExtractor.Extract(mesh, settings);
        }

        public EdgeFeatureData DetectEdges(RenderTexture sourceRT, MeshDataMaps dataMaps, BakeSettings settings)
        {
            return _edgeDetector.Detect(sourceRT, dataMaps, settings);
        }

        public RenderTexture CreateLinearSourceRT(Texture2D source, int resolution)
        {
            return _colorSpace.SourceToLinear(source, resolution);
        }

        public RenderTexture StylizeToSRGB(
            RenderTexture linearSourceRT,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            List<IStyleModule> modules,
            int resolution)
        {
            var linearResult = _stylizationEngine.Execute(linearSourceRT, dataMaps, edgeData, modules, resolution);
            var srgbResult = _colorSpace.CreateSRGBCopy(linearResult);
            linearResult.Release();
            UnityEngine.Object.DestroyImmediate(linearResult);
            return srgbResult;
        }

        public BakeResult FullBake(
            Mesh mesh,
            Texture2D sourceTexture,
            BakeSettings settings,
            List<IStyleModule> modules,
            Action<string, float> onProgress = null)
        {
            int resolution = settings.ResolutionValue;
            var result = new BakeResult { Resolution = resolution };

            try
            {
                onProgress?.Invoke("Extracting mesh data...", 0f);
                result.DataMaps = _dataExtractor.Extract(mesh, settings);

                onProgress?.Invoke("Detecting edges...", 0.15f);
                var linearSourceRT = _colorSpace.SourceToLinear(sourceTexture, resolution);
                result.EdgeData = _edgeDetector.Detect(linearSourceRT, result.DataMaps, settings);

                onProgress?.Invoke("Applying stylization...", 0.35f);
                var linearStylized = _stylizationEngine.Execute(
                    linearSourceRT, result.DataMaps, result.EdgeData, modules, resolution);

                onProgress?.Invoke("Building outline mask...", 0.55f);
                result.OutlineMask = ExtractOutlineMask(linearSourceRT, result.DataMaps, result.EdgeData, modules, resolution);
                result.CompositeEdge = TextureUtility.CloneRT(result.EdgeData.CompositeEdge);

                onProgress?.Invoke("UV compositing...", 0.7f);
                _uvCompositor.Composite(linearStylized, result.DataMaps.UVIslandMask, mesh, settings.paddingPixels, settings.seamBlendRadius);

                onProgress?.Invoke("Converting color space...", 0.9f);
                result.StylizedColor = _colorSpace.CreateSRGBCopy(linearStylized);

                linearStylized.Release();
                UnityEngine.Object.DestroyImmediate(linearStylized);
                linearSourceRT.Release();
                UnityEngine.Object.DestroyImmediate(linearSourceRT);

                onProgress?.Invoke("Complete", 1f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StylizedBaker] Bake failed: {e.Message}\n{e.StackTrace}");
                result.Dispose();
                return null;
            }

            return result;
        }

        public BakeResult BakeFromCachedData(
            Mesh mesh,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture linearSourceRT,
            BakeSettings settings,
            List<IStyleModule> modules,
            Action<string, float> onProgress = null)
        {
            int resolution = settings.ResolutionValue;
            var result = new BakeResult
            {
                Resolution = resolution,
                DataMaps = dataMaps,
                EdgeData = edgeData
            };

            try
            {
                onProgress?.Invoke("Applying stylization...", 0.15f);
                var linearStylized = _stylizationEngine.Execute(
                    linearSourceRT, dataMaps, edgeData, modules, resolution);

                onProgress?.Invoke("Building outline mask...", 0.4f);
                result.OutlineMask = ExtractOutlineMask(linearSourceRT, dataMaps, edgeData, modules, resolution);
                result.CompositeEdge = TextureUtility.CloneRT(edgeData.CompositeEdge);

                onProgress?.Invoke("UV compositing...", 0.6f);
                _uvCompositor.Composite(linearStylized, dataMaps.UVIslandMask, mesh, settings.paddingPixels, settings.seamBlendRadius);

                onProgress?.Invoke("Converting color space...", 0.85f);
                result.StylizedColor = _colorSpace.CreateSRGBCopy(linearStylized);

                linearStylized.Release();
                UnityEngine.Object.DestroyImmediate(linearStylized);

                onProgress?.Invoke("Complete", 1f);
            }
            catch (Exception e)
            {
                Debug.LogError($"[StylizedBaker] Bake failed: {e.Message}\n{e.StackTrace}");
                result.DataMaps = null;
                result.EdgeData = null;
                result.Dispose();
                return null;
            }

            return result;
        }

        public void Export(BakeResult result, string objectName, BakeSettings settings)
        {
            if (result == null) return;
            _exporter.ExportBakeResult(result, settings.outputFolder, objectName, settings);
        }

        private RenderTexture ExtractOutlineMask(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            List<IStyleModule> modules,
            int resolution)
        {
            foreach (var module in modules)
            {
                if (module.Type != StyleType.Outline || !module.Enabled) continue;

                var mask = TextureUtility.CreateMaskRT(resolution);
                TextureUtility.ClearRT(mask, Color.clear);
                module.Execute(sourceTexture, dataMaps, edgeData, mask, resolution);
                return mask;
            }

            return null;
        }
    }
}
