// 「GOAT」 컷인의 하늘 — 화면 전체를 덮는 세로 그라데이션 (시안 goat_cutin.js 의 PB).
// 지평선(_GoatHorizon, 화면 세로 −1 ~ 1)에서 위로는 꼭대기 색, 아래로는 바닥 색으로. 지평선은 GoatWorld 가 매 프레임 전역 값으로 넣는다.
Shader "Chapchu/CardFx/Goat Sky"
{
    Properties
    {
        _Top ("Top (꼭대기)", Color) = (0.04, 0.05, 0.06, 1)
        _Horizon ("Horizon (지평선)", Color) = (0.2, 0.21, 0.23, 1)
        _Bottom ("Bottom (바닥)", Color) = (0.05, 0.05, 0.06, 1)
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Top, _Horizon, _Bottom;
            float _GoatHorizon;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float y : TEXCOORD0;
            };

            // 메시 좌표는 쓰지 않고 uv 로 화면 전체를 덮는다 (렌더 텍스처에 그릴 때 뒤집힘은 _ProjectionParams.x)
            v2f vert(appdata v)
            {
                v2f o;
                o.y = v.uv.y * 2.0 - 1.0;
                o.pos = float4(v.uv.x * 2.0 - 1.0, o.y * _ProjectionParams.x, 0.5, 1.0);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = i.y - _GoatHorizon;
                float3 col = d > 0.0 ? lerp(_Horizon.rgb, _Top.rgb, smoothstep(0.0, 1.0, d)) : lerp(_Horizon.rgb, _Bottom.rgb, smoothstep(0.0, 0.7, -d));
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
