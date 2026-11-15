using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 집중선 — 이 오브젝트 자리를 향해 바깥에서 들어오는 가는 쐐기들 (시안 focus(cx, cy, r0, seed, t, {a, n, w, jit, col})).
    /// 1/12 초마다 모양이 살짝 바뀐다 (만화 선의 떨림) — 장면이 매 프레임 <see cref="SetTime"/> 을 부른다.
    /// 색 · 진하기(a) = Graphic.color. 가운데 빈 원 반지름은 <see cref="Radius"/>.
    /// </summary>
    public class ComicFocusLines : ComicGraphic
    {
        // 시안: 1/12 초 칸마다 씨앗에 더하는 수
        private const int SeedStride = 7919;

        [Tooltip("선이 시작하는 반지름 — 가운데 빈 원 (설계 px, 시안 r0).")]
        [SerializeField] private float radius = 260f;
        [Tooltip("씨앗 (시안 seed).")]
        [SerializeField] private int seed = 11;
        [Tooltip("선 수 (시안 n).")]
        [SerializeField] private int count = 120;
        [Tooltip("선 굵기 배율 (시안 w).")]
        [SerializeField] private float widthScale = 1f;
        [Tooltip("시작 반지름이 들쭉날쭉한 정도 (시안 jit).")]
        [SerializeField] private float jitter = 0.5f;
        [Tooltip("1초에 모양이 바뀌는 횟수 (시안 12).")]
        [SerializeField] private float changesPerSecond = 12f;
        [Tooltip("선 끝까지 거리 (설계 px, 시안 2600).")]
        [SerializeField] private float reach = 2600f;

        private int _frame;

        /// <summary>가운데 빈 원 반지름 (설계 px). 바뀌면 다시 그린다.</summary>
        public float Radius
        {
            get => radius;
            set
            {
                if (Mathf.Approximately(radius, value))
                    return;
                radius = value;
                Redraw();
            }
        }

        /// <summary>장면 시각 (초). 모양이 바뀌는 칸을 넘으면 다시 그린다.</summary>
        public void SetTime(float t)
        {
            int frame = Mathf.FloorToInt(t * changesPerSecond);
            if (frame == _frame)
                return;
            _frame = frame;
            Redraw();
        }

        protected override void Paint(ComicPen pen)
        {
            var random = new ComicRandom(seed + _frame * SeedStride);
            pen.BeginPath();
            for (int i = 0; i < count; i++)
            {
                float angle = (i + random.Next()) / count * ComicKit.Tau;
                float width = widthScale * (0.0035f + random.Next() * random.Next() * 0.016f);
                float start = radius * (1f + random.Next() * jitter);
                pen.MoveTo(Mathf.Cos(angle) * start, Mathf.Sin(angle) * start);
                pen.LineTo(Mathf.Cos(angle - width) * reach, Mathf.Sin(angle - width) * reach);
                pen.LineTo(Mathf.Cos(angle + width) * reach, Mathf.Sin(angle + width) * reach);
                pen.ClosePath();
            }
            pen.Fill(Color.white);
        }
    }
}
