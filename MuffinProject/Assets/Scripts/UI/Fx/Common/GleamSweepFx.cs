using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 빛 훑기 — 빛이 켜지고, 그 위를 하얀 띠가 위에서 아래로 훑는다 (시안 sw-gleam, 지옥참마도: 칼날 빛).
    /// </summary>
    public class GleamSweepFx : CardFxStep
    {
        [Tooltip("칼날에 켜지는 붉은 빛.")]
        [SerializeField] private Image glow;
        [Tooltip("훑는 하얀 띠. glow 의 자식이어야 한다 — 그 높이만큼 움직인다.")]
        [SerializeField] private Image gleam;

        [Tooltip("켜질 때 붉은 빛 투명도.")]
        [Range(0f, 1f)]
        [SerializeField] private float glowAlpha = 1f;
        [Tooltip("훑는 시간 (ms). 앞 70% 동안 움직이고 사라진다.")]
        [SerializeField] private float sweepMs = 300f;
        [Tooltip("띠가 출발 · 도착하는 위치 (빛 높이 대비, 위 + · 아래 -).")]
        [SerializeField] private float sweepReach = 1.2f;

        public override float DurationMs => sweepMs;

        private void Awake()
        {
            if (glow == null || gleam == null)
                Debug.LogError($"[{nameof(GleamSweepFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            glow.DOKill();
            SetAlpha(glow, 0f);
            SetAlpha(gleam, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            SetAlpha(glow, glowAlpha);

            RectTransform rect = gleam.rectTransform;
            float height = ((RectTransform)rect.parent).rect.height;
            rect.anchoredPosition = new Vector2(0f, height * sweepReach);
            SetAlpha(gleam, 1f);
            DOTween.Sequence().SetLink(gameObject)
                .Append(rect.DOAnchorPosY(-height * sweepReach, Seconds(sweepMs * 0.7f)))
                .Append(gleam.DOFade(0f, 0f))
                .AppendInterval(Seconds(sweepMs * 0.3f));
        }
    }
}
