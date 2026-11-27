using System;
using Chapchu.Game;
using Chapchu.UI.Fx;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 왼쪽 아래 내 HUD (통합 화면 UI v3 .mehud) — 닉네임 + 「나의 턴」(찹츄면 「찹츄!」) 이름표 · 하트 HP · 손패 카드 · HP 막대, 그 위 「찹츄까지 n장」 (.mgoal).
    /// 큰 캐릭터 · 머리 옆 스톱워치는 따로 있고 여기서 켜고 끈다. 값을 받아 표시만 한다.
    /// 나를 포함하는 카드(OneAny)의 대상 선택 중에는 빨간 테 + 탭으로 나를 고른다 (10-ui.md 6절). 평소에는 탭 영역의 레이캐스트를 끈다.
    /// </summary>
    public class MyPanelView : MonoBehaviour, IPointerClickHandler
    {
        private const string TurnTagText = "나의 턴";
        private const string ChapchuTagText = "찹츄!";
        private const string GoalFormat = "찹츄까지 <size={1}><color=#{2}>{0}</color></size> 장";   // 시안 .mgoal — 「찹츄까지 n장」, 글자 사이 틈(gap 4 × 2)은 빈칸으로
        private const string GoalOverFormat = "10장에서 <size={1}><color=#{2}>+{0}</color></size>"; // 10장을 넘으면
        private const string GoalTenText = "찹츄!";
        private const string DeadHpText = "사망";
        private const int ChapchuCount = GameServer.ChapchuHandCount;

        [Header("이름 줄")]
        [SerializeField] private TMP_Text nicknameText;
        [Tooltip("닉네임 글의 레이아웃 — 폭을 글 너비(최대 maxNicknameWidth)로 맞춘다. 넘치면 … 로 줄인다.")]
        [SerializeField] private LayoutElement nicknameLayout;
        [Tooltip("닉네임 최대 폭 — 넘치면 … (내 HUD 폭 384 에서 이름표 자리를 뺀 값).")]
        [SerializeField] private float maxNicknameWidth = 240f;
        [Tooltip("「나의 턴」 · 「찹츄!」 이름표 (시안 .mtag).")]
        [SerializeField] private GameObject turnTag;
        [SerializeField] private Graphic turnTagFill;
        [SerializeField] private TMP_Text turnTagText;
        [SerializeField] private GameObject turnTagCrown;
        [Tooltip("내 캐릭터 머리 옆 스톱워치 — 내 턴에만 켠다 (시안 .swatch.me). 값은 TurnTimerPresenter 가 넣는다.")]
        [SerializeField] private GameObject turnTimer;

        [Header("HP · 손패")]
        [SerializeField] private TMP_Text hpText;
        [Tooltip("「사망」 글자 크기.")]
        [SerializeField] private float lifeHpFontSize = 48f;
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private Graphic hpFillGraphic;
        [SerializeField] private float maxFillWidth = 376f;
        [Tooltip("HP 가 이 값 이하면 수치를 빨강으로 (시안: 20).")]
        [SerializeField] private int lowHpThreshold = 20;
        [SerializeField] private HandCountBadgeView handBadge;
        [Tooltip("내 캐릭터 — 사망이면 흐리게.")]
        [SerializeField] private Image character;
        [Tooltip("내 턴 캐릭터 배율 — 발밑 기준 (시안 .char.turn 1.05).")]
        [SerializeField] private float turnCharacterScale = 1.05f;

        [Header("찹츄까지")]
        [SerializeField] private Graphic goalFill;
        [SerializeField] private TMP_Text goalText;
        [SerializeField] private GameObject goalCrown;
        [Tooltip("내게 걸린 효과 알약 — 행동 금지 · 스킵 (PM 2026-11-27).")]
        [SerializeField] private StatusChipsView status;
        [Tooltip("「찹츄까지 n장」 의 숫자 크기 (시안 .mgoal b 18 × 2).")]
        [SerializeField] private float goalNumberSize = 36f;

        [Header("색 (통합 화면 UI v3)")]
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color hpLowInk = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color turnColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color chapchuColor = new Color32(0xff, 0x4f, 0x8b, 0xFF);
        [SerializeField] private Color goalColor = new Color(1f, 1f, 1f, 0.88f);
        [SerializeField] private Color goalOverColor = new Color32(0xff, 0xf1, 0xe0, 0xFF);
        [SerializeField] private Color goalNumberColor = new Color32(0x2f, 0x7c, 0xf6, 0xFF);
        [SerializeField] private Color hpHighColor = new Color32(0x3c, 0xbf, 0x72, 0xFF);
        [SerializeField] private Color hpMidColor = new Color32(0xff, 0x9f, 0x1a, 0xFF);
        [SerializeField] private Color hpLowColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color deadCharacterColor = new Color(0.8f, 0.8f, 0.8f, 0.45f);

        [Header("대상 선택")]
        [Tooltip("나를 고를 수 있을 때 내 발밑 빨간 점선 타원 (상대 좌석의 것과 같다).")]
        [SerializeField] private GameObject pickRing;
        [Tooltip("나를 고를 수 있을 때 HUD 둘레 빨간 점선.")]
        [SerializeField] private GameObject pickOutline;
        [Tooltip("HUD 와 캐릭터를 덮는 투명 탭 영역 — 대상 선택 중에만 레이캐스트를 켠다.")]
        [SerializeField] private Image tapArea;
        [Tooltip("고르는 동안 가림판 위로 올리는 하위 캔버스 — 뒤에서 앞 순서 (캐릭터 · 링 · HUD) — 링은 캐릭터 발 위.")]
        [SerializeField] private Canvas[] pickLayers;

        /// <summary>대상 선택 중 내 HUD 를 탭했다.</summary>
        public event Action OnClicked;

        private bool _isMyTurn;
        private bool _chapchu;
        private bool _targetable;
        private bool _targeted;
        private LifeState _life;
        private int _hp = GameServer.MaxHp;
        private float _hpFontSize;

        private void Awake()
        {
            if (nicknameText == null || nicknameLayout == null || turnTag == null || turnTagFill == null || turnTagText == null || turnTagCrown == null || turnTimer == null
                || hpText == null || hpFill == null || hpFillGraphic == null || handBadge == null || character == null || goalFill == null || goalText == null || goalCrown == null || status == null
                || pickRing == null || pickOutline == null || tapArea == null || pickLayers == null || pickLayers.Length == 0)
                Debug.LogError($"[{nameof(MyPanelView)}] 참조 중 비어 있는 것이 있습니다.", this);
            else
                _hpFontSize = hpText.fontSize;
        }

        public void SetNickname(string nickname)
        {
            nicknameText.text = nickname;
            nicknameLayout.preferredWidth = Mathf.Min(nicknameText.GetPreferredValues(nickname).x, maxNicknameWidth);
        }

        public void SetHp(int hp, int maxHp)
        {
            _hp = Mathf.Clamp(hp, 0, maxHp);
            RefreshHp(maxHp);
        }

        /// <summary>손패 장수 — 손패 카드 그림 + 「찹츄까지 n장」.</summary>
        /// <summary>4 단계 공통 연출 자리 — 내 캐릭터 가운데에서 터지고, 캐릭터 · HUD 가 흔들리고, 손패 장수 칩이 톡 (시안 「4단계 공통 연출」).</summary>
        public ResultFx.Seat FxSeat => new ResultFx.Seat(character.rectTransform, new[] { character.rectTransform, (RectTransform)transform }, (RectTransform)handBadge.transform, handBadge.Face);

        public void SetHandCount(int count)
        {
            handBadge.SetCount(count);

            bool ten = count == ChapchuCount;
            bool over = count > ChapchuCount;
            string number = ColorUtility.ToHtmlStringRGB(goalNumberColor);
            goalText.text = ten ? GoalTenText
                : over ? string.Format(GoalOverFormat, count - ChapchuCount, goalNumberSize, number)
                : string.Format(GoalFormat, ChapchuCount - count, goalNumberSize, number);
            goalText.color = ten ? Color.white : ink;
            goalFill.color = ten ? chapchuColor : over ? goalOverColor : goalColor;
            goalCrown.SetActive(ten);
        }

        /// <summary>내 턴이면 「나의 턴」 이름표 · 스톱워치.</summary>
        public void SetMyTurn(bool isMyTurn)
        {
            _isMyTurn = isMyTurn;
            Refresh();
        }

        /// <summary>찹츄 선언 상태 — 이름표가 분홍 「찹츄!」 (턴보다 먼저).</summary>
        public void SetChapchu(bool declared)
        {
            _chapchu = declared;
            Refresh();
        }

        /// <summary>행동 카드 금지 (1007 짖기) — 「행동 금지」 알약.</summary>
        public void SetBlocked(bool blocked) => status.SetBlocked(blocked);

        /// <summary>다음 턴 스킵 — 「스킵」 알약. 스킵된 턴이 지나가면 뗀다 (10-ui.md 3절).</summary>
        public void SetSkip(bool skip) => status.SetSkip(skip);

        /// <summary>살아 있음 · 최종 사망 (06-health.md) — HP 자리 · 캐릭터 · 최종 사망이면 「찹츄까지」 칩을 숨긴다. 사망 대기는 살아 있을 때와 같다 — HP 0 만 (10-ui.md 2-1).</summary>
        public void SetLife(LifeState life)
        {
            _life = life;
            if (life == LifeState.Dead)
                status.Clear();
            Refresh();
            RefreshHp(GameServer.MaxHp);
        }

        /// <summary>대상으로 고를 수 있는 상태 — 발밑 빨간 타원 · HUD 빨간 점선을 켜고, 가림판 위로 올려 탭을 받는다 (10-ui.md 6절).</summary>
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
            _targetable = targetable;
            pickRing.SetActive(targetable || _targeted);
            pickOutline.SetActive(targetable || _targeted);
            tapArea.raycastTarget = targetable;
        }

        /// <summary>처리를 기다리는 체인 맨 위 카드의 대상이 나다 — 발밑 빨간 타원 · HUD 빨간 점선 (10-ui.md 5-2 #3 「대상 표시」). 층을 올리거나 탭을 받지는 않는다.</summary>
        public void SetTargeted(bool targeted)
        {
            _targeted = targeted;
            pickRing.SetActive(_targetable || targeted);
            pickOutline.SetActive(_targetable || targeted);
        }

        /// <summary>내 캐릭터 가운데(화면 좌표) — 대상 표시 점선이 닿는 자리.</summary>
        public Vector2 CharacterScreenPoint => character.rectTransform.TransformPoint(character.rectTransform.rect.center);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke();
        }

        private void Refresh()
        {
            bool alive = _life != LifeState.Dead;
            turnTag.SetActive(alive && (_chapchu || _isMyTurn));
            turnTagFill.color = _chapchu ? chapchuColor : turnColor;
            turnTagText.text = _chapchu ? ChapchuTagText : TurnTagText;
            turnTagText.color = _chapchu ? Color.white : ink;
            turnTagCrown.SetActive(_chapchu);
            turnTimer.SetActive(_isMyTurn && _life != LifeState.Dead);
            character.color = _life == LifeState.Dead ? deadCharacterColor : Color.white;
            character.rectTransform.localScale = Vector3.one * (_isMyTurn && alive ? turnCharacterScale : 1f);
            // 최종 사망이면 「찹츄까지 n장」 칩(goalFill 의 부모 = 칩 전체)을 숨긴다 — 더 모을 수 없다
            goalFill.transform.parent.gameObject.SetActive(_life != LifeState.Dead);
        }

        private void RefreshHp(int maxHp)
        {
            bool dead = _life == LifeState.Dead;
            int shown = dead ? 0 : _hp;
            float fraction = maxHp > 0 ? (float)shown / maxHp : 0f;

            hpText.text = dead ? DeadHpText : _hp.ToString();
            hpText.fontSize = dead ? lifeHpFontSize : _hpFontSize;
            hpText.color = !dead && _hp > 0 && _hp <= lowHpThreshold ? hpLowInk : ink;
            hpFill.sizeDelta = new Vector2(maxFillWidth * fraction, hpFill.sizeDelta.y);
            int percent = Mathf.RoundToInt(fraction * 100f);
            hpFillGraphic.color = percent > 50 ? hpHighColor : percent > 20 ? hpMidColor : hpLowColor;
        }
    }
}
