using UnityEngine;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 둥근 빛 — 가운데가 진하고 반지름 끝에서 사라진다 (시안 glow(x, y, r, a, col)). 가운데 = 이 RectTransform 의 피벗.
    /// 색 = Graphic.color 의 rgb, 진하기(시안 a) = Graphic.color.a. 시안이 「lighter」 로 그린 빛은 머티리얼을 FxAdditive 로 둔다.
    /// </summary>
    public class ComicGlow : ComicGraphic
    {
        [Tooltip("반지름 (설계 px, 시안 r).")]
        [SerializeField] private float radius = 200f;

        /// <summary>반지름 (설계 px). 바뀌면 다시 그린다.</summary>
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

        protected override void Paint(ComicPen pen)
        {
            if (radius <= 0f)
                return;
            pen.FillRadial(0f, 0f, radius, Color.white, new Color(1f, 1f, 1f, 0f));
        }
    }
}
