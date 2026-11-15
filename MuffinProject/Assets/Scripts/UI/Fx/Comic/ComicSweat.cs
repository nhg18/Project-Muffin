using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 땀방울 — 하늘색 물방울 + 먹 테 + 흰 반짝 (시안 sweat(x, y, s, a)). 기준점 = 이 RectTransform 의 피벗 (물방울 가운데 아래쪽).
    /// 크기(시안 s) = 이 RectTransform 의 localScale, 투명도(시안 a) = Graphic.color.a.
    /// </summary>
    public class ComicSweat : ComicGraphic
    {
        // 시안 sweat 의 물방울 모양 (설계 px, s = 1)
        private const float Top = -26f;
        private const float Bottom = 14f;
        private const float BulgeX = 16f;
        private const float ShoulderX = 14f;
        private const float ShoulderY = -6f;
        private const float BellyY = 8f;
        private static readonly Vector2 ShineCenter = new Vector2(-4f, 2f);
        private static readonly Vector2 ShineRadius = new Vector2(3f, 6f);
        private const float ShineAngle = 0.3f;

        [Tooltip("물방울 색 (시안 #bfe9ff).")]
        [SerializeField] private Color water = new Color32(0xbf, 0xe9, 0xff, 0xff);
        [Tooltip("테 두께 (설계 px, s = 1 기준, 시안 4).")]
        [SerializeField] private float lineWidth = 4f;

        protected override void Paint(ComicPen pen)
        {
            pen.BeginPath();
            pen.MoveTo(0f, Top);
            pen.BezierCurveTo(ShoulderX, ShoulderY, BulgeX, BellyY, 0f, Bottom);
            pen.BezierCurveTo(-BulgeX, BellyY, -ShoulderX, ShoulderY, 0f, Top);
            pen.Fill(water);
            pen.Stroke(ComicKit.Ink, lineWidth);
            pen.BeginPath();
            pen.Ellipse(ShineCenter.x, ShineCenter.y, ShineRadius.x, ShineRadius.y, ShineAngle, 0f, ComicKit.Tau);
            pen.Fill(Color.white);
        }
    }
}
