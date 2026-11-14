// 3D 카드의 면 — 앞면 틀 · 그림 · 뒷면 (10-ui.md 2-1 「카드 생김새」). 프로토타입 v2 의 C3D 카드 셰이더(FS)를 옮겼다.
//   모양: 흰 바탕(둥근 모서리) + 바깥 외곽선. 그 안쪽 _Pad 만큼 들어간 자리에 텍스처(틀 · 그림 · 뒷면 그림)를 흰 바탕 위에 얹는다.
//   빛:   빛 반사(_Glare, 보는 각도를 따라 움직이는 흰 빛) · 홀로(_Foil, 그림 칸의 은박 결) · 등급 반짝(_Grade, 몇 초마다 훑는 빛 띠 — CardView 가 넣는다).
//   그림이 없는 카드(_HasTex 0)는 연한 바탕에 발자국을 그린다.
//   _Opaque 1 = 흰 바탕 위에 텍스처(틀 · 뒷면), 0 = 텍스처의 투명한 곳은 아래(틀)가 보인다(그림).
// 길이는 전부 캔버스 px(1 유닛 = 100). 깊이는 반 이상 덮인 곳에만 쓰고(첫 패스) 가장자리는 섞어 그린다(둘째 패스).
// 투명 순서 — 카드 루트의 SortingGroup 이 카드 한 장을 묶어 뒤 카드부터 그리므로 모서리가 뒤 카드 위에 매끈하게 섞인다.
Shader "Chapchu/Card Face"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [MaterialToggle] _HasTex ("Has Texture (끄면 발자국)", Float) = 1
        [MaterialToggle] _Opaque ("Opaque (흰 바탕 위에 — 틀 · 뒷면)", Float) = 1
        _PawColor ("Paw Color (발자국)", Color) = (0, 0.616, 1, 0.7)
        _PawPaper ("Paw Paper (발자국 바탕)", Color) = (0.965, 0.941, 0.906, 1)
        _Size ("Quad Size px (w, h)", Vector) = (253.68, 363.68, 0, 0)
        _Pad ("Texture Inset px (외곽선 + 흰 여백)", Float) = 13.84
        _Radius ("White Corner Radius px", Float) = 9.22
        _Outline ("Outline Width px", Float) = 4.61
        _OutlineColor ("Outline Color", Color) = (0, 0.616, 1, 1)
        _CardRect ("Quad In Card UV (x0, y0, x1, y1)", Vector) = (-0.0612, -0.0412, 1.0612, 1.0412)
        _Foil ("Foil (그림 홀로)", Range(0, 1)) = 0
        _Glare ("Glare (빛 반사)", Range(0, 1)) = 0.6
        [HideInInspector] _Grade ("Grade (0 일반 ~ 3 개판)", Float) = 0
        [HideInInspector] _GradeSpeed ("Grade Sweep Speed", Float) = 0
        [HideInInspector] _GradeOffset ("Grade Sweep Offset", Float) = 0
        [HideInInspector] _Gray ("Gray (낼 수 없는 카드)", Range(0, 1)) = 0
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_ST;
    float _HasTex, _Opaque;
    fixed4 _PawColor, _PawPaper, _OutlineColor;
    float4 _Size, _CardRect;
    float _Pad, _Radius, _Outline, _Foil, _Glare, _Grade, _GradeSpeed, _GradeOffset, _Gray;

    struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 view : TEXCOORD1; };

    v2f vert(appdata v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        // 카메라 쪽 방향을 면의 축(오른쪽 · 위 · 앞)으로 — 크기를 뺀 축이라 Quad 의 배율과 상관없다. Quad 는 −Z 를 본다.
        float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
        float3 toCam = normalize(_WorldSpaceCameraPos - world);
        float3 right = normalize(unity_ObjectToWorld._m00_m10_m20);
        float3 up = normalize(unity_ObjectToWorld._m01_m11_m21);
        float3 back = normalize(unity_ObjectToWorld._m02_m12_m22);
        o.view = float3(dot(toCam, right), dot(toCam, up), -dot(toCam, back));
        return o;
    }

    float sdRoundRect(float2 p, float2 halfSize, float r)
    {
        float2 q = abs(p) - halfSize + r;
        return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
    }

    // 발자국 (프로토타입 v2 PAW — 64 칸, 위가 0): 큰 발바닥 하나 + 발가락 넷
    float pawDistance(float2 q)
    {
        float d = (length((q - float2(32, 42)) / float2(15, 12)) - 1.0) * 12.0;
        d = min(d, (length((q - float2(14, 27)) / float2(6, 8)) - 1.0) * 6.0);
        d = min(d, (length((q - float2(26, 17)) / float2(6, 8)) - 1.0) * 6.0);
        d = min(d, (length((q - float2(38, 17)) / float2(6, 8)) - 1.0) * 6.0);
        d = min(d, (length((q - float2(50, 27)) / float2(6, 8)) - 1.0) * 6.0);
        return d;
    }

    // 알파 (모양) — 두 패스가 같이 쓴다
    float shapeAlpha(float2 px, out float dOuter)
    {
        float2 halfSize = _Size.xy * 0.5;
        dOuter = sdRoundRect(px - halfSize, halfSize, _Radius + _Outline);
        float aa = max(fwidth(dOuter), 1e-4);
        return saturate(0.5 - dOuter / aa);
    }

    fixed4 shade(v2f i)
    {
        float2 px = i.uv * _Size.xy;
        float dOuter;
        float alpha = shapeAlpha(px, dOuter);

        // 텍스처 — 흰 바탕 위에 (그림은 투명 바탕)
        float2 inner = _Size.xy - 2.0 * _Pad;
        float2 tuv = (px - _Pad) / inner;
        float inTex = step(0.0, tuv.x) * step(tuv.x, 1.0) * step(0.0, tuv.y) * step(tuv.y, 1.0);
        fixed4 tex = tex2D(_MainTex, tuv * _MainTex_ST.xy + _MainTex_ST.zw);
        float3 content = lerp(float3(1, 1, 1), tex.rgb, tex.a);
        if (_Opaque < 0.5 && _HasTex > 0.5)
        {
            content = tex.rgb;
            alpha *= lerp(1.0, tex.a, inTex);
        }
        if (_HasTex < 0.5)
        {
            // 프로토타입 v2: 칸 폭의 0.36 크기 발자국을 가운데에, 종류 색 70%
            float side = inner.x * 0.36;
            float2 center = _Pad + inner * 0.5;
            float2 q = float2(px.x - (center.x - side * 0.5), (center.y + side * 0.5) - px.y) / side * 64.0;
            float d = pawDistance(q);
            float paw = saturate(0.5 - d / max(fwidth(d), 1e-4)) * _PawColor.a;
            content = lerp(_PawPaper.rgb, _PawColor.rgb, paw);
        }
        float3 col = lerp(float3(1, 1, 1), content, inTex);

        // 외곽선
        float2 halfSize = _Size.xy * 0.5;
        float dWhite = sdRoundRect(px - halfSize, halfSize - _Outline, _Radius);
        float inWhite = _Outline > 0.0 ? saturate(0.5 - dWhite / max(fwidth(dWhite), 1e-4)) : 1.0;
        col = lerp(_OutlineColor.rgb, col, inWhite);

        // 빛 — 카드 전체 좌표(cu)로 계산해 틀 · 그림 Quad 가 이어진다
        float2 cu = lerp(_CardRect.xy, _CardRect.zw, i.uv);
        float2 t = i.view.xy / max(i.view.z, 0.25);
        float lum = dot(col, float3(0.299, 0.587, 0.114));
        float tiltAmt = clamp(length(t) * 2.4, 0.3, 1.0);

        // 홀로: 기울기에 따라 흐르는 흰 빛 띠 + 가는 결 (색 없음)
        float band = cu.x * 0.9 + cu.y * 1.5 + t.x * 2.4 - t.y * 1.8;
        float sheen = pow(0.5 + 0.5 * sin(band * 6.2832 * 1.3), 6.0);
        float lines = 0.6 + 0.4 * sin((cu.x - cu.y * 0.35) * 230.0);
        float3 foil = saturate(col * (0.9 + 0.1 * lines) + sheen * lines * (0.25 + lum * 0.55));
        col = lerp(col, foil, _Foil * tiltAmt);

        // 등급 반짝: 몇 초마다 앞면을 대각선으로 훑는 흰 빛 띠. 개판은 띠 끝이 무지개.
        // 앞면이 카메라를 볼 때만 — 뒤집히는 중에는 옅어진다 (시안 clamp((cos ry − .15) / .5))
        float face = saturate((i.view.z - 0.15) / 0.5) * (1.0 - 0.8 * _Gray);
        if (_Grade > 0.5 && face > 0.001)
        {
            float cyc = frac(_Time.y * _GradeSpeed + _GradeOffset);
            float pos = cyc / 0.5 * 1.8 - 0.4;
            float dd = (cu.x * 0.6 + (1.0 - cu.y) * 0.4) - pos;
            float bw = _Grade > 2.5 ? 0.075 : 0.06;
            float sh = exp(-dd * dd / (bw * bw)) * step(cyc, 0.5);
            float3 tint = float3(1, 1, 1);
            if (_Grade > 2.5)
            {
                float3 k = abs(frac(dd * 4.0 + _Time.y * 0.3 + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                tint = lerp(float3(1, 1, 1), saturate(k - 1.0), 0.35);
            }
            col = saturate(col + tint * sh * (0.45 + 0.25 * lum) * face);
        }

        // 빛 반사: 보는 각도 반대쪽으로 옮겨 가는 둥근 흰 빛. 글자 · 잉크 선은 덜 덮는다
        float2 gc = 0.5 - t * 1.35;
        float gl = smoothstep(0.8, 0.0, length((cu - gc) * float2(1.0, 1.49)));
        col += gl * gl * _Glare * 0.5 * (0.15 + 0.85 * lum);

        // 낼 수 없는 카드: 채도를 뺀다 (밝기는 앞에 덮는 Shade 판)
        float l2 = dot(col, float3(0.299, 0.587, 0.114));
        col = lerp(col, l2 * 0.86 + 0.05, _Gray);

        return fixed4(col, alpha);
    }
    ENDCG

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        Lighting Off

        // 1. 깊이 — 반 이상 덮인 곳만
        Pass
        {
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragDepth
            fixed4 fragDepth(v2f i) : SV_Target
            {
                float dOuter;
                clip(shapeAlpha(i.uv * _Size.xy, dOuter) - 0.5);
                return 0;
            }
            ENDCG
        }

        // 2. 색 — 가장자리는 섞어서. 알파는 따로 쌓아 앞 층 렌더 텍스처(투명 바탕)에서도 덮인 정도가 맞다
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = shade(i);
                clip(c.a - 0.004);
                return c;
            }
            ENDCG
        }
    }
}
