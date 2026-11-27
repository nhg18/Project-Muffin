// 대소동 강아지 그림 12장 (나 · 초코 · 콩이 · 보리 × 몸 · 꼬리 · 다리) 중 하나를 골라 읽는다 — 미리 곱한 알파로 돌려준다.
#ifndef NUKE_DOG_TEX_INCLUDED
#define NUKE_DOG_TEX_INCLUDED

sampler2D _Tex0, _Tex1, _Tex2, _Tex3, _Tex4, _Tex5, _Tex6, _Tex7, _Tex8, _Tex9, _Tex10, _Tex11;

float4 dogTex(float index, float2 uv)
{
    float4 t;
    if (index < 0.5) t = tex2D(_Tex0, uv);
    else if (index < 1.5) t = tex2D(_Tex1, uv);
    else if (index < 2.5) t = tex2D(_Tex2, uv);
    else if (index < 3.5) t = tex2D(_Tex3, uv);
    else if (index < 4.5) t = tex2D(_Tex4, uv);
    else if (index < 5.5) t = tex2D(_Tex5, uv);
    else if (index < 6.5) t = tex2D(_Tex6, uv);
    else if (index < 7.5) t = tex2D(_Tex7, uv);
    else if (index < 8.5) t = tex2D(_Tex8, uv);
    else if (index < 9.5) t = tex2D(_Tex9, uv);
    else if (index < 10.5) t = tex2D(_Tex10, uv);
    else t = tex2D(_Tex11, uv);
    return float4(t.rgb * t.a, t.a);
}

#endif
