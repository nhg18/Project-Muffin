// 「GOAT」 컷인의 빛 · 연기 조각 — 시안 goat_cutin.js 의 스프라이트 셰이더(PS).
// uv.z = 모양: 0 둥근 빛(가운데에서 바깥으로 제곱으로 흐려짐) · 1 빛줄기(폭 가운데가 밝고 멀어질수록 흐려짐). uv.xy = −1 ~ 1 (빛줄기의 y 는 0 ~ 1).
// 깊이는 읽기만 한다 (돌 뒤에 가려진다). 섞는 방식은 머티리얼에서: 연기 = 보통, 빛 = 더하기.
Shader "Chapchu/CardFx/Goat Sprite"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (10 보통 · 1 더하기)", Float) = 10
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha [_DstBlend]
        // 렌더 텍스처의 알파는 건드리지 않는다 — 화면에 올릴 때 비치지 않게
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float f = 1.0;
                if (i.uv.z < 0.5)
                {
                    f = saturate(1.0 - length(i.uv.xy));
                    f *= f;
                }
                else if (i.uv.z < 1.5)
                {
                    f = pow(saturate(1.0 - abs(i.uv.x)), 1.5) * (1.0 - i.uv.y);
                }
                return fixed4(i.color.rgb, i.color.a * f);
            }
            ENDCG
        }
    }
}
