using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 제목 위 한마디 상자 — 그림자 + 종이 바탕 + 왼쪽 색 줄 + 먹 테 (시안 tagSprite). 가운데 = 이 RectTransform 의 피벗 = 시안 한마디 그림의 가운데.
    /// 상자 너비는 글자 폭 + 64 — <see cref="ComicTitleBlock"/> 가 글자를 바꿀 때 정한다. 글자는 형제 TMP 로 둔다.
    /// </summary>
    public class ComicTagBox : ComicGraphic
    {
        // 시안 tagSprite: 그림 = 상자 + 16, 상자는 (4, 4) 에서 시작, 그림자 (8, 9) 어긋남 rgba(0, 0, 0, .3), 색 줄 폭 14, 테 5
        private const float Margin = 16f;
        private const float Inset = 4f;
        private static readonly Vector2 ShadowOffset = new Vector2(8f, 9f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.3f);
        private const float StripeWidth = 14f;
        private const float LineWidth = 5f;

        [Tooltip("상자 너비 · 높이 (설계 px, 시안 w = 글자 폭 + 64, h = 34·1.9).")]
        [SerializeField] private Vector2 size = new Vector2(400f, 64.6f);
        [Tooltip("왼쪽 색 줄 (장면 accent).")]
        [SerializeField] private Color accent = new Color32(0xff, 0x8a, 0x1f, 0xff);

        /// <summary>상자 너비 · 높이 (설계 px).</summary>
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

        /// <summary>상자 왼쪽 위 — 이 오브젝트(그림 가운데) 기준 (설계 px, 아래로 +).</summary>
        public Vector2 TopLeft => new Vector2(Inset - (size.x + Margin) / 2f, Inset - (size.y + Margin) / 2f);

        /// <summary>왼쪽 색 줄 색.</summary>
        public Color Accent
        {
            get => accent;
            set
            {
                if (accent == value)
                    return;
                accent = value;
                Redraw();
            }
        }

        protected override void Paint(ComicPen pen)
        {
            Vector2 o = TopLeft;
            float w = size.x, h = size.y;
            pen.BeginPath();
            pen.Rect(o.x + ShadowOffset.x, o.y + ShadowOffset.y, w, h);
            pen.Fill(ShadowColor);
            pen.BeginPath();
            pen.Rect(o.x, o.y, w, h);
            pen.Fill(ComicKit.Paper);
            pen.BeginPath();
            pen.Rect(o.x, o.y, StripeWidth, h);
            pen.Fill(accent);
            pen.BeginPath();
            pen.Rect(o.x, o.y, w, h);
            pen.Stroke(ComicKit.Ink, LineWidth);
        }
    }
}
