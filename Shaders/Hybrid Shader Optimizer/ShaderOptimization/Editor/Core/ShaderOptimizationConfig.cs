using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ShaderOptimization.Core
{
    public class ShaderOptimizationConfig : ScriptableObject
    {
        public bool EnableAutoStripping;
        public List<string> ShadersToStrip = new List<string>();
        public List<string> VariantsToStrip = new List<string>();

        private static ShaderOptimizationConfig instance;
        public static ShaderOptimizationConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    var guids = AssetDatabase.FindAssets("t:ShaderOptimizationConfig");
                    if (guids.Length > 0)
                    {
                        instance = AssetDatabase.LoadAssetAtPath<ShaderOptimizationConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                    else
                    {
                        instance = CreateInstance<ShaderOptimizationConfig>();
                        if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                        {
                            AssetDatabase.CreateFolder("Assets", "Editor");
                        }
                        AssetDatabase.CreateAsset(instance, "Assets/Editor/ShaderOptimizationConfig.asset");
                        AssetDatabase.SaveAssets();
                    }
                }
                return instance;
            }
        }

        public void SaveConfig()
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}