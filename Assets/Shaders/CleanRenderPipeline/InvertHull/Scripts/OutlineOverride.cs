using UnityEngine;

namespace CleanRenderPipeline.InvertHull
{
    /// <summary>
    /// OutlineOverride — OPTIONAL per-object component
    ///
    /// You do NOT need this for outlines to work or be culled.
    /// OutlineManager auto-discovers all InvertHull renderers and culls them.
    ///
    /// Add this ONLY when you need:
    ///   - Per-object width multiplier (different from global)
    ///   - Per-object color override
    ///
    /// The Manager reads widthOverride/colorOverride from this component
    /// when processing the renderer.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    [ExecuteAlways]
    public class OutlineOverride : MonoBehaviour
    {
        [Header("Per-Object Settings")]
        [Tooltip("Width multiplier for this object. Final width = this * global width.")]
        [Range(0f, 5f)]
        public float widthOverride = 1.0f;

        [Tooltip("Override outline color. Alpha=0 → use global color.")]
        public Color colorOverride = new Color(0, 0, 0, 0);

        [Header("Debug (Read-Only)")]
        [SerializeField] private float _currentWidth;
        [SerializeField] private bool _isVisible;

        /// <summary>Called by OutlineManager to update debug display</summary>
        public void SetDebugState(float width, bool visible)
        {
            _currentWidth = width;
            _isVisible = visible;
        }
    }
}
