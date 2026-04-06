Shader "CleanRender/UI/HueStrip"
{
    Properties
    {
        _Direction("Direction (0=X, 1=Y)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "HueStrip"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Direction;
            CBUFFER_END

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

            // HSV to RGB - standard conversion
            half3 HSVtoRGB(half h, half s, half v)
            {
                half3 rgb = saturate(abs(fmod(h * 6.0h + half3(0.0h, 4.0h, 2.0h), 6.0h) - 3.0h) - 1.0h);
                return v * lerp(half3(1.0h, 1.0h, 1.0h), rgb, s);
            }

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

                // Pick UV axis based on direction
                half t = (_Direction > 0.5) ? (half)input.uv.y : (half)input.uv.x;

                half3 color = HSVtoRGB(t, 0.85h, 1.0h);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
