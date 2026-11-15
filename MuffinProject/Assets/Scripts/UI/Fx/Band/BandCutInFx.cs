using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>그림 움직임 — 시안 M 의 mv (MV 표).</summary>
    public enum BandMove
    {
        Nod,
        Dash,
        Pop,
        Shiver,
        Wag,
        Bob,
        Flip,
    }

    /// <summary>
    /// 띠 컷인 — 고급 등급 카드의 3 단계 (아티팩트 「찹츄 카드 이펙트 v2」 의 M 표 + rareCI mini + cutinFrame).
    /// 판이 어두워지고 기운 띠가 미끄러져 들어온 뒤, 왼쪽에 카드 그림(없으면 캐릭터 동그라미 + 글자)이 날아와 움직이고
    /// 말풍선 → 한마디 · 카드 이름 → 고리 → 효과 한 줄 순으로 뜨고, 끝에 판째 사라진다.
    /// 카드마다 다른 것은 「카드 값」 아래 값뿐이다 — 카드 하나 더하기 = 틀 프리팹(Prefab/UI/CardFx/Band/BandCutIn)의 변형 Card_{ID} + 값.
    /// 4 단계 장식은 <see cref="post"/> 아래 조각(<see cref="CardFxStep"/>)을 시작 시각대로 재생한다 (없으면 바로 끝난다).
    /// </summary>
    public class BandCutInFx : CardFx
    {
        // 시안 rareCI · cutinFrame 의 키프레임 (CSS 그대로 — x 오른쪽 +, y 아래 +, 각도 시계 방향 +)
        // .ci-band: translateX(-120%) → 0
        private const float BandFrom = -1.2f;
        // .ci-in: translateX(-70vw) scale(1.25) 투명 → 75% translateX(3vw) scale(1.02) → 제자리 (x 는 화면 너비 대비)
        private static readonly Pose[] CharacterIn =
        {
            new Pose(0f, -0.7f, 0f, 1.25f, 0f, 0f), new Pose(0.75f, 0.03f, 0f, 1.02f, 0f, 1f), new Pose(1f, 0f, 0f, 1f, 0f, 1f),
        };
        // .ci-nin: scale(2.6) 투명 → 60% scale(.92) → 1 (시안의 blur(8px) 는 빼고 투명도만)
        private static readonly Pose[] TitleIn =
        {
            new Pose(0f, 0f, 0f, 2.6f, 0f, 0f), new Pose(0.6f, 0f, 0f, 0.92f, 0f, 1f), new Pose(1f, 0f, 0f, 1f, 0f, 1f),
        };
        // .rr-tag: translateY(26px) scale(.5) 투명 → 60% translateY(-5px) scale(1.16) → 제자리 (y 는 시안 px)
        private static readonly Pose[] TagIn =
        {
            new Pose(0f, 0f, 26f, 0.5f, 0f, 0f), new Pose(0.6f, 0f, -5f, 1.16f, 0f, 1f), new Pose(1f, 0f, 0f, 1f, 0f, 1f),
        };
        // .ci-bub: scale(0) rotate(-12deg) → 40% 1.25 · 4deg → 60% 1 · 0 → 85% 1 → scale(.6) 투명
        private static readonly Pose[] BubbleIn =
        {
            new Pose(0f, 0f, 0f, 0f, -12f, 1f), new Pose(0.4f, 0f, 0f, 1.25f, 4f, 1f), new Pose(0.6f, 0f, 0f, 1f, 0f, 1f),
            new Pose(0.85f, 0f, 0f, 1f, 0f, 1f), new Pose(1f, 0f, 0f, 0.6f, 0f, 0f),
        };
        // .ci-ring: scale(.1) → scale(3) 투명
        private static readonly Pose[] RingOut = { new Pose(0f, 0f, 0f, 0.1f, 0f, 1f), new Pose(1f, 0f, 0f, 3f, 0f, 0f) };

        // 시안 MV — B = rotate(-3deg). x · y 는 그림 크기 대비
        private const float RestAngle = -3f;
        private static readonly Dictionary<BandMove, Pose[]> Moves = new Dictionary<BandMove, Pose[]>
        {
            [BandMove.Nod] = new[] { Rest(0f), new Pose(0.4f, 0f, 0.04f, 1f, -9f, 1f), Rest(1f) },
            [BandMove.Dash] = new[] { Rest(0f), new Pose(0.25f, -0.1f, 0f, 1f, -3f, 1f), new Pose(0.55f, 0.08f, 0f, 1f, -6f, 1f), Rest(1f) },
            [BandMove.Pop] = new[] { Rest(0f), new Pose(0.35f, 0f, 0f, 1.1f, -3f, 1f), Rest(1f) },
            [BandMove.Shiver] = new[]
            {
                Rest(0f), new Pose(0.2f, -0.02f, 0f, 1f, -5f, 1f), new Pose(0.4f, 0.02f, 0f, 1f, -1f, 1f),
                new Pose(0.6f, -0.02f, 0f, 1f, -5f, 1f), new Pose(0.8f, 0.02f, 0f, 1f, -1f, 1f), Rest(1f),
            },
            [BandMove.Wag] = new[] { Rest(0f), new Pose(0.25f, 0f, 0f, 1f, -9f, 1f), new Pose(0.5f, 0f, 0f, 1f, 3f, 1f), new Pose(0.75f, 0f, 0f, 1f, -7f, 1f), Rest(1f) },
            [BandMove.Bob] = new[] { Rest(0f), new Pose(0.35f, 0f, 0.06f, 1f, -3f, 1f), new Pose(0.7f, 0f, -0.02f, 1f, -3f, 1f), Rest(1f) },
            [BandMove.Flip] = new[] { Rest(0f), new Pose(0.4f, 0f, 0f, -1f, 1f, -3f, 1f), new Pose(0.6f, 0f, 0f, -1f, 1f, -3f, 1f), Rest(1f) },
        };

        // CSS 이징 (cubic-bezier 네 값)
        private static readonly Vector4 Snap = new Vector4(0.2f, 0.9f, 0.3f, 1f);
        private static readonly Vector4 EaseOut = new Vector4(0f, 0f, 0.58f, 1f);
        private static readonly Vector4 EaseInOut = new Vector4(0.42f, 0f, 0.58f, 1f);
        private static readonly Vector4 Linear = new Vector4(0f, 0f, 1f, 1f);

        // TMP 기울임 태그 — 시안 .ci-nin b 의 skewX(-8deg)
        private const string SlantFormat = "<i angle={0}>{1}</i>";

        [Header("카드 값 (시안 M 한 줄)")]
        [Tooltip("띠 가운데 색 · 제목 그림자 색 · 글자 동그라미 색 (시안 c1).")]
        [SerializeField] private Color bandColor = new Color32(0xe0, 0x11, 0x2b, 0xff);
        [Tooltip("띠 위아래 가장자리 색 (시안 c2).")]
        [SerializeField] private Color bandEdgeColor = new Color32(0x5a, 0x00, 0x10, 0xff);
        [Tooltip("빛 · 고리 · 효과 한 줄 글자 색 (시안 glow).")]
        [SerializeField] private Color glowColor = new Color32(0xff, 0x2a, 0x3a, 0xff);
        [Tooltip("카드 이름 위 한마디 (시안 who).")]
        [SerializeField] private string who = string.Empty;
        [Tooltip("말풍선 글자 (시안 say).")]
        [SerializeField] private string say = string.Empty;
        [Tooltip("이름 아래 효과 한 줄 (시안 tag).")]
        [SerializeField] private string effectLine = string.Empty;
        [Tooltip("카드 그림이 없을 때 캐릭터 동그라미에 붙는 글자 (시안 ico).")]
        [SerializeField] private string icon = string.Empty;
        [Tooltip("그림 움직임 (시안 mv).")]
        [SerializeField] private BandMove move = BandMove.Nod;
        [Tooltip("켜면 띠 줄무늬가 흐르지 않는다 (시안 still).")]
        [SerializeField] private bool stillLines;

        [Header("카드 그림 창")]
        [Tooltip("그림을 창에 꽉 채운 뒤 더 키우는 배율 (1 = 창 너비에 맞춤).")]
        [Min(1f)]
        [SerializeField] private float artZoom = 1f;
        [Tooltip("창에 보일 곳 — 그림 안 비율 (x 왼쪽 0 · y 위 0). 창보다 남는 쪽만 움직인다.")]
        [SerializeField] private Vector2 artFocus = new Vector2(0.5f, 0.24f);

        [Header("시간표 (ms — 시안 mk · rareCI · cutinFrame)")]
        [Tooltip("컷인 길이 — 끝나기 fadeOutMs 전부터 판째 사라진다 (시안 SPECIAL_MS).")]
        [SerializeField] private float lengthMs = 1150f;
        [Tooltip("판째 사라지는 시간.")]
        [SerializeField] private float fadeOutMs = 240f;
        [Tooltip("컷인이 사라진 뒤 4 단계까지 (시안 special2 의 +80).")]
        [SerializeField] private float afterMs = 80f;
        [Tooltip("판이 어두워지는 시간 (.ci-bg).")]
        [SerializeField] private float backdropInMs = 160f;
        [Tooltip("띠가 들어오기 시작 · 들어오는 시간 (.ci-band).")]
        [SerializeField] private Vector2 bandTime = new Vector2(110f, 240f);
        [Tooltip("그림이 날아오기 시작 · 날아오는 시간 (.ci-in).")]
        [SerializeField] private Vector2 characterTime = new Vector2(200f, 380f);
        [Tooltip("그림 움직임 시작 · 시간 (MV, ease-in-out).")]
        [SerializeField] private Vector2 moveTime = new Vector2(300f, 420f);
        [Tooltip("말풍선 시작 · 시간.")]
        [SerializeField] private Vector2 bubbleTime = new Vector2(380f, 900f);
        [Tooltip("한마디 · 카드 이름 시작 · 시간 (F.title).")]
        [SerializeField] private Vector2 titleTime = new Vector2(520f, 380f);
        [Tooltip("고리 시작 · 시간 (ringAt, ease-out).")]
        [SerializeField] private Vector2 ringTime = new Vector2(700f, 520f);
        [Tooltip("효과 한 줄 시작 · 시간 (F.tag).")]
        [SerializeField] private Vector2 tagTime = new Vector2(760f, 380f);
        [Tooltip("줄무늬가 한 주기 흐르는 시간 (시안 ciLines .35s).")]
        [SerializeField] private float stripeLoopMs = 350f;

        [Header("글자 칸 (시안 px)")]
        [Tooltip("한마디 칸의 좌우 여백 (.ci-nin small padding .7em).")]
        [SerializeField] private float whoPadding = 18.2f;
        [Tooltip("말풍선 크기 = 글자 + 이만큼 (좌우 · 위아래 합, 여백 + 테).")]
        [SerializeField] private Vector2 bubblePadding = new Vector2(51.2f, 14.9f);
        [Tooltip("제목 뒤 빛이 글자 밖으로 퍼지는 거리 (시안 text-shadow 0 0 .6em).")]
        [SerializeField] private float titleGlowSpread = 40f;
        [Tooltip("글자 동그라미의 좌우 여백 (.rr-ico padding .35em + 테).")]
        [SerializeField] private float iconPadding = 21f;
        [Tooltip("제목 기울기 (TMP <i angle> — 100 이 45°쯤. 시안 skewX(-8deg) ≈ 14).")]
        [SerializeField] private int titleSlant = 14;

        [Header("부품")]
        [Tooltip("3 단계 판 전체 — 끝에 판째 사라진다.")]
        [SerializeField] private CanvasGroup cutIn;
        [Tooltip("어두운 판 (.ci-bg) — 판의 색 · 투명도는 그림에, 나타나기는 이 투명도로.")]
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private CutInBand band;
        [Tooltip("날아오는 그림 칸 (.ci-in).")]
        [SerializeField] private RectTransform character;
        [SerializeField] private CanvasGroup characterGroup;
        [Tooltip("카드 그림 틀 (.ci-art) — 그림이 있을 때 움직인다.")]
        [SerializeField] private RectTransform art;
        [Tooltip("카드 그림 — 창 크기에 맞춰 잘라 보인다.")]
        [SerializeField] private RawImage artPicture;
        [Tooltip("캐릭터 동그라미 (.ci-av) — 카드 그림이 없을 때 움직인다.")]
        [SerializeField] private RectTransform avatar;
        [SerializeField] private Image avatarPicture;
        [Tooltip("캐릭터 동그라미에 붙는 글자 동그라미 (.rr-ico).")]
        [SerializeField] private RectTransform iconBadge;
        [SerializeField] private Graphic iconFill;
        [SerializeField] private TMP_Text iconLabel;
        [Tooltip("한마디 · 카드 이름 묶음 (.ci-nin) — 크기는 글자에 맞춘다.")]
        [SerializeField] private RectTransform titleGroup;
        [SerializeField] private CanvasGroup titleGroupAlpha;
        [SerializeField] private RectTransform whoBox;
        [SerializeField] private TMP_Text whoLabel;
        [SerializeField] private TMP_Text titleLabel;
        [Tooltip("제목 뒤 번지는 빛.")]
        [SerializeField] private RectTransform titleGlow;
        [Tooltip("효과 한 줄 (.rr-tag).")]
        [SerializeField] private RectTransform effectTag;
        [SerializeField] private CanvasGroup effectTagGroup;
        [SerializeField] private TMP_Text effectLabel;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private CanvasGroup bubbleGroup;
        [SerializeField] private TMP_Text bubbleLabel;
        [SerializeField] private RectTransform ring;
        [SerializeField] private CanvasGroup ringGroup;
        [Tooltip("빛 색(glow)을 입힐 그림 — 투명도는 그대로 둔다.")]
        [SerializeField] private Graphic[] glowFills;
        [Tooltip("빛 색(glow)을 입힐 도형 테 — 투명도는 그대로 둔다.")]
        [SerializeField] private UIShape[] glowBorders;

        [Header("4 단계 장식")]
        [Tooltip("이 아래 조각을 4 단계에 시작 시각대로 재생한다. 비어 있으면 4 단계 장식 없음.")]
        [SerializeField] private RectTransform post;

        private readonly List<Material> _materials = new List<Material>();
        private List<CardFxStep> _postSteps;
        private Vector2 _characterOrigin;
        private Vector2 _artOrigin;
        private Vector2 _avatarOrigin;
        private Vector2 _effectTagOrigin;
        private Vector2 _bubbleOrigin;
        private Vector2 _ringOrigin;
        private RectTransform _moving;
        private Sequence _timeline;
        private Tween _stripes;

        private float _pixelScale = 1.5f;

        private void Awake()
        {
            var missing = new List<string>();
            void Check(object value, string name)
            {
                if (value == null || (value is UnityEngine.Object unityObject && unityObject == null))
                    missing.Add(name);
            }

            Check(cutIn, nameof(cutIn));
            Check(backdrop, nameof(backdrop));
            Check(band, nameof(band));
            Check(character, nameof(character));
            Check(characterGroup, nameof(characterGroup));
            Check(art, nameof(art));
            Check(artPicture, nameof(artPicture));
            Check(avatar, nameof(avatar));
            Check(avatarPicture, nameof(avatarPicture));
            Check(iconBadge, nameof(iconBadge));
            Check(iconFill, nameof(iconFill));
            Check(iconLabel, nameof(iconLabel));
            Check(titleGroup, nameof(titleGroup));
            Check(titleGroupAlpha, nameof(titleGroupAlpha));
            Check(whoBox, nameof(whoBox));
            Check(whoLabel, nameof(whoLabel));
            Check(titleLabel, nameof(titleLabel));
            Check(titleGlow, nameof(titleGlow));
            Check(effectTag, nameof(effectTag));
            Check(effectTagGroup, nameof(effectTagGroup));
            Check(effectLabel, nameof(effectLabel));
            Check(bubble, nameof(bubble));
            Check(bubbleGroup, nameof(bubbleGroup));
            Check(bubbleLabel, nameof(bubbleLabel));
            Check(ring, nameof(ring));
            Check(ringGroup, nameof(ringGroup));
            Check(glowFills, nameof(glowFills));
            Check(glowBorders, nameof(glowBorders));
            Check(post, nameof(post));
            if (missing.Count > 0)
            {
                Debug.LogError($"[{nameof(BandCutInFx)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
                return;
            }

            _postSteps = Collect(post);
            _characterOrigin = character.anchoredPosition;
            _artOrigin = art.anchoredPosition;
            _avatarOrigin = avatar.anchoredPosition;
            _effectTagOrigin = effectTag.anchoredPosition;
            _bubbleOrigin = bubble.anchoredPosition;
            _ringOrigin = ring.anchoredPosition;

            // 투명도 0 인 그림은 GPU 가 그리지 않게
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
                graphic.canvasRenderer.cullTransparentMesh = true;
        }

        private void OnDestroy()
        {
            foreach (Material material in _materials)
                Destroy(material);
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            if (context.Layer != null)
                _pixelScale = context.Layer.PixelScale;
            ShowCard(context);
            ResetState();
            PlayTimeline();
            yield return new WaitForSeconds(Seconds(lengthMs + afterMs));
            cutIn.gameObject.SetActive(false);
            _stripes?.Kill();
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            if (_postSteps.Count == 0)
                yield break;

            foreach (CardFxStep step in _postSteps)
                step.ResetState();

            yield return TimelineRoutine(_postSteps, context, null);
        }

        // 켜진 조각을 시작 시각 순으로 (꺼진 오브젝트 · 컴포넌트는 빠진다. 같으면 계층 위부터 — OrderBy 는 순서를 지킨다).
        // HellBladeFx 와 같은 식 — CardFx 로 올리는 것은 같이 쓰는 코드라 공통 커밋에서 한다
        private static List<CardFxStep> Collect(Transform root)
        {
            return root.GetComponentsInChildren<CardFxStep>().Where(step => step.enabled).OrderBy(step => step.StartMs).ToList();
        }

        // 시작부터 조각마다 제 시각에 재생하고, 가장 늦게 끝나는 조각까지 기다린다 (HellBladeFx 와 같은 식)
        private static IEnumerator TimelineRoutine(List<CardFxStep> steps, CardFxContext context, RectTransform target)
        {
            float start = Time.time;
            float end = 0f;
            foreach (CardFxStep step in steps)
            {
                while ((Time.time - start) * 1000f < step.StartMs)
                    yield return null;
                step.Play(context, target);
                end = Mathf.Max(end, step.StartMs + step.DurationMs);
            }

            while ((Time.time - start) * 1000f < end)
                yield return null;
        }

        // 카드 값을 부품에 넣는다 — 글자 · 색 · 그림 · 칸 크기
        private void ShowCard(CardFxContext context)
        {
            band.SetColors(bandColor, bandEdgeColor, glowColor);
            foreach (Graphic graphic in glowFills)
                graphic.color = WithAlpha(glowColor, graphic.color.a);
            foreach (UIShape shape in glowBorders)
                shape.BorderColor = WithAlpha(glowColor, shape.BorderColor.a);
            iconFill.color = WithAlpha(bandColor, iconFill.color.a);
            TintUnderlay(titleLabel, bandColor);
            TintUnderlay(effectLabel, glowColor);
            effectLabel.color = WithAlpha(glowColor, effectLabel.color.a);

            ShowPicture(context);

            whoLabel.text = who;
            whoBox.gameObject.SetActive(!string.IsNullOrEmpty(who));
            float whoWidth = whoLabel.GetPreferredValues(who).x + whoPadding * 2f * _pixelScale;
            whoBox.sizeDelta = new Vector2(whoWidth, whoBox.sizeDelta.y);

            titleLabel.text = string.Format(SlantFormat, titleSlant, context.CardName);
            Vector2 titleSize = titleLabel.GetPreferredValues(titleLabel.text);
            // 빛은 제목 글자 둘레 — 제목처럼 오른쪽 끝에 붙여 둔다
            float spread = titleGlowSpread * _pixelScale;
            titleGlow.sizeDelta = new Vector2(titleSize.x + spread * 2f, titleSize.y + spread * 2f);
            titleGlow.anchoredPosition = new Vector2(spread, titleGlow.anchoredPosition.y);

            // .ci-nin 은 두 줄 중 넓은 쪽 너비 — 커지며 나타날 때 그 가운데가 기준점
            float groupWidth = Mathf.Max(whoBox.gameObject.activeSelf ? whoWidth : 0f, titleSize.x);
            titleGroup.sizeDelta = new Vector2(groupWidth, titleGroup.sizeDelta.y);
            titleGroup.anchoredPosition = new Vector2(-groupWidth / 2f, titleGroup.anchoredPosition.y);

            // 효과 한 줄도 제 가운데를 기준으로 커진다 — 칸을 글자 너비로, 오른쪽 끝은 그대로
            effectLabel.text = effectLine;
            float tagWidth = effectLabel.GetPreferredValues(effectLine).x;
            effectTag.sizeDelta = new Vector2(tagWidth, effectTag.sizeDelta.y);
            _effectTagOrigin = new Vector2(-tagWidth / 2f, _effectTagOrigin.y);

            bubbleLabel.text = say;
            bubble.sizeDelta = bubbleLabel.GetPreferredValues(say) + bubblePadding * _pixelScale;
        }

        // 카드 그림이 있으면 그림 틀, 없으면 캐릭터 동그라미 + 글자 (시안 rareCI)
        private void ShowPicture(CardFxContext context)
        {
            bool hasArt = context.CardArt != null;
            art.gameObject.SetActive(hasArt);
            avatar.gameObject.SetActive(!hasArt);
            _moving = hasArt ? art : avatar;
            iconBadge.gameObject.SetActive(!hasArt && !string.IsNullOrEmpty(icon));
            if (hasArt)
            {
                artPicture.texture = context.CardArt.texture;
                artPicture.uvRect = CoverUv(context.CardArt, artPicture.rectTransform.rect.size, artZoom, artFocus);
                return;
            }

            avatarPicture.sprite = context.Character;
            Vector2 circle = ((RectTransform)avatarPicture.rectTransform.parent).rect.size;
            float aspect = context.Character != null ? context.Character.rect.width / context.Character.rect.height : 1f;
            avatarPicture.rectTransform.sizeDelta = aspect >= 1f ? new Vector2(circle.y * aspect, circle.y) : new Vector2(circle.x, circle.x / aspect);

            iconLabel.text = icon;
            float width = Mathf.Max(iconBadge.sizeDelta.y, iconLabel.GetPreferredValues(icon).x + iconPadding * 2f * _pixelScale);
            iconBadge.sizeDelta = new Vector2(width, iconBadge.sizeDelta.y);
        }

        // 시작 모습 — 시안 fill:both 의 첫 키프레임
        private void ResetState()
        {
            _timeline?.Kill();
            _stripes?.Kill();
            cutIn.gameObject.SetActive(true);
            cutIn.alpha = 1f;
            backdrop.alpha = 0f;
            band.Slide = BandFrom * band.rectTransform.rect.width;
            band.StripeShift = 0f;
            Apply(character, characterGroup, CharacterIn[0], new Vector2(ScreenWidth, 0f), _characterOrigin);
            Apply(_moving, null, Rest(0f), _moving.rect.size, _moving == art ? _artOrigin : _avatarOrigin);
            if (_moving == avatar)
                avatar.localRotation = Quaternion.identity;
            titleGroup.localScale = Vector3.one;
            titleGroupAlpha.alpha = 0f;
            Apply(effectTag, effectTagGroup, TagIn[0], Vector2.one * _pixelScale, _effectTagOrigin);
            Apply(bubble, bubbleGroup, BubbleIn[0], Vector2.zero, _bubbleOrigin);
            Apply(ring, ringGroup, RingOut[0], Vector2.zero, _ringOrigin);
            ringGroup.alpha = 0f;
        }

        private void PlayTimeline()
        {
            float screen = ScreenWidth;
            float bandFrom = BandFrom * band.rectTransform.rect.width;
            Vector2 moveUnit = _moving.rect.size;
            Vector2 moveOrigin = _moving == art ? _artOrigin : _avatarOrigin;
            Pose[] moveKeys = Moves.TryGetValue(move, out Pose[] keys) ? keys : Moves[BandMove.Nod];

            _timeline = DOTween.Sequence().SetLink(gameObject);
            _timeline.Insert(0f, Progress(backdropInMs, Linear, p => backdrop.alpha = p));
            _timeline.Insert(Seconds(bandTime.x), Progress(bandTime.y, Snap, p => band.Slide = Mathf.LerpUnclamped(bandFrom, 0f, p)));
            _timeline.Insert(Seconds(characterTime.x), Animate(characterTime.y, Snap, CharacterIn, pose => Apply(character, characterGroup, pose, new Vector2(screen, 0f), _characterOrigin)));
            _timeline.Insert(Seconds(moveTime.x), Animate(moveTime.y, EaseInOut, moveKeys, pose => Apply(_moving, null, pose, moveUnit, moveOrigin)));
            _timeline.Insert(Seconds(bubbleTime.x), Animate(bubbleTime.y, Linear, BubbleIn, pose => Apply(bubble, bubbleGroup, pose, Vector2.zero, _bubbleOrigin)));
            _timeline.Insert(Seconds(titleTime.x), Animate(titleTime.y, Snap, TitleIn, pose =>
            {
                titleGroup.localScale = new Vector3(pose.ScaleX, pose.ScaleY, 1f);
                titleGroupAlpha.alpha = pose.Alpha;
            }));
            _timeline.Insert(Seconds(ringTime.x), Animate(ringTime.y, EaseOut, RingOut, pose => Apply(ring, ringGroup, pose, Vector2.zero, _ringOrigin)));
            _timeline.Insert(Seconds(tagTime.x), Animate(tagTime.y, Linear, TagIn, pose => Apply(effectTag, effectTagGroup, pose, Vector2.one * _pixelScale, _effectTagOrigin)));
            _timeline.Insert(Seconds(lengthMs - fadeOutMs), Progress(fadeOutMs, Linear, p => cutIn.alpha = 1f - p));
            // 그림 움직임이 끝나면 동그라미는 기울지 않은 처음으로 (시안: 동그라미에는 기본 기울기가 없다)
            if (_moving == avatar)
                _timeline.InsertCallback(Seconds(moveTime.x + moveTime.y), () => avatar.localRotation = Quaternion.identity);

            if (!stillLines && stripeLoopMs > 0f)
                _stripes = DOVirtual.Float(0f, band.StripePeriod, Seconds(stripeLoopMs), value => band.StripeShift = value)
                    .SetEase(Ease.Linear).SetLoops(-1).SetLink(gameObject);
        }

        // 글자 그림자(underlay) 색은 카드마다 달라 이 연출만 쓰는 머티리얼 사본에 넣는다 (연출이 지워질 때 같이 지운다). 투명도는 머티리얼 값 그대로
        private void TintUnderlay(TMP_Text label, Color color)
        {
            Material material = label.fontSharedMaterial;
            if (!_materials.Contains(material))
            {
                material = new Material(material);
                label.fontSharedMaterial = material;
                _materials.Add(material);
            }
            material.SetColor(ShaderUtilities.ID_UnderlayColor, WithAlpha(color, material.GetColor(ShaderUtilities.ID_UnderlayColor).a));
        }

        private float ScreenWidth => ((RectTransform)transform).rect.width;

        private static Tween Progress(float ms, Vector4 ease, Action<float> apply)
        {
            return DOVirtual.Float(0f, 1f, Seconds(ms), p => apply(CubicBezier(ease, p))).SetEase(Ease.Linear);
        }

        private static Tween Animate(float ms, Vector4 ease, Pose[] poses, Action<Pose> apply)
        {
            return Progress(ms, ease, p => apply(Sample(poses, p)));
        }

        // CSS transform 순서(회전 → 이동)대로 — 이동은 회전한 축을 따른다. y 는 위가 + 인 유니티 좌표로 뒤집는다
        private static void Apply(RectTransform target, CanvasGroup group, Pose pose, Vector2 unit, Vector2 origin)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, -pose.Angle);
            Vector2 offset = rotation * new Vector2(pose.X * unit.x, -pose.Y * unit.y);
            target.anchoredPosition = origin + offset;
            target.localRotation = rotation;
            target.localScale = new Vector3(pose.ScaleX, pose.ScaleY, 1f);
            if (group != null)
                group.alpha = pose.Alpha;
        }

        // 키프레임 사이는 곧게 (WAAPI 기본)
        private static Pose Sample(Pose[] poses, float progress)
        {
            for (int i = 0; i < poses.Length - 1; i++)
            {
                if (progress <= poses[i + 1].At)
                    return Pose.Lerp(poses[i], poses[i + 1], Mathf.InverseLerp(poses[i].At, poses[i + 1].At, progress));
            }
            return poses[poses.Length - 1];
        }

        // 카드 그림을 창에 꽉 채우고(cover) 배율만큼 더 키운 뒤, 보일 곳에 맞춰 자른 UV
        private static Rect CoverUv(Sprite sprite, Vector2 window, float zoom, Vector2 focus)
        {
            Texture texture = sprite.texture;
            // 묶이지 않은 스프라이트의 textureRect 는 투명한 가장자리를 잘라 낸 칸(Tight)이라 그림 전체 칸(rect)을 쓴다
            Rect pixels = sprite.packed ? sprite.textureRect : sprite.rect;
            var full = new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
            float spriteAspect = pixels.width / pixels.height;
            float windowAspect = window.y > 0f ? window.x / window.y : 1f;
            float width = (spriteAspect > windowAspect ? windowAspect / spriteAspect : 1f) / zoom;
            float height = (spriteAspect > windowAspect ? 1f : spriteAspect / windowAspect) / zoom;
            // focus 는 위가 0 — UV 는 아래가 0
            float x = (1f - width) * Mathf.Clamp01(focus.x);
            float y = (1f - height) * (1f - Mathf.Clamp01(focus.y));
            return new Rect(full.x + x * full.width, full.y + y * full.height, width * full.width, height * full.height);
        }

        // CSS cubic-bezier(x1, y1, x2, y2) — 진행률 x 에 대한 y
        private static float CubicBezier(Vector4 curve, float x)
        {
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float current = Bezier(curve.x, curve.z, t) - x;
                float slope = 3f * (1f - t) * (1f - t) * curve.x + 6f * (1f - t) * t * (curve.z - curve.x) + 3f * t * t * (1f - curve.z);
                if (Mathf.Abs(slope) < 1e-5f)
                    break;
                t = Mathf.Clamp01(t - current / slope);
            }
            return Bezier(curve.y, curve.w, t);
        }

        private static float Bezier(float p1, float p2, float t)
        {
            float u = 1f - t;
            return 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t;
        }

        private static Pose Rest(float at) => new Pose(at, 0f, 0f, 1f, RestAngle, 1f);

        private static float Seconds(float ms) => ms / 1000f;

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // 시안 키프레임 하나 — 시점(0~1) · 이동(x 오른쪽 + · y 아래 +, 단위는 쓰는 곳이 정함) · 배율 · 각도(도, 시계 방향 +) · 투명도
        private readonly struct Pose
        {
            public readonly float At;
            public readonly float X;
            public readonly float Y;
            public readonly float ScaleX;
            public readonly float ScaleY;
            public readonly float Angle;
            public readonly float Alpha;

            public Pose(float at, float x, float y, float scale, float angle, float alpha) : this(at, x, y, scale, scale, angle, alpha)
            {
            }

            public Pose(float at, float x, float y, float scaleX, float scaleY, float angle, float alpha)
            {
                At = at;
                X = x;
                Y = y;
                ScaleX = scaleX;
                ScaleY = scaleY;
                Angle = angle;
                Alpha = alpha;
            }

            public static Pose Lerp(Pose a, Pose b, float t)
            {
                return new Pose(Mathf.Lerp(a.At, b.At, t), Mathf.Lerp(a.X, b.X, t), Mathf.Lerp(a.Y, b.Y, t), Mathf.Lerp(a.ScaleX, b.ScaleX, t),
                    Mathf.Lerp(a.ScaleY, b.ScaleY, t), Mathf.Lerp(a.Angle, b.Angle, t), Mathf.Lerp(a.Alpha, b.Alpha, t));
            }
        }
    }
}
