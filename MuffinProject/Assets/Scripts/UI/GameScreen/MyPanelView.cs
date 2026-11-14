using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 좌하단 내 패널 — 머리 띠(목걸이 색 점 · 닉네임(나) · 왕관 · 상황 한 마디) + 몸통(HP 뼈다귀 · 수치) + 모서리 손패 배지 (10-ui.md 2-1 HUD v2 「내 패널」).
    /// 머리 띠 색 = 상태 (찹츄 분홍 > 내 턴 노랑 > 보통 민트). 값을 받아 표시만 한다. 남은 초는 여기 없다 — 턴 순서 띠.
    /// 나를 포함하는 카드(OneAny)의 대상 선택 중에는 빨간 테 + 탭으로 나를 고른다 (10-ui.md 6절). 평소에는 탭 영역의 레이캐스트를 끈다.
    /// </summary>
    public class MyPanelView : MonoBehaviour, IPointerClickHandler
    {
        private const string WaitingStatus = "기다리는 중";

        [Header("머리 띠")]
        [SerializeField] private Image band;
        [SerializeField] private Image dot;
        [SerializeField] private TMP_Text nicknameText;
        [Tooltip("닉네임 글의 레이아웃 — 폭을 글 너비(최대 maxNicknameWidth)로 맞춘다. 상황 글이 남은 폭을 쓰고 넘치면 … 로 줄인다.")]
        [SerializeField] private LayoutElement nicknameLayout;
        [Tooltip("닉네임 최대 폭 — 넘치면 … (시안 .mine2 .nk span max-width 90 × 2).")]
        [SerializeField] private float maxNicknameWidth = 180f;
        [SerializeField] private GameObject crown;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("내 캐릭터 발밑 타원 — 내 턴에만 켠다 (시안 .spot).")]
        [SerializeField] private GameObject turnSpot;
        [Tooltip("내 캐릭터 머리 옆 스톱워치 — 내 턴에만 켠다 (시안 .swatch). 값은 TurnTimerPresenter 가 넣는다.")]
        [SerializeField] private GameObject turnTimer;
        [SerializeField] private Color normalBandColor = new Color32(0xcf, 0xee, 0xdd, 0xFF);
        [SerializeField] private Color turnBandColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color chapchuBandColor = new Color32(0xff, 0x4f, 0x8b, 0xFF);

        [Header("몸통")]
        [SerializeField] private BoneGaugeView bone;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("HP 가 이 값 이하면 수치를 진한 빨강으로 (시안: 20).")]
        [SerializeField] private int lowHpThreshold = 20;
        [SerializeField] private Color hpInk = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color lowHpInk = new Color32(0xb5, 0x12, 0x1f, 0xFF);
        [SerializeField] private HandCountBadgeView handBadge;

        [Header("대상 선택")]
        [Tooltip("나를 고를 수 있을 때 내 발밑 빨간 링 (상대 좌석의 링과 같다).")]
        [SerializeField] private GameObject pickRing;
        [Tooltip("나를 고를 수 있을 때 패널 둘레 빨간 테 (상대 이름판의 테와 같다).")]
        [SerializeField] private GameObject pickOutline;
        [Tooltip("패널 전체를 덮는 투명 탭 영역 — 대상 선택 중에만 레이캐스트를 켠다.")]
        [SerializeField] private Image tapArea;
        [Tooltip("고르는 동안 가림판 위로 올리는 하위 캔버스 — 뒤에서 앞 순서 (캐릭터 · 링 · 패널) — 링은 캐릭터 발 위.")]
        [SerializeField] private Canvas[] pickLayers;

        /// <summary>대상 선택 중 내 패널을 탭했다.</summary>
        public event Action OnClicked;

        private bool _isMyTurn;
        private bool _chapchu;

        private void Awake()
        {
            if (band == null || dot == null || nicknameText == null || nicknameLayout == null || crown == null || statusText == null || turnSpot == null || turnTimer == null || bone == null || hpText == null || handBadge == null || pickRing == null || pickOutline == null || tapArea == null || pickLayers == null || pickLayers.Length == 0)
                Debug.LogError($"[{nameof(MyPanelView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetNickname(string nickname)
        {
            nicknameText.text = nickname;
            nicknameLayout.preferredWidth = Mathf.Min(nicknameText.GetPreferredValues(nickname).x, maxNicknameWidth);
        }

        public void SetColor(Color necklace) => dot.color = necklace;

        public void SetHp(int hp, int maxHp)
        {
            bone.SetHp(hp, maxHp);
            hpText.text = hp.ToString();
            hpText.color = hp <= lowHpThreshold ? lowHpInk : hpInk;
        }

        public void SetHandCount(int count) => handBadge.SetCount(count);

        /// <summary>내 턴이면 머리 띠 노랑 · 상황 문구 없음, 아니면 「기다리는 중」.</summary>
        public void SetMyTurn(bool isMyTurn)
        {
            _isMyTurn = isMyTurn;
            statusText.text = isMyTurn ? string.Empty : WaitingStatus;
            turnSpot.SetActive(isMyTurn);
            turnTimer.SetActive(isMyTurn);
            RefreshBand();
        }

        /// <summary>찹츄 선언 상태 — 왕관 + 분홍 머리 띠 (기능 8 에서 쓴다).</summary>
        public void SetChapchu(bool declared)
        {
            _chapchu = declared;
            crown.SetActive(declared);
            RefreshBand();
        }

        /// <summary>대상으로 고를 수 있는 상태 — 발밑 빨간 링 · 패널 빨간 테를 켜고, 가림판 위로 올려 탭을 받는다 (10-ui.md 6절).</summary>
        public void SetTargetable(bool targetable)
        {
            // 켤 때는 먼저 켜고 층을 올린다(rootCanvas 를 읽으려면 켜져 있어야 한다), 끌 때는 층을 내린 뒤 끈다 — 꺼진 캔버스의 값은 바뀌지 않는다.
            if (targetable)
                pickRing.SetActive(true);
            for (int i = 0; i < pickLayers.Length; i++)
            {
                pickLayers[i].overrideSorting = targetable;
                if (targetable)
                    pickLayers[i].sortingOrder = pickLayers[i].rootCanvas.sortingOrder + 1 + i;
            }
            pickRing.SetActive(targetable);
            pickOutline.SetActive(targetable);
            tapArea.raycastTarget = targetable;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke();
        }

        private void RefreshBand() => band.color = _chapchu ? chapchuBandColor : _isMyTurn ? turnBandColor : normalBandColor;
    }
}
