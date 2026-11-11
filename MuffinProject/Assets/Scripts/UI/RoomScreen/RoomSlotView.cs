using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 플레이어 칸 하나 (프리팹 RoomSlot) — 방석 위에 선 캐릭터 + 이름표 + 꼬리표(방장 · 나 · 봇).
    /// 빈 칸은 흐린 방석만 남고, 방장에게는 「봇 추가」 가 보인다. 봇 칸은 방장에게 「제거」 가 보인다.
    /// 기준 문서: docs/systems/16-room-ui-full.md 7-3 · 7-4, docs/systems/18-bot.md 8-1
    /// </summary>
    public class RoomSlotView : MonoBehaviour
    {
        [Tooltip("칸 전체 — 사람이 있는 칸만 눌린다 (플레이어 정보 판)")]
        [SerializeField] private Button slotButton;

        [Header("방석 · 캐릭터")]
        [SerializeField] private Image cushion;

        [Tooltip("빈 칸의 방석 알파 (시안 .slot.empty .cush)")]
        [SerializeField] private float emptyCushionAlpha = 0.6f;

        [Tooltip("캐릭터 묶음 (그림자 + 몸) — 빈 칸이면 끈다")]
        [SerializeField] private GameObject character;
        [SerializeField] private Image characterBody;

        [Tooltip("봇 캐릭터에 입히는 색 — 사람보다 흐리게 (시안 saturate .35)")]
        [SerializeField] private Color botTint = new Color32(0xE2, 0xE8, 0xF2, 0xFF);

        [Header("이름표")]
        [SerializeField] private GameObject namePlate;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject hostTag;
        [SerializeField] private GameObject meTag;
        [SerializeField] private GameObject botTag;

        [Header("봇 버튼 — 방장에게만")]
        [SerializeField] private Button addBotButton;
        [SerializeField] private Button removeBotButton;

        /// <summary>사람이 있는 칸을 눌렀다.</summary>
        public event Action<RoomSlotView> OnClicked;

        /// <summary>빈 칸의 「봇 추가」 를 눌렀다.</summary>
        public event Action OnAddBotRequested;

        /// <summary>봇 칸의 「제거」 를 눌렀다.</summary>
        public event Action<RoomSlotView> OnRemoveBotRequested;

        private void Awake()
        {
            if (slotButton == null || cushion == null || character == null || characterBody == null || namePlate == null || nameText == null
                || hostTag == null || meTag == null || botTag == null || addBotButton == null || removeBotButton == null)
                Debug.LogError($"[{nameof(RoomSlotView)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            slotButton.onClick.AddListener(HandleSlotClicked);
            addBotButton.onClick.AddListener(HandleAddBotClicked);
            removeBotButton.onClick.AddListener(HandleRemoveBotClicked);
        }

        private void OnDisable()
        {
            slotButton.onClick.RemoveListener(HandleSlotClicked);
            addBotButton.onClick.RemoveListener(HandleAddBotClicked);
            removeBotButton.onClick.RemoveListener(HandleRemoveBotClicked);
        }

        public void ShowEmpty(bool canAddBot)
        {
            Show(isOccupied: false);
            addBotButton.gameObject.SetActive(canAddBot);
        }

        public void ShowPlayer(string nickname, bool isHost, bool isMe)
        {
            Show(isOccupied: true);
            nameText.text = nickname;
            characterBody.color = Color.white;
            slotButton.interactable = true;
            hostTag.SetActive(isHost);
            meTag.SetActive(isMe);
        }

        public void ShowBot(string nickname, bool canRemove)
        {
            Show(isOccupied: true);
            nameText.text = nickname;
            characterBody.color = botTint;
            botTag.SetActive(true);
            removeBotButton.gameObject.SetActive(canRemove);
        }

        // 칸을 비우거나 채운 기본 모습 — 꼬리표 · 버튼은 전부 끄고, 부른 쪽이 필요한 것만 켠다
        private void Show(bool isOccupied)
        {
            Color cushionColor = cushion.color;
            cushionColor.a = isOccupied ? 1f : emptyCushionAlpha;
            cushion.color = cushionColor;

            character.SetActive(isOccupied);
            namePlate.SetActive(isOccupied);
            slotButton.interactable = false;
            hostTag.SetActive(false);
            meTag.SetActive(false);
            botTag.SetActive(false);
            addBotButton.gameObject.SetActive(false);
            removeBotButton.gameObject.SetActive(false);
        }

        private void HandleSlotClicked() => OnClicked?.Invoke(this);

        private void HandleAddBotClicked() => OnAddBotRequested?.Invoke();

        private void HandleRemoveBotClicked() => OnRemoveBotRequested?.Invoke(this);
    }
}
