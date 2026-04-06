using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShaderOptimization.Core
{
    public class ShaderBuildProcessor : IPreprocessShaders
    {
        public int callbackOrder => 0;

        private HashSet<string> strippedVariantsCache;
        private HashSet<string> strippedShadersCache;
        private int lastConfigHash;

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            var config = ShaderOptimizationConfig.Instance;
            if (config == null || !config.EnableAutoStripping) return;

            int currentHash = GetConfigHash(config);
            if (strippedVariantsCache == null || lastConfigHash != currentHash)
            {
                strippedVariantsCache = new HashSet<string>(config.VariantsToStrip);
                strippedShadersCache = new HashSet<string>(config.ShadersToStrip);
                lastConfigHash = currentHash;
            }

            if (strippedShadersCache.Contains(shader.name))
            {
                data.Clear();
                return;
            }

            if (strippedVariantsCache.Count > 0)
            {
                for (int i = data.Count - 1; i >= 0; --i)
                {
                    var keywords = data[i].shaderKeywordSet.GetShaderKeywords();
                    bool shouldStrip = false;

                    foreach (var kw in keywords)
                    {
                        if (strippedVariantsCache.Contains(GetKeywordString(shader, kw)))
                        {
                            shouldStrip = true;
                            break;
                        }
                    }

                    if (shouldStrip)
                    {
                        data.RemoveAt(i);
                    }
                }
            }
        }

        private int GetConfigHash(ShaderOptimizationConfig config)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + config.VariantsToStrip.Count;
                hash = hash * 31 + config.ShadersToStrip.Count;
                
                if (config.VariantsToStrip.Count > 0)
                    hash = hash * 31 + config.VariantsToStrip[0].GetHashCode();
                    
                if (config.ShadersToStrip.Count > 0)
                    hash = hash * 31 + config.ShadersToStrip[0].GetHashCode();

                return hash;
            }
        }

        private static string GetKeywordString(Shader shader, ShaderKeyword kw)
        {
#if UNITY_2021_2_OR_NEWER
            return kw.name;
#else
            return ShaderKeyword.GetKeywordName(shader, kw);
#endif
        }
    }
}