Shader "CleanRender/UI/SmartToneStrip"
{
    Properties
    {
        _CurrentHue("Current Hue", Range(0, 1)) = 0.0
        _PastelSaturation("Pastel Saturation", Range(0, 1)) = 0.70
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
            Name "SmartToneStrip"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half  _CurrentHue;
                half  _PastelSaturation;
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

            half3 HSVtoRGB(half h, half s, half v)
            {
                half3 rgb = saturate(abs(fmod(h * 6.0h + half3(0.0h, 4.0h, 2.0h), 6.0h) - 3.0h) - 1.0h);
                return v * lerp(half3(1.0h, 1.0h, 1.0h), rgb, s);
            }

            // Smart Tone curve:
            //   t = 0.0 (bottom) → S = pastelSat, V = 0.0  → Black
            //   t = 0.5 (middle) → S = pastelSat, V = 1.0  → Pastel color
            //   t = 1.0 (top)    → S = 0.0,       V = 1.0  → White
            void SmartToneCurve(half t, half pastelSat, out half saturation, out half value)
            {
                // Lower half: V ramps 0→1, S stays at pastel
                // Upper half: V stays 1, S ramps pastelSat→0
                half tLower = saturate(t * 2.0h);          // 0→1 over bottom half
                half tUpper = saturate((t - 0.5h) * 2.0h); // 0→1 over top half

                value      = tLower;                                     // 0→1 in bottom, clamped to 1 in top
                saturation = lerp(pastelSat, 0.0h, tUpper);             // pastelSat in bottom half, ramp to 0 in top
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

                half t = (_Direction > 0.5) ? (half)input.uv.y : (half)input.uv.x;

                half s, v;
                SmartToneCurve(t, _PastelSaturation, s, v);

                half3 color = HSVtoRGB(_CurrentHue, s, v);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
