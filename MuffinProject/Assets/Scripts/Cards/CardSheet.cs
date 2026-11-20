using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Chapchu.Game;
using Chapchu.Game.Effects;
using UnityEngine;

namespace Chapchu.Cards
{
    /// <summary>
    /// 카드 데이터 파일(cards.json) 한 벌 — 읽기 · 확인값 · 카드 정의에 얹기. 기준: docs/systems/26-card-data.md 7-2 · 7-3
    /// 빌드 기본본(「카드 가져오기」)과 원격 패치(CardDataService)가 같은 파일을 같은 코드로 얹는다 — 값의 원본은 이 파일 하나다.
    /// 파일이 바꿀 수 있는 것: 이름 · 문구 · 대상 · 대상 후보 조건 · 등급 · 장수 · 효과 값. 카드 종류 · 효과의 종류와 순서는 빌드가 정한다.
    /// </summary>
    [Serializable]
    public class CardSheet
    {
        public CardSheetEntry[] cards;

        // 문구의 값 자리 — {0} {1} … (26 7-2 「값 자리」)
        private static readonly Regex SlotPattern = new Regex(@"\{(\d+)\}");

        /// <summary>
        /// 파일 내용의 확인값 (SHA-256, 소문자 16진수). 줄바꿈(CRLF · LF)과 BOM 은 빼고 센다 —
        /// 같은 파일이 저장소 줄바꿈 정책 · 받는 길에 따라 다른 값이 되지 않게.
        /// </summary>
        public static string HashOf(string json)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Normalize(json));
            using (SHA256 sha = SHA256.Create())
            {
                var builder = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(bytes))
                    builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }

        /// <summary>파일을 읽는다. 모양이 틀리면 false 와 이유.</summary>
        public static bool TryParse(string json, out CardSheet sheet, out string error)
        {
            sheet = null;
            error = null;
            try
            {
                sheet = JsonUtility.FromJson<CardSheet>(Normalize(json));
            }
            catch (ArgumentException exception)
            {
                error = $"JSON 이 아니다: {exception.Message}";
                return false;
            }

            if (sheet?.cards == null || sheet.cards.Length == 0)
            {
                error = "카드가 없다";
                return false;
            }

            var ids = new HashSet<int>();
            int total = 0;
            foreach (CardSheetEntry entry in sheet.cards)
            {
                if (!ids.Add(entry.id))
                {
                    error = $"카드 {entry.id} 가 두 번 나온다";
                    return false;
                }

                if (entry.count < 0)
                {
                    error = $"카드 {entry.id}: 장수 {entry.count}";
                    return false;
                }

                total += entry.count;
            }

            if (total == 0)
            {
                error = "덱이 0장이다";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 파일의 카드 한 줄을 카드 정의에 얹는다. 종류 · 효과의 종류와 순서가 정의와 다르면 얹지 않고 false —
        /// 그런 변경은 앱 업데이트로만 낸다 (26 3절 #9). 실패하면 data 가 반쯤 바뀌어 있을 수 있다 — 사본에 얹고, 실패하면 사본을 버린다.
        /// </summary>
        public static bool TryApply(CardSheetEntry entry, CardData data, out string error)
        {
            if (!Enum.TryParse(entry.type, out CardType type) || type != data.type)
                return Fail(entry, $"종류 「{entry.type}」 — 빌드는 {data.type}", out error);

            if (!Enum.TryParse(entry.target, out TargetType target))
                return Fail(entry, $"대상 「{entry.target}」 를 모른다", out error);

            TargetCondition condition = TargetCondition.None;
            if (!string.IsNullOrEmpty(entry.targetCondition) && !Enum.TryParse(entry.targetCondition, out condition))
                return Fail(entry, $"대상 후보 조건 「{entry.targetCondition}」 를 모른다", out error);

            if (!Enum.TryParse(entry.grade, out CardGrade grade))
                return Fail(entry, $"등급 「{entry.grade}」 를 모른다", out error);

            if (string.IsNullOrEmpty(entry.name))
                return Fail(entry, "이름이 비었다", out error);

            int effectCount = entry.effects?.Length ?? 0;
            if (effectCount != data.effects.Count)
                return Fail(entry, $"효과 {effectCount}개 — 빌드는 {data.effects.Count}개", out error);

            var values = new List<string>();
            for (int i = 0; i < effectCount; i++)
            {
                CardEffect effect = data.effects[i];
                if (effect == null || effect.GetType().Name != entry.effects[i].type)
                    return Fail(entry, $"효과 {i}번 「{entry.effects[i].type}」 — 빌드는 {(effect == null ? "빈 칸" : effect.GetType().Name)}", out error);

                if (!TrySetArgs(effect, entry.effects[i].args, values, out string reason))
                    return Fail(entry, $"효과 {i}번 {reason}", out error);
            }

            if (!TryRenderText(entry.text, values, out string text, out string textReason))
                return Fail(entry, textReason, out error);

            data.cardName = entry.name;
            data.targetType = target;
            data.targetCondition = condition;
            data.grade = grade;
            data.description = text;
            error = null;
            return true;
        }

        /// <summary>
        /// 문구의 값 자리({0} {1} …)를 효과 값으로 채운다. 값 = 그 카드의 효과 args 에 적힌 숫자를 적힌 순서대로 (26 7-2).
        /// 값 자리가 없는 문구는 그대로 나온다.
        /// </summary>
        public static bool TryRenderText(string template, IReadOnlyList<string> values, out string text, out string error)
        {
            string failed = null;
            text = SlotPattern.Replace(template ?? "", match =>
            {
                int index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                if (index < values.Count)
                    return values[index];

                failed = $"문구의 값 자리 {match.Value} — 효과 값은 {values.Count}개";
                return match.Value;
            });
            error = failed;
            return failed == null;
        }

        // "필드=값, 필드=값" 을 효과의 public 필드에 넣는다. 숫자 값은 적힌 순서대로 values 에 모은다 (문구의 값 자리가 쓴다).
        private static bool TrySetArgs(CardEffect effect, string args, List<string> values, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(args))
                return true;

            foreach (string pair in args.Split(','))
            {
                string[] parts = pair.Split('=');
                FieldInfo field = parts.Length == 2 ? effect.GetType().GetField(parts[0].Trim(), BindingFlags.Public | BindingFlags.Instance) : null;
                if (field == null)
                {
                    error = $"{effect.GetType().Name} 에 「{pair.Trim()}」 를 넣을 수 없다";
                    return false;
                }

                string raw = parts[1].Trim();
                if (!TryConvert(raw, field.FieldType, out object value, out bool isNumber))
                {
                    error = $"{effect.GetType().Name}.{field.Name} 에 「{raw}」 를 넣을 수 없다";
                    return false;
                }

                field.SetValue(effect, value);
                if (isNumber)
                    values.Add(raw);
            }

            return true;
        }

        // 음수는 받지 않는다 — 피해 · 회복 · 장수 · 초 어느 것도 음수가 뜻이 없다
        private static bool TryConvert(string raw, Type type, out object value, out bool isNumber)
        {
            value = null;
            isNumber = false;
            if (type == typeof(int))
            {
                isNumber = true;
                bool parsed = int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out int number);
                value = number;
                return parsed;
            }

            if (type == typeof(float))
            {
                isNumber = true;
                bool parsed = float.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float number);
                value = number;
                return parsed;
            }

            if (type == typeof(bool))
            {
                bool parsed = bool.TryParse(raw, out bool flag);
                value = flag;
                return parsed;
            }

            if (type.IsEnum && Enum.IsDefined(type, raw))
            {
                value = Enum.Parse(type, raw);
                return true;
            }

            return false;
        }

        private static string Normalize(string json)
        {
            if (string.IsNullOrEmpty(json))
                return "";

            return json.TrimStart('﻿').Replace("\r", "");
        }

        private static bool Fail(CardSheetEntry entry, string reason, out string error)
        {
            error = $"카드 {entry.id}: {reason}";
            return false;
        }
    }

    /// <summary>카드 데이터 파일의 카드 한 줄 — 노션 카드 DB 속성과 1:1 인 값 + 효과 값 (11-card-list.md 0절).</summary>
    [Serializable]
    public class CardSheetEntry
    {
        public int id;
        public string name;
        public string type;            // CardType 이름
        public string target;          // TargetType 이름
        public string targetCondition; // TargetCondition 이름, 비우면 None
        public string grade;           // CardGrade 이름
        public int count;              // 덱에 들어가는 장수. 0 이면 덱에서 빠진다
        public string text;            // 카드 문구. 수치는 값 자리({0})로 쓴다
        public CardSheetEffect[] effects;
    }

    /// <summary>효과 하나 — type 은 CardEffect 를 상속한 클래스 이름, args 는 "필드=값, 필드=값" (예: "subject=Target, amount=20").</summary>
    [Serializable]
    public class CardSheetEffect
    {
        public string type;
        public string args;
    }
}
