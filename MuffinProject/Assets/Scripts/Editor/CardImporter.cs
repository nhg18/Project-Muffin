using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEditor;
using UnityEngine;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 가져오기 (11-card-list.md 0절 · 26-card-data.md 7-3) — cards.json 의 카드마다 Card_{ID}.asset 을 만들거나 고치고, 장수대로 Deck_Main 을 다시 짠다.
    /// **새 카드를 넣을 때** 쓴다: 에셋을 만들고 Deck_Main 의 목록에 넣는다. 값만 바뀐 것은 누르지 않아도 된다 — 게임이 플레이를 시작할 때 파일 값을 얹는다 (DeckData).
    /// 누르면 이름 · 종류 · 대상 · 대상 후보 조건 · 문구 · 등급 · 장수 · 효과 값을 파일 값으로 덮어, 인스펙터에 보이는 에셋 값도 파일과 같아진다.
    /// 효과의 종류와 순서는 에셋이 정한다: 에셋에 효과가 하나도 없을 때만 파일의 종류대로 처음 만들고, 그 뒤로 파일과 다르면 그 카드는 실패로 알린다.
    /// 카운터의 반응 종류 · 반응 조건 · 「막을 수 없음」 은 파일에 reactsTo 가 적힌 카드만 덮는다 — 가져오기만 옮기고, 게임이 얹는 값에는 들지 않는다.
    /// cards.json 에는 구현할 카드(노션 확정 + 효과가 만들어진 카드)만 적는다.
    /// </summary>
    public static class CardImporter
    {
        private const string Folder = "Assets/Cards/Main";
        private const string JsonPath = Folder + "/cards.json";
        private const string DeckPath = Folder + "/Deck_Main.asset";
        private const string ArtFolder = "Assets/Resources/CardArt"; // ResourcePaths.CardArt

        [MenuItem("Chapchu/카드 가져오기")]
        public static void Import()
        {
            // 플레이 중에는 고치지 않는다 — 게임이 사본을 만든 카드 에셋이 실행 중에 바뀐다
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning($"[{nameof(CardImporter)}] 플레이 중에는 가져오지 않는다 — 플레이를 끝낸 뒤 다시 누르기");
                return;
            }

            if (!File.Exists(JsonPath))
            {
                Debug.LogError($"[{nameof(CardImporter)}] {JsonPath} 가 없다");
                return;
            }

            string json = File.ReadAllText(JsonPath);
            if (!CardSheet.TryParse(json, out CardSheet sheet, out string error))
            {
                Debug.LogError($"[{nameof(CardImporter)}] {JsonPath}: {error}");
                return;
            }

            var entries = new List<DeckData.Entry>();
            int created = 0, changed = 0, failed = 0;

            foreach (CardSheetEntry entry in sheet.cards)
            {
                string path = $"{Folder}/Card_{entry.id}.asset";
                CardData data = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CardData>();
                    AssetDatabase.CreateAsset(data, path);
                    created++;
                }

                // 복제 · 손으로 고친 에셋의 id 도 파일 값으로 되돌린다 (26 7-2)
                data.id = entry.id;
                string before = Describe(data);

                // 사본에 얹고, 다 맞을 때만 에셋에 옮긴다 — 실패한 카드는 반쯤 바뀐 채 저장되지 않는다
                CardData copy = UnityEngine.Object.Instantiate(data);
                try
                {
                    copy.name = data.name;
                    // 모르는 종류 이름이면 사본의 종류를 그대로 둔다 — TryApply 가 종류가 다르다며 실패로 센다
                    if (TryParseName(entry.type, out CardType type))
                        copy.type = type;
                    if (copy.effects.Count == 0 && entry.effects != null && entry.effects.Length > 0)
                    {
                        // 하나라도 못 만들면 넣지 않는다 — 효과 수가 달라 실패로 센다. 짧은 목록이 저장되면 다음 가져오기가 다시 만들지 못한다
                        List<CardEffect> effects = entry.effects.Select(e => CreateEffect(e, entry.id)).ToList();
                        if (!effects.Contains(null))
                            copy.effects = effects;
                    }

                    if (!TryApplyReaction(entry, copy, out error) || !TryApplyTrapTrigger(entry, copy, out error) || !CardSheet.TryApply(entry, copy, out error))
                    {
                        Debug.LogError($"[{nameof(CardImporter)}] {error} — 에셋의 효과 구성과 cards.json 을 맞추기", data);
                        failed++;
                    }
                    else
                    {
                        EditorUtility.CopySerialized(copy, data);
                        if (Describe(data) != before)
                        {
                            Debug.Log($"[{nameof(CardImporter)}] 카드 {entry.id} 바뀜\n  전: {before}\n  후: {Describe(data)}", data);
                            changed++;
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }

                EnsureSprite(entry.id);
                EditorUtility.SetDirty(data);
                entries.Add(new DeckData.Entry { card = data, count = entry.count });
            }

            DeckData deck = AssetDatabase.LoadAssetAtPath<DeckData>(DeckPath);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<DeckData>();
                AssetDatabase.CreateAsset(deck, DeckPath);
            }
            deck.entries = entries;
            deck.sheet = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath); // 게임이 플레이를 시작할 때 이 파일 값을 얹는다 (DeckData)
            EditorUtility.SetDirty(deck);

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(CardImporter)}] 카드 {sheet.cards.Length}종 (새로 {created} · 바뀜 {changed} · 실패 {failed}) · Deck_Main {entries.Sum(e => e.count)}장");
        }

        // 카운터의 반응 종류 · 반응 조건 · 「막을 수 없음」 — 비어 있으면 어디에도 못 내는 카드가 된다 (CardData.reactsTo). 파일에 reactsTo 가 적힌 카드만 덮는다
        private static bool TryApplyReaction(CardSheetEntry entry, CardData data, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(entry.reactsTo))
                return true;

            var reactsTo = new List<CardType>();
            foreach (string raw in entry.reactsTo.Split(','))
            {
                if (!TryParseName(raw.Trim(), out CardType type))
                {
                    error = $"카드 {entry.id}: 반응 종류 「{raw.Trim()}」 를 모른다";
                    return false;
                }

                reactsTo.Add(type);
            }

            ReactCondition condition = ReactCondition.Any;
            if (!string.IsNullOrEmpty(entry.reactCondition) && !TryParseName(entry.reactCondition, out condition))
            {
                error = $"카드 {entry.id}: 반응 조건 「{entry.reactCondition}」 를 모른다";
                return false;
            }

            data.reactsTo = reactsTo;
            data.reactCondition = condition;
            data.uncounterable = entry.uncounterable;
            return true;
        }

        // 함정의 발동 조건 종류 · 값 — 파일에 trapTrigger 가 적힌 카드만 덮는다 (04-card.md 6절 「발동 조건」)
        private static bool TryApplyTrapTrigger(CardSheetEntry entry, CardData data, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(entry.trapTrigger))
                return true;

            if (!TryParseName(entry.trapTrigger.Trim(), out TrapTrigger trigger))
            {
                error = $"카드 {entry.id}: 함정 발동 조건 「{entry.trapTrigger}」 를 모른다";
                return false;
            }

            data.trapTrigger = trigger;
            data.triggerValue = entry.triggerValue;
            return true;
        }

        // enum 이름이 정확히 맞을 때만 — CardSheet 의 이름 읽기와 같은 기준 (숫자 · 쉼표 목록은 받지 않는다)
        private static bool TryParseName<T>(string raw, out T value) where T : struct, Enum
        {
            value = default;
            if (raw == null || !Enum.IsDefined(typeof(T), raw))
                return false;

            value = (T)Enum.Parse(typeof(T), raw);
            return true;
        }

        // 파일이 덮는 값 전부를 한 줄로 — 가져오기 전후를 비교해 바뀐 카드를 알린다
        private static string Describe(CardData data)
        {
            string effects = string.Join(" + ", data.effects.Select(e => e == null ? "(빈 칸)" : $"{e.GetType().Name}{JsonUtility.ToJson(e)}"));
            string reaction = data.reactsTo.Count > 0 ? $" · 반응 {string.Join("/", data.reactsTo)} {data.reactCondition}{(data.uncounterable ? " 막을 수 없음" : "")}" : "";
            string trap = data.trapTrigger != TrapTrigger.None ? $" · 발동 {data.trapTrigger}{(data.triggerValue > 0 ? " " + data.triggerValue : "")}" : "";
            return $"{data.cardName} · {data.type} · {data.targetType} · {data.targetCondition} · {data.grade} · 「{data.description}」 · {effects}{reaction}{trap}";
        }

        // 그림 파일이 아직 Sprite 가 아니면 가져오기 설정을 바꾼다 — CardData.LoadArt 가 Sprite 로 불러온다. 그림이 없는 카드는 넘어간다
        private static void EnsureSprite(int cardId)
        {
            var importer = AssetImporter.GetAtPath($"{ArtFolder}/{cardId}.png") as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Sprite)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }

        // 효과를 종류만 만든다 — 값은 CardSheet.TryApply 가 넣는다
        private static CardEffect CreateEffect(CardSheetEffect entry, int cardId)
        {
            Type type = typeof(CardEffect).Assembly.GetTypes().FirstOrDefault(t => t.Name == entry.type && typeof(CardEffect).IsAssignableFrom(t) && !t.IsAbstract);
            if (type == null)
            {
                Debug.LogError($"[{nameof(CardImporter)}] 카드 {cardId}: 효과 {entry.type} 가 없다");
                return null;
            }

            return (CardEffect)Activator.CreateInstance(type);
        }
    }
}
