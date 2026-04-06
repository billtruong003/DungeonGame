Shader "CleanRender/ToonLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white"{}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MinBaseColor("Min Base Color Clamp", Color) = (0.0784, 0.0784, 0.0784, 1.0)

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Toggle(_USE_LOCAL_TOON)] _UseLocalToon("Use Local Params", Float) = 0
        _ShadowColor("Shadow Color", Color) = (0.3, 0.3, 0.4, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.5
        _Smoothness("Shadow Smoothness", Range(0.001, 0.5)) = 0.05[Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 4
        _RimThreshold("Rim Threshold", Range(0.0, 1.0)) = 0.4
        _RimSmoothness("Rim Smoothness", Range(0.001, 0.5)) = 0.05
        _RimStrength("Rim Strength", Range(0.0, 5.0)) = 1.5

        [Header(Emissive Mask)][Toggle(_EMISSIVE_MASK)] _UseEmissiveMask("Use Emissive Mask", Float) = 0
        _EmissiveMaskMap("Emissive Mask (R)", 2D) = "black"{}
        _EmissiveMaskStrength("Mask Strength", Range(0.0, 2.0)) = 1.0

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Toggle(_EMISSION)] _Emission("Emission", Float) = 0[HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0)
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
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/Shaders/CleanRenderPipeline/Core/Shaders/Includes/ToonLighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _MinBaseColor;
            half4  _ShadowColor;
            half4  _RimColor;
            float  _Threshold;
            float  _Smoothness;
            float  _RimPower;
            float  _RimThreshold;
            float  _RimSmoothness;
            float  _RimStrength;
            float  _Cutoff;
            half   _EmissiveMaskStrength;
            half4  _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);         SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);     SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_EmissiveMaskMap); SAMPLER(sampler_EmissiveMaskMap);

        half DirectionalRim(half3 normalWS, half3 viewDirWS, half3 lightDir,
                            half shadowAtten, half power, half threshold, half smooth, half strength)
        {
            half NdotV = saturate(dot(normalWS, viewDirWS));
            half fresnel = pow(1.0h - NdotV, power);
            half NdotL = saturate(dot(normalWS, lightDir));
            half rim = fresnel * NdotL * shadowAtten;
            rim = smoothstep(threshold - smooth, threshold + smooth, rim);
            return rim * strength;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

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

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _EMISSIVE_MASK
            #pragma shader_feature_local _USE_LOCAL_TOON

            struct Attributes
            {
                float4 positionOS  : POSITION;
                float3 normalOS    : NORMAL;
                float2 uv          : TEXCOORD0;
                float2 lightmapUV  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD1;
                half3  normalWS    : TEXCOORD2;
                float4 uv          : TEXCOORD0;
                half   fogFactor   : TEXCOORD3;
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
                o.normalWS   = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv.xy      = TRANSFORM_TEX(input.uv, _BaseMap);
                o.uv.zw      = ToonTransformLightmapUV(input.lightmapUV);
                o.fogFactor  = (half)ComputeFogFactor(o.positionCS.z);

                return o;
            }

            half4 ToonLitFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv.xy) * _BaseColor;
                albedo.rgb = max(albedo.rgb, _MinBaseColor.rgb);

                #ifdef _ALPHATEST_ON
                    clip(albedo.a - _Cutoff);
                #endif

                half3 N = normalize(input.normalWS);
                float3 V = normalize(GetCameraPositionWS() - input.positionWS);

                float4 shadowCoord = ToonGetShadowCoordSimple(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 L = (half3)mainLight.direction;
                half NdotL = dot(N, L);
                half shadowAtten = (half)mainLight.shadowAttenuation;

                #ifdef _USE_LOCAL_TOON
                    half threshold = _Threshold;
                    half smooth = _Smoothness;
                    half3 shadowCol = _ShadowColor.rgb;
                #else
                    half threshold = _Threshold;
                    half smooth = _Smoothness;
                    half3 shadowCol = _ShadowColor.rgb;
                #endif

                half cel = ToonCelRamp(NdotL * shadowAtten, threshold, smooth);
                half3 diffuse = albedo.rgb * lerp(shadowCol, (half3)mainLight.color, cel);

                half3 gi;
                #if defined(LIGHTMAP_ON)
                    gi = SampleLightmap(input.uv.zw, 0.0, N) * albedo.rgb;
                #else
                    gi = SampleSH(N) * albedo.rgb;
                #endif

                half rimFactor = DirectionalRim(
                    N, (half3)V, L, shadowAtten,
                    _RimPower, _RimThreshold, _RimSmoothness, _RimStrength);
                half3 rim = _RimColor.rgb * rimFactor * albedo.rgb;

                half3 finalColor = diffuse + gi + rim;

                #if defined(_ADDITIONAL_LIGHTS)
                {
                    uint lightCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < lightCount; i++)
                    {
                        Light addLight = GetAdditionalLight(i, input.positionWS);
                        half addAtten = (half)(addLight.distanceAttenuation * addLight.shadowAttenuation);
                        half addNdotL = saturate(dot(N, (half3)addLight.direction));
                        half addCel = ToonCelRamp(addNdotL * addAtten, threshold, smooth);
                        finalColor += albedo.rgb * (half3)addLight.color * addCel;
                    }
                }
                #endif

                #if defined(LIGHTMAP_SHADOW_MIXING) && defined(LIGHTMAP_ON)
                    diffuse *= shadowAtten;
                #endif

                #ifdef _EMISSIVE_MASK
                    half mask = SAMPLE_TEXTURE2D(_EmissiveMaskMap, sampler_EmissiveMaskMap, input.uv.xy).r;
                    finalColor += albedo.rgb * mask * _EmissiveMaskStrength;
                #endif

                #ifdef _EMISSION
                    finalColor += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv.xy).rgb * _EmissionColor.rgb;
                #endif

                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, albedo.a);
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
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
                #endif
                return 0;
            }
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
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };

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

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
                float2 uv         : TEXCOORD1;
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
                float2 uv0        : TEXCOORD0;
                float2 uv1        : TEXCOORD1;
                float2 uv2        : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
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

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = max(base.rgb * _BaseColor.rgb, _MinBaseColor.rgb);

                #ifdef _EMISSION
                    metaInput.Emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
}