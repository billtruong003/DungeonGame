using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StylizedTextureBaker
{
    public class MeshAnalyzer
    {
        public float[] VertexCurvature { get; private set; }
        public float[] VertexEdgeMask { get; private set; }
        public Vector3[] PrincipalDirections { get; private set; }

        private readonly Vector3[] _vertices;
        private readonly Vector3[] _normals;
        private readonly int[] _triangles;
        private Dictionary<int, List<int>> _adjacency;
        private Dictionary<long, List<int>> _edgeToTriangles;

        public MeshAnalyzer(Mesh mesh)
        {
            _vertices = mesh.vertices;
            _normals = mesh.normals;
            _triangles = mesh.triangles;
        }

        public void Analyze(BakeSettings settings)
        {
            BuildAdjacency();
            BuildEdgeToTriangleMap();
            ComputeCurvature(settings.curvatureSmoothIterations, settings.curvatureScale);
            NormalizeCurvature(settings.curvatureNormalizationPercentile);
            ComputeEdgeMask(settings.DihedralSoftRadians, settings.DihedralHardRadians);
            ComputePrincipalDirections();
        }

        private void BuildAdjacency()
        {
            _adjacency = new Dictionary<int, List<int>>(_vertices.Length);

            for (int i = 0; i < _vertices.Length; i++)
                _adjacency[i] = new List<int>(6);

            for (int i = 0; i < _triangles.Length; i += 3)
            {
                int a = _triangles[i];
                int b = _triangles[i + 1];
                int c = _triangles[i + 2];

                AddAdjacent(a, b);
                AddAdjacent(a, c);
                AddAdjacent(b, c);
            }
        }

        private void AddAdjacent(int v0, int v1)
        {
            if (!_adjacency[v0].Contains(v1)) _adjacency[v0].Add(v1);
            if (!_adjacency[v1].Contains(v0)) _adjacency[v1].Add(v0);
        }

        private void BuildEdgeToTriangleMap()
        {
            _edgeToTriangles = new Dictionary<long, List<int>>(_triangles.Length);

            for (int i = 0; i < _triangles.Length; i += 3)
            {
                int triIndex = i / 3;
                RegisterEdgeTriangle(_triangles[i], _triangles[i + 1], triIndex);
                RegisterEdgeTriangle(_triangles[i + 1], _triangles[i + 2], triIndex);
                RegisterEdgeTriangle(_triangles[i + 2], _triangles[i], triIndex);
            }
        }

        private void RegisterEdgeTriangle(int v0, int v1, int triIndex)
        {
            long key = PackEdgeKey(v0, v1);
            if (!_edgeToTriangles.ContainsKey(key))
                _edgeToTriangles[key] = new List<int>(2);
            _edgeToTriangles[key].Add(triIndex);
        }

        private static long PackEdgeKey(int v0, int v1)
        {
            int lo = v0 < v1 ? v0 : v1;
            int hi = v0 < v1 ? v1 : v0;
            return ((long)lo << 32) | (uint)hi;
        }

        private void ComputeCurvature(int smoothIterations, float scale)
        {
            int count = _vertices.Length;
            VertexCurvature = new float[count];

            for (int i = 0; i < count; i++)
            {
                var neighbors = _adjacency[i];
                if (neighbors.Count < 2)
                    continue;

                VertexCurvature[i] = ComputeMeanCurvatureAtVertex(i, neighbors) * scale;
            }

            for (int iter = 0; iter < smoothIterations; iter++)
                SmoothCurvaturePass();
        }

        private float ComputeMeanCurvatureAtVertex(int vi, List<int> neighbors)
        {
            Vector3 pos = _vertices[vi];
            Vector3 nrm = _normals[vi];
            Vector3 laplacian = Vector3.zero;
            float totalWeight = 0f;

            for (int n = 0; n < neighbors.Count; n++)
            {
                int vj = neighbors[n];
                Vector3 edge = _vertices[vj] - pos;

                if (edge.sqrMagnitude < 1e-12f)
                    continue;

                float w = ComputeRobustCotangentWeight(vi, vj);
                laplacian += w * edge;
                totalWeight += w;
            }

            if (totalWeight < 1e-8f)
                return 0f;

            laplacian /= totalWeight;
            return Vector3.Dot(laplacian, nrm);
        }

        private float ComputeRobustCotangentWeight(int vi, int vj)
        {
            long key = PackEdgeKey(vi, vj);

            if (!_edgeToTriangles.TryGetValue(key, out var tris))
                return UniformWeight(vi, vj);

            float weight = 0f;
            bool anyValid = false;

            foreach (int triIndex in tris)
            {
                int opposite = FindOppositeVertex(triIndex, vi, vj);
                if (opposite < 0) continue;

                Vector3 toVi = _vertices[vi] - _vertices[opposite];
                Vector3 toVj = _vertices[vj] - _vertices[opposite];

                float cross = Vector3.Cross(toVi, toVj).magnitude;
                float dot = Vector3.Dot(toVi, toVj);

                if (cross < 1e-8f)
                    continue;

                float cotAlpha = dot / cross;
                cotAlpha = Mathf.Clamp(cotAlpha, -10f, 10f);
                weight += cotAlpha;
                anyValid = true;
            }

            if (!anyValid)
                return UniformWeight(vi, vj);

            weight *= 0.5f;

            if (weight < 0.001f)
                return Mathf.Max(weight, 0.001f);

            return weight;
        }

        private float UniformWeight(int vi, int vj)
        {
            float dist = (_vertices[vi] - _vertices[vj]).magnitude;
            return dist > 1e-8f ? 1f / dist : 1f;
        }

        private int FindOppositeVertex(int triIndex, int v0, int v1)
        {
            int a = _triangles[triIndex * 3];
            int b = _triangles[triIndex * 3 + 1];
            int c = _triangles[triIndex * 3 + 2];

            if (a != v0 && a != v1) return a;
            if (b != v0 && b != v1) return b;
            if (c != v0 && c != v1) return c;
            return -1;
        }

        private void SmoothCurvaturePass()
        {
            float[] smoothed = new float[VertexCurvature.Length];

            for (int i = 0; i < _vertices.Length; i++)
            {
                var neighbors = _adjacency[i];
                if (neighbors.Count == 0)
                {
                    smoothed[i] = VertexCurvature[i];
                    continue;
                }

                float weightedSum = VertexCurvature[i] * 2f;
                float totalWeight = 2f;

                for (int n = 0; n < neighbors.Count; n++)
                {
                    float dist = (_vertices[neighbors[n]] - _vertices[i]).magnitude;
                    float w = dist > 1e-8f ? 1f / dist : 1f;
                    weightedSum += VertexCurvature[neighbors[n]] * w;
                    totalWeight += w;
                }

                smoothed[i] = weightedSum / totalWeight;
            }

            VertexCurvature = smoothed;
        }

        private void NormalizeCurvature(float percentile)
        {
            if (VertexCurvature.Length == 0)
                return;

            float[] absValues = new float[VertexCurvature.Length];
            for (int i = 0; i < VertexCurvature.Length; i++)
                absValues[i] = Mathf.Abs(VertexCurvature[i]);

            System.Array.Sort(absValues);

            int percentileIndex = Mathf.Clamp(
                Mathf.FloorToInt(percentile * (absValues.Length - 1)),
                0,
                absValues.Length - 1);

            float maxVal = absValues[percentileIndex];

            if (maxVal < 1e-8f)
                maxVal = absValues[absValues.Length - 1];

            if (maxVal < 1e-8f)
                return;

            float invMax = 1f / maxVal;
            for (int i = 0; i < VertexCurvature.Length; i++)
                VertexCurvature[i] = Mathf.Clamp(VertexCurvature[i] * invMax, -1f, 1f);
        }

        private void ComputeEdgeMask(float softRadians, float hardRadians)
        {
            int count = _vertices.Length;
            var vertexMaxEdge = new float[count];

            foreach (var kvp in _edgeToTriangles)
            {
                var tris = kvp.Value;

                if (tris.Count < 2)
                {
                    MarkBoundaryEdge(kvp.Key, vertexMaxEdge, 1f);
                    continue;
                }

                float maxAngle = 0f;
                for (int i = 0; i < tris.Count - 1; i++)
                {
                    for (int j = i + 1; j < tris.Count; j++)
                    {
                        Vector3 n0 = ComputeTriangleNormal(tris[i]);
                        Vector3 n1 = ComputeTriangleNormal(tris[j]);

                        float dot = Mathf.Clamp(Vector3.Dot(n0, n1), -1f, 1f);
                        float angle = Mathf.Acos(dot);
                        maxAngle = Mathf.Max(maxAngle, angle);
                    }
                }

                float edgeStrength = Mathf.InverseLerp(softRadians, hardRadians, maxAngle);

                int v0 = (int)(kvp.Key >> 32);
                int v1 = (int)(kvp.Key & 0xFFFFFFFF);

                vertexMaxEdge[v0] = Mathf.Max(vertexMaxEdge[v0], edgeStrength);
                vertexMaxEdge[v1] = Mathf.Max(vertexMaxEdge[v1], edgeStrength);
            }

            VertexEdgeMask = vertexMaxEdge;
        }

        private void MarkBoundaryEdge(long edgeKey, float[] vertexEdge, float strength)
        {
            int v0 = (int)(edgeKey >> 32);
            int v1 = (int)(edgeKey & 0xFFFFFFFF);
            vertexEdge[v0] = Mathf.Max(vertexEdge[v0], strength);
            vertexEdge[v1] = Mathf.Max(vertexEdge[v1], strength);
        }

        private Vector3 ComputeTriangleNormal(int triIndex)
        {
            Vector3 a = _vertices[_triangles[triIndex * 3]];
            Vector3 b = _vertices[_triangles[triIndex * 3 + 1]];
            Vector3 c = _vertices[_triangles[triIndex * 3 + 2]];

            Vector3 cross = Vector3.Cross(b - a, c - a);
            float mag = cross.magnitude;

            return mag > 1e-10f ? cross / mag : Vector3.up;
        }

        private void ComputePrincipalDirections()
        {
            int count = _vertices.Length;
            PrincipalDirections = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                var neighbors = _adjacency[i];
                PrincipalDirections[i] = neighbors.Count >= 2
                    ? EstimatePrincipalDirection(i, neighbors)
                    : Vector3.right;
            }
        }

        private Vector3 EstimatePrincipalDirection(int vi, List<int> neighbors)
        {
            Vector3 pos = _vertices[vi];
            Vector3 nrm = _normals[vi];

            BuildTangentFrame(nrm, out Vector3 t1, out Vector3 t2);

            float sumUU = 0, sumUV = 0, sumVV = 0;
            float sumUK = 0, sumVK = 0;

            foreach (int vj in neighbors)
            {
                Vector3 edge = _vertices[vj] - pos;
                float edgeLen = edge.magnitude;
                if (edgeLen < 1e-8f) continue;

                float u = Vector3.Dot(edge, t1);
                float v = Vector3.Dot(edge, t2);

                Vector3 normalDelta = _normals[vj] - nrm;
                float kappa = -2f * Vector3.Dot(normalDelta, edge) / (edgeLen * edgeLen);

                float w = 1f / edgeLen;
                sumUU += w * u * u;
                sumUV += w * u * v;
                sumVV += w * v * v;
                sumUK += w * u * kappa;
                sumVK += w * v * kappa;
            }

            float det = sumUU * sumVV - sumUV * sumUV;
            if (Mathf.Abs(det) < 1e-10f)
                return t1;

            float kU = (sumVV * sumUK - sumUV * sumVK) / det;
            float kV = (sumUU * sumVK - sumUV * sumUK) / det;

            Vector3 dir = (t1 * kU + t2 * kV);
            return dir.sqrMagnitude > 0.001f ? dir.normalized : t1;
        }

        private static void BuildTangentFrame(Vector3 normal, out Vector3 tangent1, out Vector3 tangent2)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.99f
                ? Vector3.up
                : Vector3.right;

            tangent1 = Vector3.Cross(normal, up).normalized;
            tangent2 = Vector3.Cross(normal, tangent1).normalized;
        }
    }
}
