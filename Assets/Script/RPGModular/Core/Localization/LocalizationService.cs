using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGModular
{
    /// <summary>
    /// Singleton localization service. Load JSON key-value files from Resources/Localization/.
    /// Fallback chain: currentLanguage → fallbackLanguage → raw key.
    /// </summary>
    public class LocalizationService
    {
        private static LocalizationService _instance;
        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null) _instance = new LocalizationService();
                return _instance;
            }
        }

        private Dictionary<string, string> _currentStrings = new Dictionary<string, string>();
        private Dictionary<string, string> _fallbackStrings = new Dictionary<string, string>();
        private string _currentLanguage = "vi";
        private string _fallbackLanguage = "en";
        private LocalizationConfig _config;

        public string CurrentLanguage => _currentLanguage;
        public event Action<string> OnLanguageChanged;

        private LocalizationService()
        {
            _config = Resources.Load<LocalizationConfig>("LocalizationConfig");
            if (_config != null)
            {
                _currentLanguage = _config.defaultLanguage;
                _fallbackLanguage = _config.fallbackLanguage;
            }

            LoadLanguage(_fallbackLanguage, _fallbackStrings);
            LoadLanguage(_currentLanguage, _currentStrings);
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";

            if (_currentStrings.TryGetValue(key, out string val))
                return val;

            if (_fallbackStrings.TryGetValue(key, out string fallback))
                return fallback;

            return key; // show raw key as last resort
        }

        public string Get(string key, params (string name, string value)[] args)
        {
            string text = Get(key);
            foreach (var (name, value) in args)
            {
                text = text.Replace($"{{{name}}}", value);
            }
            return text;
        }

        public void SetLanguage(string langCode)
        {
            if (langCode == _currentLanguage) return;

            _currentLanguage = langCode;
            _currentStrings.Clear();
            LoadLanguage(_currentLanguage, _currentStrings);
            OnLanguageChanged?.Invoke(_currentLanguage);
        }

        public LanguageEntry[] AvailableLanguages =>
            _config != null ? _config.supportedLanguages : Array.Empty<LanguageEntry>();

        private void LoadLanguage(string langCode, Dictionary<string, string> target)
        {
            var textAsset = Resources.Load<TextAsset>($"Localization/{langCode}");
            if (textAsset == null)
            {
                Debug.LogWarning($"[Loc] Language file not found: Localization/{langCode}");
                return;
            }

            var data = JsonUtility.FromJson<LocalizationFileData>("{\"entries\":" + textAsset.text + "}");
            if (data?.entries == null)
            {
                // Fallback: try flat parsing
                ParseFlatJson(textAsset.text, target);
                return;
            }

            foreach (var entry in data.entries)
                target[entry.key] = entry.value;
        }

        private void ParseFlatJson(string json, Dictionary<string, string> target)
        {
            // Simple flat JSON parser: {"key1":"value1","key2":"value2"}
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            bool inKey = false, inValue = false;
            bool escaped = false;
            string currentKey = "", currentValue = "";
            bool readingKey = true;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (escaped) { (readingKey ? ref currentKey : ref currentValue) += c; escaped = false; continue; }
                if (c == '\\') { escaped = true; continue; }

                if (c == '"')
                {
                    if (!inKey && !inValue)
                    {
                        if (readingKey) inKey = true;
                        else inValue = true;
                    }
                    else if (inKey) { inKey = false; }
                    else if (inValue) { inValue = false; }
                    continue;
                }

                if (inKey) { currentKey += c; continue; }
                if (inValue) { currentValue += c; continue; }

                if (c == ':') { readingKey = false; continue; }
                if (c == ',')
                {
                    if (!string.IsNullOrEmpty(currentKey))
                        target[currentKey] = currentValue;
                    currentKey = "";
                    currentValue = "";
                    readingKey = true;
                }
            }

            if (!string.IsNullOrEmpty(currentKey))
                target[currentKey] = currentValue;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() => _instance = null;

        [Serializable]
        private class LocalizationFileData
        {
            public LocalizationEntry[] entries;
        }

        [Serializable]
        private class LocalizationEntry
        {
            public string key;
            public string value;
        }
    }
}
