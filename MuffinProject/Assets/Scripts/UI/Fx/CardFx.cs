using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 카드 한 장의 연출 (컷인 · 마무리). 카드마다 프리팹 하나 + 이 클래스를 상속한 컴포넌트 하나.
    /// 연출은 조각(<see cref="CardFxStep"/>: 카드별 장면 · 공통 이펙트)들로 나뉘어 있고, 조각 하나만 따로 재생할 수도 있다 (이펙트 시험장).
    /// 규칙은 모른다 — 결과(피해 숫자 · 체력)를 보여 줄 순간에 <see cref="CardFxContext.Impact"/> 만 부른다 (06-health 「HP 변경 애니메이션은 결과 수신 후」).
    /// 시안: 아티팩트 「찹츄 카드 이펙트」.
    /// </summary>
    public abstract class CardFx : MonoBehaviour
    {
        /// <summary>이 연출의 조각들, 재생 순서대로.</summary>
        public abstract IReadOnlyList<CardFxStep> Steps { get; }

        /// <summary>조각의 묶음 이름 (예: 장면 · 이펙트 · 마무리). 시험장 목록에 쓴다.</summary>
        public abstract string GroupOf(CardFxStep step);

        /// <summary>처음부터 끝까지 재생한다. 끝나면 부른 쪽이 이 오브젝트를 지운다.</summary>
        public abstract IEnumerator PlayRoutine(CardFxContext context);

        /// <summary>조각 하나만 재생한다. showScenery 면 이펙트 뒤에 그 시각의 장면을 깐다. 끝나면 부른 쪽이 지운다.</summary>
        public abstract IEnumerator PlayStepRoutine(CardFxStep step, CardFxContext context, bool showScenery);
    }
}
