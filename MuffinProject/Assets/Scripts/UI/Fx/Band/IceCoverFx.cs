using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 4 단계 장식 「꽁꽁」 — 대상 자리에 얼음 덮개가 크게 나타나며 내려앉는다 (시안 ice · .vw-ice — 얼음!).
    /// 시안에서는 덮개가 자리에 남지만, 남는 상태 표시(턴 건너뜀)는 게임 화면 몫이라 이 덮개는 연출이 지워질 때 같이 사라진다.
    /// </summary>
    public class IceCoverFx : CardFxStep
    {
        [Header("부품")]
        [Tooltip("얼음 덮개 — 대상 자리 크기에 맞춘다. 가운데가 기준점.")]
        [SerializeField] private RectTransform cover;
        [SerializeField] private CanvasGroup coverGroup;

        [Header("모양 · 시간")]
        [Tooltip("자리 둘레 밖으로 나오는 거리 (시안 px — .vw-ice inset -6px 은 판 테 3px 안쪽 기준).")]
        [SerializeField] private float outset = 3f;
        [Tooltip("나타날 때 처음 배율.")]
        [SerializeField] private float fromScale = 1.3f;
        [Tooltip("나타나는 시간 (ms).")]
        [SerializeField] private float appearMs = 300f;
        [Tooltip("나타난 뒤 장식이 끝날 때까지 (ms) — 시안 s4 는 모두 400ms.")]
        [SerializeField] private float holdMs = 100f;

        public override float DurationMs => appearMs + holdMs;

        public override RectTransform Visual => cover;

        private void Awake()
        {
            if (cover == null || coverGroup == null)
                Debug.LogError($"[{nameof(IceCoverFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            cover.DOKill();
            coverGroup.DOKill();
            coverGroup.alpha = 0f;
            cover.localScale = Vector3.one * fromScale;
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            CardFxSeat seat = context.MainTarget;
            if (seat == null || seat.Anchor == null)
                return;

            float pixelScale = context.Layer != null ? context.Layer.PixelScale : 1f;
            Fit(seat.Anchor, outset * pixelScale);
            cover.localScale = Vector3.one * fromScale;
            coverGroup.alpha = 0f;
            cover.DOScale(1f, Seconds(appearMs)).SetEase(Ease.Linear).SetLink(gameObject);
            coverGroup.DOFade(1f, Seconds(appearMs)).SetEase(Ease.Linear).SetLink(gameObject);
        }

        // 덮개를 자리 둘레(부모 좌표) + 바깥 거리에 맞춘다
        private void Fit(RectTransform anchor, float grow)
        {
            var parent = (RectTransform)cover.parent;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            cover.sizeDelta = max - min + Vector2.one * grow * 2f;
            cover.anchoredPosition = (min + max) / 2f - Rect.NormalizedToPoint(parent.rect, cover.anchorMin);
        }
    }
}
