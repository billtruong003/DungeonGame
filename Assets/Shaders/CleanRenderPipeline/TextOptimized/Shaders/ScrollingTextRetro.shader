Shader "Custom/ScrollingTextRetro"
{
    Properties
    {
        _MainTex        ("Text Texture", 2D) = "black" {}

        [Header(UV)]
        _UVRotation     ("Rotation (degrees)", Range(0, 360)) = 0
        _UVScaleX       ("Scale X", Range(0.1, 20)) = 1
        _UVScaleY       ("Scale Y", Range(0.1, 20)) = 1

        [Header(Scroll)]
        _ScrollSpeed    ("Speed", Range(0.05, 5.0)) = 0.5
        [Toggle] _ScrollRight ("Right", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        ZWrite On
        Cull Back

        Pass
        {
            Name "ScrollTextRetro"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            half _UVRotation;
            half _UVScaleX;
            half _UVScaleY;
            half _ScrollSpeed;
            half _ScrollRight;

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
            };

            float2 RotateUV(float2 uv, float deg)
            {
                float r = radians(deg);
                float s, c;
                sincos(r, s, c);
                uv -= 0.5;
                return float2(uv.x * c - uv.y * s,
                              uv.x * s + uv.y * c) + 0.5;
            }

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.posCS = TransformObjectToHClip(IN.posOS.xyz);
                o.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = RotateUV(IN.uv, _UVRotation);
                uv = float2(uv.x * _UVScaleX, uv.y * _UVScaleY);

                half dir = lerp(-1.0h, 1.0h, _ScrollRight);
                uv.x += _Time.y * _ScrollSpeed * dir;

                half3 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
