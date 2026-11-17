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
            title.localScale = Vector3.one * 0.3f;
            titleGroup.alpha = 0f;
            title.DOScale(1f, 0.6f).SetDelay(0.15f).SetEase(Ease.OutBack, 2f).SetLink(gameObject);
            titleGroup.DOFade(1f, 0.3f).SetDelay(0.15f).SetLink(gameObject);

            Vector2 home = pills.anchoredPosition;
            pills.anchoredPosition = home + new Vector2(0f, -20f);
            pillsGroup.alpha = 0f;
            pills.DOAnchorPos(home, 0.4f).SetDelay(0.55f).SetEase(Ease.OutQuad).SetLink(gameObject);
            pillsGroup.DOFade(1f, 0.4f).SetDelay(0.55f).SetLink(gameObject);
        }
    }
}
