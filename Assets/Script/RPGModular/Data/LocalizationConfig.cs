using System;
using UnityEngine;
using TMPro;
using BillInspector;

namespace RPGModular
{
    [CreateAssetMenu(menuName = "Game/Localization Config")]
    [BillTitle("Localization Config", "Multi-language configuration")]
    public class LocalizationConfig : ScriptableObject
    {
        [BillTableList]
        public LanguageEntry[] supportedLanguages = new LanguageEntry[]
        {
            new LanguageEntry { code = "vi", displayName = "Tiếng Việt" },
            new LanguageEntry { code = "en", displayName = "English" },
        };

        public string defaultLanguage = "vi";
        public string fallbackLanguage = "en";
    }

    [Serializable]
    public class LanguageEntry
    {
        public string code;
        public string displayName;
        public TMP_FontAsset font;
        public bool isRTL;
    }
}
