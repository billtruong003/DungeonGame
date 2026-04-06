#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace PrefabGallery.Editor
{
    public static class PreviewGenerator
    {
        public const int PREVIEW_SIZE = 256;

        public static Texture2D GeneratePreview(GameObject prefab, string savePath)
        {
            if (prefab == null) return null;

            Texture2D assetPreview = null;
            int attempts = 0;
            while (assetPreview == null && attempts < 40)
            {
                AssetPreview.SetPreviewTextureCacheSize(512);
                assetPreview = AssetPreview.GetAssetPreview(prefab);
                if (assetPreview == null)
                    System.Threading.Thread.Sleep(50);
                attempts++;
            }

            if (assetPreview == null)
                assetPreview = CreatePlaceholder(prefab.name);

            RenderTexture rt = RenderTexture.GetTemporary(PREVIEW_SIZE, PREVIEW_SIZE, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(assetPreview, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(PREVIEW_SIZE, PREVIEW_SIZE, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, PREVIEW_SIZE, PREVIEW_SIZE), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            string dir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllBytes(savePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.ForceUpdate);

            TextureImporter imp = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.GUI;
                imp.npotScale = TextureImporterNPOTScale.None;
                imp.mipmapEnabled = false;
                imp.maxTextureSize = PREVIEW_SIZE;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        private static Texture2D CreatePlaceholder(string name)
        {
            Texture2D tex = new Texture2D(PREVIEW_SIZE, PREVIEW_SIZE, TextureFormat.RGBA32, false);
            Color bg = new Color(0.13f, 0.13f, 0.16f);
            Color[] px = new Color[PREVIEW_SIZE * PREVIEW_SIZE];

            for (int i = 0; i < px.Length; i++) px[i] = bg;

            Color accent = new Color(0.3f, 0.5f, 0.7f, 0.4f);
            int cx = PREVIEW_SIZE / 2, cy = PREVIEW_SIZE / 2, r = PREVIEW_SIZE / 4;
            for (int y = 0; y < PREVIEW_SIZE; y++)
                for (int x = 0; x < PREVIEW_SIZE; x++)
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r)
                        px[y * PREVIEW_SIZE + x] = accent;

            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static string SanitizeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }
    }
}
#endif
