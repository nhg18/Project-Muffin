// 3D 카드 모양의 반투명 판 — 그림자(흐림) · 낼 수 없는 카드를 덮는 어두운 판(흐림 없음) (10-ui.md 2-1).
// 둥근 사각형 거리장에 흐림을 준다. 길이는 캔버스 px (1 유닛 = 100). 양면.
// 그림자는 카드보다 뒤에 놓여 바로 뒤 카드(장당 0.02)보다도 멀 수 있다 — 깊이 비교 없이(Always) 카드의 SortingGroup 순서로 뒤 카드 위에 그린다 (시안도 깊이 비교 없음).
Shader "Chapchu/Card Soft"
{
    Properties
    {
        _Color ("Color", Color) = (0.165, 0.141, 0.125, 0.3)
        _Size ("Quad Size px (w, h)", Vector) = (364.5, 474.5, 0, 0)
        _Half ("Shape Half Size px", Vector) = (126.84, 181.84, 0, 0)
        _Radius ("Corner Radius px", Float) = 13.84
        _Blur ("Blur px (0 = 가장자리만 매끈하게)", Float) = 27.7
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest (그림자는 Always — 뒤 카드 위에 진다)", Float) = 4
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

            fixed4 _Color;
            float4 _Size, _Half;
            float _Radius, _Blur;

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
                float2 p = (i.uv - 0.5) * _Size.xy;
                float d = sdRoundRect(p, _Half.xy, _Radius);
                float blur = max(_Blur, fwidth(d));
                float a = _Color.a * smoothstep(blur, -blur, d);
                clip(a - 0.002);
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
