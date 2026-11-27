// 3D 카드의 그림자 — 종이 모양을 아래로 밀어 흐리게 (시안 통합 UI v3.20 .card::before box-shadow 0 8px 12px rgba(70,45,30,.3)).
// 둥근 사각형 거리장에 흐림을 준다 — CSS 흐림 반지름 B(가우스 σ = B/2)를 ±B 의 smoothstep 으로. 길이는 u (캔버스 px, 1 유닛 = 100). 양면.
// 쉴 때 값은 머티리얼, 포인터 아래 · 고른 카드의 값(_Offset · _Blur · _Color 알파)과 흐림(_Filter)은 CardView 가 MaterialPropertyBlock 으로 넣는다.
// 그림자는 카드보다 뒤에 놓여 바로 뒤 카드(장당 0.02)보다도 멀 수 있다 — 깊이 비교 없이(Always) 카드의 SortingGroup 순서로 뒤 카드 위에 그린다 (시안도 깊이 비교 없음).
//
// 등급 연출(10-ui.md 2-1 「카드 등급 연출」, 시안 「카드 반짝 · 빛 반사 v3」 1안)도 이 셰이더로 그린다 — CardView 가 그림자 판을 복제해 _Mode 를 바꾼다.
//   _Mode 1 글로우: 그림자와 같은 흐린 둥근 사각형을 카드 뒤에. _Grad.w 가 1 이면 색이 세 마디 직선 그라디언트(_Color → _Color2 → _Color3, 방향 _Grad.xy · 길이 _Grad.z).
//   _Mode 2 반짝이: 카드 둘레의 반짝이 _SparkCount 개 (최대 11) — 네 갈래 별(흰 심 · 색 테 · 색 번짐) + 가장 밝을 때 십자 빛줄기, 또는 두 번 깜빡이는 빛 알갱이.
//          반짝이마다 _SparkA = (x, y 카드 가운데 기준 u, 반지름 u, 회전), _SparkB = (색 rgb, 알갱이면 1), _SparkC = (별 크기 배율, 빛줄기 0 ~ 1, 알갱이 밝기 0 ~ 1). 모두에 _Color.a 를 곱한다.
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
        [HideInInspector] _Mode ("Mode (0 그림자 · 1 글로우 · 2 반짝이)", Float) = 0
        [HideInInspector] _Color2 ("Gradient Middle", Color) = (1, 1, 1, 1)
        [HideInInspector] _Color3 ("Gradient End", Color) = (1, 1, 1, 1)
        [HideInInspector] _Grad ("Gradient (방향 x, y, 길이 u, 켬)", Vector) = (0, 1, 1, 0)
        [HideInInspector] _SparkCount ("Spark Count", Float) = 0
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

            #define MAX_SPARKS 11

            fixed4 _Color, _Color2, _Color3;
            float4 _Size, _Half, _Grad;
            float _Radius, _Offset, _Blur, _Mode, _SparkCount;
            float4 _SparkA[MAX_SPARKS], _SparkB[MAX_SPARKS], _SparkC[MAX_SPARKS];

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

            float Coverage(float d, float aa) { return saturate(0.5 - d / aa); }

            // 미리 곱한 알파로 위에 얹는다
            float4 Over(float4 below, float3 color, float alpha)
            {
                return float4(color * alpha + below.rgb * (1.0 - alpha), alpha + below.a * (1.0 - alpha));
            }

            // 색 번짐 — 가운데 a, 반지름 .45 에서 a × .45, 끝 0 (시안 halo 의 원형 그라디언트)
            float Halo(float dist, float radius)
            {
                float t = dist / max(radius, 1e-4);
                return t >= 1.0 ? 0.0 : t < 0.45 ? lerp(1.0, 0.45, t / 0.45) : lerp(0.45, 0.0, (t - 0.45) / 0.55);
            }

            // 글로우 색 — 단색 또는 세 마디 그라디언트 (CSS linear-gradient 와 같이 상자를 덮는 선 위에서)
            float3 GlowColor(float2 p)
            {
                if (_Grad.w < 0.5) return _Color.rgb;
                float t = saturate(dot(p, _Grad.xy) / max(_Grad.z, 1e-4) + 0.5);
                return t < 0.5 ? lerp(_Color.rgb, _Color2.rgb, t * 2.0) : lerp(_Color2.rgb, _Color3.rgb, t * 2.0 - 1.0);
            }

            float4 Sparks(float2 p, float aa)
            {
                float4 c = 0;
                for (int i = 0; i < MAX_SPARKS; i++)
                {
                    if (i >= (int)_SparkCount) break;

                    float4 a = _SparkA[i], b = _SparkB[i], k = _SparkC[i];
                    float2 d = p - a.xy;
                    float r = a.z;
                    float dist = length(d);
                    if (b.w > 0.5)
                    {
                        // 빛 알갱이 — 번짐 반지름 3.2r · 흰 심 .6r, 둘 다 밝기 k.z
                        c = Over(c, b.rgb, 0.9 * k.z * Halo(dist, r * 3.2));
                        c = Over(c, float3(1.0, 1.0, 1.0), k.z * Coverage(dist - r * 0.6, aa));
                        continue;
                    }

                    // 십자 빛줄기 — 길이 2.6r × k.y, 굵기 1.3 u
                    if (k.y > 0.001)
                    {
                        float len = r * 2.6 * k.y;
                        float2 q = abs(d);
                        float lineX = length(float2(max(q.x - len, 0.0), q.y)) - 0.65;
                        float lineY = length(float2(q.x, max(q.y - len, 0.0))) - 0.65;
                        c = Over(c, b.rgb, 0.85 * k.y * Coverage(min(lineX, lineY), aa));
                    }

                    // 네 갈래 별 — 크기 r × k.x, 번짐 1.9 배
                    float rs = r * k.x;
                    if (rs <= 0.3) continue;
                    c = Over(c, b.rgb, 0.7 * Halo(dist, rs * 1.9));
                    float cs = cos(a.w), sn = sin(a.w);
                    float2 l = float2(cs * d.x + sn * d.y, -sn * d.x + cs * d.y) / rs;
                    float star = (pow(abs(l.x), 0.55) + pow(abs(l.y), 0.55) - 1.0) * rs * 0.45; // 오목한 네 갈래 (시안 quadraticCurve 제어점 .17r 과 비슷한 굽이) — 대략의 거리 u
                    float stroke = max(0.8, rs * 0.12);
                    c = Over(c, float3(1.0, 1.0, 1.0), Coverage(star, aa));
                    c = Over(c, b.rgb, 0.95 * Coverage(abs(star) - stroke * 0.5, aa));
                }
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * _Size.xy;
                float aa = max(0.5 * (length(ddx(p)) + length(ddy(p))), 1e-4); // 분기 밖에서 — 분기 안에서는 미분이 안 된다
                if (_Mode > 1.5)
                {
                    float4 c = Sparks(p, aa) * _Color.a;
                    clip(c.a - 0.002);
                    return fixed4(c.rgb / c.a, c.a);
                }

                p += float2(0.0, _Offset);
                float d = sdRoundRect(p, _Half.xy, _Radius);
                float blur = max(_Blur, fwidth(d));
                float a = _Color.a * smoothstep(blur, -blur, d);
                clip(a - 0.002);
                return fixed4(_Mode > 0.5 ? GlowColor(p) : CardFilter(_Color.rgb), a);
            }
            ENDCG
        }
    }
}
