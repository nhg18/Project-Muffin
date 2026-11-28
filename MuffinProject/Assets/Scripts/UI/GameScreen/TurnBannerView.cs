using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 턴 띠 (10-ui.md 5-2 「내 턴 띠」, 프로토타입 v2 .turnban) — 내 턴이 오면 노란 띠 「내 턴!」 이 왼쪽에서 들어와 잠깐 머물고 오른쪽으로 나간다.
    /// 화면을 막지 않는다(누름을 받지 않는다). 판정과 상관없는 연출 — 연출 큐도 기다리지 않는다.
    /// </summary>
    public class TurnBannerView : MonoBehaviour
    {
        [Tooltip("띠 — 캔버스 폭에 맞춰 가로로 늘인 것. 꺼 둔 채로 저장한다.")]
        [SerializeField] private RectTransform band;
        [Tooltip("띠 글 — 「내 턴!」.")]
        [SerializeField] private TMP_Text label;

        [Header("움직임 (시안 v2 .turnban — 1.05초)")]
        [SerializeField] private float seconds = 1.05f;
        [Tooltip("들어와서 지나치는 거리 (px) — 시안 16 × 2.")]
        [SerializeField] private float overshoot = 32f;

        private void Awake()
        {
            if (band == null || label == null)
                Debug.LogError($"[{nameof(TurnBannerView)}] {nameof(band)} · {nameof(label)} 참조 중 비어 있는 것이 있습니다.", this);

            band.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            band.DOKill();
            band.gameObject.SetActive(false);
        }

        /// <summary>띠를 한 번 지나가게 한다. 지나가는 중에 다시 부르면 처음부터.</summary>
        public void Play()
        {
            float width = ((RectTransform)band.parent).rect.width;
            float y = band.anchoredPosition.y;

            band.DOKill();
            band.anchoredPosition = new Vector2(-width, y);
            band.gameObject.SetActive(true);

            // 시안 키프레임: 0% −W → 22% +16 → 30% 0 → 70% 0 → 100% +W
            DOTween.Sequence().SetTarget(band)
                .Append(band.DOAnchorPosX(overshoot, seconds * 0.22f).SetEase(Ease.OutCubic))
                .Append(band.DOAnchorPosX(0f, seconds * 0.08f).SetEase(Ease.InOutQuad))
                .AppendInterval(seconds * 0.4f)
                .Append(band.DOAnchorPosX(width, seconds * 0.3f).SetEase(Ease.InCubic))
                .OnComplete(() => band.gameObject.SetActive(false));
        }
    }
}
