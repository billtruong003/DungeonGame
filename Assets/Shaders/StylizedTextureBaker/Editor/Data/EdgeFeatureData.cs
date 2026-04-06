using UnityEngine;

namespace StylizedTextureBaker
{
    public class EdgeFeatureData : System.IDisposable
    {
        public RenderTexture TextureEdge { get; private set; }
        public RenderTexture GeometryEdge { get; private set; }
        public RenderTexture CompositeEdge { get; private set; }
        public int Resolution { get; private set; }

        public EdgeFeatureData(int resolution)
        {
            Resolution = resolution;
            TextureEdge = CreateEdgeMap(resolution);
            GeometryEdge = CreateEdgeMap(resolution);
            CompositeEdge = CreateEdgeMap(resolution);
        }

        private static RenderTexture CreateEdgeMap(int resolution)
        {
            var rt = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat)
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
            Release(TextureEdge);
            Release(GeometryEdge);
            Release(CompositeEdge);
        }

        private static void Release(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
