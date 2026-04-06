using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    public abstract class StyleModuleBase : IStyleModule
    {
        public abstract string DisplayName { get; }
        public abstract StyleType Type { get; }
        public bool Enabled { get; set; } = true;
        public int Order { get; set; }
        public StyleBlendMode BlendMode { get; set; } = StyleBlendMode.Normal;
        public float Opacity { get; set; } = 1f;

        private static Texture2D _fallbackWhite;
        private static Texture2D _fallbackBlack;

        public abstract void Execute(
            RenderTexture sourceTexture,
            MeshDataMaps dataMaps,
            EdgeFeatureData edgeData,
            RenderTexture outputLayer,
            int resolution
        );

        public abstract void DrawGUI();
        public abstract string Serialize();
        public abstract void Deserialize(string data);

        protected ComputeShader LoadComputeShader(string name)
        {
            return ShaderLocator.Find(name);
        }

        protected void DispatchFullscreen(ComputeShader shader, int kernel, int resolution)
        {
            int threadGroups = Mathf.CeilToInt(resolution / 8f);
            shader.Dispatch(kernel, threadGroups, threadGroups, 1);
        }

        protected void BindTexture(ComputeShader shader, int kernel, string name, Texture texture)
        {
            shader.SetTexture(kernel, name, texture != null ? texture : FallbackWhite);
        }

        protected void BindTextureOrBlack(ComputeShader shader, int kernel, string name, Texture texture)
        {
            shader.SetTexture(kernel, name, texture != null ? texture : FallbackBlack);
        }

        protected static Texture2D FallbackWhite
        {
            get
            {
                if (_fallbackWhite != null) return _fallbackWhite;
                _fallbackWhite = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color[16];
                for (int i = 0; i < 16; i++) pixels[i] = Color.white;
                _fallbackWhite.SetPixels(pixels);
                _fallbackWhite.Apply();
                _fallbackWhite.hideFlags = HideFlags.HideAndDontSave;
                return _fallbackWhite;
            }
        }

        protected static Texture2D FallbackBlack
        {
            get
            {
                if (_fallbackBlack != null) return _fallbackBlack;
                _fallbackBlack = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                var pixels = new Color[16];
                for (int i = 0; i < 16; i++) pixels[i] = new Color(0, 0, 0, 0);
                _fallbackBlack.SetPixels(pixels);
                _fallbackBlack.Apply();
                _fallbackBlack.hideFlags = HideFlags.HideAndDontSave;
                return _fallbackBlack;
            }
        }

        protected static float SliderField(string label, float value, float min, float max)
        {
            return EditorGUILayout.Slider(label, value, min, max);
        }

        protected static int IntSliderField(string label, int value, int min, int max)
        {
            return EditorGUILayout.IntSlider(label, value, min, max);
        }

        protected static Color ColorField(string label, Color value)
        {
            return EditorGUILayout.ColorField(label, value);
        }

        protected static bool ToggleField(string label, bool value)
        {
            return EditorGUILayout.Toggle(label, value);
        }

        protected static T ObjectField<T>(string label, T obj) where T : UnityEngine.Object
        {
            return (T)EditorGUILayout.ObjectField(label, obj, typeof(T), false);
        }
    }
}
