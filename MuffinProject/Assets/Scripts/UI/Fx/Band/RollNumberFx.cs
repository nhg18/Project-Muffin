using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Chapchu.Cards;
using Chapchu.Game.Effects;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 4 단계 장식 「몇 장?」 — 쓴 사람 위 말풍선에서 숫자가 굴러가다 뽑은 장수에서 멈춘다 (시안 rollNum — 상상도 못한 정체).
    /// 장수는 부르는 쪽의 뽑기 알림(<see cref="CardFxContext.OnCardsDrawn"/>)으로 받는다. 다 구를 때까지 알림이 없으면 시안처럼 범위 안에서 아무 수나.
    /// 범위는 카드 데이터의 뽑기 효과(최소 count ~ 최대 randomMax)에서 읽는다 — 규칙 수치를 연출 값으로 따로 두지 않는다.
    /// 카드가 손패로 날아가는 것은 게임 화면 몫이라 여기에는 없다.
    /// </summary>
    public class RollNumberFx : CardFxStep
    {
        private const string RollingFormat = "{0}장?";
        private const string LandedFormat = "{0}장!";

        [Header("부품")]
        [Tooltip("말풍선 (.vw-bub) — 가운데가 기준점.")]
        [SerializeField] private RectTransform bubble;
        [SerializeField] private CanvasGroup bubbleGroup;
        [SerializeField] private TMP_Text label;

        [Header("모양 (시안 px)")]
        [Tooltip("말풍선 크기 = 글자 + 이만큼 (좌우 · 위아래 합 — 여백 14 · 4 + 테 3).")]
        [SerializeField] private Vector2 padding = new Vector2(34f, 14f);
        [Tooltip("쓴 사람 자리 가운데에서 말풍선 가운데까지 위로.")]
        [SerializeField] private float rise = 84f;

        [Header("시간 (ms)")]
        [Tooltip("숫자가 바뀌는 횟수.")]
        [Min(1)]
        [SerializeField] private int ticks = 11;
        [Tooltip("첫 숫자가 머무는 시간.")]
        [SerializeField] private float tickMs = 60f;
        [Tooltip("숫자가 바뀔 때마다 머무는 시간이 늘어나는 만큼.")]
        [SerializeField] private float tickGrowMs = 14f;
        [Tooltip("멈춘 숫자가 커졌다(배율) 돌아오는 시간.")]
        [SerializeField] private float landMs = 260f;
        [Tooltip("멈춘 숫자가 커지는 배율 — 여기서 1 로 돌아온다 (시안 scale(1.5)).")]
        [SerializeField] private float landScale = 1.5f;
        [Tooltip("멈춘 뒤 말풍선이 남아 있는 시간 (시안 450 + 900).")]
        [SerializeField] private float holdMs = 1350f;

        [Header("숫자")]
        [Tooltip("뽑을 장수를 읽을 카드 데이터 (상상도 못한 정체) — 구를 때 · 알림이 없을 때 고르는 범위는 이 카드 뽑기 효과의 최소 ~ 최대 장수.")]
        [SerializeField] private CardData card;

        private Vector2Int _range;

        public override float DurationMs => ticks * tickMs + tickGrowMs * ticks * (ticks - 1) / 2f + holdMs;

        public override RectTransform Visual => bubble;

        private void Awake()
        {
            var missing = new List<string>();
            if (bubble == null)
                missing.Add(nameof(bubble));
            if (bubbleGroup == null)
                missing.Add(nameof(bubbleGroup));
            if (label == null)
                missing.Add(nameof(label));
            if (!TryReadRange(out _range))
                missing.Add(nameof(card) + " (뽑기 효과)");
            if (missing.Count > 0)
                Debug.LogError($"[{nameof(RollNumberFx)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            bubble.DOKill();
            bubbleGroup.alpha = 0f;
            bubble.localScale = Vector3.one;
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            StartRoutine(RollRoutine(context));
        }

        private IEnumerator RollRoutine(CardFxContext context)
        {
            CardFxSeat seat = context.CasterSeat;
            int drawn = 0;
            Action<CardFxSeat, int> handleCardsDrawn = (who, count) =>
            {
                if (who == seat)
                    drawn = count;
            };
            context.OnCardsDrawn += handleCardsDrawn;
            try
            {
                float pixelScale = context.Layer != null ? context.Layer.PixelScale : 1f;
                if (seat != null && seat.Anchor != null)
                    bubble.anchoredPosition = PositionOf(seat.Anchor) + new Vector2(0f, rise * pixelScale);
                bubbleGroup.alpha = 1f;

                float start = Time.time;
                float at = 0f;
                for (int i = 0; i < ticks; i++)
                {
                    Show(RollingFormat, Random.Range(_range.x, _range.y + 1), pixelScale);
                    at += tickMs + i * tickGrowMs;
                    yield return WaitUntilMs(start, at);
                }

                Show(LandedFormat, drawn > 0 ? drawn : Random.Range(_range.x, _range.y + 1), pixelScale);
                bubble.localScale = Vector3.one * landScale;
                bubble.DOScale(1f, Seconds(landMs)).SetEase(Ease.Linear).SetLink(gameObject);
                yield return WaitUntilMs(start, at + holdMs);
                bubbleGroup.alpha = 0f;
            }
            finally
            {
                context.OnCardsDrawn -= handleCardsDrawn;
            }
        }

        // 카드 뽑기 효과의 장수 범위 (최소, 최대). 무작위 장수가 아니면 최소 = 최대
        private bool TryReadRange(out Vector2Int range)
        {
            range = Vector2Int.zero;
            if (card == null)
                return false;
            foreach (CardEffect effect in card.effects)
            {
                if (effect is DrawEffect draw && !draw.useTriggerAmount)
                {
                    range = new Vector2Int(draw.count, Mathf.Max(draw.count, draw.randomMax));
                    return true;
                }
            }
            return false;
        }

        // 글자를 바꾸고 말풍선을 글자에 맞춘다
        private void Show(string format, int count, float pixelScale)
        {
            label.text = string.Format(format, count);
            bubble.sizeDelta = label.GetPreferredValues(label.text) + padding * pixelScale;
        }

        // 자리 가운데를 말풍선 부모 좌표(가운데 기준점)로
        private Vector2 PositionOf(RectTransform anchor)
        {
            var parent = (RectTransform)bubble.parent;
            Vector2 local = parent.InverseTransformPoint(anchor.TransformPoint(anchor.rect.center));
            return local - Rect.NormalizedToPoint(parent.rect, bubble.anchorMin);
        }
    }
}
