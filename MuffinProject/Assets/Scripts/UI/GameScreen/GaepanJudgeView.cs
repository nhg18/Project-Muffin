using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 개판 오분전 판정 팻말 (프로토타입 v2 GP_FX.judge — 121). 화면을 어둡게 → 「땡 개판 끝!」 → 기준 팻말(HP 꼴찌 / 카드 꼴찌)이 룰렛처럼 번갈아 뒤집히다 정해진 쪽에 멈춤
    /// → 살아 있는 사람 머리 위에 기준 값 표가 하나씩 → 꼴찌 표만 분홍 「꼴찌」, 나머지는 흐리게 · 띠는 「꼴찌는?!」 → 0.65초 뒤 사라진다.
    /// 시안의 처형씬(치와와 뭉치)은 넣지 않는다 (사용자 2026-11-13). 시간표는 방장의 GameServer.GaepanJudgeSeconds 와 같다 — 끝나면 꼴찌의 HP 가 0 이 된다.
    /// 판정 · 꼴찌는 방장이 정해 보낸 값만 그린다.
    /// </summary>
    public class GaepanJudgeView : MonoBehaviour
    {
        private const float DimSeconds = 0.22f;
        private const float BannerSeconds = 0.38f;
        private const float RollStart = 0.52f;   // GP_ROLL0
        private const float RollLength = 1.6f;   // GP_ROLL
        private const float RollFirstStep = 0.07f;
        private const float RollStepGrowth = 1.17f;
        private const float TagStart = 2.38f;    // GP_TAG0 = 0.52 + 1.6 + 0.26
        private const float TagStagger = 0.19f;
        private const float TagSeconds = 0.3f;
        private const float RevealAfter = 0.65f; // GP_PRE — 꼴찌가 보인 뒤 사라지기까지
        private const float FadeSeconds = 0.25f;
        private const float TagRaise = 20f;      // 시안 c.y − 10 (× 2) — 캐릭터 가운데보다 조금 위
        private const float TagEdge = 140f;      // 시안 innerWidth − 70 · 70 (× 2)
        private const float TagPadding = 56f;    // 표 좌우 여백 · 테두리 + 화면 끝 틈
        private const string BannerFirst = "땡 개판 끝!";
        private const string BannerLoserWord = "꼴찌는";
        private const string BannerLoserMark = "?!";
        private const string HpSide = "HP 꼴찌";
        private const string CardSide = "카드 꼴찌";
        private const string HpTagFormat = "<size=58%><alpha=#{1}>HP<alpha=#FF></size><space=0.18em>{0}";
        private const string CardTagFormat = "<size=58%><alpha=#{1}>카드<alpha=#FF></size><space=0.18em>{0}<size=58%><alpha=#{1}>장<alpha=#FF></size>";

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image dim;
        [Tooltip("띠 (시안 .gp-jban) — 위 17%, −3° 기울어 있다.")]
        [SerializeField] private RectTransform banner;
        [SerializeField] private StickerWordView bannerWord;
        [Tooltip("기준 팻말 (시안 .gp-roll) — 위 31%.")]
        [SerializeField] private RectTransform roll;
        [SerializeField] private CanvasGroup rollGroup;
        [SerializeField] private Image rollFill;
        [SerializeField] private TMP_Text rollText;
        [SerializeField] private TMP_Text heartIcon;
        [SerializeField] private GameObject cardIcon;
        [Tooltip("머리 위 값 표 템플릿 (비활성) — 자식: Body(Fill · Text · LowBadge).")]
        [SerializeField] private RectTransform tagTemplate;
        [SerializeField] private float tagFontSize = 48f;
        [SerializeField] private float lowTagFontSize = 69f;

        [SerializeField] private Color cream = new Color32(0xFF, 0xFC, 0xF6, 0xFF);
        [SerializeField] private Color ink = new Color32(0x2B, 0x1E, 0x1A, 0xFF);
        [SerializeField] private Color pink = new Color32(0xFF, 0x5C, 0x8A, 0xFF);
        [SerializeField] private Color yellow = new Color32(0xFF, 0xE2, 0x7A, 0xFF);
        [SerializeField] private Color hpFill = new Color32(0xFF, 0xE0, 0xEA, 0xFF);
        [SerializeField] private Color cardFill = new Color32(0xE3, 0xF0, 0xFF, 0xFF);
        [SerializeField] private Color cardOnFill = new Color32(0x4E, 0x9B, 0xEA, 0xFF);

        private readonly List<GameObject> _tags = new List<GameObject>();

        private void Awake()
        {
            if (group == null || dim == null || banner == null || bannerWord == null || roll == null || rollGroup == null || rollFill == null || rollText == null || heartIcon == null || cardIcon == null || tagTemplate == null)
                Debug.LogError($"[{nameof(GaepanJudgeView)}] 참조가 비어 있습니다.", this);
        }

        /// <summary>
        /// 판정을 보이고 사라질 때까지 기다린다. byHp = 기준, values = 살아 있던 사람의 기준 값, screenPoints = 그 사람 캐릭터 가운데(화면 좌표, 없으면 null),
        /// loserIndex = 꼴찌의 순번.
        /// </summary>
        public IEnumerator PlayRoutine(bool byHp, IReadOnlyList<int> values, IReadOnlyList<Vector2?> screenPoints, int loserIndex)
        {
            Clear();
            gameObject.SetActive(true);
            DOTween.Kill(this);
            group.alpha = 1f;

            // 어둡게 · 띠 「땡 개판 끝!」 (2.4배 · −8° → 1배 · −3°, 0.38초)
            dim.color = new Color(dim.color.r, dim.color.g, dim.color.b, 0f);
            dim.DOFade(0.42f, DimSeconds).SetTarget(this);
            var bannerGroup = banner.GetComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            banner.localScale = Vector3.one * 2.4f;
            banner.localRotation = Quaternion.Euler(0f, 0f, 8f);
            DOTween.Sequence().SetTarget(this)
                .Append(bannerGroup.DOFade(1f, BannerSeconds))
                .Join(banner.DOScale(1f, BannerSeconds).SetEase(Ease.OutBack))
                .Join(banner.DOLocalRotate(new Vector3(0f, 0f, 3f), BannerSeconds).SetEase(Ease.OutBack));
            bannerWord.Set(new[] { ("땡", StickerTone.Bang), (BannerFirst.Substring(1), StickerTone.Fur) }, 0, 0f);

            ScheduleRoll(byHp);

            // 머리 위 값 표 — 하나씩 (0 → 1.25 → 1배, 0.3초)
            var tags = new List<RectTransform>();
            for (int i = 0; i < values.Count; i++)
            {
                RectTransform tag = MakeTag(byHp, values[i], screenPoints[i]);
                tags.Add(tag);
                if (tag == null)
                    continue;
                var body = (RectTransform)tag.GetChild(0);
                var tagGroup = tag.GetComponent<CanvasGroup>();
                tagGroup.alpha = 0f;
                body.localScale = Vector3.zero;
                DOTween.Sequence().SetTarget(this).SetDelay(TagStart + i * TagStagger)
                    .Append(tagGroup.DOFade(1f, TagSeconds * 0.6f))
                    .Join(body.DOScale(1.25f, TagSeconds * 0.6f).SetEase(Ease.OutQuad))
                    .Append(body.DOScale(1f, TagSeconds * 0.4f).SetEase(Ease.OutQuad));
            }

            float reveal = TagStart + Mathf.Max(0.6f, values.Count * TagStagger + 0.38f);
            yield return new WaitForSeconds(reveal);

            // 꼴찌 — 그 표만 분홍 「꼴찌」(1.6 → 1배), 나머지는 흐리게. 띠는 「꼴찌는?!」
            for (int i = 0; i < tags.Count; i++)
            {
                if (tags[i] == null)
                    continue;
                if (i == loserIndex)
                    SetLow(tags[i], byHp, values[i]);
                else
                    tags[i].GetComponent<CanvasGroup>().alpha = 0.5f;
            }
            bannerWord.Set(new[] { (BannerLoserWord, StickerTone.Pink), (BannerLoserMark, StickerTone.Bang) }, 0, 0f);

            yield return new WaitForSeconds(RevealAfter);
            yield return group.DOFade(0f, FadeSeconds).SetTarget(this).WaitForCompletion();
            Hide();
        }

        public void Hide()
        {
            DOTween.Kill(this);
            Clear();
            gameObject.SetActive(false);
        }

        // 기준 룰렛 — 0.07초부터 1.17배씩 느려지며 HP / 카드가 번갈아 뒤집히고, 마지막 면이 정해진 기준이 되게 앞쪽 뒤집기를 덜어 낸다 (시안 그대로)
        private void ScheduleRoll(bool byHp)
        {
            var flips = new List<float>();
            float time = 0f;
            for (float step = RollFirstStep; time < RollLength - 0.04f; step *= RollStepGrowth)
            {
                flips.Add(time);
                time += step;
            }
            int final = byHp ? 0 : 1;
            while (flips.Count > 1 && (flips.Count - 1) % 2 != final)
                flips.RemoveAt(0);

            rollGroup.alpha = 0f;
            roll.localScale = Vector3.one;
            float shift = flips[0];
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            for (int j = 0; j < flips.Count; j++)
            {
                int index = j;
                bool last = j == flips.Count - 1;
                sequence.InsertCallback(RollStart + flips[j] - shift, () => Flip(index % 2 == 0, index == 0, last));
            }
        }

        private void Flip(bool hpSide, bool first, bool last)
        {
            rollText.text = hpSide ? HpSide : CardSide;
            heartIcon.gameObject.SetActive(hpSide);
            cardIcon.SetActive(!hpSide);
            rollFill.color = last ? (hpSide ? pink : cardOnFill) : (hpSide ? hpFill : cardFill);
            rollText.color = last ? cream : ink;
            heartIcon.color = last ? yellow : pink;

            roll.DOKill();
            if (first)
            {
                rollGroup.alpha = 1f;
                roll.localScale = Vector3.one * 0.3f;
                roll.DOScale(1f, 0.22f).SetEase(Ease.OutBack);
            }
            else
            {
                roll.localScale = new Vector3(1f, 0.17f, 1f); // rotateX(80°) → 0 — 납작했다 펴진다
                roll.DOScaleY(1f, 0.09f);
            }
            if (last)
            {
                roll.localScale = Vector3.one * 1.45f;
                roll.DOScale(1f, BannerSeconds).SetEase(Ease.OutBack);
            }
        }

        private RectTransform MakeTag(bool byHp, int value, Vector2? screenPoint)
        {
            if (screenPoint == null)
                return null;

            var parent = (RectTransform)tagTemplate.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint.Value, null, out Vector2 local))
                return null;

            RectTransform tag = Instantiate(tagTemplate, parent);
            tag.gameObject.SetActive(true);
            TMP_Text text = tag.GetComponentInChildren<TMP_Text>(true);
            // 화면 끝에서 잘리지 않게 — 꼴찌 표(가장 큰 모양)의 반 너비 + 테두리만큼 안쪽으로
            text.fontSize = lowTagFontSize;
            float edge = Mathf.Max(TagEdge, text.GetPreferredValues(string.Format(byHp ? HpTagFormat : CardTagFormat, value, "D9")).x / 2f + TagPadding);
            float halfWidth = parent.rect.width / 2f;
            tag.anchoredPosition = new Vector2(Mathf.Clamp(local.x, -halfWidth + edge, halfWidth - edge), local.y + TagRaise);
            text.fontSize = tagFontSize;
            text.color = ink;
            text.text = string.Format(byHp ? HpTagFormat : CardTagFormat, value, "A6");
            _tags.Add(tag.gameObject);
            return tag;
        }

        private void SetLow(RectTransform tag, bool byHp, int value)
        {
            var body = (RectTransform)tag.GetChild(0);
            body.Find("Fill").GetComponent<Image>().color = pink;
            TMP_Text text = tag.GetComponentInChildren<TMP_Text>(true);
            text.fontSize = lowTagFontSize;
            text.color = cream;
            text.text = string.Format(byHp ? HpTagFormat : CardTagFormat, value, "D9");
            body.Find("LowBadge").gameObject.SetActive(true);
            body.DOKill();
            body.localScale = Vector3.one * 1.6f;
            body.DOScale(1f, TagSeconds).SetEase(Ease.OutBack);
        }

        private void Clear()
        {
            foreach (GameObject tag in _tags)
                if (tag != null)
                {
                    DOTween.Kill(tag.transform.GetChild(0));
                    Destroy(tag);
                }
            _tags.Clear();
        }
    }
}
