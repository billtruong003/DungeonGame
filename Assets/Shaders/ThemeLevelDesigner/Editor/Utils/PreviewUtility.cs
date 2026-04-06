using UnityEditor;
using UnityEngine;
using System.IO;

namespace ThemeLevelDesigner.Editor
{
    /// <summary>
    /// Generates preview thumbnails for section prefabs using Unity's PreviewRenderUtility.
    /// Works correctly with URP/HDRP/Built-in. Saves textures to disk so they persist.
    /// </summary>
    public static class PreviewUtility
    {
        const int DefaultResolution = 256;

        /// <summary>
        /// Generate a preview texture for a prefab and optionally save it to disk.
        /// </summary>
        /// <param name="prefab">Source prefab</param>
        /// <param name="resolution">Texture resolution</param>
        /// <param name="saveToDisk">If true, saves as PNG next to the prefab</param>
        public static Texture2D GeneratePreview(GameObject prefab, int resolution = DefaultResolution, bool saveToDisk = true)
        {
            if (prefab == null) return null;

            Texture2D result = null;

            // Method 1: Try Unity's built-in AssetPreview (most reliable across all pipelines)
            result = TryAssetPreview(prefab, resolution);

            // Method 2: Use PreviewRenderUtility
            if (result == null)
                result = TryPreviewRenderUtility(prefab, resolution);

            // Method 3: Fallback — simple colored placeholder
            if (result == null)
                result = CreatePlaceholder(prefab.name, resolution);

            // Save to disk so it persists across domain reloads
            if (saveToDisk && result != null)
                result = SavePreviewToDisk(prefab, result);

            return result;
        }

        /// <summary>
        /// Load an existing preview from disk if available.
        /// </summary>
        public static Texture2D LoadExistingPreview(GameObject prefab)
        {
            var path = GetPreviewPath(prefab);
            if (string.IsNullOrEmpty(path)) return null;

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ==================== METHOD 1: AssetPreview ====================

        static Texture2D TryAssetPreview(GameObject prefab, int resolution)
        {
            // Force Unity to generate the asset preview
            var editor = UnityEditor.Editor.CreateEditor(prefab);
            if (editor == null) return null;

            Texture2D preview = null;

            try
            {
                // Request preview — may need a few attempts as Unity generates async
                AssetPreview.SetPreviewTextureCacheSize(256);

                for (int attempt = 0; attempt < 50; attempt++)
                {
                    preview = AssetPreview.GetAssetPreview(prefab);
                    if (preview != null && preview.width > 32) break;

                    // Let Unity process
                    System.Threading.Thread.Sleep(20);
                    AssetPreview.GetAssetPreview(prefab); // re-request
                }

                if (preview != null && preview.width > 32)
                {
                    // AssetPreview textures are temporary — copy to a persistent texture
                    var copy = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
                    copy.SetPixels(preview.GetPixels());
                    copy.Apply();

                    // Resize if needed
                    if (copy.width != resolution)
                        copy = ResizeTexture(copy, resolution, resolution);

                    return copy;
                }
            }
            finally
            {
                Object.DestroyImmediate(editor);
            }

            return null;
        }

        // ==================== METHOD 2: PreviewRenderUtility ====================

        static Texture2D TryPreviewRenderUtility(GameObject prefab, int resolution)
        {
            PreviewRenderUtility previewUtil = null;

            try
            {
                previewUtil = new PreviewRenderUtility();

                // Setup camera
                var cam = previewUtil.camera;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.15f, 0.15f, 0.18f, 1f);
                cam.orthographic = true;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 200f;

                // Instantiate prefab in preview scene
                var instance = previewUtil.InstantiatePrefabInScene(prefab);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                // Calculate bounds
                var renderers = instance.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return null;

                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                // Position camera: top-down angled view
                float maxDim = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                cam.orthographicSize = maxDim * 0.65f;

                // Isometric-like angle for better visual
                var camPos = bounds.center + new Vector3(-0.5f, 1.2f, -0.5f).normalized * (maxDim * 2f);
                cam.transform.position = camPos;
                cam.transform.LookAt(bounds.center);

                // Add basic lighting
                previewUtil.lights[0].intensity = 1.2f;
                previewUtil.lights[0].transform.rotation = Quaternion.Euler(45, -30, 0);
                previewUtil.lights[1].intensity = 0.4f;

                // Render
                var renderRect = new Rect(0, 0, resolution, resolution);
                previewUtil.BeginPreview(renderRect, GUIStyle.none);
                previewUtil.camera.Render();
                var tex = previewUtil.EndStaticPreview();

                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PreviewUtility] PreviewRenderUtility failed for '{prefab.name}': {e.Message}");
                return null;
            }
            finally
            {
                previewUtil?.Cleanup();
            }
        }

        // ==================== METHOD 3: Placeholder ====================

        static Texture2D CreatePlaceholder(string name, int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var bg = new Color(0.2f, 0.2f, 0.25f, 1f);
            var pixels = new Color[resolution * resolution];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

            // Simple border
            var border = new Color(0.4f, 0.6f, 0.8f, 1f);
            for (int x = 0; x < resolution; x++)
            {
                pixels[x] = border;                              // bottom
                pixels[(resolution - 1) * resolution + x] = border; // top
            }
            for (int y = 0; y < resolution; y++)
            {
                pixels[y * resolution] = border;                  // left
                pixels[y * resolution + resolution - 1] = border; // right
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // ==================== SAVE TO DISK ====================

        static Texture2D SavePreviewToDisk(GameObject prefab, Texture2D tex)
        {
            var savePath = GetPreviewPath(prefab);
            if (string.IsNullOrEmpty(savePath)) return tex;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(savePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Save as PNG
            var pngBytes = tex.EncodeToPNG();
            File.WriteAllBytes(savePath, pngBytes);
            AssetDatabase.ImportAsset(savePath, ImportAssetOptions.Default);

            // Set texture import settings
            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.GUI;
                importer.maxTextureSize = 256;
                importer.textureCompression = TextureImporterCompression.CompressedLQ;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            // Load back as a persistent asset reference
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
            return loaded != null ? loaded : tex;
        }

        static string GetPreviewPath(GameObject prefab)
        {
            var prefabPath = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(prefabPath)) return null;

            var dir = Path.GetDirectoryName(prefabPath);
            var previewDir = Path.Combine(dir, "Previews").Replace('\\', '/');
            var fileName = Path.GetFileNameWithoutExtension(prefabPath) + "_preview.png";

            return Path.Combine(previewDir, fileName).Replace('\\', '/');
        }

        // ==================== UTILS ====================

        static Texture2D ResizeTexture(Texture2D source, int width, int height)
        {
            var rt = RenderTexture.GetTemporary(width, height);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            Graphics.Blit(source, rt);
            var result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Object.DestroyImmediate(source);

            return result;
        }
    }
}
