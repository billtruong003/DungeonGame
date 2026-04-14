using UnityEngine;
using TMPro;

namespace RPGModular
{
    /// <summary>
    /// Attach to UI Text/TMP. Auto-refresh when language changes.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string localizationKey;

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Refresh();
            LocalizationService.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            if (LocalizationService.Instance != null)
                LocalizationService.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        public void SetKey(string key)
        {
            localizationKey = key;
            Refresh();
        }

        private void OnLanguageChanged(string _) => Refresh();

        private void Refresh()
        {
            if (_text != null && !string.IsNullOrEmpty(localizationKey))
                _text.text = Loc.Get(localizationKey);
        }
    }
}
