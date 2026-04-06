using System.Collections.Generic;
using UnityEngine;

namespace StylizedTextureBaker
{
    public class SeamBlender
    {
        private struct SeamPixelPair
        {
            public Vector2Int PixelA;
            public Vector2Int PixelB;
        }

        public void Blend(RenderTexture stylizedTexture, Mesh mesh, int blendRadius)
        {
            var uvs = mesh.uv;
            var triangles = mesh.triangles;
            int resolution = stylizedTexture.width;

            var seamPairs = FindSeamPixelPairs(uvs, triangles, resolution);
            if (seamPairs.Count == 0) return;

            var texture = TextureUtility.RTToTexture2D(stylizedTexture, TextureFormat.RGBAFloat);
            var pixels = texture.GetPixels();

            foreach (var pair in seamPairs)
                BlendPixelNeighborhood(pixels, pair, resolution, blendRadius);

            texture.SetPixels(pixels);
            texture.Apply();

            Graphics.Blit(texture, stylizedTexture);
            Object.DestroyImmediate(texture);
        }

        private List<SeamPixelPair> FindSeamPixelPairs(Vector2[] uvs, int[] triangles, int resolution)
        {
            var pairs = new List<SeamPixelPair>();
            var edgeUVMap = new Dictionary<long, List<Vector2[]>>();

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                RegisterEdgeUV(edgeUVMap, a, b, uvs[a], uvs[b]);
                RegisterEdgeUV(edgeUVMap, b, c, uvs[b], uvs[c]);
                RegisterEdgeUV(edgeUVMap, c, a, uvs[c], uvs[a]);
            }

            foreach (var kvp in edgeUVMap)
            {
                if (kvp.Value.Count < 2) continue;

                var uvPairA = kvp.Value[0];
                var uvPairB = kvp.Value[1];

                bool isSeam = Vector2.Distance(uvPairA[0], uvPairB[0]) > 0.001f ||
                              Vector2.Distance(uvPairA[1], uvPairB[1]) > 0.001f;

                if (!isSeam) continue;

                int sampleCount = Mathf.Max(
                    (int)(Vector2.Distance(uvPairA[0], uvPairA[1]) * resolution),
                    2
                );

                for (int s = 0; s <= sampleCount; s++)
                {
                    float t = (float)s / sampleCount;

                    Vector2 uvA = Vector2.Lerp(uvPairA[0], uvPairA[1], t);
                    Vector2 uvB = Vector2.Lerp(uvPairB[0], uvPairB[1], t);

                    var pixelA = UVToPixel(uvA, resolution);
                    var pixelB = UVToPixel(uvB, resolution);

                    if (pixelA == pixelB) continue;

                    pairs.Add(new SeamPixelPair { PixelA = pixelA, PixelB = pixelB });
                }
            }

            return pairs;
        }

        private static void RegisterEdgeUV(
            Dictionary<long, List<Vector2[]>> map,
            int v0, int v1,
            Vector2 uv0, Vector2 uv1)
        {
            int min = Mathf.Min(v0, v1);
            int max = Mathf.Max(v0, v1);
            long key = ((long)min << 32) | (uint)max;

            if (!map.ContainsKey(key))
                map[key] = new List<Vector2[]>(2);

            bool isReversed = v0 > v1;
            map[key].Add(isReversed ? new[] { uv1, uv0 } : new[] { uv0, uv1 });
        }

        private static Vector2Int UVToPixel(Vector2 uv, int resolution)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(uv.x * resolution), 0, resolution - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(uv.y * resolution), 0, resolution - 1);
            return new Vector2Int(x, y);
        }

        private static void BlendPixelNeighborhood(
            Color[] pixels,
            SeamPixelPair pair,
            int resolution,
            int radius)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy > radius * radius) continue;

                    Vector2Int offsetA = pair.PixelA + new Vector2Int(dx, dy);
                    Vector2Int offsetB = pair.PixelB + new Vector2Int(dx, dy);

                    if (!IsInBounds(offsetA, resolution) || !IsInBounds(offsetB, resolution)) continue;

                    int indexA = offsetA.y * resolution + offsetA.x;
                    int indexB = offsetB.y * resolution + offsetB.x;

                    float weight = 1f - (Mathf.Sqrt(dx * dx + dy * dy) / (radius + 1));
                    float blend = weight * 0.5f;

                    Color colorA = pixels[indexA];
                    Color colorB = pixels[indexB];

                    pixels[indexA] = Color.Lerp(colorA, colorB, blend);
                    pixels[indexB] = Color.Lerp(colorB, colorA, blend);
                }
            }
        }

        private static bool IsInBounds(Vector2Int pixel, int resolution)
        {
            return pixel.x >= 0 && pixel.x < resolution && pixel.y >= 0 && pixel.y < resolution;
        }
    }
}
