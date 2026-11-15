using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 켜져 있는 동안 크기가 1 ↔ scale 배로 오간다 (InOutSine 왕복). 꺼지면 1 배로 돌아간다.
    /// 대상으로 고를 수 있는 좌석의 발밑 빨간 링 (10-ui.md 2-1 「좌석」, 시안 .ring ringp 0.8초 · 1.08 배).
    /// </summary>
    public class PulseScale : MonoBehaviour
    {
        [Tooltip("가장 커졌을 때 배율.")]
        [SerializeField] private float scale = 1.08f;
        [Tooltip("1 배에서 가장 커질 때까지 초 (한 번 오가는 데 두 배).")]
        [SerializeField] private float halfPeriod = 0.8f;

        private Tween _pulse;

        private void OnEnable()
        {
            transform.localScale = Vector3.one;
            _pulse = transform.DOScale(scale, halfPeriod).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        private void OnDisable()
        {
            _pulse?.Kill();
            _pulse = null;
            transform.localScale = Vector3.one;
        }
    }
}
