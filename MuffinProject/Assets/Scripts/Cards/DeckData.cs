using System;
using System.Collections.Generic;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEngine;

namespace Chapchu.Cards
{
    /// <summary>
    /// 덱 구성 1개 (에셋, 05-deck.md 8절) — 어떤 카드를 몇 장 넣는가. 카드가 무엇인지(CardData)와 몇 장인지는 다른 질문이라 나눈다.
    /// 게임에 나오는 카드는 전부 덱에서 나오므로, 화면이 종류 ID 로 카드를 찾는 사전도 이것이다 (GetCard).
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

        /// <summary>이 덱에 들어가는 카드를 종류 ID(CardData.id)로 찾는다. 없으면 null.</summary>
        public CardData GetCard(int cardId)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, CardData>();
                foreach (Entry entry in entries)
                {
                    if (entry.card != null)
                        _byId[entry.card.id] = entry.card;
                }
            }

            return _byId.TryGetValue(cardId, out CardData data) ? data : null;
        }

        /// <summary>
        /// 게임에 쓸 카드를 장마다 만든다 (05-deck.md 3절 1 · 2). 장 번호(InstanceId)는 1부터 (09-network.md 7절).
        /// 방장이 게임 시작 때 한 번 부른다. 효과 목록은 복사해 넘긴다 — 에셋 리스트를 규칙과 공유하지 않는다.
        /// </summary>
        public List<Card> CreateCards()
        {
            var cards = new List<Card>();
            int nextInstanceId = 1;

            foreach (Entry entry in entries)
            {
                if (entry.card == null)
                    continue;

                CardData data = entry.card;
                CardEffect[] effects = GetEffects(data);
                CardType[] reactsTo = data.reactsTo.ToArray();

                for (int i = 0; i < entry.count; i++)
                    cards.Add(new Card(nextInstanceId++, data.id, data.type, data.targetType, effects, reactsTo, data.revealSeconds, data.resolveSeconds, data.trapTrigger, data.triggerValue, data.reactCondition));
            }

            return cards;
        }

        // 비어 있는 효과는 빼고 알린다 — 효과 클래스 이름 · 네임스페이스가 바뀌면 에셋에서 그 칸이 비어 버린다.
        private static CardEffect[] GetEffects(CardData data)
        {
            var effects = new List<CardEffect>();
            for (int i = 0; i < data.effects.Count; i++)
            {
                if (data.effects[i] == null)
                {
                    Debug.LogError($"[{nameof(DeckData)}] {data.name} 의 효과 {i}번이 비어 있다 — 인스펙터에서 지우고 다시 추가", data);
                    continue;
                }

                effects.Add(data.effects[i]);
            }

            return effects.ToArray();
        }

        private void OnValidate()
        {
            _byId = null; // 인스펙터에서 바뀌면 사전을 다시 만든다
        }
    }
}
