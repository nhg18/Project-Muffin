using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 좌석 슬롯 하나 (10-ui.md 2-1 「좌석 슬롯」 · 3절). 값을 받아 표시하고, 탭을 이벤트로만 알린다.
    /// 어느 플레이어(actorNumber)의 좌석인지는 모른다 — 좌석 인덱스 ↔ 플레이어 대응은 프레젠터가 한다 (02-player.md 3절).
    /// 뒷면 손패 부채꼴은 이후 작업 — <see cref="HandFanAnchor"/> 아래에 붙인다.
    /// </summary>
    public class OpponentSeatView : MonoBehaviour, IPointerClickHandler
    {
        private const string NicknameFormat = "|{0}";

        [Header("이름 줄")]
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text handCountText;
        [SerializeField] private GameObject chapchuBadge;

        [Header("HP")]
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("HP 가 이 값 이하면 게이지를 경고 색으로 (시안: 20). 표시 연출일 뿐 규칙이 아니다.")]
        [SerializeField] private int lowHpThreshold = 20;
        [SerializeField] private Color lowHpColor = new Color32(0xF5, 0x4A, 0x18, 0xFF);

        [Header("캐릭터 · 함정")]
        [SerializeField] private Image characterImage;
        [SerializeField] private RectTransform handFanAnchor;
        [SerializeField] private Image[] trapSlotBorders = new Image[3];
        [SerializeField] private Image[] trapSlotFills = new Image[3];
        [SerializeField] private Color trapEmptyBorderColor = new Color32(0x99, 0x99, 0x99, 0xFF);
        [SerializeField] private Color trapEmptyFillColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color trapFilledBorderColor = new Color32(0x11, 0x11, 0x11, 0xFF);

        [Header("상태 표시")]
        [Tooltip("좌석 색 (슬롯 1 파랑 · 2 주황 · 3 초록). HP 채움 · 설치된 함정 칸에 쓴다.")]
        [SerializeField] private Color seatColor = new Color32(0x0A, 0x9C, 0xFF, 0xFF);
        [SerializeField] private GameObject turnOutline;
        [SerializeField] private GameObject targetHighlight;

        /// <summary>뒷면 손패 부채꼴이 붙을 자리 (캐릭터 아래 가운데).</summary>
        public RectTransform HandFanAnchor => handFanAnchor;

        /// <summary>좌석을 탭했다 — 대상 선택에 쓴다 (10-ui.md 6절). 판정은 하지 않는다.</summary>
        public event Action Clicked;

        private void Awake()
        {
            if (nicknameText == null || handCountText == null || hpFill == null || hpText == null)
                Debug.LogError($"[{nameof(OpponentSeatView)}] 이름 · 손패 수 · HP 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetNickname(string nickname) => nicknameText.text = string.Format(NicknameFormat, nickname);

        public void SetHandCount(int count) => handCountText.text = count.ToString();

        /// <summary>HP 게이지 · 수치. 게이지 폭 = hp / maxHp.</summary>
        public void SetHp(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            hpFill.anchorMax = new Vector2(ratio, 1f);
            hpText.text = hp.ToString();

            if (hpFillImage != null)
                hpFillImage.color = hp <= lowHpThreshold ? lowHpColor : seatColor;
        }

        /// <summary>설치된 함정 수(0~3). 앞에서부터 채운다.</summary>
        public void SetTrapCount(int count)
        {
            for (int i = 0; i < trapSlotBorders.Length; i++)
            {
                bool filled = i < count;
                if (trapSlotBorders[i] != null)
                    trapSlotBorders[i].color = filled ? trapFilledBorderColor : trapEmptyBorderColor;
                if (trapSlotFills[i] != null)
                    trapSlotFills[i].color = filled ? seatColor : trapEmptyFillColor;
            }
        }

        public void SetChapchu(bool declared) => chapchuBadge.SetActive(declared);

        public void SetTurn(bool isTurn) => turnOutline.SetActive(isTurn);

        /// <summary>대상 선택 중 고를 수 있는 좌석 강조 (10-ui.md 3절).</summary>
        public void SetTargetHighlight(bool on) => targetHighlight.SetActive(on);

        public void SetCharacter(Sprite sprite)
        {
            characterImage.sprite = sprite;
            characterImage.enabled = sprite != null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            Clicked?.Invoke();
        }
    }
}
