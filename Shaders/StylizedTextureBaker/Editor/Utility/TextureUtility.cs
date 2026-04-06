using UnityEngine;

namespace StylizedTextureBaker
{
    public static class TextureUtility
    {
        public static RenderTexture CreateRT(int resolution, RenderTextureFormat format)
        {
            var rt = new RenderTexture(resolution, resolution, 0, format)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        public static RenderTexture CreateColorRT(int resolution)
        {
            return CreateRT(resolution, RenderTextureFormat.ARGBFloat);
        }

        public static RenderTexture CreateMaskRT(int resolution)
        {
            return CreateRT(resolution, RenderTextureFormat.RFloat);
        }

        public static void CopyTexture(RenderTexture source, RenderTexture destination)
        {
            Graphics.Blit(source, destination);
        }

        public static RenderTexture CloneRT(RenderTexture source)
        {
            var clone = CreateRT(source.width, source.format);
            Graphics.Blit(source, clone);
            return clone;
        }

        public static RenderTexture TextureToRT(Texture2D texture, int resolution)
        {
            var rt = CreateColorRT(resolution);
            Graphics.Blit(texture, rt);
            return rt;
        }

        public static Texture2D RTToTexture2D(RenderTexture rt, TextureFormat format = TextureFormat.RGBA32)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;

            var texture = new Texture2D(rt.width, rt.height, format, false);
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();

            RenderTexture.active = previous;
            return texture;
        }

        public static void ClearRT(RenderTexture rt, Color color)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, color);
            RenderTexture.active = previous;
        }

        public static void DispatchCompute(ComputeShader shader, int kernel, int resolution)
        {
            int threadGroups = Mathf.CeilToInt(resolution / 8f);
            shader.Dispatch(kernel, threadGroups, threadGroups, 1);
        }

        public static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
            rt = null;
        }
    }
}
