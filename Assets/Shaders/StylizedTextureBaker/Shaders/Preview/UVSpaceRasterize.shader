Shader "Hidden/StylizedBaker/UVSpaceRasterize"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            float2 uv2 : TEXCOORD2;
            float2 uv3 : TEXCOORD3;
        };

        struct v2f
        {
            float4 pos : SV_POSITION;
            float3 objectNormal : TEXCOORD0;
            float3 objectPos : TEXCOORD1;
            float4 vertexColor : TEXCOORD2;
            float2 extraUV2 : TEXCOORD3;
            float2 extraUV3 : TEXCOORD4;
            float3 bary : TEXCOORD5;
        };

        v2f vertBase(appdata v)
        {
            v2f o;
            o.pos = float4(v.uv.x * 2.0 - 1.0, v.uv.y * 2.0 - 1.0, 0.5, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
            o.pos.y = -o.pos.y;
            #endif
            o.objectNormal = v.normal;
            o.objectPos = v.vertex.xyz;
            o.vertexColor = v.color;
            o.extraUV2 = v.uv2;
            o.extraUV3 = v.uv3;
            o.bary = float3(0, 0, 0);
            return o;
        }
        ENDCG

        Pass
        {
            Name "BakeCurvature"
            CGPROGRAM
            #pragma vertex vertBase
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float curvature = i.vertexColor.r * 2.0 - 1.0;
                float convex = max(curvature, 0.0);
                float concave = max(-curvature, 0.0);
                return float4(convex, concave, 0, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "BakeNormal"
            CGPROGRAM
            #pragma vertex vertBase
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.objectNormal);
                return float4(n * 0.5 + 0.5, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "BakePosition"
            CGPROGRAM
            #pragma vertex vertBase
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                return float4(i.objectPos, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "BakeEdgeMask"
            CGPROGRAM
            #pragma vertex vertBase
            #pragma fragment frag
            float frag(v2f i) : SV_Target
            {
                float edgeMask = i.vertexColor.g;
                float2 dx = ddx(float2(edgeMask, 0));
                float2 dy = ddy(float2(edgeMask, 0));
                float gradient = sqrt(dx.x * dx.x + dy.x * dy.x);
                float sharpened = saturate(edgeMask * (1.0 + gradient * 8.0));
                return sharpened;
            }
            ENDCG
        }

        Pass
        {
            Name "BakeDirectionalField"
            CGPROGRAM
            #pragma vertex vertBase
            #pragma fragment frag
            float4 frag(v2f i) : SV_Target
            {
                float3 dir = float3(
                    i.extraUV2.x * 2.0 - 1.0,
                    i.extraUV2.y * 2.0 - 1.0,
                    i.extraUV3.x * 2.0 - 1.0
                );
                dir = normalize(dir);
                return float4(dir.x * 0.5 + 0.5, dir.y * 0.5 + 0.5, 0, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "BakeUVIslandMask"
            CGPROGRAM
            #pragma vertex vertBase
            #pragma fragment frag
            float frag(v2f i) : SV_Target
            {
                return 1.0;
            }
            ENDCG
        }
    }
}
