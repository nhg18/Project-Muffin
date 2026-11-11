using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>상점 머리줄의 세 갈래</summary>
    public enum LobbyShopTab { Gacha, Item, Charge }

    /// <summary>
    /// 로비 섹션(꾸미기 · 상점)의 틀 — 머리줄(뒤로 · 이름 · 상점 탭) · 캐릭터 아래 정보 줄 · 오른쪽 판이 미끄러져 들어오는 움직임.
    /// 판 안 내용은 LobbyItemPaneView · LobbyGachaPaneView 가 그린다.
    /// 기준: 「통합 화면 UI v2」 로비 #sec · .pane · renderSecbar · renderInfo
    /// </summary>
    public class LobbySectionView : MonoBehaviour
    {
        private const float FadeSeconds = 0.2f;
        private const float SlideSeconds = 0.25f;

        [SerializeField] private CanvasGroup head;
        [SerializeField] private RectTransform pane;
        [SerializeField] private CanvasGroup paneContent;
        [SerializeField] private CanvasGroup info;

        [Header("머리줄")]
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject shopTabRow;
        [SerializeField] private LobbyTabView[] shopTabs;

        [Header("정보 줄")]
        [SerializeField] private GameObject infoItem;
        [SerializeField] private TMP_Text infoName;
        [SerializeField] private GameObject gradeTag;
        [SerializeField] private Image gradeTagBorder;
        [SerializeField] private TMP_Text gradeTagText;
        [SerializeField] private Image infoLeadIcon;
        [SerializeField] private TMP_Text infoSubText;
        [SerializeField] private Image infoPriceIcon;
        [SerializeField] private TMP_Text infoPriceText;
        [SerializeField] private TMP_Text infoHint;
        [SerializeField] private Button infoButton;
        [SerializeField] private Image infoButtonIcon;
        [SerializeField] private TMP_Text infoButtonLabel;

        [Tooltip("판이 화면 밖에 있을 때 오른쪽으로 밀려나 있는 거리 (캔버스 px)")]
        [SerializeField] private float paneHiddenOffset = 1300f;

        private Vector2 _paneOrigin;
        private bool _isOpen;

        public event Action OnBackRequested;
        public event Action<LobbyShopTab> OnShopTabRequested;
        public event Action OnInfoButtonClicked;

        private void Awake()
        {
            if (head == null || pane == null || paneContent == null || info == null || backButton == null || nameText == null
                || shopTabRow == null || shopTabs == null || shopTabs.Length != 3)
                Debug.LogError($"[{nameof(LobbySectionView)}] 머리줄 · 판 참조가 비어 있습니다.", this);

            if (infoItem == null || infoName == null || gradeTag == null || gradeTagBorder == null || gradeTagText == null || infoLeadIcon == null
                || infoSubText == null || infoPriceIcon == null || infoPriceText == null || infoHint == null
                || infoButton == null || infoButtonIcon == null || infoButtonLabel == null)
                Debug.LogError($"[{nameof(LobbySectionView)}] 정보 줄 참조가 비어 있습니다.", this);

            _paneOrigin = pane.anchoredPosition;
            pane.anchoredPosition = _paneOrigin + new Vector2(paneHiddenOffset, 0f);
            SetGroup(head, false);
            SetGroup(info, false);
            paneContent.blocksRaycasts = false;
        }

        private void OnEnable()
        {
            backButton.onClick.AddListener(HandleBackClicked);
            infoButton.onClick.AddListener(HandleInfoButtonClicked);
            foreach (LobbyTabView tab in shopTabs)
                tab.OnClicked += HandleShopTabClicked;
        }

        private void OnDisable()
        {
            backButton.onClick.RemoveListener(HandleBackClicked);
            infoButton.onClick.RemoveListener(HandleInfoButtonClicked);
            foreach (LobbyTabView tab in shopTabs)
                tab.OnClicked -= HandleShopTabClicked;

            head.DOKill();
            info.DOKill();
            pane.DOKill();
            paneContent.DOKill();
        }

        /// <summary>섹션을 연다 / 닫는다 — 머리줄 · 정보 줄은 0.2초 페이드, 판은 오른쪽에서 0.25초.</summary>
        public void SetOpen(bool open)
        {
            if (_isOpen == open)
                return;

            _isOpen = open;
            Fade(head, open);
            Fade(info, open);
            paneContent.blocksRaycasts = open;
            pane.DOKill();
            pane.DOAnchorPos(open ? _paneOrigin : _paneOrigin + new Vector2(paneHiddenOffset, 0f), SlideSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        /// <summary>섹션 ↔ 섹션 — 판 안 내용 · 정보 줄만 0.2초에 다시 나타난다 (틀 · 머리줄은 제자리).</summary>
        public void FlashContent()
        {
            paneContent.DOKill();
            paneContent.alpha = 0f;
            paneContent.DOFade(1f, FadeSeconds).SetUpdate(true);
            info.DOKill();
            info.alpha = 0f;
            info.DOFade(1f, FadeSeconds).SetUpdate(true);
        }

        /// <summary>머리줄 — 꾸미기는 이름만, 상점은 이름 + 탭(뽑기 · 아이템 · 충전).</summary>
        public void SetHeader(string sectionName, bool showShopTabs, LobbyShopTab current)
        {
            nameText.text = sectionName;
            shopTabRow.SetActive(showShopTabs);
            for (int i = 0; i < shopTabs.Length; i++)
                shopTabs[i].SetPressed(i == (int)current);
        }

        /// <summary>정보 줄 — 이름 + 아랫줄(등급 꼬리표 · 앞 아이콘 · 글 · 값). grade 가 null 이면 꼬리표 없음.</summary>
        public void SetInfo(string itemName, HubGrade? grade, string sub, Sprite leadIcon = null, Sprite priceIcon = null, string price = null)
        {
            infoItem.SetActive(true);
            infoHint.gameObject.SetActive(false);
            infoName.text = itemName;
            gradeTag.SetActive(grade.HasValue);
            if (grade.HasValue)
            {
                Color color = LobbyStyle.GradeColor(grade.Value);
                gradeTagBorder.color = color;
                gradeTagText.color = color;
                gradeTagText.text = LobbyHubData.GradeName(grade.Value);
            }

            infoLeadIcon.gameObject.SetActive(leadIcon != null);
            infoLeadIcon.sprite = leadIcon;
            infoSubText.text = sub;
            infoPriceIcon.gameObject.SetActive(priceIcon != null);
            infoPriceIcon.sprite = priceIcon;
            infoPriceText.gameObject.SetActive(price != null);
            infoPriceText.text = price;
        }

        /// <summary>고른 것이 없을 때의 안내 한 줄.</summary>
        public void SetInfoHint(string hint)
        {
            infoItem.SetActive(false);
            infoHint.gameObject.SetActive(true);
            infoHint.text = hint;
        }

        /// <summary>정보 줄 오른쪽 버튼 (「재생」 · 「확률 보기」). label 이 null 이면 없다.</summary>
        public void SetInfoButton(string label, Sprite icon = null)
        {
            infoButton.gameObject.SetActive(label != null);
            infoButtonLabel.text = label;
            infoButtonIcon.gameObject.SetActive(icon != null);
            infoButtonIcon.sprite = icon;
        }

        private static void Fade(CanvasGroup group, bool visible)
        {
            group.blocksRaycasts = visible;
            group.DOKill();
            group.DOFade(visible ? 1f : 0f, FadeSeconds).SetUpdate(true);
        }

        private static void SetGroup(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
        }

        private void HandleBackClicked() => OnBackRequested?.Invoke();

        private void HandleInfoButtonClicked() => OnInfoButtonClicked?.Invoke();

        private void HandleShopTabClicked(LobbyTabView tab) => OnShopTabRequested?.Invoke((LobbyShopTab)Array.IndexOf(shopTabs, tab));
    }
}
