using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 들어오기 · 나가기 닦기 — 비스듬한 색 띠(장면 accent, 먹 테)가 왼쪽에서 오른쪽으로 지나간다 (시안 wipe(k) 두 번: 처음 0.2 초에 걷히고, 끝 0.2 초에 덮는다).
    /// 시안은 먹(검정) 띠였지만 밝은 게임 화면 사이에서 검게 번쩍이므로 장면 색으로 칠한다.
    /// 이 오브젝트는 설계 원점(왼쪽 위)에 두고, 확대 · 흔들림이 없는 설계 판(시안 setTransform(s, …)) 안에 둔다. 컷인이 매 프레임 <see cref="Set"/> 을 부른다.
    /// </summary>
    public class ComicWipe : ComicGraphic
    {
        // 시안 wipe: 띠 왼쪽 끝이 k 0 → 1 동안 −2200 → 1500, 띠 너비 1950, 위아래로 200 더, 기울기 −0.42
        private const float FromX = -2200f;
        private const float ToX = 1500f;
        private const float BandWidth = 1950f;
        private const float BandMargin = 200f;
        private const float Slant = -0.42f;

        [Tooltip("닦는 데 걸리는 시간 (초, 시안 0.2).")]
        [SerializeField] private float duration = 0.2f;
        [Tooltip("띠 색 — 컷인이 장면 accent 로 넣는다.")]
        [SerializeField] private Color tint = ComicKit.Ink;
        [Tooltip("띠 가장자리 먹 테 두께 (설계 px).")]
        [SerializeField] private float edgeWidth = 8f;

        private float _opening;
        private float _closing;

        /// <summary>띠 색 (장면 accent).</summary>
        public Color Tint
        {
            get => tint;
            set
            {
                tint = value;
                Redraw();
            }
        }

        /// <summary>장면 시각 t (초) · 장면 길이 (초) — 시작 닦기 k = 0.5 + t/0.2·0.5, 끝 닦기 k = (t − (len − 0.2))/0.2·0.5.</summary>
        public void Set(float t, float length)
        {
            float opening = 0.5f + t / duration * 0.5f;
            float closing = (t - (length - duration)) / duration * 0.5f;
            if (Mathf.Approximately(opening, _opening) && Mathf.Approximately(closing, _closing))
                return;
            _opening = opening;
            _closing = closing;
            Redraw();
        }

        protected override void Paint(ComicPen pen)
        {
            Band(pen, _opening);
            Band(pen, _closing);
        }

        private void Band(ComicPen pen, float k)
        {
            if (k <= 0f || k >= 1f)
                return;
            float x = ComicKit.Lerp(FromX, ToX, k);
            pen.Save();
            pen.Transform(1f, 0f, Slant, 1f, 0f, 0f);
            pen.BeginPath();
            pen.Rect(x, -BandMargin, BandWidth, ComicKit.Height + BandMargin * 2f);
            pen.Fill(tint);
            pen.Stroke(ComicKit.Ink, edgeWidth);
            pen.Restore();
        }
    }
}
