using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>CSS cubic-bezier(x1, y1, x2, y2) 이징 — 시간 비율 x(0~1) 의 진행 비율. 시안의 CSS 곡선을 연출 · 시험장이 같이 쓴다.</summary>
        public static float CssEase(float x1, float y1, float x2, float y2, float x)
        {
            x = Mathf.Clamp01(x);
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float error = BezierAt(x1, x2, t) - x;
                if (Mathf.Abs(error) < 1e-5f)
                    break;
                float slope = BezierSlope(x1, x2, t);
                if (Mathf.Abs(slope) < 1e-6f)
                    break;
                t = Mathf.Clamp01(t - error / slope);
            }

            return BezierAt(y1, y2, t);
        }

        // 0 · p1 · p2 · 1 을 잇는 3차 베지어의 한 축
        private static float BezierAt(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }

        private static float BezierSlope(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * p1 + 6f * u * t * (p2 - p1) + 3f * t * t * (1f - p2);
        }
    }
}
