// 대소동 3D 장면이 같이 쓰는 값 · 잡음 — 시안 D3R COMMON · NUKE3D GX NZ 와 같다.
// 좌표는 시안 좌표 (유니티 월드 − _NkOrigin). 값은 NukeScene 이 매 프레임 전역으로 넣는다.
#ifndef NUKE_COMMON_INCLUDED
#define NUKE_COMMON_INCLUDED

float3 _NkOrigin;
float3 _NkFogC;
float3 _NkLd;       // 빛이 나아가는 방향
float3 _NkSunC;
float3 _NkShC;
float3 _NkAmb;
float3 _NkGnd;
float4 _NkPl;       // 점광 (xyz, 반지름)
float3 _NkPlC;
float2 _NkFx;       // 안개 (짙기, 최대)
float3 _NkZen;
float3 _NkHor;
float3 _NkSunS;
float _NkTime;
float _NkEmi;
float3 _NkGain;
float4x4 _NkLVP;    // 그림자 맵 (시안 좌표 → GL 클립)
sampler2D _NkShadowMap;
float _NkSmT;       // 그림자 맵 한 칸

// GLSL mod (음수에서도 0 ~ y)
#define NK_MOD(x, y) ((x) - (y) * floor((x) / (y)))

float nkHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float nkNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(nkHash(i), nkHash(i + float2(1, 0)), f.x), lerp(nkHash(i + float2(0, 1)), nkHash(i + float2(1, 1)), f.x), f.y);
}

float nkFbm3(float2 p)
{
    float a = 0.5;
    float s = 0.0;
    for (int i = 0; i < 3; i++)
    {
        s += a * nkNoise(p);
        p = p * 2.03 + float2(17.3, 9.1);
        a *= 0.5;
    }
    return s;
}

// 시안 shadowAt — 그림자 맵은 빛 방향 깊이(0~1)를 담은 RFloat. 5x5 대신 3x3 으로 고르게 섞는다
float nkShadow(float4 L, float bias)
{
    float3 q = L.xyz / L.w * 0.5 + 0.5;
    if (q.x < 0.0 || q.x > 1.0 || q.y < 0.0 || q.y > 1.0 || q.z > 1.0)
        return 1.0;
    float s = 0.0;
    for (int i = -1; i <= 1; i++)
    {
        for (int j = -1; j <= 1; j++)
        {
            float d = tex2Dlod(_NkShadowMap, float4(q.xy + float2(i, j) * _NkSmT * 1.6, 0, 0)).r;
            s += q.z - bias <= d ? 1.0 : 0.0;
        }
    }
    return s / 9.0;
}

// 시안 paper — 화면 칸마다 아주 작은 얼룩
float3 nkPaper(float3 c, float2 fragCoord)
{
    float g = nkHash(floor(fragCoord * 0.5));
    float g2 = nkNoise(fragCoord * 0.03);
    return c * (0.965 + 0.04 * g + 0.02 * g2);
}

#endif
