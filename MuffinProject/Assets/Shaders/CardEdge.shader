// 3D 카드의 옆면 (두께) — 잉크 한 색 (10-ui.md 2-1 「손패 · 카드 (3D 층)」, 프로토타입 v2 C3D edge).
// 옆면은 카드가 카메라를 볼 때 1 px 보다 얇게 비스듬히 보여 MSAA 없이는 점선처럼 끊긴다 — 옆면이 카메라 쪽으로 돌아설수록(뒤집히는 중 · 크게 기울 때) 나타나게 한다.
Shader "Chapchu/Card Edge"
{
    Properties
    {
        _Color ("Color", Color) = (0.169, 0.157, 0.337, 1)
        _FadeStart ("Fade Start (옆면이 이만큼 카메라를 보면 나타나기 시작)", Range(0, 1)) = 0.3
        _FadeEnd ("Fade End (다 보임)", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        Lighting Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _FadeStart, _FadeEnd;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float facing : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 toCam = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);
                o.facing = dot(normalize(UnityObjectToWorldNormal(v.normal)), toCam);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float a = _Color.a * smoothstep(_FadeStart, _FadeEnd, i.facing);
                clip(a - 0.004);
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
