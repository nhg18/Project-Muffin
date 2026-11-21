using System;
using System.Collections.Generic;
using Chapchu.UI.Fx;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 결과 화면 아래 버튼 줄 (시안 「결과화면 UI v3」 .btns) — 「로비로」(남은 초) · 「한 판 더!」(준비 점 · n/m 준비).
    /// 누르면 알리기만 한다. 준비 · 나감 · 남은 초는 ResultPresenter 가 넣어 준다.
    /// </summary>
    public class ResultButtonsView : MonoBehaviour
    {
        // 시안 문구
        private const string AgainLabel = "한 판 더!";
        private const string AgainReadyLabel = "준비 완료!";
        private const string AgainOffLabel = "한 판 더";
        private const string ReadyCountFormat = "{0}/{1} 준비";
        private const string ReadyCancelSuffix = " · 누르면 취소";
        private const string GoneFormat = "{0} 나감";
        private const string LobbyAutoFormat = "{0}초 후 자동 이동";
        private const string LobbyAloneFormat = "로비에서 새로 모집 · {0}초";

        [SerializeField] private RectTransform root;
        [SerializeField] private CanvasGroup group;

        [Header("판 기록")]
        [SerializeField] private Button boardButton;

        [Header("로비로")]
        [SerializeField] private Button lobbyButton;
        [SerializeField] private UIShape lobbyBox;
        [SerializeField] private TMP_Text lobbySubText;

        [Header("한 판 더")]
        [SerializeField] private Button againButton;
        [SerializeField] private UIShape againBox;
        [SerializeField] private UIShape againShadow;
        [SerializeField] private TMP_Text againText;
        [SerializeField] private TMP_Text againSubText;
        [Tooltip("준비 점이 놓이는 줄.")]
        [SerializeField] private RectTransform dotRow;
        [SerializeField] private ResultReadyDotView dotPrefab;

        [Header("색 (시안 :root)")]
        [SerializeField] private Color ink = new Color32(0x5E, 0x50, 0x47, 0xFF);
        [SerializeField] private Color yellow = new Color32(0xFF, 0xDC, 0x6E, 0xFF);
        [Tooltip("내가 준비했을 때의 「한 판 더」 면 색.")]
        [SerializeField] private Color yellowHigh = new Color32(0xFF, 0xF1, 0xBD, 0xFF);
        [Tooltip("꺼진 「한 판 더」 의 면 색.")]
        [SerializeField] private Color offFill = new Color32(0xF7, 0xF3, 0xED, 0xFF);
        [Tooltip("꺼진 「한 판 더」 의 테 · 그림자 색.")]
        [SerializeField] private Color offEdge = new Color32(0xD6, 0xCD, 0xC2, 0xFF);
        [Tooltip("꺼진 「한 판 더」 의 글자 색.")]
        [SerializeField] private Color offInk = new Color32(0xBD, 0xB4, 0xAA, 0xFF);

        [Header("등장 연출 (시안 .stage.intro .bottom)")]
        [Tooltip("떠오르기 시작하는 깊이 — 제자리보다 이만큼 아래 (시안 fadeup 20).")]
        [SerializeField] private float riseDistance = 20f;
        [Tooltip("떠오르는 시간 (초).")]
        [SerializeField] private float riseSeconds = 0.4f;

        private readonly List<ResultReadyDotView> _dots = new List<ResultReadyDotView>();
        private bool _isAlone;
        private int _seconds = -1;

        /// <summary>「한 판 더」 를 눌렀다.</summary>
        public event Action AgainClicked;
        /// <summary>「로비로」 를 눌렀다.</summary>
        public event Action LobbyClicked;
        /// <summary>「판 기록」 을 눌렀다.</summary>
        public event Action BoardClicked;

        private void Awake()
        {
            if (root == null || group == null || boardButton == null || lobbyButton == null || lobbyBox == null || lobbySubText == null || againButton == null || againBox == null
                || againShadow == null || againText == null || againSubText == null || dotRow == null || dotPrefab == null)
                Debug.LogError($"[{nameof(ResultButtonsView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            boardButton.onClick.AddListener(HandleBoardButtonClicked);
            lobbyButton.onClick.AddListener(HandleLobbyButtonClicked);
            againButton.onClick.AddListener(HandleAgainButtonClicked);
        }

        private void OnDisable()
        {
            boardButton.onClick.RemoveListener(HandleBoardButtonClicked);
            lobbyButton.onClick.RemoveListener(HandleLobbyButtonClicked);
            againButton.onClick.RemoveListener(HandleAgainButtonClicked);
        }

        private void HandleBoardButtonClicked() => BoardClicked?.Invoke();

        private void HandleLobbyButtonClicked() => LobbyClicked?.Invoke();

        private void HandleAgainButtonClicked() => AgainClicked?.Invoke();

        /// <summary>준비 점을 사람 수만큼 만든다 — 한 번만.</summary>
        public void BuildDots(int count)
        {
            for (int i = _dots.Count; i < count; i++)
                _dots.Add(Instantiate(dotPrefab, dotRow));
        }

        /// <summary>index 번째 사람의 준비 점.</summary>
        public void ShowDot(int index, Color color, bool isReady, bool isGone) => _dots[index].Show(color, isReady, isGone);

        /// <summary>
        /// 「한 판 더」 — 방에 2명 이상 남아 있으면 「n/m 준비」, 내가 준비했으면 「준비 완료! · 누르면 취소」.
        /// 혼자 남았으면(goneNames = 나간 사람들) 꺼지고 「로비로」 가 주 버튼이 된다.
        /// </summary>
        public void ShowAgain(bool canRematch, bool isMeReady, int readyCount, int memberCount, string goneNames)
        {
            _isAlone = !canRematch;
            againButton.interactable = canRematch;
            againBox.raycastTarget = canRematch;
            againBox.color = !canRematch ? offFill : isMeReady ? yellowHigh : yellow;
            againBox.BorderColor = canRematch ? ink : offEdge;
            againShadow.color = canRematch ? ink : offEdge;
            againText.color = canRematch ? ink : offInk;
            againText.text = !canRematch ? AgainOffLabel : isMeReady ? AgainReadyLabel : AgainLabel;
            againSubText.text = canRematch
                ? string.Format(ReadyCountFormat, readyCount, memberCount) + (isMeReady ? ReadyCancelSuffix : string.Empty)
                : string.Format(GoneFormat, goneNames);

            lobbyBox.color = canRematch ? Color.white : yellow;
            ShowLobbyText();
        }

        /// <summary>자동 이동까지 남은 초.</summary>
        public void ShowSeconds(int seconds)
        {
            if (_seconds == seconds)
                return;

            _seconds = seconds;
            ShowLobbyText();
        }

        private void ShowLobbyText() => lobbySubText.text = string.Format(_isAlone ? LobbyAloneFormat : LobbyAutoFormat, Mathf.Max(_seconds, 0));

        /// <summary>등장 연출 (시안 .stage.intro .bottom) — delay 초 뒤 떠오른다. 보이기 전에는 눌리지 않는다.</summary>
        public void PlayIntro(float delay) => ResultPodiumView.Rise(root, group, delay, riseSeconds, riseDistance, blockUntilShown: true);
    }
}
