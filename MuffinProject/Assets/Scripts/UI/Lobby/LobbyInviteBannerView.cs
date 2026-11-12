using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 초대 배너 — 오른쪽 위, 친구 버튼 아래. 30초 동안 「수락」 · 「거절」, 시간이 다 되면 문구 없이 사라진다. 가장 최근 초대 하나만.
    /// 기준: 「통합 화면 UI v2」 로비 .invite, docs/systems/17-lobby-ui-full.md 5-1
    /// </summary>
    public class LobbyInviteBannerView : MonoBehaviour
    {
        private const string TitleFormat = "{0}님이 초대했어요";
        private const string SecondsFormat = "{0}초";

        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text secondsText;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;

        private Coroutine _countdown;

        public bool IsShown => content.activeSelf;

        /// <summary>지금 떠 있는 초대를 보낸 사람 — 없으면 null</summary>
        public string Inviter { get; private set; }

        public event Action OnAccepted;
        public event Action OnDeclined;
        public event Action OnExpired;

        private void Awake()
        {
            if (content == null || titleText == null || secondsText == null || acceptButton == null || declineButton == null)
                Debug.LogError($"[{nameof(LobbyInviteBannerView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            acceptButton.onClick.AddListener(HandleAcceptClicked);
            declineButton.onClick.AddListener(HandleDeclineClicked);
        }

        private void OnDisable()
        {
            acceptButton.onClick.RemoveListener(HandleAcceptClicked);
            declineButton.onClick.RemoveListener(HandleDeclineClicked);
            _countdown = null; // 꺼지면 코루틴도 멈춘다
        }

        public void Show(string inviter, int seconds)
        {
            Hide();
            Inviter = inviter;
            content.SetActive(true);
            titleText.text = string.Format(TitleFormat, inviter);
            _countdown = StartCoroutine(CountdownRoutine(seconds));
        }

        public void Hide()
        {
            if (_countdown != null)
                StopCoroutine(_countdown);

            _countdown = null;
            Inviter = null;
            content.SetActive(false);
        }

        private IEnumerator CountdownRoutine(int seconds)
        {
            var wait = new WaitForSecondsRealtime(1f);
            for (int left = seconds; left > 0; left--)
            {
                secondsText.text = string.Format(SecondsFormat, left);
                yield return wait;
            }

            _countdown = null;
            Hide();
            OnExpired?.Invoke();
        }

        private void HandleAcceptClicked()
        {
            Hide();
            OnAccepted?.Invoke();
        }

        private void HandleDeclineClicked()
        {
            Hide();
            OnDeclined?.Invoke();
        }
    }
}
