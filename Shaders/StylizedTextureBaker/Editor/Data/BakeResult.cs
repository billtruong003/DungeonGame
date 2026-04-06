using UnityEngine;

namespace StylizedTextureBaker
{
    public class BakeResult : System.IDisposable
    {
        public RenderTexture StylizedColor { get; set; }
        public RenderTexture OutlineMask { get; set; }
        public RenderTexture CompositeEdge { get; set; }
        public MeshDataMaps DataMaps { get; set; }
        public EdgeFeatureData EdgeData { get; set; }
        public int Resolution { get; set; }

        public void Dispose()
        {
            Release(StylizedColor);
            Release(OutlineMask);
            Release(CompositeEdge);
            DataMaps?.Dispose();
            EdgeData?.Dispose();
        }

        private static void Release(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
