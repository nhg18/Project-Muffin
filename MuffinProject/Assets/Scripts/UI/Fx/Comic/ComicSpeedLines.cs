using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 속도선 — 가로로 흘러가는 가는 쐐기들 (시안 speed(t, seed, {n, dir, y0, y1, a, col})).
    /// 이 오브젝트는 설계 원점(왼쪽 위)에 둔다 — 선은 설계 판 전체 폭을 흐른다. 장면이 매 프레임 <see cref="SetTime"/> 을 부른다.
    /// 색 · 진하기(a) = Graphic.color.
    /// </summary>
    public class ComicSpeedLines : ComicGraphic
    {
        // 시안: 선이 화면 밖으로 나갔다 들어오는 여유 (W + 900 주기, 왼쪽으로 450)
        private const float WrapMargin = 900f;
        private const float WrapLead = 450f;
        // 시안: 선마다 시작 위치를 흩는 범위 (설계 px)
        private const float PhaseRange = 3000f;

        [Tooltip("씨앗 (시안 seed).")]
        [SerializeField] private int seed = 1;
        [Tooltip("선 수 (시안 n).")]
        [SerializeField] private int count = 46;
        [Tooltip("흐르는 방향 — 1 이면 오른쪽으로, −1 이면 왼쪽으로 (시안 dir).")]
        [SerializeField] private int direction = 1;
        [Tooltip("선이 놓이는 높이 범위 (설계 px, 위 · 아래 — 시안 y0 · y1).")]
        [SerializeField] private Vector2 band = new Vector2(0f, ComicKit.Height);
        [Tooltip("선 길이 (설계 px, 최소 + 0 ~ 덧붙는 값 — 시안 120 + R·420).")]
        [SerializeField] private Vector2 length = new Vector2(120f, 420f);
        [Tooltip("선 굵기 (설계 px, 최소 + 0 ~ 덧붙는 값 — 시안 1 + R²·7).")]
        [SerializeField] private Vector2 thickness = new Vector2(1f, 7f);
        [Tooltip("흐르는 빠르기 (설계 px/초, 최소 + 0 ~ 덧붙는 값 — 시안 1400 + R·1600).")]
        [SerializeField] private Vector2 speed = new Vector2(1400f, 1600f);

        private float _time;

        /// <summary>장면 시각 (초) — 매번 다시 그린다.</summary>
        public void SetTime(float t)
        {
            _time = t;
            Redraw();
        }

        protected override void Paint(ComicPen pen)
        {
            var random = new ComicRandom(seed);
            float span = ComicKit.Width + WrapMargin;
            int dir = direction < 0 ? -1 : 1;
            pen.BeginPath();
            for (int i = 0; i < count; i++)
            {
                float y = ComicKit.Lerp(band.x, band.y, random.Next());
                float len = length.x + random.Next() * length.y;
                float th = thickness.x + random.Next() * random.Next() * thickness.y;
                float sp = (speed.x + random.Next() * speed.y) * dir;
                float phase = random.Next() * PhaseRange;
                float x = ((phase + _time * sp) % span + span) % span - WrapLead;
                if (dir < 0)
                    x = ComicKit.Width - x;
                pen.MoveTo(x, y - th / 2f);
                pen.LineTo(x - len * dir, y);
                pen.LineTo(x, y + th / 2f);
                pen.ClosePath();
            }
            pen.Fill(Color.white);
        }
    }
}
