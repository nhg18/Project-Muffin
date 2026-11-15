// 개판 오분전 3D 장면(눈 마을)이 같이 쓰는 값 · 함수. 시안 「찹츄 카드 이펙트 v2」 D3R 의 COMMON · 빛 · 안개와 같다.
// 값은 GaepanWorld 가 매 프레임 전역(Shader.SetGlobal*)으로 넣는다.
#ifndef GAEPAN_COMMON_INCLUDED
#define GAEPAN_COMMON_INCLUDED

// xyz = 빛이 나아가는 방향 (정규화)
float4 _GpLightDir;
// 햇빛 · 그늘 색
float4 _GpSun;
float4 _GpShade;
// 안개 색 · 거리 (x 시작, y 끝)
float4 _GpFog;
float4 _GpFogRange;
// x = 카메라 초점 거리(화면 px), y = 화면 px / 시안 px (높이 1080 기준), z = 땅 높이(월드 y)
float4 _GpView;

float GpHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float GpNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(GpHash(i), GpHash(i + float2(1, 0)), f.x), lerp(GpHash(i + float2(0, 1)), GpHash(i + float2(1, 1)), f.x), f.y);
}

// 시안 px 로 바꾼 화면 좌표 (빗금 · 종이 결의 간격이 화면 크기와 무관하게)
float2 GpScreenPx(float4 screenPos)
{
    return screenPos.xy / max(screenPos.w, 1e-5) * _ScreenParams.xy / max(_GpView.y, 1e-3);
}

// 종이 결
float3 GpPaper(float3 c, float2 px)
{
    float g = GpHash(floor(px * 0.5));
    float g2 = GpNoise(px * 0.03);
    return c * (0.965 + 0.04 * g + 0.02 * g2);
}

// 그늘의 크레용 빗금 (1 = 빗금 자리)
float GpHatch(float2 px)
{
    return step(0.62, frac((px.x - px.y) * 0.085 + GpNoise(px * 0.04) * 0.7));
}

// 안개 섞는 비율
float GpFogAmount(float3 worldPos)
{
    float d = distance(worldPos, _WorldSpaceCameraPos);
    return saturate((d - _GpFogRange.x) / max(_GpFogRange.y - _GpFogRange.x, 1e-3)) * 0.88;
}

#endif
