using System.Collections.Generic;
using UnityEngine;

namespace CleanRenderPipeline.InvertHull
{
    /// <summary>
    /// OutlineManager v4 — SRP Batcher Compatible
    ///
    /// v3 BUG: Used MaterialPropertyBlock to set _OutlineWidth=0 for culling.
    ///         MaterialPropertyBlock BREAKS SRP Batcher for EVERY renderer it touches.
    ///         500 outlined objects = 500 broken batches = massive draw call spike.
    ///
    /// v4 FIX: Removed ALL MaterialPropertyBlock usage.
    ///
    ///   [Frustum Cull]  Unity already culls renderers outside the camera frustum.
    ///                   The outline pass is part of the same renderer, so it gets
    ///                   culled too. No C# intervention needed.
    ///
    ///   [Distance Fade] The shader already does per-vertex distance fade via
    ///                   global properties (_Global_OutlineDistFade, _Global_OutlineDistMax).
    ///                   This works correctly for ALL meshes including baked ones.
    ///                   No C# per-object work needed.
    ///
    ///   [Per-Object Override] OutlineOverride components create material instances
    ///                         (renderer.material) ONLY for objects that need custom
    ///                         width/color. Objects without overrides share materials
    ///                         and batch perfectly via SRP Batcher.
    ///
    /// NET RESULT: SRP Batcher stays intact. Objects with identical materials batch
    ///             together. Only objects with OutlineOverride get material instances.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public class OutlineManager : MonoBehaviour
    {
        [Header("Required")]
        public OutlineGlobalSettings settings;

        [Header("Auto-Collect")]
        [Tooltip("How often to re-scan scene for new OutlineOverride components (seconds).")]
        [Range(0.5f, 10f)]
        public float collectInterval = 3f;

        [Header("Debug")]
        [SerializeField] private int _totalInvertHullRenderers;
        [SerializeField] private int _overrideCount;

        // --- Singleton ---
        private static OutlineManager _instance;
        public static OutlineManager Instance => _instance;

        // --- Tracked renderers with overrides only ---
        private struct OverrideRenderer
        {
            public Renderer renderer;
            public OutlineOverride overrideComp;
            public Material instanceMat;
            public float lastWidth;
            public Color lastColor;
        }

        private List<OverrideRenderer> _overrides = new List<OverrideRenderer>(64);
        private HashSet<int> _trackedInstanceIDs = new HashSet<int>();
        private float _lastCollectTime;
        private bool _isVR;

        // Shader property IDs
        private static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");

        // InvertHull shader names for detection
        private static readonly HashSet<string> InvertHullShaders = new HashSet<string>
        {
            "CleanRender/ToonLit InvertHull",
            "CleanRender/ToonMetal InvertHull",
            "CleanRender/ToonCrystal InvertHull",
        };

        // ================================================================
        // Lifecycle
        // ================================================================
        private void OnEnable()
        {
            _instance = this;
            _isVR = UnityEngine.XR.XRSettings.isDeviceActive;
            _lastCollectTime = -999f;
            if (settings != null) settings.Apply(_isVR);
        }

        private void OnDisable()
        {
            RestoreAllMaterials();
            Shader.SetGlobalFloat("_Global_OutlineWidth", 0f);
            if (_instance == this) _instance = null;
        }

        // ================================================================
        // Public API
        // ================================================================

        public void EnableOutlines()
        {
            if (settings != null) { settings.outlineEnabled = true; settings.Apply(_isVR); }
        }

        public void DisableOutlines()
        {
            if (settings != null) settings.DisableAll();
        }

        public void SetGlobalWidth(float width)
        {
            if (settings != null) { settings.globalWidth = width; settings.Apply(_isVR); }
        }

        public void SetFadeDistance(float start, float end)
        {
            if (settings != null)
            {
                settings.fadeStartDistance = start;
                settings.fadeEndDistance = end;
                settings.Apply(_isVR);
            }
        }

        /// <summary>Force re-collect (e.g. after spawning objects)</summary>
        public void ForceCollect()
        {
            _lastCollectTime = -999f;
        }

        // ================================================================
        // Update — lightweight: only pushes globals + updates overrides
        // ================================================================
        private void LateUpdate()
        {
            if (settings == null) return;

            // Push global settings — Shader.SetGlobalXxx is free, never breaks batching
            settings.Apply(_isVR);

            // Auto-collect overrides periodically
            float time = Time.unscaledTime;
            if (time - _lastCollectTime > collectInterval)
            {
                CollectOverrides();
                _lastCollectTime = time;
            }

            // Apply per-object overrides via material instances
            ApplyOverrides();
        }

        // ================================================================
        // Collect: only find renderers with OutlineOverride
        //
        // Renderers WITHOUT OutlineOverride need ZERO C# work:
        //   - SRP Batcher batches them by shared material automatically
        //   - Shader does per-vertex distance fade via globals
        //   - Unity does frustum culling built-in
        // ================================================================
        private void CollectOverrides()
        {
            // Clean destroyed entries
            for (int i = _overrides.Count - 1; i >= 0; i--)
            {
                var entry = _overrides[i];
                if (entry.renderer == null || entry.overrideComp == null)
                {
                    if (entry.instanceMat != null)
                    {
#if UNITY_EDITOR
                        DestroyImmediate(entry.instanceMat);
#else
                        Destroy(entry.instanceMat);
#endif
                    }
                    _trackedInstanceIDs.Remove(entry.renderer != null
                        ? entry.renderer.GetInstanceID() : 0);
                    _overrides.RemoveAt(i);
                }
            }

            // Find new OutlineOverride components
            var allOverrides = FindObjectsByType<OutlineOverride>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var ov in allOverrides)
            {
                var r = ov.GetComponent<Renderer>();
                if (r == null) continue;

                int id = r.GetInstanceID();
                if (_trackedInstanceIDs.Contains(id)) continue;

                // Verify it uses an InvertHull shader
                bool usesIH = false;
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && InvertHullShaders.Contains(mats[m].shader.name))
                    {
                        usesIH = true;
                        break;
                    }
                }
                if (!usesIH) continue;

                // Create material instance for this renderer.
                // renderer.material clones the sharedMaterial — this instance is
                // unique to this renderer and does NOT affect SRP Batcher for
                // other renderers still sharing the original material.
                Material instanceMat = r.material;

                _overrides.Add(new OverrideRenderer
                {
                    renderer = r,
                    overrideComp = ov,
                    instanceMat = instanceMat,
                    lastWidth = -1f,
                    lastColor = Color.clear
                });
                _trackedInstanceIDs.Add(id);
            }

            _overrideCount = _overrides.Count;

#if UNITY_EDITOR
            // Debug: count total InvertHull renderers (editor only, skip in builds)
            int total = 0;
            var allRenderers = FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var r in allRenderers)
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] != null && InvertHullShaders.Contains(mats[m].shader.name))
                    { total++; break; }
                }
            }
            _totalInvertHullRenderers = total;
#endif
        }

        // ================================================================
        // Apply per-object overrides via material instance properties
        //
        // Only runs on the handful of objects with OutlineOverride.
        // All other objects use shared materials and batch untouched.
        // ================================================================
        private void ApplyOverrides()
        {
            for (int i = _overrides.Count - 1; i >= 0; i--)
            {
                var entry = _overrides[i];

                if (entry.renderer == null || !entry.renderer.gameObject.activeInHierarchy)
                {
                    if (entry.instanceMat != null)
                    {
#if UNITY_EDITOR
                        DestroyImmediate(entry.instanceMat);
#else
                        Destroy(entry.instanceMat);
#endif
                    }
                    _trackedInstanceIDs.Remove(entry.renderer != null
                        ? entry.renderer.GetInstanceID() : 0);
                    _overrides.RemoveAt(i);
                    continue;
                }

                float width = entry.overrideComp.widthOverride;
                Color color = entry.overrideComp.colorOverride;

                // Only update material properties if values changed
                bool changed = false;

                if (!Mathf.Approximately(width, entry.lastWidth))
                {
                    entry.instanceMat.SetFloat(PropOutlineWidth, width);
                    entry.lastWidth = width;
                    changed = true;
                }

                if (color != entry.lastColor && color.a > 0.01f)
                {
                    entry.instanceMat.SetColor(PropOutlineColor, color);
                    entry.lastColor = color;
                    changed = true;
                }

                if (changed)
                    _overrides[i] = entry;

                entry.overrideComp.SetDebugState(width, true);
            }
        }

        // ================================================================
        // Cleanup: destroy material instances to prevent leaks
        // ================================================================
        private void RestoreAllMaterials()
        {
            for (int i = 0; i < _overrides.Count; i++)
            {
                var entry = _overrides[i];
                if (entry.instanceMat != null)
                {
#if UNITY_EDITOR
                    DestroyImmediate(entry.instanceMat);
#else
                    Destroy(entry.instanceMat);
#endif
                }
            }
            _overrides.Clear();
            _trackedInstanceIDs.Clear();
        }
    }
}
