Shader "Hidden/StylizedBaker/DataMapPreview"
{
    Properties
    {
        _MainTex ("Data Map", 2D) = "black" {}
        _ChannelMode ("Channel Mode", Float) = 0
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
            Name "URPDataMap"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _ChannelMode;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float4 data = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                if (_ChannelMode < 0.5)
                    return half4(data.r, data.r, data.r, 1);

                if (_ChannelMode < 1.5)
                    return half4(data.r, data.g, 0, 1);

                return half4(data.rgb, 1);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "BuiltinDataMap"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float _ChannelMode;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 data = tex2D(_MainTex, i.uv);

                if (_ChannelMode < 0.5)
                    return fixed4(data.r, data.r, data.r, 1);

                if (_ChannelMode < 1.5)
                    return fixed4(data.r, data.g, 0, 1);

                return fixed4(data.rgb, 1);
            }
            ENDCG
        }
    }
}
