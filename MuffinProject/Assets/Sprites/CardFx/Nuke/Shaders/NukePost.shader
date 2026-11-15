// 대소동 3D 장면의 후처리 — 시안 NUKE3D GX 의 AOF · APF · BR · DN · UP · GRF · CO 와 같다.
// 0 주변광 차폐(반 해상도) · 1 밝은 곳 · 2 줄이기 · 3 키우기(더하기) · 4 빛줄기 · 5 마무리(차폐 · 물결 · 방사 흐림 · 색수차 · 심도 · 블룸 · 노출 · 색 · 비네트 · 암전 · 알갱이).
// 장면 알파 = 물체가 보이는 정도 — 강아지 · 입자가 알파를 깎는다. 시안은 입자를 그리기 전에 차폐를 먹이는데, 여기서는 마무리에서 알파만큼만 먹여 같게 한다.
// 깊이를 읽는 패스는 모두 장면 원본을 _MainTex 로 받는다 (D3D 에서 원본이 뒤집혀 있으면 깊이도 맞춰 읽으려고).
Shader "Hidden/Chapchu/Nuke/Post"
{
    Properties
    {
        _MainTex ("", 2D) = "black" {}
    }
    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
    float2 _Px;          // 읽는 텍스처 한 칸
    float _K;            // 세기
    float _Thr;          // 밝은 곳 문턱
    float2 _KK;          // tan(반 화각) x · y

    float hash(float2 p)
    {
        return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
    }

    // 깊이 텍스처는 뒤집히지 않은 쪽이라 _MainTex 가 뒤집혀 있으면 맞춰 읽는다
    float2 depthUV(float2 uv)
    {
        #if UNITY_UV_STARTS_AT_TOP
        if (_MainTex_TexelSize.y < 0)
            uv.y = 1.0 - uv.y;
        #endif
        return uv;
    }

    // 반복문 안에서도 읽으므로 밉 0 으로 (기울기 없이)
    float rawDepth(float2 uv)
    {
        return SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(depthUV(uv), 0.0, 0.0));
    }

    float eyeDepth(float2 uv)
    {
        return LinearEyeDepth(rawDepth(uv));
    }

    bool isSky(float2 uv)
    {
        return Linear01Depth(rawDepth(uv)) > 0.9999;
    }
    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // 0 주변광 차폐
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            float3 vp(float2 u)
            {
                float z = eyeDepth(u);
                return float3((u * 2.0 - 1.0) * _KK * z, z);
            }

            float4 frag(v2f_img i) : SV_Target
            {
                if (isSky(i.uv))
                    return 1;
                float3 P = vp(i.uv);
                float3 n = normalize(cross(ddx(P), ddy(P)));
                if (n.z > 0.0)
                    n = -n;
                float rad = 1.3;
                float rp = clamp(rad * 0.5 / (_KK.y * P.z), 0.004, 0.1);
                float ang = hash(i.pos.xy) * 6.283;
                float oc = 0.0;
                for (int k = 0; k < 10; k++)
                {
                    float fi = (k + 0.5) / 10.0;
                    float a = ang + fi * 15.1;
                    float2 off = float2(cos(a), sin(a)) * rp * sqrt(fi) * float2(_KK.y / _KK.x, 1.0);
                    float3 v = vp(i.uv + off) - P;
                    float dd = dot(v, v);
                    oc += max(dot(n, v) / sqrt(dd + 1e-4) - 0.14, 0.0) / (1.0 + dd / (rad * rad) * 1.6);
                }
                float ao = lerp(clamp(1.0 - oc * 0.26, 0.0, 1.0), 1.0, smoothstep(110.0, 260.0, P.z));
                return float4(ao, ao, ao, 1.0);
            }
            ENDCG
        }

        // 1 밝은 곳
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            float4 frag(v2f_img i) : SV_Target
            {
                float3 c = 0;
                for (int k = 0; k < 4; k++)
                {
                    float2 d = float2((k & 1) * 2.0 - 1.0, (k >> 1) * 2.0 - 1.0) * _Px;
                    float3 s = min(tex2D(_MainTex, i.uv + d).rgb, 24.0);
                    float l = max(s.r, max(s.g, s.b));
                    c += s * max(l - _Thr, 0.0) / max(l, 0.001);
                }
                return float4(c * 0.25, 1.0);
            }
            ENDCG
        }

        // 2 줄이기
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            float4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb * 0.5;
                for (int k = 0; k < 4; k++)
                {
                    float2 d = float2((k & 1) * 2.0 - 1.0, (k >> 1) * 2.0 - 1.0) * _Px;
                    c += min(tex2D(_MainTex, i.uv + d).rgb, 12.0) * 0.125;
                }
                return float4(c, 1.0);
            }
            ENDCG
        }

        // 3 키우기 (더하기)
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            float4 frag(v2f_img i) : SV_Target
            {
                float3 c = 0;
                for (int k = 0; k < 4; k++)
                {
                    float2 d = float2((k & 1) * 2.0 - 1.0, (k >> 1) * 2.0 - 1.0) * _Px;
                    c += tex2D(_MainTex, i.uv + d).rgb;
                }
                return float4(c * 0.25 * _K, 1.0);
            }
            ENDCG
        }

        // 4 빛줄기 — 해 쪽으로 하늘의 밝은 곳을 끌어온다
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            float2 _Sun;

            float4 frag(v2f_img i) : SV_Target
            {
                float2 dl = (i.uv - _Sun) / 30.0 * 0.92;
                float2 u = i.uv - dl * hash(i.pos.xy);
                float w = 1.0;
                float3 c = 0;
                for (int k = 0; k < 30; k++)
                {
                    float3 s = min(tex2D(_MainTex, u).rgb, 5.0);
                    float m = (isSky(u) ? 1.0 : 0.0) * smoothstep(0.75, 1.7, max(s.r, max(s.g, s.b)));
                    c += s * m * w;
                    w *= 0.955;
                    u -= dl;
                }
                return float4(c / 30.0 * _K, 1.0);
            }
            ENDCG
        }

        // 5 마무리
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0

            sampler2D _AoTex;
            float2 _AoPx;
            float _AoK;
            sampler2D _BloomTex;
            sampler2D _RayTex;
            sampler2D _HalfTex;
            sampler2D _QuarterTex;
            float _Expo, _Bloom, _Sat, _Con, _Vig, _Grain, _Ca, _Rb, _Time2, _Fade, _Asp, _Foc, _DofK;
            float3 _Tint, _VigC, _FadeC;
            float4 _Rip;
            float2 _Rbc;

            float4 frag(v2f_img i) : SV_Target
            {
                float2 u = i.uv;
                if (_Rip.w > 0.0)
                {
                    float2 rd = (u - _Rip.xy) * float2(_Asp, 1.0);
                    float l = length(rd);
                    float k = (l - _Rip.z) / max(0.02, _Rip.z * 0.16);
                    u -= rd / max(l, 1e-4) / float2(_Asp, 1.0) * exp(-k * k) * _Rip.w * 0.035;
                }
                float2 d = u - 0.5;
                float3 c;
                if (_Rb > 0.001)
                {
                    c = 0;
                    float j = hash(i.pos.xy);
                    for (int n = 0; n < 18; n++)
                    {
                        float s = 1.0 - _Rb * (n + j) / 17.0;
                        c += min(tex2D(_MainTex, (u - _Rbc) * s + _Rbc).rgb, 6.0);
                    }
                    c /= 18.0;
                }
                else
                {
                    if (_Ca > 0.0001)
                    {
                        float2 off = d * _Ca;
                        c = float3(tex2D(_MainTex, u + off).r, tex2D(_MainTex, u).g, tex2D(_MainTex, u - off).b);
                    }
                    else
                    {
                        c = tex2D(_MainTex, u).rgb;
                    }
                    if (_DofK > 0.001)
                    {
                        float z = eyeDepth(u);
                        float coc = clamp(_DofK * max(z - _Foc, 0.0) / max(z, 0.5), 0.0, 1.0);
                        c = lerp(c, tex2D(_HalfTex, u).rgb, clamp(coc * 2.0, 0.0, 1.0));
                        c = lerp(c, tex2D(_QuarterTex, u).rgb, clamp(coc * 2.0 - 1.0, 0.0, 1.0));
                    }
                }
                if (_AoK > 0.01)
                {
                    float a = 0.0;
                    for (int k = 0; k < 16; k++)
                        a += tex2D(_AoTex, u + (float2(k & 3, k >> 2) - 1.5) * _AoPx).r;
                    c *= lerp(1.0, lerp(1.0, a / 16.0, _AoK), saturate(tex2D(_MainTex, u).a));
                }
                c += tex2D(_BloomTex, u).rgb * _Bloom + tex2D(_RayTex, u).rgb;
                c *= _Expo;
                float l = dot(c, float3(0.3, 0.59, 0.11));
                c = lerp(float3(l, l, l), c, _Sat);
                c = (c - 0.5) * _Con + 0.5;
                c = clamp(c * _Tint, 0.0, 6.0);
                float3 hi = 0.76 + 0.24 * tanh((c - 0.76) / 0.24);
                c = lerp(c, hi, step(0.76, c));
                float v = smoothstep(0.32, 1.02, length(d * float2(1.0, 0.82)) * 1.38);
                c = lerp(c, _VigC, clamp(_Vig * v, 0.0, 1.0));
                c = lerp(c, _FadeC, _Fade);
                c += (hash(i.pos.xy + frac(_Time2) * 61.0) - 0.5) * _Grain;
                return float4(saturate(c), 1.0);
            }
            ENDCG
        }
    }
}
