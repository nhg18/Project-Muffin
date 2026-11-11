using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 플레이어 정보 판 — 칸을 누르면 뜬다. 캐릭터 · 닉네임 · 방장 / 참가자 · 「나」. 닫기 버튼으로만 닫힌다.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-5, docs/systems/22-popup-ui.md 1절 #4
    /// </summary>
    public class RoomPlayerInfoView : MonoBehaviour
    {
        [Tooltip("가림막 + 판 — 닫혀 있으면 꺼 둔다")]
        [SerializeField] private GameObject content;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject hostTag;
        [SerializeField] private GameObject guestTag;
        [SerializeField] private GameObject meTag;

        [Tooltip("오른쪽 위 X · 아래 「닫기」")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button okButton;

        public bool IsOpen => content.activeSelf;

        /// <summary>지금 보여 주는 사람의 접속 번호 — 그 사람이 나가면 판을 닫는 데 쓴다.</summary>
        public int ActorNumber { get; private set; }

        private void Awake()
        {
            if (content == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(content)} 참조가 비어 있습니다.", this);

            if (nameText == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(nameText)} 참조가 비어 있습니다.", this);

            if (hostTag == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(hostTag)} 참조가 비어 있습니다.", this);

            if (guestTag == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(guestTag)} 참조가 비어 있습니다.", this);

            if (meTag == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(meTag)} 참조가 비어 있습니다.", this);

            if (closeButton == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(closeButton)} 참조가 비어 있습니다.", this);

            if (okButton == null)
                Debug.LogError($"[{nameof(RoomPlayerInfoView)}] {nameof(okButton)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            closeButton.onClick.AddListener(HandleCloseClicked);
            okButton.onClick.AddListener(HandleCloseClicked);
        }

        private void OnDisable()
        {
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            okButton.onClick.RemoveListener(HandleCloseClicked);
        }

        public void Show(int actorNumber, string nickname, bool isHost, bool isMe)
        {
            ActorNumber = actorNumber;
            nameText.text = nickname;
            hostTag.SetActive(isHost);
            guestTag.SetActive(!isHost);
            meTag.SetActive(isMe);
            content.SetActive(true);
        }

        public void Hide() => content.SetActive(false);

        private void HandleCloseClicked() => Hide();
    }
}
