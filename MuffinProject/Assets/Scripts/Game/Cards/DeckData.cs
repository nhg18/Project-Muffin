using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.Game.Cards
{
    /// <summary>
    /// 덱 구성 1개 (에셋) — 어떤 카드를 몇 장 넣는가 (05-deck.md 8절).
    /// 카드 정의(CardData)와 분리한다: 카드가 무엇인지와 이번 덱에 몇 장 들어가는지는 다른 질문이다.
    /// 게임에 나오는 카드는 전부 덱에서 나오므로, 화면이 종류 ID 로 카드를 찾는 사전도 이것이다 (GetCard). 프로토타입은 덱 하나.
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

        private Dictionary<int, CardData> _byId;

        /// <summary>이 덱에 들어가는 카드를 종류 ID(CardData.id)로 찾는다. 화면이 OnDrawn · OnCardUsed 의 cardId 로 그릴 때 쓴다. 없으면 null.</summary>
        public CardData GetCard(int cardId)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, CardData>();
                foreach (Entry entry in entries)
                    if (entry.card != null)
                        _byId[entry.card.id] = entry.card;
            }

            return _byId.TryGetValue(cardId, out CardData data) ? data : null;
        }

        private void OnValidate()
        {
            _byId = null; // 인스펙터에서 바뀌면 사전을 다시 만든다
        }
    }
}
