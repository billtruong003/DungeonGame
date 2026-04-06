Shader "CleanRender/UI/ToonMirror"
{
    Properties
    {
        _MainTex("Mirror Texture", 2D) = "white" {}
        _NoiseScale("Noise Scale", Range(0.0, 50.0)) = 12.0
        _NoiseStrength("Noise Strength", Range(0.0, 0.02)) = 0.003
        _NoiseSpeed("Noise Speed", Range(0.0, 5.0)) = 0.8
        _Tint("Tint", Color) = (0.95, 0.97, 1.0, 1.0)
        _Vignette("Vignette Strength", Range(0.0, 1.0)) = 0.15
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
            Name "MirrorSurface"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Tint;
                float  _NoiseScale;
                float  _NoiseStrength;
                float  _NoiseSpeed;
                half   _Vignette;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

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

            // Simple hash-based noise — no texture needed, zero bandwidth
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;

                // Subtle UV distortion from animated noise
                float time = _Time.y * _NoiseSpeed;
                float2 noiseUV = uv * _NoiseScale + float2(time, time * 0.7);
                float nx = ValueNoise(noiseUV) * 2.0 - 1.0;
                float ny = ValueNoise(noiseUV + 100.0) * 2.0 - 1.0;
                uv += float2(nx, ny) * _NoiseStrength;

                half4 mirror = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Tint
                mirror.rgb *= _Tint.rgb;

                // Soft vignette at edges
                float2 centered = uv * 2.0 - 1.0;
                float vigFactor = 1.0 - dot(centered, centered) * _Vignette;
                mirror.rgb *= saturate(vigFactor);

                return half4(mirror.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
