using System.Collections.Generic;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEngine;

namespace Chapchu.Cards
{
    /// <summary>카드 등급 (노션 「등급」 · 11-card-list.md 1-1). 에셋에는 순서 번호로 저장된다 — 끝에만 추가한다.</summary>
    public enum CardGrade
    {
        Normal, // 일반
        Fine,   // 고급
        Rare,   // 희귀
        Gaepan  // 개판
    }

    /// <summary>
    /// 카드 종류 1개의 정의 (에셋, 05-deck.md 8절). "이 카드가 무엇인가" 만 담는다 — 덱에 몇 장인지는 DeckData, 게임 안의 한 장은 Card.
    /// 화면은 CardId 로 이것을 찾아 그린다.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "CardSystem/Card Data")]
    public class CardData : ScriptableObject
    {
        public int id;
        public string cardName;
        public Sprite cardImage;
        public CardType type;
        public TargetType targetType;

        [Tooltip("고르는 대상 타입만: 대상 후보 조건 (11-card-list.md 1-2 「추가 사용 조건」). 맞지 않는 사람은 고를 수 없다.")]
        public TargetCondition targetCondition;

        [Tooltip("카드 앞면 연출 단계 (노션 「등급」, 10-ui.md 2-1 「카드 등급 연출」). 판정과 무관한 표시 값.")]
        public CardGrade grade;

        [TextArea]
        public string description;

        [Tooltip("카운터 · 「카드 반응」 함정: 체인 맨 위 카드가 이 종류일 때 낼 수 있다 (04-card.md 6 · 7절). 비우면 어디에도 못 낸다.")]
        public List<CardType> reactsTo = new List<CardType>();

        [Tooltip("함정만: 발동 조건 종류 (04-card.md 6절 「발동 시점」).")]
        public TrapTrigger trapTrigger;

        [Tooltip("함정 발동 조건의 값 — HpAtMost 면 이 HP 이하일 때, AfterDrewMany 면 이 장수 이상.")]
        public int triggerValue;

        [Tooltip("카운터 · 「카드 반응」 함정: 반응 조건 (04-card.md 7절 「반응 조건」).")]
        public ReactCondition reactCondition;

        [Header("연출 시간 (10-ui.md 5-1) — 방장이 반응 · 턴 시각 계산에 쓴다. 연출 길이를 바꾸면 같이 바꾼다")]
        [Tooltip("2 ~ 3 단계(카드 이펙트 · 가운데 확대) 초 — 1 단계는 없다 (2026-11-11). 반응 시간이 이 뒤에 열린다.")]
        [Min(0)] public float revealSeconds = 1f;

        [Tooltip("4 단계 + 버림 더미로 이동 초. 체인 맨 아래 카드면 체인을 처리하고 이만큼 뒤에 다음 턴.")]
        [Min(0)] public float resolveSeconds = 1.28f;

        [Header("화면 덮기 (10-ui.md 5-1) — 효과가 적용될 때 모든 화면을 이 색으로 덮었다가 걷는다. 화면만의 연출이라 방장 시각 계산에 들지 않는다")]
        [Tooltip("덮는 색 · 진하기(알파). 알파 0 이면 덮지 않는다 — 113 기억 삭제: 흰색 97% (시안 whiteout).")]
        public Color resolveFlashColor = Color.clear;

        [Tooltip("다 걷힐 때까지 초 — 113 기억 삭제: 1 (시안 whiteout).")]
        [Min(0)] public float resolveFlashSeconds;

        // 위에서부터 순서대로 실행. 인스펙터의 + 메뉴(Editor/CardDataEditor)로 종류를 골라 추가한다.
        [SerializeReference]
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
