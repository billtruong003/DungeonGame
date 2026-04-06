using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShaderOptimization.Models
{
    [Serializable]
    public class ShaderAnalysisResult
    {
        public Shader Shader;
        public string Name;
        public string Path;
        public bool InBuildScenes;
        public bool InDynamicRoots;
        public bool IsAlwaysIncluded;
        public long FileSize;
        public bool IsExpanded;
        public List<string> ReferencingMaterials = new List<string>();

        public bool IsUsed => InBuildScenes || InDynamicRoots || IsAlwaysIncluded;
        public bool IsUsedOnlyOutsideScenes => (InDynamicRoots || IsAlwaysIncluded) && !InBuildScenes;
        public bool IsUnused => !IsUsed;
        public bool IsBuiltIn => Path.StartsWith("Resources/") || Path.StartsWith("Library/");
    }
}