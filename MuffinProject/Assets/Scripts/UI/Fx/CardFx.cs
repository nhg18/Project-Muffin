using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 카드 한 장의 연출. 카드마다 프리팹 하나(Resources/CardFx/Card_{카드 ID}) + 이 클래스를 상속한 컴포넌트 하나 (10-ui 5-1).
    /// 3 단계(능력 발동 — 컷인 · 전용 장면)는 <see cref="PlayRoutine"/>, 4 단계(후처리)에 카드만의 연출을 더하는 것은 개판 등급 카드뿐이고 <see cref="PlayPostRoutine"/> (10-ui 5-1, 2026-11-17).
    /// 4 단계의 공통 결과(피해 숫자 · 회복 · 손패 이동)는 게임 화면이 따로 그린다.
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

        /// <summary>3 단계(능력 발동)를 처음부터 끝까지 재생한다. 3 단계가 없는 카드는 바로 끝난다. 끝나도 지우지 않는다 — 4 단계 연출이 이어질 수 있다.</summary>
        public virtual IEnumerator PlayRoutine(CardFxContext context)
        {
            yield break;
        }

        /// <summary>
        /// 4 단계(후처리)의 카드만의 연출 — <b>개판 등급 카드만</b> 부른다 (10-ui 5-1, 예: 지옥참마도의 대상 자리 칼질). 3 단계 다음에 부르고,
        /// 부르는 쪽은 이 루틴이 끝날 때까지 기다린 뒤 공통 결과(피해 숫자 등)를 보인다 — 루틴은 결과를 보일 순간에 끝나고(<see cref="CardFxContext.Impact"/>), 남은 꼬리는 부르는 쪽이 이 오브젝트를 지울 때까지 이어진다.
        /// 개판 밖 카드에 예전에 만든 장식(말풍선 · 얼음 · 효과음 글자 등)은 코드에 남아 있지만 부르지 않는다 — 지울지 공통 연출로 옮길지는 10-ui 11절 #23 ⑤ (PM 미정).
        /// </summary>
        public virtual IEnumerator PlayPostRoutine(CardFxContext context)
        {
            yield break;
        }

        /// <summary>
        /// 기획에서 정한 카드만의 4 단계가 있다 — 부르는 쪽은 개판 등급이고 이것이 true 일 때만 <see cref="PlayPostRoutine"/> 을 부른다.
        /// 지금은 지옥참마도만 (10-ui 5-1 「개판 등급의 카드별 4 단계」 — 개판 오분전 · 대소동은 미정).
        /// </summary>
        public virtual bool HasPostStage => false;

        /// <summary>조각 하나만 재생한다. showScenery 면 이펙트 뒤에 그 시각의 장면을 깐다. 조각이 없는 연출은 전체를 재생한다. 끝나면 부른 쪽이 지운다.</summary>
        public virtual IEnumerator PlayStepRoutine(CardFxStep step, CardFxContext context, bool showScenery) => PlayRoutine(context);

        private ResourceRequest _soundLoad;

        /// <summary>
        /// 이 카드의 연출 소리(Resources/CardFxSound/Card_{카드 ID}, 4 단계는 _Post)를 불러오기 시작한다 — 장면을 준비하는 동안 같이 읽게 연출 맨 앞에서 부른다.
        /// 소리는 「카드 이펙트 v2」 시안의 소리를 장면 시작부터 그대로 녹음한 한 파일이라, 장면 시계가 0 이 되는 순간에 <see cref="StartSound"/> 로 틀면 시안과 같은 시각에 울린다. 파일이 없는 카드는 소리 없이 넘어간다.
        /// </summary>
        protected void LoadSound(CardFxContext context, bool post = false)
        {
            string path = ResourcePaths.CardFxSound + CardFxLoader.NameOf(context.CardId) + (post ? "_Post" : "");
            _soundLoad = Resources.LoadAsync<AudioClip>(path);
        }

        /// <summary>장면 시계가 시작하는 순간에 부른다. 아직 덜 읽었으면 다 읽은 뒤 그동안 흐른 만큼 건너뛰고 튼다 — 소리가 장면보다 늦게 밀리지 않게.</summary>
        protected void StartSound()
        {
            if (_soundLoad == null) return;

            StartCoroutine(SoundRoutine(_soundLoad, Time.time));
            _soundLoad = null;
        }

        private static IEnumerator SoundRoutine(ResourceRequest load, float startTime)
        {
            if (!load.isDone)
                yield return load;
            if (load.asset is AudioClip clip)
                SoundManager.PlayCardFx(clip, Time.time - startTime);
        }

        // 켜진 조각을 시작 시각 순으로 (같으면 계층 위부터 — OrderBy 는 순서를 지킨다)
        protected static List<CardFxStep> Collect(Transform root)
        {
            return root.GetComponentsInChildren<CardFxStep>().Where(step => step.enabled).OrderBy(step => step.StartMs).ToList();
        }

        // 시작부터 조각마다 제 시각에 재생하고, 가장 늦게 끝나는 조각까지 기다린다
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
