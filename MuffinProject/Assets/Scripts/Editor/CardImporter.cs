using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEditor;
using UnityEngine;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 가져오기 (11-card-list.md 0절) — cards.json 의 카드마다 Card_{ID}.asset 을 만들거나 고치고, 장수대로 Deck_Main 을 다시 짠다.
    /// 노션 속성과 1:1 인 값(이름 · 종류 · 대상 · 대상 후보 조건 · 문구 · 등급 · 장수)과 그림은 매번 덮는다.
    /// 효과는 에셋에 효과가 하나도 없을 때만 json 으로 처음 채운다 — 그 뒤로는 에셋(인스펙터)이 원본이다.
    /// cards.json 에는 구현할 카드(노션 확정 + 효과가 만들어진 카드)만 적는다.
    /// </summary>
    public static class CardImporter
    {
        private const string Folder = "Assets/Cards/Main";
        private const string JsonPath = Folder + "/cards.json";
        private const string DeckPath = Folder + "/Deck_Main.asset";

        [Serializable]
        private class CardList
        {
            public CardEntry[] cards;
        }

        [Serializable]
        private class CardEntry
        {
            public int id;
            public string name;
            public string type;            // CardType 이름
            public string target;          // TargetType 이름
            public string targetCondition; // TargetCondition 이름, 비우면 None
            public string grade;           // CardGrade 이름
            public int count;
            public string text;
            public string art;             // 그림 경로 (Assets/...), 비우면 그림 없음
            public EffectEntry[] effects;
        }

        // 효과 하나 — type 은 CardEffect 를 상속한 클래스 이름, args 는 "필드=값, 필드=값" (예: "subject=Target, amount=20")
        [Serializable]
        private class EffectEntry
        {
            public string type;
            public string args;
        }

        [MenuItem("Chapchu/카드 가져오기")]
        public static void Import()
        {
            if (!File.Exists(JsonPath))
            {
                Debug.LogError($"[{nameof(CardImporter)}] {JsonPath} 가 없다");
                return;
            }

            CardList list = JsonUtility.FromJson<CardList>(File.ReadAllText(JsonPath));
            var entries = new List<DeckData.Entry>();
            int created = 0, effectsFilled = 0;

            foreach (CardEntry entry in list.cards)
            {
                string path = $"{Folder}/Card_{entry.id}.asset";
                CardData data = AssetDatabase.LoadAssetAtPath<CardData>(path);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<CardData>();
                    AssetDatabase.CreateAsset(data, path);
                    created++;
                }

                data.id = entry.id;
                data.cardName = entry.name;
                data.type = ParseEnum<CardType>(entry.type, entry.id);
                data.targetType = ParseEnum<TargetType>(entry.target, entry.id);
                data.targetCondition = string.IsNullOrEmpty(entry.targetCondition) ? TargetCondition.None : ParseEnum<TargetCondition>(entry.targetCondition, entry.id);
                data.grade = ParseEnum<CardGrade>(entry.grade, entry.id);
                data.description = entry.text;
                data.cardImage = string.IsNullOrEmpty(entry.art) ? null : LoadSprite(entry.art, entry.id);

                if (data.effects.Count == 0 && entry.effects != null && entry.effects.Length > 0)
                {
                    data.effects = entry.effects.Select(e => CreateEffect(e, entry.id)).Where(e => e != null).ToList();
                    effectsFilled++;
                }

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
            EditorUtility.SetDirty(deck);

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(CardImporter)}] 카드 {list.cards.Length}종 (새로 {created} · 효과 처음 채움 {effectsFilled}) · Deck_Main {entries.Sum(e => e.count)}장");
        }

        private static T ParseEnum<T>(string value, int cardId) where T : struct
        {
            if (Enum.TryParse(value, out T result))
                return result;
            throw new ArgumentException($"카드 {cardId}: {typeof(T).Name} 에 「{value}」 가 없다");
        }

        // 그림 파일을 Sprite 로 가져온다 — 아직 Sprite 가 아니면 가져오기 설정을 바꾼다.
        private static Sprite LoadSprite(string path, int cardId)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[{nameof(CardImporter)}] 카드 {cardId}: 그림 {path} 가 없다");
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static CardEffect CreateEffect(EffectEntry entry, int cardId)
        {
            Type type = typeof(CardEffect).Assembly.GetTypes().FirstOrDefault(t => t.Name == entry.type && typeof(CardEffect).IsAssignableFrom(t) && !t.IsAbstract);
            if (type == null)
            {
                Debug.LogError($"[{nameof(CardImporter)}] 카드 {cardId}: 효과 {entry.type} 가 없다");
                return null;
            }

            var effect = (CardEffect)Activator.CreateInstance(type);
            if (string.IsNullOrWhiteSpace(entry.args))
                return effect;

            foreach (string pair in entry.args.Split(','))
            {
                string[] kv = pair.Split('=');
                FieldInfo field = type.GetField(kv[0].Trim(), BindingFlags.Public | BindingFlags.Instance);
                if (field == null || kv.Length != 2)
                    throw new ArgumentException($"카드 {cardId}: {entry.type} 에 「{pair.Trim()}」 를 넣을 수 없다");

                string raw = kv[1].Trim();
                object value = field.FieldType.IsEnum ? Enum.Parse(field.FieldType, raw) : Convert.ChangeType(raw, field.FieldType);
                field.SetValue(effect, value);
            }
            return effect;
        }
    }
}
