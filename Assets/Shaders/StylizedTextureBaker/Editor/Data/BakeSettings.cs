using UnityEngine;

namespace StylizedTextureBaker
{
    [CreateAssetMenu(fileName = "BakeSettings", menuName = "Stylized Baker/Bake Settings")]
    public class BakeSettings : ScriptableObject
    {
        [Header("Resolution")]
        public BakeResolution resolution = BakeResolution._1024;
        public BakeResolution previewResolution = BakeResolution._512;
        public bool useReducedPreview = true;

        [Header("Curvature")]
        [Range(0, 10)]
        public int curvatureSmoothIterations = 3;
        [Range(0.01f, 5f)]
        public float curvatureScale = 1f;
        [Range(0.8f, 1f)]
        public float curvatureNormalizationPercentile = 0.95f;

        [Header("Mesh Edge Detection (Dihedral Angle)")]
        [Range(5f, 90f)]
        public float dihedralAngleSoft = 20f;
        [Range(10f, 180f)]
        public float dihedralAngleHard = 60f;

        [Header("Curvature Edge Enhancement")]
        [Range(0f, 1f)]
        public float curvatureEdgeThreshold = 0.15f;
        [Range(0f, 1f)]
        public float curvatureEdgeWeight = 0.5f;

        [Header("Edge Compositing")]
        [Range(0f, 1f)]
        public float geometryEdgeWeight = 0.7f;
        [Range(0f, 1f)]
        public float textureEdgeWeight = 0.3f;
        [Range(0, 8)]
        public int edgeThickenPixels = 2;
        [Range(0f, 1f)]
        public float minimumEdgeStrength = 0.15f;
        [Range(0.01f, 3f)]
        public float edgeSoftness = 0.5f;

        [Header("Ambient Occlusion")]
        [Range(8, 128)]
        public int aoRayCount = 32;
        [Range(0.01f, 5f)]
        public float aoRadius = 0.5f;
        [Range(0f, 2f)]
        public float aoIntensity = 1f;

        [Header("UV Compositing")]
        [Range(1, 32)]
        public int paddingPixels = 8;
        [Range(0, 4)]
        public int seamBlendRadius = 2;

        [Header("Output")]
        public ExportFormat exportFormat = ExportFormat.PNG;
        public string outputFolder = "Assets/Textures/Baked";
        public bool exportOutlineMask = true;
        public bool exportCompositeEdge;
        public bool exportDataMaps;

        public int ResolutionValue => (int)resolution;
        public int PreviewResolutionValue => useReducedPreview ? (int)previewResolution : (int)resolution;
        public float DihedralSoftRadians => dihedralAngleSoft * Mathf.Deg2Rad;
        public float DihedralHardRadians => dihedralAngleHard * Mathf.Deg2Rad;

        public static string ValidateMesh(MeshFilter meshFilter)
        {
            if (meshFilter == null) return "No MeshFilter assigned.";
            if (meshFilter.sharedMesh == null) return "MeshFilter has no mesh.";

            var mesh = meshFilter.sharedMesh;
            if (mesh.uv == null || mesh.uv.Length == 0) return "Mesh has no UV0. Unwrap it first.";
            if (mesh.vertexCount < 3) return "Mesh has fewer than 3 vertices.";
            if (mesh.triangles.Length < 3) return "Mesh has no triangles.";

            return null;
        }

        public static string ValidateTexture(Texture2D texture)
        {
            if (texture == null) return "No source texture assigned.";
            if (!texture.isReadable) return "Source texture must have Read/Write enabled in Import Settings.";

            return null;
        }
    }
}
