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
    /// 값의 원본은 값 파일(cards.json)이다 — 플레이를 시작하면 그 값을 카드 정의의 사본에 얹어 쓴다. 에셋은 고치지 않는다 (26-card-data.md 6-1).
    /// 이 에셋의 목록은 「이 빌드에 있는 카드」 와, 값 파일이 없거나 읽지 못했을 때 쓰는 값이다.
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

        [Tooltip("이 덱의 값 파일(cards.json). 플레이를 시작하면 이 파일의 이름 · 문구 · 수치 · 장수를 카드 정의의 사본에 얹어 쓴다 — 에셋은 고치지 않는다 (26-card-data.md 6-1). 비우면 위 목록의 에셋 값 그대로.")]
        public TextAsset sheet;

        private Dictionary<int, CardData> _byId;
        private List<Entry> _byIdSource; // _byId 를 만든 목록 — 플레이를 끝내면 사본 대신 에셋 목록으로 다시 만든다

        // 값 파일을 얹은 목록 (26 6-1) — 플레이 중 처음 쓸 때 한 번 만든다. 저장되지 않고, 플레이를 시작할 때마다 다시 만든다
        [NonSerialized] private List<Entry> _applied;

        /// <summary>
        /// 게임이 쓰는 카드 목록 — 플레이 중이고 값 파일이 있으면 그 값을 얹은 사본(파일 순서), 아니면 에셋 값.
        /// 에디터 도구(플레이 중이 아닐 때)는 에셋을 본다 — 사본을 씬 · 에셋에 물리지 않게.
        /// </summary>
        public IReadOnlyList<Entry> Entries => ActiveEntries;

        private List<Entry> ActiveEntries
        {
            get
            {
                if (sheet == null || !Application.isPlaying)
                    return entries;

                return _applied ??= ApplySheet();
            }
        }

        /// <summary>이 덱에 들어가는 카드를 종류 ID(CardData.id)로 찾는다. 없으면 null.</summary>
        public CardData GetCard(int cardId)
        {
            List<Entry> active = ActiveEntries;
            if (_byId == null || _byIdSource != active)
            {
                _byId = new Dictionary<int, CardData>();
                _byIdSource = active;
                foreach (Entry entry in active)
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
        /// 플레이 밖(에디터의 규칙 시험 · 봇 측정)에서도 게임과 같은 값 파일의 값으로 만든다 — 그때 만든 사본은 카드를 만든 뒤 바로 버린다.
        /// 카드(Card)는 카드 정의를 쥐지 않으므로 사본이 씬 · 에셋에 물릴 일이 없다 (26 6-1).
        /// </summary>
        public List<Card> CreateCards()
        {
            var cards = new List<Card>();
            var byId = new Dictionary<int, CardData>();
            int nextInstanceId = 1;
            bool temporary = sheet != null && !Application.isPlaying;
            List<Entry> source = temporary ? ApplySheet() : ActiveEntries;

            foreach (Entry entry in source)
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

            if (temporary)
            {
                foreach (Entry entry in source)
                {
                    if (entry.card != null && IsCopy(entry.card))
                        DestroyImmediate(entry.card);
                }
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

        // 값 파일을 카드 정의의 사본에 얹는다 (26 6-1 · 8절). 순서는 파일 순서 — 같은 빌드끼리 장 번호(InstanceId)가 같아야 방장 이주가 된다 (09-network.md 9.1).
        // 카드 한 줄이 틀리면 그 카드만 에셋 값으로 간다. 파일을 읽지 못하면 전부 에셋 값으로 간다. 어느 쪽이든 에러로 알린다 — 같은 빌드는 모두 같은 결과다.
        private List<Entry> ApplySheet()
        {
            if (!CardSheet.TryParse(sheet.text, out CardSheet parsed, out string error))
            {
                Debug.LogError($"[{nameof(DeckData)}] {sheet.name}: {error} — 에셋 값으로 간다", this);
                return entries;
            }

            var assets = new Dictionary<int, CardData>();
            foreach (Entry entry in entries)
            {
                if (entry.card == null)
                    continue;

                CheckId(assets, entry.card); // 겹치면 뒤의 것만 남는다 — 앞 카드가 조용히 빠지지 않게 알린다
                assets[entry.card.id] = entry.card;
            }

            var applied = new List<Entry>();
            foreach (CardSheetEntry line in parsed.cards)
            {
                if (!assets.TryGetValue(line.id, out CardData asset))
                {
                    Debug.LogError($"[{nameof(DeckData)}] 카드 {line.id}: {name} 에 카드 에셋이 없다 — 덱에서 빠진다. 새 카드는 「카드 가져오기」 로 에셋을 만든다 (26-card-data.md 7-3)", this);
                    continue;
                }

                assets.Remove(line.id);
                CardData copy = Instantiate(asset);
                copy.name = asset.name;
                // 저장만 막는다 — DontSave 의 DontUnloadUnusedAsset 까지 붙이면 플레이를 끝내도 거둬지지 않고 쌓인다 (26 6-1)
                copy.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                if (CardSheet.TryApply(line, copy, out error))
                {
                    applied.Add(new Entry { card = copy, count = line.count });
                    continue;
                }

                // 실패한 사본은 반쯤 바뀌어 있다 — 버리고 에셋 값을 쓴다. 장수는 파일대로
                Debug.LogError($"[{nameof(DeckData)}] {error} — 이 카드는 에셋 값으로 간다", asset);
                if (Application.isPlaying)
                    Destroy(copy);
                else
                    DestroyImmediate(copy);
                applied.Add(new Entry { card = asset, count = line.count });
            }

            // 파일에 없는 카드는 덱에서 빠진다 (0장). 정의는 남긴다 — 찾으면 에셋 값으로 보인다
            foreach (CardData rest in assets.Values)
                applied.Add(new Entry { card = rest, count = 0 });

            return applied;
        }

        // ApplySheet 가 만든 사본인지 — 에셋에는 저장 막는 표시가 없다
        private static bool IsCopy(CardData data) => (data.hideFlags & HideFlags.DontSaveInEditor) != 0;

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
