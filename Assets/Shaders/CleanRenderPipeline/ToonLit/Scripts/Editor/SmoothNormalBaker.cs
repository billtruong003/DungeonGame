using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CleanRender
{
    public static class SmoothNormalBaker
    {
        [MenuItem("Tools/CleanRender/Bake Smooth Normals to Tangent")]
        static void BakeSelected()
        {
            MeshFilter[] filters = Selection.GetFiltered<MeshFilter>(SelectionMode.Deep);
            SkinnedMeshRenderer[] skinned = Selection.GetFiltered<SkinnedMeshRenderer>(SelectionMode.Deep);

            int count = 0;

            foreach (MeshFilter mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                Mesh baked = Bake(mf.sharedMesh);
                SaveBakedMesh(baked, mf.sharedMesh.name);
                mf.sharedMesh = baked;
                count++;
            }

            foreach (SkinnedMeshRenderer smr in skinned)
            {
                if (smr.sharedMesh == null) continue;
                Mesh baked = Bake(smr.sharedMesh);
                SaveBakedMesh(baked, smr.sharedMesh.name);
                smr.sharedMesh = baked;
                count++;
            }

            if (count > 0)
                Debug.Log(string.Format("[SmoothNormalBaker] Baked {0} mesh(es).", count));
            else
                Debug.LogWarning("[SmoothNormalBaker] No meshes found in selection.");
        }

        [MenuItem("Tools/CleanRender/Bake Smooth Normals to Tangent", true)]
        static bool BakeSelectedValidate()
        {
            return Selection.activeGameObject != null;
        }

        public static Mesh Bake(Mesh source)
        {
            Mesh mesh = Object.Instantiate(source);
            mesh.name = source.name + "_SmoothedTangent";

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int vertCount = vertices.Length;

            Vector3[] smoothNormals = new Vector3[vertCount];
            Dictionary<Vector3, List<int>> vertexBucket = new Dictionary<Vector3, List<int>>(vertCount);

            for (int i = 0; i < vertCount; i++)
            {
                Vector3 key = RoundVertex(vertices[i]);
                if (!vertexBucket.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>(4);
                    vertexBucket[key] = list;
                }
                list.Add(i);
            }

            foreach (KeyValuePair<Vector3, List<int>> kvp in vertexBucket)
            {
                List<int> indices = kvp.Value;
                Vector3 avg = Vector3.zero;
                foreach (int idx in indices)
                    avg += normals[idx];
                avg.Normalize();

                foreach (int idx in indices)
                    smoothNormals[idx] = avg;
            }

            Vector4[] tangents = new Vector4[vertCount];
            for (int i = 0; i < vertCount; i++)
            {
                Vector3 sn = smoothNormals[i];
                tangents[i] = new Vector4(sn.x, sn.y, sn.z, 0f);
            }

            mesh.tangents = tangents;
            return mesh;
        }

        static Vector3 RoundVertex(Vector3 v)
        {
            const float precision = 10000f;
            return new Vector3(
                Mathf.Round(v.x * precision) / precision,
                Mathf.Round(v.y * precision) / precision,
                Mathf.Round(v.z * precision) / precision
            );
        }

        static void SaveBakedMesh(Mesh mesh, string originalName)
        {
            string folder = "Assets/Meshes/BakedSmoothNormals";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Meshes"))
                    AssetDatabase.CreateFolder("Assets", "Meshes");
                AssetDatabase.CreateFolder("Assets/Meshes", "BakedSmoothNormals");
            }

            string path = string.Format("{0}/{1}_SmoothedTangent.asset", folder, originalName);
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            Debug.Log(string.Format("[SmoothNormalBaker] Saved: {0}", path));
        }
    }
}