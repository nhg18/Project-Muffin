using System.Collections.Generic;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEngine;

namespace Chapchu.Cards
{
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

        [TextArea]
        public string description;

        [Tooltip("카운터만: 체인 맨 위 카드가 이 종류일 때 낼 수 있다 (04-card.md 7절). 비우면 어디에도 못 낸다.")]
        public List<CardType> reactsTo = new List<CardType>();

        [Header("연출 시간 (10-ui.md 5-1) — 방장이 반응 · 턴 시각 계산에 쓴다. 연출 길이를 바꾸면 같이 바꾼다")]
        [Tooltip("1 ~ 3 단계(내는 중 · 테이블 · 카드 이펙트) 초. 반응 시간 5초가 이 뒤에 열린다.")]
        [Min(0)] public float revealSeconds = 1.46f;

        [Tooltip("4 단계 + 버림 더미로 이동 초. 체인 맨 아래 카드면 체인을 처리하고 이만큼 뒤에 다음 턴.")]
        [Min(0)] public float resolveSeconds = 1.28f;

        // 위에서부터 순서대로 실행. 인스펙터의 + 메뉴(Editor/CardDataEditor)로 종류를 골라 추가한다.
        [SerializeReference]
        public List<CardEffect> effects = new List<CardEffect>();
    }
}
