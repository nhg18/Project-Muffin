using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 같은 오브젝트의 그림(<see cref="UIShape"/> · Image)을 띠 기울기로 기울인다 — CSS skewY 처럼 세로 변은 세로 그대로, 가로 변만 가운데를 기준으로 기운다.
    /// 기울기는 부모 <see cref="CutInBand"/> 의 값 하나를 같이 쓴다.
    /// 띠와 가로 가운데가 같은 조각에만 붙인다. 180° 돌린 조각은 그대로 맞지만, 한 축만 뒤집으면(scale −1) 반대로 기운다.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class BandSkew : BaseMeshEffect
    {
        private CutInBand _band;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive())
                return;
            if (_band == null)
                _band = GetComponentInParent<CutInBand>();
            if (_band == null)
                return;

            float slope = _band.Slope;
            float center = graphic.rectTransform.rect.center.x;
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.position.y += slope * (vertex.position.x - center);
                vh.SetUIVertex(vertex, i);
            }
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _band = null;
        }
    }
}
