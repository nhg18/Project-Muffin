using UnityEngine;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 흔들기 — 부모의 <see cref="FxShaker"/> 판을 무작위로 흔든다 (시안 shakeC). 그림은 없다.
    /// </summary>
    public class ShakeFx : CardFxStep
    {
        [Tooltip("흔들림 세기 (px).")]
        [SerializeField] private float amount = 6f;
        [Tooltip("흔들리는 시간 (ms).")]
        [SerializeField] private float durationMs = 160f;

        private FxShaker _shaker;

        public override float DurationMs => durationMs;

        private FxShaker Shaker => _shaker != null ? _shaker : _shaker = GetComponentInParent<FxShaker>(true);

        public override void ResetState()
        {
            base.ResetState();
            if (Shaker != null)
                Shaker.ResetState();
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            if (Shaker == null)
            {
                Debug.LogWarning($"[{nameof(ShakeFx)}] 부모에 흔들 판(FxShaker)이 없어 흔들지 않습니다.", this);
                return;
            }
            Shaker.Shake(amount, durationMs);
        }
    }
}
