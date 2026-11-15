// 개판 오분전 눈 마을의 하늘 — 화면 전체를 덮는 판 (시안 DOGPILE.paintedSky · ground).
// Part 0 (구름보다 먼저): 지평선 기준 세로 그라데이션 + 왼쪽 해무리. Part 1 (구름 다음): 지평선 아래 땅 그라데이션 + 지평선 아지랑이.
// 지평선 · 화면 칸은 GaepanSky 가 매 프레임 전역 값으로 넣는다. 좌표는 렌더 텍스처 px, 위에서 아래로.
Shader "Chapchu/CardFx/Gaepan Sky"
{
    Properties
    {
        [Enum(Sky,0,Ground,1)] _Part ("Part (0 하늘 · 1 땅 · 아지랑이)", Float) = 0
        _Sky0 ("Sky 0% (꼭대기)", Color) = (0.373, 0.561, 0.816, 1)
        _Sky1 ("Sky 50%", Color) = (0.616, 0.741, 0.902, 1)
        _Sky2 ("Sky 80%", Color) = (0.863, 0.839, 0.886, 1)
        _Sky3 ("Sky 93%", Color) = (0.953, 0.851, 0.8, 1)
        _Sky4 ("Sky 100% (지평선)", Color) = (0.973, 0.863, 0.776, 1)
        _Glow ("Glow (해무리, 알파 = 가운데 진하기)", Color) = (1, 0.839, 0.667, 0.55)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Part;
            float4 _Sky0, _Sky1, _Sky2, _Sky3, _Sky4, _Glow;
            // (화면 칸 x, y, 너비, 높이) — 시안의 가로세로비 칸
            float4 _GpSkyBox;
            // (지평선 y — 칸 기준, 아지랑이 알파, 땅 켜짐 0/1, 화면 px / 시안 px)
            float4 _GpSkyHorizon;
            float4 _GpGroundTop, _GpGroundBottom;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // 메시 좌표는 쓰지 않고 uv 로 화면 전체를 덮는다 (렌더 텍스처에 그릴 때 뒤집힘은 _ProjectionParams.x)
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = float4(v.uv.x * 2.0 - 1.0, (v.uv.y * 2.0 - 1.0) * _ProjectionParams.x, 0.5, 1.0);
                o.uv = v.uv;
                return o;
            }

            float3 SkyColor(float t)
            {
                if (t < 0.5)
                    return lerp(_Sky0.rgb, _Sky1.rgb, t / 0.5);
                if (t < 0.8)
                    return lerp(_Sky1.rgb, _Sky2.rgb, (t - 0.5) / 0.3);
                if (t < 0.93)
                    return lerp(_Sky2.rgb, _Sky3.rgb, (t - 0.8) / 0.13);
                return lerp(_Sky3.rgb, _Sky4.rgb, saturate((t - 0.93) / 0.07));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = float2(i.uv.x * _ScreenParams.x, (1.0 - i.uv.y) * _ScreenParams.y) - _GpSkyBox.xy;
                float bw = _GpSkyBox.z, bh = _GpSkyBox.w, hy = _GpSkyHorizon.x;
                if (_Part < 0.5)
                {
                    float top = hy - bh * 1.15, bottom = hy + 4.0 * _GpSkyHorizon.w;
                    float3 col = SkyColor(saturate((p.y - top) / max(bottom - top, 1.0)));
                    float glow = _Glow.a * saturate(1.0 - length(p - float2(bw * 0.15, hy)) / max(bw * 0.7, 1.0));
                    return fixed4(lerp(col, _Glow.rgb, glow), 1.0);
                }

                // 지평선이 화면 아래로 내려가면 땅 · 아지랑이 모두 없다
                bool groundOn = _GpSkyHorizon.z > 0.5 && hy < bh;
                float4 o = float4(0, 0, 0, 0);
                float groundTop = max(0.0, hy);
                if (groundOn && p.y >= groundTop)
                    o = float4(lerp(_GpGroundTop.rgb, _GpGroundBottom.rgb, saturate((p.y - groundTop) / max(bh - groundTop, 1.0))), 1.0);
                float y0 = hy - bh * 0.06, y1 = hy + bh * 0.05;
                float band = (p.y - y0) / max(y1 - y0, 1.0);
                float haze = groundOn && band >= 0.0 && band <= 1.0 ? _GpSkyHorizon.y * (1.0 - abs(band - 0.5) * 2.0) : 0.0;
                float alpha = haze + o.a * (1.0 - haze);
                if (alpha <= 0.0)
                    discard;
                float3 rgb = (float3(1, 1, 1) * haze + o.rgb * o.a * (1.0 - haze)) / alpha;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
