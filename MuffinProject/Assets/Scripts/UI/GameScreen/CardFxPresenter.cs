using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.UI.Fx;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 연출 3 단계(카드별 능력 발동 연출)와 개판 등급 카드의 4 단계 — 효과가 적용된 카드의 연출 프리팹(Resources/CardFx/Card_{카드 ID})을
    /// 그때 불러와 화면 전체를 덮는 연출 레이어에서 재생하고, 끝나면 메모리에서 내린다 (10-ui.md 5-1). 연출이 없는 카드는 바로 끝난다.
    /// 화면만의 연출이라 판정과 상관없다. 4 단계 공통 연출(피해 숫자 · +n장 등)은 게임 화면이 뒤따르는 결과 알림으로 그린다 — 연출의 결과 요청(Impact)은 쓰지 않는다.
    /// 다음 턴이 연출 중에 시작하지 않게 카드 데이터의 후처리 시간(resolveSeconds)이 이 연출 길이를 담는다 (10-ui.md 5-1 「연출 시간」).
    /// </summary>
    public class CardFxPresenter : MonoBehaviour
    {
        [Tooltip("연출 레이어 — 화면 전체를 덮는다. 연출 프리팹을 이 아래에 만든다.")]
        [SerializeField] private FxLayer fxLayer;
        [Tooltip("카드 뒷면 — 컷인 속 카드.")]
        [SerializeField] private Sprite cardBack;
        [Tooltip("캐릭터 그림 — 컷인에 나오는 강아지 (사람마다 그림이 생기기 전까지 모두 같다).")]
        [SerializeField] private Sprite character;
        [Tooltip("개판 등급 4 단계가 결과를 보인 뒤 연출을 지울 때까지 (초) — 칼질 꼬리가 사라지는 시간.")]
        [SerializeField] private float postTailSeconds = 0.6f;
        [Tooltip("게임 화면에서 3 단계를 재생하지 않는 카드 ID. 1021 개판 오분전 = 시작 제목(GaepanTitleView)만 보이고 시안의 컷인 장면은 없다 (10-ui.md 2-1 「개판 시작 제목」, 사용자 2026-11-13) — 컷인에도 같은 제목 · 도장이 들어 있다. 이펙트 시험장은 그대로 재생한다.")]
        [SerializeField] private int[] skipCardIds = { 1021 };

        private void Awake()
        {
            if (fxLayer == null || cardBack == null || character == null)
                Debug.LogError($"[{nameof(CardFxPresenter)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>
        /// 카드 하나의 3 단계(+ 개판 등급이면 카드만의 4 단계)를 재생한다. 개판 4 단계는 결과를 보일 순간에 끝난다 — 그 뒤 공통 연출이 이어지는 동안 꼬리만 남았다 지워진다.
        /// seats = 판에 남은 사람 전부(좌석 순서, 나 먼저 — 강아지 얼굴이 이 순서로 정해진다), receivers = 카드를 한 장씩 받는 사람 (무야호).
        /// </summary>
        public IEnumerator PlayRoutine(CardData card, CardFxSeat caster, IReadOnlyList<CardFxSeat> targets, IReadOnlyList<CardFxSeat> seats, IReadOnlyList<CardFxSeat> receivers)
        {
            if (System.Array.IndexOf(skipCardIds, card.id) >= 0)
                yield break;

            CardFx fx = null;
            yield return CardFxLoader.CreateRoutine(CardFxLoader.NameOf(card.id), fxLayer.transform, created => fx = created, false);
            if (fx == null)
                yield break;

            var rect = (RectTransform)fx.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;

            var anchors = new RectTransform[targets.Count];
            for (int i = 0; i < targets.Count; i++)
                anchors[i] = targets[i].Anchor;
            var context = new CardFxContext
            {
                Layer = fxLayer,
                Caster = caster.Anchor,
                Targets = anchors,
                CardId = card.id,
                Card = card,
                CardName = card.cardName,
                CardArt = card.LoadArt(),
                CardBack = cardBack,
                Character = character,
                CasterSeat = caster,
                TargetSeats = targets,
                Seats = seats,
                Receivers = receivers,
            };

            yield return fx.PlayRoutine(context);
            if (card.grade == CardGrade.Gaepan && fx.HasPostStage)
            {
                yield return fx.PlayPostRoutine(context);
                StartCoroutine(ReleaseLaterRoutine(fx, postTailSeconds));
                yield break;
            }
            StartCoroutine(CardFxLoader.ReleaseRoutine(fx));
        }

        private IEnumerator ReleaseLaterRoutine(CardFx fx, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            StartCoroutine(CardFxLoader.ReleaseRoutine(fx));
            fx = null;
        }
    }
}
