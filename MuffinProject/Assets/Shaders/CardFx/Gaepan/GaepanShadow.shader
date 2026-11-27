// 개판 오분전 눈 마을의 그림자 — 물건을 빛 방향으로 땅(평면)에 눕혀 그늘 색으로 곱한다 (시안 D3R 의 그림자 맵 대신).
// 땅 색 × 햇빛 → 땅 색 × 그늘빛 이 되게 (그늘 / 햇빛) 비율을 곱한다. 비율이 1 을 넘는 파랑 때문에 두 배 곱하기(DstColor · SrcColor)로 섞는다.
// 겹친 그림자가 두 번 어두워지지 않게 스텐실로 한 번만 칠한다. 스프라이트(강아지)는 그림의 알파로 모양을 자른다.
Shader "Chapchu/CardFx/Gaepan Shadow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (스프라이트면 모양을 자른다)", 2D) = "white" {}
        _Cutoff ("Cutoff (그림 알파가 이보다 작으면 그림자 없음)", Range(0, 1)) = 0
        _Ratio ("Shade / Sun (그늘빛 ÷ 햇빛)", Vector) = (0.66, 0.779, 1.105, 0)
        _PlaneY ("Plane Y (그림자가 눕는 높이 — 땅 기준)", Float) = 0.03
        [Toggle] _FromObject ("Plane From Object (켜면 물건 원점 높이 기준)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Geometry+50" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Blend DstColor SrcColor
        Stencil
        {
            Ref 1
            Comp NotEqual
            Pass Replace
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "GaepanCommon.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Cutoff;
            float4 _Ratio;
            float _PlaneY;
            float _FromObject;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float alpha : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                v2f o;
                float4 world = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0));
                float plane = (_FromObject > 0.5 ? unity_ObjectToWorld._m13 : _GpView.z) + _PlaneY;
                // 평면보다 위면 빛 방향으로 내려 눕히고, 아래면 그 자리 평면으로
                float t = max((plane - world.y) / min(_GpLightDir.y, -1e-3), 0.0);
                world.xyz += _GpLightDir.xyz * t;
                world.y = plane;
                o.pos = mul(UNITY_MATRIX_VP, world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.alpha = v.color.a;
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                clip(tex2D(_MainTex, i.uv).a * i.alpha - _Cutoff - 1e-4);
                float hatch = GpHatch(GpScreenPx(i.screenPos));
                return fixed4(_Ratio.rgb * 0.5 * (1.0 - hatch * 0.07), 1.0);
            }
            ENDCG
        }
    }
}
