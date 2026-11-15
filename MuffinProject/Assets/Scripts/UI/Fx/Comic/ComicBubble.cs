using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 둥근 말풍선 + 꼬리 (시안 bubble(x, y, w, h, tx, ty, {fill, lw})). 가운데 = 이 RectTransform 의 피벗.
    /// 꼬리 끝은 <see cref="Tail"/> — 이 오브젝트 기준 설계 좌표 (아래로 +). 말풍선 속 글자는 자식 TMP 로 둔다.
    /// 투명도 = Graphic.color.a.
    /// </summary>
    public class ComicBubble : ComicGraphic
    {
        // 시안: 꼬리 뿌리 = 가운데에서 반지름의 0.38 배, 뿌리 폭 = 반지름의 0.08 배 (양쪽)
        private const float TailRoot = 0.38f;
        private const float TailWidth = 0.08f;
        // 시안: 안쪽을 한 번 더 칠할 때 테 안쪽으로 더 들이는 값 (설계 px)
        private const float InnerInset = 0.5f;

        [Tooltip("말풍선 너비 · 높이 (설계 px, 시안 w · h).")]
        [SerializeField] private Vector2 size = new Vector2(196f, 88f);
        [Tooltip("꼬리 끝 — 이 오브젝트 기준 (설계 px, 아래로 +).")]
        [SerializeField] private Vector2 tail = new Vector2(-48f, 60f);
        [Tooltip("바탕 색 (시안 fill).")]
        [SerializeField] private Color fill = Color.white;
        [Tooltip("테 두께 (설계 px, 시안 lw).")]
        [SerializeField] private float lineWidth = 6f;

        /// <summary>말풍선 너비 · 높이 (설계 px). 바뀌면 다시 그린다.</summary>
        public Vector2 Size
        {
            get => size;
            set
            {
                if (size == value)
                    return;
                size = value;
                Redraw();
            }
        }

        /// <summary>꼬리 끝 — 이 오브젝트 기준 설계 좌표 (아래로 +). 바뀌면 다시 그린다.</summary>
        public Vector2 Tail
        {
            get => tail;
            set
            {
                if (tail == value)
                    return;
                tail = value;
                Redraw();
            }
        }

        /// <summary>바탕 색. 바뀌면 다시 그린다.</summary>
        public Color Fill
        {
            get => fill;
            set
            {
                if (fill == value)
                    return;
                fill = value;
                Redraw();
            }
        }

        protected override void Paint(ComicPen pen)
        {
            float w = size.x, h = size.y;
            pen.BeginPath();
            pen.Ellipse(0f, 0f, w / 2f, h / 2f, 0f, 0f, ComicKit.Tau);
            pen.Fill(fill);
            pen.Stroke(ComicKit.Ink, lineWidth);

            float a = Mathf.Atan2(tail.y, tail.x);
            float bx = Mathf.Cos(a) * w * TailRoot, by = Mathf.Sin(a) * h * TailRoot;
            float nx = -Mathf.Sin(a) * w * TailWidth, ny = Mathf.Cos(a) * h * TailWidth;
            pen.BeginPath();
            pen.MoveTo(bx + nx, by + ny);
            pen.LineTo(tail.x, tail.y);
            pen.LineTo(bx - nx, by - ny);
            pen.ClosePath();
            pen.Fill(fill);
            pen.Stroke(ComicKit.Ink, lineWidth);

            // 꼬리 뿌리의 테를 덮는다
            float inset = lineWidth / 2f + InnerInset;
            pen.BeginPath();
            pen.Ellipse(0f, 0f, w / 2f - inset, h / 2f - inset, 0f, 0f, ComicKit.Tau);
            pen.Fill(fill);
        }
    }
}
