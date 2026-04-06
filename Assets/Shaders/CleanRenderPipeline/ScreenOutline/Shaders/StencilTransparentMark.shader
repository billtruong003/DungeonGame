Shader "Hidden/CleanRender/StencilTransparentMark"
{
    // This shader is used as an override material by the OutlineFeature
    // to mark transparent objects in the stencil buffer.
    // It writes ONLY stencil — no color, no depth.
    // Used during a dedicated stencil-only pass that leverages
    // Unity's existing culling results (no extra CPU cull cost).

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StencilMark"

            // Write stencil bit 1
            Stencil
            {
                Ref 1
                WriteMask 1
                Comp Always
                Pass Replace
            }

            // No color or depth output — stencil only
            ColorMask 0
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED UNITY_STEREO_MULTIVIEW_ENABLED

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0; // ColorMask 0 → this never writes
            }
            ENDHLSL
        }
    }
}
