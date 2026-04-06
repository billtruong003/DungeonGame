using UnityEngine;

namespace StylizedTextureBaker
{
    public class ColorSpaceHelper
    {
        private ComputeShader _shader;

        public bool IsLinearColorSpace => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public RenderTexture SourceToLinear(Texture2D source, int resolution)
        {
            var rt = TextureUtility.CreateColorRT(resolution);
            Graphics.Blit(source, rt);

            if (!IsLinearColorSpace)
                ApplyConversion(rt, resolution, "SRGBToLinear");

            return rt;
        }

        public void ConvertToSRGB(RenderTexture linearRT, RenderTexture srgbRT)
        {
            if (!IsLinearColorSpace)
            {
                Graphics.Blit(linearRT, srgbRT);
                return;
            }

            ApplyConversion(linearRT, srgbRT, linearRT.width, "LinearToSRGB");
        }

        public RenderTexture CreateSRGBCopy(RenderTexture linearRT)
        {
            var srgbRT = TextureUtility.CreateColorRT(linearRT.width);

            if (IsLinearColorSpace)
                ApplyConversion(linearRT, srgbRT, linearRT.width, "LinearToSRGB");
            else
                Graphics.Blit(linearRT, srgbRT);

            return srgbRT;
        }

        private void ApplyConversion(RenderTexture inPlace, int resolution, string kernelName)
        {
            EnsureShader();
            if (_shader == null) return;

            var temp = TextureUtility.CloneRT(inPlace);
            int kernel = _shader.FindKernel(kernelName);

            _shader.SetInt("_Resolution", resolution);
            _shader.SetTexture(kernel, "_Input", temp);
            _shader.SetTexture(kernel, "_Output", inPlace);

            TextureUtility.DispatchCompute(_shader, kernel, resolution);

            temp.Release();
            Object.DestroyImmediate(temp);
        }

        private void ApplyConversion(RenderTexture input, RenderTexture output, int resolution, string kernelName)
        {
            EnsureShader();
            if (_shader == null) return;

            int kernel = _shader.FindKernel(kernelName);

            _shader.SetInt("_Resolution", resolution);
            _shader.SetTexture(kernel, "_Input", input);
            _shader.SetTexture(kernel, "_Output", output);

            TextureUtility.DispatchCompute(_shader, kernel, resolution);
        }

        private void EnsureShader()
        {
            if (_shader != null) return;
            _shader = ShaderLocator.Find("GammaCorrection");
        }
    }
}
