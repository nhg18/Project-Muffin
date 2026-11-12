using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI
{
    /// <summary>
    /// 시안 v2 키트의 작은 손맛 — 튀어나오기(fxPop) · 흔들기(fxShake). 시간 · 모양은 시안 값 그대로, 거리는 캔버스 px (시안 × 2).
    /// 기준: 「통합 화면 UI v2」 키트 JS
    /// </summary>
    public static class UIJuice
    {
        private const float PopSeconds = 0.24f;
        private const float PopPeak = 1.08f;
        private const float PopPeakAt = 0.55f;
        private const int ShakeSteps = 8;

        /// <summary>from 배율에서 1.08 로 커졌다가 1 로 (0.24초, ease-out).</summary>
        public static void Pop(RectTransform target, float from = 0.88f)
        {
            target.DOKill(true);
            target.localScale = Vector3.one * from;
            DOTween.Sequence()
                .SetTarget(target)
                .SetUpdate(true)
                .Append(target.DOScale(PopPeak, PopSeconds * PopPeakAt).SetEase(Ease.OutQuad))
                .Append(target.DOScale(1f, PopSeconds * (1f - PopPeakAt)).SetEase(Ease.OutQuad));
        }

        /// <summary>좌우로 줄어드는 흔들림 9단계 (위아래는 0.3배). amplitude 는 캔버스 px.</summary>
        public static void Shake(RectTransform target, float amplitude = 14f, float seconds = 0.38f)
        {
            target.DOKill(true);
            Vector2 origin = target.anchoredPosition;
            Sequence sequence = DOTween.Sequence().SetTarget(target).SetUpdate(true);
            for (int i = 0; i <= ShakeSteps; i++)
            {
                float d = amplitude * (1f - (float)i / ShakeSteps);
                var offset = new Vector2(i % 2 == 1 ? -d : d, -((i % 3) - 1) * d * 0.3f);
                if (i == ShakeSteps)
                    offset = Vector2.zero;

                if (i == 0)
                    target.anchoredPosition = origin + offset;
                else
                    sequence.Append(target.DOAnchorPos(origin + offset, seconds / ShakeSteps).SetEase(Ease.Linear));
            }
        }
    }
}
