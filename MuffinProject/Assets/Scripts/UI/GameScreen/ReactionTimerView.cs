using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 반응 타이머 — 크게 둔 처리 카드의 아래 가장자리에 걸치는 알약 (10-ui.md 5절 「반응 타이머」 · 2-1 「카드 연출 자리」, 시안 .rt2).
    /// 열리기 전: 회색 「반응 · 곧」. 진행 중: 노란 막대가 줄고 남은 초, 얼마 안 남으면 빨강. 마감 뒤: 「‖ 처리하는 중」.
    /// 반응 시간에 낼 카운터가 있으면 알약 대신 오른쪽 아래 「스킵」 버튼 + 내 캐릭터 머리 옆 「!」 (통합 화면 UI v3 .btn.skip · .bang.me).
    /// 값은 ReactionTimerPresenter 가 매 프레임 넘긴다. 받은 값을 표시만 한다.
    /// </summary>
    public class ReactionTimerView : MonoBehaviour
    {
        private const string ReactionLabel = "반응";
        private const string ResolvingLabel = "처리하는 중";
        private const string SoonText = "곧";

        [Tooltip("켜고 끄는 몸통 — 루트는 켜 둔다.")]
        [SerializeField] private GameObject body;
        [SerializeField] private Image border;
        [SerializeField] private GameObject clockIcon;
        [SerializeField] private GameObject pauseIcon;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private GameObject bar;
        [Tooltip("막대의 채움 — 오른쪽 앵커(0~1)로 남은 비율을 그린다.")]
        [SerializeField] private Image barFill;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private GameObject unitText;
        [Tooltip("알약 전체를 누르는 버튼 — 고르는 중 알약은 누르지 않으므로 늘 꺼 둔다 (스킵은 skipCornerButton).")]
        [SerializeField] private Button skipButton;

        [Header("반응 시간 스킵 (통합 화면 UI v3)")]
        [Tooltip("오른쪽 아래 「스킵」 버튼 (시안 .btn.skip) — 낼 수 있는 카운터가 있을 때만 켠다.")]
        [SerializeField] private Button skipCornerButton;
        [Tooltip("내 캐릭터 머리 옆 「!」 (시안 .bang.me) — 스킵 버튼과 같이 켠다. 튀어나온 뒤 위아래로 흔들린다.")]
        [SerializeField] private RectTransform bang;
        [Tooltip("「!」 가 튀어나오는 시간 (시안 popin .3s).")]
        [SerializeField] private float bangPopSeconds = 0.3f;
        [Tooltip("「!」 흔들림 높이 · 한 번 오르는 시간 (시안 bob 1s · −4px × 2).")]
        [SerializeField] private float bangBobHeight = 8f;
        [SerializeField] private float bangBobSeconds = 0.5f;

        private Vector2 _bangRest;

        /// <summary>스킵을 눌렀다.</summary>
        public event System.Action SkipClicked;

        [Header("손패를 올렸을 때 (10-ui.md 2-1 「카드 연출 자리」)")]
        [Tooltip("무엇에 반응하는지 — 체인 맨 위 카드 이름. 손패를 올렸을 때만 보인다 (카드가 작아져 이름이 안 읽히므로).")]
        [SerializeField] private TMP_Text cardNameText;
        [Tooltip("손패를 올렸을 때 알약 윗변의 자리 (위 끝에서 캔버스 px) — 시안: 가운데가 손패 카드 높이 + 98 위 (1080 − 336 − 98 = 646). 줄어든 카드의 아래 끝(637)에 살짝 걸친다.")]
        [SerializeField] private float dockedTop = 616f;

        [Tooltip("평소 알약 윗변이 체인 카드의 아래 끝에서 올라와 걸치는 높이 (캔버스 px).")]
        [SerializeField] private float hang = 40f;

        private float _normalY;
        private bool _docked;
        private string _cardName;

        [Header("색 (시안 .rt2 · .low · .soon)")]
        [Tooltip("진행 중 · 처리 중 테두리와 글자색.")]
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [Tooltip("남은 시간 막대 색.")]
        [SerializeField] private Color fillColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [Tooltip("남은 시간이 적을 때 막대 색.")]
        [SerializeField] private Color lowFillColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [Tooltip("남은 시간이 적을 때 남은 초 글자색.")]
        [SerializeField] private Color lowInk = new Color32(0xc8, 0x1e, 0x2b, 0xFF);
        [Tooltip("반응 시간이 열리기 전(「곧」) 테두리 색.")]
        [SerializeField] private Color soonBorder = new Color32(0xb8, 0xae, 0xa4, 0xFF);
        [Tooltip("반응 시간이 열리기 전(「곧」) 글자색.")]
        [SerializeField] private Color soonInk = new Color32(0x8a, 0x81, 0x7a, 0xFF);

        private void Awake()
        {
            if (body == null || border == null || clockIcon == null || pauseIcon == null || labelText == null || bar == null || barFill == null || secondsText == null || unitText == null || cardNameText == null || skipButton == null
                || skipCornerButton == null || bang == null)
            {
                Debug.LogError($"[{nameof(ReactionTimerView)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            _normalY = ((RectTransform)transform).anchoredPosition.y;
            _bangRest = bang.anchoredPosition;
            cardNameText.gameObject.SetActive(false);
            Hide();
        }

        /// <summary>손패가 올라왔으면 올라온 손패 바로 위로 내려가 카드 이름을 함께 보인다. 내려가면 크게 둔 카드 아래 가장자리로 돌아간다.</summary>
        public void SetDocked(bool docked)
        {
            _docked = docked;
            var rect = (RectTransform)transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, docked ? -dockedTop : _normalY);
            RefreshCardName();
        }

        /// <summary>체인 카드의 아래 끝이 바뀌었다 (캔버스 y, 화면 가운데가 0 · 위가 +) — 평소 자리가 그 가장자리를 따라간다. 체인이 길어 카드가 줄었을 때.</summary>
        public void SetCardsBottom(float canvasY)
        {
            var rect = (RectTransform)transform;
            _normalY = canvasY + hang - ((RectTransform)rect.parent).rect.height / 2f;
            if (!_docked)
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, _normalY);
        }

        /// <summary>반응을 기다리는 카드의 이름 — 손패를 올렸을 때 보인다. null 이면 숨긴다.</summary>
        public void SetCardName(string cardName)
        {
            _cardName = cardName;
            RefreshCardName();
        }

        private void RefreshCardName()
        {
            bool show = _docked && !string.IsNullOrEmpty(_cardName);
            cardNameText.gameObject.SetActive(show);
            if (show)
                cardNameText.text = _cardName;
        }

        /// <summary>반응 시간이 아직 열리지 않았다 (카드 연출 중) — 회색 「반응 · 곧」.</summary>
        public void ShowSoon()
        {
            ShowParts(true);
            border.color = soonBorder;
            labelText.text = ReactionLabel;
            labelText.color = soonInk;
            SetFill(0f, fillColor);
            secondsText.text = SoonText;
            secondsText.color = soonInk;
            unitText.SetActive(false);
        }

        /// <summary>반응 시간 진행 중 — 남은 초 · 남은 비율(0~1). low 면 빨강. label 이 있으면 「반응」 대신 (1019 「{이름} 고르는 중」).</summary>
        public void ShowRemaining(int seconds, float fraction, bool low, string label = null)
        {
            ShowParts(true);
            border.color = ink;
            labelText.text = label ?? ReactionLabel;
            labelText.color = ink;
            SetFill(fraction, low ? lowFillColor : fillColor);
            secondsText.text = seconds.ToString();
            secondsText.color = low ? lowInk : ink;
            unitText.SetActive(true);
        }

        /// <summary>마감이 지났고 결과를 기다린다 — 「‖ 처리하는 중」.</summary>
        public void ShowResolving()
        {
            ShowParts(false);
            border.color = ink;
            labelText.text = ResolvingLabel;
            labelText.color = ink;
        }

        /// <summary>반응 시간에 내가 낼 수 있는 카운터가 있다 — 오른쪽 아래 「스킵」 버튼 + 내 머리 옆 「!」. 남은 시간은 보이지 않는다 (10-ui.md 5절).</summary>
        public void ShowSkip()
        {
            body.SetActive(false);
            skipCornerButton.gameObject.SetActive(true);
            ShowBang();
        }

        public void Hide()
        {
            body.SetActive(false);
            HideSkip();
        }

        private void HideSkip()
        {
            skipCornerButton.gameObject.SetActive(false);
            bang.DOKill();
            bang.gameObject.SetActive(false);
        }

        // 튀어나와(0 → 1, 살짝 넘침) 위아래로 계속 흔들린다 (시안 popin · bob)
        private void ShowBang()
        {
            if (bang.gameObject.activeSelf)
                return;

            bang.DOKill();
            bang.gameObject.SetActive(true);
            bang.anchoredPosition = _bangRest;
            bang.localScale = Vector3.zero;
            bang.DOScale(1f, bangPopSeconds).SetEase(Ease.OutBack, 2.4f).OnComplete(() =>
                bang.DOAnchorPosY(_bangRest.y + bangBobHeight, bangBobSeconds).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo));
        }

        private void OnEnable()
        {
            if (skipCornerButton != null)
                skipCornerButton.onClick.AddListener(HandleSkipClicked);
        }

        private void OnDisable()
        {
            if (skipCornerButton != null)
                skipCornerButton.onClick.RemoveListener(HandleSkipClicked);
            if (bang != null)
                bang.DOKill();
        }

        private void HandleSkipClicked() => SkipClicked?.Invoke();

        // counting = 시계 · 막대 · 초, 아니면 멈춤 아이콘 + 글만
        private void ShowParts(bool counting)
        {
            body.SetActive(true);
            HideSkip();
            skipButton.enabled = false; // 고르는 중 알약은 누르는 것이 아니다
            clockIcon.SetActive(counting);
            pauseIcon.SetActive(!counting);
            bar.SetActive(counting);
            secondsText.gameObject.SetActive(counting);
            if (!counting)
                unitText.SetActive(false);
        }

        private void SetFill(float fraction, Color color)
        {
            RectTransform fill = barFill.rectTransform;
            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), fill.anchorMax.y);
            barFill.color = color;
        }
    }
}
