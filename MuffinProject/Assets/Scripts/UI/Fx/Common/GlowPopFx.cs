using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 빛 번짐 — 한 점에서 빛(번짐 + 가로 빛줄)이 커지며 켜진다 (지옥참마도: 눈빛). 이 오브젝트의 CanvasGroup 째 켠다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GlowPopFx : CardFxStep
    {
        [Tooltip("켜지는 시간 (ms).")]
        [SerializeField] private float glowMs = 200f;
        [Tooltip("켜질 때 투명도.")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 1f;
        [SerializeField] private float fromScale = 0.3f;
        [SerializeField] private float toScale = 1.2f;

        private CanvasGroup _glow;

        public override float DurationMs => glowMs;

        private CanvasGroup Glow => _glow != null ? _glow : _glow = GetComponent<CanvasGroup>();

        public override void ResetState()
        {
            base.ResetState();
            Glow.DOKill();
            Glow.transform.DOKill();
            Glow.alpha = 0f;
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            Glow.transform.localScale = Vector3.one * fromScale;
            Glow.DOFade(alpha, Seconds(glowMs)).SetLink(gameObject);
            Glow.transform.DOScale(toScale, Seconds(glowMs)).SetLink(gameObject);
        }
    }
}
