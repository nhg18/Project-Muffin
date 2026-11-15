using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인 그리기 도구의 부모 — <see cref="Paint"/> 에서 <see cref="ComicPen"/> 으로 경로를 그리면 그대로 이 Graphic 의 메시가 된다.
    /// 원점은 이 RectTransform 의 피벗, 좌표 · 각도는 시안 그대로 (y 아래로 +, 라디안 시계 방향 +).
    /// Graphic.color 는 모든 색에 곱한다 — 도구 전체를 흐리게 할 때 color.a 를 바꾼다.
    /// 값을 바꾼 뒤에는 <see cref="Redraw"/> (매 프레임 바뀌는 값만 매 프레임 다시 그린다).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))] // Graphic 은 CanvasRenderer 를 스스로 붙이지 않는다
    public abstract class ComicGraphic : MaskableGraphic
    {
        private ComicPen _pen;

        /// <summary>값이 바뀌었으니 다음 그리기 때 메시를 다시 만든다.</summary>
        public void Redraw()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_pen == null)
                _pen = new ComicPen();
            _pen.Begin(vh, color);
            Paint(_pen);
            _pen.End();
        }

        /// <summary>그린다. 펜은 비어 있고 변환은 처음 상태다.</summary>
        protected abstract void Paint(ComicPen pen);

#if UNITY_EDITOR
        protected override void Reset()
        {
            base.Reset();
            raycastTarget = false;
        }
#endif
    }
}
