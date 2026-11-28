using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Chapchu.Core;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개발자 도구의 「5 단계 띠」 · 「실측 표」 (10-ui.md 12절, 프로토타입 v2) — 카드 연출이 지금 몇 단계인지와 단계마다 실제로 걸린 초를 잰다.
    /// 단계는 GameScreenPresenter 의 연출 큐가 알린다: 1 · 2 내기 · 확대 → 대기(반응 시간) → 3 발동 → 4 후처리(결과 · 버림 더미로). 다음 단계가 시작되면 앞 단계가 끝난다.
    /// 개발자 모드가 꺼져 있으면 아무것도 하지 않는다. 시간은 실제 초(속도 배수와 무관).
    /// </summary>
    public static class DevStageMeter
    {
        public const string Play = "1 · 2 내기 · 확대";
        public const string Wait = "대기 (반응 시간)";
        public const string Resolve = "3 발동";
        public const string After = "4 후처리";
        private static readonly string[] Order = { Play, Wait, Resolve, After };

        private sealed class Stat
        {
            public int Count;
            public float Total;
            public float Last;
            public float Max;
        }

        private static readonly Dictionary<string, Stat> Stats = new Dictionary<string, Stat>();
        private static string _current;
        private static float _since;

        /// <summary>단계가 바뀌었다 — 이름, 끝났으면 null.</summary>
        public static event Action<string> OnStageChanged;

        public static string Current => _current;

        /// <summary>지금 단계가 시작된 뒤 지난 실제 초.</summary>
        public static float Elapsed => _current != null ? Time.unscaledTime - _since : 0f;

        public static void Begin(string stage)
        {
            if (!DevMode.IsOn || _current == stage) return; // 같은 단계가 이어지면(3 발동 뒤 후처리 → 버림 더미로) 한 번으로 잰다

            Close();
            _current = stage;
            _since = Time.unscaledTime;
            OnStageChanged?.Invoke(stage);
        }

        public static void End()
        {
            if (_current == null) return;

            Close();
            OnStageChanged?.Invoke(null);
        }

        /// <summary>stage 단계로 routine 을 돌리고, 끝나면 then 단계로(없으면 끝). routine 이 null 이어도 단계는 알린다.</summary>
        public static IEnumerator Wrap(string stage, IEnumerator routine, string then = null)
        {
            Begin(stage);
            if (routine != null)
                yield return routine;
            if (then != null)
                Begin(then);
            else
                End();
        }

        public static void Reset()
        {
            Stats.Clear();
            _current = null;
            OnStageChanged?.Invoke(null);
        }

        /// <summary>실측 표 — 단계마다 한 줄: 횟수 · 마지막 · 평균 · 가장 긴 초.</summary>
        public static string Table()
        {
            var text = new StringBuilder();
            foreach (string stage in Order)
            {
                text.Append("<b>").Append(stage).Append("</b>  ");
                if (!Stats.TryGetValue(stage, out Stat stat))
                {
                    text.Append("아직 없음\n");
                    continue;
                }
                text.Append(stat.Count).Append("회 · 마지막 ").Append(stat.Last.ToString("0.00"))
                    .Append(" · 평균 ").Append((stat.Total / stat.Count).ToString("0.00"))
                    .Append(" · 최대 ").Append(stat.Max.ToString("0.00")).Append("초\n");
            }
            return text.ToString().TrimEnd('\n');
        }

        private static void Close()
        {
            if (_current == null) return;

            float seconds = Time.unscaledTime - _since;
            if (!Stats.TryGetValue(_current, out Stat stat))
                Stats[_current] = stat = new Stat();
            stat.Count++;
            stat.Total += seconds;
            stat.Last = seconds;
            stat.Max = Mathf.Max(stat.Max, seconds);
            _current = null;
        }
    }
}
