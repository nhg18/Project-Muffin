using System.Collections;
using UnityEngine;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 4 단계 장식이 대상 자리의 터짐 글자 하나뿐인 카드의 연출 (시안 boomAt, 10-ui 5-1). 카드마다 클래스를 만들지 않고 프리팹마다 값만 바꿔 쓴다 — 물어뜯기 「콱!」.
    /// 3 단계는 없다. 글자는 연출 레이어(<see cref="FxLayer.Boom(Vector2, Color, string)"/>)에 그려서, 장식이 끝나 이 오브젝트가 지워져도 혼자 사라진다.
    /// 같은 순간의 피해 숫자(-n)는 부르는 쪽이 그린다.
    /// 시안: 아티팩트 「찹츄 카드 이펙트 v2」 FXA 의 s4.
    /// </summary>
    public class PostBoomFx : CardFx
    {
        [Tooltip("터짐 글자.")]
        [SerializeField] private string text = "";
        [Tooltip("글자 뒤 별 모양 · 빛의 색.")]
        [SerializeField] private Color color = Color.white;
        [Tooltip("글자가 뜬 뒤 장식이 끝날 때까지 (ms, 시안 s4 의 wait). 글자는 이 뒤에도 레이어에 남아 혼자 사라진다.")]
        [SerializeField] private float holdMs;

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            FxLayer layer = context.Layer;
            // 시안 boomAt(cOf(대상), …) — 대상 가운데에서 조금 떨어진 곳
            layer.Boom(layer.BoomPoint(layer.CenterOf(context.MainTarget.Anchor)), color, text);

            if (holdMs > 0f)
                yield return new WaitForSeconds(holdMs / 1000f);
        }
    }
}
