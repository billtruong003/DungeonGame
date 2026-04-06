using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace BillDev.SSOutline
{
    public sealed class OutlineFeature : ScriptableRendererFeature
    {
        public static event Action<RasterCommandBuffer, LayerMask> OnRenderFoliageMask;

        [SerializeField] private Shader outlineShader;
        [SerializeField] private Shader maskShader;

        private static readonly int PID_Thickness = Shader.PropertyToID("_Thickness");
        private static readonly int PID_Color = Shader.PropertyToID("_OutlineColor");
        private static readonly int PID_DepthThresh = Shader.PropertyToID("_DepthThreshold");
        private static readonly int PID_NormalThresh = Shader.PropertyToID("_NormalThreshold");
        private static readonly int PID_DepthViewBias = Shader.PropertyToID("_DepthViewBias");
        private static readonly int PID_NormalViewBias = Shader.PropertyToID("_NormalViewBias");
        private static readonly int PID_Intensity = Shader.PropertyToID("_OutlineIntensity");
        private static readonly int PID_DebugMode = Shader.PropertyToID("_DebugMode");
        private static readonly int PID_FadeStart = Shader.PropertyToID("_FadeStart");
        private static readonly int PID_FadeEnd = Shader.PropertyToID("_FadeEnd");
        private static readonly int PID_VRFade = Shader.PropertyToID("_VRPeripheryFade");
        private static readonly int PID_SelectionMask = Shader.PropertyToID("_SelectionMaskTexture");
        private static readonly int PID_OcclusionMask = Shader.PropertyToID("_OcclusionMaskTexture");

        private sealed class MaskPass : ScriptableRenderPass, IDisposable
        {
            private readonly string _profilerTag;
            private readonly string _textureName;
            private readonly int _textureId;
            private readonly bool _isOcclusion;
            private readonly bool _useSceneDepth;
            private readonly bool _halfRes;
            private Material _overrideMaterial;
            private Shader _maskShader;
            private LayerMask _layerMask;

            private RTHandle _cachedRT;
            private float _lastRenderTime = -1f;
            private float _cacheInterval;
            private int _cachedWidth;
            private int _cachedHeight;
            private int _cachedMsaa;
            private TextureDimension _cachedDimension;

            private static readonly ShaderTagId TAG_MASK = new ShaderTagId("SelectionMask");
            private static readonly ShaderTagId TAG_OCCLUDE = new ShaderTagId("OcclusionMask");
            private static readonly ShaderTagId[] TAG_FORWARD =
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
            };

            public TextureHandle MaskTextureHandle { get; private set; }

            public MaskPass(string tag, string texName, bool isOcclusion, bool useSceneDepth, bool halfRes)
            {
                _profilerTag = tag;
                _textureName = texName;
                _textureId = Shader.PropertyToID(texName);
                _isOcclusion = isOcclusion;
                _useSceneDepth = useSceneDepth;
                _halfRes = halfRes;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                if (isOcclusion)
                    ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public void Setup(LayerMask mask, Shader shader, float interval)
            {
                _layerMask = mask;
                _maskShader = shader;
                _cacheInterval = interval;
                if (_overrideMaterial != null) return;
                if (_maskShader != null)
                    _overrideMaterial = CoreUtils.CreateEngineMaterial(_maskShader);
            }

            private sealed class PassData
            {
                public RendererListHandle dedicatedList;
                public RendererListHandle fallbackList;
                public bool isOcclusion;
                public bool useFallback;
                public LayerMask layerMask;
            }

            private bool NeedsRender(int w, int h, int msaa, TextureDimension dim)
            {
                if (_cachedRT == null) return true;
                if (_cachedWidth != w || _cachedHeight != h || _cachedMsaa != msaa || _cachedDimension != dim) return true;
                if (_cacheInterval <= 0f) return true;
                return Time.time - _lastRenderTime >= _cacheInterval;
            }

            private void EnsureRT(int w, int h, int msaa, TextureDimension dim, int slices)
            {
                if (_cachedRT != null && _cachedWidth == w && _cachedHeight == h && _cachedMsaa == msaa && _cachedDimension == dim)
                    return;

                _cachedRT?.Release();

                var rtDesc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0, 1)
                {
                    msaaSamples = msaa,
                    dimension = dim,
                    volumeDepth = slices,
                    useMipMap = false,
                    autoGenerateMips = false,
                    sRGB = false,
                };
                RenderingUtils.ReAllocateHandleIfNeeded(ref _cachedRT, rtDesc, FilterMode.Bilinear, name: _textureName + "_Cached");

                _cachedWidth = w;
                _cachedHeight = h;
                _cachedMsaa = msaa;
                _cachedDimension = dim;
                _lastRenderTime = -1f;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var resourceData = frameData.Get<UniversalResourceData>();

                var camDesc = cameraData.cameraTargetDescriptor;
                int msaa = _useSceneDepth ? camDesc.msaaSamples : 1;
                int w = _halfRes ? Mathf.Max(1, camDesc.width / 2) : camDesc.width;
                int h = _halfRes ? Mathf.Max(1, camDesc.height / 2) : camDesc.height;
                var dim = camDesc.dimension;
                int slices = dim == TextureDimension.Tex2DArray ? camDesc.volumeDepth : 1;

                EnsureRT(w, h, msaa, dim, slices);

                bool needsRender = _layerMask != 0 && NeedsRender(w, h, msaa, dim);
                var imported = renderGraph.ImportTexture(_cachedRT);
                MaskTextureHandle = imported;

                if (_layerMask == 0 || !needsRender)
                {
                    if (_layerMask == 0 && _lastRenderTime < 0f)
                    {
                        using var clearBuilder = renderGraph.AddRasterRenderPass<PassData>(
                            _profilerTag + "_Clear", out _);
                        clearBuilder.SetRenderAttachment(imported, 0, AccessFlags.Write);
                        clearBuilder.SetGlobalTextureAfterPass(imported, _textureId);
                        clearBuilder.SetRenderFunc((PassData _, RasterGraphContext ctx) =>
                        {
                            ctx.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1f, 0);
                        });
                        _lastRenderTime = Time.time;
                    }
                    else
                    {
                        using var reuseBuilder = renderGraph.AddRasterRenderPass<PassData>(
                            _profilerTag + "_Reuse", out _);
                        reuseBuilder.SetRenderAttachment(imported, 0, AccessFlags.ReadWrite);
                        reuseBuilder.SetGlobalTextureAfterPass(imported, _textureId);
                        reuseBuilder.SetRenderFunc((PassData _, RasterGraphContext _) => { });
                    }
                    return;
                }

                var sorting = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.CommonOpaque };
                var filtering = new FilteringSettings(RenderQueueRange.all, _layerMask);

                var dedicatedRL = renderGraph.CreateRendererList(new RendererListParams(
                    renderingData.cullResults,
                    new DrawingSettings(_isOcclusion ? TAG_OCCLUDE : TAG_MASK, sorting),
                    filtering));

                RendererListHandle fallbackRL = default;
                bool useFallback = true;

                if (useFallback)
                {
                    var fallbackDrawing = new DrawingSettings(TAG_FORWARD[0], sorting)
                    {
                        overrideMaterial = _overrideMaterial,
                        overrideMaterialPassIndex = 0,
                    };
                    for (int i = 1; i < TAG_FORWARD.Length; i++)
                        fallbackDrawing.SetShaderPassName(i, TAG_FORWARD[i]);

                    fallbackRL = renderGraph.CreateRendererList(new RendererListParams(
                        renderingData.cullResults, fallbackDrawing, filtering));
                }

                using var builder = renderGraph.AddRasterRenderPass<PassData>(_profilerTag, out var pd);
                pd.dedicatedList = dedicatedRL;
                pd.fallbackList = fallbackRL;
                pd.isOcclusion = _isOcclusion;
                pd.layerMask = _layerMask;
                pd.useFallback = useFallback;

                builder.UseRendererList(pd.dedicatedList);
                if (useFallback)
                    builder.UseRendererList(pd.fallbackList);
                builder.SetRenderAttachment(imported, 0, AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(imported, _textureId);

                if (_useSceneDepth)
                {
                    var sceneDepth = resourceData.activeDepthTexture;
                    if (sceneDepth.IsValid())
                        builder.SetRenderAttachmentDepth(sceneDepth, AccessFlags.Read);
                }

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(RTClearFlags.Color, Color.black, 1f, 0);
                    ctx.cmd.DrawRendererList(data.dedicatedList);
                    if (data.useFallback)
                        ctx.cmd.DrawRendererList(data.fallbackList);
                    if (!data.isOcclusion)
                        OnRenderFoliageMask?.Invoke(ctx.cmd, data.layerMask);
                });

                _lastRenderTime = Time.time;
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_overrideMaterial);
                _overrideMaterial = null;
                _cachedRT?.Release();
                _cachedRT = null;
            }
        }

        private sealed class OutlineCompositePass : ScriptableRenderPass, IDisposable
        {
            private Material _material;
            private Shader _outlineShader;
            private MaskPass _selectionPass;
            private MaskPass _occlusionPass;

            public OutlineCompositePass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void SetShader(Shader shader) => _outlineShader = shader;

            public void LinkMaskPasses(MaskPass selection, MaskPass occlusion)
            {
                _selectionPass = selection;
                _occlusionPass = occlusion;
            }

            private bool EnsureMaterial()
            {
                if (_material != null) return true;
                if (_outlineShader == null) return false;
                _material = CoreUtils.CreateEngineMaterial(_outlineShader);
                return _material != null;
            }

            private void ApplySettings(OutlineVolume settings, bool hasDepth, bool hasNormals)
            {
                _material.SetFloat(PID_Thickness, settings.thickness.value);
                _material.SetColor(PID_Color, settings.outlineColor.value);
                _material.SetFloat(PID_DepthThresh, settings.depthThreshold.value);
                _material.SetFloat(PID_NormalThresh, settings.normalThreshold.value);
                _material.SetFloat(PID_DepthViewBias, settings.depthViewBias.value);
                _material.SetFloat(PID_NormalViewBias, settings.normalViewBias.value);
                _material.SetFloat(PID_Intensity, settings.outlineIntensity.value);
                _material.SetFloat(PID_FadeStart, settings.fadeDistanceStart.value);
                _material.SetFloat(PID_FadeEnd, settings.fadeDistanceEnd.value);
                _material.SetFloat(PID_VRFade, settings.vrPeripheryFade.value);
                _material.SetInt(PID_DebugMode, (int)settings.debugMode.value);

                ToggleKeyword("USE_DEPTH", hasDepth);
                ToggleKeyword("USE_NORMALS", hasNormals);
                ToggleKeyword("OUTLINE_FULL", settings.mode.value == OutlineVolume.OutlineMode.FullScreen);
                ToggleKeyword("OUTLINE_SELECTION", settings.mode.value == OutlineVolume.OutlineMode.SelectionOnly);
                ToggleKeyword("OUTLINE_MIXED", settings.mode.value == OutlineVolume.OutlineMode.Mixed);
            }

            private void ToggleKeyword(string keyword, bool on)
            {
                if (on) _material.EnableKeyword(keyword);
                else _material.DisableKeyword(keyword);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var settings = VolumeManager.instance.stack.GetComponent<OutlineVolume>();
                if (settings == null || !settings.IsActive()) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType == CameraType.Preview) return;

                var resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return;
                if (!EnsureMaterial()) return;

                var source = resourceData.activeColorTexture;
                if (!source.IsValid()) return;

                var depth = resourceData.activeDepthTexture;
                var normals = resourceData.cameraNormalsTexture;
                bool hasDepth = settings.useDepth.value && depth.IsValid();
                bool hasNormals = settings.useNormals.value && normals.IsValid();

                ApplySettings(settings, hasDepth, hasNormals);

                var destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "CameraColor-BillOutline";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                var selTex = _selectionPass?.MaskTextureHandle ?? TextureHandle.nullHandle;
                var occTex = _occlusionPass?.MaskTextureHandle ?? TextureHandle.nullHandle;

                var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0);

                using (var builder = renderGraph.AddBlitPass(blitParams, "BillOutline_Composite", returnBuilder: true))
                {
                    if (selTex.IsValid())
                        builder.UseTexture(selTex, AccessFlags.Read);
                    if (occTex.IsValid())
                        builder.UseTexture(occTex, AccessFlags.Read);
                    if (hasDepth && depth.IsValid())
                        builder.UseTexture(depth, AccessFlags.Read);
                    if (hasNormals && normals.IsValid())
                        builder.UseTexture(normals, AccessFlags.Read);
                }

                resourceData.cameraColor = destination;
            }

            public void Dispose()
            {
                CoreUtils.Destroy(_material);
                _material = null;
            }
        }

        private MaskPass _selectionPass;
        private MaskPass _occlusionPass;
        private OutlineCompositePass _outlinePass;

        public override void Create()
        {
            if (outlineShader == null) outlineShader = Shader.Find("Hidden/BillDev/SSOutline");
            if (maskShader == null) maskShader = Shader.Find("Hidden/BillDev/SelectionMask");

            _selectionPass = new MaskPass(
                "BillOutline_SelectionMask", "_SelectionMaskTexture",
                isOcclusion: false, useSceneDepth: false, halfRes: true);

            _occlusionPass = new MaskPass(
                "BillOutline_OcclusionMask", "_OcclusionMaskTexture",
                isOcclusion: true, useSceneDepth: true, halfRes: false);

            _outlinePass = new OutlineCompositePass();
            _outlinePass.SetShader(outlineShader);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var settings = VolumeManager.instance.stack.GetComponent<OutlineVolume>();
            if (settings == null || !settings.IsActive()) return;

            var inputFlags = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
            if (settings.useNormals.value)
                inputFlags |= ScriptableRenderPassInput.Normal;
            _outlinePass.ConfigureInput(inputFlags);

            float interval = settings.maskUpdateInterval.value;
            _selectionPass.Setup(settings.selectionLayer.value, maskShader, interval);
            _occlusionPass.Setup(settings.occlusionLayer.value, maskShader, interval);

            renderer.EnqueuePass(_selectionPass);
            renderer.EnqueuePass(_occlusionPass);

            _outlinePass.LinkMaskPasses(_selectionPass, _occlusionPass);
            renderer.EnqueuePass(_outlinePass);
        }

        protected override void Dispose(bool disposing)
        {
            _selectionPass?.Dispose();
            _occlusionPass?.Dispose();
            _outlinePass?.Dispose();
        }
    }
}
