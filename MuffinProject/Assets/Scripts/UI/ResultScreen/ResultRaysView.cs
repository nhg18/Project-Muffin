using Chapchu.UI.Fx.Comic;
using UnityEngine;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면 배경의 도는 빛줄기 (시안 .rays — repeating-conic-gradient 흰 띠 6° / 15° 마다, 120초에 한 바퀴).
    /// 띠는 한 번만 그리고 RectTransform 을 돌린다. 띠의 색 · 투명도는 Graphic.color.
    /// </summary>
    public class ResultRaysView : ComicGraphic
    {
        [Tooltip("띠의 길이 — 가운데에서 끝까지 (시안은 3000 정사각형의 가운데에서).")]
        [SerializeField] private float radius = 1500f;
        [Tooltip("띠 하나의 각 (도, 시안 6).")]
        [SerializeField] private float bandDegrees = 6f;
        [Tooltip("띠가 되풀이되는 각 (도, 시안 15).")]
        [SerializeField] private float stepDegrees = 15f;
        [Tooltip("한 바퀴 도는 시간 (초, 시안 120).")]
        [SerializeField] private float turnSeconds = 120f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -360f / turnSeconds * Time.deltaTime); // 시계 방향
        }

        protected override void Paint(ComicPen pen)
        {
            for (float degrees = 0f; degrees < 360f; degrees += stepDegrees)
            {
                float from = degrees * Mathf.Deg2Rad;
                float to = (degrees + bandDegrees) * Mathf.Deg2Rad;
                pen.BeginPath();
                pen.MoveTo(0f, 0f);
                pen.LineTo(Mathf.Cos(from) * radius, Mathf.Sin(from) * radius);
                pen.LineTo(Mathf.Cos(to) * radius, Mathf.Sin(to) * radius);
                pen.ClosePath();
                pen.Fill(Color.white);
            }
        }
    }
}
