using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Chapchu.Cards;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Chapchu.EditorTools
{
    /// <summary>
    /// 카드 패치하기 (11-card-list.md 0절 · 26-card-data.md 7-2 · 7-3) — cards.json 의 카드마다 Card_{ID}.asset 을 만들거나 고치고, 장수대로 Deck_Main 을 다시 짠다.
    /// 이름 · 종류 · 대상 · 대상 후보 조건 · 문구 · 등급 · 장수 · 효과 값을 매번 파일 값으로 덮는다 — 값의 원본은 cards.json 이다.
    /// 효과의 종류와 순서는 에셋이 정한다: 에셋에 효과가 하나도 없을 때만 파일의 종류대로 처음 만들고, 그 뒤로 파일과 다르면 그 카드는 실패로 알린다.
    /// 다 맞으면 Deck_Main 에 파일의 확인값을 적는다 — 파일이 바뀌었는데 아직 패치하지 않았는지를 이 값으로 가른다 (IsApplied).
    /// cards.json 에는 구현할 카드(노션 확정 + 효과가 만들어진 카드)만 적는다.
    /// </summary>
    public static class CardImporter
    {
        private const string Folder = "Assets/Cards/Main";
        private const string JsonPath = Folder + "/cards.json";
        private const string DeckPath = Folder + "/Deck_Main.asset";
        private const string ArtFolder = "Assets/Resources/CardArt"; // ResourcePaths.CardArt

        public const string MenuPath = "Chapchu/카드 패치하기";

        /// <summary>
        /// cards.json 이 카드 에셋에 반영돼 있는지 — Deck_Main 에 적힌 확인값이 파일의 확인값과 같은가. 파일이 없으면 반영할 것이 없다.
        /// 에셋을 손으로 고친 것은 잡지 못한다 — 다음 패치가 되돌린다.
        /// </summary>
        public static bool IsApplied()
        {
            if (!File.Exists(JsonPath))
                return true;

            DeckData deck = AssetDatabase.LoadAssetAtPath<DeckData>(DeckPath);
            return deck != null && deck.dataHash == CardSheet.HashOf(File.ReadAllText(JsonPath));
        }

        /// <summary>path 가 카드 데이터 파일(cards.json)인지 — 파일이 새로 들어오면 패치 알림 창이 확인한다.</summary>
        public static bool IsCardSheet(string path) => path == JsonPath;

        /// <summary>지난 패치가 카드를 다 얹지 못했는지 — 그때 Deck_Main 의 확인값을 비워 둔다 (26-card-data.md 8절).</summary>
        public static bool HasFailed()
        {
            DeckData deck = AssetDatabase.LoadAssetAtPath<DeckData>(DeckPath);
            return deck != null && string.IsNullOrEmpty(deck.dataHash);
        }

        [MenuItem(MenuPath)]
        public static void Import()
        {
            // 플레이 중에는 고치지 않는다 — 게임이 읽고 있는 카드 에셋이 실행 중에 바뀐다 (26-card-data.md 5절). 창 버튼 · 메뉴가 여기서 같이 막힌다
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning($"[{nameof(CardImporter)}] 플레이 중에는 패치하지 않는다 — 플레이를 끝낸 뒤 다시 누르기");
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

                    if (!CardSheet.TryApply(entry, copy, out error))
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
            // 하나라도 못 얹었으면 에셋이 파일과 다르다 — 확인값을 비워, 패치가 끝난 척하지 않게
            deck.dataHash = failed == 0 ? CardSheet.HashOf(json) : "";
            EditorUtility.SetDirty(deck);

            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(CardImporter)}] 카드 {sheet.cards.Length}종 (새로 {created} · 바뀜 {changed} · 실패 {failed}) · Deck_Main {entries.Sum(e => e.count)}장 · 확인값 {deck.dataHash}");
            CardPatchWindow.RefreshOpen(); // 메뉴로 패치해도 열려 있는 창이 옛 상태로 남지 않게
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

    /// <summary>
    /// cards.json 을 카드 에셋에 반영하지 않은 채로는 빌드하지 않는다 — 카드 패치가 빠진 빌드가 나가지 않게 (26-card-data.md 7-3).
    /// 스스로 패치하지는 않는다: 바뀐 에셋을 사람이 보고 커밋해야 저장소와 빌드가 같아진다.
    /// </summary>
    public class CardPatchBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!CardImporter.IsApplied())
                throw new BuildFailedException($"cards.json 이 카드 에셋에 반영되지 않았다 — Unity 메뉴 {CardImporter.MenuPath} 를 누르고, 바뀐 카드 에셋 · Deck_Main 을 커밋한 뒤 다시 빌드한다 (26-card-data.md 7-3)");
        }
    }
}
