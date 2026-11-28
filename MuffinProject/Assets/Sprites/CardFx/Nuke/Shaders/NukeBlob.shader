// 대폭발 3D 장면 — 강아지 발밑에 깔리는 둥근 그림자 (시안 D3R 아틀라스의 __shadow: rgba(50,60,110) 가운데 .6 → .55 에서 .3 → 끝 0).
// uv = 판 안의 자리 (0~1), uv1.y = 알파.
Shader "Chapchu/Nuke/Blob"
{
    Properties
    {
        _Color ("그림자 색", Color) = (0.196, 0.235, 0.431, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "AlphaTest-1" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 info : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float a : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2.0 - 1.0;
                o.a = v.info.y;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                float a = r < 0.55 ? lerp(0.6, 0.3, r / 0.55) : lerp(0.3, 0.0, saturate((r - 0.55) / 0.45));
                a *= i.a;
                return float4(_Color.rgb * a, a);
            }
            ENDCG
        }
    }
}
