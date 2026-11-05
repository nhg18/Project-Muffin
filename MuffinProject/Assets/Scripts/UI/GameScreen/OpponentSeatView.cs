using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 좌석 슬롯 하나 — 이름 줄 · HP 줄 · 턴 외곽선 · 대상 선택 외곽선 (10-ui.md 2-1 「좌석 슬롯」 · 3절 · 6절). 값을 받아 표시만 한다.
    /// 어느 플레이어(actorNumber)의 좌석인지는 모른다 — 대응은 OpponentSeatsPresenter 가 한다 (02-player.md 3절).
    /// 손패 수는 숫자 상자와 뒷면 부채꼴(OpponentHandView)에 같이 넣는다 (10-ui.md 4절 「상대 손패」).
    /// 탭은 대상 선택 중(SetTargetable)에만 받는다 — 평소에는 탭 영역의 레이캐스트를 꺼서 덱 등 겹치는 버튼을 가리지 않는다.
    /// 캐릭터 · 함정 슬롯은 보류 (2026-11-03) — 자리만 비워 둔다.
    /// </summary>
    public class OpponentSeatView : MonoBehaviour, IPointerClickHandler
    {
        private const string NicknameFormat = "|{0}";

        [Header("이름 줄")]
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text handCountText;
        [Tooltip("뒷면 부채꼴 — 캐릭터 아래 가운데 앵커.")]
        [SerializeField] private OpponentHandView hand;
        [Tooltip("찹츄 선언 배지. 선언 이벤트가 생기기 전까지는 꺼 둔다 (기능 8).")]
        [SerializeField] private GameObject chapchuBadge;

        [Header("HP")]
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private Image hpFillImage;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("HP 가 이 값 이하면 게이지를 경고 색으로 (10-ui.md 2-1 HP 줄, 제안: 20). 표시 연출일 뿐 규칙이 아니다.")]
        [SerializeField] private int lowHpThreshold = 20;
        [SerializeField] private Color lowHpColor = new Color32(0xF5, 0x4A, 0x18, 0xFF);

        [Header("상태 표시")]
        [Tooltip("좌석 색 (좌석 1 파랑 · 2 주황 · 3 초록). HP 채움에 쓴다.")]
        [SerializeField] private Color seatColor = new Color32(0x0A, 0x9C, 0xFF, 0xFF);
        [SerializeField] private GameObject turnOutline;
        [Tooltip("대상 선택 중 파란 외곽선 (10-ui.md 6절).")]
        [SerializeField] private GameObject targetOutline;
        [Tooltip("좌석 전체를 덮는 투명 탭 영역. 대상 선택 중에만 레이캐스트를 켠다.")]
        [SerializeField] private Image tapArea;

        /// <summary>대상 선택 중 이 좌석을 탭했다.</summary>
        public event Action<OpponentSeatView> OnClicked;

        private void Awake()
        {
            if (nicknameText == null || handCountText == null || hand == null || chapchuBadge == null || hpFill == null || hpFillImage == null || hpText == null
                || turnOutline == null || targetOutline == null || tapArea == null)
                Debug.LogError($"[{nameof(OpponentSeatView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetNickname(string nickname) => nicknameText.text = string.Format(NicknameFormat, nickname);

        /// <summary>손패 장수 — 숫자 상자와 뒷면 부채꼴을 같이 맞춘다.</summary>
        public void SetHandCount(int count)
        {
            handCountText.text = count.ToString();
            hand.SetCount(count);
        }

        /// <summary>HP 게이지 · 수치. 게이지 폭 = hp / maxHp.</summary>
        public void SetHp(int hp, int maxHp)
        {
            float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;
            hpFill.anchorMax = new Vector2(ratio, 1f);
            hpText.text = hp.ToString();
            hpFillImage.color = hp <= lowHpThreshold ? lowHpColor : seatColor;
        }

        public void SetChapchu(bool declared) => chapchuBadge.SetActive(declared);

        /// <summary>이 플레이어의 턴이면 이름 줄 · HP 줄을 초록 외곽선으로 감싼다 — 캐릭터는 덮지 않는다 (02-player.md 5절 · 10-ui.md 2-1).</summary>
        public void SetTurn(bool isTurn) => turnOutline.SetActive(isTurn);

        /// <summary>대상으로 고를 수 있는 상태 — 파란 외곽선을 켜고 탭을 받는다 (10-ui.md 6절).</summary>
        public void SetTargetable(bool targetable)
        {
            targetOutline.SetActive(targetable);
            tapArea.raycastTarget = targetable;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke(this);
        }
    }
}
