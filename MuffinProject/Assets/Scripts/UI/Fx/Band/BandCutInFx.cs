using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
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
    /// 판이 어두워지고 기운 띠가 미끄러져 들어온 뒤, 왼쪽에 카드 그림(틀 없이 오려 낸 캐릭터 — <see cref="BandArtCutOut"/>, 없으면 캐릭터 동그라미 + 글자)이 날아와 움직이고
    /// 말풍선 → 한마디 · 카드 이름 → 효과 한 줄 순으로 뜬 뒤 그림이 한 번 더 움직이고, 끝에 판째 위아래로 접혀 사라진다.
    /// 시안의 번지는 빛(띠 · 제목 · 그림)과 퍼지는 고리, 판째 흐려지기는 뺐다 — 반투명 빛이 겹쳐 흐릿하고 장면이 유령처럼 남았다.
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
        private static readonly Vector4 EaseIn = new Vector4(0.42f, 0f, 1f, 1f);
        private static readonly Vector4 EaseInOut = new Vector4(0.42f, 0f, 0.58f, 1f);
        private static readonly Vector4 Linear = new Vector4(0f, 0f, 1f, 1f);

        // TMP 기울임 태그 — 시안 .ci-nin b 의 skewX(-8deg)
        private const string SlantFormat = "<i angle={0}>{1}</i>";

        [Header("카드 값 (시안 M 한 줄)")]
        [Tooltip("띠 가운데 색 · 제목 그림자 색 · 글자 동그라미 색 (시안 c1).")]
        [SerializeField] private Color bandColor = new Color32(0xe0, 0x11, 0x2b, 0xff);
        [Tooltip("띠 위아래 가장자리 색 (시안 c2).")]
        [SerializeField] private Color bandEdgeColor = new Color32(0x5a, 0x00, 0x10, 0xff);
        [Tooltip("그림이 없을 때 캐릭터 동그라미의 빛 테 색 (시안 glow).")]
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
        [Tooltip("켜면 띠 안에 흐르는 가로 줄이 없다 — 멈춘 띠 (시안 still).")]
        [SerializeField] private bool stillLines;

        [Header("시간표 (ms — 시안 mk · rareCI · cutinFrame)")]
        [Tooltip("컷인 길이 — 끝나기 closeMs 전부터 판째 위아래로 접힌다 (시안 SPECIAL_MS).")]
        [SerializeField] private float lengthMs = 1850f;
        [Tooltip("판째 접혀 사라지는 시간.")]
        [FormerlySerializedAs("fadeOutMs")]
        [SerializeField] private float closeMs = 240f;
        [Tooltip("컷인이 사라진 뒤 4 단계까지 (시안 special2 의 +80).")]
        [SerializeField] private float afterMs = 80f;
        [Tooltip("판이 어두워지는 시간 (.ci-bg).")]
        [SerializeField] private float backdropInMs = 160f;
        [Tooltip("띠가 들어오기 시작 · 들어오는 시간 (.ci-band).")]
        [SerializeField] private Vector2 bandTime = new Vector2(110f, 240f);
        [Tooltip("그림이 날아오기 시작 · 날아오는 시간 (.ci-in).")]
        [SerializeField] private Vector2 characterTime = new Vector2(200f, 380f);
        [Tooltip("그림 움직임 시작 · 시간 (MV, ease-in-out).")]
        [SerializeField] private Vector2 moveTime = new Vector2(380f, 560f);
        [Tooltip("말풍선 시작 · 시간.")]
        [SerializeField] private Vector2 bubbleTime = new Vector2(520f, 900f);
        [Tooltip("한마디 · 카드 이름 시작 · 시간 (F.title).")]
        [SerializeField] private Vector2 titleTime = new Vector2(760f, 380f);
        [Tooltip("효과 한 줄 시작 · 시간 (F.tag).")]
        [SerializeField] private Vector2 tagTime = new Vector2(1150f, 380f);
        [Tooltip("그림이 한 번 더 움직이는 시작 · 시간 (MV, ease-in-out).")]
        [SerializeField] private Vector2 secondMoveTime = new Vector2(1400f, 520f);

        [Header("글자 칸 (시안 px)")]
        [Tooltip("한마디 칸의 좌우 여백 (.ci-nin small padding .7em).")]
        [SerializeField] private float whoPadding = 18.2f;
        [Tooltip("말풍선 크기 = 글자 + 이만큼 (좌우 · 위아래 합, 여백 + 테).")]
        [SerializeField] private Vector2 bubblePadding = new Vector2(51.2f, 14.9f);
        [Tooltip("글자 동그라미의 좌우 여백 (.rr-ico padding .35em + 테).")]
        [SerializeField] private float iconPadding = 21f;
        [Tooltip("제목 기울기 (TMP <i angle> — 100 이 45°쯤. 시안 skewX(-8deg) ≈ 14).")]
        [SerializeField] private int titleSlant = 14;

        [Header("부품")]
        [Tooltip("3 단계 판 전체 — 끝에 판째 위아래로 접힌다.")]
        [SerializeField] private CanvasGroup cutIn;
        [Tooltip("어두운 판 (.ci-bg) — 판의 색 · 투명도는 그림에, 나타나기는 이 투명도로.")]
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private CutInBand band;
        [Tooltip("날아오는 그림 칸 (.ci-in).")]
        [SerializeField] private RectTransform character;
        [SerializeField] private CanvasGroup characterGroup;
        [Tooltip("카드 그림 칸 (.ci-art.ci-cut) — 그림이 있을 때 움직인다.")]
        [SerializeField] private RectTransform art;
        [Tooltip("오려 낸 카드 그림 (art 를 채운다).")]
        [SerializeField] private BandArtCutOut artCutOut;
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
        [Tooltip("효과 한 줄 (.rr-tag).")]
        [SerializeField] private RectTransform effectTag;
        [SerializeField] private CanvasGroup effectTagGroup;
        [SerializeField] private TMP_Text effectLabel;
        [SerializeField] private RectTransform bubble;
        [SerializeField] private CanvasGroup bubbleGroup;
        [SerializeField] private TMP_Text bubbleLabel;
        [Tooltip("빛 색(glow)을 입힐 그림 — 투명도는 그대로 둔다.")]
        [SerializeField] private Graphic[] glowFills;
        [Tooltip("빛 색(glow)을 입힐 도형 테 — 투명도는 그대로 둔다.")]
        [SerializeField] private UIShape[] glowBorders;

        [Header("4 단계 장식")]
        [Tooltip("이 아래 조각을 4 단계에 시작 시각대로 재생한다. 비어 있으면 4 단계 장식 없음.")]
        [SerializeField] private RectTransform post;

        private readonly List<Material> _materials = new List<Material>();
        private readonly List<CardFxStep> _postSteps = new List<CardFxStep>();
        private Vector2 _characterOrigin;
        private Vector2 _artOrigin;
        private Vector2 _avatarOrigin;
        private Vector2 _effectTagOrigin;
        private Vector2 _bubbleOrigin;
        private RectTransform _moving;
        private Sequence _timeline;

        private float _pixelScale = 1.5f;

        private float ScreenWidth => ((RectTransform)transform).rect.width;

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
            Check(artCutOut, nameof(artCutOut));
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
            Check(effectTag, nameof(effectTag));
            Check(effectTagGroup, nameof(effectTagGroup));
            Check(effectLabel, nameof(effectLabel));
            Check(bubble, nameof(bubble));
            Check(bubbleGroup, nameof(bubbleGroup));
            Check(bubbleLabel, nameof(bubbleLabel));
            if (glowFills == null || Array.IndexOf(glowFills, null) >= 0)
                missing.Add(nameof(glowFills));
            if (glowBorders == null || Array.IndexOf(glowBorders, null) >= 0)
                missing.Add(nameof(glowBorders));
            Check(post, nameof(post));
            if (missing.Count > 0)
            {
                Debug.LogError($"[{nameof(BandCutInFx)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
                return;
            }

            // 켜진 4 단계 조각 (꺼진 오브젝트 · 컴포넌트는 빠진다)
            _postSteps.AddRange(Collect(post));
            _characterOrigin = character.anchoredPosition;
            _artOrigin = art.anchoredPosition;
            _avatarOrigin = avatar.anchoredPosition;
            _effectTagOrigin = effectTag.anchoredPosition;
            _bubbleOrigin = bubble.anchoredPosition;

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
            // 오려 낸 그림을 만드는 동안 (일꾼 스레드, 한 프레임 ~ 몇 프레임) 기다린다 — 그림 없이 날아오지 않게
            while (!artCutOut.IsReady)
                yield return null;
            PlayTimeline();
            yield return new WaitForSeconds(Seconds(lengthMs + afterMs));
            cutIn.gameObject.SetActive(false);
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            if (_postSteps.Count == 0)
                yield break;

            foreach (CardFxStep step in _postSteps)
                step.ResetState();
            // 시작 시각 0 인 조각은 첫 yield 전에 재생된다 — 결과 알림을 받는 조각이 부르는 쪽의 알림보다 먼저 구독한다
            yield return TimelineRoutine(_postSteps, context, null);
        }

        // 카드 값을 부품에 넣는다 — 글자 · 색 · 그림 · 칸 크기
        private void ShowCard(CardFxContext context)
        {
            band.SetColors(bandColor, bandEdgeColor);
            foreach (Graphic graphic in glowFills)
                graphic.color = WithAlpha(glowColor, graphic.color.a);
            foreach (UIShape shape in glowBorders)
                shape.BorderColor = WithAlpha(glowColor, shape.BorderColor.a);
            iconFill.color = WithAlpha(bandColor, iconFill.color.a);
            TintUnderlay(titleLabel, bandColor);
            // 효과 한 줄은 띠 위의 흰 글자 — 그림자만 띠 가장자리 색
            TintUnderlay(effectLabel, bandEdgeColor);

            ShowPicture(context);

            whoLabel.text = who;
            whoBox.gameObject.SetActive(!string.IsNullOrEmpty(who));
            float whoWidth = whoLabel.GetPreferredValues(who).x + whoPadding * 2f * _pixelScale;
            whoBox.sizeDelta = new Vector2(whoWidth, whoBox.sizeDelta.y);

            titleLabel.text = string.Format(SlantFormat, titleSlant, context.CardName);
            Vector2 titleSize = titleLabel.GetPreferredValues(titleLabel.text);

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
                artCutOut.Build(context.CardArt, glowColor, _pixelScale);
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
            cutIn.gameObject.SetActive(true);
            cutIn.alpha = 1f;
            cutIn.transform.localScale = Vector3.one;
            backdrop.alpha = 0f;
            band.Slide = BandFrom * band.Width;
            band.ShowStreaks(!stillLines);
            Apply(character, characterGroup, CharacterIn[0], new Vector2(ScreenWidth, 0f), _characterOrigin);
            Apply(_moving, null, Rest(0f), _moving.rect.size, _moving == art ? _artOrigin : _avatarOrigin);
            if (_moving == avatar)
                avatar.localRotation = Quaternion.identity;
            titleGroup.localScale = Vector3.one;
            titleGroupAlpha.alpha = 0f;
            Apply(effectTag, effectTagGroup, TagIn[0], Vector2.one * _pixelScale, _effectTagOrigin);
            Apply(bubble, bubbleGroup, BubbleIn[0], Vector2.zero, _bubbleOrigin);
        }

        private void PlayTimeline()
        {
            float screen = ScreenWidth;
            float bandFrom = BandFrom * band.Width;
            Vector2 moveUnit = _moving.rect.size;
            Vector2 moveOrigin = _moving == art ? _artOrigin : _avatarOrigin;
            Pose[] moveKeys = Moves.TryGetValue(move, out Pose[] keys) ? keys : Moves[BandMove.Nod];
            // 동그라미에는 기본 기울기가 없다 (시안) — 키의 각도에서 B(rotate(-3deg))를 뺀다
            float moveAngle = _moving == avatar ? -RestAngle : 0f;

            _timeline = DOTween.Sequence().SetLink(gameObject);
            _timeline.Insert(0f, Progress(backdropInMs, Linear, p => backdrop.alpha = p));
            _timeline.Insert(Seconds(bandTime.x), Progress(bandTime.y, Snap, p => band.Slide = Mathf.LerpUnclamped(bandFrom, 0f, p)));
            _timeline.Insert(Seconds(characterTime.x), Animate(characterTime.y, Snap, CharacterIn, pose => Apply(character, characterGroup, pose, new Vector2(screen, 0f), _characterOrigin)));
            _timeline.Insert(Seconds(moveTime.x), Animate(moveTime.y, EaseInOut, moveKeys, pose => Apply(_moving, null,
                new Pose(pose.At, pose.X, pose.Y, pose.ScaleX, pose.ScaleY, pose.Angle + moveAngle, pose.Alpha), moveUnit, moveOrigin)));
            _timeline.Insert(Seconds(bubbleTime.x), Animate(bubbleTime.y, Linear, BubbleIn, pose => Apply(bubble, bubbleGroup, pose, Vector2.zero, _bubbleOrigin)));
            _timeline.Insert(Seconds(titleTime.x), Animate(titleTime.y, Snap, TitleIn, pose =>
            {
                titleGroup.localScale = new Vector3(pose.ScaleX, pose.ScaleY, 1f);
                titleGroupAlpha.alpha = pose.Alpha;
            }));
            _timeline.Insert(Seconds(tagTime.x), Animate(tagTime.y, Linear, TagIn, pose => Apply(effectTag, effectTagGroup, pose, Vector2.one * _pixelScale, _effectTagOrigin)));
            _timeline.Insert(Seconds(secondMoveTime.x), Animate(secondMoveTime.y, EaseInOut, moveKeys, pose => Apply(_moving, null,
                new Pose(pose.At, pose.X, pose.Y, pose.ScaleX, pose.ScaleY, pose.Angle + moveAngle, pose.Alpha), moveUnit, moveOrigin)));
            // 판째 위아래로 접힌다 — 끝으로 갈수록 빨리
            _timeline.Insert(Seconds(lengthMs - closeMs), Progress(closeMs, EaseIn, p => cutIn.transform.localScale = new Vector3(1f, 1f - p, 1f)));

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

        private static Tween Progress(float ms, Vector4 ease, Action<float> apply)
        {
            return DOVirtual.Float(0f, 1f, Seconds(ms), p => apply(FxLayer.CubicBezier(ease, p))).SetEase(Ease.Linear);
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
