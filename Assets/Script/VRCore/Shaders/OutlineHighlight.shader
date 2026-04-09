Shader "VRCore/OutlineHighlight"
{
    Properties
    {
        _Color ("Outline Color", Color) = (0.3, 0.8, 1.0, 0.6)
        _Width ("Outline Width", Range(0.001, 0.05)) = 0.005
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Width;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 expandedPos = input.positionOS.xyz + input.normalOS * _Width;
                output.positionCS = TransformObjectToHClip(expandedPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
