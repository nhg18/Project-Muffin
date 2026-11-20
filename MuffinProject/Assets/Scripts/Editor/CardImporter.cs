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
    /// 카드 가져오기 (11-card-list.md 0절 · 26-card-data.md 7-2) — cards.json 의 카드마다 Card_{ID}.asset 을 만들거나 고치고, 장수대로 Deck_Main 을 다시 짠다.
    /// 이름 · 종류 · 대상 · 대상 후보 조건 · 문구 · 등급 · 장수 · 효과 값을 매번 파일 값으로 덮는다 — 값의 원본은 cards.json 이다.
    /// 효과의 종류와 순서는 에셋이 정한다: 에셋에 효과가 하나도 없을 때만 파일의 종류대로 처음 만들고, 그 뒤로 파일과 다르면 그 카드는 실패로 알린다.
    /// 다 맞으면 Deck_Main 에 파일의 확인값을 적는다 — 이 빌드의 카드 데이터가 무엇인지 (26 7-6).
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
                    data.id = entry.id;
                    AssetDatabase.CreateAsset(data, path);
                    created++;
                }

                string before = Describe(data);
                data.type = ParseEnum<CardType>(entry.type, entry.id);
                if (data.effects.Count == 0 && entry.effects != null && entry.effects.Length > 0)
                    data.effects = entry.effects.Select(e => CreateEffect(e, entry.id)).Where(e => e != null).ToList();

                if (!CardSheet.TryApply(entry, data, out error))
                {
                    Debug.LogError($"[{nameof(CardImporter)}] {error} — 에셋의 효과 구성과 cards.json 을 맞추기", data);
                    failed++;
                }
                else if (Describe(data) != before)
                {
                    Debug.Log($"[{nameof(CardImporter)}] 카드 {entry.id} 바뀜\n  전: {before}\n  후: {Describe(data)}", data);
                    changed++;
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
            // 하나라도 못 얹었으면 에셋이 파일과 다르다 — 확인값을 비워, 파일과 같은 데이터인 척하지 않게
            deck.dataHash = failed == 0 ? CardSheet.HashOf(json) : "";
            EditorUtility.SetDirty(deck);

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(CardImporter)}] 카드 {sheet.cards.Length}종 (새로 {created} · 바뀜 {changed} · 실패 {failed}) · Deck_Main {entries.Sum(e => e.count)}장 · 확인값 {deck.dataHash}");
        }

        private static T ParseEnum<T>(string value, int cardId) where T : struct
        {
            if (Enum.TryParse(value, out T result))
                return result;
            throw new ArgumentException($"카드 {cardId}: {typeof(T).Name} 에 「{value}」 가 없다");
        }

        // 파일이 덮는 값 전부를 한 줄로 — 가져오기 전후를 비교해 바뀐 카드를 알린다
        private static string Describe(CardData data)
        {
            string effects = string.Join(" + ", data.effects.Select(e => e == null ? "(빈 칸)" : $"{e.GetType().Name}{JsonUtility.ToJson(e)}"));
            return $"{data.cardName} · {data.type} · {data.targetType} · {data.targetCondition} · {data.grade} · 「{data.description}」 · {effects}";
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
