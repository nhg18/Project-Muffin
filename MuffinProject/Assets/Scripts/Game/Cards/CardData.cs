using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chapchu.Game.Cards
{
    // CardType · TargetType 은 서버도 쓰므로 서버 어셈블리(Game/Server/CardRule.cs)에 있다.

    /// <summary>
    /// 카드 종류 1개의 원본 데이터 (에셋). 서버 · 화면 양쪽이 CardId 로 찾아 읽는다 — 화면은 표시(이름 · 그림 · 설명)에, 서버는 ToRule() 로 뽑은 규칙에.
    /// 게임 안의 카드 한 장(Card)은 서버(GameServer.InitDeck)만 만든다. 이 에셋은 장을 만들지 않는다.
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

        [Header("카드 효과 리스트")]
        public List<CardEffect> effects = new List<CardEffect>();

        [Header("덱 구성")]
        [Tooltip("이번 게임 덱에 몇 장 넣는가. 0 이면 덱에 안 들어간다. 매수는 05-deck.md 미정 — 지금 값은 임시")]
        public int deckCount;

        /// <summary>서버(GameServer)가 판정에 쓰는 규칙만 뽑는다. 게임 시작 때 방장이 1회 부른다. 데미지는 effects 의 DamageEffect 수치를 합친다.</summary>
        public CardRule ToRule()
        {
            int damage = effects.OfType<DamageEffect>().Sum(e => e.damageAmount);
            return new CardRule(id, type, targetType, damage);
        }
    }
}
