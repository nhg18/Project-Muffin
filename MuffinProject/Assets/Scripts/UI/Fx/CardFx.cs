using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 카드 한 장의 연출. 카드마다 프리팹 하나(Resources/CardFx/Card_{카드 ID}) + 이 클래스를 상속한 컴포넌트 하나 (10-ui 5-1).
    /// 3 단계(능력 발동 — 컷인 · 전용 장면)는 <see cref="PlayRoutine"/>, 4 단계(후처리)의 카드만의 장식(말풍선 · 얼음 · 효과음 글자)은 <see cref="PlayPostRoutine"/>.
    /// 4 단계의 공통 결과(피해 숫자 · 회복 · 손패 이동)는 게임 화면이 따로 그린다 — 장식이 결과 값을 알아야 하면 <see cref="CardFxContext"/> 의 알림을 받는다.
    /// 연출은 조각(<see cref="CardFxStep"/>)으로 나눌 수 있고, 조각 하나만 따로 재생할 수도 있다 (이펙트 시험장).
    /// 규칙은 모른다 — 결과를 보여 줄 순간에 <see cref="CardFxContext.Impact"/> 만 부른다 (06-health 「HP 변경 애니메이션은 결과 수신 후」).
    /// 시안: 아티팩트 「찹츄 카드 이펙트 v2」.
    /// </summary>
    public abstract class CardFx : MonoBehaviour
    {
        /// <summary>이 연출의 조각들, 재생 순서대로. 조각으로 나누지 않은 연출은 비어 있다.</summary>
        public virtual IReadOnlyList<CardFxStep> Steps => Array.Empty<CardFxStep>();

        /// <summary>조각의 묶음 이름 (예: 장면 · 이펙트 · 마무리). 시험장 목록에 쓴다.</summary>
        public virtual string GroupOf(CardFxStep step) => string.Empty;

        /// <summary>3 단계(능력 발동)를 처음부터 끝까지 재생한다. 3 단계가 없는 카드는 바로 끝난다. 끝나도 지우지 않는다 — 4 단계 장식이 이어진다.</summary>
        public virtual IEnumerator PlayRoutine(CardFxContext context)
        {
            yield break;
        }

        /// <summary>
        /// 4 단계(후처리)의 카드만의 장식. 3 단계 다음에, 공통 결과 연출(피해 숫자 · 손패 이동)과 같이 돈다 — 부르는 쪽은 기다리지 않고, 끝나면 이 오브젝트를 지운다.
        /// 결과 값이 필요하면 <see cref="CardFxContext.OnHpChanged"/> 등을 받는다 (값은 장식이 시작한 뒤에 온다).
        /// </summary>
        public virtual IEnumerator PlayPostRoutine(CardFxContext context)
        {
            yield break;
        }

        /// <summary>조각 하나만 재생한다. showScenery 면 이펙트 뒤에 그 시각의 장면을 깐다. 조각이 없는 연출은 전체를 재생한다. 끝나면 부른 쪽이 지운다.</summary>
        public virtual IEnumerator PlayStepRoutine(CardFxStep step, CardFxContext context, bool showScenery) => PlayRoutine(context);

        /// <summary>켜진 조각을 시작 시각 순으로 (꺼진 오브젝트 · 컴포넌트는 빠진다. 같으면 계층 위부터 — OrderBy 는 순서를 지킨다).</summary>
        protected static List<CardFxStep> Collect(Transform root)
        {
            return root.GetComponentsInChildren<CardFxStep>().Where(step => step.enabled).OrderBy(step => step.StartMs).ToList();
        }

        /// <summary>시작부터 조각마다 제 시각에 재생하고, 가장 늦게 끝나는 조각까지 기다린다.</summary>
        protected static IEnumerator TimelineRoutine(List<CardFxStep> steps, CardFxContext context, RectTransform target)
        {
            float start = Time.time;
            float end = 0f;
            foreach (CardFxStep step in steps)
            {
                while ((Time.time - start) * 1000f < step.StartMs)
                    yield return null;
                step.Play(context, target);
                end = Mathf.Max(end, step.StartMs + step.DurationMs);
            }

            while ((Time.time - start) * 1000f < end)
                yield return null;
        }
    }
}
