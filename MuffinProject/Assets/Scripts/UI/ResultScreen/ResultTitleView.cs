using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면 위쪽 (시안 「결과화면 UI v3」 .title-wrap) — 제목 「○○ 승리!」 · 승리 방식 알약 · 내 순위 알약.
    /// 문구는 ResultPresenter 가 만든다. 받아서 그리기만 한다.
    /// </summary>
    public class ResultTitleView : MonoBehaviour
    {
        [SerializeField] private RectTransform title;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text titleText;

        [Header("알약")]
        [SerializeField] private RectTransform pills;
        [SerializeField] private CanvasGroup pillsGroup;
        [Tooltip("승리 방식 — 노란 꼬리표 「찹츄 승리」.")]
        [SerializeField] private TMP_Text reasonLabelText;
        [Tooltip("승리 방식 설명.")]
        [SerializeField] private TMP_Text reasonText;
        [Tooltip("내 순위 알약 — 내가 승자가 아닐 때만 보인다.")]
        [SerializeField] private GameObject mePill;
        [SerializeField] private TMP_Text meRankText;
        [SerializeField] private TMP_Text meDetailText;

        [Header("등장 연출 (시안 .stage.intro)")]
        [Tooltip("제목이 튀어나오기 시작하는 크기 (배).")]
        [SerializeField] private float titleStartScale = 0.3f;
        [Tooltip("제목이 튀어나오기까지 · 제 크기가 되는 시간 · 나타나는 시간 (초).")]
        [SerializeField] private Vector3 titleTiming = new Vector3(0.15f, 0.6f, 0.3f);
        [Tooltip("알약이 떠오르기까지 · 떠오르는 시간 (초).")]
        [SerializeField] private Vector2 pillsTiming = new Vector2(0.55f, 0.4f);
        [Tooltip("알약이 떠오르기 시작하는 깊이 — 제자리보다 이만큼 아래 (시안 fadeup 20).")]
        [SerializeField] private float riseDistance = 20f;

        private void Awake()
        {
            if (title == null || titleGroup == null || titleText == null || pills == null || pillsGroup == null || reasonLabelText == null || reasonText == null
                || mePill == null || meRankText == null || meDetailText == null)
                Debug.LogError($"[{nameof(ResultTitleView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>제목과 승리 방식 알약.</summary>
        public void Show(string titleLine, string reasonLabel, string reason)
        {
            titleText.text = titleLine;
            reasonLabelText.text = reasonLabel;
            reasonText.text = reason;
        }

        /// <summary>내 순위 알약 — 「나 공동 2위 · HP 54」. rank 가 null 이면 숨긴다 (내가 승자 · 무승부).</summary>
        public void ShowMe(string rank, string detail)
        {
            mePill.SetActive(rank != null);
            meRankText.text = rank;
            meDetailText.text = detail;
        }

        /// <summary>등장 연출 (시안 .stage.intro) — 제목이 튀어나오고 알약이 떠오른다.</summary>
        public void PlayIntro()
        {
            title.localScale = Vector3.one * titleStartScale;
            titleGroup.alpha = 0f;
            title.DOScale(1f, titleTiming.y).SetDelay(titleTiming.x).SetEase(Ease.OutBack, 2f).SetLink(gameObject);
            titleGroup.DOFade(1f, titleTiming.z).SetDelay(titleTiming.x).SetLink(gameObject);

            ResultPodiumView.Rise(pills, pillsGroup, pillsTiming.x, pillsTiming.y, riseDistance);
        }
    }
}
