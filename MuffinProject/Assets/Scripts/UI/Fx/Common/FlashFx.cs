using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 번쩍 — 화면을 덮는 그림이 그림 색으로 번쩍였다가 딱 꺼진다 (흐려지며 사라지면 회색 막이 남는다). 색은 이 오브젝트의 Image 색.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class FlashFx : CardFxStep
    {
        [Tooltip("번쩍일 때 투명도 — 반투명이면 회색 막처럼 보여 1 로 짧게 쓴다.")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 1f;
        [Tooltip("켜 두는 시간 (ms) — 지나면 딱 꺼진다.")]
        [SerializeField] private float durationMs = 60f;

        private Image _image;

        public override float DurationMs => durationMs;

        private Image Image => _image != null ? _image : _image = GetComponent<Image>();

        public override void ResetState()
        {
            base.ResetState();
            SetAlpha(Image, 0f);
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            Flash(Image, alpha, durationMs);
        }
    }
}
