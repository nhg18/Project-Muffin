// 개판 등급 카드 둘레의 별 파티클 (10-ui.md 2-1 「카드 등급 연출」) — 그림 파일 없이 십자 별 + 가운데 점을 그린다.
// 색 · 투명도는 파티클 정점 색(무지개 · 수명에 따라 나타났다 사라짐). 프로토타입 v2 C3D 의 PT_FS.
Shader "Chapchu/Card Spark"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
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

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

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
                float2 q = i.uv - 0.5;
                float ax = abs(q.x), ay = abs(q.y);
                float cross = max(smoothstep(0.07, 0.0, ax) * smoothstep(0.5, 0.0, ay), smoothstep(0.07, 0.0, ay) * smoothstep(0.5, 0.0, ax));
                float core = smoothstep(0.2, 0.0, length(q));
                float a = max(cross, core) * i.color.a;
                clip(a - 0.01);
                return fixed4(lerp(i.color.rgb, float3(1, 1, 1), core * 0.5), a);
            }
            ENDCG
        }
    }
}
