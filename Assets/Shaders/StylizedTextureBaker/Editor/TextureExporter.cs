using System.IO;
using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    public class TextureExporter
    {
        public string Export(RenderTexture source, string filePath, ExportFormat format)
        {
            if (source == null) return null;

            bool isHdr = format == ExportFormat.EXR;
            TextureFormat texFormat = isHdr ? TextureFormat.RGBAFloat : TextureFormat.RGBA32;
            var texture = TextureUtility.RTToTexture2D(source, texFormat);

            byte[] bytes;
            string extension;

            switch (format)
            {
                case ExportFormat.TGA:
                    bytes = texture.EncodeToTGA();
                    extension = ".tga";
                    break;
                case ExportFormat.EXR:
                    bytes = texture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP);
                    extension = ".exr";
                    break;
                default:
                    bytes = texture.EncodeToPNG();
                    extension = ".png";
                    break;
            }

            Object.DestroyImmediate(texture);

            string fullPath = filePath + extension;
            EnsureDirectoryExists(fullPath);
            File.WriteAllBytes(fullPath, bytes);

            return fullPath;
        }

        public void ExportBakeResult(BakeResult result, string folder, string baseName, BakeSettings settings)
        {
            string colorPath = Export(result.StylizedColor, $"{folder}/{baseName}_Color", settings.exportFormat);
            Debug.Log($"[StylizedBaker] Exported: {colorPath}");

            if (settings.exportOutlineMask && result.OutlineMask != null)
            {
                string maskPath = Export(result.OutlineMask, $"{folder}/{baseName}_OutlineMask", ExportFormat.PNG);
                Debug.Log($"[StylizedBaker] Exported: {maskPath}");
            }

            if (settings.exportCompositeEdge && result.CompositeEdge != null)
            {
                string edgePath = Export(result.CompositeEdge, $"{folder}/{baseName}_CompositeEdge", ExportFormat.PNG);
                Debug.Log($"[StylizedBaker] Exported: {edgePath}");
            }

            if (settings.exportDataMaps && result.DataMaps != null)
            {
                Export(result.DataMaps.CurvatureMap, $"{folder}/{baseName}_Curvature", ExportFormat.EXR);
                Export(result.DataMaps.NormalMap, $"{folder}/{baseName}_Normal", ExportFormat.EXR);
                Export(result.DataMaps.PositionMap, $"{folder}/{baseName}_Position", ExportFormat.EXR);
                Export(result.DataMaps.AOMap, $"{folder}/{baseName}_AO", ExportFormat.PNG);
                Export(result.DataMaps.EdgeMask, $"{folder}/{baseName}_EdgeMask", ExportFormat.PNG);
                Debug.Log($"[StylizedBaker] Exported data maps to: {folder}");
            }

            AssetDatabase.Refresh();
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
