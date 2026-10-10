using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.Game.Cards
{
    /// <summary>
    /// 덱 구성 1개 (에셋) — 어떤 카드를 몇 장 넣는가 (05-deck.md 8절).
    /// 카드 정의(CardData)와 분리한다: 카드가 무엇인지와 이번 덱에 몇 장 들어가는지는 다른 질문이다.
    /// 덱 구성(카드별 매수)은 미정 — 지금 에셋 값은 임시.
    /// </summary>
    [CreateAssetMenu(fileName = "Deck_", menuName = "CardSystem/Deck Data")]
    public class DeckData : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public CardData card;
            [Min(0)] public int count;
        }

        [Header("카드별 매수")]
        public List<Entry> entries = new List<Entry>();
    }
}
