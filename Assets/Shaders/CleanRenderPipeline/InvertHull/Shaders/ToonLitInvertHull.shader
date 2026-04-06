Shader "CleanRender/ToonLit InvertHull"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
        [Toggle(_USE_LOCAL_TOON)] _UseLocalToon("Use Local Params", Float) = 0
        _ShadowColor("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.5
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.05
        _RimColor("Rim Color", Color) = (1, 1, 1, 0.5)
        _RimPower("Rim Power", Range(0.1, 10)) = 3
        
        [Header(Fresnel Outline)]
        [Toggle(_FRESNEL_OUTLINE)] _FresnelOutline("Enable Fresnel Outline", Float) = 0
        _FresnelOutlineColor("Fresnel Outline Color", Color) = (0, 0, 0, 1)
        _OutlineThreshold("Fresnel Outline Threshold", Range(0.01, 1.0)) = 0.8
        _OutlineSmoothness("Fresnel Outline Smoothness", Range(0.001, 0.5)) = 0.01

        [Header(Invert Hull Outline)]
        [Toggle(_INVERT_HULL_ON)] _InvertHullOn("Enable Invert Hull Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width (pixels)", Range(0.1, 10.0)) = 1.5
        _OutlineZOffset("Outline Z Offset", Range(0, 5)) = 1.0
        [Toggle(_OUTLINE_SMOOTH_NORMAL)] _OutlineSmoothNormal("Use Smooth Normals (Tangent)", Float) = 0

        [Header(Alpha and Emission)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_EMISSION)] _Emission("Emission", Float) = 0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0)
        _EmissionMap("Emission Map", 2D) = "white"{}
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 200
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/InvertHullOutline.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _RimColor;
            float _Threshold;
            float _Smoothness;
            float _RimPower;
            
            half4 _FresnelOutlineColor;
            float _OutlineThreshold;
            float _OutlineSmoothness;

            half4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineZOffset;

            float _Cutoff;
            half4 _EmissionColor;
        CBUFFER_END
        
        TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);
        ENDHLSL

        // ============================================================
        // Pass 0: Invert Hull Outline (renders back faces extruded)
        // ============================================================
        Pass
        {
            Name "InvertHullOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            
            Cull Front
            ZWrite On
            
            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _INVERT_HULL_ON
            #pragma shader_feature_local _OUTLINE_SMOOTH_NORMAL
            #pragma shader_feature_local _ALPHATEST_ON
            
            OutlineVaryings OutlineVert(OutlineAttributes input)
            {
                return InvertHullOutlineVert(input, _OutlineWidth, _OutlineZOffset);
            }
            
            half4 OutlineFrag(OutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
                #endif
                
                // Discard if fully faded (VR distance optimization)
                clip(input.fadeFactor - 0.001h);
                
                return half4(_OutlineColor.rgb, _OutlineColor.a * input.fadeFactor);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 1: ForwardLit (identical to base ToonLit)
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull[_Cull]
            AlphaToMask[_ALPHATEST_ON]
            
            HLSLPROGRAM
            #pragma vertex ToonLitVert
            #pragma fragment ToonLitFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _USE_LOCAL_TOON
            #pragma shader_feature_local _FRESNEL_OUTLINE
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                half3  normalWS : TEXCOORD2;
                float4 uv : TEXCOORD0;
                half   fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings ToonLitVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv.xy = TRANSFORM_TEX(input.uv, _BaseMap);
                o.uv.zw = ToonTransformLightmapUV(input.lightmapUV);
                o.fogFactor = (half)ComputeFogFactor(o.positionCS.z);
                return o;
            }
            
            half4 ToonLitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy) * _BaseColor;
                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif
                
                half3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                
                #ifdef _USE_LOCAL_TOON
                    ToonLightResult lit = ComputeToonMainLight(
                        input.positionWS, normalWS, viewDirWS, albedo.rgb, input.uv.zw,
                        _Threshold, _Smoothness, _ShadowColor.rgb,
                        _RimColor.rgb, _RimPower, _RimColor.a, 1.0h);
                #else
                    ToonLightResult lit = ComputeToonMainLightGlobal(
                        input.positionWS, normalWS, viewDirWS, albedo.rgb, input.uv.zw);
                #endif
                
                half3 finalColor = lit.diffuse + lit.rim + lit.globalIllumination;
                
                #ifdef _ADDITIONAL_LIGHTS
                    finalColor += ComputeToonAdditionalLightsGlobal(input.positionWS, normalWS, albedo.rgb);
                #endif
                
                #ifdef _EMISSION
                    finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv.xy).rgb * _EmissionColor.rgb;
                #endif

                #ifdef _FRESNEL_OUTLINE
                    half invNdotV = 1.0h - saturate(dot(normalWS, viewDirWS));
                    half fresnel = smoothstep(_OutlineThreshold - _OutlineSmoothness, _OutlineThreshold + _OutlineSmoothness, invNdotV);
                    finalColor = lerp(finalColor, _FresnelOutlineColor.rgb, fresnel * _FresnelOutlineColor.a);
                #endif

                finalColor = MixFog(finalColor, input.fogFactor);
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 2: ShadowCaster
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _ALPHATEST_ON
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            float3 _LightDirection;
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                #ifdef _ALPHATEST_ON
                    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #else
                    o.uv = 0;
                #endif
                return o;
            }
            
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 3: DepthOnly
        // ============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _ALPHATEST_ON
            
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            
            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                #ifdef _ALPHATEST_ON
                    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #else
                    o.uv = 0;
                #endif
                return o;
            }
            
            half4 DepthFrag(Varyings input) : SV_Target
            {
                #ifdef _ALPHATEST_ON
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 4: DepthNormals
        // ============================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull[_Cull]
            
            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED
            #pragma shader_feature_local _ALPHATEST_ON
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            Varyings DNVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                #ifdef _ALPHATEST_ON
                    o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #else
                    o.uv = 0;
                #endif
                return o;
            }
            
            half4 DNFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                #ifdef _ALPHATEST_ON
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 5: Meta
        // ============================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            Varyings MetaVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                o.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return o;
            }
            
            half4 MetaFrag(Varyings input) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                #ifdef _ALPHATEST_ON
                    clip(base.a * _BaseColor.a - _Cutoff);
                #endif
                half3 albedo = base.rgb * _BaseColor.rgb;
                half3 emission = half3(0.0h, 0.0h, 0.0h);
                #ifdef _EMISSION
                    emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = albedo;
                metaInput.Emission = emission;
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
}
