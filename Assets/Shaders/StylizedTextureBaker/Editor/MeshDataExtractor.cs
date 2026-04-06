using UnityEngine;
using UnityEngine.Rendering;

namespace StylizedTextureBaker
{
    public class MeshDataExtractor
    {
        private ComputeShader _aoBakeShader;
        private Material _rasterMaterial;

        public MeshDataMaps Extract(Mesh mesh, BakeSettings settings)
        {
            ValidateInput(mesh);
            LoadResources();

            int resolution = settings.ResolutionValue;
            var maps = new MeshDataMaps(resolution);

            var analyzer = new MeshAnalyzer(mesh);
            analyzer.Analyze(settings);

            var bakedMesh = BuildBakeMesh(mesh, analyzer);

            RasterizePass(bakedMesh, maps.CurvatureMap, 0, resolution);
            RasterizePass(bakedMesh, maps.NormalMap, 1, resolution);
            RasterizePass(bakedMesh, maps.PositionMap, 2, resolution);
            RasterizePass(bakedMesh, maps.EdgeMask, 3, resolution);
            RasterizePass(bakedMesh, maps.DirectionalField, 4, resolution);
            RasterizePass(bakedMesh, maps.UVIslandMask, 5, resolution);

            Object.DestroyImmediate(bakedMesh);

            BakeAO(maps, settings);

            return maps;
        }

        private void LoadResources()
        {
            if (_rasterMaterial == null)
            {
                var shader = Shader.Find("Hidden/StylizedBaker/UVSpaceRasterize");
                _rasterMaterial = new Material(shader);
            }

            if (_aoBakeShader == null)
                _aoBakeShader = ShaderLocator.Find("AOBake");
        }

        private static void ValidateInput(Mesh mesh)
        {
            if (mesh.uv == null || mesh.uv.Length == 0)
                throw new System.InvalidOperationException("[StylizedBaker] Mesh has no UV0. Unwrap it first.");

            if (mesh.normals == null || mesh.normals.Length == 0)
                mesh.RecalculateNormals();
        }

        private static Mesh BuildBakeMesh(Mesh source, MeshAnalyzer analyzer)
        {
            var baked = Object.Instantiate(source);
            baked.hideFlags = HideFlags.HideAndDontSave;

            var vertexCount = source.vertexCount;
            var colors = new Color[vertexCount];
            var uv2 = new Vector2[vertexCount];
            var uv3 = new Vector2[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                float curvPacked = analyzer.VertexCurvature[i] * 0.5f + 0.5f;
                float edgePacked = analyzer.VertexEdgeMask[i];
                colors[i] = new Color(curvPacked, edgePacked, 0f, 1f);

                Vector3 dir = analyzer.PrincipalDirections[i];
                uv2[i] = new Vector2(dir.x * 0.5f + 0.5f, dir.y * 0.5f + 0.5f);
                uv3[i] = new Vector2(dir.z * 0.5f + 0.5f, 0f);
            }

            baked.colors = colors;
            baked.uv2 = uv2;
            baked.uv3 = uv3;

            return baked;
        }

        private void RasterizePass(Mesh mesh, RenderTexture target, int passIndex, int resolution)
        {
            var cmd = new CommandBuffer();
            cmd.SetRenderTarget(target);
            cmd.ClearRenderTarget(true, true, Color.clear);
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            cmd.DrawMesh(mesh, Matrix4x4.identity, _rasterMaterial, 0, passIndex);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }

        private void BakeAO(MeshDataMaps maps, BakeSettings settings)
        {
            if (_aoBakeShader == null) return;

            int kernel = _aoBakeShader.FindKernel("BakeAO");
            int resolution = settings.ResolutionValue;

            _aoBakeShader.SetInt("_Resolution", resolution);
            _aoBakeShader.SetInt("_RayCount", settings.aoRayCount);
            _aoBakeShader.SetFloat("_Radius", settings.aoRadius);
            _aoBakeShader.SetFloat("_Intensity", settings.aoIntensity);
            _aoBakeShader.SetTexture(kernel, "_PositionMap", maps.PositionMap);
            _aoBakeShader.SetTexture(kernel, "_NormalMap", maps.NormalMap);
            _aoBakeShader.SetTexture(kernel, "_AOOut", maps.AOMap);

            TextureUtility.DispatchCompute(_aoBakeShader, kernel, resolution);
        }
    }
}
