using Chapchu.UI.Fx.Comic;
using UnityEngine;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면 배경의 노란 빛 (시안 .glow — radial-gradient(ellipse 520px 420px, rgba(255,238,170,.65), 투명 70%)). 승자 칸 뒤에 놓는다.
    /// 가운데 색에서 바깥으로 옅어지는 타원 하나 — 피벗이 가운데다.
    /// </summary>
    public class ResultGlowView : ComicGraphic
    {
        [Tooltip("타원의 반지름 — 가로 · 세로 (시안 520 · 420).")]
        [SerializeField] private Vector2 radii = new Vector2(520f, 420f);
        [Tooltip("반지름의 이 비율에서 완전히 투명해진다 (시안 70%).")]
        [SerializeField] private float fadeAt = 0.7f;
        [Tooltip("가운데 색 (시안 rgba(255,238,170,.65)).")]
        [SerializeField] private Color inner = new Color(1f, 238f / 255f, 170f / 255f, 0.65f);

        protected override void Paint(ComicPen pen)
        {
            pen.Scale(1f, radii.y / radii.x);
            pen.FillRadial(0f, 0f, radii.x * fadeAt, inner, new Color(inner.r, inner.g, inner.b, 0f));
        }
    }
}
