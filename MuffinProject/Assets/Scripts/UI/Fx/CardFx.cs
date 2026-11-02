using System.Collections;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 카드 한 장의 연출 (컷인 · 마무리). 카드마다 프리팹 하나 + 이 클래스를 상속한 컴포넌트 하나.
    /// 규칙은 모른다 — 결과(피해 숫자 · 체력)를 보여 줄 순간에 <see cref="CardFxContext.Impact"/> 만 부른다 (06-health 「HP 변경 애니메이션은 결과 수신 후」).
    /// 시안: 아티팩트 「찹츄 카드 이펙트」.
    /// </summary>
    public abstract class CardFx : MonoBehaviour
    {
        /// <summary>처음부터 끝까지 재생한다. 끝나면 부른 쪽이 이 오브젝트를 지운다.</summary>
        public abstract IEnumerator PlayRoutine(CardFxContext context);
    }
}
