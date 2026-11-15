using System;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 장면이 직접 그리는 자유 그림 — 시안 장면의 draw 안에 있는 c.arc · c.bezierCurveTo · c.fill 같은 그 장면만의 모양.
    /// 장면이 <see cref="Painter"/> 를 한 번 정해 두고(Awake), 값이 바뀌는 프레임마다 <see cref="ComicGraphic.Redraw"/> 를 부른다.
    /// 여러 장면에서 되풀이되는 모양이면 이것 대신 도구 컴포넌트를 하나 만든다.
    /// </summary>
    public class ComicSketch : ComicGraphic
    {
        /// <summary>그리는 함수. 비어 있으면 아무것도 그리지 않는다.</summary>
        public Action<ComicPen> Painter { get; set; }

        protected override void Paint(ComicPen pen)
        {
            Painter?.Invoke(pen);
        }
    }
}
