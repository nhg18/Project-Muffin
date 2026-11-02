using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 승리 오버레이 (10-ui.md 2-1 「승리 오버레이」). 결과 화면 구성은 미정(11절 #6)이라 시안 모양만 둔다.
    /// 승자 · 문구를 받아 보이고, 버튼 탭을 이벤트로만 알린다. 기본은 비활성.
    /// </summary>
    public class VictoryOverlayView : MonoBehaviour
    {
        private const string WinnerFormat = "{0} 승리!";

        [SerializeField] private TMP_Text winnerText;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button toRoomButton;

        public event Action RestartClicked;
        public event Action ToRoomClicked;

        private void Awake()
        {
            if (winnerText == null || messageText == null || restartButton == null || toRoomButton == null)
                Debug.LogError($"[{nameof(VictoryOverlayView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            restartButton.onClick.AddListener(HandleRestartClicked);
            toRoomButton.onClick.AddListener(HandleToRoomClicked);
        }

        private void OnDisable()
        {
            restartButton.onClick.RemoveListener(HandleRestartClicked);
            toRoomButton.onClick.RemoveListener(HandleToRoomClicked);
        }

        public void Show(string winnerNickname, string message)
        {
            winnerText.text = string.Format(WinnerFormat, winnerNickname);
            messageText.text = message;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void HandleRestartClicked() => RestartClicked?.Invoke();

        private void HandleToRoomClicked() => ToRoomClicked?.Invoke();
    }
}
