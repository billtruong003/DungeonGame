using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace EditorTools
{
    [CreateAssetMenu(fileName = "TextureImportRuleset", menuName = "Tools/Texture Import Ruleset", order = 200)]
    public class TextureImportRuleset : ScriptableObject
    {
        [System.Serializable]
        public class TextureTypeRule
        {
            public TextureImporterType TextureType = TextureImporterType.Default;
            public int MaxSize = 2048;
            public bool MipMaps = true;
            public TextureImporterCompression Compression = TextureImporterCompression.Compressed;
            public bool Crunch = true;
            [Range(0, 100)] public int CrunchQuality = 50;
            public FilterMode FilterMode = FilterMode.Bilinear;
            public TextureImporterFormat AndroidASTCFormat = TextureImporterFormat.ASTC_6x6;
            public int AndroidMaxSize = 0;
            public int EffectiveAndroidMaxSize => AndroidMaxSize > 0 ? AndroidMaxSize : MaxSize;
        }

        [System.Serializable]
        public class FolderOverride
        {
            public string PathPrefix = "";
            public int MaxSize = 512;
            public TextureImporterFormat AndroidASTCFormat = TextureImporterFormat.Automatic;
        }

        [Header("Global")]
        public bool OverrideAndroid = true;
        public bool AutoFitSize = true;
        public string HighResLabel = "HighRes";
        public int HighResMaxSize = 4096;
        public bool SkipEditorFolders = true;

        [Header("Per-Type Rules")]
        public List<TextureTypeRule> Rules = new List<TextureTypeRule>
        {
            new TextureTypeRule
            {
                TextureType = TextureImporterType.Default,
                MaxSize = 2048, MipMaps = true,
                Compression = TextureImporterCompression.Compressed,
                Crunch = true, CrunchQuality = 50,
                AndroidASTCFormat = TextureImporterFormat.ASTC_6x6,
            },
            new TextureTypeRule
            {
                TextureType = TextureImporterType.NormalMap,
                MaxSize = 2048, MipMaps = true,
                Compression = TextureImporterCompression.Compressed,
                Crunch = false, CrunchQuality = 50,
                AndroidASTCFormat = TextureImporterFormat.ASTC_8x8,
            },
            new TextureTypeRule
            {
                TextureType = TextureImporterType.Sprite,
                MaxSize = 2048, MipMaps = false,
                Compression = TextureImporterCompression.Compressed,
                Crunch = true, CrunchQuality = 50,
                AndroidASTCFormat = TextureImporterFormat.ASTC_6x6,
            },
            new TextureTypeRule
            {
                TextureType = TextureImporterType.GUI,
                MaxSize = 1024, MipMaps = false,
                Compression = TextureImporterCompression.Compressed,
                Crunch = true, CrunchQuality = 50,
                AndroidASTCFormat = TextureImporterFormat.ASTC_6x6,
            },
            new TextureTypeRule
            {
                TextureType = TextureImporterType.Lightmap,
                MaxSize = 2048, MipMaps = true,
                Compression = TextureImporterCompression.Compressed,
                Crunch = false, CrunchQuality = 50,
                AndroidASTCFormat = TextureImporterFormat.ASTC_6x6,
            },
            new TextureTypeRule
            {
                TextureType = TextureImporterType.SingleChannel,
                MaxSize = 2048, MipMaps = true,
                Compression = TextureImporterCompression.Compressed,
                Crunch = true, CrunchQuality = 50,
                AndroidASTCFormat = TextureImporterFormat.ASTC_8x8,
            },
        };

        [Header("Folder Overrides")]
        public List<FolderOverride> FolderOverrides = new List<FolderOverride>();

        public TextureTypeRule GetRuleForType(TextureImporterType type)
        {
            foreach (var r in Rules)
                if (r.TextureType == type) return r;
            return Rules.Count > 0 ? Rules[0] : new TextureTypeRule();
        }

        public FolderOverride GetFolderOverride(string assetPath)
        {
            FolderOverride best = null;
            int bestLen = 0;
            foreach (var fo in FolderOverrides)
            {
                if (string.IsNullOrEmpty(fo.PathPrefix)) continue;
                if (assetPath.StartsWith(fo.PathPrefix) && fo.PathPrefix.Length > bestLen)
                { best = fo; bestLen = fo.PathPrefix.Length; }
            }
            return best;
        }

        private static TextureImportRuleset _cached;
        public static TextureImportRuleset FindInProject()
        {
            if (_cached != null) return _cached;
            string[] guids = AssetDatabase.FindAssets("t:TextureImportRuleset");
            if (guids.Length > 0)
                _cached = AssetDatabase.LoadAssetAtPath<TextureImportRuleset>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
            return _cached;
        }
        public static void ClearCache() { _cached = null; }
    }
}