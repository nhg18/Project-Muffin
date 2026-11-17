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
        [Tooltip("탈락한 캐릭터의 기울기 (도).")]
        [SerializeField] private float outTilt = 7f;
        [Tooltip("탈락한 캐릭터가 내려앉는 거리.")]
        [SerializeField] private float outDrop = 6f;

        [Header("색 (시안 :root)")]
        [Tooltip("순위별 시상대 면 색 — 1 · 2 · 3 · 4위.")]
        [SerializeField] private Color[] podiumColors =
        {
            new Color32(0xFF, 0xDE, 0x78, 0xFF), new Color32(0xF4, 0xF7, 0xFE, 0xFF), new Color32(0xFD, 0xE8, 0xD2, 0xFF), new Color32(0xFF, 0xFC, 0xF7, 0xFF),
        };
        [Tooltip("순위별 시상대 밝은 띠 색 — 1 · 2 · 3 · 4위.")]
        [SerializeField] private Color[] podiumHighlightColors =
        {
            new Color32(0xFF, 0xF2, 0xC6, 0xFF), Color.white, new Color32(0xFF, 0xF4, 0xE8, 0xFF), Color.white,
        };
        [SerializeField] private Color ink = new Color32(0x5E, 0x50, 0x47, 0xFF);
        [Tooltip("흐린 글자 — 나간 사람의 이름 · 탈락한 사람의 HP 숫자.")]
        [SerializeField] private Color muted = new Color32(0xA8, 0x9E, 0x95, 0xFF);
        [Tooltip("HP 가 낮을 때의 숫자 색.")]
        [SerializeField] private Color danger = new Color32(0xEC, 0x6B, 0x5D, 0xFF);
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

        private bool _isWinner;

        private void Awake()
        {
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
            rankText.fontSize = info.Rank >= 3 ? lowRankFontSize : rankFontSize;
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

        /// <summary>「한 판 더」 를 눌렀는지 — 이름표의 「준비」 꼬리표.</summary>
        public void SetReady(bool isReady) => readyChip.SetActive(isReady);

        /// <summary>방을 나갔는지 — 이름표가 점선 · 흐린 글자가 되고 「나감」 꼬리표가 붙는다. 나간 사람은 돌아오지 않는다.</summary>
        public void SetGone(bool isGone)
        {
            if (goneChip.activeSelf == isGone)
                return;

            goneChip.SetActive(isGone);
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
            podium.DOScaleY(1f, 0.55f).SetDelay(delay).SetEase(Ease.OutBack, 1.2f).SetLink(gameObject);

            Drop(character, characterGroup, delay + 0.35f);
            Drop(nameTag, nameTagGroup, delay + 0.35f);
            Rise(hpBar, hpBarGroup, delay + 0.75f, 0.35f);

            if (crown.gameObject.activeSelf)
            {
                Vector2 crownHome = crown.anchoredPosition;
                crown.anchoredPosition = crownHome + new Vector2(0f, 140f);
                crown.localRotation = Quaternion.Euler(0f, 0f, 30f);
                crownGroup.alpha = 0f;
                crown.DOAnchorPos(crownHome, 0.6f).SetDelay(crownAt).SetEase(Ease.OutBack, 2f).SetLink(gameObject);
                crown.DOLocalRotate(Vector3.zero, 0.6f).SetDelay(crownAt).SetEase(Ease.OutBack, 2f).SetLink(gameObject);
                crownGroup.DOFade(1f, 0.3f).SetDelay(crownAt).SetLink(gameObject);
            }

            if (winMark.gameObject.activeSelf)
            {
                winMark.localScale = Vector3.one * 2.2f;
                winMarkGroup.alpha = 0f;
                winMark.DOScale(1f, 0.45f).SetDelay(markAt).SetEase(Ease.OutBack, 2.2f).SetLink(gameObject);
                winMarkGroup.DOFade(1f, 0.2f).SetDelay(markAt).SetLink(gameObject);
            }

            // 승자는 1.6초마다 한 번 깡충 (시안 hop: 70% → 80% 에 16 위로, 90% 에 제자리)
            if (_isWinner)
            {
                RectTransform body = characterImage.rectTransform;
                float home = body.anchoredPosition.y;
                DOTween.Sequence().SetLink(gameObject).SetDelay(hopAt).SetLoops(-1)
                    .AppendInterval(1.12f)
                    .Append(body.DOAnchorPosY(home + 16f, 0.16f).SetEase(Ease.OutQuad))
                    .Append(body.DOAnchorPosY(home, 0.16f).SetEase(Ease.InQuad))
                    .AppendInterval(0.16f);
            }

            // 「나」 화살표는 1.1초 주기로 위아래 8
            if (mePointer.gameObject.activeSelf)
                mePointer.DOAnchorPosY(mePointer.anchoredPosition.y + 8f, 0.55f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        // 위 180 에서 떨어지며 나타난다 (시안 drop)
        private void Drop(RectTransform target, CanvasGroup group, float delay)
        {
            Vector2 home = target.anchoredPosition;
            target.anchoredPosition = home + new Vector2(0f, 180f);
            group.alpha = 0f;
            target.DOAnchorPos(home, 0.5f).SetDelay(delay).SetEase(Ease.OutBack, 1.8f).SetLink(gameObject);
            group.DOFade(1f, 0.25f).SetDelay(delay).SetLink(gameObject);
        }

        // 아래 20 에서 떠오르며 나타난다 (시안 fadeup)
        private void Rise(RectTransform target, CanvasGroup group, float delay, float seconds)
        {
            Vector2 home = target.anchoredPosition;
            target.anchoredPosition = home + new Vector2(0f, -20f);
            group.alpha = 0f;
            target.DOAnchorPos(home, seconds).SetDelay(delay).SetEase(Ease.OutQuad).SetLink(gameObject);
            group.DOFade(1f, seconds).SetDelay(delay).SetLink(gameObject);
        }
    }
}
