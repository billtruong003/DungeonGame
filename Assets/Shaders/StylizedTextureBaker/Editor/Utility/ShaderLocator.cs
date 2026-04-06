using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    public static class ShaderLocator
    {
        private static readonly Dictionary<string, ComputeShader> Cache = new Dictionary<string, ComputeShader>();

        public static ComputeShader Find(string name)
        {
            if (Cache.TryGetValue(name, out var cached) && cached != null)
                return cached;

            string[] guids = AssetDatabase.FindAssets($"{name} t:ComputeShader");
            if (guids.Length == 0)
            {
                Debug.LogError($"[StylizedBaker] Compute shader '{name}' not found in project.");
                return null;
            }

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            Cache[name] = shader;
            return shader;
        }

        public static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
                Debug.LogError($"[StylizedBaker] Shader '{name}' not found.");
            return shader;
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }
    }
}
