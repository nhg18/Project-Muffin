// 3D 카드 둘레의 빛 — 고른 카드 · 낼 수 있는 카운터의 테두리 빛, 등급 오라 (10-ui.md 2-1). 그림 없이 거리장으로 그린다.
// 프로토타입 v2 C3D 의 GLOW_FS 그대로: 외곽선 둘레 거리 → 일렁이는 번짐 + 테두리를 도는 하이라이트 2개 + 반짝 알갱이 + 맥박.
// 길이는 캔버스 px (1 유닛 = 100). 시안 값(카드 폭 196 기준)을 226 기준으로 1.153 배 했다.
Shader "Chapchu/Card Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.824, 0.247, 1)
        _Amount ("Amount", Range(0, 1)) = 1
        [MaterialToggle] _Pulse ("Pulse (카운터 깜빡임)", Float) = 0
        [MaterialToggle] _Rainbow ("Rainbow (개판)", Float) = 0
        [MaterialToggle] _FaceFade ("Face Fade (앞면이 보일 때만 — 등급 오라)", Float) = 0
        _Reach ("Reach", Float) = 1
        _Size ("Quad Size px (w, h)", Vector) = (401.3, 511.3, 0, 0)
        _Half ("Card Half Size px (외곽선 바깥)", Vector) = (126.84, 181.84, 0, 0)
        _Radius ("Corner Radius px (외곽선 바깥)", Float) = 13.84
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Amount, _Pulse, _Rainbow, _FaceFade, _Reach, _Radius;
            float4 _Size, _Half;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float face : TEXCOORD1; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // 앞면이 카메라를 보는 정도 (Quad 는 −Z 를 본다) — 시안 clamp((cos ry − .15) / .5)
                float3 toCam = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
                float facing = -dot(toCam, normalize(unity_ObjectToWorld._m02_m12_m22));
                o.face = lerp(1.0, saturate((facing - 0.15) / 0.5), _FaceFade);
                return o;
            }

            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float h1(float x) { return frac(sin(x * 12.9898) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                const float k = 1.153; // 시안 px → 캔버스 px
                float time = _Time.y;
                float2 p = (i.uv - 0.5) * _Size.xy;
                float d = sdRoundRect(p, _Half.xy, _Radius);
                clip(d + 1.0); // 카드 안쪽은 카드가 덮는다

                float a = atan2(p.y, p.x);
                float u = a / 6.2832 + 0.5;
                float dd = max(d, 0.0);

                // 일렁이는 번짐 (각도에 정수 주파수만 → 이음매 없음)
                float wob = 0.5 + 0.22 * sin(a * 7.0 + time * 2.6) + 0.18 * sin(a * 13.0 - time * 3.4) + 0.1 * sin(a * 23.0 + time * 5.1);
                float reach = (10.0 + 26.0 * wob) * k * _Reach;
                float halo = exp(-dd / reach * 2.4);
                float core = exp(-abs(d - 1.5 * k) / (2.2 * k));
                // 테두리를 따라 도는 하이라이트 2개
                float sw = pow(0.5 + 0.5 * cos((u - time * 0.32) * 6.2832 * 2.0), 22.0);
                // 반짝 알갱이
                float cell = floor(u * 96.0);
                float tick = floor(time * 9.0 + h1(cell) * 7.0);
                float spark = step(0.9, h1(cell * 1.7 + tick * 3.1)) * exp(-abs(d - (6.0 + h1(cell + tick) * 10.0) * k) / (2.5 * k))
                            * smoothstep(0.5, 0.08, abs(frac(u * 96.0) - 0.5));
                float pulse = 0.82 + 0.18 * sin(time * 4.2);
                float alpha = (halo * 0.9 + core) * pulse + sw * (core * 1.3 + halo * 0.6) + spark * 0.9;

                float3 base = _Color.rgb;
                if (_Rainbow > 0.5)
                {
                    float3 hue = abs(frac(u - time * 0.12 + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                    base = lerp(float3(1, 1, 1), saturate(hue - 1.0), 0.75);
                }
                float3 col = lerp(base, float3(1, 1, 1), saturate(core * 0.6 + sw * 0.7 + spark * 0.8));

                // 카운터 깜빡임 — 시안 .6 + .4·sin(t·10.5)
                float amount = _Amount * lerp(1.0, 0.6 + 0.4 * sin(time * 10.5), _Pulse) * i.face;
                return fixed4(col, saturate(alpha) * amount);
            }
            ENDCG
        }
    }
}
