// 개판 오분전 눈 마을의 강아지 부품 (시안 D3R 의 SFS — 햇빛 색 · 종이 결 · 안개, 미리 곱한 알파).
// 깊이를 쓴다 — 눈뭉치 · 다른 강아지와 앞뒤가 맞게. 부품 순서는 강아지마다 SortingGroup 의 순서로.
Shader "Chapchu/CardFx/Gaepan Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Tint ("Tint (해를 받는 색)", Color) = (1, 0.985, 0.95, 1)
        _Cutoff ("Cutoff (이보다 투명하면 버린다)", Range(0, 1)) = 0.04
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite On
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "GaepanCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Tint;
            float _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.worldPos = world.xyz;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv) * i.color;
                clip(c.a - _Cutoff);
                float3 col = c.rgb * c.a * _Tint.rgb;
                col = GpPaper(col, GpScreenPx(i.screenPos));
                return fixed4(lerp(col, _GpFog.rgb * c.a, GpFogAmount(i.worldPos)), c.a);
            }
            ENDCG
        }
    }
}
