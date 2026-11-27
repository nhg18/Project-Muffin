using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 망점(스크린톤) — 설계 판 전체에 가장자리로 갈수록 커지는 점 무늬 (시안 halftone(col, a, inner, step) 을 drawImage 한 것, 장면 묶음의 tone()).
    /// 줄마다 반 칸씩 엇갈린 격자, 점 반지름 = step/2 · √k (k = 가운데에서 떨어진 정도). 가운데 inner 안쪽은 비어 있다.
    /// 점 하나 = 동그라미 그림 한 장 (그림은 fx_dot). 색 · 진하기(a) = Graphic.color. 이 오브젝트는 설계 원점(왼쪽 위)에 둔다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class ComicHalftone : MaskableGraphic
    {
        // 시안: 가운데에서 떨어진 정도를 이 값으로 나눈다 (판 모서리 = 약 1.13)
        private const float DistanceScale = 1.25f;
        // fx_dot 그림에서 동그라미가 차지하는 비율 (반지름 61 / 64) — 점 크기를 시안과 맞춘다
        private const float DotCoverage = 0.96f;

        [Tooltip("점 그림 — 꽉 찬 흰 동그라미.")]
        [SerializeField] private Sprite dot;
        [Tooltip("점 사이 간격 (설계 px, 시안 step).")]
        [SerializeField] private float step = 13f;
        [Tooltip("점이 없는 가운데 범위 (0 ~ 1, 시안 inner).")]
        [SerializeField] private float inner = 0.35f;

        public override Texture mainTexture => dot != null ? dot.texture : s_WhiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (step <= 0f)
                return;
            Vector4 uv = dot != null ? DataUtility.GetOuterUV(dot) : new Vector4(0f, 0f, 1f, 1f);
            Color32 c = color;
            float w = ComicKit.Width, h = ComicKit.Height;
            int row = 0;
            for (float y = 0f; y < h + step; y += step, row++)
            {
                for (float x = row % 2 * step / 2f; x < w + step; x += step)
                {
                    float d = Mathf.Sqrt(Sq((x - w / 2f) / (w / 2f)) + Sq((y - h / 2f) / (h / 2f))) / DistanceScale;
                    float k = ComicKit.Cl((d - inner) / (1f - inner));
                    if (k <= 0f)
                        continue;
                    float r = step * 0.5f * Mathf.Sqrt(k) / DotCoverage;
                    int start = vh.currentVertCount;
                    vh.AddVert(new Vector3(x - r, -(y + r)), c, new Vector4(uv.x, uv.y, 0f, 0f));
                    vh.AddVert(new Vector3(x - r, -(y - r)), c, new Vector4(uv.x, uv.w, 0f, 0f));
                    vh.AddVert(new Vector3(x + r, -(y - r)), c, new Vector4(uv.z, uv.w, 0f, 0f));
                    vh.AddVert(new Vector3(x + r, -(y + r)), c, new Vector4(uv.z, uv.y, 0f, 0f));
                    vh.AddTriangle(start, start + 1, start + 2);
                    vh.AddTriangle(start, start + 2, start + 3);
                }
            }
        }

        private static float Sq(float v) => v * v;

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif
    }
}
