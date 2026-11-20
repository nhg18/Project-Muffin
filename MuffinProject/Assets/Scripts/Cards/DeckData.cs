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
    /// 에셋 값이 빌드 기본본이다. 원격 카드 데이터를 받으면 그 값을 얹은 사본으로 바꿔 쓴다 (TryPatch, 26-card-data.md 6-1) — 에셋은 고치지 않는다.
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

        [Tooltip("이 덱을 만든 카드 데이터 파일(cards.json)의 확인값 — 「카드 가져오기」 가 적는다. 같은 값끼리만 같은 방에 든다 (26-card-data.md 7-6). 손으로 고치지 않는다.")]
        public string dataHash;

        private Dictionary<int, CardData> _byId;

        // 원격 카드 데이터를 얹은 사본 (26 6-1). 없으면 에셋 값(빌드 기본본)을 쓴다. 저장되지 않는다 — 켤 때마다 CardDataService 가 맞춘다
        [NonSerialized] private List<Entry> _patched;
        [NonSerialized] private string _patchedHash;

        /// <summary>지금 쓰는 카드 데이터의 확인값 — 원격 데이터를 얹었으면 그 값, 아니면 빌드 기본본의 값.</summary>
        public string ActiveHash => _patched != null ? _patchedHash : dataHash;

        /// <summary>원격 카드 데이터를 얹어 쓰는 중인지.</summary>
        public bool IsPatched => _patched != null;

        private List<Entry> ActiveEntries => _patched ?? entries;

        /// <summary>이 덱에 들어가는 카드를 종류 ID(CardData.id)로 찾는다. 없으면 null.</summary>
        public CardData GetCard(int cardId)
        {
            if (_byId == null)
            {
                _byId = new Dictionary<int, CardData>();
                foreach (Entry entry in ActiveEntries)
                {
                    if (entry.card == null)
                        continue;

                    CheckId(_byId, entry.card);
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
            var byId = new Dictionary<int, CardData>();
            int nextInstanceId = 1;

            foreach (Entry entry in ActiveEntries)
            {
                if (entry.card == null)
                    continue;

                CardData data = entry.card;
                CheckId(byId, data);
                byId[data.id] = data;
                CardEffect[] effects = GetEffects(data);
                CardType[] reactsTo = data.reactsTo.ToArray();

                for (int i = 0; i < entry.count; i++)
                    cards.Add(new Card(nextInstanceId++, data.id, data.type, data.targetType, effects, reactsTo, data.revealSeconds, data.resolveSeconds, data.trapTrigger, data.triggerValue, data.reactCondition,
                        data.targetCondition));
            }

            return cards;
        }

        /// <summary>
        /// 덱에 든 카드 종류의 그림을 전부 불러온다 (26 6-3) — 장수가 아니라 종류 수만큼. 게임 화면이 열릴 때 한 번 부른다.
        /// 돌려준 목록을 들고 있는 동안 그림이 메모리에 남고, 놓으면 다음 씬 전환 때 내려간다.
        /// </summary>
        public List<Sprite> LoadArt()
        {
            var art = new List<Sprite>();
            foreach (Entry entry in ActiveEntries)
            {
                if (entry.card == null || entry.count <= 0)
                    continue;

                Sprite sprite = entry.card.LoadArt();
                if (sprite != null)
                    art.Add(sprite);
            }

            return art;
        }

        /// <summary>
        /// 카드 데이터 파일을 얹는다 (26 6-1 · 8절). 카드 정의의 사본을 만들어 파일 값을 넣고, 전부 맞을 때만 그 사본으로 바꾼다.
        /// 이 빌드가 모르는 카드 · 종류가 다른 카드 · 효과 구성이 다른 카드가 하나라도 있으면 아무것도 바꾸지 않고 false.
        /// 순서는 파일 순서 — 같은 파일을 얹은 기기끼리 장 번호(InstanceId)가 같아야 방장 이주가 된다 (09-network.md 9.1).
        /// </summary>
        public bool TryPatch(CardSheet sheet, string hash, out string error)
        {
            var builtIn = new Dictionary<int, CardData>();
            foreach (Entry entry in entries)
            {
                if (entry.card != null)
                    builtIn[entry.card.id] = entry.card;
            }

            var patched = new List<Entry>();
            error = null;
            foreach (CardSheetEntry line in sheet.cards)
            {
                if (!builtIn.TryGetValue(line.id, out CardData original))
                {
                    error = $"카드 {line.id}: 이 빌드에 없다";
                    break;
                }

                CardData copy = Instantiate(original);
                copy.name = original.name;
                copy.hideFlags = HideFlags.DontSave;
                patched.Add(new Entry { card = copy, count = line.count });
                builtIn.Remove(line.id);
                if (!CardSheet.TryApply(line, copy, out error))
                    break;
            }

            if (error != null)
            {
                DestroyCopies(patched);
                return false;
            }

            // 파일에 없는 카드는 덱에서 빠진다 (0장). 정의는 남겨 둔다 — 화면이 그 카드를 찾을 일은 없지만, 찾으면 빌드 값으로 보인다
            foreach (CardData rest in builtIn.Values)
                patched.Add(new Entry { card = rest, count = 0 });

            ClearPatch();
            _patched = patched;
            _patchedHash = hash;
            return true;
        }

        /// <summary>얹은 것을 걷고 에셋 값(빌드 기본본)으로 돌아간다.</summary>
        public void ClearPatch()
        {
            if (_patched != null)
                DestroyCopies(_patched);

            _patched = null;
            _patchedHash = null;
            _byId = null;
        }

        // 사본만 지운다 — 파일에 없어 그대로 넣은 에셋(빌드 카드)은 건드리지 않는다
        private static void DestroyCopies(List<Entry> patched)
        {
            foreach (Entry entry in patched)
            {
                if (entry.card == null || entry.card.hideFlags != HideFlags.DontSave)
                    continue;

                if (Application.isPlaying)
                    Destroy(entry.card);
                else
                    DestroyImmediate(entry.card);
            }
        }

        // 종류 ID 가 0(새로 만든 에셋의 기본값)이거나 다른 카드와 겹치면 알린다 — 규칙(Card)과 화면(GetCard)이 서로 다른 카드를 보게 된다 (09-network.md 7절).
        private void CheckId(Dictionary<int, CardData> byId, CardData data)
        {
            if (data.id == 0)
                Debug.LogError($"[{nameof(DeckData)}] {data.name} 의 id 가 0 이다 — 다른 카드와 겹치지 않는 값을 넣기", data);
            else if (byId.TryGetValue(data.id, out CardData other) && other != data)
                Debug.LogError($"[{nameof(DeckData)}] {other.name} 와 {data.name} 의 id({data.id})가 겹친다 — {name} 에서", data);
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
