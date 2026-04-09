using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace BillGameCore
{
    public class SceneService : ISceneService, IInitializable, IDisposableService
    {
        private bool _loading;
        private TransitionOverlay _overlay;

        public string CurrentSceneName => SceneManager.GetActiveScene().name;
        public int CurrentBuildIndex => SceneManager.GetActiveScene().buildIndex;
        public bool IsLoading => _loading;

        public void Initialize() => _overlay = new TransitionOverlay();

        public void Load(string name) => LoadImpl(name, -1, LoadSceneMode.Single, TransitionType.None, 0f);
        public void Load(string name, TransitionType t, float d = 0.5f) => LoadImpl(name, -1, LoadSceneMode.Single, t, d);
        public void Load(int idx) => LoadImpl(null, idx, LoadSceneMode.Single, TransitionType.None, 0f);
        public void LoadAdditive(string name) => LoadImpl(name, -1, LoadSceneMode.Additive, TransitionType.None, 0f);

        public void Unload(string name)
        {
            var s = SceneManager.GetSceneByName(name);
            if (s.isLoaded) SceneManager.UnloadSceneAsync(s);
        }

        public void Reload() => Load(CurrentBuildIndex);
        public void LoadNext() { int n = CurrentBuildIndex + 1; if (n < SceneManager.sceneCountInBuildSettings) Load(n); }
        public void LoadPrevious() { int p = CurrentBuildIndex - 1; if (p >= 0) Load(p); }

        public void LoadAsync(string name, Action<float> onProgress = null, Action onComplete = null)
            => CoroutineRunner.Run(AsyncRoutine(name, onProgress, onComplete));

        private void LoadImpl(string name, int idx, LoadSceneMode mode, TransitionType trans, float dur)
        {
            if (_loading) return;
            CoroutineRunner.Run(LoadRoutine(name, idx, mode, trans, dur));
        }

        private IEnumerator LoadRoutine(string name, int idx, LoadSceneMode mode, TransitionType trans, float dur)
        {
            _loading = true;
            string sceneName = name ?? $"BuildIndex:{idx}";
            Bill.Events?.Fire(new SceneLoadStartEvent { SceneName = sceneName });

            // Transition in
            if (trans == TransitionType.Fade && dur > 0f)
            {
                _overlay.Show();
                yield return CoroutineRunner.Instance.StartCoroutine(_overlay.FadeIn(dur * 0.5f));
            }

            // Load scene
            AsyncOperation op = !string.IsNullOrEmpty(name)
                ? SceneManager.LoadSceneAsync(name, mode)
                : SceneManager.LoadSceneAsync(idx, mode);

            if (op == null) { Debug.LogError($"[Bill.Scene] Failed to load: {sceneName}"); _loading = false; yield break; }
            while (!op.isDone) yield return null;

            // Transition out
            if (trans == TransitionType.Fade && dur > 0f)
            {
                yield return CoroutineRunner.Instance.StartCoroutine(_overlay.FadeOut(dur * 0.5f));
                _overlay.Hide();
            }

            _loading = false;
            Bill.Events?.Fire(new SceneLoadCompleteEvent { SceneName = SceneManager.GetActiveScene().name });
        }

        private IEnumerator AsyncRoutine(string name, Action<float> onProgress, Action onComplete)
        {
            _loading = true;
            var op = SceneManager.LoadSceneAsync(name);
            if (op == null) { _loading = false; yield break; }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f) { onProgress?.Invoke(op.progress / 0.9f); yield return null; }
            onProgress?.Invoke(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            _loading = false;
            onComplete?.Invoke();
        }

        public void Cleanup() { _loading = false; _overlay?.Destroy(); }
    }

    /// <summary>
    /// Full-screen fade overlay using UI Toolkit. Created entirely via code.
    /// </summary>
    internal class TransitionOverlay
    {
        private UIDocument _doc;
        private VisualElement _panel;

        public TransitionOverlay()
        {
            var go = new GameObject("[Bill.Transition]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _doc = go.AddComponent<UIDocument>();
            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ConstantPixelSize;
            ps.sortingOrder = 9990;
            _doc.panelSettings = ps;

            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.left = _panel.style.right = _panel.style.top = _panel.style.bottom = 0;
            _panel.style.backgroundColor = Color.black;
            _panel.style.opacity = 0f;
            _panel.style.display = DisplayStyle.None;
            _panel.pickingMode = PickingMode.Ignore;
            _doc.rootVisualElement.Add(_panel);
        }

        public void Show() => _panel.style.display = DisplayStyle.Flex;
        public void Hide() => _panel.style.display = DisplayStyle.None;

        public IEnumerator FadeIn(float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _panel.style.opacity = Mathf.Clamp01(t / duration);
                yield return null;
            }
            _panel.style.opacity = 1f;
        }

        public IEnumerator FadeOut(float duration)
        {
            float t = 0;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _panel.style.opacity = 1f - Mathf.Clamp01(t / duration);
                yield return null;
            }
            _panel.style.opacity = 0f;
        }

        public void Destroy() { if (_doc != null) UnityEngine.Object.Destroy(_doc.gameObject); }
    }
}
