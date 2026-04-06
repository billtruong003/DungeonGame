Shader "CleanRender/PlaneFog"
{
    Properties
    {
        [HDR] _FogColor("Fog Color", Color) = (0.5, 0.6, 0.8, 1)
        _FogStart("Fog Start Distance", Float) = 2.0
        _FogStrength("Fog Density", Range(0.001, 2.0)) = 0.05
        _SkyboxAmp("Skybox Amplifier", Range(0.0, 5.0)) = 1.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Overlay" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Off
        Cull Off

        Pass
        {
            Name "PlaneFogPass"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                float _FogStart;
                float _FogStrength;
                half  _SkyboxAmp;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                float planeDepth = input.screenPos.w;

                #if UNITY_REVERSED_Z
                    half isSky = step(rawDepth, 1e-6h);
                #else
                    half isSky = step(0.999999h, rawDepth);
                #endif

                float depthDiff = max(0.0, sceneDepth - planeDepth - _FogStart);
                half fogFactor = saturate((half)(depthDiff * _FogStrength));

                fogFactor = lerp(fogFactor, saturate(fogFactor * _SkyboxAmp), isSky);

                return half4(_FogColor.rgb, fogFactor * _FogColor.a);
            }
            ENDHLSL
        }
    }
}