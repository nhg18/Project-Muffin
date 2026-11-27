// 3D 카드의 그림자 — 종이 모양을 아래로 밀어 흐리게 (시안 통합 UI v3.20 .card::before box-shadow 0 8px 12px rgba(70,45,30,.3)).
// 둥근 사각형 거리장에 흐림을 준다 — CSS 흐림 반지름 B(가우스 σ = B/2)를 ±B 의 smoothstep 으로. 길이는 u (캔버스 px, 1 유닛 = 100). 양면.
// 쉴 때 값은 머티리얼, 포인터 아래 · 고른 카드의 값(_Offset · _Blur · _Color 알파)과 흐림(_Filter)은 CardView 가 MaterialPropertyBlock 으로 넣는다.
// 그림자는 카드보다 뒤에 놓여 바로 뒤 카드(장당 0.02)보다도 멀 수 있다 — 깊이 비교 없이(Always) 카드의 SortingGroup 순서로 뒤 카드 위에 그린다 (시안도 깊이 비교 없음).
Shader "Chapchu/Card Soft"
{
    Properties
    {
        _Color ("Color", Color) = (0.275, 0.176, 0.118, 0.3)
        _Size ("Quad Size u (w, h)", Vector) = (361.94, 543.94, 0, 0)
        _Half ("Shape Half Size u", Vector) = (126.97, 181.97, 0, 0)
        _Radius ("Corner Radius u", Float) = 13.77
        _Offset ("Offset u (아래로)", Float) = 16.39
        _Blur ("Blur u (CSS 흐림 반지름, 0 = 가장자리만 매끈하게)", Float) = 24.65
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (그림자는 Always — 뒤 카드 위에 진다)", Float) = 4
        [HideInInspector] _Filter ("Filter (채도, 밝기, 대비)", Vector) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [_ZTest]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CardFilter.cginc"

            fixed4 _Color;
            float4 _Size, _Half;
            float _Radius, _Offset, _Blur;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * _Size.xy + float2(0.0, _Offset);
                float d = sdRoundRect(p, _Half.xy, _Radius);
                float blur = max(_Blur, fwidth(d));
                float a = _Color.a * smoothstep(blur, -blur, d);
                clip(a - 0.002);
                return fixed4(CardFilter(_Color.rgb), a);
            }
            ENDCG
        }
    }
}
