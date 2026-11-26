using System.Collections.Generic;
using Chapchu.UI.Fx;
using Chapchu.UI.GameScreen;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>시상대 한 칸에 그릴 값 — ResultPresenter 가 종료 요약에서 만든다.</summary>
    public readonly struct ResultPodiumInfo
    {
        public readonly string Name;
        public readonly Color Color;
        /// <summary>순위 (1부터). 시상대 높이 · 색은 4위까지만 다르다.</summary>
        public readonly int Rank;
        public readonly bool IsTied;
        public readonly bool IsWinner;
        public readonly bool IsMe;
        /// <summary>탈락 · 이탈 — 캐릭터가 회색으로 기울고 HP 는 빈 뼈다귀 0.</summary>
        public readonly bool IsOut;
        public readonly int Hp;
        public readonly int MaxHp;
        /// <summary>「탈락 · N턴」 — 없으면 null.</summary>
        public readonly string Stat;
        /// <summary>「○○에게 당함」 — 없으면 null.</summary>
        public readonly string Sub;
        /// <summary>찹츄로 이겼다 — 캐릭터 옆 「찹츄 승리!」 마크.</summary>
        public readonly bool HasWinMark;

        public ResultPodiumInfo(string name, Color color, int rank, bool isTied, bool isWinner, bool isMe, bool isOut, int hp, int maxHp, string stat, string sub, bool hasWinMark)
        {
            Name = name;
            Color = color;
            Rank = rank;
            IsTied = isTied;
            IsWinner = isWinner;
            IsMe = isMe;
            IsOut = isOut;
            Hp = hp;
            MaxHp = maxHp;
            Stat = stat;
            Sub = sub;
            HasWinMark = hasWinMark;
        }
    }

    /// <summary>시상대의 업적 배지 하나에 그릴 값 — ResultPresenter 가 지표의 1등에서 만든다.</summary>
    public readonly struct ResultBadgeInfo
    {
        public readonly Sprite Icon;
        public readonly string Award;
        public readonly string Value;

        public ResultBadgeInfo(Sprite icon, string award, string value)
        {
            Icon = icon;
            Award = award;
            Value = value;
        }
    }

    /// <summary>
    /// 결과 화면의 시상대 한 칸 (시안 「결과화면 UI v3」 .col) — 캐릭터 · 왕관 · 「나」 화살표 · HP 뼈다귀 · 이름표 · 시상대(순위 · 탈락 턴 · 당한 사람).
    /// 값을 받아 그리기만 한다. 프리팹은 4위 · 생존 모습으로 저장돼 있고 Show 가 순위에 맞게 높이 · 색을 바꾼다.
    /// </summary>
    public class ResultPodiumView : MonoBehaviour
    {
        [Header("시상대")]
        [Tooltip("시상대 — 아래가 축이다 (높이를 바꾸고, 등장할 때 아래에서 솟는다).")]
        [SerializeField] private RectTransform podium;
        [SerializeField] private UIShape podiumBox;
        [Tooltip("시상대 위쪽의 밝은 띠.")]
        [SerializeField] private Graphic podiumHighlight;
        [Tooltip("밝은 띠의 둥근 아래 모서리를 덮는 판 — 시상대 면과 같은 색.")]
        [SerializeField] private Graphic podiumHighlightEdge;
        [Tooltip("내 시상대를 두르는 흰 · 잉크 두 겹 테.")]
        [SerializeField] private GameObject meRing;
        [SerializeField] private TMP_Text rankText;
        [SerializeField] private GameObject tiedChip;
        [SerializeField] private TMP_Text statText;
        [SerializeField] private TMP_Text subText;

        [Header("이름표 · 캐릭터 묶음")]
        [Tooltip("이름표 + 캐릭터 — 시상대 위에 얹힌다 (시상대 높이만큼 올린다).")]
        [SerializeField] private RectTransform upper;
        [SerializeField] private RectTransform nameTag;
        [SerializeField] private CanvasGroup nameTagGroup;
        [SerializeField] private UIShape nameTagBox;
        [SerializeField] private Image colorDot;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject meChip;
        [SerializeField] private GameObject readyChip;
        [SerializeField] private GameObject goneChip;

        [Header("캐릭터")]
        [Tooltip("캐릭터 자리 — 키를 바꾼다 (승자는 더 크다).")]
        [SerializeField] private RectTransform character;
        [SerializeField] private CanvasGroup characterGroup;
        [Tooltip("캐릭터 그림 — 탈락하면 회색으로 기울고, 승자는 깡충 뛴다.")]
        [SerializeField] private Image characterImage;
        [SerializeField] private RectTransform crown;
        [SerializeField] private CanvasGroup crownGroup;
        [SerializeField] private RectTransform mePointer;
        [SerializeField] private RectTransform winMark;
        [SerializeField] private CanvasGroup winMarkGroup;

        [Header("HP")]
        [SerializeField] private RectTransform hpBar;
        [SerializeField] private CanvasGroup hpBarGroup;
        [SerializeField] private UIShape hpBox;
        [SerializeField] private BoneGaugeView bone;
        [Tooltip("뼈다귀의 빈 자리(바탕 · 끝 마디) — 탈락하면 회색으로.")]
        [SerializeField] private Graphic[] boneEmptyParts;
        [SerializeField] private TMP_Text hpText;

        [Header("업적 배지 (시상대 아래쪽에 붙는다)")]
        [Tooltip("순위 · 탈락 줄 묶음 — 높이를 재서 배지가 들어갈지 본다.")]
        [SerializeField] private RectTransform head;
        [SerializeField] private VerticalLayoutGroup headColumn;
        [Tooltip("순위 숫자의 줄 높이.")]
        [SerializeField] private LayoutElement rankSize;
        [SerializeField] private TMP_Text tiedText;
        [Tooltip("배지가 쌓이는 자리 — 아래가 축.")]
        [SerializeField] private RectTransform badgeArea;
        [SerializeField] private VerticalLayoutGroup badgeColumn;
        [SerializeField] private ResultBadgeView badgePrefab;

        [Header("말풍선 (시안 .bubble) — 이모트 · 「준비!」")]
        [Tooltip("말풍선 — 왼쪽 아래가 축 (거기서 튀어나온다). 숨어 있을 때는 크기 0 · 투명.")]
        [SerializeField] private RectTransform bubble;
        [SerializeField] private CanvasGroup bubbleGroup;
        [SerializeField] private TMP_Text bubbleText;
        [Tooltip("말풍선이 떠 있는 시간 (초, 시안 2.4).")]
        [SerializeField] private float bubbleSeconds = 2.4f;
        [Tooltip("말풍선이 튀어나오고 들어가는 시간 (초, 시안 0.25).")]
        [SerializeField] private float bubblePopSeconds = 0.25f;
        [Tooltip("말풍선이 나타나고 사라지며 투명도가 바뀌는 시간 (초, 시안 0.2).")]
        [SerializeField] private float bubbleFadeSeconds = 0.2f;

        [Header("등장 연출 (시안 .stage.intro)")]
        [Tooltip("시상대가 아래에서 솟는 시간 (초).")]
        [SerializeField] private float podiumRiseSeconds = 0.55f;
        [Tooltip("시상대가 솟기 시작한 뒤 캐릭터 · 이름표가 떨어지기까지 (초).")]
        [SerializeField] private float dropDelay = 0.35f;
        [Tooltip("캐릭터 · 이름표가 떨어지기 시작하는 높이 (시안 drop 180).")]
        [SerializeField] private float dropHeight = 180f;
        [Tooltip("캐릭터 · 이름표가 떨어지는 시간 (초).")]
        [SerializeField] private float dropSeconds = 0.5f;
        [Tooltip("캐릭터 · 이름표가 떨어지며 나타나는 시간 (초).")]
        [SerializeField] private float dropFadeSeconds = 0.25f;
        [Tooltip("시상대가 솟기 시작한 뒤 HP 판이 떠오르기까지 (초).")]
        [SerializeField] private float hpRiseDelay = 0.75f;
        [Tooltip("HP 판이 떠오르는 시간 (초).")]
        [SerializeField] private float hpRiseSeconds = 0.35f;
        [Tooltip("HP 판이 떠오르기 시작하는 깊이 — 제자리보다 이만큼 아래 (시안 fadeup 20).")]
        [SerializeField] private float riseDistance = 20f;
        [Tooltip("시상대가 솟기 시작한 뒤 첫 배지가 튀어나오기까지 (초).")]
        [SerializeField] private float badgeDelay = 0.7f;
        [Tooltip("배지가 하나씩 튀어나오는 간격 (초).")]
        [SerializeField] private float badgeStep = 0.14f;
        [Tooltip("왕관이 떨어지기 시작하는 높이.")]
        [SerializeField] private float crownDropHeight = 140f;
        [Tooltip("왕관이 떨어지기 시작할 때의 기울기 (도).")]
        [SerializeField] private float crownTilt = 30f;
        [Tooltip("왕관이 떨어져 자리 잡는 시간 (초).")]
        [SerializeField] private float crownSeconds = 0.6f;
        [Tooltip("왕관이 나타나는 시간 (초).")]
        [SerializeField] private float crownFadeSeconds = 0.3f;
        [Tooltip("찹츄 승리 마크가 줄어들기 시작하는 크기 (배).")]
        [SerializeField] private float winMarkStartScale = 2.2f;
        [Tooltip("찹츄 승리 마크가 제 크기로 줄어드는 시간 (초).")]
        [SerializeField] private float winMarkSeconds = 0.45f;
        [Tooltip("찹츄 승리 마크가 나타나는 시간 (초).")]
        [SerializeField] private float winMarkFadeSeconds = 0.2f;
        [Tooltip("승자가 깡충 뛰기 전에 쉬는 시간 (초, 시안 hop 1.6초 주기의 70%).")]
        [SerializeField] private float hopRestSeconds = 1.12f;
        [Tooltip("승자가 깡충 뛰는 높이.")]
        [SerializeField] private float hopHeight = 16f;
        [Tooltip("깡충의 한 마디 — 오르기 · 내리기 · 내린 뒤 쉬기 각각 (초, 시안 hop 1.6초 주기의 10%).")]
        [SerializeField] private float hopStepSeconds = 0.16f;
        [Tooltip("「나」 화살표가 위아래로 움직이는 거리.")]
        [SerializeField] private float mePointerDistance = 8f;
        [Tooltip("「나」 화살표가 한쪽으로 가는 시간 (초) — 주기의 절반.")]
        [SerializeField] private float mePointerSeconds = 0.55f;

        [Header("배지가 들어갈 자리 계산 (시안 fitPodium)")]
        [Tooltip("시상대 위 끝에서 순위 줄까지 (테두리 6 + 안쪽 여백 18).")]
        [SerializeField] private float headTop = 24f;
        [Tooltip("순위 줄 묶음과 배지 사이의 최소 틈.")]
        [SerializeField] private float headBadgeGap = 6f;
        [Tooltip("배지 높이 · 배지 사이 틈.")]
        [SerializeField] private Vector2 badgeSize = new Vector2(44f, 6f);
        [Tooltip("시상대 아래 끝에서 배지까지 (테두리 6 + 안쪽 여백 12).")]
        [SerializeField] private float badgeBottom = 18f;

        [Header("모자랄 때 줄이는 값 (시안 .podium.tight)")]
        [SerializeField] private float tightHeadTop = 16f;
        [SerializeField] private float tightBadgeBottom = 15f;
        [SerializeField] private float tightBadgeGap = 4f;
        [SerializeField] private float tightRankFontSize = 54f;
        [SerializeField] private float tightTiedFontSize = 20f;
        [SerializeField] private float tightStatFontSize = 24f;
        [SerializeField] private float tightSubFontSize = 17f;
        [SerializeField] private float tightHeadSpacing = 1f;

        [Header("크기 (시안 :root)")]
        [Tooltip("순위별 시상대 높이 — 1 · 2 · 3 · 4위.")]
        [SerializeField] private float[] podiumHeights = { 288f, 266f, 252f, 240f };
        [Tooltip("시상대와 이름표 사이.")]
        [SerializeField] private float tagGap = 10f;
        [SerializeField] private float characterHeight = 212f;
        [SerializeField] private float winnerCharacterHeight = 232f;
        [Tooltip("캐릭터 머리 위 HP 판까지의 틈.")]
        [SerializeField] private float hpGap = 10f;
        [Tooltip("승자의 HP 판 틈 — 왕관 위로 비켜 선다.")]
        [SerializeField] private float winnerHpGap = 40f;
        [SerializeField] private float rankFontSize = 84f;
        [Tooltip("3 · 4위의 순위 숫자 크기.")]
        [SerializeField] private float lowRankFontSize = 68f;
        [Tooltip("순위 숫자의 줄 높이 — 글자 크기의 배수 (시안 .rank line-height).")]
        [SerializeField] private float rankLineHeight = 0.95f;
        [Tooltip("탈락한 캐릭터의 기울기 (도).")]
        [SerializeField] private float outTilt = 7f;
        [Tooltip("탈락한 캐릭터가 내려앉는 거리.")]
        [SerializeField] private float outDrop = 6f;

        [Header("색 (시안 :root)")]
        [Tooltip("순위별 시상대 면 색 — 1 · 2 · 3 · 4위.")]
        [SerializeField] private Color[] podiumColors =
        {
            new Color32(0xFF, 0xC1, 0x2E, 0xFF), new Color32(0xF4, 0xF7, 0xFE, 0xFF), new Color32(0xFD, 0xE8, 0xD2, 0xFF), new Color32(0xFF, 0xFC, 0xF7, 0xFF),
        };
        [Tooltip("순위별 시상대 밝은 띠 색 — 1 · 2 · 3 · 4위.")]
        [SerializeField] private Color[] podiumHighlightColors =
        {
            new Color32(0xFF, 0xD7, 0x66, 0xFF), Color.white, new Color32(0xFF, 0xF4, 0xE8, 0xFF), Color.white,
        };
        [SerializeField] private Color ink = new Color32(0x2A, 0x24, 0x20, 0xFF);
        [Tooltip("흐린 글자 — 나간 사람의 이름 · 탈락한 사람의 HP 숫자.")]
        [SerializeField] private Color muted = new Color32(0x6E, 0x64, 0x5C, 0xFF);
        [Tooltip("HP 가 낮을 때의 숫자 색.")]
        [SerializeField] private Color danger = new Color32(0xEC, 0x3B, 0x46, 0xFF);
        [Tooltip("탈락한 사람의 HP 판 테 색.")]
        [SerializeField] private Color outBorder = new Color32(0xB5, 0xAD, 0xA3, 0xFF);
        [Tooltip("탈락한 사람의 빈 뼈다귀 색.")]
        [SerializeField] private Color outBone = new Color32(0xEF, 0xE8, 0xDE, 0xFF);
        [Tooltip("탈락한 캐릭터에 입히는 색 — 흑백 대신 회색 색조.")]
        [SerializeField] private Color outCharacter = new Color(0.82f, 0.81f, 0.8f, 0.92f);
        [Tooltip("HP 가 이 값 이하면 숫자가 빨갛다.")]
        [SerializeField] private int lowHp = 20;

        [Header("이름표 점선 (나간 사람)")]
        [SerializeField] private float tagRadius = 18f;
        [SerializeField] private float tagBorder = 5f;
        [SerializeField] private float goneDash = 12f;
        [SerializeField] private float goneGap = 8f;

        private readonly List<ResultBadgeView> _badges = new List<ResultBadgeView>();
        private bool _isWinner;
        private Sequence _bubbleTween;

        private void Awake()
        {
            if (head == null || headColumn == null || rankSize == null || tiedText == null || badgeArea == null || badgeColumn == null || badgePrefab == null
                || bubble == null || bubbleGroup == null || bubbleText == null)
                Debug.LogError($"[{nameof(ResultPodiumView)}] 배지 · 말풍선 참조 중 비어 있는 것이 있습니다.", this);

            if (podium == null || podiumBox == null || podiumHighlight == null || podiumHighlightEdge == null || meRing == null || rankText == null || tiedChip == null
                || statText == null || subText == null || upper == null || nameTag == null || nameTagGroup == null || nameTagBox == null || colorDot == null || nameText == null
                || meChip == null || readyChip == null || goneChip == null || character == null || characterGroup == null || characterImage == null || crown == null
                || crownGroup == null || mePointer == null || winMark == null || winMarkGroup == null || hpBar == null || hpBarGroup == null || hpBox == null || bone == null
                || boneEmptyParts == null || hpText == null)
                Debug.LogError($"[{nameof(ResultPodiumView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>한 사람의 결과를 그린다. 준비 · 나감은 SetReady · SetGone 으로 따로 바꾼다.</summary>
        public void Show(ResultPodiumInfo info)
        {
            _isWinner = info.IsWinner;
            int tier = Mathf.Clamp(info.Rank, 1, podiumHeights.Length) - 1;

            // 시상대
            float height = podiumHeights[tier];
            podium.sizeDelta = new Vector2(podium.sizeDelta.x, height);
            upper.anchoredPosition = new Vector2(upper.anchoredPosition.x, height + tagGap);
            podiumBox.color = podiumColors[tier];
            podiumHighlightEdge.color = podiumColors[tier];
            podiumHighlight.color = podiumHighlightColors[tier];
            meRing.SetActive(info.IsMe);
            rankText.text = info.Rank.ToString();
            SetRankSize(info.Rank >= 3 ? lowRankFontSize : rankFontSize);
            tiedChip.SetActive(info.IsTied);
            statText.gameObject.SetActive(info.Stat != null);
            statText.text = info.Stat;
            subText.gameObject.SetActive(info.Sub != null);
            subText.text = info.Sub;

            // 이름표
            colorDot.color = info.Color;
            nameText.text = info.Name;
            meChip.SetActive(info.IsMe);

            // 캐릭터 — 탈락하면 몸만 기울인다 (머리 위 HP · 「나」 표시는 똑바로)
            character.sizeDelta = new Vector2(character.sizeDelta.x, info.IsWinner ? winnerCharacterHeight : characterHeight);
            if (info.IsOut)
            {
                characterImage.color = outCharacter;
                characterImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, outTilt);
                characterImage.rectTransform.anchoredPosition += new Vector2(0f, -outDrop);
            }
            crown.gameObject.SetActive(info.IsWinner);
            mePointer.gameObject.SetActive(info.IsMe && !info.IsWinner);
            winMark.gameObject.SetActive(info.HasWinMark);

            // HP — 탈락은 빈 회색 뼈다귀 · 0
            int hp = info.IsOut ? 0 : info.Hp;
            hpBar.anchoredPosition = new Vector2(hpBar.anchoredPosition.x, info.IsWinner ? winnerHpGap : hpGap);
            bone.SetHp(hp, info.MaxHp);
            hpText.text = hp.ToString();
            hpText.color = info.IsOut ? muted : hp <= lowHp ? danger : ink;
            if (info.IsOut)
            {
                hpBox.BorderColor = outBorder;
                foreach (Graphic part in boneEmptyParts)
                    part.color = outBone;
            }
        }

        /// <summary>
        /// 업적 배지를 시상대 아래쪽에 쌓는다 — Show 뒤에 한 번. 순위 줄 + 배지가 시상대 높이에 안 들어가면 숫자 · 글자 · 배지를 조금씩 줄인다 (시안 fitPodium).
        /// </summary>
        public void ShowBadges(IReadOnlyList<ResultBadgeInfo> badges)
        {
            if (badges.Count == 0)
                return;

            LayoutRebuilder.ForceRebuildLayoutImmediate(head);
            float need = headTop + head.rect.height + headBadgeGap + badges.Count * badgeSize.x + (badges.Count - 1) * badgeSize.y + badgeBottom;
            bool isTight = need > podium.rect.height;
            if (isTight)
            {
                head.anchoredPosition = new Vector2(head.anchoredPosition.x, -tightHeadTop);
                headColumn.spacing = tightHeadSpacing;
                SetRankSize(tightRankFontSize);
                tiedText.fontSize = tightTiedFontSize;
                statText.fontSize = tightStatFontSize;
                subText.fontSize = tightSubFontSize;
                badgeArea.anchoredPosition = new Vector2(badgeArea.anchoredPosition.x, tightBadgeBottom);
                badgeColumn.spacing = tightBadgeGap;
            }

            foreach (ResultBadgeInfo info in badges)
            {
                ResultBadgeView badge = Instantiate(badgePrefab, badgeArea);
                badge.Show(info.Icon, info.Award, info.Value, isTight);
                _badges.Add(badge);
            }
        }

        // 순위 숫자의 크기 — 줄 높이도 따라 바뀐다
        private void SetRankSize(float fontSize)
        {
            rankText.fontSize = fontSize;
            rankSize.minHeight = fontSize * rankLineHeight;
            rankSize.preferredHeight = fontSize * rankLineHeight;
        }

        /// <summary>말풍선을 다른 칸 위에 그려지는 층으로 옮긴다 — 자리는 그대로. 값을 그린 뒤(Show), 등장 연출 전에 한 번.</summary>
        public void MoveBubbleTo(RectTransform layer) => bubble.SetParent(layer, true);

        /// <summary>캐릭터 옆에 말풍선을 띄운다 (이모트 · 「준비!」) — 잠깐 뒤 스스로 사라진다. 나간 사람은 띄우지 않는다.</summary>
        public void Say(string text)
        {
            if (goneChip.activeSelf)
                return;

            bubbleText.text = text;
            _bubbleTween?.Kill();
            _bubbleTween = DOTween.Sequence().SetLink(bubble.gameObject)
                .Append(bubble.DOScale(1f, bubblePopSeconds).SetEase(Ease.OutBack, 2f))
                .Join(bubbleGroup.DOFade(1f, bubbleFadeSeconds))
                .AppendInterval(bubbleSeconds - bubblePopSeconds)
                .Append(bubble.DOScale(0f, bubblePopSeconds).SetEase(Ease.InQuad))
                .Join(bubbleGroup.DOFade(0f, bubbleFadeSeconds));
        }

        /// <summary>「한 판 더」 를 눌렀는지 — 이름표의 「준비」 꼬리표.</summary>
        public void SetReady(bool isReady) => readyChip.SetActive(isReady);

        /// <summary>방을 나갔는지 — 이름표가 점선 · 흐린 글자가 되고 「나감」 꼬리표가 붙는다. 나간 사람은 돌아오지 않는다.</summary>
        public void SetGone(bool isGone)
        {
            if (goneChip.activeSelf == isGone)
                return;

            goneChip.SetActive(isGone);
            if (isGone)
            {
                _bubbleTween?.Kill(); // 떠 있던 말풍선도 지운다
                bubble.localScale = Vector3.zero;
                bubbleGroup.alpha = 0f;
            }
            nameText.color = isGone ? muted : ink;
            nameTagBox.Set(UIShape.ShapeKind.RoundRect, tagRadius, tagBorder, ink, 1f, isGone ? goneDash : 0f, isGone ? goneGap : 0f);
        }

        /// <summary>
        /// 등장 연출 (시안 .stage.intro) — delay 초 뒤 시상대가 솟고, 캐릭터 · 이름표가 떨어지고, HP 판이 떠오른다.
        /// 왕관 · 찹츄 승리 마크 · 승자의 깡충은 화면 전체 기준 시각(crownAt · markAt · hopAt)에.
        /// </summary>
        public void PlayIntro(float delay, float crownAt, float markAt, float hopAt)
        {
            podium.localScale = new Vector3(1f, 0f, 1f);
            podium.DOScaleY(1f, podiumRiseSeconds).SetDelay(delay).SetEase(Ease.OutBack, 1.2f).SetLink(gameObject);

            Drop(character, characterGroup, delay + dropDelay);
            Drop(nameTag, nameTagGroup, delay + dropDelay);
            Rise(hpBar, hpBarGroup, delay + hpRiseDelay, hpRiseSeconds, riseDistance);
            for (int i = 0; i < _badges.Count; i++)
                _badges[i].PlayIntro(delay + badgeDelay + i * badgeStep);

            if (crown.gameObject.activeSelf)
            {
                Vector2 crownHome = crown.anchoredPosition;
                crown.anchoredPosition = crownHome + new Vector2(0f, crownDropHeight);
                crown.localRotation = Quaternion.Euler(0f, 0f, crownTilt);
                crownGroup.alpha = 0f;
                crown.DOAnchorPos(crownHome, crownSeconds).SetDelay(crownAt).SetEase(Ease.OutBack, 2f).SetLink(gameObject);
                crown.DOLocalRotate(Vector3.zero, crownSeconds).SetDelay(crownAt).SetEase(Ease.OutBack, 2f).SetLink(gameObject);
                crownGroup.DOFade(1f, crownFadeSeconds).SetDelay(crownAt).SetLink(gameObject);
            }

            if (winMark.gameObject.activeSelf)
            {
                winMark.localScale = Vector3.one * winMarkStartScale;
                winMarkGroup.alpha = 0f;
                winMark.DOScale(1f, winMarkSeconds).SetDelay(markAt).SetEase(Ease.OutBack, 2.2f).SetLink(gameObject);
                winMarkGroup.DOFade(1f, winMarkFadeSeconds).SetDelay(markAt).SetLink(gameObject);
            }

            // 승자는 1.6초마다 한 번 깡충 (시안 hop: 70% → 80% 에 16 위로, 90% 에 제자리)
            if (_isWinner)
            {
                RectTransform body = characterImage.rectTransform;
                float home = body.anchoredPosition.y;
                DOTween.Sequence().SetLink(gameObject).SetDelay(hopAt, false).SetLoops(-1) // false = 지연은 처음 한 번만 — 기본은 반복마다 다시 든다
                    .AppendInterval(hopRestSeconds)
                    .Append(body.DOAnchorPosY(home + hopHeight, hopStepSeconds).SetEase(Ease.OutQuad))
                    .Append(body.DOAnchorPosY(home, hopStepSeconds).SetEase(Ease.InQuad))
                    .AppendInterval(hopStepSeconds);
            }

            // 「나」 화살표는 1.1초 주기로 위아래 8
            if (mePointer.gameObject.activeSelf)
                mePointer.DOAnchorPosY(mePointer.anchoredPosition.y + mePointerDistance, mePointerSeconds).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        /// <summary>
        /// 아래 distance 에서 떠오르며 나타난다 (시안 fadeup) — 결과 화면의 뷰들이 함께 쓴다. delay 초 뒤 seconds 초 동안.
        /// blockUntilShown 이면 다 보일 때까지 눌리지 않는다.
        /// </summary>
        public static void Rise(RectTransform target, CanvasGroup group, float delay, float seconds, float distance, bool blockUntilShown = false)
        {
            Vector2 home = target.anchoredPosition;
            target.anchoredPosition = home + new Vector2(0f, -distance);
            group.alpha = 0f;
            target.DOAnchorPos(home, seconds).SetDelay(delay).SetEase(Ease.OutQuad).SetLink(target.gameObject);
            Tween fade = group.DOFade(1f, seconds).SetDelay(delay).SetLink(group.gameObject);
            if (!blockUntilShown)
                return;

            group.blocksRaycasts = false;
            fade.OnComplete(() => group.blocksRaycasts = true);
        }

        // 위에서 떨어지며 나타난다 (시안 drop)
        private void Drop(RectTransform target, CanvasGroup group, float delay)
        {
            Vector2 home = target.anchoredPosition;
            target.anchoredPosition = home + new Vector2(0f, dropHeight);
            group.alpha = 0f;
            target.DOAnchorPos(home, dropSeconds).SetDelay(delay).SetEase(Ease.OutBack, 1.8f).SetLink(gameObject);
            group.DOFade(1f, dropFadeSeconds).SetDelay(delay).SetLink(gameObject);
        }
    }
}
