// 대폭발 3D 장면의 강아지 판 — 시안 D3R 의 SVS · SFS (몸 · 꼬리 · 다리를 카메라 쪽으로 세운 판).
// 그림 12장 (나 · 초코 · 콩이 · 보리 × 몸 · 꼬리 · 다리) 을 한 번에 그리려고 uv1.x 로 고른다 (판 순서 = 먼 것부터, 한 번의 그리기).
// uv1.y = 알파. 그림자는 받지 않는다 (시안 nosh). 카메라 깊이 텍스처에도 들어간다 — 심도가 강아지를 흐리지 않게.
// 장면 알파는 깎는다 — 후처리가 강아지에는 주변광 차폐를 먹이지 않게 (시안 AOF 의 S.a).
Shader "Chapchu/Nuke/Dog"
{
    Properties
    {
        _Tex0 ("나 몸", 2D) = "white" {}
        _Tex1 ("나 꼬리", 2D) = "white" {}
        _Tex2 ("나 다리", 2D) = "white" {}
        _Tex3 ("초코 몸", 2D) = "white" {}
        _Tex4 ("초코 꼬리", 2D) = "white" {}
        _Tex5 ("초코 다리", 2D) = "white" {}
        _Tex6 ("콩이 몸", 2D) = "white" {}
        _Tex7 ("콩이 꼬리", 2D) = "white" {}
        _Tex8 ("콩이 다리", 2D) = "white" {}
        _Tex9 ("보리 몸", 2D) = "white" {}
        _Tex10 ("보리 꼬리", 2D) = "white" {}
        _Tex11 ("보리 다리", 2D) = "white" {}
        _Cut ("이보다 옅으면 버림", Float) = 0.04
        _FogRange ("안개 시작 · 끝 (m)", Vector) = (70, 520, 0, 0)
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "NukeCommon.cginc"
    #include "NukeDogTex.cginc"

    float _Cut;
    float4 _FogRange;
    ENDCG
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Off
            ZWrite On
            Blend One OneMinusSrcAlpha, Zero OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float2 info : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 info : TEXCOORD1;
                float3 w : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float4 wu = mul(unity_ObjectToWorld, v.vertex);
                o.pos = mul(UNITY_MATRIX_VP, wu);
                o.uv = v.uv;
                o.info = v.info;
                o.w = wu.xyz;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 c = dogTex(i.info.x, i.uv) * i.info.y;
                clip(c.a - _Cut);
                float fd = clamp((distance(i.w, _WorldSpaceCameraPos) - _FogRange.x) / (_FogRange.y - _FogRange.x), 0.0, 1.0) * 0.88;
                float3 col = c.rgb * float3(1.0, 0.985, 0.95) * _NkGain;
                return float4(lerp(nkPaper(col, i.pos.xy), _NkFogC * c.a, fd), c.a);
            }
            ENDCG
        }

        Pass
        {
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_shadowcaster

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 info : TEXCOORD1;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                float2 info : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER(o)
                o.uv = v.uv;
                o.info = v.info;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                clip(dogTex(i.info.x, i.uv).a * i.info.y - 0.45);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
}
