using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 방 화면 루트 뷰. 값을 받아 표시하고, 버튼 탭을 이벤트로만 알린다.
    /// 네트워크 · 씬 전환은 모른다 (LobbyView 와 같은 구조). 기준 문서: docs/systems/16-room-ui.md
    /// </summary>
    public class RoomView : MonoBehaviour
    {
        // 08-room.md 5절 #4: 로그 최대 30줄
        private const int MaxLogLines = 30;
        private const string EmptySlotText = "대기 중";
        private const string HostTagText = "(방장)";

        [Serializable]
        private class PlayerSlot
        {
            public Image box;
            public TMP_Text nameText;
            public TMP_Text hostText;
        }

        [SerializeField] private TMP_Text roomCodeText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private PlayerSlot[] playerSlots;
        [SerializeField] private TMP_Text logText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;

        // 빈 칸 · 찬 칸의 박스 투명도 (16-room-ui 7-1 스타일)
        [SerializeField] private float filledBoxAlpha = 0.92f;
        [SerializeField] private float emptyBoxAlpha = 0.4f;

        private readonly Queue<string> _logLines = new Queue<string>();

        public event Action StartRequested;
        public event Action LeaveRequested;

        private void OnEnable()
        {
            startButton.onClick.AddListener(HandleStartClicked);
            leaveButton.onClick.AddListener(HandleLeaveClicked);
        }

        private void OnDisable()
        {
            startButton.onClick.RemoveListener(HandleStartClicked);
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
        }

        public void SetRoomCode(string code) => roomCodeText.text = code;

        public void SetPlayerCount(int current, int max) => playerCountText.text = $"{current} / {max}";

        /// <summary>플레이어 칸 갱신. 순서대로 채우고 남는 칸은 "대기 중".</summary>
        public void SetPlayers(IReadOnlyList<(string name, bool isHost)> players)
        {
            for (int i = 0; i < playerSlots.Length; i++)
            {
                PlayerSlot slot = playerSlots[i];
                bool filled = i < players.Count;

                slot.nameText.text = filled ? players[i].name : EmptySlotText;
                slot.hostText.text = filled && players[i].isHost ? HostTagText : string.Empty;

                Color color = slot.box.color;
                color.a = filled ? filledBoxAlpha : emptyBoxAlpha;
                slot.box.color = color;
            }
        }

        /// <summary>시작 버튼은 방장에게만 보이고, 최소 인원이 찼을 때만 누를 수 있다 (16-room-ui 3절 #2).</summary>
        public void SetStartButton(bool visible, bool interactable)
        {
            startButton.gameObject.SetActive(visible);
            startButton.interactable = interactable;
        }

        public void AddLog(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines)
                _logLines.Dequeue();

            logText.text = string.Join("\n", _logLines);
        }

        private void HandleStartClicked() => StartRequested?.Invoke();
        private void HandleLeaveClicked() => LeaveRequested?.Invoke();
    }
}
