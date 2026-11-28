using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Chapchu.Game;
using UnityEngine;

namespace Chapchu.Cards
{
    /// <summary>
    /// 카드 키워드 한 줄 (11-card-list.md 5-1 사전). 글 안 링크 ID 가 Id 이고, 설명 창은 Name · Category · Meaning 을 보인다.
    /// 손패 + / − 는 동사마다 줄이 따로다 — 머리(Name)는 같고 뜻 줄만 다르다 (5-1 아래 줄).
    /// </summary>
    public sealed class CardKeyword
    {
        public CardKeyword(string id, string name, string category, Color32 color, string icon, string meaning)
        {
            Id = id;
            Name = name;
            Category = category;
            Color = color;
            Icon = icon;
            Meaning = meaning;
        }

        /// <summary>글 안 링크 ID (<c>&lt;link="Id"&gt;</c>).</summary>
        public string Id { get; }

        /// <summary>설명 창 머리의 키워드 이름 — 피해 · 손패 + · 금지 …</summary>
        public string Name { get; }

        /// <summary>분류 이름 — 피해 · 회복 · 손패 · 막기 · 턴 · 함정 · 판 뒤집기 · 시간.</summary>
        public string Category { get; }

        /// <summary>분류 색 — 글 · 설명 창 머리 바탕.</summary>
        public Color32 Color { get; }

        /// <summary>아이콘 이름 — 스프라이트 에셋 CardKeywords 안의 이름.</summary>
        public string Icon { get; }

        /// <summary>설명 창 뜻 줄.</summary>
        public string Meaning { get; }
    }

    /// <summary>
    /// 카드 문구의 「수치 + 키워드」를 찾아 분류 색 · 글 높이 아이콘을 입힌다 (11-card-list.md 5절 · 10-ui.md 7-1 「대표 키워드」).
    /// 노션 문구에는 표시 기호가 없다 — 5-1 사전의 「찾는 말」로 찾는다. 칠하는 것은 행동 카드만 (카운터 문구는 5절 미정 14).
    /// 줄은 띄어쓰기에서만 바꾼다 (시안 word-break: keep-all) — TMP 는 한글을 글자마다 끊으므로 낱말을 nobr 로 묶는다. 아이콘은 그 낱말 안에 넣어 글과 갈리지 않는다.
    /// 시안 「카드 키워드 v2」 https://claude.ai/artifact/W4a9GLX6YEGduhXSAcQatn 게시본 1795080331-a0b0.
    /// </summary>
    public static class CardKeywords
    {
        /// <summary>아이콘 스프라이트 에셋 이름 — TMP 가 Resources/Sprite Assets/ 에서 찾는다 (TMP Settings 「Default Sprite Asset Path」).</summary>
        public const string IconAssetName = "CardKeywords";

        private const string DamageCategory = "피해";
        private const string HandCategory = "손패";
        private const string StopCategory = "막기 · 턴";
        private const string FlipCategory = "판 뒤집기";

        private static readonly Color32 DamageColor = new Color32(0xe0, 0x30, 0x3c, 0xFF);
        private static readonly Color32 HealColor = new Color32(0x2a, 0x9a, 0x52, 0xFF);
        private static readonly Color32 HandColor = new Color32(0x2f, 0x63, 0xd6, 0xFF);
        private static readonly Color32 StopColor = new Color32(0x0e, 0x8f, 0x98, 0xFF);
        private static readonly Color32 TrapColor = new Color32(0xe2, 0x54, 0x0f, 0xFF);
        private static readonly Color32 FlipColor = new Color32(0x8b, 0x3f, 0xd4, 0xFF);
        private static readonly Color32 TimeColor = new Color32(0x6e, 0x64, 0x5c, 0xFF); // 시간은 색 없음 — 글자만 옅은 잉크

        private static readonly CardKeyword[] All =
        {
            new CardKeyword("damage", "피해", DamageCategory, DamageColor, "burst", "HP 를 그만큼 깎는다. 0 이 되면 사망 대기."),
            new CardKeyword("zero", "HP 0", DamageCategory, DamageColor, "skull", "HP 를 0 으로 정한다. 피해가 아니라 줄이거나 막을 수 없다 → 사망 대기."),
            new CardKeyword("heal", "회복", "회복", HealColor, "heal", "HP 를 그만큼 채운다. 100 을 넘지 않는다."),
            new CardKeyword("draw", "손패 +", HandCategory, HandColor, "gain", "덱(또는 버림 더미)에서 손패로 가져온다."),
            new CardKeyword("steal", "손패 +", HandCategory, HandColor, "gain", "상대 손패에서 무작위로 가져온다. 고를 수 없다."),
            new CardKeyword("give", "손패 −", HandCategory, HandColor, "lose", "손패에서 무작위로 넘긴다."),
            new CardKeyword("discard", "손패 −", HandCategory, HandColor, "lose", "손패를 버림 더미로 보낸다. 설치한 함정은 그대로."),
            new CardKeyword("ban", "금지", StopCategory, StopColor, "lock", "정해진 동안 그 일을 할 수 없다."),
            new CardKeyword("skip", "건너뛰기", StopCategory, StopColor, "skip", "그 사람의 턴이 오지 않고 다음 사람으로 넘어간다."),
            new CardKeyword("reverse", "반대로", StopCategory, StopColor, "uturn", "턴이 도는 방향을 반대로 바꾼다."),
            new CardKeyword("destroy", "파괴", "함정", TrapColor, "trapx", "설치된 함정을 발동 없이 버림 더미로 보낸다."),
            new CardKeyword("swap", "바꾸기", FlipCategory, FlipColor, "swap", "두 사람의 HP를 서로 바꾼다. 다쳐서 줄어드는 게 아니라서 막을 수 없다."),
            new CardKeyword("reset", "새 판", FlipCategory, FlipColor, "reset", "지금 방의 멤버로 게임을 처음부터 다시 시작한다."),
            new CardKeyword("time", "시간", "시간", TimeColor, "clock", "효과가 언제 일어나는지, 언제까지인지."),
        };

        // 5-1 「문구에서 찾는 말」 — 키워드 Id 와 짝. 겹치면 앞에서 시작하는 것, 같은 자리면 긴 것이 이긴다
        private static readonly (Regex pattern, string id)[] Finds =
        {
            (new Regex(@"\d+ 피해|점점 커지는 피해"), "damage"),
            (new Regex(@"HP 0"), "zero"),
            (new Regex(@"\d+ 회복"), "heal"),
            (new Regex(@"\d+(?:~\d+)?장(?:씩|을)? (?:뽑는다|가져온다)"), "draw"),
            (new Regex(@"\d+장(?:씩|을)? 빼앗(?:는다|고)"), "steal"),
            (new Regex(@"\d+장씩? 준다"), "give"),
            (new Regex(@"손패를? (?:모두|전부) 버(?:리고|리기|릴지)"), "discard"),
            (new Regex(@"못 (?:쓰게|이긴다)"), "ban"),
            (new Regex(@"\d+턴 건너뛴다"), "skip"),
            (new Regex(@"반대로"), "reverse"),
            (new Regex(@"파괴"), "destroy"),
            (new Regex(@"서로 바꾼다"), "swap"),
            (new Regex(@"새 판"), "reset"),
            (new Regex(@"다음 턴에|\d+바퀴 뒤|\d+바퀴 동안(?: 내 턴 시작마다)?|이후 턴마다|내 다음 턴까지"), "time"),
        };

        private static readonly Dictionary<string, CardKeyword> ById = BuildLookup();

        /// <summary>링크 ID 로 사전 줄을 찾는다.</summary>
        public static bool TryGet(string id, out CardKeyword keyword) => ById.TryGetValue(id ?? string.Empty, out keyword);

        /// <summary>
        /// 카드 문구를 TMP 글로 바꾼다 — 낱말마다 nobr, 행동 카드는 키워드에 링크 · 분류 색 · 앞 아이콘.
        /// paint 를 주면 키워드 색과 아이콘에 그 거르기를 입힌다 — 흐린 카드(CardView 의 흐림)가 키워드까지 흐리게.
        /// </summary>
        public static string Format(string description, CardType type, Func<Color, Color> paint = null)
        {
            if (string.IsNullOrEmpty(description)) return string.Empty;

            List<(int start, int end, CardKeyword keyword)> spans = type == CardType.Action ? FindSpans(description) : null;
            string iconTint = paint != null ? $" tint=1 color=#{ColorUtility.ToHtmlStringRGB(paint(UnityEngine.Color.white))}" : string.Empty;
            var builder = new StringBuilder(description.Length * 4);
            int next = 0; // 아직 지나지 않은 첫 키워드

            for (int i = 0; i < description.Length;)
            {
                if (char.IsWhiteSpace(description[i]))
                {
                    builder.Append(description[i]);
                    i++;
                    continue;
                }

                int wordEnd = i;
                while (wordEnd < description.Length && !char.IsWhiteSpace(description[wordEnd]))
                    wordEnd++;

                builder.Append("<nobr>");
                for (int p = i; p < wordEnd;)
                {
                    while (spans != null && next < spans.Count && spans[next].end <= p)
                        next++;

                    bool inKeyword = spans != null && next < spans.Count && spans[next].start <= p;
                    if (!inKeyword)
                    {
                        int plainEnd = spans != null && next < spans.Count ? Math.Min(wordEnd, spans[next].start) : wordEnd;
                        builder.Append(description, p, plainEnd - p);
                        p = plainEnd;
                        continue;
                    }

                    (int start, int end, CardKeyword keyword) span = spans[next];
                    Color color = span.keyword.Color;
                    if (paint != null)
                        color = paint(color);
                    int pieceEnd = Math.Min(wordEnd, span.end);
                    builder.Append("<link=\"").Append(span.keyword.Id).Append("\"><color=#").Append(ColorUtility.ToHtmlStringRGB(color)).Append('>');
                    if (p == span.start)
                        builder.Append("<sprite=\"").Append(IconAssetName).Append("\" name=\"").Append(span.keyword.Icon).Append('"').Append(iconTint).Append('>');
                    builder.Append(description, p, pieceEnd - p);
                    builder.Append("</color></link>");
                    p = pieceEnd;
                }

                builder.Append("</nobr>");
                i = wordEnd;
            }

            return builder.ToString();
        }

        /// <summary>문구에 칠할 키워드가 있는가 — 행동 카드만 칠한다.</summary>
        public static bool HasKeywords(string description, CardType type) =>
            type == CardType.Action && !string.IsNullOrEmpty(description) && FindSpans(description).Count > 0;

        private static List<(int start, int end, CardKeyword keyword)> FindSpans(string description)
        {
            var found = new List<(int start, int end, CardKeyword keyword)>();
            foreach ((Regex pattern, string id) in Finds)
            {
                CardKeyword keyword = ById[id];
                foreach (Match match in pattern.Matches(description))
                    found.Add((match.Index, match.Index + match.Length, keyword));
            }

            found.Sort((a, b) => a.start != b.start ? a.start.CompareTo(b.start) : b.end.CompareTo(a.end));
            var spans = new List<(int start, int end, CardKeyword keyword)>(found.Count);
            foreach ((int start, int end, CardKeyword keyword) span in found)
            {
                if (spans.Count > 0 && span.start < spans[spans.Count - 1].end) continue;

                spans.Add(span);
            }

            return spans;
        }

        private static Dictionary<string, CardKeyword> BuildLookup()
        {
            var lookup = new Dictionary<string, CardKeyword>(All.Length);
            foreach (CardKeyword keyword in All)
                lookup.Add(keyword.Id, keyword);
            return lookup;
        }
    }
}
