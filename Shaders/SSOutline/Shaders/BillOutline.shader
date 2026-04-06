Shader "Hidden/BillDev/SSOutline"
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

        Pass
        {
            Name "BillOutlinePass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_local _ OUTLINE_FULL OUTLINE_SELECTION OUTLINE_MIXED
            #pragma multi_compile_local _ USE_DEPTH
            #pragma multi_compile_local _ USE_NORMALS

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

            TEXTURE2D_X(_SelectionMaskTexture);
            TEXTURE2D_X(_OcclusionMaskTexture);

            CBUFFER_START(UnityPerMaterial)
                half  _Thickness;
                half4 _OutlineColor;
                half  _DepthThreshold;
                half  _NormalThreshold;
                half  _DepthViewBias;
                half  _NormalViewBias;
                half  _OutlineIntensity;
                int   _DebugMode;
                half  _FadeStart;
                half  _FadeEnd;
                half  _VRPeripheryFade;
            CBUFFER_END

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 sceneColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                #if !defined(OUTLINE_FULL) && !defined(OUTLINE_SELECTION) && !defined(OUTLINE_MIXED)
                    return sceneColor;
                #endif

                half occlusionMask = SAMPLE_TEXTURE2D_X(_OcclusionMaskTexture, sampler_LinearClamp, uv).r;
                if (occlusionMask > 0.5h)
                    return sceneColor;

                #if defined(USE_DEPTH)
                    float rawDepth = SampleSceneDepth(uv);
                    #if UNITY_REVERSED_Z
                        if (rawDepth < 1e-6) return sceneColor;
                    #else
                        if (rawDepth > 0.999999) return sceneColor;
                    #endif
                    half linearEye = (half)LinearEyeDepth(rawDepth, _ZBufferParams);
                    half distFade = 1.0h - saturate((linearEye - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001h));
                    if (distFade < 0.001h) return sceneColor;
                #else
                    half distFade = 1.0h;
                #endif

                #if defined(USE_DEPTH)
                    half thickScale = lerp(1.0h, 0.5h, saturate(linearEye * 0.002h));
                #else
                    half thickScale = 1.0h;
                #endif

                float2 delta = _BlitTexture_TexelSize.xy * _Thickness * thickScale;
                half edge = 0.0h;

                #if defined(OUTLINE_FULL) || defined(OUTLINE_MIXED)
                    #if defined(USE_DEPTH)
                        half d0 = (half)LinearEyeDepth(SampleSceneDepth(uv + float2(-delta.x, -delta.y)), _ZBufferParams);
                        half d1 = (half)LinearEyeDepth(SampleSceneDepth(uv + float2(delta.x, delta.y)), _ZBufferParams);
                        half viewPitch = saturate(abs((half)UNITY_MATRIX_V[2].y));
                        half dynDepthThresh = max(0.001h, _DepthThreshold * 0.03h * (1.0h - viewPitch * _DepthViewBias));
                        half depthDiff = abs(d0 - d1) / max(linearEye, 0.01h);
                        edge = max(edge, saturate((depthDiff - dynDepthThresh) * 25.0h));
                    #endif

                    #if defined(USE_NORMALS)
                        half3 n0 = (half3)SampleSceneNormals(uv + float2(-delta.x, -delta.y));
                        half3 n1 = (half3)SampleSceneNormals(uv + float2(delta.x, delta.y));
                        half dynNormThresh = max(0.001h, _NormalThreshold * (1.0h - saturate(abs((half)UNITY_MATRIX_V[2].y)) * _NormalViewBias));
                        half normalDiff = 1.0h - dot(n0, n1);
                        edge = max(edge, saturate((normalDiff - dynNormThresh) * 5.0h));
                    #endif
                #endif

                #if defined(OUTLINE_SELECTION) || defined(OUTLINE_MIXED)
                    half centerMask = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv).r;
                    if (centerMask < 0.01h)
                    {
                        half s0 = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv + float2(delta.x, 0)).r;
                        half s1 = SAMPLE_TEXTURE2D_X(_SelectionMaskTexture, sampler_LinearClamp, uv + float2(0, delta.y)).r;
                        half selDiff = max(abs(centerMask - s0), abs(centerMask - s1));
                        edge = max(edge, saturate((selDiff - 0.05h) * 4.0h));
                    }
                #endif

                edge *= distFade;

                if (_VRPeripheryFade > 0.001h)
                {
                    half2 centered = (half2)(uv * 2.0h - 1.0h);
                    edge *= 1.0h - saturate(dot(centered, centered) * _VRPeripheryFade);
                }

                edge = saturate(edge * _OutlineIntensity);
                return lerp(sceneColor, half4(_OutlineColor.rgb, 1.0h), edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
