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

        /// <summary>다시 하기 버튼을 누름. 동작은 미정 (10-ui.md 11절 #6)</summary>
        public event Action OnRestartRequested;

        /// <summary>방으로 버튼을 누름. 동작은 미정 (10-ui.md 11절 #6)</summary>
        public event Action OnToRoomRequested;

        private void Awake()
        {
            if (winnerText == null || messageText == null || restartButton == null || toRoomButton == null)
                Debug.LogError($"[{nameof(VictoryOverlayView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            restartButton.onClick.AddListener(HandleRestartRequested);
            toRoomButton.onClick.AddListener(HandleToRoomRequested);
        }

        private void OnDisable()
        {
            restartButton.onClick.RemoveListener(HandleRestartRequested);
            toRoomButton.onClick.RemoveListener(HandleToRoomRequested);
        }

        /// <summary>승자 · 문구를 넣고 보인다.</summary>
        public void Show(string winnerNickname, string message)
        {
            winnerText.text = string.Format(WinnerFormat, winnerNickname);
            messageText.text = message;
            gameObject.SetActive(true);
        }

        /// <summary>감춘다.</summary>
        public void Hide() => gameObject.SetActive(false);

        private void HandleRestartRequested() => OnRestartRequested?.Invoke();

        private void HandleToRoomRequested() => OnToRoomRequested?.Invoke();
    }
}
