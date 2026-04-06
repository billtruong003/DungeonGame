using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EditorTools
{
    public class TextureOptimizerWindow : EditorWindow
    {
        private enum TargetPreset { Original, Max4096, Max2048, Max1024, Max512, Max256 }

        private TargetPreset _targetPreset = TargetPreset.Max2048;
        private int _jpgQuality = 85;
        private bool _autoConvertOpaqueToJpg = true;
        private bool _stripUselessAlpha = true;
        private bool _applyColorDilation = true;
        private int _dilationPasses = 8;
        private bool _forcePOT;
        private Vector2 _scrollPos;
        private Vector2 _logScroll;
        private List<string> _selectedPaths = new List<string>();
        private List<ResultEntry> _results = new List<ResultEntry>();
        private long _totalSavedBytes;
        private bool _settingsFoldout = true;

        private static readonly string[] ValidExtensions = { ".png", ".jpg", ".jpeg" };

        [MenuItem("Tools/Texture Optimizer %&t")]
        public static void Open()
        {
            var w = GetWindow<TextureOptimizer>("Texture Optimizer");
            w.minSize = new Vector2(420, 500);
        }

        private void OnEnable()
        {
            LoadPrefs();
            RefreshSelection();
        }

        private void OnDisable() => SavePrefs();

        private void OnSelectionChange()
        {
            RefreshSelection();
            Repaint();
        }

        private void RefreshSelection()
        {
            _selectedPaths.Clear();
            foreach (var guid in Selection.assetGUIDs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ValidExtensions.Contains(ext))
                    _selectedPaths.Add(path);
            }
            _selectedPaths = _selectedPaths.Distinct().ToList();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawHeader();
            DrawSettings();
            DrawOptimizeButton();
            DrawResults();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            GUILayout.Space(8);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var countStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = _selectedPaths.Count > 0 ? new Color(0.2f, 0.75f, 0.3f) : new Color(0.85f, 0.25f, 0.25f) }
            };

            EditorGUILayout.LabelField(_selectedPaths.Count > 0
                ? $"{_selectedPaths.Count} texture(s) selected"
                : "Select textures in Project window", countStyle);

            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }

        private void DrawSettings()
        {
            _settingsFoldout = EditorGUILayout.Foldout(_settingsFoldout, "Settings", true, EditorStyles.foldoutHeader);
            if (!_settingsFoldout) return;

            EditorGUI.indentLevel++;

            _targetPreset = (TargetPreset)EditorGUILayout.EnumPopup("Max Size", _targetPreset);
            _jpgQuality = EditorGUILayout.IntSlider("JPG Quality", _jpgQuality, 1, 100);

            GUILayout.Space(6);
            _autoConvertOpaqueToJpg = EditorGUILayout.Toggle("Opaque PNG → JPG", _autoConvertOpaqueToJpg);
            _stripUselessAlpha = EditorGUILayout.Toggle("Strip Useless Alpha", _stripUselessAlpha);
            _forcePOT = EditorGUILayout.Toggle("Force Power of Two", _forcePOT);
            _applyColorDilation = EditorGUILayout.Toggle("Alpha Bleed (Sprite Fix)", _applyColorDilation);

            if (_applyColorDilation)
            {
                EditorGUI.indentLevel++;
                _dilationPasses = EditorGUILayout.IntSlider("Bleed Passes", _dilationPasses, 1, 32);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(6);
        }

        private void DrawOptimizeButton()
        {
            GUI.enabled = _selectedPaths.Count > 0;

            var btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 42
            };

            if (GUILayout.Button("Optimize", btnStyle))
                RunOptimization();

            GUI.enabled = true;
        }

        private void DrawResults()
        {
            if (_results.Count == 0) return;

            GUILayout.Space(12);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Results", EditorStyles.boldLabel);

            if (_totalSavedBytes > 0)
            {
                var savedStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.2f, 0.8f, 0.3f) } };
                EditorGUILayout.LabelField($"Total saved: {FormatBytes(_totalSavedBytes)}", savedStyle);
            }

            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                _results.Clear();
                _totalSavedBytes = 0;
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);
            _logScroll = EditorGUILayout.BeginScrollView(_logScroll, GUILayout.Height(Mathf.Min(_results.Count * 22 + 10, 250)));

            foreach (var r in _results)
            {
                Color c;
                switch (r.Status)
                {
                    case ResultStatus.Saved: c = new Color(0.2f, 0.8f, 0.3f); break;
                    case ResultStatus.Skipped: c = new Color(0.8f, 0.8f, 0.2f); break;
                    case ResultStatus.Rollback: c = new Color(0.6f, 0.6f, 0.6f); break;
                    default: c = new Color(0.8f, 0.3f, 0.3f); break;
                }

                var style = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = c }, fontSize = 11 };
                EditorGUILayout.LabelField(r.Summary, style);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void RunOptimization()
        {
            _results.Clear();
            _totalSavedBytes = 0;
            int maxPx = ResolveMaxSize();

            try
            {
                for (int i = 0; i < _selectedPaths.Count; i++)
                {
                    var path = _selectedPaths[i];
                    EditorUtility.DisplayProgressBar("Optimizing", Path.GetFileName(path), (float)(i + 1) / _selectedPaths.Count);
                    OptimizeFile(path, maxPx);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }
        }

        private void OptimizeFile(string path, int maxPx)
        {
            byte[] originalBytes = File.ReadAllBytes(path);
            long oldSize = originalBytes.Length;
            string fileName = Path.GetFileName(path);
            string ext = Path.GetExtension(path).ToLowerInvariant();
            bool isPng = ext == ".png";

            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(originalBytes))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                AddResult(fileName, oldSize, oldSize, ResultStatus.Error, "Failed to decode");
                return;
            }

            bool hasAlpha = false;
            if (isPng)
                hasAlpha = DetectAlpha(tex);

            if (_forcePOT && (!Mathf.IsPowerOfTwo(tex.width) || !Mathf.IsPowerOfTwo(tex.height)))
            {
                var potTex = Resize(tex, Mathf.ClosestPowerOfTwo(tex.width), Mathf.ClosestPowerOfTwo(tex.height));
                UnityEngine.Object.DestroyImmediate(tex);
                tex = potTex;
            }

            if (maxPx > 0 && (tex.width > maxPx || tex.height > maxPx))
            {
                float ratio = (float)tex.width / tex.height;
                int w, h;
                if (tex.width >= tex.height) { w = maxPx; h = Mathf.RoundToInt(maxPx / ratio); }
                else { h = maxPx; w = Mathf.RoundToInt(maxPx * ratio); }
                var resized = Resize(tex, w, h);
                UnityEngine.Object.DestroyImmediate(tex);
                tex = resized;
            }

            if (_applyColorDilation && isPng && hasAlpha)
                Dilate(tex, _dilationPasses);

            byte[] outputBytes;
            string targetPath = path;
            bool convertToJpg = isPng && _autoConvertOpaqueToJpg && !hasAlpha;

            if (convertToJpg)
            {
                outputBytes = tex.EncodeToJPG(_jpgQuality);
                targetPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".jpg");
            }
            else if (isPng)
            {
                if (_stripUselessAlpha && !hasAlpha)
                {
                    var rgb = new Texture2D(tex.width, tex.height, TextureFormat.RGB24, false);
                    rgb.SetPixels32(tex.GetPixels32());
                    rgb.Apply(false, false);
                    outputBytes = rgb.EncodeToPNG();
                    UnityEngine.Object.DestroyImmediate(rgb);
                }
                else
                {
                    outputBytes = tex.EncodeToPNG();
                }
            }
            else
            {
                outputBytes = tex.EncodeToJPG(_jpgQuality);
            }

            UnityEngine.Object.DestroyImmediate(tex);

            long newSize = outputBytes.Length;

            if (newSize >= oldSize && targetPath == path)
            {
                AddResult(fileName, oldSize, newSize, ResultStatus.Rollback, "Already optimal");
                return;
            }

            File.WriteAllBytes(targetPath, outputBytes);

            if (targetPath != path)
            {
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.DeleteAsset(path);
            }

            long saved = oldSize - newSize;
            _totalSavedBytes += saved;
            float pct = (float)saved / oldSize * 100f;
            AddResult(Path.GetFileName(targetPath), oldSize, newSize, ResultStatus.Saved, $"-{pct:F1}%");
        }

        private int ResolveMaxSize()
        {
            switch (_targetPreset)
            {
                case TargetPreset.Max256: return 256;
                case TargetPreset.Max512: return 512;
                case TargetPreset.Max1024: return 1024;
                case TargetPreset.Max2048: return 2048;
                case TargetPreset.Max4096: return 4096;
                default: return 0;
            }
        }

        private static bool DetectAlpha(Texture2D tex)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
                if (px[i].a < 255) return true;
            return false;
        }

        private static Texture2D Resize(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Bilinear;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(src, rt);
            var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            result.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private static readonly int[] Dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] Dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        private static void Dilate(Texture2D tex, int passes)
        {
            int w = tex.width, h = tex.height;
            var buf = tex.GetPixels32();
            var tmp = new Color32[buf.Length];

            for (int p = 0; p < passes; p++)
            {
                Array.Copy(buf, tmp, buf.Length);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        if (tmp[idx].a > 0) continue;

                        int r = 0, g = 0, b = 0, n = 0;
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + Dx[d], ny = y + Dy[d];
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            var nb = tmp[ny * w + nx];
                            if (nb.a == 0) continue;
                            r += nb.r; g += nb.g; b += nb.b; n++;
                        }

                        if (n > 0)
                            buf[idx] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 0);
                    }
                }
            }

            tex.SetPixels32(buf);
            tex.Apply(false, false);
        }

        private void AddResult(string name, long oldSize, long newSize, ResultStatus status, string note)
        {
            string summary = status == ResultStatus.Rollback
                ? $"  {name}  {FormatBytes(oldSize)} — {note}"
                : $"  {name}  {FormatBytes(oldSize)} → {FormatBytes(newSize)}  {note}";

            _results.Add(new ResultEntry { Summary = summary, Status = status });
        }

        private static string FormatBytes(long b)
        {
            string[] u = { "B", "KB", "MB" };
            double v = b; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.#} {u[i]}";
        }

        private void LoadPrefs()
        {
            _targetPreset = (TargetPreset)EditorPrefs.GetInt("TO_MaxSize", 3);
            _jpgQuality = EditorPrefs.GetInt("TO_Quality", 85);
            _autoConvertOpaqueToJpg = EditorPrefs.GetBool("TO_AutoJpg", true);
            _stripUselessAlpha = EditorPrefs.GetBool("TO_StripAlpha", true);
            _applyColorDilation = EditorPrefs.GetBool("TO_Dilate", true);
            _dilationPasses = EditorPrefs.GetInt("TO_DilatePasses", 8);
            _forcePOT = EditorPrefs.GetBool("TO_POT", false);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetInt("TO_MaxSize", (int)_targetPreset);
            EditorPrefs.SetInt("TO_Quality", _jpgQuality);
            EditorPrefs.SetBool("TO_AutoJpg", _autoConvertOpaqueToJpg);
            EditorPrefs.SetBool("TO_StripAlpha", _stripUselessAlpha);
            EditorPrefs.SetBool("TO_Dilate", _applyColorDilation);
            EditorPrefs.SetInt("TO_DilatePasses", _dilationPasses);
            EditorPrefs.SetBool("TO_POT", _forcePOT);
        }

        private enum ResultStatus { Saved, Skipped, Rollback, Error }

        private struct ResultEntry
        {
            public string Summary;
            public ResultStatus Status;
        }
    }
}