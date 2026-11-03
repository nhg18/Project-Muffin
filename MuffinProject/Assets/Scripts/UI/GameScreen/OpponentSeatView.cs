using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 좌석 슬롯 하나 — 이름 줄 · HP 줄 · 턴 외곽선 (10-ui.md 2-1 「좌석 슬롯」 · 3절). 값을 받아 표시만 한다.
    /// 어느 플레이어(actorNumber)의 좌석인지는 모른다 — 대응은 OpponentSeatsPresenter 가 한다 (02-player.md 3절).
    /// 캐릭터 · 함정 슬롯 · 뒷면 부채꼴 · 대상 선택 탭은 보류 (2026-11-03) — 자리만 비워 둔다.
    /// </summary>
    public class OpponentSeatView : MonoBehaviour
    {
        private const string NicknameFormat = "|{0}";

        [Header("이름 줄")]
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private TMP_Text handCountText;
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

        private void Awake()
        {
            if (nicknameText == null || handCountText == null || chapchuBadge == null || hpFill == null || hpFillImage == null || hpText == null || turnOutline == null)
                Debug.LogError($"[{nameof(OpponentSeatView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetNickname(string nickname) => nicknameText.text = string.Format(NicknameFormat, nickname);

        public void SetHandCount(int count) => handCountText.text = count.ToString();

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
    }
}
