// 대소동 3D 장면의 물체 (건물 · 땅 · 보도 · 차 · 나무 · 가로등 · 미사일 · 불덩이) — 시안 D3R 의 FS cine() 와 같다.
// 정점 색 rgb = 바탕색, a = 재질 (1 무광 · .85 건물 외벽(창은 여기서 그림) · .7 도장/금속 · .55 유리 · .4 나뭇잎 · .25 발광).
// _Mat: 0 보통 · 3 땅(길 · 차선) · 4 보도 판. 건물마다 _MpA (창 폭 · 층고 · 1층 높이 · 양식) · _MpB (씨앗 · 불 켜진 비율 · 폭 · 깊이).
// 빛은 유니티 조명이 아니라 NukeScene 이 넣는 전역 값으로 직접 계산한다 (게임 화면의 다른 물체와 섞이지 않게).
Shader "Chapchu/Nuke/Cine"
{
    Properties
    {
        _Mat ("재질 (0 보통 · 3 땅 · 4 보도)", Float) = 0
        _MpA ("건물 A (창 폭 · 층고 · 1층 · 양식)", Vector) = (0, 0, 0, 0)
        _MpB ("건물 B (씨앗 · 켜진 비율 · 폭 · 깊이)", Vector) = (0, 0, 0, 0)
        _EmiSelf ("이 물체의 발광 (음수면 장면 값)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "DisableBatching" = "True" }

        Pass
        {
            Cull Back
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "NukeCommon.cginc"

            float _Mat;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MpA)
                UNITY_DEFINE_INSTANCED_PROP(float4, _MpB)
                UNITY_DEFINE_INSTANCED_PROP(float, _EmiSelf)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 vW : TEXCOORD0;   // xyz 시안 좌표, w 발광
                float3 vN : TEXCOORD1;
                float4 vC : TEXCOORD2;   // rgb 색, a 재질
                float3 vO : TEXCOORD3;
                float3 vNo : TEXCOORD4;
                float4 vL : TEXCOORD5;
                float4 mpA : TEXCOORD6;
                float4 mpB : TEXCOORD7;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float4 wu = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, wu);
                float3 w = wu.xyz - _NkOrigin;
                float3 nw = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float emiSelf = UNITY_ACCESS_INSTANCED_PROP(Props, _EmiSelf);
                o.vW = float4(w, emiSelf < 0.0 ? _NkEmi : emiSelf);
                o.vN = nw;
                o.vC = v.color;
                o.vO = v.vertex.xyz;
                o.vNo = normalize(v.normal);
                o.vL = mul(_NkLVP, float4(w + nw * 0.06, 1.0));
                o.mpA = UNITY_ACCESS_INSTANCED_PROP(Props, _MpA);
                o.mpB = UNITY_ACCESS_INSTANCED_PROP(Props, _MpB);
                return o;
            }

            float fb2(float2 p)
            {
                return nkNoise(p) * 0.55 + nkNoise(p * 2.13 + 7.7) * 0.3 + nkNoise(p * 4.31 + 3.1) * 0.15;
            }

            // 반사에 쓰는 하늘 + 가짜 건물 숲
            float3 envC(float3 d)
            {
                float3 c = lerp(_NkHor, _NkZen, pow(smoothstep(-0.02, 0.7, d.y), 0.55));
                float sd = max(dot(d, -_NkLd), 0.0);
                c += _NkSunS * (pow(sd, 60.0) * 1.1 + pow(sd, 6.0) * 0.3);
                float az = atan2(d.x, d.z) * 9.0;
                float sk = 0.04 + 0.42 * nkHash(float2(floor(az), 3.0)) * nkHash(float2(floor(az * 0.37), 9.0));
                c = lerp(c, lerp(_NkGnd * 0.5, _NkHor * 0.45, 0.4), smoothstep(sk + 0.012, sk - 0.012, d.y) * 0.88);
                if (d.y < 0.0)
                    c = lerp(c, _NkGnd * 0.55, smoothstep(0.0, -0.3, d.y));
                return c;
            }

            // 땅: 아스팔트 · 차선 · 횡단보도 · 맨홀 (길은 x=52k, z=30+52k)
            float3 gndMat(float2 P, float px, inout float3 N, out float ks, out float sp)
            {
                float xs = NK_MOD(P.x + 26.0, 52.0) - 26.0;
                float zs = NK_MOD(P.y - 4.0, 52.0) - 26.0;
                float dx = abs(xs);
                float dz = abs(zs);
                bool rx = dx < 6.0;
                bool rz = dz < 6.0;
                float n1 = fb2(P * 0.23);
                float n2 = nkNoise(P * 2.7);
                float n3 = nkNoise(P * 13.0);
                float df = smoothstep(0.25, 0.03, px);
                if (!(rx || rz))
                {
                    ks = 0.03;
                    sp = 20.0;
                    float t = nkHash(floor(P / 0.6));
                    return float3(0.52, 0.5, 0.47) * (0.82 + 0.2 * n1) * (1.0 - 0.06 * t * df);
                }
                float3 c = float3(0.25, 0.25, 0.262) * (0.84 + 0.22 * n1 + 0.05 * n2 * df + 0.05 * (n3 - 0.5) * df);
                float2 pc = floor(P / float2(6.3, 9.1));
                float2 pf = frac(P / float2(6.3, 9.1));
                float ph = nkHash(pc);
                if (ph > 0.78)
                {
                    float r = step(0.12, pf.x) * step(pf.x, 0.2 + 0.6 * nkHash(pc + 3.0)) * step(0.1, pf.y) * step(pf.y, 0.2 + 0.7 * nkHash(pc + 7.0));
                    c *= 1.0 - 0.2 * r;
                }
                float cr = abs(fb2(P * 0.55 + 11.0) - 0.5);
                c *= 1.0 - 0.3 * smoothstep(0.006, 0.001, cr) * df;
                float mk = 0.0;
                float3 mc = float3(0.78, 0.77, 0.72);
                if (rx != rz)
                {
                    float a = rx ? dz : dx;
                    float al = rx ? P.y : P.x;
                    float ac = rx ? P.x : P.y;
                    float s = rx ? dx : dz;
                    float sg = rx ? xs * zs : -zs * xs;
                    float w = abs(NK_MOD(s, 3.0) - 1.5);
                    c *= 1.0 + 0.08 * smoothstep(0.3, 0.0, abs(w - 0.75));
                    c *= lerp(0.7, 1.0, smoothstep(0.0, 1.2, 6.0 - s));
                    float lw = clamp(0.17 / px, 0.0, 1.0);
                    float yl = smoothstep(0.075 + px, 0.075, abs(s - 0.2)) * step(11.2, a) * lw;
                    float dsh = smoothstep(0.075 + px, 0.075, abs(s - 3.0)) * step(NK_MOD(al, 6.0), 2.6) * step(11.2, a) * lw;
                    float ed = smoothstep(0.06 + px, 0.06, abs(s - 5.55)) * step(11.2, a) * lw;
                    float zb = step(7.4, a) * step(a, 10.0) * lerp(0.5, smoothstep(0.3 + px, 0.3, abs(NK_MOD(ac, 1.2) - 0.6)), clamp(0.5 / px, 0.0, 1.0)) * step(s, 5.6);
                    float sl = step(10.5, a) * step(a, 10.95) * step(s, 5.6) * step(sg, 0.0);
                    mk = max(max(dsh, ed), max(zb, sl));
                    if (yl > mk)
                    {
                        mk = yl;
                        mc = float3(0.8, 0.58, 0.13);
                    }
                    mk *= 0.45 + 0.55 * smoothstep(0.2, 0.55, n2 * 0.5 + n3 * 0.5 + 0.12);
                }
                c = lerp(c, mc, mk * 0.92);
                float2 mq = float2(26.0, 17.0);
                float2 mh = frac(P / mq) * mq - float2(4.4 + 17.0 * nkHash(floor(P / mq)), 8.5);
                float md = length(mh);
                if (md < 0.48)
                    c = lerp(float3(0.17, 0.16, 0.155), float3(0.27, 0.26, 0.25), step(0.5, frac(md * 9.0))) * (0.9 + 0.2 * n3);
                N = normalize(N + float3(n3 - 0.5, 0.0, nkNoise(P * 13.0 + 5.0) - 0.5) * 0.1 * df);
                ks = 0.22 + 0.3 * n1;
                sp = 30.0;
                return c;
            }

            // 보도 블록 판 (40x40, 높이 0.2)
            float3 paveMat(float3 O, float3 No, float3 W, float px, out float ks, out float sp)
            {
                ks = 0.04;
                sp = 16.0;
                float e = 20.0 - max(abs(O.x), abs(O.z));
                float n1 = fb2(W.xz * 0.31);
                float n3 = nkNoise(W.xz * 9.0);
                float df = smoothstep(0.2, 0.03, px);
                if (No.y < 0.5)
                    return float3(0.6, 0.59, 0.56) * (0.8 + 0.25 * n3);
                if (e < 0.34)
                    return float3(0.64, 0.63, 0.6) * (0.82 + 0.2 * n1) * (1.0 - 0.3 * smoothstep(0.03, 0.0, abs(frac((O.x + O.z) * 0.55) - 0.5) - 0.46) * df);
                float2 t = O.xz / float2(0.9, 0.45);
                t.x += 0.5 * NK_MOD(floor(t.y), 2.0);
                float2 f = frac(t);
                float jn = clamp(step(f.x, 0.035) + step(f.y, 0.07), 0.0, 1.0) * df;
                float tv = nkHash(floor(t));
                float3 c = float3(0.53, 0.51, 0.48) * (0.82 + 0.14 * tv * df + 0.2 * n1);
                c *= 1.0 - 0.28 * jn;
                c *= 1.0 - 0.2 * smoothstep(0.6, 0.72, nkNoise(W.xz * 1.3 + 3.0));
                c *= lerp(0.86, 1.0, smoothstep(0.3, 1.2, e));
                return c;
            }

            // 방 안 광선 추적(인테리어 매핑): 창 안쪽에 진짜 깊이가 보인다. xyz=닿은 점, w=0 뒷벽 1 천장 2 바닥 3 옆벽
            float4 room(float3 p0, float3 d, float hwid, float y0, float y1, float Dp)
            {
                float dx = abs(d.x) < 1e-4 ? (d.x < 0.0 ? -1e-4 : 1e-4) : d.x;
                float dy = abs(d.y) < 1e-4 ? (d.y < 0.0 ? -1e-4 : 1e-4) : d.y;
                float tx = ((dx > 0.0 ? hwid : -hwid) - p0.x) / dx;
                float ty = ((dy > 0.0 ? y1 : y0) - p0.y) / dy;
                float tz = Dp / d.z;
                float t = clamp(min(tz, min(tx, ty)), 0.0, tz);
                float3 hp = p0 + float3(dx, dy, d.z) * t;
                hp.z = clamp(hp.z, 0.0, Dp);
                return float4(hp, t >= tz - 1e-4 ? 0.0 : (t >= ty - 1e-4 ? (dy > 0.0 ? 1.0 : 2.0) : 3.0));
            }

            // 건물 외벽: 창 · 창틀 · 벽 재질 · 1층 상가. rgb=벽 색, a=유리 · 간판 비율, em=그 빛
            float4 facade(float3 c, float3 N, float3 V, float sh, float px, float3 vO, float3 vNo, float4 mpA, float4 mpB, float emi, out float3 em, inout float ks, inout float sp)
            {
                em = 0;
                bool zf = abs(vNo.z) > 0.5;
                float W = zf ? mpB.z : mpB.w;
                float u = zf ? vO.x * sign(vNo.z) : -vO.z * sign(vNo.x);
                float v = vO.y;
                float sty = mpA.w;
                float fh = mpA.y;
                float g0 = mpA.z;
                float nb = max(1.0, floor(W / mpA.x + 0.5));
                float bw = W / nb;
                float seed = mpB.x;
                float uu = u / bw + nb * 0.5;
                float ci = floor(uu);
                float cu = frac(uu);
                float fj = (v - g0) / fh;
                float j = floor(fj);
                float cv = frac(fj);
                float ax = abs(cu - 0.5) * bw;
                float ed = min(cu, 1.0 - cu) * bw;
                bool cw = sty > 2.5 && sty < 3.5;
                float wx = sty < 0.5 ? 0.7 : sty < 1.5 ? 0.5 : sty < 2.5 ? 0.56 : cw ? 0.955 : 0.52;
                float s0 = sty < 0.5 ? 0.28 : sty < 1.5 ? 0.3 : sty < 2.5 ? 0.26 : cw ? 0.24 : 0.32;
                float s1 = sty < 0.5 ? 0.86 : sty < 1.5 ? 0.84 : sty < 2.5 ? 0.9 : cw ? 1.01 : 0.82;
                float hw = wx * bw * 0.5;
                float df = smoothstep(0.5, 0.06, px);
                float lon = min(emi, 1.2);
                c *= 0.9 + 0.2 * nkHash(float2(seed, 7.0));
                float3 T = normalize(cross(float3(0.0, 1.0, 0.0), N));
                float3 B = cross(N, T);
                float3 dv = -V;
                float3 d = float3(dot(dv, T), dot(dv, B), max(-dot(dv, N), 0.03));
                float3 Rf = reflect(-V, N);
                float F = 0.05 + 0.95 * pow(1.0 - max(dot(N, V), 0.0), 5.0);
                float3 env = envC(Rf) * (F * 0.86 + 0.13) + _NkSunC * pow(max(dot(Rf, -_NkLd), 0.0), 300.0) * 1.6 * sh;
                float3 tg = lerp(float3(0.72, 0.84, 0.9), float3(0.86, 0.74, 0.54), step(0.72, nkHash(float2(seed, 2.0))));
                float wm = 0.0;
                float frm = 0.0;
                float sgm = 0.0;
                float3 wall = c;
                float3 inner = 0;
                float3 sgc = 0;
                float2 rid = float2(ci, j) + seed * 17.0;
                float r1 = nkHash(rid);
                float r2 = nkHash(rid + 5.3);
                float r3 = nkHash(rid + 9.1);
                if (v < g0)
                {
                    // 1층: 상가 / 로비
                    float sp2 = frac(uu * 0.5);
                    float si = floor(uu * 0.5);
                    float h1 = nkHash(float2(si, seed + 1.0));
                    float h2 = nkHash(float2(si, seed + 4.0));
                    float shop = (cw || sty < 0.5) ? 0.0 : 1.0;
                    float top = g0 - (shop > 0.5 ? 1.3 : 0.55);
                    float pil = smoothstep(0.24 + px, 0.24, ed);
                    wm = (1.0 - pil) * smoothstep(-px, px, v - 0.45) * smoothstep(-px, px, top - v);
                    float fi = (1.0 - smoothstep(0.31 + px, 0.31, ed)) * smoothstep(-px, px, v - 0.52) * smoothstep(-px, px, top - 0.07 - v) * (1.0 - smoothstep(0.03 + px, 0.03, ax) * df);
                    frm = wm * (1.0 - fi);
                    wall = lerp(c * 0.72, float3(0.26, 0.26, 0.28), step(v, 0.45));
                    float4 hp = room(float3((sp2 - 0.5) * 2.0 * bw, v, 0.0), d, bw, 0.0, g0 - 0.2, shop > 0.5 ? 5.5 : 9.0);
                    float dep = hp.z / (shop > 0.5 ? 5.5 : 9.0);
                    float lit = lon * step(0.16, h2);
                    float3 rc;
                    if (shop > 0.5)
                    {
                        if (hp.w < 0.5)
                        {
                            float2 q = floor(float2(hp.x * 6.0 + si * 7.0, hp.y * 2.6));
                            rc = 0.5 + 0.45 * cos(6.283 * (nkHash(q) + float3(0.0, 0.33, 0.67)));
                            rc = lerp(rc, float3(0.8, 0.74, 0.64), 0.68) * (0.3 + 0.7 * step(0.22, frac(hp.y * 2.6))) * (0.8 + 0.2 * step(0.12, frac(hp.x * 6.0)));
                        }
                        else if (hp.w < 1.5)
                            rc = float3(0.9, 0.88, 0.82) + lit * 1.4 * step(0.4, frac(hp.z * 0.8)) * step(frac(hp.x * 0.7), 0.5);
                        else if (hp.w < 2.5)
                            rc = float3(0.42, 0.38, 0.33) * (0.8 + 0.3 * step(0.5, frac(hp.x * 1.2 + floor(hp.z * 1.2) * 0.5)));
                        else
                            rc = float3(0.7, 0.66, 0.58) * (0.75 + 0.25 * step(0.3, frac(hp.z * 0.9))) * (0.6 + 0.4 * smoothstep(0.0, 1.4, hp.y));
                        inner = rc * (lit * (1.1 - 0.5 * dep) * float3(1.0, 0.92, 0.78) + 0.13 * (_NkAmb + _NkSunC * 0.2));
                        if (h1 > 0.84)
                        {
                            wm = 0.0;
                            frm = 0.0;
                            wall = float3(0.5, 0.52, 0.54) * (0.72 + 0.28 * step(0.5, frac(v * 8.0))) * step(0.45, v) + wall * step(v, 0.45);
                        }
                        if (v > g0 - 1.2 && v < g0 - 0.2)
                        {
                            float3 sc = 0.5 + 0.5 * cos(6.283 * (h1 * 3.7 + float3(0.0, 0.33, 0.67)));
                            sc = lerp(sc, float3(0.92, 0.92, 0.92), 0.12);
                            float gl = step(0.3, nkHash(float2(floor(u * 1.1), si + seed))) * smoothstep(0.2, 0.45, abs(frac(u * 6.5) - 0.5) * 2.0) * smoothstep(0.2 + px, 0.2 - px, abs(v - (g0 - 0.7))) * step(0.14, sp2) * step(sp2, 0.86) * df;
                            sgc = lerp(sc * 0.8, float3(1.0, 0.98, 0.94), gl) * (0.3 * (_NkAmb + _NkSunC * 0.35 * sh) + 0.95 * lon * step(h2, 0.93));
                            sgm = step(0.03, sp2) * step(sp2, 0.97);
                        }
                    }
                    else
                    {
                        if (hp.w < 0.5)
                        {
                            float q = frac(hp.x * 0.28 + si * 0.37);
                            rc = float3(0.66, 0.6, 0.52) * (0.75 + 0.25 * step(0.5, frac(hp.x * 0.9)));
                            rc = lerp(rc, float3(0.42, 0.43, 0.45), step(0.62, q) * step(hp.y, 2.6) * step(0.08, frac(q * 9.0)));
                            rc *= 1.0 - 0.5 * step(hp.y, 1.1) * step(q, 0.4);
                        }
                        else
                        {
                            rc = hp.w < 1.5 ? float3(0.8, 0.79, 0.76) + lon * 0.9 * step(0.6, frac(hp.z * 0.5)) * step(0.5, frac(hp.x * 0.6))
                                : hp.w < 2.5 ? float3(0.42, 0.4, 0.38) * (0.8 + 0.3 * step(0.5, frac(hp.x * 0.5 + floor(hp.z * 0.5) * 0.5)))
                                : float3(0.58, 0.55, 0.5) * (0.72 + 0.28 * step(0.25, frac(hp.z * 0.55))) * (0.55 + 0.45 * smoothstep(0.0, 1.6, hp.y));
                        }
                        inner = rc * (lon * (0.72 - 0.3 * dep) * float3(1.0, 0.93, 0.82) + 0.16 * (_NkAmb + _NkSunC * 0.2));
                    }
                    inner = lerp(float3(0.6, 0.52, 0.4) * (lit * 0.9 + 0.1), inner, df);
                }
                else
                {
                    // 위층
                    float a1 = smoothstep(-px, px, hw - ax);
                    float a2 = smoothstep(-px, px, (cv - s0) * fh);
                    float a3 = smoothstep(-px, px, (s1 - cv) * fh);
                    wm = a1 * a2 * a3;
                    float fi = smoothstep(-px, px, hw - ax - 0.07) * smoothstep(-px, px, (cv - s0) * fh - 0.07) * smoothstep(-px, px, (s1 - cv) * fh - 0.07);
                    float bar = cw ? 0.0 : max(smoothstep(0.03 + px, 0.03, ax) * step(1.5, hw * 2.0), smoothstep(0.03 + px, 0.03, abs(cv - (s0 + (s1 - s0) * 0.68)) * fh));
                    frm = wm * (1.0 - fi * (1.0 - bar * df));
                    float lit = step(r1, mpB.y) * lon;
                    float Dp = 3.5 + 3.0 * r3;
                    float4 hp = room(float3((cu - 0.5) * bw, cv * fh, 0.0), d, bw * 0.5, 0.0, fh, Dp);
                    float3 wt = lerp(float3(0.78, 0.7, 0.56), float3(0.56, 0.64, 0.7), r2);
                    float3 rc;
                    if (hp.w < 0.5)
                    {
                        rc = wt;
                        float q = hp.x / bw + 0.5;
                        rc *= 1.0 - 0.5 * step(hp.y, fh * 0.36) * step(0.12, q) * step(q, 0.2 + 0.5 * r3);
                        rc *= 1.0 - 0.3 * step(0.74, q) * step(q, 0.92) * step(hp.y, fh * 0.72);
                    }
                    else if (hp.w < 1.5)
                    {
                        rc = float3(0.86, 0.85, 0.8);
                        float2 cp = frac(hp.xz / 1.5);
                        rc += lit * 1.6 * step(0.3, cp.x) * step(cp.x, 0.7) * step(0.38, cp.y) * step(cp.y, 0.62);
                    }
                    else if (hp.w < 2.5)
                        rc = float3(0.32, 0.26, 0.2) * (0.7 + 0.5 * r2);
                    else
                        rc = wt * 0.82;
                    float3 lc = lerp(float3(1.0, 0.8, 0.52), float3(0.86, 0.93, 1.0), step(0.68, r2));
                    float dep = hp.z / Dp;
                    inner = rc * (lit * (1.05 - 0.5 * dep) * lc + (0.16 - 0.1 * dep) * (_NkAmb + _NkSunC * 0.25));
                    if (r3 > 0.58)
                    {
                        float bl = s1 - (s1 - s0) * (0.25 + 0.7 * r2);
                        inner = lerp(inner, float3(0.8, 0.78, 0.72) * (lit * 0.75 * lc + 0.4 * (_NkAmb + _NkSunC * 0.3)) * (0.8 + 0.2 * step(0.5, frac(cv * fh * 13.0))), step(bl, cv) * 0.92);
                    }
                    inner = lerp(wt * (lit * 0.8 * lc + 0.1 * _NkAmb), inner, df);
                    if (sty < 0.5)
                    {
                        float jl = max(smoothstep(0.025 + px, 0.025, ed), smoothstep(0.025 + px, 0.025, cv * fh));
                        wall = c * (0.9 + 0.16 * fb2(float2(u, v) * 0.45)) * (1.0 - 0.3 * jl * df);
                    }
                    else if (sty < 1.5)
                    {
                        float2 b = float2(u / 0.26, v / 0.085);
                        b.x += 0.5 * NK_MOD(floor(b.y), 2.0);
                        float2 bf = frac(b);
                        float mo = clamp(step(bf.x, 0.07) + step(bf.y, 0.16), 0.0, 1.0);
                        float bd = smoothstep(0.07, 0.012, px);
                        wall = lerp(c, lerp(c * (0.78 + 0.4 * nkHash(floor(b))), float3(0.6, 0.58, 0.54), mo), bd);
                        float li = step(ax, hw + 0.14) * step(s1, cv) * step(cv, s1 + 0.1) + step(ax, hw + 0.1) * step(s0 - 0.06, cv) * step(cv, s0);
                        wall = lerp(wall, float3(0.7, 0.67, 0.6), clamp(li, 0.0, 1.0));
                    }
                    else if (sty < 2.5)
                    {
                        float pier = smoothstep(hw + 0.02, hw + 0.1, ax);
                        wall = lerp(c * 0.74 * (1.0 + 0.08 * step(0.5, frac(u * 2.5))), c * (1.0 + 0.06 * sin(u * 20.0) * df), pier);
                        float2 b = float2(u / 1.1, v / 0.55);
                        b.x += 0.5 * NK_MOD(floor(b.y), 2.0);
                        float2 bf = frac(b);
                        wall *= 1.0 - 0.18 * clamp(step(bf.x, 0.03) + step(bf.y, 0.05), 0.0, 1.0) * df * pier;
                    }
                    else if (cw)
                    {
                        wall = c * 0.42;
                        ks = 0.9;
                        sp = 90.0;
                        float ml = max(smoothstep(0.04 + px, 0.04, ed), max(smoothstep(0.05 + px, 0.05, cv * fh), smoothstep(0.05 + px, 0.05, abs(cv - s0) * fh)));
                        wall = lerp(wall, float3(0.6, 0.62, 0.65), ml * 0.9);
                    }
                    else
                    {
                        wall = c * (0.92 + 0.12 * fb2(float2(u, v) * 0.7));
                        float tr = step(ax, hw + 0.1) * step(s0 - 0.035, cv) * step(cv, s1 + 0.035);
                        wall = lerp(wall, c * 1.18, tr * 0.8);
                        if (nkHash(rid + 2.0) > 0.7)
                        {
                            float ac = step(ax, 0.45) * step(s0 - 0.2, cv) * step(cv, s0 - 0.035);
                            wall = lerp(wall, float3(0.62, 0.63, 0.62) * (0.75 + 0.25 * step(0.5, frac(cv * fh * 22.0))), ac * df);
                        }
                    }
                    if (!cw)
                        wall *= 1.0 - 0.24 * fb2(float2(u * 5.0, v * 0.35 + seed)) * smoothstep(s0, s0 - 0.3, cv) * step(ax, hw + 0.15);
                }
                if (!cw)
                    wall *= (0.86 + 0.22 * fb2(float2(u * 0.13 + seed, v * 0.09))) * lerp(0.7, 1.0, smoothstep(0.0, 1.8, v));
                wall = lerp(wall, cw ? float3(0.52, 0.54, 0.57) : c * 0.42, frm);
                float gm = max(wm - frm, 0.0);
                em = min(inner, float3(3.0, 3.0, 3.0)) * tg * (1.0 - F) + min(env, float3(4.0, 4.0, 4.0));
                if (sgm > 0.0)
                {
                    em = lerp(em, sgc, sgm);
                    wall = lerp(wall, sgc, sgm);
                    gm = max(gm, sgm);
                }
                return float4(wall, gm);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 vW = i.vW.xyz;
                float emi = i.vW.w;
                float vM = i.vC.a;
                float3 eye = _WorldSpaceCameraPos - _NkOrigin;
                float3 c = i.vC.rgb;
                float3 N = normalize(i.vN);
                float3 V = normalize(eye - vW);
                float3 L = -_NkLd;
                float3 em = 0;
                float ds = distance(vW, eye);
                float sh = nkShadow(i.vL, 0.0014);
                float ks = 0.04;
                float sp = 16.0;
                float refl = 0.0;
                float ao = 1.0;
                float gm = 0.0;
                float px = max(length(fwidth(vW)), 1e-4);
                bool fol = false;
                if (_Mat > 2.5 && _Mat < 3.5)
                {
                    c = gndMat(vW.xz, px, N, ks, sp);
                }
                else if (_Mat > 3.5 && _Mat < 4.5)
                {
                    c = paveMat(i.vO, i.vNo, vW, px, ks, sp);
                }
                else if (vM > 0.925)
                {
                    float n1 = nkNoise(vW.xz * 0.55 + vW.y * 0.4);
                    float n2 = nkNoise(vW.xy * 2.7 + vW.z * 2.1);
                    c *= (0.86 + 0.22 * n1) * (0.94 + 0.12 * n2);
                    if (N.y > 0.7 && vW.y > 6.0)
                        c *= 0.78 + 0.34 * nkNoise(vW.xz * 9.0);
                    ao = lerp(lerp(0.72, 1.0, smoothstep(0.0, 2.4, vW.y)), 1.0, step(0.5, N.y));
                }
                else if (vM > 0.775)
                {
                    float4 f = facade(c, N, V, sh, px, i.vO, normalize(i.vNo), i.mpA, i.mpB, emi, em, ks, sp);
                    c = f.rgb;
                    gm = f.a;
                }
                else if (vM > 0.625)
                {
                    refl = 0.05 + 0.5 * pow(1.0 - max(dot(N, V), 0.0), 4.0);
                    ks = 1.1;
                    sp = 150.0;
                }
                else if (vM > 0.475)
                {
                    refl = 0.14 + 0.8 * pow(1.0 - max(dot(N, V), 0.0), 4.0);
                    ks = 2.0;
                    sp = 260.0;
                    c *= 0.45;
                }
                else if (vM > 0.325)
                {
                    fol = true;
                    float nz = nkNoise(vW.xz * 11.0 + vW.y * 8.0) * 0.55 + nkNoise(vW.xy * 29.0 + vW.z * 19.0) * 0.45;
                    float edg = abs(dot(N, V));
                    if (nz > 0.33 + edg * 0.62 && ds < 120.0)
                        discard;
                    c *= (0.4 + 1.0 * nkNoise(vW.xz * 4.0 + vW.y * 3.0)) * (0.55 + 0.75 * nkNoise(vW.xy * 17.0 + vW.z * 11.0)) * lerp(0.55, 1.0, smoothstep(-0.5, 0.5, N.y));
                }
                else
                {
                    float fe = (1.0 - exp(-ds * _NkFx.x)) * _NkFx.y;
                    return float4(lerp(c * emi, _NkFogC, fe * 0.5), 1.0);
                }
                float ndl = dot(N, L);
                float dif = fol ? clamp(ndl * 0.6 + 0.4, 0.0, 1.0) * lerp(0.5, 1.0, sh) : clamp(ndl, 0.0, 1.0) * sh;
                float3 ambc = lerp(_NkGnd, _NkAmb, N.y * 0.5 + 0.5);
                float3 dl = _NkPl.xyz - vW;
                float d2 = dot(dl, dl);
                float att = _NkPl.w > 0.0 ? _NkPl.w * _NkPl.w / (d2 + _NkPl.w * _NkPl.w) : 0.0;
                float3 pc = _NkPlC * att * (max(dot(N, normalize(dl)), 0.0) * 0.75 + 0.25);
                float3 col = c * (ambc * ao + _NkSunC * dif + pc);
                if (fol)
                    col += c * _NkSunC * pow(max(dot(-V, L), 0.0), 3.0) * 0.5 * lerp(0.35, 1.0, sh) * float3(1.1, 1.0, 0.5);
                col += _NkSunC * ks * pow(max(dot(N, normalize(L + V)), 0.0), sp) * sh * smoothstep(0.0, 0.08, ndl);
                if (refl > 0.0)
                    col = lerp(col, envC(reflect(-V, N)) + _NkPlC * att * 0.5, refl);
                col = lerp(col, em, gm);
                float f = (1.0 - exp(-ds * _NkFx.x)) * _NkFx.y;
                f = max(f, (1.0 - exp(-ds * _NkFx.x * 2.2)) * _NkFx.y * smoothstep(8.0, 0.0, vW.y) * 0.6);
                float3 fc = _NkFogC + _NkSunS * pow(max(dot(-V, L), 0.0), 8.0) * 0.28;
                return float4(lerp(col, fc, clamp(f, 0.0, 1.0)), 1.0);
            }
            ENDCG
        }

        // 카메라 깊이 텍스처(주변광 차폐 · 심도 · 입자 가림)에 들어가는 깊이
        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(appdata_base v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
