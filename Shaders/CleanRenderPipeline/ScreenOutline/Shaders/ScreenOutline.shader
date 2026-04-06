Shader "Hidden/CleanRender/ScreenOutline"
{
    Properties
    {
        _BlitTexture ("Source", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        // ================================================================
        // Pass 0: Edge detect + composite (Quest optimized)
        // ================================================================
        // On Quest standalone at native res (1832x1920/eye):
        //   - Roberts Cross on depth = 2 extra depth samples (not 4)
        //   - Optional normal edge = +2 samples (off by default for Quest)
        //   - Distance fade from depth (already sampled, free)
        //   - Stencil skip for transparent (0 extra draw calls)
        //   - Half-res aware via _OutlineTexelScale
        //
        // Total: 3 texture samples (1 scene + 2 depth neighbors)
        // vs old: 8-10 samples
        // ================================================================
        Pass
        {
            Name "ScreenOutlineComposite"

            // Stencil: skip pixels marked by transparent pass
            Stencil
            {
                Ref 1
                ReadMask 1
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ USE_DEPTH
            #pragma multi_compile_local _ USE_NORMALS
            #pragma multi_compile_local _ _HALF_RES

            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #if defined(USE_DEPTH)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            #if defined(USE_NORMALS)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #endif

            CBUFFER_START(UnityPerMaterial)
                half  _Thickness;
                half4 _OutlineColor;
                half  _DepthThreshold;
                half  _NormalThreshold;
                half  _Intensity;
                half  _FadeStart;
                half  _FadeEnd;
                half  _VRPeripheryFade;
                float2 _OutlineTexelScale;  // (1,1) for full-res, (2,2) for half-res
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                // Scene color (always needed)
                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                #if !defined(USE_DEPTH) && !defined(USE_NORMALS)
                    return sceneColor;
                #endif

                // === DEPTH EDGE (Roberts Cross — 2 samples) ===
                half edge = 0.0h;
                half distFade = 1.0h;

                #if defined(USE_DEPTH)
                    float rawDepth = SampleSceneDepth(uv);

                    // Sky early-out
                    #if UNITY_REVERSED_Z
                        if (rawDepth < 1e-6) return sceneColor;
                    #else
                        if (rawDepth > 0.999999) return sceneColor;
                    #endif

                    half linearEye = (half)LinearEyeDepth(rawDepth, _ZBufferParams);

                    // Distance fade (free — depth already sampled)
                    distFade = 1.0h - saturate((linearEye - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001h));
                    if (distFade < 0.001h) return sceneColor;

                    // Roberts Cross: diagonal pair only (2 samples vs Sobel's 8)
                    // Enough for toon outline, much cheaper
                    float2 delta = _BlitTexture_TexelSize.xy * _Thickness * _OutlineTexelScale;

                    half d_tl = (half)LinearEyeDepth(SampleSceneDepth(uv + float2(-delta.x, -delta.y)), _ZBufferParams);
                    half d_br = (half)LinearEyeDepth(SampleSceneDepth(uv + float2( delta.x,  delta.y)), _ZBufferParams);

                    // Relative depth difference (view-independent)
                    half depthEdge = abs(d_tl - d_br) / max(linearEye, 0.01h);
                    edge = saturate((depthEdge - _DepthThreshold * 0.03h) * 25.0h);
                #else
                    float2 delta = _BlitTexture_TexelSize.xy * _Thickness * _OutlineTexelScale;
                #endif

                // === NORMAL EDGE (optional, off for Quest by default) ===
                #if defined(USE_NORMALS)
                    half3 n_tl = (half3)SampleSceneNormals(uv + float2(-delta.x, -delta.y));
                    half3 n_br = (half3)SampleSceneNormals(uv + float2( delta.x,  delta.y));
                    half normalEdge = 1.0h - dot(n_tl, n_br);
                    edge = max(edge, saturate((normalEdge - _NormalThreshold) * 5.0h));
                #endif

                // Apply distance fade
                edge *= distFade;

                // VR periphery fade (reduce outline at screen edges for comfort)
                if (_VRPeripheryFade > 0.001h)
                {
                    half2 centered = (half2)(uv * 2.0h - 1.0h);
                    edge *= 1.0h - saturate(dot(centered, centered) * _VRPeripheryFade);
                }

                edge = saturate(edge * _Intensity);
                return lerp(sceneColor, half4(_OutlineColor.rgb, 1.0h), edge * _OutlineColor.a);
            }
            ENDHLSL
        }

        // ================================================================
        // Pass 1: Stencil mark (for transparent objects)
        // ================================================================
        // This pass runs during transparent rendering (LightMode tag).
        // It ONLY writes stencil bit 1 — no color, no depth.
        // Zero extra draw calls because URP already renders these objects;
        // we just add a stencil write to their existing render.
        //
        // Actually, this pass is not used directly — instead, transparent
        // shaders should add stencil write in their existing passes.
        // See StencilTransparentMark shader below for override material approach.
        // ================================================================
    }
}
