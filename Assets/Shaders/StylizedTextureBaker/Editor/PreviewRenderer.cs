using UnityEngine;
using UnityEditor;

namespace StylizedTextureBaker
{
    public class PreviewRenderer : System.IDisposable
    {
        public enum ViewMode
        {
            Unlit,
            Lit
        }

        private PreviewRenderUtility _preview;
        private Material _material;
        private Mesh _mesh;

        private float _rotX = 20f;
        private float _rotY = -30f;
        private float _zoom = 3f;
        private Vector3 _pivot = Vector3.zero;

        private bool _initialized;
        private Rect _lastValidRect;

        public ViewMode Mode { get; set; } = ViewMode.Unlit;

        public void SetMesh(Mesh mesh)
        {
            _mesh = mesh;
            if (mesh == null) return;

            var bounds = mesh.bounds;
            _pivot = bounds.center;
            _zoom = Mathf.Max(bounds.extents.magnitude * 2.8f, 0.5f);
        }

        public void SetTexture(Texture texture)
        {
            EnsureInitialized();
            if (_material != null)
                _material.mainTexture = texture;
        }

        public bool Render(Rect rect)
        {
            if (_mesh == null) return false;

            if (rect.width >= 2f && rect.height >= 2f)
                _lastValidRect = rect;

            EnsureInitialized();
            if (_preview == null || _material == null) return false;

            bool inputChanged = ProcessInput(rect);

            if (Event.current.type == EventType.Repaint && _lastValidRect.width >= 2f)
                DrawPreview(_lastValidRect);

            return inputChanged;
        }

        public void Dispose()
        {
            _preview?.Cleanup();
            _preview = null;

            if (_material != null)
                UnityEngine.Object.DestroyImmediate(_material);

            _material = null;
            _initialized = false;
        }

        private void DrawPreview(Rect rect)
        {
            _material.SetFloat("_PreviewMode", Mode == ViewMode.Lit ? 1f : 0f);

            _preview.BeginPreview(rect, GUIStyle.none);

            var cam = _preview.camera;
            cam.nearClipPlane = 0.001f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.12f, 0.14f, 1f);

            Quaternion rotation = Quaternion.Euler(_rotX, _rotY, 0f);
            cam.transform.position = _pivot + rotation * new Vector3(0f, 0f, -_zoom);
            cam.transform.LookAt(_pivot, Vector3.up);

            _preview.DrawMesh(_mesh, Matrix4x4.identity, _material, 0);
            cam.Render();

            var resultTex = _preview.EndPreview();
            GUI.DrawTexture(rect, resultTex, ScaleMode.StretchToFill, false);

            var hintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(1f, 1f, 1f, 0.25f) },
                alignment = TextAnchor.LowerLeft,
                padding = new RectOffset(6, 0, 0, 4)
            };
            EditorGUI.LabelField(rect, "LMB: Orbit  |  RMB: Zoom  |  MMB: Pan", hintStyle);
        }

        private bool ProcessInput(Rect rect)
        {
            var evt = Event.current;
            if (!rect.Contains(evt.mousePosition)) return false;

            bool changed = false;

            switch (evt.type)
            {
                case EventType.MouseDrag when evt.button == 0:
                    _rotY += evt.delta.x * 0.4f;
                    _rotX += evt.delta.y * 0.4f;
                    _rotX = Mathf.Clamp(_rotX, -89f, 89f);
                    changed = true;
                    evt.Use();
                    break;

                case EventType.MouseDrag when evt.button == 1:
                    _zoom *= 1f + evt.delta.y * 0.005f;
                    _zoom = Mathf.Clamp(_zoom, 0.05f, 100f);
                    changed = true;
                    evt.Use();
                    break;

                case EventType.MouseDrag when evt.button == 2:
                    Quaternion rot = Quaternion.Euler(_rotX, _rotY, 0f);
                    float panSpeed = _zoom * 0.0015f;
                    _pivot -= rot * Vector3.right * evt.delta.x * panSpeed;
                    _pivot += rot * Vector3.up * evt.delta.y * panSpeed;
                    changed = true;
                    evt.Use();
                    break;

                case EventType.ScrollWheel:
                    _zoom *= 1f + evt.delta.y * 0.06f;
                    _zoom = Mathf.Clamp(_zoom, 0.05f, 100f);
                    changed = true;
                    evt.Use();
                    break;
            }

            return changed;
        }

        private void EnsureInitialized()
        {
            if (_initialized && _preview != null && _material != null) return;

            _preview?.Cleanup();
            _preview = new PreviewRenderUtility();

            var shader = Shader.Find("Hidden/StylizedBaker/Preview");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            if (_material != null)
                UnityEngine.Object.DestroyImmediate(_material);

            _material = new Material(shader);
            _initialized = true;
        }
    }
}
