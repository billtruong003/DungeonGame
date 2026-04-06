using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CleanRenderPipeline.ScreenOutline
{
    [Serializable, VolumeComponentMenu("CleanRender/Screen Outline")]
    public sealed class OutlineSettings : VolumeComponent, IPostProcessComponent
    {
        [Header("Master")]
        public BoolParameter isActive = new BoolParameter(false);

        [Header("Appearance")]
        public ColorParameter outlineColor = new ColorParameter(new Color(0f, 0f, 0f, 1f), hdr: false, showAlpha: true, showEyeDropper: true);
        public ClampedIntParameter thickness = new ClampedIntParameter(2, 1, 5);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);

        [Header("Edge Detection")]
        public BoolParameter useDepth = new BoolParameter(true);
        public BoolParameter useNormals = new BoolParameter(false);  // OFF by default for Quest
        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(1.5f, 0f, 10f);
        public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.35f, 0f, 1f);

        [Header("Distance Fade")]
        public FloatParameter fadeStart = new FloatParameter(60f);
        public FloatParameter fadeEnd = new FloatParameter(120f);

        [Header("VR Optimization")]
        [Tooltip("Fade outline at screen edges (reduces peripheral visual noise in VR)")]
        public ClampedFloatParameter vrPeripheryFade = new ClampedFloatParameter(0.3f, 0f, 3f);

        [Tooltip("Render outline at half resolution. Saves ~75% fill rate. Recommended for Quest.")]
        public BoolParameter halfResolution = new BoolParameter(true);

        [Header("Transparent Handling")]
        [Tooltip("Layer mask for transparent objects that should NOT have outlines drawn over them. Uses stencil — 0 extra draw calls.")]
        public LayerMaskParameter transparentSkipLayer = new LayerMaskParameter(0);

        public bool IsActive() => isActive.value;
        public bool IsTileCompatible() => false;
    }
}
