// 카드 흐림 — 지금 낼 수 없는 손패 (시안 통합 UI v3.5 .card.hand.dim{filter:saturate(.15) brightness(.68) contrast(.8)}).
// CSS filter 함수를 적힌 순서대로, 감마(sRGB) 값 그대로 거른다 (색 공간 Gamma — 브라우저도 sRGB 에서 거른다). 단계마다 0 ~ 1 로 자른다.
// _Filter = (채도, 밝기, 대비, 안 씀) — (1, 1, 1) 이면 그대로. 값 · 넘어가는 시간은 CardView 가 넣고, TMP 글자색도 CardView 가 같은 식으로 거른다.
#ifndef CHAPCHU_CARD_FILTER_INCLUDED
#define CHAPCHU_CARD_FILTER_INCLUDED

float4 _Filter;

float3 CardFilter(float3 c)
{
    // saturate — Filter Effects 의 feColorMatrix saturate 행렬 (밝기 가중치 .213 · .715 · .072)
    const float3 luma = float3(0.213, 0.715, 0.072);
    float s = _Filter.x;
    float3 r;
    r.r = dot(c, luma + s * float3(0.787, -0.715, -0.072));
    r.g = dot(c, luma + s * float3(-0.213, 0.285, -0.072));
    r.b = dot(c, luma + s * float3(-0.213, -0.715, 0.928));
    c = saturate(r);
    c = saturate(c * _Filter.y);               // brightness
    c = saturate((c - 0.5) * _Filter.z + 0.5); // contrast
    return c;
}

// 등급 표면 빛 — 앞면을 비스듬히 지나가는 부드러운 빛 띠 (시안 「카드 반짝 · 빛 반사 v3」 1안 잔잔한 윤기, 10-ui.md 2-1 「카드 등급 연출」).
// 시안의 두 캔버스를 한 번에: 색 띠는 곱하기(mix-blend multiply), 그 가운데의 좁은 흰 띠는 위에 얹기. 띠 단면은 0 · .25 · .5 · .75 · 1 다섯 마디의 직선.
// 길이는 시안 캔버스 px = u. 띠 위치 s = dot(p, 축) + 더할 값 — 축 · 더할 값은 면마다 CardView 가 넣는다(그림 칸은 칸 가운데가 0 이라 더할 값이 다르다).
// _Sheen = (띠 가운데 s, 띠 반폭, 세기 0 ~ 1, 안 씀) · _SheenLine = (축 x, 축 y, 더할 값, 흰 띠 폭 ÷ 색 띠 폭)
// _SheenA · B · C = 곱하기 띠의 .25 · .5 · .75 마디 (rgb, 알파) — 양 끝은 알파 0 · _SheenWhite = 흰 띠 가운데 알파. 세기 0 이면 그대로.
float4 _Sheen, _SheenLine, _SheenA, _SheenB, _SheenC;
float _SheenWhite;

float4 SheenStops(float t)
{
    if (t <= 0.0 || t >= 1.0) return float4(1.0, 1.0, 1.0, 0.0);
    if (t < 0.25) return float4(_SheenA.rgb, _SheenA.a * t / 0.25);
    if (t < 0.5) return lerp(_SheenA, _SheenB, (t - 0.25) / 0.25);
    if (t < 0.75) return lerp(_SheenB, _SheenC, (t - 0.5) / 0.25);
    return float4(_SheenC.rgb, _SheenC.a * (1.0 - t) / 0.25);
}

// 흰 띠 단면 — 가운데 1, .25 · .75 마디 .3 (시안 bell)
float SheenBell(float t)
{
    t = abs(t - 0.5);
    return t >= 0.5 ? 0.0 : t < 0.25 ? lerp(1.0, 0.3, t / 0.25) : lerp(0.3, 0.0, (t - 0.25) / 0.25);
}

float3 CardSheen(float3 c, float2 p)
{
    if (_Sheen.z <= 0.001) return c;

    float s = dot(p, _SheenLine.xy) + _SheenLine.z - _Sheen.x;
    float4 band = SheenStops(s / (2.0 * _Sheen.y) + 0.5);
    c *= lerp(float3(1.0, 1.0, 1.0), band.rgb, band.a * _Sheen.z);
    float white = SheenBell(s / (2.0 * _Sheen.y * _SheenLine.w) + 0.5) * _SheenWhite * _Sheen.z;
    return lerp(c, float3(1.0, 1.0, 1.0), white);
}

#endif
