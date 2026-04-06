Shader "CleanRender/ToonLitMaskOutline"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        _MaskMap("Mask (RGB)", 2D) = "black"{}
        [HDR] _ColorR("Color R", Color) = (1, 0, 0, 1)
        [HDR] _ColorG("Color G", Color) = (0, 1, 0, 1)
        [HDR] _ColorB("Color B", Color) = (0, 0, 1, 1)

        [Toggle(_EMISSIVE)] _UseEmissive("Enable Emissive", Float) = 0
        _EmissiveMask("Emissive Mask", 2D) = "black"{}
        [HDR] _EmissiveColor("Emissive Color", Color) = (1, 1, 1, 1)
        _EmissiveStrength("Emissive Strength", Range(0.0, 10.0)) = 1.0

        _ShadowColor("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.5
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.05

        [Toggle(_RIM)] _UseRim("Enable Rim", Float) = 0
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 4
        _RimThreshold("Rim Threshold", Range(0.0, 1.0)) = 0.4
        _RimSmoothness("Rim Smoothness", Range(0.001, 0.5)) = 0.05
        _RimStrength("Rim Strength", Range(0.0, 5.0)) = 1.5

        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Range(0.0, 5.0)) = 1.0
        [Toggle(_OUTLINE_COLORED)] _OutlineColored("Tinted Outline", Float) = 0
        _OutlineTintBlend("Tint Blend", Range(0.0, 1.0)) = 0.5

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
    }

    CustomEditor "ToonLitMaskGUI"

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _ShadowColor;
            half4  _RimColor;
            half4  _ColorR;
            half4  _ColorG;
            half4  _ColorB;
            half4  _EmissiveColor;
            half4  _OutlineColor;
            half   _Threshold;
            half   _Smoothness;
            half   _RimPower;
            half   _RimThreshold;
            half   _RimSmoothness;
            half   _RimStrength;
            half   _EmissiveStrength;
            half   _OutlineWidth;
            half   _OutlineTintBlend;
        CBUFFER_END

        TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap);      SAMPLER(sampler_MaskMap);
        TEXTURE2D(_EmissiveMask); SAMPLER(sampler_EmissiveMask);

        inline half3 ApplyMaskTint(half3 baseColor, half3 mask, half4 colR, half4 colG, half4 colB)
        {
            half lum = dot(baseColor, half3(0.299h, 0.587h, 0.114h));
            half3 result = baseColor;
            result = lerp(result, lum * colR.rgb, mask.r);
            result = lerp(result, lum * colG.rgb, mask.g);
            result = lerp(result, lum * colB.rgb, mask.b);
            return result;
        }
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _OUTLINE_COLORED

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half   fogFactor  : TEXCOORD0;
                #ifdef _OUTLINE_COLORED
                float2 uv        : TEXCOORD1;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 smoothNormalOS = input.tangentOS.xyz;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                half3 smoothNormWS = (half3)TransformObjectToWorldNormal(smoothNormalOS);

                float4 posCS = TransformWorldToHClip(posWS);
                float3 clipNormal = mul((float3x3)UNITY_MATRIX_VP, smoothNormWS);

                float aspect = _ScreenParams.x * rcp(_ScreenParams.y);
                float2 screenOffset = normalize(clipNormal.xy);
                screenOffset.x *= rcp(aspect);
                posCS.xy += screenOffset * (_OutlineWidth * 0.001h) * posCS.w;

                o.positionCS = posCS;
                o.fogFactor = (half)ComputeFogFactor(posCS.z);

                #ifdef _OUTLINE_COLORED
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #endif

                return o;
            }

            half4 OutlineFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 color = _OutlineColor.rgb;

                #ifdef _OUTLINE_COLORED
                {
                    half3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                    half3 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).rgb;
                    half3 tinted = ApplyMaskTint(base, mask, _ColorR, _ColorG, _ColorB);
                    color = lerp(color, tinted * _OutlineColor.rgb, _OutlineTintBlend);
                }
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma shader_feature_local _RIM
            #pragma shader_feature_local _EMISSIVE

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3  normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS   = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv.xy      = TRANSFORM_TEX(input.uv, _BaseMap);
                o.uv.zw      = ToonTransformLightmapUV(input.lightmapUV);
                o.fogFactor  = (half)ComputeFogFactor(o.positionCS.z);

                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy) * _BaseColor;
                half3 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv.xy).rgb;
                half3 albedo = ApplyMaskTint(base.rgb, mask, _ColorR, _ColorG, _ColorB);

                half3 N = normalize(input.normalWS);
                float4 shadowCoord = ToonGetShadowCoordSimple(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 L = (half3)mainLight.direction;
                half NdotL = dot(N, L);
                half shadowAtten = (half)mainLight.shadowAttenuation;

                half cel = ToonCelRamp(NdotL * shadowAtten, _Threshold, _Smoothness);
                half3 color = albedo * lerp(_ShadowColor.rgb, (half3)mainLight.color, cel);

                #if defined(LIGHTMAP_ON)
                    color += SampleLightmap(input.uv.zw, 0.0, N) * albedo;
                #else
                    color += SampleSH(N) * albedo;
                #endif

                #if defined(_ADDITIONAL_LIGHTS)
                {
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; i++)
                    {
                        Light addLight = GetAdditionalLight(i, input.positionWS);
                        half addAtten = (half)(addLight.distanceAttenuation * addLight.shadowAttenuation);
                        half addNdotL = saturate(dot(N, (half3)addLight.direction));
                        half addCel = ToonCelRamp(addNdotL * addAtten, _Threshold, _Smoothness);
                        color += albedo * (half3)addLight.color * addCel;
                    }
                }
                #endif

                #ifdef _RIM
                {
                    half3 V = (half3)normalize(GetCameraPositionWS() - input.positionWS);
                    half NdotV = saturate(dot(N, V));
                    half fresnel = pow(1.0h - NdotV, _RimPower);
                    half rimLit = saturate(dot(N, L)) * shadowAtten;
                    half rim = smoothstep(_RimThreshold - _RimSmoothness, _RimThreshold + _RimSmoothness, fresnel * rimLit);
                    color += _RimColor.rgb * rim * _RimStrength * albedo;
                }
                #endif

                #ifdef _EMISSIVE
                {
                    half emMask = SAMPLE_TEXTURE2D(_EmissiveMask, sampler_EmissiveMask, input.uv.xy).r;
                    color = lerp(color, base.rgb * _EmissiveColor.rgb * _EmissiveStrength, emMask);
                }
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, base.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
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
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings  { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
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
                o.normalWS   = (half3)TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DNFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }
}
