// 「GOAT」 컷인의 돌 회랑 — 시안 goat_cutin.js 의 물체 셰이더(PO)를 그대로 옮겼다.
// 빛 = 바탕빛 + 한 방향 빛 + 점 빛 넷(거리 제곱으로 약해짐), 스스로 빛나는 면(uv.x)은 안개를 덜 먹는다. 양면을 그리고 등진 면은 법선을 뒤집는다.
// 빛 · 안개 값은 GoatWorld 가 매 프레임 전역 값으로 넣는다.
Shader "Chapchu/CardFx/Goat Stone"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _GoatAmbient;
            // 빛이 오는 쪽 (정규화)
            float4 _GoatSunDir;
            float4 _GoatSun;
            // rgb = 안개 색, a = 짙기
            float4 _GoatFog;
            // 스스로 빛나는 면의 세기 (문이 열릴수록 커진다)
            float _GoatEmissive;
            float4 _GoatLightPos[4];
            float4 _GoatLightColor[4];

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 color : COLOR;
                float emissive : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, world);
                o.worldPos = world.xyz;
                o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
                o.color = v.color;
                o.emissive = v.uv.x;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 toCamera = _WorldSpaceCameraPos - i.worldPos;
                float3 n = normalize(i.normal);
                if (dot(n, toCamera) < 0.0)
                    n = -n;

                float3 light = _GoatAmbient.rgb + _GoatSun.rgb * max(dot(n, _GoatSunDir.xyz), 0.0);
                for (int k = 0; k < 4; k++)
                {
                    float3 d = _GoatLightPos[k].xyz - i.worldPos;
                    float r = length(d);
                    light += _GoatLightColor[k].rgb * max(dot(n, d / r), 0.0) / (1.0 + 0.08 * r * r);
                }

                float glow = i.emissive * _GoatEmissive;
                float3 col = i.color.rgb * light + i.color.rgb * glow;
                float fog = 1.0 - exp(-_GoatFog.a * length(toCamera));
                fog *= 1.0 - 0.7 * min(glow, 1.0);
                return fixed4(lerp(col, _GoatFog.rgb, fog), 1.0);
            }
            ENDCG
        }
    }
}
