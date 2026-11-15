using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 장면 바탕 — 위에서 아래로 두 색 그라데이션 (시안 장면 묶음의 bgGrad(a, b)). 설계 판 위 끝(y 0) = 위 색, 아래 끝(y 720) = 아래 색.
    /// 흔들리고 확대돼도 빈 곳이 보이지 않게 판 밖으로 여유만큼 더 칠한다. 이 오브젝트는 설계 원점(왼쪽 위)에 둔다.
    /// </summary>
    public class ComicGradient : ComicGraphic
    {
        [Tooltip("위 색 (설계 판 위 끝).")]
        [SerializeField] private Color top = new Color32(0xff, 0xd0, 0x8a, 0xff);
        [Tooltip("아래 색 (설계 판 아래 끝).")]
        [SerializeField] private Color bottom = new Color32(0xff, 0x7a, 0x1f, 0xff);
        [Tooltip("판 밖으로 더 칠하는 여유 (설계 px, 시안 500).")]
        [SerializeField] private float margin = 500f;

        protected override void Paint(ComicPen pen)
        {
            pen.Rect(-margin, -margin, ComicKit.Width + margin * 2f, ComicKit.Height + margin * 2f);
            pen.FillLinear(0f, 0f, 0f, ComicKit.Height, top, bottom);
        }
    }
}
