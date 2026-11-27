// 3D 카드의 종이 한 면 — 앞면 · 뒷면 (시안 통합 UI v3.20 카드 4번 .card, 앞면 110 × 164 px → 226 × 336 u).
//   아래부터: 종이 두께 띠 3 겹(종이를 아래로 1 · 2 · 3 px 민 모양, 시안 box-shadow) → 흰 종이(앞면보다 사방 6.8 px 큰 둥근 사각형)
//   → 앞면 칸의 그림(틀 · 뒷면 그림, 모서리 1.5 px) → 노란 형광펜(고른 카드 · 놓으면 사용 — 종이 테에서 바깥으로 12 u, 흔들림)
//   → 잉크 선 한 줄(종이 테 위 3 u, 살짝 흔들림) → 등급 수만큼 낙서 별 (일반 0 · 고급 1 · 희귀 2 · 개판 3) → 등급 표면 빛 (CardFilter.cginc CardSheen).
// 형광펜(_Mark) · 별 수(_Grade) · 별 보이기(_StarShow) · 흐림(_Filter) · 표면 빛(_Sheen*)은 CardView 가 MaterialPropertyBlock 으로 넣는다.
// 별은 등급 연출과 같이 보이고 숨는다 — 손패에서는 숨긴다 (10-ui.md 2-1 「카드 등급 연출」). 머티리얼 기본값은 보임(1) — 등급 연출을 모르는 화면은 그대로. 뒷면은 형광펜 · 별 없이 종이 · 뒷면 그림 · 잉크 선만.
// 모양 값(_Size · _Paper · _Face · _Star*)은 TableBuilder 가 카드 크기에서 계산해 넣는다. 길이는 전부 u (캔버스 px, 1 유닛 = 100).
// 흔들림 = SVG feTurbulence(fractalNoise) + feDisplacementMap 흉내 — 잉크 cdwobs(주파수 0.02 · 1 겹 · 세기 1.6) · 형광펜 cdwob(0.03 · 2 겹 · 3.4).
// 깊이는 종이에만 쓰고(첫 패스) 색은 미리 곱한 알파로 섞어 그린다(둘째 패스) — 앞 층 렌더 텍스처(투명 바탕)에서도 덮인 정도가 맞다.
Shader "Chapchu/Card Paper"
{
    Properties
    {
        _MainTex ("Face Texture (틀 · 뒷면 그림)", 2D) = "white" {}
        _Size ("Quad Size u (w, h)", Vector) = (294, 416, 0, 0)
        _Paper ("Paper (반폭 x, 반높이 y, 모서리 u)", Vector) = (126.97, 181.97, 13.77, 0)
        _Face ("Face (반폭 x, 반높이 y, 모서리 u)", Vector) = (113, 168, 3.08, 0)
        _ThickStep ("Thickness Step u (두께 띠 한 겹 — 시안 1 px)", Float) = 2.05
        _Thick1 ("Thickness 1 (시안 #f1ebe1)", Color) = (0.945, 0.922, 0.882, 1)
        _Thick2 ("Thickness 2 (시안 #e6dccb)", Color) = (0.902, 0.863, 0.796, 1)
        _Thick3 ("Thickness 3 (시안 #d8cab4)", Color) = (0.847, 0.792, 0.706, 1)
        _InkColor ("Ink Color (시안 #2b2856)", Color) = (0.169, 0.157, 0.337, 1)
        _InkWidth ("Ink Width u", Float) = 3
        _InkWobble ("Ink Wobble (주파수 / u, 세기 u)", Vector) = (0.02, 1.6, 0, 0)
        _MarkColor ("Mark Color (형광펜 — 시안 #ffd23f · .9)", Color) = (1, 0.824, 0.247, 0.9)
        _MarkWidth ("Mark Width u", Float) = 12
        _MarkWobble ("Mark Wobble (주파수 / u, 세기 u)", Vector) = (0.03, 3.4, 0, 0)
        _StarUnit ("Star Unit u (별 viewBox 1 칸)", Float) = 1.25
        _Star1 ("Star 1 (x, y, 배율)", Vector) = (101.34, 192.45, 1, 0)
        _Star2 ("Star 2 (x, y, 배율)", Vector) = (134.35, 134.22, 0.6, 0)
        _Star3 ("Star 3 (x, y, 배율)", Vector) = (-134.82, 134.22, 0.75, 0)
        [HideInInspector] _Mark ("Mark (형광펜)", Range(0, 1)) = 0
        [HideInInspector] _Grade ("Grade (별 수)", Float) = 0
        [HideInInspector] _StarShow ("Star Show (별 보이기 0 ~ 1)", Range(0, 1)) = 1
        [HideInInspector] _Sheen ("Sheen (가운데, 반폭, 세기)", Vector) = (0, 1, 0, 0)
        [HideInInspector] _SheenLine ("Sheen Line (축 x, 축 y, 더할 값, 흰 띠 비율)", Vector) = (1, 0, 0, 0.42)
        [HideInInspector] _SheenA ("Sheen .25", Vector) = (1, 1, 1, 0)
        [HideInInspector] _SheenB ("Sheen .5", Vector) = (1, 1, 1, 0)
        [HideInInspector] _SheenC ("Sheen .75", Vector) = (1, 1, 1, 0)
        [HideInInspector] _SheenWhite ("Sheen White", Float) = 0
        [HideInInspector] _Filter ("Filter (채도, 밝기, 대비)", Vector) = (1, 1, 1, 0)
    }

    CGINCLUDE
    #include "UnityCG.cginc"
    #include "CardFilter.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float4 _Size, _Paper, _Face, _InkWobble, _MarkWobble, _Star1, _Star2, _Star3;
    fixed4 _Thick1, _Thick2, _Thick3, _InkColor, _MarkColor;
    float _ThickStep, _InkWidth, _MarkWidth, _StarUnit, _Mark, _Grade, _StarShow;

    // 별(시안 CSTAR) 곡선의 첫 8분면 — (10, 0) 에서 (2.675, 2.675) 까지, 3차 곡선을 t 0 ~ 0.5 에서 8 마디로. 별은 8 겹 대칭이라 이것만 본다.
    static const float2 StarCurve[9] =
    {
        float2(10.0, 0.0), float2(8.6199, 0.2903), float2(7.3965, 0.5691), float2(6.3178, 0.8486), float2(5.3719, 1.1406),
        float2(4.5466, 1.4573), float2(3.8301, 1.8105), float2(3.2102, 2.2125), float2(2.675, 2.675)
    };

    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct v2f { float4 pos : SV_POSITION; float2 p : TEXCOORD0; };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.p = (v.uv - 0.5) * _Size.xy; // 카드 가운데가 0, 위가 + (u)
        return o;
    }

    float sdRoundRect(float2 p, float2 halfSize, float r)
    {
        float2 q = abs(p) - halfSize + r;
        return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
    }

    float PaperDistance(float2 p) { return sdRoundRect(p, _Paper.xy, _Paper.z); }

    // 화면 1 px 의 u 길이 — 가장자리를 매끈하게. 분기 밖에서 구한다 (분기 안에서는 미분이 안 된다)
    float PixelSize(float2 p) { return max(0.5 * (length(ddx(p)) + length(ddy(p))), 1e-4); }

    // 덮인 정도 — 거리 d (u, 안쪽이 음수)
    float Coverage(float d, float aa) { return saturate(0.5 - d / aa); }

    // 미리 곱한 알파로 위에 얹는다
    float4 Over(float4 below, float3 color, float alpha)
    {
        return float4(color * alpha + below.rgb * (1.0 - alpha), alpha + below.a * (1.0 - alpha));
    }

    float2 Hash2(float2 p)
    {
        p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
        return frac(sin(p) * 43758.5453) * 2.0 - 1.0;
    }

    // 기울기 잡음 (대략 −0.7 ~ 0.7) — feTurbulence 의 noise2 자리
    float Noise(float2 p)
    {
        float2 i = floor(p), f = frac(p);
        float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
        float a = dot(Hash2(i), f);
        float b = dot(Hash2(i + float2(1, 0)), f - float2(1, 0));
        float c = dot(Hash2(i + float2(0, 1)), f - float2(0, 1));
        float d = dot(Hash2(i + float2(1, 1)), f - float2(1, 1));
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    // feDisplacementMap — 밀린 거리 = 세기 × (잡음 합 ÷ 2), 가로 · 세로는 서로 다른 잡음 (R · G 채널)
    float2 NoisePair(float2 q, float seed) { return float2(Noise(q + seed), Noise(q + seed + 17.31)); }
    float2 InkWobble(float2 p) { return NoisePair(p * _InkWobble.x, 3.0) * (_InkWobble.y * 0.5); }
    float2 MarkWobble(float2 p)
    {
        float2 q = p * _MarkWobble.x;
        return (NoisePair(q, 1.0) + NoisePair(q * 2.0, 1.0) * 0.5) * (_MarkWobble.y * 0.5);
    }

    float Cross2(float2 a, float2 b) { return a.x * b.y - a.y * b.x; }

    // 별까지의 거리 (별 단위, 안쪽이 음수) — 접어서 첫 8분면 곡선만 본다. 곡선은 원점을 반시계로 돌아 왼쪽이 안.
    float StarDistance(float2 p)
    {
        p = abs(p);
        if (p.y > p.x) p = p.yx;
        float d = 1e5, side = 1.0;
        for (int i = 0; i < 8; i++)
        {
            float2 a = StarCurve[i], b = StarCurve[i + 1];
            float2 ab = b - a, ap = p - a;
            float t = saturate(dot(ap, ab) / dot(ab, ab));
            d = min(d, length(ap - ab * t));
            if (Cross2(a, p) >= 0.0 && Cross2(p, b) >= 0.0)
                side = Cross2(ab, ap) > 0.0 ? -1.0 : 1.0;
        }
        return d * side;
    }

    // 별 한 개 (시안 .star — viewBox 24 · rotate −6deg = 화면에서 반시계 · 잉크 테 1.8) — star = (x, y, 배율)
    float4 DrawStar(float4 below, float2 p, float3 star, float3 fill, float aa)
    {
        float unit = _StarUnit * star.z;
        float2 d = p - star.xy;
        if (dot(d, d) > 144.0 * unit * unit) return below;

        const float c = 0.994522, s = 0.104528; // cos 6° · sin 6°
        float2 local = float2(c * d.x + s * d.y, -s * d.x + c * d.y) / unit;
        float dist = StarDistance(local) * unit;
        float halfStroke = 0.9 * unit;
        float4 o = Over(below, fill, _StarShow * Coverage(dist, aa));
        return Over(o, _InkColor.rgb, _InkColor.a * _StarShow * Coverage(abs(dist) - halfStroke, aa));
    }

    float4 PaperColor(float2 p)
    {
        float aa = PixelSize(p);
        float4 c = 0;

        // 1. 두께 띠 — 시안 box-shadow 0 3px 0 #d8cab4 · 0 2px 0 #e6dccb · 0 1px 0 #f1ebe1 (먼저 적힌 것이 위)
        c = Over(c, _Thick3.rgb, Coverage(PaperDistance(p + float2(0.0, 3.0 * _ThickStep)), aa));
        c = Over(c, _Thick2.rgb, Coverage(PaperDistance(p + float2(0.0, 2.0 * _ThickStep)), aa));
        c = Over(c, _Thick1.rgb, Coverage(PaperDistance(p + float2(0.0, _ThickStep)), aa));

        // 2. 흰 종이
        float dPaper = PaperDistance(p);
        c = Over(c, float3(1.0, 1.0, 1.0), Coverage(dPaper, aa));

        // 3. 앞면 칸의 그림 — 틀 · 뒷면 그림
        float2 uv = p / (2.0 * _Face.xy) + 0.5;
        fixed4 tex = tex2D(_MainTex, TRANSFORM_TEX(uv, _MainTex));
        c = Over(c, tex.rgb, tex.a * Coverage(sdRoundRect(p, _Face.xy, _Face.z), aa));

        // 종이 테 가까이만 — 형광펜 · 잉크 선
        if (dPaper > -4.0 && dPaper < _MarkWidth + 8.0)
        {
            // 4. 형광펜 (시안 .ink .mk — 잉크 선 가운데에서 바깥으로 12 u 띠)
            if (_Mark > 0.001)
            {
                float d = PaperDistance(p + MarkWobble(p)) - _MarkWidth * 0.5;
                c = Over(c, _MarkColor.rgb, _MarkColor.a * _Mark * Coverage(abs(d) - _MarkWidth * 0.5, aa));
            }

            // 5. 잉크 선 (시안 .ink .o1 — 종이 테에 가운데를 맞춘 3 u)
            float dInk = PaperDistance(p + InkWobble(p));
            c = Over(c, _InkColor.rgb, _InkColor.a * Coverage(abs(dInk) - _InkWidth * 0.5, aa));
        }

        // 6. 등급 별 (시안 cardStars — 칠 색 고급 #fff6c4 / 희귀 #d9c6ff · #fff / 개판 #ffe45f · #ff8fc8 · #7fe0ff)
        if (_Grade > 0.5 && _StarShow > 0.001)
        {
            float3 first = _Grade > 2.5 ? float3(1.0, 0.894, 0.373) : _Grade > 1.5 ? float3(0.851, 0.776, 1.0) : float3(1.0, 0.965, 0.769);
            c = DrawStar(c, p, _Star1.xyz, first, aa);
            if (_Grade > 1.5)
                c = DrawStar(c, p, _Star2.xyz, _Grade > 2.5 ? float3(1.0, 0.561, 0.784) : float3(1.0, 1.0, 1.0), aa);
            if (_Grade > 2.5)
                c = DrawStar(c, p, _Star3.xyz, float3(0.498, 0.878, 1.0), aa);
        }

        return c;
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        Lighting Off

        // 1. 깊이 — 종이의 반 이상 덮인 곳만 (두께 띠 · 형광펜 · 별은 쓰지 않는다)
        Pass
        {
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            fixed4 fragDepth(v2f i) : SV_Target
            {
                clip(Coverage(PaperDistance(i.p), PixelSize(i.p)) - 0.5);
                return 0;
            }
            ENDCG
        }

        // 2. 색 — 미리 곱한 알파. 흐림은 섞기 전 색에 건다
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                float4 c = PaperColor(i.p);
                clip(c.a - 0.002);
                float3 color = CardSheen(CardFilter(c.rgb / c.a), i.p);
                return fixed4(color * c.a, c.a);
            }
            ENDCG
        }
    }
}
