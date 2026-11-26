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
    /// 상대 좌석 하나 — 머리 위 이름표(이름 → 손패 카드 · 하트 HP → HP 막대) · 캐릭터 · 손의 뒷면 부채꼴 (통합 화면 UI v3 「상대 머리 위」 .tftp).
    /// 이름 칸 = 상태 (사망 > 겨눔 > 사망 대기 > 턴 > 찹츄 > 보통 — 보통은 흰 테두리 글자만). 값을 받아 표시만 한다.
    /// 어느 플레이어(actorNumber)의 좌석인지는 모른다 — 대응은 OpponentSeatsPresenter 가 한다 (02-player.md 3절).
    /// 탭은 대상 선택 중(SetTargetable)에만 받는다 — 평소에는 탭 영역의 레이캐스트를 꺼서 덱 등 겹치는 입력을 가리지 않는다.
    /// </summary>
    public class OpponentSeatView : MonoBehaviour, IPointerClickHandler
    {
        private const string PendingHpText = "대기";
        private const string DeadHpText = "사망";
        private const string PendingTagFormat = "사망 대기 {0}초";
        private const float FillOverhang = 8f; // 채움의 왼쪽 둥근 끝을 막대 마스크 밖으로 빼는 길이 — 왼쪽 끝은 각지게 보인다 (시안 .tg i)

        [Header("머리 위 이름표")]
        [Tooltip("이름표 전체 — 겨눌 때 키운다 (시안 .tftp.aim scale 1.1).")]
        [SerializeField] private RectTransform plate;
        [Tooltip("사망이면 흐리게 (시안 .tftp.dead opacity .6).")]
        [SerializeField] private CanvasGroup plateGroup;
        [Tooltip("이름 칸 바탕 + 잉크 테 — 상태에 따라 칠한다. 보통은 투명.")]
        [SerializeField] private UIShape nameTag;
        [SerializeField] private TMP_Text nicknameText;
        [Tooltip("이름 칸이 투명할 때의 글자 머티리얼 — 흰 테두리 (시안 -webkit-text-stroke #fff).")]
        [SerializeField] private Material nameOutlineMaterial;
        [Tooltip("이름 칸이 칠해졌을 때의 글자 머티리얼 — 테두리 없음.")]
        [SerializeField] private Material namePlainMaterial;
        [SerializeField] private GameObject crown;
        [Tooltip("대상 선택 중 이름 칸 둘레 빨간 점선 (시안 .tftp.pick .tn outline).")]
        [SerializeField] private GameObject pickOutline;
        [SerializeField] private HandCountBadgeView handBadge;
        [SerializeField] private Graphic heart;
        [SerializeField] private TMP_Text hpText;
        [Tooltip("사망 · 대기 글자 크기 (시안 .tftp.dead .hpI b 16 × 2).")]
        [SerializeField] private float lifeHpFontSize = 32f;
        [Tooltip("HP 막대 바탕 — 사망이면 흑백.")]
        [SerializeField] private Graphic hpBar;
        [Tooltip("HP 막대 채움 — 왼쪽 기준, 폭 = 비율 × maxFillWidth.")]
        [SerializeField] private RectTransform hpFill;
        [SerializeField] private Graphic hpFillGraphic;
        [SerializeField] private float maxFillWidth = 184f;
        [Tooltip("HP 가 이 값 이하면 수치를 빨강으로 (시안: 20).")]
        [SerializeField] private int lowHpThreshold = 20;

        [Header("색 (통합 화면 UI v3)")]
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [SerializeField] private Color hpLowInk = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color turnColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private Color chapchuColor = new Color32(0xff, 0x4f, 0x8b, 0xFF);
        [SerializeField] private Color pendingColor = new Color32(0xff, 0x9f, 0x1a, 0xFF);
        [SerializeField] private Color aimColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [SerializeField] private Color deadTagColor = new Color32(0xdd, 0xd6, 0xcf, 0xFF);
        [SerializeField] private Color hpHighColor = new Color32(0x3c, 0xbf, 0x72, 0xFF);
        [SerializeField] private Color hpMidColor = new Color32(0xff, 0x9f, 0x1a, 0xFF);
        [SerializeField] private Color hpLowColor = new Color32(0xec, 0x3b, 0x46, 0xFF);
        [Tooltip("사망 · 사망 대기 캐릭터 색 (시안 .char.dead grayscale opacity .45 · pend brightness .6).")]
        [SerializeField] private Color deadCharacterColor = new Color(0.8f, 0.8f, 0.8f, 0.45f);
        [SerializeField] private Color pendingCharacterColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        [Tooltip("사망이면 하트 · HP 막대 · 캐릭터를 흑백으로 (시안 .tftp.dead · .char.dead grayscale 1) — Chapchu/UI Gray.")]
        [SerializeField] private Material grayMaterial;

        [Header("캐릭터")]
        [Tooltip("캐릭터 그림 자리 — 그림이 오면 스프라이트만 끼운다 (19-character-items).")]
        [SerializeField] private Image character;
        [Tooltip("턴인 캐릭터 배율 — 발밑 기준 (시안 .char.turn 1.05).")]
        [SerializeField] private float turnCharacterScale = 1.05f;
        [Tooltip("겨눈 캐릭터 배율 (시안 .char.aim 1.08).")]
        [SerializeField] private float aimCharacterScale = 1.08f;
        [Tooltip("턴일 때 캐릭터 머리 옆 스톱워치 (시안 .swatch). 값은 TurnTimerPresenter 가 넣는다.")]
        [SerializeField] private GameObject turnTimer;
        [Tooltip("대상 선택 중 발밑 빨간 점선 타원 (시안 .ring).")]
        [SerializeField] private GameObject pickRing;
        [Tooltip("찹츄 선언 말풍선 (기능 8).")]
        [SerializeField] private GameObject shout;
        [Tooltip("뒷면 부채꼴 — 캐릭터 손 자리.")]
        [SerializeField] private OpponentHandView hand;
        [Tooltip("캐릭터 가슴의 「사망 대기 n초」 · 「사망」 팻말 (시안 .dtag).")]
        [SerializeField] private GameObject lifeTag;
        [SerializeField] private Graphic lifeTagFill;
        [SerializeField] private TMP_Text lifeTagText;
        [Tooltip("사망 대기 중 머리 옆 땀방울 (시안 .sweat).")]
        [SerializeField] private GameObject sweat;
        [Tooltip("좌석 전체를 덮는 투명 탭 영역. 대상 선택 중에만 레이캐스트를 켠다.")]
        [SerializeField] private Image tapArea;
        [Tooltip("고를 수 있는 동안 가림판 위로 올리는 하위 캔버스 — 뒤에서 앞 순서 (캐릭터 · 링 · 부채꼴 · 이름표 · 탭 영역) (시안 .char.pick 31 · .tftp.pick 32 > .dimmer 30).")]
        [SerializeField] private Canvas[] pickLayers;

        private bool _isTurn;
        private bool _chapchu;
        private bool _targetable;
        private bool _aimed;
        private LifeState _life;
        private int _hp = GameServer.MaxHp;
        private float _hpFontSize;
        private int _pendingSeconds = -1;

        /// <summary>대상 선택 중 이 좌석을 탭했다.</summary>
        public event Action<OpponentSeatView> OnClicked;

        private void Awake()
        {
            if (plate == null || plateGroup == null || nameTag == null || nicknameText == null || nameOutlineMaterial == null || namePlainMaterial == null || crown == null
                || pickOutline == null || handBadge == null || heart == null || hpText == null || hpBar == null || hpFill == null || hpFillGraphic == null || grayMaterial == null || character == null
                || turnTimer == null || pickRing == null || shout == null || hand == null || lifeTag == null || lifeTagFill == null || lifeTagText == null || sweat == null || tapArea == null
                || pickLayers == null || pickLayers.Length == 0)
                Debug.LogError($"[{nameof(OpponentSeatView)}] 참조 중 비어 있는 것이 있습니다.", this);
            else
                _hpFontSize = hpText.fontSize;
        }

        public void SetNickname(string nickname) => nicknameText.text = nickname;

        /// <summary>뒷면 부채꼴의 화면 좌표 — 이 좌석이 낸 카드가 출발하는 자리. Overlay 캔버스라 월드 위치가 곧 화면 픽셀이다.</summary>
        public Vector2 HandScreenPoint => hand.transform.position;

        /// <summary>캐릭터 가운데(화면 좌표) — 1021 판정 표가 뜨는 자리.</summary>
        public Vector2 CharacterScreenPoint => character.rectTransform.TransformPoint(character.rectTransform.rect.center);

        /// <summary>손패 장수 — 손패 카드 그림과 뒷면 부채꼴을 같이 맞춘다.</summary>
        public void SetHandCount(int count)
        {
            handBadge.SetCount(count);
            hand.SetCount(count);
        }

        public void SetHp(int hp, int maxHp)
        {
            _hp = Mathf.Clamp(hp, 0, maxHp);
            RefreshHp(maxHp);
        }

        /// <summary>찹츄 선언 — 분홍 이름 칸 + 왕관 · 캐릭터 옆 「찹츄!」 말풍선.</summary>
        public void SetChapchu(bool declared)
        {
            _chapchu = declared;
            Refresh();
        }

        /// <summary>이 플레이어의 턴 — 노란 이름 칸 + 머리 옆 스톱워치 (02-player.md 5절).</summary>
        public void SetTurn(bool isTurn)
        {
            _isTurn = isTurn;
            Refresh();
        }

        /// <summary>대상으로 고를 수 있는 상태 — 발밑 빨간 타원 · 이름 칸 빨간 점선을 켜고 탭을 받는다 (10-ui.md 6절).</summary>
        public void SetTargetable(bool targetable)
        {
            _targetable = targetable;
            Refresh();
        }

        /// <summary>끄는 카드가 이 좌석 위에 있다 — 놓으면 이 상대가 대상이라는 표시 (10-ui.md 4절). 빨간 이름 칸 + 발밑 타원, 탭은 받지 않는다.</summary>
        public void SetAimed(bool aimed)
        {
            _aimed = aimed;
            Refresh();
        }

        /// <summary>살아 있음 · 사망 대기 · 최종 사망 (06-death.md) — 이름 칸 · HP 자리 · 캐릭터 팻말.</summary>
        public void SetLife(LifeState life)
        {
            _life = life;
            _pendingSeconds = -1;
            Refresh();
            RefreshHp(GameServer.MaxHp);
        }

        /// <summary>사망 대기 남은 초 — 팻말 「사망 대기 n초」. 프레젠터가 매 프레임 넘기고, 초가 바뀔 때만 글자를 바꾼다.</summary>
        public void SetPendingSeconds(int seconds)
        {
            if (_life != LifeState.DeathPending || seconds == _pendingSeconds) return;

            _pendingSeconds = seconds;
            lifeTagText.text = string.Format(PendingTagFormat, seconds);
        }

        /// <summary>이 화면 좌표가 좌석(캐릭터 · 이름표를 덮는 탭 영역) 위인가.</summary>
        public bool ContainsScreenPoint(Vector2 screenPoint) => RectTransformUtility.RectangleContainsScreenPoint(tapArea.rectTransform, screenPoint, null);

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;

            OnClicked?.Invoke(this);
        }

        // 이름 칸 · 캐릭터 · 발밑 · 팻말을 상태대로 (시안 우선순위: 사망 > 겨눔 > 사망 대기 > 턴 > 찹츄 > 보통)
        private void Refresh()
        {
            bool dead = _life == LifeState.Dead;
            bool pending = _life == LifeState.DeathPending;
            bool aim = _aimed && !dead;
            bool pick = _targetable && !dead;

            bool chapchuTag = !dead && !aim && !pending && !_isTurn && _chapchu;
            Color tag = dead ? Color.clear : aim ? aimColor : pending ? pendingColor : _isTurn ? turnColor : chapchuTag ? chapchuColor : Color.clear;
            bool filled = tag.a > 0f;
            nameTag.color = tag;
            nameTag.BorderColor = filled ? ink : Color.clear;
            nicknameText.fontSharedMaterial = filled ? namePlainMaterial : nameOutlineMaterial;
            nicknameText.color = aim || chapchuTag ? Color.white : ink;
            crown.SetActive(_chapchu && !dead);
            pickOutline.SetActive(pick && !aim);
            tapArea.raycastTarget = pick;
            plate.localScale = Vector3.one * (aim ? 1.1f : 1f);
            // 사망 — 흐리게 + 흑백. 이름 줄긋기는 시안 화면에 그려지지 않아 넣지 않는다 (inline-flex 라 text-decoration 이 안 보임)
            plateGroup.alpha = dead ? 0.6f : 1f;
            handBadge.gameObject.SetActive(!dead);
            hand.gameObject.SetActive(!dead); // 06-health 9절 「손패 · 함정 슬롯 0 으로 표시」 — 부채꼴도 비운다 (시안: 사망이면 0장)
            Material gray = dead ? grayMaterial : null;
            heart.material = gray;
            hpBar.material = gray;
            character.material = gray;

            character.color = dead ? deadCharacterColor : pending ? pendingCharacterColor : Color.white;
            character.rectTransform.localScale = Vector3.one * (dead ? 1f : aim ? aimCharacterScale : _isTurn ? turnCharacterScale : 1f);
            turnTimer.SetActive(_isTurn && !dead);
            // 층은 링이 켜진 동안에 올리고 내린다 — 꺼진 캔버스는 rootCanvas 를 못 읽고 값도 바뀌지 않는다.
            if (!pick)
                SetLayersRaised(false);
            pickRing.SetActive(pick || aim);
            if (pick)
                SetLayersRaised(true);
            shout.SetActive(_chapchu && !pending && !dead);
            sweat.SetActive(pending);
            lifeTag.SetActive(dead || pending);
            lifeTagFill.color = dead ? deadTagColor : pendingColor;
            if (dead)
                lifeTagText.text = DeadHpText;
        }

        private void SetLayersRaised(bool raised)
        {
            for (int i = 0; i < pickLayers.Length; i++)
            {
                pickLayers[i].overrideSorting = raised;
                if (raised)
                    pickLayers[i].sortingOrder = pickLayers[i].rootCanvas.sortingOrder + 1 + i;
            }
        }

        private void RefreshHp(int maxHp)
        {
            bool dead = _life == LifeState.Dead;
            bool pending = _life == LifeState.DeathPending;
            int shown = dead || pending ? 0 : _hp;
            float fraction = maxHp > 0 ? (float)shown / maxHp : 0f;

            hpText.text = dead ? DeadHpText : pending ? PendingHpText : _hp.ToString();
            hpText.fontSize = dead ? lifeHpFontSize : _hpFontSize; // 시안 .tftp.dead .hpI b 16 × 2 — 「대기」 는 숫자 크기 그대로
            hpText.rectTransform.sizeDelta = new Vector2(hpText.rectTransform.sizeDelta.x, hpText.fontSize); // 줄 높이 = 글자 크기 (시안 line-height 1) — 사망이면 이름표가 줄어든다
            hpText.color = !dead && !pending && _hp > 0 && _hp <= lowHpThreshold ? hpLowInk : ink;
            hpFill.sizeDelta = new Vector2(FillOverhang + maxFillWidth * fraction, hpFill.sizeDelta.y);
            int percent = Mathf.RoundToInt(fraction * 100f);
            hpFillGraphic.color = percent > 50 ? hpHighColor : percent > 20 ? hpMidColor : hpLowColor;
        }
    }
}
