Shader "CleanRender/UI/TargetPartIcon"
{
    Properties
    {
        _IconArray("Icon Array", 2DArray) = "" {}
        _SliceIndex("Part Index", Float) = 0
        _BaseColor("Tint", Color) = (1, 1, 1, 1)
        _SelectedColor("Selected Highlight", Color) = (1, 0.9, 0.3, 1)
        _IsSelected("Is Selected", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "TargetPartIcon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma require 2darray
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SelectedColor;
                float _SliceIndex;
                float _IsSelected;
            CBUFFER_END

            TEXTURE2D_ARRAY(_IconArray);
            SAMPLER(sampler_IconArray);

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 uvw = float3(input.uv, _SliceIndex);
                half4 icon = SAMPLE_TEXTURE2D_ARRAY(_IconArray, sampler_IconArray, uvw.xy, uvw.z);

                // Tint: blend between base and selected color
                half4 tint = lerp(_BaseColor, _SelectedColor, (half)_IsSelected);
                icon.rgb *= tint.rgb;
                icon.a   *= tint.a;

                return icon;
            }
            ENDHLSL
        }
    }
}
