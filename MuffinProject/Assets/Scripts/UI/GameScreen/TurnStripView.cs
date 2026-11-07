using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>「나의 턴 / ○○ 차례 / 멈춤」 칩의 상태.</summary>
    public enum TurnNowState
    {
        Other, // 흰 바탕 「○○ 차례」
        Me,    // 노랑 「나의 턴」
        Halt,  // 주황 「멈춤 · ○○ 사망 대기」 (기능 7)
    }

    /// <summary>
    /// 화면 위 턴 순서 띠 (10-ui.md 2-1 HUD v2 「턴 순서 띠」) — 방향 화살표 · 플레이어 칩(목걸이 색 점 + 이름, 발자국으로 구분) · 지금 차례 칩 · 20초 타이머.
    /// 칩은 템플릿을 복제해 만든다 (구조: 루트 → Chapchu · Border · Fill · Dot/Fill · Name). 값을 받아 그리기만 하고, 누가 몇 번째인지는 프레젠터가 정한다.
    /// 턴 방향은 한 방향뿐이다 (03-turn.md 에 방향 개념이 없어 「반대로」는 두지 않는다).
    /// </summary>
    public class TurnStripView : MonoBehaviour
    {
        private const string MyTurnText = "나의 턴";
        private const string OtherTurnFormat = "{0} 차례";

        [Header("플레이어 칩")]
        [SerializeField] private RectTransform rail;
        [Tooltip("복제할 칩 — 비활성으로 둔다. 구조: 루트 → Chapchu(분홍 테) · Border(잉크 테) · Fill(노랑 바탕) · Dot(잉크 고리 → Fill 목걸이 색) · Name.")]
        [SerializeField] private RectTransform chipTemplate;
        [Tooltip("칩 사이 발자국 — 비활성으로 둔다.")]
        [SerializeField] private RectTransform separatorTemplate;

        [Header("지금 차례")]
        [SerializeField] private Image nowChip;
        [SerializeField] private TMP_Text nowText;
        [SerializeField] private Color nowOtherColor = Color.white;
        [SerializeField] private Color nowMeColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color nowHaltColor = new Color32(0xff, 0x9f, 0x1a, 0xFF);

        [Header("타이머")]
        [Tooltip("남은 초 칩 전체 — 턴이 없을 때 숨긴다.")]
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private Image timerChip;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private GameObject pauseIcon;
        [Tooltip("남은 초가 이 값 이하면 빨강 (시안: 5).")]
        [SerializeField] private int urgentSeconds = 5;
        [SerializeField] private Color timerColor = Color.white;
        [SerializeField] private Color timerInk = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color urgentColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color urgentInk = Color.white;
        [SerializeField] private Color pausedInk = new Color32(0x8a, 0x81, 0x7a, 0xFF);

        private readonly List<Chip> _chips = new List<Chip>();
        private readonly List<GameObject> _separators = new List<GameObject>();

        private void Awake()
        {
            if (rail == null || chipTemplate == null || separatorTemplate == null || nowChip == null || nowText == null || timerRoot == null || timerChip == null || timerText == null || pauseIcon == null)
                Debug.LogError($"[{nameof(TurnStripView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>칩을 순서대로 다시 만든다 — 이름 · 목걸이 색.</summary>
        public void SetPlayers(IReadOnlyList<(string name, Color color)> players)
        {
            foreach (Chip chip in _chips)
                Destroy(chip.Root.gameObject);
            foreach (GameObject separator in _separators)
                Destroy(separator);
            _chips.Clear();
            _separators.Clear();

            for (int i = 0; i < players.Count; i++)
            {
                if (i > 0)
                {
                    GameObject separator = Instantiate(separatorTemplate.gameObject, rail);
                    separator.SetActive(true);
                    _separators.Add(separator);
                }

                var chip = new Chip(Instantiate(chipTemplate, rail));
                chip.Root.gameObject.SetActive(true);
                chip.Name.text = players[i].name;
                chip.Dot.color = players[i].color;
                chip.SetCurrent(false);
                _chips.Add(chip);
            }
        }

        /// <summary>지금 차례인 칩만 노랑 · 크게. 음수면 전부 끈다.</summary>
        public void SetCurrent(int index)
        {
            for (int i = 0; i < _chips.Count; i++)
                _chips[i].SetCurrent(i == index);
        }

        /// <summary>찹츄 선언한 칩에 분홍 테 (기능 8 에서 쓴다).</summary>
        public void SetChapchu(int index, bool declared)
        {
            if (index >= 0 && index < _chips.Count)
                _chips[index].Chapchu.SetActive(declared);
        }

        /// <summary>「나의 턴」(노랑) · 「○○ 차례」(흰) · 「멈춤 …」(주황).</summary>
        public void SetNow(TurnNowState state, string playerName)
        {
            nowChip.color = state == TurnNowState.Me ? nowMeColor : state == TurnNowState.Halt ? nowHaltColor : nowOtherColor;
            nowText.text = state == TurnNowState.Me ? MyTurnText : string.Format(OtherTurnFormat, playerName);
        }

        /// <summary>남은 초. 5초 이하 빨강, 멈춤이면 회색 + ‖. 프레젠터가 마감 시각에서 계산해 넘긴다.</summary>
        public void SetTimer(int seconds, bool paused)
        {
            timerText.text = seconds.ToString();
            pauseIcon.SetActive(paused);

            bool urgent = !paused && seconds <= urgentSeconds;
            timerChip.color = urgent ? urgentColor : timerColor;
            timerText.color = paused ? pausedInk : urgent ? urgentInk : timerInk;
        }

        public void SetTimerVisible(bool visible) => timerRoot.SetActive(visible);

        // 칩 하나의 부품 — 템플릿 구조(루트 → Chapchu · Border · Fill · Dot/Fill · Name)에서 이름으로 찾는다.
        private class Chip
        {
            public readonly RectTransform Root;
            public readonly GameObject Fill;
            public readonly GameObject Chapchu;
            public readonly Image Dot;
            public readonly TMP_Text Name;
            private readonly Image _border;
            private readonly LayoutElement _size;

            public Chip(RectTransform root)
            {
                Root = root;
                _border = root.Find("Border").GetComponent<Image>();
                _size = root.GetComponent<LayoutElement>();
                Fill = root.Find("Fill").gameObject;
                Chapchu = root.Find("Chapchu").gameObject;
                Dot = root.Find("Dot/Fill").GetComponent<Image>();
                Name = root.Find("Name").GetComponent<TMP_Text>();
            }

            // 지금 차례: 테두리 + 노랑 바탕 + 큰 글자 (시안 .pn2.cur — 높이 29 → 58 · 글자 16 → 32). 아니면 투명 · 작은 글자 (24 → 48 · 14 → 28).
            public void SetCurrent(bool current)
            {
                _border.enabled = current;
                Fill.SetActive(current);
                Name.fontSize = current ? 32f : 28f;
                _size.preferredHeight = current ? 58f : 48f;
            }
        }
    }
}
