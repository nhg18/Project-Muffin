using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 좌하단 내 패널 — 머리 띠(목걸이 색 점 · 닉네임(나) · 왕관 · 상황 한 마디) + 몸통(HP 뼈다귀 · 수치) + 모서리 손패 배지 (10-ui.md 2-1 HUD v2 「내 패널」).
    /// 머리 띠 색 = 상태 (찹츄 분홍 > 내 턴 노랑 > 보통 민트). 값을 받아 표시만 한다. 남은 초는 여기 없다 — 턴 순서 띠.
    /// </summary>
    public class MyPanelView : MonoBehaviour
    {
        private const string WaitingStatus = "기다리는 중";

        [Header("머리 띠")]
        [SerializeField] private Image band;
        [SerializeField] private Image dot;
        [SerializeField] private TMP_Text nicknameText;
        [SerializeField] private GameObject crown;
        [SerializeField] private TMP_Text statusText;
        [Tooltip("내 캐릭터 발밑 타원 — 내 턴에만 켠다 (시안 .spot).")]
        [SerializeField] private GameObject turnSpot;
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

        private bool _isMyTurn;
        private bool _chapchu;

        private void Awake()
        {
            if (band == null || dot == null || nicknameText == null || crown == null || statusText == null || turnSpot == null || bone == null || hpText == null || handBadge == null)
                Debug.LogError($"[{nameof(MyPanelView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetNickname(string nickname) => nicknameText.text = nickname;

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
            RefreshBand();
        }

        /// <summary>찹츄 선언 상태 — 왕관 + 분홍 머리 띠 (기능 8 에서 쓴다).</summary>
        public void SetChapchu(bool declared)
        {
            _chapchu = declared;
            crown.SetActive(declared);
            RefreshBand();
        }

        private void RefreshBand() => band.color = _chapchu ? chapchuBandColor : _isMyTurn ? turnBandColor : normalBandColor;
    }
}
