using UnityEngine;

namespace StylizedTextureBaker
{
    public class UVCompositor
    {
        private ComputeShader _paddingShader;
        private readonly SeamBlender _seamBlender = new SeamBlender();

        public void Composite(RenderTexture stylizedTexture, RenderTexture uvIslandMask, Mesh mesh, int paddingPixels, int seamBlendRadius)
        {
            if (seamBlendRadius > 0 && mesh != null)
                _seamBlender.Blend(stylizedTexture, mesh, seamBlendRadius);

            ApplyPadding(stylizedTexture, uvIslandMask, paddingPixels);
        }

        private void ApplyPadding(RenderTexture stylizedTexture, RenderTexture uvIslandMask, int paddingPixels)
        {
            LoadShader();
            if (_paddingShader == null) return;

            int resolution = stylizedTexture.width;

            var maskA = TextureUtility.CreateMaskRT(resolution);
            var maskB = TextureUtility.CreateMaskRT(resolution);
            var colorB = TextureUtility.CreateColorRT(resolution);

            int initKernel = _paddingShader.FindKernel("InitMask");
            _paddingShader.SetInt("_Resolution", resolution);
            _paddingShader.SetTexture(initKernel, "_UVIslandMask", uvIslandMask);
            _paddingShader.SetTexture(initKernel, "_MaskWrite", maskA);
            TextureUtility.DispatchCompute(_paddingShader, initKernel, resolution);

            int dilateKernel = _paddingShader.FindKernel("DilatePingPong");
            bool readFromA = true;

            for (int i = 0; i < paddingPixels; i++)
            {
                var readColor = readFromA ? stylizedTexture : colorB;
                var writeColor = readFromA ? colorB : stylizedTexture;
                var readMask = readFromA ? maskA : maskB;
                var writeMask = readFromA ? maskB : maskA;

                _paddingShader.SetInt("_Resolution", resolution);
                _paddingShader.SetTexture(dilateKernel, "_ColorRead", readColor);
                _paddingShader.SetTexture(dilateKernel, "_MaskRead", readMask);
                _paddingShader.SetTexture(dilateKernel, "_ColorWrite", writeColor);
                _paddingShader.SetTexture(dilateKernel, "_MaskWrite", writeMask);
                TextureUtility.DispatchCompute(_paddingShader, dilateKernel, resolution);

                readFromA = !readFromA;
            }

            if (!readFromA)
                Graphics.Blit(colorB, stylizedTexture);

            maskA.Release();
            Object.DestroyImmediate(maskA);
            maskB.Release();
            Object.DestroyImmediate(maskB);
            colorB.Release();
            Object.DestroyImmediate(colorB);
        }

        private void LoadShader()
        {
            if (_paddingShader != null) return;
            _paddingShader = ShaderLocator.Find("EdgePadding");
        }
    }
}
