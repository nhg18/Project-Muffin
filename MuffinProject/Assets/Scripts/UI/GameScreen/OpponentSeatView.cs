using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 상대 좌석 하나 — 캐릭터 자리 · 발밑 표시(턴 타원 · 대상 고리) · 이름표(머리 띠 + HP 뼈다귀) · 손패 배지 · 뒷면 부채꼴 (10-ui.md 2-1 HUD v2 「상대 이름표」 · 3절 · 6절).
    /// 머리 띠 색 = 상태 (턴 노랑 > 찹츄 분홍 > 보통 민트). 값을 받아 표시만 한다.
    /// 어느 플레이어(actorNumber)의 좌석인지는 모른다 — 대응은 OpponentSeatsPresenter 가 한다 (02-player.md 3절).
    /// 탭은 대상 선택 중(SetTargetable)에만 받는다 — 평소에는 탭 영역의 레이캐스트를 꺼서 덱 등 겹치는 입력을 가리지 않는다.
    /// 캐릭터 그림은 아직 없다(기능 8P) — 자리만 두고 스프라이트를 끼운다. 사망 · 사망 대기 표시는 기능 7 에서.
    /// </summary>
    public class OpponentSeatView : MonoBehaviour, IPointerClickHandler
    {
        [Header("이름표")]
        [SerializeField] private Image band;
        [SerializeField] private Image dot;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private GameObject crown;
        [SerializeField] private BoneGaugeView bone;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("HP 가 이 값 이하면 수치를 진한 빨강으로 (시안: 20).")]
        [SerializeField] private int lowHpThreshold = 20;
        [SerializeField] private Color hpInk = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color lowHpInk = new Color32(0xb5, 0x12, 0x1f, 0xFF);
        [SerializeField] private Color normalBandColor = new Color32(0xcf, 0xee, 0xdd, 0xFF);
        [SerializeField] private Color turnBandColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color chapchuBandColor = new Color32(0xff, 0x4f, 0x8b, 0xFF);
        [SerializeField] private HandCountBadgeView handBadge;

        [Header("캐릭터")]
        [Tooltip("캐릭터 그림 자리 — 그림이 오면 스프라이트만 끼운다 (19-character-items).")]
        [SerializeField] private Image character;
        [Tooltip("턴일 때 발밑 노란 타원.")]
        [SerializeField] private GameObject turnSpot;
        [Tooltip("대상 선택 중 발밑 빨간 고리 + 이름표 둘레 빨간 테.")]
        [SerializeField] private GameObject pickRing;
        [SerializeField] private GameObject pickOutline;
        [Tooltip("찹츄 선언 말풍선 (기능 8).")]
        [SerializeField] private GameObject shout;
        [Tooltip("뒷면 부채꼴 — 캐릭터 손 자리.")]
        [SerializeField] private OpponentHandView hand;
        [Tooltip("좌석 전체를 덮는 투명 탭 영역. 대상 선택 중에만 레이캐스트를 켠다.")]
        [SerializeField] private Image tapArea;

        private bool _isTurn;
        private bool _chapchu;

        /// <summary>대상 선택 중 이 좌석을 탭했다.</summary>
        public event Action<OpponentSeatView> OnClicked;

        private void Awake()
        {
            if (band == null || dot == null || nicknameText == null || crown == null || bone == null || hpText == null || handBadge == null || character == null
                || turnSpot == null || pickRing == null || pickOutline == null || shout == null || hand == null || tapArea == null)
                Debug.LogError($"[{nameof(OpponentSeatView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetNickname(string nickname) => nicknameText.text = nickname;

        /// <summary>목걸이 색 — 머리 띠의 점.</summary>
        public void SetColor(Color necklace) => dot.color = necklace;

        /// <summary>손패 장수 — 배지와 뒷면 부채꼴을 같이 맞춘다.</summary>
        public void SetHandCount(int count)
        {
            handBadge.SetCount(count);
            hand.SetCount(count);
        }

        public void SetHp(int hp, int maxHp)
        {
            bone.SetHp(hp, maxHp);
            hpText.text = hp.ToString();
            hpText.color = hp <= lowHpThreshold ? lowHpInk : hpInk;
        }

        /// <summary>찹츄 선언 — 왕관 · 분홍 띠 · 캐릭터 옆 「찹츄!」 말풍선.</summary>
        public void SetChapchu(bool declared)
        {
            _chapchu = declared;
            crown.SetActive(declared);
            shout.SetActive(declared);
            RefreshBand();
        }

        /// <summary>이 플레이어의 턴 — 머리 띠 노랑 + 발밑 노란 타원 (02-player.md 5절).</summary>
        public void SetTurn(bool isTurn)
        {
            _isTurn = isTurn;
            turnSpot.SetActive(isTurn);
            RefreshBand();
        }

        /// <summary>대상으로 고를 수 있는 상태 — 발밑 빨간 고리 · 이름표 빨간 테를 켜고 탭을 받는다 (10-ui.md 6절).</summary>
        public void SetTargetable(bool targetable)
        {
            pickRing.SetActive(targetable);
            pickOutline.SetActive(targetable);
            tapArea.raycastTarget = targetable;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke(this);
        }

        private void RefreshBand() => band.color = _isTurn ? turnBandColor : _chapchu ? chapchuBandColor : normalBandColor;
    }
}
