using UnityEngine;

namespace StylizedTextureBaker
{
    public static class NoiseGenerator
    {
        private static Texture2D _cached;
        private const int Size = 256;
        private const int Seed = 42;

        public static Texture2D GetDeterministic()
        {
            if (_cached != null) return _cached;

            _cached = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color[Size * Size];
            var state = (uint)Seed;

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(
                    NextFloat(ref state),
                    NextFloat(ref state),
                    NextFloat(ref state),
                    1f
                );
            }

            _cached.SetPixels(pixels);
            _cached.Apply();
            return _cached;
        }

        private static float NextFloat(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x7FFFFFFF) / (float)0x7FFFFFFF;
        }
    }
}
