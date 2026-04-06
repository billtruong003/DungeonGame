using System.Collections.Generic;
using UnityEngine;

namespace StylizedTextureBaker
{
    public class StylizationEngine
    {
        private ComputeShader _compositeShader;

        public RenderTexture Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            List<IStyleModule> modules,
            int resolution)
        {
            LoadShader();

            var result = TextureUtility.CloneRT(sourceTexture);
            var sorted = new List<IStyleModule>(modules);
            sorted.Sort((a, b) => a.Order.CompareTo(b.Order));

            foreach (var module in sorted)
            {
                if (!module.Enabled) continue;

                var layer = TextureUtility.CreateColorRT(resolution);
                TextureUtility.ClearRT(layer, Color.clear);

                module.Execute(sourceTexture, dataMaps, edgeData, layer, resolution);
                Composite(result, layer, module.BlendMode, module.Opacity, resolution);

                layer.Release();
                UnityEngine.Object.DestroyImmediate(layer);
            }

            return result;
        }

        public RenderTexture ExecuteSingle(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            IStyleModule module,
            int resolution)
        {
            var layer = TextureUtility.CreateColorRT(resolution);
            TextureUtility.ClearRT(layer, Color.clear);
            module.Execute(sourceTexture, dataMaps, edgeData, layer, resolution);
            return layer;
        }

        private void LoadShader()
        {
            if (_compositeShader != null) return;
            _compositeShader = ShaderLocator.Find("LayerComposite");
        }

        private void Composite(RenderTexture baseRT, RenderTexture layerRT, StyleBlendMode mode, float opacity, int resolution)
        {
            if (_compositeShader == null) return;

            int kernel = _compositeShader.FindKernel("CompositeLayer");
            var readBase = TextureUtility.CloneRT(baseRT);

            _compositeShader.SetInt("_Resolution", resolution);
            _compositeShader.SetInt("_BlendMode", (int)mode);
            _compositeShader.SetFloat("_Opacity", opacity);
            _compositeShader.SetTexture(kernel, "_Base", readBase);
            _compositeShader.SetTexture(kernel, "_Layer", layerRT);
            _compositeShader.SetTexture(kernel, "_Result", baseRT);

            TextureUtility.DispatchCompute(_compositeShader, kernel, resolution);

            readBase.Release();
            UnityEngine.Object.DestroyImmediate(readBase);
        }
    }
}
