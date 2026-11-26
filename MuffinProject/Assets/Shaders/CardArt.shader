// 3D 카드의 그림 칸 (시안 통합 UI v3.20 .card .art — 앞면 x 5.6 ~ 94.4 % · 위에서 15.6 ~ 60.1 %, 흰 바탕, 모서리 없음).
//   그림은 칸에 꽉 채워(cover) 넘치는 쪽을 자른다 — 자를 자리(_MainTex_ST)는 CardView 가 넣는다 (가로 가운데 · 세로는 위에서 30 %, 시안 object-position 50% 30%).
//   틀의 발바닥 배지 자리(틀 그림 원 (121, 117) r 109)는 뚫는다 — 아래 틀이 보여 시안 .bdg 처럼 배지가 그림 위로 온다.
//   그림이 아직 없는 카드(_HasTex 0)는 「그림 준비 중」 칸 (시안 v3.21 도감 .art.none): 종류 색 5 % 바탕 · 38 % 점선 테 · 30 % 발바닥. 글자는 CardView 의 TMP.
//   점선 테는 시안을 그린 크롬 그대로 — .7 cqw 테는 1 px 보다 얇아 1 px(2.05 u)로 올라가고, 얇은 테의 칸 · 틈은 두께 × 3 · × 2 다.
// 종류 색(_TypeColor) · 그림 · 흐림(_Filter)은 CardView 가 MaterialPropertyBlock 으로 넣는다. 길이는 u (캔버스 px), 칸 가운데가 0 · 위가 +.
Shader "Chapchu/Card Art"
{
    Properties
    {
        _MainTex ("Art", 2D) = "white" {}
        [MaterialToggle] _HasTex ("Has Art (끄면 그림 준비 중)", Float) = 1
        _Size ("Quad Size u (w, h)", Vector) = (200.69, 149.52, 0, 0)
        _Badge ("Badge Hole (x, y, 반지름 u)", Vector) = (-79.65, 95.0, 30.04, 0)
        _PendingDash ("Pending Dash (테 두께 u, 칸 u, 틈 u)", Vector) = (2.0545, 6.1636, 4.1091, 0)
        _PendingPaw ("Pending Paw (x, y, 폭 u)", Vector) = (0, 9.95, 42.14, 0)
        [HideInInspector] _TypeColor ("Type Color", Color) = (0, 0.616, 1, 1)
        [HideInInspector] _Filter ("Filter (채도, 밝기, 대비)", Vector) = (1, 1, 1, 0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CardFilter.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST, _Size, _Badge, _PendingDash, _PendingPaw;
            fixed4 _TypeColor;
            float _HasTex;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 p : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.p = (v.uv - 0.5) * _Size.xy;
                return o;
            }

            float Coverage(float d, float aa) { return saturate(0.5 - d / aa); }

            // 발바닥 (시안 PAW — viewBox 24, 가운데가 0 · 위가 +): 큰 발바닥 타원 하나 + 발가락 넷
            float PawDistance(float2 q)
            {
                float d = (length((q - float2(0.0, -4.0)) / float2(5.2, 4.4)) - 1.0) * 4.4;
                d = min(d, length(q - float2(-6.4, 1.6)) - 2.3);
                d = min(d, length(q - float2(-2.4, 5.6)) - 2.4);
                d = min(d, length(q - float2(2.4, 5.6)) - 2.4);
                d = min(d, length(q - float2(6.4, 1.6)) - 2.3);
                return d;
            }

            // 점선 — 크롬 dashed 처럼 변 양 끝이 칸이고, 칸 수는 틈이 기준 틈에 가장 가까운 쪽으로 고른다 (칸 길이는 그대로)
            float DashOn(float along, float sideLength, float dash, float gap, float aa)
            {
                float fewer = floor((sideLength + gap) / (dash + gap));
                float fewerGap = (sideLength - fewer * dash) / max(fewer - 1.0, 1.0);
                float moreGap = (sideLength - (fewer + 1.0) * dash) / max(fewer, 1.0);
                float best = moreGap <= 0.0 || abs(fewerGap - gap) < abs(moreGap - gap) ? fewerGap : moreGap;
                float period = dash + best;
                float t = frac(along / period) * period;
                float d = t <= dash ? -min(t, dash - t) : min(t - dash, period - t);
                float ends = step(along, dash) + step(sideLength - dash, along); // 양 끝 칸은 변 끝까지 꽉 — 칸 가장자리는 칸 테두리가 다듬는다
                return max(Coverage(d, aa), saturate(ends));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.p;
                float aa = max(0.5 * (length(ddx(p)) + length(ddy(p))), 1e-4);
                float2 halfBox = _Size.xy * 0.5;

                // 그림 — 흰 바탕 위에
                fixed4 tex = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw);
                float3 art = lerp(float3(1.0, 1.0, 1.0), tex.rgb, tex.a);

                // 그림 준비 중 — 바탕 · 점선 테 · 발바닥
                float3 tc = _TypeColor.rgb;
                float3 pending = lerp(float3(1.0, 1.0, 1.0), tc, 0.05);
                float2 edge = halfBox - abs(p);
                float w = _PendingDash.x;
                float sides = Coverage(edge.x - w, aa) * DashOn(p.y + halfBox.y, 2.0 * halfBox.y, _PendingDash.y, _PendingDash.z, aa);
                float ends = Coverage(edge.y - w, aa) * DashOn(p.x + halfBox.x, 2.0 * halfBox.x, _PendingDash.y, _PendingDash.z, aa);
                pending = lerp(pending, tc, 0.38 * max(sides, ends));
                float pawUnit = _PendingPaw.z / 24.0;
                float paw = Coverage(PawDistance((p - _PendingPaw.xy) / pawUnit) * pawUnit, aa);
                pending = lerp(pending, lerp(float3(1.0, 1.0, 1.0), tc, 0.3), paw);

                float3 color = _HasTex > 0.5 ? art : pending;

                // 칸 가장자리는 매끈하게 · 배지 자리는 뚫는다
                float2 q = abs(p) - halfBox;
                float alpha = Coverage(max(q.x, q.y), aa) * (1.0 - Coverage(length(p - _Badge.xy) - _Badge.z, aa));
                clip(alpha - 0.002);
                return fixed4(CardFilter(color) * alpha, alpha);
            }
            ENDCG
        }
    }
}
