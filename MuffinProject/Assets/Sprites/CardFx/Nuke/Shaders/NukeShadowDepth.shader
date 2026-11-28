// 대폭발 3D 장면의 그림자 맵 — 해 쪽에서 본 깊이(0~1, 시안 LVP 의 z)를 RFloat 에 쓴다. NukeRender 가 명령 버퍼로만 부른다 (물체에 붙이지 않는다).
// 패스 0 = 물체, 패스 1 = 빛을 보는 판으로 세운 강아지 (그림이 옅은 곳은 버린다).
Shader "Hidden/Chapchu/Nuke/ShadowDepth"
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
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "NukeCommon.cginc"
    #include "NukeDogTex.cginc"

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
        float d : TEXCOORD2;
    };

    v2f vert(appdata v)
    {
        v2f o;
        float4 wu = mul(unity_ObjectToWorld, v.vertex);
        o.pos = mul(UNITY_MATRIX_VP, wu);
        o.uv = v.uv;
        o.info = v.info;
        o.d = mul(_NkLVP, float4(wu.xyz - _NkOrigin, 1.0)).z * 0.5 + 0.5;
        return o;
    }
    ENDCG
    SubShader
    {
        Cull Off
        ZWrite On
        ZTest LEqual
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 frag(v2f i) : SV_Target
            {
                return float4(i.d, 0, 0, 1);
            }
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            float4 frag(v2f i) : SV_Target
            {
                clip(dogTex(i.info.x, i.uv).a * i.info.y - 0.45);
                return float4(i.d, 0, 0, 1);
            }
            ENDCG
        }
    }
}
