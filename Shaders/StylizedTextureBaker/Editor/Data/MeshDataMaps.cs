using UnityEngine;

namespace StylizedTextureBaker
{
    public class MeshDataMaps : System.IDisposable
    {
        public RenderTexture CurvatureMap { get; private set; }
        public RenderTexture NormalMap { get; private set; }
        public RenderTexture PositionMap { get; private set; }
        public RenderTexture AOMap { get; private set; }
        public RenderTexture EdgeMask { get; private set; }
        public RenderTexture UVIslandMask { get; private set; }
        public RenderTexture DirectionalField { get; private set; }
        public int Resolution { get; private set; }

        public MeshDataMaps(int resolution)
        {
            Resolution = resolution;
            CurvatureMap = CreateMap(RenderTextureFormat.RGFloat);
            NormalMap = CreateMap(RenderTextureFormat.ARGBFloat);
            PositionMap = CreateMap(RenderTextureFormat.ARGBFloat);
            AOMap = CreateMap(RenderTextureFormat.RFloat);
            EdgeMask = CreateMap(RenderTextureFormat.RFloat);
            UVIslandMask = CreateMap(RenderTextureFormat.RFloat);
            DirectionalField = CreateMap(RenderTextureFormat.RGFloat);
        }

        private RenderTexture CreateMap(RenderTextureFormat format)
        {
            var rt = new RenderTexture(Resolution, Resolution, 0, format)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            rt.Create();
            return rt;
        }

        public void Dispose()
        {
            Release(CurvatureMap);
            Release(NormalMap);
            Release(PositionMap);
            Release(AOMap);
            Release(EdgeMask);
            Release(UVIslandMask);
            Release(DirectionalField);
        }

        private static void Release(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
