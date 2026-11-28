// 대폭발 3D 장면의 입자 (비행운 · 불덩이 · 버섯구름 · 먼지 벽 · 불똥 · 연기 · 낙진) — 시안 NUKE3D GX 의 PVS · PFS 와 같다.
// 정점 = 입자 가운데, uv0 = (모서리 x, 모서리 y, 크기, 회전), color = 색 · 알파, uv1 = (열, 씨앗). 열>0 불, 열<0 색 그대로 빛남.
// 장면 깊이(카메라 깊이 텍스처)로 직접 가리고 가장자리를 부드럽게 한다 — 깊이 버퍼는 쓰지 않는다.
Shader "Chapchu/Nuke/Particle"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NukeCommon.cginc"

            float3 _NkCamR;
            float3 _NkCamU;
            float3 _NkCamF;
            float3 _NkSunV;     // 판 공간(오른쪽 · 위 · 앞쪽으로 나옴)의 해 방향
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float4 qs : TEXCOORD0;
                float2 hs : TEXCOORD1;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 vc : TEXCOORD1;
                float4 vh : TEXCOORD2;  // 열 · 씨앗 · 크기 · 깊이
                float4 sp : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float c = cos(v.qs.w);
                float s = sin(v.qs.w);
                float2 k = float2(v.qs.x * c - v.qs.y * s, v.qs.x * s + v.qs.y * c) * v.qs.z;
                float3 w = v.vertex.xyz + _NkCamR * k.x + _NkCamU * k.y;
                o.pos = mul(UNITY_MATRIX_VP, float4(w, 1.0));
                o.uv = v.qs.xy;
                o.vc = v.color;
                o.vh = float4(v.hs, v.qs.z, dot(w - _WorldSpaceCameraPos, _NkCamF));
                o.sp = ComputeScreenPos(o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float r = length(i.uv);
                if (r > 1.0)
                    discard;
                float heat = i.vh.x;
                float vd = i.vh.w;
                float sz = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.sp)));
                if (vd > sz)
                    discard;
                float soft = clamp((sz - vd) / (i.vh.z * 0.45 + 0.04), 0.0, 1.0);
                if (heat < 0.0)
                {
                    float ga = smoothstep(1.0, 0.0, r);
                    ga *= ga * i.vc.a * soft;
                    return float4(i.vc.rgb * (-heat) * ga, 0.0);
                }
                float n = nkFbm3(i.uv * 1.7 + i.vh.y * 19.0);
                float n2 = nkNoise(i.uv * 5.0 + i.vh.y * 7.0);
                float a = smoothstep(0.0, 0.5, (1.0 - r) + (n - 0.5) * 1.05) * i.vc.a * soft * clamp((vd - 0.35) / 1.2, 0.0, 1.0);
                if (a < 0.004)
                    discard;
                float3 N = normalize(float3(i.uv * (0.55 + n * 0.7), sqrt(max(0.0, 1.0 - r * r)) + 0.15));
                float l = dot(N, _NkSunV) * 0.5 + 0.5;
                float3 col = i.vc.rgb * (_NkAmb * 0.95 + _NkSunC * l * l * 0.85) * (0.7 + 0.6 * n) * (0.92 + 0.16 * n2);
                float add = 0.0;
                if (heat > 0.0)
                {
                    float h = heat * (0.35 + 1.1 * n * n + 0.5 * (1.0 - r));
                    float3 e = lerp(float3(0.85, 0.1, 0.02), float3(1.0, 0.5, 0.08), smoothstep(0.15, 0.6, h));
                    e = lerp(e, float3(1.0, 0.9, 0.62), smoothstep(0.6, 1.15, h));
                    float k = clamp(h * 1.4, 0.0, 1.0);
                    col = col * (1.0 - k * 0.75) + e * h * h * 5.5;
                    add = k * 0.55;
                }
                float f = (1.0 - exp(-vd * _NkFx.x)) * _NkFx.y;
                col = lerp(col, _NkFogC, clamp(f, 0.0, 1.0) * (1.0 - add));
                return float4(col * a, a * (1.0 - add));
            }
            ENDCG
        }
    }
}
