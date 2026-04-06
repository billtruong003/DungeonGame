using UnityEngine;

namespace CleanRenderPipeline.InvertHull
{
    /// <summary>
    /// OutlineGlobalSettings - ScriptableObject
    /// 
    /// ONE asset that controls ALL outline behavior across the scene.
    /// Create via Assets > Create > CleanRender > Outline Global Settings
    /// 
    /// The OutlineManager reads this asset and pushes values to shaders
    /// via Shader.SetGlobalFloat/Color. No per-object setup needed for
    /// basic usage — just tweak this asset and everything updates.
    /// </summary>
    [CreateAssetMenu(fileName = "OutlineSettings", menuName = "CleanRender/Outline Global Settings")]
    public class OutlineGlobalSettings : ScriptableObject
    {
        [Header("Master Control")]
        [Tooltip("Master on/off. When false, ALL outlines in the scene are disabled.")]
        public bool outlineEnabled = true;

        [Tooltip("Global width multiplier (in screen pixels). All per-material widths are multiplied by this.")]
        [Range(0f, 10f)]
        public float globalWidth = 1.5f;

        [Tooltip("Global outline color. Per-material color overrides this if set.")]
        public Color globalColor = Color.black;

        [Header("Distance Fade")]
        [Tooltip("Distance from camera where outlines start fading. 0 = no fade (always visible).")]
        public float fadeStartDistance = 0f;

        [Tooltip("Distance from camera where outlines are fully invisible.")]
        public float fadeEndDistance = 50f;

        [Header("Camera Optimization")]
        [Tooltip("Cull outlines outside camera frustum + this margin (meters). Saves GPU fill-rate on large scenes.")]
        public float frustumCullMargin = 5f;

        [Tooltip("How many frames between distance/frustum updates. 1 = every frame, 2 = every other frame.")]
        [Range(1, 8)]
        public int updateInterval = 2;

        [Header("VR Settings")]
        [Tooltip("When true, distance fade is always active regardless of platform.")]
        public bool alwaysUseFade = false;

        [Tooltip("VR-only: near-clip distance to hide outlines very close to face.")]
        public float vrNearClip = 0.3f;

        [Header("Platform Defaults")]
        [Tooltip("On desktop (non-VR), what should the default outline state be?")]
        public DesktopOutlineMode desktopMode = DesktopOutlineMode.AlwaysOn;

        public enum DesktopOutlineMode
        {
            AlwaysOn,           // Desktop: outlines always show, no distance fade
            AlwaysOff,          // Desktop: outlines never show
            UseDistanceFade     // Desktop: same distance fade as VR
        }

        // ================================================================
        // Push all settings to global shader properties
        // Called by OutlineManager every frame (or on change)
        // ================================================================
        public void Apply(bool isVR)
        {
            float effectiveWidth = outlineEnabled ? globalWidth : 0f;

            // Platform logic
            if (!isVR)
            {
                switch (desktopMode)
                {
                    case DesktopOutlineMode.AlwaysOff:
                        effectiveWidth = 0f;
                        break;
                    case DesktopOutlineMode.AlwaysOn:
                        // Full width, no fade
                        Shader.SetGlobalFloat("_Global_OutlineWidth", effectiveWidth);
                        Shader.SetGlobalFloat("_Global_OutlineDistFade", 0f); // 0 = no fade
                        Shader.SetGlobalFloat("_Global_OutlineDistMax", 0f);
                        Shader.SetGlobalFloat("_Global_OutlineNearClip", 0f);
                        Shader.SetGlobalColor("_Global_OutlineColor", globalColor);
                        return;
                    case DesktopOutlineMode.UseDistanceFade:
                        // Fall through to VR-like behavior
                        break;
                }
            }

            float fadeStart = (isVR || alwaysUseFade || desktopMode == DesktopOutlineMode.UseDistanceFade)
                ? fadeStartDistance : 0f;
            float fadeEnd = (isVR || alwaysUseFade || desktopMode == DesktopOutlineMode.UseDistanceFade)
                ? fadeEndDistance : 0f;

            Shader.SetGlobalFloat("_Global_OutlineWidth", effectiveWidth);
            Shader.SetGlobalFloat("_Global_OutlineDistFade", fadeStart);
            Shader.SetGlobalFloat("_Global_OutlineDistMax", fadeEnd);
            Shader.SetGlobalFloat("_Global_OutlineNearClip", isVR ? vrNearClip : 0f);
            Shader.SetGlobalColor("_Global_OutlineColor", globalColor);
        }

        /// <summary>Immediately disable all outlines (e.g. for perf toggle)</summary>
        public void DisableAll()
        {
            Shader.SetGlobalFloat("_Global_OutlineWidth", 0f);
        }
    }
}
