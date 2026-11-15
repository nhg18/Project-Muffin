// 대소동 3D 장면의 하늘 — 해 · 구름 · 착탄 뒤 구름에 뚫리는 구멍과 그 테두리 · 착탄점 쪽 붉은 빛. 시안 NUKE3D GX 의 SKY 와 같다.
// 카메라에 붙은 공 안쪽에 그린다 (방향 = 정점 − 카메라).
Shader "Chapchu/Nuke/Sky"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" "PreviewType" = "Skybox" }
        Pass
        {
            Cull Off
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "NukeCommon.cginc"

            float3 _NkSkySun;     // 하늘에 비치는 해 색
            float3 _NkCloudC;
            float3 _NkCloudS;
            float3 _NkGlowC;
            float4 _NkSkyK;       // x 구름 덮개, y 구멍 반지름, z 구멍 테 세기, w 착탄 빛

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                float4 wu = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, wu);
                o.dir = wu.xyz - _WorldSpaceCameraPos;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 cam = _WorldSpaceCameraPos - _NkOrigin;
                float3 sunD = -_NkLd;
                float cover = _NkSkyK.x;
                float hole = _NkSkyK.y;
                float3 c = lerp(_NkHor, _NkZen, pow(smoothstep(-0.02, 0.7, d.y), 0.55));
                if (d.y < 0.0)
                    c = lerp(_NkHor, _NkHor * 0.62, smoothstep(0.0, -0.25, d.y));
                float sd = max(dot(d, sunD), 0.0);
                c += _NkSkySun * (pow(sd, 1400.0) * 26.0 + pow(sd, 90.0) * 0.6 + pow(sd, 7.0) * 0.2);
                if (d.y > 0.012)
                {
                    float tt = (190.0 - cam.y) / d.y;
                    float2 q = cam.xz + d.xz * tt;
                    float2 s = q * 0.0034 + float2(_NkTime * 0.01, _NkTime * 0.004);
                    float f = nkFbm3(s) * 0.66 + nkFbm3(s * 3.3 + 7.0) * 0.26 + nkNoise(s * 11.0) * 0.08;
                    float den = smoothstep(1.0 - cover, 1.0 - cover + 0.24, f);
                    float hl = length(q - float2(0.0, 30.0));
                    float rim = 0.0;
                    if (hole >= 1.0)
                        rim = smoothstep(hole * 0.82, hole, hl) * (1.0 - smoothstep(hole, hole * 1.18 + 2.0, hl));
                    den *= smoothstep(hole * 0.9, hole * 1.02 + 1.0, hl);
                    den *= smoothstep(0.012, 0.13, d.y);
                    float2 s2 = s + sunD.xz * 0.045;
                    float f2 = nkFbm3(s2) * 0.66 + nkFbm3(s2 * 3.3 + 7.0) * 0.26;
                    float lit = clamp((f - f2) * 5.0 + 0.55, 0.0, 1.0);
                    float3 cc = lerp(_NkCloudS, _NkCloudC, lit) + _NkSkySun * pow(sd, 10.0) * 0.55 * (1.0 - den * 0.7);
                    c = lerp(c, cc, den * 0.92);
                    c += float3(1.0, 0.95, 0.85) * rim * 0.7 * _NkSkyK.z * smoothstep(0.02, 0.2, d.y);
                }
                float3 di = normalize(float3(0.0, 40.0, 30.0) - cam);
                c += _NkGlowC * _NkSkyK.w * (pow(max(dot(d, di), 0.0), 5.0) + 0.07);
                return float4(c, 1.0);
            }
            ENDCG
        }
    }
}
