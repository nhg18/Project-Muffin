using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개판 오분전 시작 제목 (프로토타입 v2 개판 컷인의 .gp-title · .gp-five — 1021). 큰 스티커 글자 두 줄이 하나씩 튀어나오고 「N 바퀴 동안」 발바닥 도장이 찍힌다.
    /// 컷인 장면(눈밭을 달리는 강아지 떼)은 넣지 않는다 — 제목 · 도장만 (사용자 2026-11-13). 시간표는 시안에서 장면이 먼저 나오던 1.35초를 당겼다.
    /// 쓴 카드의 후처리 시간(Card_1021 resolveSeconds 7.16초 = 1.28 + 3 단계 3.08 + 이 제목 2.8)이 이 길이를 담는다. 길이를 바꾸면 그 값도 같이 바꾼다.
    /// </summary>
    public class GaepanTitleView : MonoBehaviour
    {
        private const string FirstLine = "개판";
        private const string SecondLine = "오분전!";

        [SerializeField] private CanvasGroup group;
        [SerializeField] private StickerWordView firstLine;
        [SerializeField] private StickerWordView secondLine;
        [Tooltip("발바닥 도장 (시안 .gp-five) — 12° 기울어 있다.")]
        [SerializeField] private RectTransform stamp;
        [Tooltip("도장의 발가락 콩 4개 — 하나씩 튀어나온다.")]
        [SerializeField] private RectTransform[] beans;
        [SerializeField] private TMP_Text roundsText;

        [Header("시간표 (초, 시작부터)")]
        [Tooltip("첫 줄 「개판」 이 튀어나오기 시작하는 때 (시안 1.45초 − 1.35).")]
        [SerializeField] private float firstDelay = 0.1f;
        [Tooltip("둘째 줄 「오분전!」 이 튀어나오기 시작하는 때 (시안 1.75초 − 1.35).")]
        [SerializeField] private float secondDelay = 0.4f;
        [Tooltip("발바닥 도장이 찍히기 시작하는 때 (시안 2.15초 − 1.35).")]
        [SerializeField] private float stampDelay = 0.8f;
        [Tooltip("도장이 찍히는 데 걸리는 초 (시안 gpStamp 0.45초) — 앞 60% 내려앉기 · 뒤 40% 자리 잡기.")]
        [SerializeField] private float stampSeconds = 0.45f;
        [Tooltip("첫 발가락 콩이 튀어나오기 시작하는 때 (시안 2.3초 − 1.35).")]
        [SerializeField] private float beanDelay = 0.95f;
        [Tooltip("발가락 콩 사이 간격 초.")]
        [SerializeField] private float beanStagger = 0.06f;
        [Tooltip("발가락 콩 하나가 튀어나오는 데 걸리는 초.")]
        [SerializeField] private float beanSeconds = 0.35f;
        [Tooltip("시작부터 흐려지기 시작할 때까지 기다리는 초.")]
        [SerializeField] private float holdUntil = 2.5f;
        [Tooltip("제목 전체가 흐려져 사라지는 초.")]
        [SerializeField] private float fadeSeconds = 0.3f;

        [Header("도장 움직임")]
        [Tooltip("도장이 내려오기 시작할 때 크기 배율.")]
        [SerializeField] private float stampStartScale = 2.4f;
        [Tooltip("도장이 내려오기 시작할 때 Z 회전 (도). 시계 방향 기울기가 음수.")]
        [SerializeField] private float stampStartAngle = -40f;
        [Tooltip("내려앉은 순간 살짝 눌린 크기 배율.")]
        [SerializeField] private float stampPressScale = 0.88f;
        [Tooltip("내려앉은 순간 Z 회전 (도).")]
        [SerializeField] private float stampPressAngle = -6f;
        [Tooltip("자리 잡은 뒤 Z 회전 (도) — 씬의 도장 기울기 12° 와 같게.")]
        [SerializeField] private float stampRestAngle = -12f;
        [Tooltip("발가락 콩이 튀어나올 때 넘쳤다 돌아오는 정도 (Ease.OutBack overshoot).")]
        [SerializeField] private float beanOvershoot = 2.5f;

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
            firstLine.Set(new[] { (FirstLine, StickerTone.Fur) }, 0, firstDelay);
            secondLine.Set(new[] { (SecondLine, StickerTone.Pink) }, 2, secondDelay);

            // 시안 gpStamp (0.45초): 40° · 2.4배 · 투명 → 60% 에 6° · 0.88배 → 12° · 1배
            stamp.DOKill();
            var stampGroup = stamp.GetComponent<CanvasGroup>();
            stampGroup.alpha = 0f;
            stamp.localScale = Vector3.one * stampStartScale;
            stamp.localRotation = Quaternion.Euler(0f, 0f, stampStartAngle);
            DOTween.Sequence().SetTarget(stamp).SetDelay(stampDelay)
                .Append(stampGroup.DOFade(1f, stampSeconds * 0.6f))
                .Join(stamp.DOScale(stampPressScale, stampSeconds * 0.6f).SetEase(Ease.OutQuad))
                .Join(stamp.DOLocalRotate(new Vector3(0f, 0f, stampPressAngle), stampSeconds * 0.6f).SetEase(Ease.OutQuad))
                .Append(stamp.DOScale(1f, stampSeconds * 0.4f).SetEase(Ease.InOutQuad))
                .Join(stamp.DOLocalRotate(new Vector3(0f, 0f, stampRestAngle), stampSeconds * 0.4f).SetEase(Ease.InOutQuad));

            for (int i = 0; i < beans.Length; i++)
            {
                RectTransform bean = beans[i];
                bean.DOKill();
                bean.localScale = Vector3.zero;
                bean.DOScale(1f, beanSeconds).SetDelay(beanDelay + i * beanStagger).SetEase(Ease.OutBack, beanOvershoot);
            }

            yield return new WaitForSeconds(holdUntil);
            yield return group.DOFade(0f, fadeSeconds).WaitForCompletion();
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
