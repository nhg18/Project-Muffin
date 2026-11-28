using System.Collections;
using System.Collections.Generic;
using Chapchu.Cards;
using Chapchu.UI.Fx;
using Chapchu.UI.Fx.Gaepan;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 카드 연출 3 단계(카드별 능력 발동 연출)와 개판 등급 카드의 4 단계 — 효과가 적용된 카드의 연출 프리팹(Resources/CardFx/Card_{카드 ID})을
    /// 그때 불러와 화면 전체를 덮는 연출 레이어에서 재생하고, 끝나면 메모리에서 내린다 (10-ui.md 5-1). 연출이 없는 카드는 바로 끝난다.
    /// 화면만의 연출이라 판정과 상관없다. 4 단계 공통 연출(피해 숫자 · +n장 등)은 게임 화면이 뒤따르는 결과 알림으로 그린다 — 연출의 결과 요청(Impact)은 쓰지 않는다.
    /// 다음 턴이 연출 중에 시작하지 않게 카드 데이터의 후처리 시간(resolveSeconds)이 이 연출 길이를 담는다 (10-ui.md 5-1 「연출 시간」).
    /// 개판 오분전의 처형 장면(5 바퀴 뒤 판정 팻말 다음)도 같은 연출 프리팹으로 여기서 재생한다 (10-ui.md 「개판 처형 장면」).
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

        /// <summary>
        /// 개판 오분전의 처형 장면을 미리 만들어 둔다 — 판정 팻말이 도는 동안 부르고, 팻말이 끝나면 <see cref="PlayGaepanExecutionRoutine"/> 으로 재생한다.
        /// 만든 것은 prepared 로 넘긴다 (연출 프리팹이 없으면 null). 재생 전에는 보이지 않는다.
        /// </summary>
        public IEnumerator PrepareGaepanExecutionRoutine(CardData card, System.Action<GaepanFx> prepared)
        {
            CardFx fx = null;
            yield return CardFxLoader.CreateRoutine(CardFxLoader.NameOf(card.id), fxLayer.transform, created => fx = created, true);
            var gaepan = fx as GaepanFx;
            if (gaepan == null)
            {
                if (fx != null)
                    Debug.LogError($"[{nameof(CardFxPresenter)}] {card.cardName} 의 연출 프리팹에 {nameof(GaepanFx)} 가 없습니다 — 처형 장면을 건너뜁니다.", this);
                StartCoroutine(CardFxLoader.ReleaseRoutine(fx));
                fx = null;
                prepared(null);
                yield break;
            }

            var rect = (RectTransform)gaepan.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            fx = null;
            prepared(gaepan);
        }

        /// <summary>
        /// 미리 만든 처형 장면을 끝까지 재생하고 지운다. caster = 개판을 건 사람(없거나 꼴찌 본인이면 다른 사람의 강아지가 뭉치를 이끈다 — 시안 GP_FX.judge),
        /// victim = 꼴찌 (판에 자리가 없으면 재생하지 않고 지운다), seats = 판에 남은 사람 전부 (강아지 얼굴이 이 순서로 정해진다).
        /// </summary>
        public IEnumerator PlayGaepanExecutionRoutine(GaepanFx fx, CardData card, CardFxSeat caster, CardFxSeat victim, IReadOnlyList<CardFxSeat> seats)
        {
            if (victim != null)
            {
                string victimKey = FxDog.KeyOf(victim, seats);
                string heroKey = caster != null && caster != victim ? FxDog.KeyOf(caster, seats) : victimKey;
                if (heroKey == victimKey)
                {
                    // 꼴찌가 아닌 다른 사람의 강아지 (없으면 꼴찌와 다른 아무 강아지)
                    foreach (CardFxSeat seat in seats)
                    {
                        if (seat != victim && FxDog.KeyOf(seat, seats) != victimKey)
                        {
                            heroKey = FxDog.KeyOf(seat, seats);
                            break;
                        }
                    }
                }
                if (heroKey == victimKey)
                    heroKey = FxDog.Keys[victimKey == FxDog.Keys[0] ? 1 : 0];
                yield return fx.PlayExecutionRoutine(card.id, heroKey, victimKey, victim.Name);
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
