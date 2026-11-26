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

#endif
