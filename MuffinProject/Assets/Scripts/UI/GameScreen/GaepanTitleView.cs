using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개판 오분전 시작 제목 (프로토타입 v2 개판 컷인의 .gp-title · .gp-five — 121). 큰 스티커 글자 두 줄이 하나씩 튀어나오고 「N 바퀴 동안」 발바닥 도장이 찍힌다.
    /// 컷인 장면(눈밭을 달리는 강아지 떼)은 넣지 않는다 — 제목 · 도장만 (사용자 2026-11-13). 시간표는 시안에서 장면이 먼저 나오던 1.35초를 당겼다.
    /// 쓴 카드의 후처리 시간(Card_121 resolveSeconds 3초) 안에 끝난다.
    /// </summary>
    public class GaepanTitleView : MonoBehaviour
    {
        private const string FirstLine = "개판";
        private const string SecondLine = "오분전!";
        private const float FirstDelay = 0.1f;   // 시안 1.45초 − 1.35
        private const float SecondDelay = 0.4f;  // 시안 1.75초 − 1.35
        private const float StampDelay = 0.8f;   // 시안 2.15초 − 1.35
        private const float StampSeconds = 0.45f;
        private const float BeanDelay = 0.95f;   // 시안 2.3초 − 1.35
        private const float BeanStagger = 0.06f;
        private const float BeanSeconds = 0.35f;
        private const float HoldUntil = 2.5f;
        private const float FadeSeconds = 0.3f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private StickerWordView firstLine;
        [SerializeField] private StickerWordView secondLine;
        [Tooltip("발바닥 도장 (시안 .gp-five) — 12° 기울어 있다.")]
        [SerializeField] private RectTransform stamp;
        [Tooltip("도장의 발가락 콩 4개 — 하나씩 튀어나온다.")]
        [SerializeField] private RectTransform[] beans;
        [SerializeField] private TMP_Text roundsText;

        private void Awake()
        {
            if (group == null || firstLine == null || secondLine == null || stamp == null || roundsText == null)
                Debug.LogError($"[{nameof(GaepanTitleView)}] 참조가 비어 있습니다.", this);
        }

        /// <summary>제목을 보이고 끝날 때까지 기다린다 (rounds = 「N 바퀴 동안」).</summary>
        public IEnumerator PlayRoutine(int rounds)
        {
            gameObject.SetActive(true);
            group.DOKill();
            group.alpha = 1f;
            roundsText.text = rounds.ToString();
            firstLine.Set(new[] { (FirstLine, StickerTone.Fur) }, 0, FirstDelay);
            secondLine.Set(new[] { (SecondLine, StickerTone.Pink) }, 2, SecondDelay);

            // 시안 gpStamp (0.45초): 40° · 2.4배 · 투명 → 60% 에 6° · 0.88배 → 12° · 1배
            stamp.DOKill();
            var stampGroup = stamp.GetComponent<CanvasGroup>();
            stampGroup.alpha = 0f;
            stamp.localScale = Vector3.one * 2.4f;
            stamp.localRotation = Quaternion.Euler(0f, 0f, -40f);
            DOTween.Sequence().SetTarget(stamp).SetDelay(StampDelay)
                .Append(stampGroup.DOFade(1f, StampSeconds * 0.6f))
                .Join(stamp.DOScale(0.88f, StampSeconds * 0.6f).SetEase(Ease.OutQuad))
                .Join(stamp.DOLocalRotate(new Vector3(0f, 0f, -6f), StampSeconds * 0.6f).SetEase(Ease.OutQuad))
                .Append(stamp.DOScale(1f, StampSeconds * 0.4f).SetEase(Ease.InOutQuad))
                .Join(stamp.DOLocalRotate(new Vector3(0f, 0f, -12f), StampSeconds * 0.4f).SetEase(Ease.InOutQuad));

            for (int i = 0; i < beans.Length; i++)
            {
                RectTransform bean = beans[i];
                bean.DOKill();
                bean.localScale = Vector3.zero;
                bean.DOScale(1f, BeanSeconds).SetDelay(BeanDelay + i * BeanStagger).SetEase(Ease.OutBack, 2.5f);
            }

            yield return new WaitForSeconds(HoldUntil);
            yield return group.DOFade(0f, FadeSeconds).WaitForCompletion();
            Hide();
        }

        public void Hide()
        {
            group.DOKill();
            stamp.DOKill();
            gameObject.SetActive(false);
        }
    }
}
