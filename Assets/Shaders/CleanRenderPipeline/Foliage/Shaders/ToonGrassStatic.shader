Shader "CleanRender/ToonGrass Static"
{
    Properties
    {
        [MainTexture] _BaseMap("Grass Texture", 2D) = "white"{}
        _BaseColor("Base Color (Bottom)", Color) = (0.2, 0.4, 0.1, 1)
        _TipColor("Tip Color (Top)", Color) = (0.5, 0.85, 0.2, 1)

        [Header(Cel Shading)]
        _ShadowColor("Shadow Color", Color) = (0.1, 0.2, 0.05, 1)
        _Threshold("Shadow Threshold", Range(0, 1)) = 0.4

        [Header(Geometry)]
        _GrassHeight("Grass Height (for gradient)", Range(0.1, 3)) = 0.8

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }
        LOD 100
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half4  _TipColor;
            half4  _ShadowColor;
            half   _Threshold;
            half   _GrassHeight;
            half   _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        // ============================================================
        // Pass 0: ForwardLit
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS     : SV_POSITION;
                float3 positionWS     : TEXCOORD1;
                half3  normalWS       : TEXCOORD2;
                float2 uv             : TEXCOORD0;
                half   heightGradient : TEXCOORD3;
                half   fogFactor      : TEXCOORD4;
                #ifdef LIGHTMAP_ON
                float2 lightmapUV     : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS     = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS     = TransformWorldToHClip(o.positionWS);
                o.normalWS       = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv             = TRANSFORM_TEX(input.uv, _BaseMap);
                o.heightGradient = saturate(input.positionOS.y * rcp(max(_GrassHeight, 1e-4h)));
                o.fogFactor      = (half)ComputeFogFactor(o.positionCS.z);

                #ifdef LIGHTMAP_ON
                    o.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif

                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(tex.a - _Cutoff);

                half3 color = lerp(_BaseColor.rgb, _TipColor.rgb, input.heightGradient) * tex.rgb;

                half3 N = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                half NdotL = dot(N, (half3)mainLight.direction);
                half shadow = (half)mainLight.shadowAttenuation;
                half intensity = smoothstep(_Threshold - 0.05h, _Threshold + 0.05h, NdotL * shadow);

                color *= lerp(_ShadowColor.rgb, (half3)mainLight.color, intensity);

                // GI
                #ifdef LIGHTMAP_ON
                    color += SampleLightmap(input.lightmapUV, 0.0, N) * color * 0.3h;
                #else
                    color += SampleSH(N) * color * 0.3h;
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 1: ShadowCaster
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 2: DepthOnly
        // ============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 3: DepthNormals
        // ============================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DNVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS   = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 DNFrag(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return half4(normalize(input.normalWS), 0.0h);
            }
            ENDHLSL
        }

        // ============================================================
        // Pass 4: Meta (lightmap baking)
        // ============================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex MetaVert
            #pragma fragment MetaFrag

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
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo   = albedo.rgb;
                metaInput.Emission = half3(0, 0, 0);
                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }
}
