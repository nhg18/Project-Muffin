// 개판 오분전 눈 마을의 소품 · 땅 · 얼음 · 눈뭉치 (시안 D3R 의 FS — 그늘 · 빗금 · 종이 결 · 안개 + 뒤집은 껍데기 외곽선).
// 색은 메시의 정점 색. 빛 · 안개는 GaepanCommon 의 전역 값. 그림자는 GaepanShadow 머티리얼을 둘째 칸에 넣어 땅에 눕혀 그린다.
Shader "Chapchu/CardFx/Gaepan Toon"
{
    Properties
    {
        _Outline ("Outline (시안 ol — 화면 2.2px 배, 0 이면 없음)", Float) = 1
        _Ink ("Ink (외곽선 색)", Color) = (0.2, 0.13, 0.12, 1)
        [Enum(Object,0,Ground,1,Ice,2)] _Surface ("Surface (0 물건 · 1 눈 땅 · 2 얼음)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "GaepanCommon.cginc"

        float _Outline;
        float4 _Ink;
        float _Surface;

        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDCG

        // 외곽선 — 뒷면을 법선 쪽으로 화면 몇 px 만큼 부풀린다. 어두운 색(밝기 0.16 밑)에는 외곽선이 없다
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                float3 normal = UnityObjectToWorldNormal(v.normal);
                float4 clip = mul(UNITY_MATRIX_VP, world);
                float depth = max(clip.w, 0.2);
                float ink = dot(v.color.rgb, float3(0.3, 0.59, 0.11)) < 0.16 ? 0.0 : 1.0;
                float width = _Outline * 2.2 * _GpView.y * ink * clamp(10.0 / depth, 0.45, 1.0);
                world.xyz += normal * (width * depth / max(_GpView.x, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, world);
                // 외곽선이 없는 머티리얼(땅 · 얼음)은 화면 밖으로 보내 그리지 않는다
                if (_Outline <= 0.0)
                    o.pos = float4(2.0, 2.0, 2.0, 1.0);
                o.worldPos = world.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(lerp(_Ink.rgb, _GpFog.rgb, GpFogAmount(i.worldPos)), 1.0);
            }
            ENDCG
        }

        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, world);
                o.worldPos = world.xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 c = i.color.rgb;
                float3 n = normalize(i.normal);
                float3 view = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 toLight = -_GpLightDir.xyz;
                bool ground = _Surface > 0.5 && _Surface < 1.5;
                bool ice = _Surface > 1.5;
                if (ground)
                {
                    // 눈 둔덕 — 법선을 잡음으로 흔든다
                    float2 q = i.worldPos.xz * 0.32;
                    float h0 = GpNoise(q), hx = GpNoise(q + float2(0.06, 0)), hz = GpNoise(q + float2(0, 0.06));
                    n = normalize(n + float3(h0 - hx, 0, h0 - hz) * 5.5);
                }
                // 가로등 불빛 (255,214,140) 은 스스로 빛난다
                bool glow = c.r > 0.97 && c.g > 0.78 && c.g < 0.9 && c.b < 0.62;
                float lit = smoothstep(-0.08, 0.28, dot(n, toLight));
                float3 col = c * lerp(_GpShade.rgb, _GpSun.rgb, lit);
                float2 px = GpScreenPx(i.screenPos);
                col *= 1.0 - GpHatch(px) * (1.0 - lit) * (ice ? 0.03 : 0.07);
                if (ground)
                {
                    float drift = smoothstep(0.2, 0.55, GpNoise(i.worldPos.xz * 0.22) * 0.7 + GpNoise(i.worldPos.xz * 0.9) * 0.3);
                    col = lerp(col * float3(0.93, 0.95, 1.0), col, drift);
                    if (GpHash(floor(i.worldPos.xz * 13.0)) > 0.9935 && lit > 0.7)
                        col = lerp(col, float3(1, 1, 1), 0.85);
                }
                if (ice)
                {
                    float3 halfDir = normalize(view - _GpLightDir.xyz);
                    col += pow(max(dot(n, halfDir), 0.0), 70.0) * 0.75;
                    col += pow(1.0 - max(dot(n, view), 0.0), 4.0) * 0.18;
                }
                if (!ground && !ice)
                    col += pow(1.0 - max(dot(n, view), 0.0), 3.0) * 0.1 * lit;
                if (glow)
                    col = c * 1.12 + 0.05;
                return fixed4(lerp(GpPaper(col, px), _GpFog.rgb, GpFogAmount(i.worldPos)), 1.0);
            }
            ENDCG
        }
    }
}
