using System.Collections;
using UnityEngine;

namespace Chapchu.UI.Fx.Post
{
    /// <summary>
    /// 4 단계 장식이 말풍선 하나뿐인 카드의 연출 (시안 vbubble). 카드마다 클래스를 만들지 않고 프리팹마다 값만 바꿔 쓴다 — 핥아주기 「할짝」 · 아 맞다! 「아 맞다!」.
    /// 3 단계는 없다. 말풍선은 연출 레이어(<see cref="FxLayer.Bubble(RectTransform, string, float)"/>)에 그려서, 장식이 끝나 이 오브젝트가 지워져도 혼자 사라진다.
    /// 시안: 아티팩트 「찹츄 카드 이펙트 v2」 FXA 의 s4.
    /// </summary>
    public class PostBubbleFx : CardFx
    {
        /// <summary>말풍선이 뜨는 자리.</summary>
        public enum Who
        {
            /// <summary>카드를 낸 사람.</summary>
            Caster,
            /// <summary>대상 (대상이 없으면 낸 사람이 아닌 첫 사람 — <see cref="CardFxContext.MainTarget"/>).</summary>
            Target
        }

        [Tooltip("말풍선이 뜨는 자리 — 낸 사람 또는 대상.")]
        [SerializeField] private Who who = Who.Target;
        [Tooltip("말풍선 글자.")]
        [SerializeField] private string text = "";
        [Tooltip("자리 가운데에서 말풍선까지 높이 (시안 px, 위로 + — 시안 vbubble 의 dy −70 이 70).")]
        [SerializeField] private float rise = 70f;
        [Tooltip("4 단계 시작부터 말풍선이 뜰 때까지 (ms) — 시안에서 공통 결과(카드 이동 등)가 끝난 뒤에 뜨는 카드.")]
        [SerializeField] private float delayMs;
        [Tooltip("말풍선이 뜬 뒤 장식이 끝날 때까지 (ms, 시안 s4 의 wait). 말풍선은 이 뒤에도 레이어에 남아 혼자 사라진다.")]
        [SerializeField] private float holdMs;

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            if (delayMs > 0f)
                yield return new WaitForSeconds(delayMs / 1000f);

            CardFxSeat seat = who == Who.Caster ? context.CasterSeat : context.MainTarget;
            context.Layer.Bubble(seat.Anchor, text, rise);

            if (holdMs > 0f)
                yield return new WaitForSeconds(holdMs / 1000f);
        }
    }
}
