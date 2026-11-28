using System.Collections;
using System.Collections.Generic;
using Chapchu.UI.Fx.Gaepan;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Domain
{
    /// <summary>
    /// 「영역전개」 연출 (3 단계 컷인, 4.95초) — 시안 「카드 이펙트 v2」 SPECIAL["영역전개"] (영역 안은 B 안: 은하 소용돌이).
    /// 어두운 방에 눈 감은 강아지가 다가온다 → 눈을 팍 뜬다(번쩍 · 고리 · 집중선 · 눈빛) → 흰 번쩍임과 함께 영역 안: 별이 빛줄기로 빨려 들며 은하가 펼쳐지고 강아지는 흑백으로 뒤집혀 흐릿하게 떠 있다
    /// → 한자 네 글자 領域展開 가 왼위 · 오른위 · 왼아래 · 오른아래 순서로 멀리서 날아와 박힌다 → 사라진다.
    /// 그 순간의 모습을 시각 하나로 다 정한다(<see cref="Evaluate"/>) — 트윈을 걸어 두지 않아 어느 시각이든 그대로 다시 그릴 수 있다.
    /// 연출 길이 4.95초 — 카드 처리 시간(resolveSeconds)에 3 단계 길이를 더해 두었다 (10-ui.md 5-1). 길이를 바꾸면 그 값도 같이 바꾼다.
    /// 소리(CardFxSound/Card_1008)는 아래 시간표 그대로 녹음한 한 파일이다 — 시각을 바꾸면 소리와 어긋난다.
    /// 4 단계 장식은 없다. 영역이 떠 있는 동안의 판 표시는 게임 화면 몫 (10-ui 「판 효과 줄」).
    /// </summary>
    public class DomainFx : CardFx
    {
        // 시안 1px — 화면 짧은 변 800 기준
        private const float DesignSide = 800f;
        // 원근 거리 (시안 .ci-dx · .dom-kj 의 perspective)
        private const float DogPerspective = 1000f, KanjiPerspective = 1100f;
        // 흔들릴 때 가장자리가 비지 않게 화면 밖으로 더 까는 폭 (시안 px)
        private const float Overscan = 40f;
        // 타원 그라데이션이 화면 구석까지 닿는 크기 (CSS farthest-corner = √2 배) + 흔들림 여유
        private const float CornerReach = 1.4143f * 1.06f;

        // CSS 이징
        private static readonly Vector4 EaseOut = new Vector4(0f, 0f, 0.58f, 1f);
        private static readonly Vector4 EaseInOut = new Vector4(0.42f, 0f, 0.58f, 1f);
        private static readonly Vector4 ArriveCurve = new Vector4(0.2f, 0.8f, 0.3f, 1f);
        private static readonly Vector4 PunchCurve = new Vector4(0.2f, 0.9f, 0.3f, 1f);
        private static readonly Vector4 DriftCurve = new Vector4(0.2f, 0.7f, 0.3f, 1f);
        private static readonly Vector4 CameraCurve = new Vector4(0.3f, 0.2f, 0.3f, 1f);

        // 키프레임 — (진행률, 값) 쌍
        // 눈 뜨기: 눈구멍이 눈보다 크게 열렸다가(1.28) 제자리로 (시안 openEyes, ms)
        private static readonly float[] EyeOpen = { 0f, 0f, 55f, 1.28f, 110f, 0.9f, 165f, 1.03f, 200f, 1f };
        // 눈 뜨는 순간 강아지가 움찔한다: 크기 · 좌우로 돌린 각 · 위아래로 숙인 각
        private static readonly float[] PunchScale = { 0f, 1.04f, 0.25f, 1.12f, 0.6f, 1.07f, 1f, 1.07f };
        private static readonly float[] PunchTurn = { 0f, 0f, 0.25f, -9f, 0.6f, 3f, 1f, 0f };
        private static readonly float[] PunchNod = { 0f, 0f, 0.25f, 4f, 0.6f, -1f, 1f, 0f };
        private static readonly float[] VignetteAlpha = { 0f, 0f, 0.2f, 1f, 1f, 0.55f };
        private static readonly float[] SpeedAlpha = { 0f, 0f, 0.25f, 0.9f, 1f, 0f };
        private static readonly float[] SpeedScale = { 0f, 0.6f, 0.25f, 1f, 1f, 1.25f };
        private static readonly float[] SpeedTurn = { 0f, 0f, 0.25f, 4f, 1f, 8f };
        private static readonly float[] MarkAlpha = { 0f, 0f, 0.4f, 1f, 1f, 0.85f };
        private static readonly float[] MarkScale = { 0f, 0.4f, 0.4f, 1.25f, 1f, 1f };
        private static readonly float[] IrisAlpha = { 0f, 0f, 0.3f, 1f, 1f, 1f };
        private static readonly float[] IrisBright = { 0f, 2.4f, 0.3f, 1.6f, 1f, 1f };
        // 영역 안의 강아지: 진하기 · 깊이(뒤가 −) · 돌린 각 · 숙인 각
        private static readonly float[] VoidAlpha = { 0f, 0f, 0.5f, 0.32f, 1f, 0.32f };
        private static readonly float[] VoidDepth = { 0f, -500f, 0.5f, -120f, 1f, -60f };
        private static readonly float[] VoidTurn = { 0f, 18f, 0.5f, -4f, 1f, -12f };
        private static readonly float[] VoidNod = { 0f, 8f, 0.5f, 2f, 1f, 0f };
        // 한자 네 글자를 함께 돌려 보는 각
        private static readonly float[] CameraNod = { 0f, 18f, 0.6f, 8f, 1f, 12f };
        private static readonly float[] CameraTurn = { 0f, -24f, 0.6f, 2f, 1f, 20f };
        // 한 글자: 멀리서 날아와 화면을 뚫고 나왔다가(140) 제자리에 박힌다
        private static readonly float[] GlyphAlpha = { 0f, 0f, 0.45f, 1f, 1f, 1f };
        private static readonly float[] GlyphDepth = { 0f, -1800f, 0.45f, 140f, 0.7f, -20f, 1f, 0f };
        private static readonly float[] GlyphNod = { 0f, 50f, 0.45f, -8f, 0.7f, 3f, 1f, 0f };
        private static readonly float[] GlyphTurn = { 0f, -40f, 0.45f, 6f, 0.7f, 0f, 1f, 0f };
        private static readonly float[] KanjiPulse = { 0f, 1f, 0.5f, 1.05f, 1f, 1.02f };

        [Header("판")]
        [Tooltip("연출 전체 — 끝에서 사라진다.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("흔들리는 판. 검은 바탕만 빼고 모두 이 안에 있다.")]
        [SerializeField] private RectTransform stage;
        [Tooltip("위아래 검은 띠 (화면 높이의 10%).")]
        [SerializeField] private RectTransform barTop;
        [SerializeField] private RectTransform barBottom;

        [Header("방 (영역에 들어가기 전)")]
        [Tooltip("어두운 방의 빛 (타원 그라데이션).")]
        [SerializeField] private Graphic room;
        [Tooltip("눈을 뜬 뒤 둘레를 누르는 어둠.")]
        [SerializeField] private Graphic vignette;
        [Tooltip("눈을 뜨는 순간의 집중선.")]
        [SerializeField] private UIQuadBatch speedLines;

        [Header("강아지")]
        [SerializeField] private RectTransform dog;
        [SerializeField] private CanvasGroup dogGroup;
        [Tooltip("숨쉬는 몸 — 눈 감은 그림 · 눈구멍 · 눈 뜬 그림.")]
        [SerializeField] private RectTransform body;
        [SerializeField] private Graphic closed;
        [SerializeField] private DomainEyes eyes;
        [SerializeField] private Graphic open;
        [Tooltip("눈 안에서 푸르게 켜지는 홍채, 그리고 밝게 번쩍일 때 겹치는 같은 그림 (더하기).")]
        [SerializeField] private Graphic iris;
        [SerializeField] private Graphic irisGlow;
        [Tooltip("이마의 두 무늬 위에 켜지는 빛.")]
        [SerializeField] private Graphic[] marks;
        [Tooltip("두 눈 둘레의 은은한 빛 번짐.")]
        [SerializeField] private Graphic[] eyeGlows;
        [Tooltip("눈의 짧은 반짝임(십자)을 그리는 판 — 강아지 칸에 꽉 차게.")]
        [SerializeField] private UIQuadBatch glints;
        [Tooltip("영역 안에서 눈 뜬 그림에 거는 흑백 뒤집기 머티리얼.")]
        [SerializeField] private Material voidMaterial;

        [Header("영역")]
        [SerializeField] private DomainSpace space;
        [Tooltip("영역에 들어가는 순간의 흰 번쩍임.")]
        [SerializeField] private Graphic strobe;

        [Header("한자")]
        [Tooltip("네 글자 묶음 — 마지막에 한 번 부푼다. 화면 가운데.")]
        [SerializeField] private RectTransform kanji;
        [Tooltip("글자 뒤에 깔리는 어둠.")]
        [SerializeField] private Graphic dark;
        [Tooltip("領 · 域 · 展 · 開 순서.")]
        [SerializeField] private DomainGlyph[] glyphs;

        [Header("겹치는 효과")]
        [Tooltip("번쩍임 — 눈 뜰 때 · 글자가 박힐 때.")]
        [SerializeField] private Graphic flash;
        [Tooltip("퍼지는 고리 — 같은 때에 많아야 넷.")]
        [SerializeField] private UIRing[] rings;

        [Header("시간표 (ms — 시안 SPECIAL[\"영역전개\"]. 소리 파일이 이 시각에 맞춰 녹음돼 있다)")]
        [Tooltip("강아지가 나타나기 시작하는 시각.")]
        [SerializeField] private float dogMs = 150f;
        [Tooltip("눈을 뜨는 시각.")]
        [SerializeField] private float eyeMs = 1350f;
        [Tooltip("영역 안으로 들어가는 시각.")]
        [SerializeField] private float voidMs = 2160f;
        [Tooltip("첫 글자가 날아오기 시작하는 시각 · 글자 사이 간격.")]
        [SerializeField] private float kanjiMs = 3060f;
        [SerializeField] private float kanjiGapMs = 330f;
        [Tooltip("3 단계 길이 · 끝에서 사라지는 시간.")]
        [SerializeField] private float durationMs = 4950f;
        [SerializeField] private float fadeOutMs = 260f;

        [Header("세기 (시안 px · 화면 짧은 변 800 기준)")]
        [Tooltip("판 흔들림: 눈 뜰 때 · 은하에 닿을 때 · 글자 · 마지막 글자.")]
        [SerializeField] private float eyeShake = 16f;
        [SerializeField] private float arriveShake = 12f;
        [SerializeField] private float glyphShake = 13f;
        [SerializeField] private float lastGlyphShake = 22f;
        [Tooltip("번쩍임 진하기: 눈 뜰 때 · 글자 · 마지막 글자.")]
        [SerializeField] private float eyeFlash = 0.55f;
        [SerializeField] private float glyphFlash = 0.45f;
        [SerializeField] private float lastGlyphFlash = 0.75f;
        [Tooltip("글자 뒤 어둠의 진하기.")]
        [SerializeField] private float darkAlpha = 0.62f;

        [Header("색")]
        [Tooltip("눈 뜰 때 퍼지는 고리 둘.")]
        [SerializeField] private Color eyeRingColor = Color.white;
        [SerializeField] private Color eyeRingColor2 = new Color32(0xB9, 0xC8, 0xFF, 0xFF);
        [Tooltip("글자가 박힐 때 · 마지막에 퍼지는 고리.")]
        [SerializeField] private Color kanjiRingColor = new Color32(0xE6, 0xDE, 0xFF, 0xFF);
        [Tooltip("집중선.")]
        [SerializeField] private Color speedColor = new Color(1f, 1f, 1f, 0.55f);

        private struct ShakeEvent
        {
            public float At, Duration;
            public Vector2[] Keys;
        }

        private struct FlashEvent
        {
            public float At, Duration, Alpha;
        }

        private struct RingEvent
        {
            public float At, Duration, Radius;
            public Vector2 Position;
            public Color Color;
        }

        private readonly List<ShakeEvent> _shakes = new List<ShakeEvent>();
        private readonly List<FlashEvent> _flashes = new List<FlashEvent>();
        private readonly List<RingEvent> _ringEvents = new List<RingEvent>();
        private Vector2 _screen;
        private float _unit;
        private float _dogSize;
        private Vector2 _dogHome;
        private Vector2 _eyeLine;
        private Vector2[] _glyphHomes;

        private void Awake()
        {
            if (group == null || stage == null || barTop == null || barBottom == null || room == null || vignette == null || speedLines == null
                || dog == null || dogGroup == null || body == null || closed == null || eyes == null || open == null || iris == null || irisGlow == null
                || glints == null || voidMaterial == null || space == null || strobe == null || kanji == null || dark == null || flash == null
                || marks.Length == 0 || eyeGlows.Length == 0 || glyphs.Length == 0 || rings.Length == 0)
                Debug.LogError($"[{nameof(DomainFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            group.alpha = 0f;
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            LoadSound(context);
            Prepare();
            StartSound();

            float start = Time.time;
            for (float ms = 0f; ms < durationMs; ms = (Time.time - start) * 1000f)
            {
                Evaluate(ms);
                yield return null;
            }

            group.alpha = 0f;
            space.Clear();
        }

        // 화면 크기에 맞춰 자리를 잡고, 재생마다 달라지는 것(우주 · 흔들림)을 새로 뽑는다
        private void Prepare()
        {
            _screen = ((RectTransform)transform).rect.size;
            float width = _screen.x, height = _screen.y, side = Mathf.Min(width, height);
            _unit = side / DesignSide;
            float overscan = Overscan * _unit;

            // 띠: 화면 높이의 10%, 흔들려도 비지 않게 바깥으로 넉넉히
            float bar = height * 0.1f;
            barTop.sizeDelta = barBottom.sizeDelta = new Vector2(overscan * 2f, bar + overscan);

            // 타원 그라데이션: 가운데가 화면 가운데보다 조금 아래 (시안 at 50% 60% · 50% 55%)
            Place(room.rectTransform, 0.6f);
            Place(vignette.rectTransform, 0.55f);
            Place(dark.rectTransform, 0.5f);

            // 강아지: 가로 화면은 아래로 조금 잘리게 크게, 세로 화면은 폭에 맞춰 (시안 .dx-dog · max-aspect-ratio:1/1)
            bool portrait = width <= height;
            _dogSize = portrait ? Mathf.Min(width * 1.5f, height * 0.8f) : Mathf.Min(height * 1.04f, width * 0.98f);
            float bottom = portrait ? height * 0.03f : -height * 0.12f;
            dog.sizeDelta = new Vector2(_dogSize, _dogSize);
            _dogHome = new Vector2(0f, bottom + _dogSize * dog.pivot.y);

            // 눈높이 (화면 가운데 기준) — 눈 뜰 때의 고리 · 집중선이 여기서 퍼진다. 그때 강아지는 1.04 배
            float pivotY = _dogHome.y - height * 0.5f;
            _eyeLine = new Vector2(0f, pivotY + (1f - 0.515f - dog.pivot.y) * _dogSize * PunchScale[1]);

            // 한자: 2 × 2, 가운데에서 화면 폭 · 높이의 1% 씩 띄운다 (시안 .dom-ch)
            float glyph = Mathf.Min(height * 0.42f, width * 0.36f);
            _glyphHomes = new Vector2[glyphs.Length];
            for (int i = 0; i < glyphs.Length; i++)
            {
                _glyphHomes[i] = new Vector2((i % 2 == 0 ? -1f : 1f) * (glyph * 0.5f + width * 0.01f), (i < 2 ? 1f : -1f) * (glyph * 0.5f + height * 0.01f));
                glyphs[i].rectTransform.sizeDelta = new Vector2(glyph, glyph);
                glyphs[i].rectTransform.anchoredPosition = _glyphHomes[i];
            }

            BuildSpeedLines(Mathf.Max(width, height));
            BuildEvents(side, glyph);
            space.Build();
            space.Clear();
            glints.Clear();
            glints.Apply();
            group.alpha = 1f;
            Evaluate(0f);
        }

        // 타원 그라데이션 칸을 화면 구석까지 닿게 — centerY = 가운데 높이 (위 기준 비율)
        private void Place(RectTransform rect, float centerY)
        {
            float reach = Mathf.Max(centerY, 1f - centerY) * 2f;
            rect.sizeDelta = new Vector2(_screen.x * CornerReach, _screen.y * reach * CornerReach);
            rect.anchoredPosition = new Vector2(0f, (0.5f - centerY) * _screen.y);
        }

        // 시안 .dx-speed — 7° 마다 0.6° 폭의 흰 선, 가운데(14%)는 비우고 26% 에서 가장 진하고 62% 에서 사라진다. 한 변 = 화면 긴 변의 1.8배
        private void BuildSpeedLines(float longSide)
        {
            const float step = 7f, width = 0.6f;
            float radius = longSide * 1.8f * 0.5f * 1.4143f;
            float start = radius * 0.14f, peak = radius * 0.26f, end = radius * 0.62f;
            Color32 solid = speedColor, clear = new Color(speedColor.r, speedColor.g, speedColor.b, 0f);
            speedLines.Clear();
            for (float angle = 0f; angle < 360f - width; angle += step)
            {
                Vector2 a = Direction(angle), b = Direction(angle + width);
                speedLines.Quad(a * start, clear, a * peak, solid, b * peak, solid, b * start, clear);
                speedLines.Quad(a * peak, solid, a * end, clear, b * end, clear, b * peak, solid);
            }
            speedLines.Apply();
            speedLines.rectTransform.anchoredPosition = _eyeLine;
        }

        // 흔들림 · 번쩍임 · 고리가 일어나는 때 (시안 shakeC · dom-fl · ringAt)
        private void BuildEvents(float side, float glyph)
        {
            _shakes.Clear();
            _flashes.Clear();
            _ringEvents.Clear();

            // 눈을 뜬다
            AddShake(eyeMs, eyeShake, 300f);
            _flashes.Add(new FlashEvent { At = eyeMs, Duration = 200f, Alpha = eyeFlash });
            AddRing(eyeMs, 520f, 26f, _eyeLine, eyeRingColor, side);
            AddRing(eyeMs + 70f, 640f, 40f, _eyeLine, eyeRingColor2, side);

            // 은하에 닿는다
            AddShake(voidMs + 800f, arriveShake, 260f);

            // 글자가 박힌다 — 마지막 글자는 더 세게
            for (int i = 0; i < glyphs.Length; i++)
            {
                bool last = i == glyphs.Length - 1;
                float at = kanjiMs + i * kanjiGapMs;
                AddShake(at, last ? lastGlyphShake : glyphShake, last ? 380f : 260f);
                _flashes.Add(new FlashEvent { At = at, Duration = last ? 220f : 150f, Alpha = last ? lastGlyphFlash : glyphFlash });
                AddRing(at + 200f, 520f, 14f, _glyphHomes[i], kanjiRingColor, side);
            }

            // 네 글자가 다 박힌 뒤 가운데서 고리 셋
            float done = kanjiMs + (glyphs.Length - 1) * kanjiGapMs + 520f;
            for (int i = 0; i < 3; i++)
                AddRing(done + i * 120f, 700f, 40f, Vector2.zero, kanjiRingColor, side);
        }

        private void AddShake(float at, float amount, float duration)
        {
            // 시안 shakeC — 무작위 여덟 곳을 거쳐 제자리로
            var keys = new Vector2[9];
            for (int i = 0; i < 8; i++)
                keys[i] = new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount)) * _unit;
            _shakes.Add(new ShakeEvent { At = at, Duration = duration, Keys = keys });
        }

        // size = 고리 지름 (화면 짧은 변의 %, 시안 vmin)
        private void AddRing(float at, float duration, float size, Vector2 position, Color color, float side)
        {
            _ringEvents.Add(new RingEvent { At = at, Duration = duration, Radius = size * 0.01f * side * 0.5f, Position = position, Color = color });
        }

        // ms 시각의 모습을 통째로 그린다
        private void Evaluate(float ms)
        {
            EvaluateFrame(ms);
            EvaluateRoom(ms);
            if (ms < voidMs)
                EvaluateDog(ms);
            else
                EvaluateVoid(ms);
            EvaluateEyeLights(ms);
            EvaluateKanji(ms);
            EvaluateEvents(ms);
        }

        // 띠 · 전체 진하기
        private void EvaluateFrame(float ms)
        {
            float overscan = Overscan * _unit;
            float hidden = _screen.y * 0.1f * (1f - Ease(EaseOut, Progress(ms, 0f, 260f)));
            barTop.anchoredPosition = new Vector2(0f, overscan + hidden);
            barBottom.anchoredPosition = new Vector2(0f, -overscan - hidden);
            group.alpha = 1f - Progress(ms, durationMs - fadeOutMs, fadeOutMs);
        }

        // 방의 빛 · 둘레 어둠 · 영역에 들어가는 번쩍임 · 집중선
        private void EvaluateRoom(float ms)
        {
            // 방은 영역에 들어가기 직전(눈 뜨고 0.43초 뒤)에 꺼진다
            SetAlpha(room, Progress(ms, dogMs, 400f) * (1f - Progress(ms, eyeMs + 430f, 300f)));
            SetAlpha(vignette, ms < eyeMs ? 0f : Keys(Progress(ms, eyeMs, 700f), VignetteAlpha));
            // 영역에 들어가는 순간 화면이 하얗게 번쩍였다가 0.28초에 걷힌다
            SetAlpha(strobe, ms < voidMs ? 0f : 1f - Progress(ms, voidMs, 280f));

            float burst = Progress(ms, eyeMs, 520f);
            bool on = ms >= eyeMs && burst < 1f;
            SetAlpha(speedLines, on ? Keys(burst, SpeedAlpha) : 0f);
            if (!on) return;

            float scale = Keys(burst, SpeedScale);
            speedLines.rectTransform.localScale = new Vector3(scale, scale, 1f);
            speedLines.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Keys(burst, SpeedTurn));
        }

        // 영역에 들어가기 전의 강아지: 나타남 → 천천히 다가옴 · 숨쉬기 → 눈 뜨며 움찔
        private void EvaluateDog(float ms)
        {
            dogGroup.alpha = Ease(ArriveCurve, Progress(ms, dogMs, 420f));
            if (ms < eyeMs)
            {
                PoseDog(1f + (PunchScale[1] - 1f) * Progress(ms, dogMs, 1000f), 0f, 0f, 0f);
            }
            else
            {
                float punch = Ease(PunchCurve, Progress(ms, eyeMs, 420f));
                PoseDog(Keys(punch, PunchScale), Keys(punch, PunchNod), Keys(punch, PunchTurn), 0f);
            }

            // 숨쉬기: 1.5초에 한 번 살짝 올라오며 커진다
            float cycle = Mathf.Repeat(Mathf.Max(0f, ms - dogMs) / 1500f, 1f);
            float breath = 1f - Mathf.Abs(Ease(EaseInOut, cycle) * 2f - 1f);
            body.anchoredPosition = new Vector2(0f, _dogSize * 0.006f * breath);
            body.localScale = Vector3.one * (1f + 0.012f * breath);

            // 눈: 0.2초에 걸쳐 눈구멍이 열리고, 다 열리면 눈 뜬 그림으로 바꾼다
            float since = ms - eyeMs;
            bool opened = since >= EyeOpen[EyeOpen.Length - 2];
            closed.enabled = !opened;
            open.enabled = opened;
            open.material = null;
            eyes.SetOpen(opened || since <= 0f ? 0f : KeysEased(since, EyeOpen));
        }

        // 영역 안의 강아지: 흑백으로 뒤집혀, 멀리서 천천히 다가오며 흐릿하게 떠 있다
        private void EvaluateVoid(float ms)
        {
            float drift = Ease(DriftCurve, Progress(ms, voidMs, 2800f));
            dogGroup.alpha = Keys(drift, VoidAlpha);
            PoseDog(1f, Keys(drift, VoidNod), Keys(drift, VoidTurn), Keys(drift, VoidDepth));
            body.anchoredPosition = Vector2.zero;
            body.localScale = Vector3.one;
            closed.enabled = false;
            open.enabled = true;
            open.material = voidMaterial;
            eyes.SetOpen(0f);

            space.Draw((ms - voidMs) / 1000f, _screen, _unit);
        }

        // 강아지 자세 — 깊이만큼 작아지며 화면 가운데로 모인다 (원근). 돌린 각은 폭이 줄어드는 만큼만 흉내 낸다 (화면이 평면이라 기울어 보이지는 않는다)
        private void PoseDog(float scale, float nod, float turn, float depth)
        {
            float far = DogPerspective / (DogPerspective - depth);
            dog.localScale = new Vector3(scale * far * Mathf.Cos(turn * Mathf.Deg2Rad), scale * far * Mathf.Cos(nod * Mathf.Deg2Rad), 1f);
            var fromCenter = new Vector2(0f, _dogHome.y - _screen.y * 0.5f);
            dog.anchoredPosition = _dogHome + fromCenter * (far - 1f);
        }

        // 눈빛: 홍채 · 이마 무늬 · 눈 둘레 빛 번짐 · 짧은 반짝임. 영역에 들어가면 0.12초에 꺼진다
        private void EvaluateEyeLights(float ms)
        {
            float off = 1f - Progress(ms, voidMs, 120f);

            // 홍채: 눈 뜨고 0.11초 뒤 밝게 켜졌다가 가라앉고, 그 뒤 0.9초마다 살짝 밝아진다
            float irisSince = ms - (eyeMs + 110f);
            float irisAlpha = 0f, bright = 1f;
            if (irisSince >= 0f && irisSince < 420f)
            {
                irisAlpha = Keys(irisSince / 420f, IrisAlpha);
                bright = Keys(irisSince / 420f, IrisBright);
            }
            else if (irisSince >= 420f)
            {
                irisAlpha = 1f;
                float pulse = (irisSince - 420f) / 900f;
                bright = pulse < 3f ? 1f + 0.25f * (1f - Mathf.Abs(Mathf.Repeat(pulse, 1f) * 2f - 1f)) : 1f;
            }
            SetAlpha(iris, irisAlpha * off);
            SetAlpha(irisGlow, irisAlpha * off * Mathf.Clamp01(bright - 1f));

            float mark = Progress(ms, eyeMs, 500f);
            foreach (Graphic graphic in marks)
            {
                SetAlpha(graphic, ms < eyeMs ? 0f : Keys(mark, MarkAlpha) * off);
                float scale = Keys(mark, MarkScale);
                graphic.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            // 시안 eyeFx: 눈 뜬 뒤 e 초 — 빛 번짐은 영역에 들어갈 때까지, 반짝임은 0.12 ~ 0.42초에 한 번
            float e = (ms - eyeMs) / 1000f;
            bool lit = e >= 0f && ms < voidMs;
            float on = Mathf.Clamp01((e - 0.1f) / 0.08f);
            float glow = lit ? on * (e < 0.35f ? 1f - (e - 0.1f) * 1.6f : 0.6f + 0.08f * Mathf.Sin(e * 7f)) : 0f;
            foreach (Graphic graphic in eyeGlows)
                SetAlpha(graphic, glow);

            float glint = lit && e > 0.12f && e < 0.42f ? Mathf.Sin((e - 0.12f) / 0.3f * Mathf.PI) : 0f;
            glints.Clear();
            if (glint > 0f)
            {
                foreach (Graphic graphic in eyeGlows)
                    DrawGlint(graphic.rectTransform, glint);
            }
            glints.Apply();
        }

        // 눈 왼쪽 위의 십자 반짝임 — 가로로 길고 세로로 짧다 (시안 gl). eye = 그 눈의 빛 번짐 칸
        private void DrawGlint(RectTransform eye, float strength)
        {
            // 빛 번짐 칸의 가로 반지름 = 눈 폭 × 0.95
            float span = eye.rect.width * 0.5f / 0.95f;
            Vector2 center = (Vector2)glints.rectTransform.InverseTransformPoint(eye.position) + new Vector2(-span * 0.16f, span * 0.14f);
            float length = span * 0.55f * strength, thick = 1.6f * _unit;
            Color32 core = new Color(1f, 1f, 1f, 0.9f * strength), tip = new Color(160f / 255f, 210f / 255f, 1f, 0f);
            foreach (Vector2 arm in new[] { new Vector2(length, 0f), new Vector2(-length, 0f), new Vector2(0f, length * 0.6f), new Vector2(0f, -length * 0.6f) })
            {
                Vector2 side = new Vector2(-arm.y, arm.x).normalized * thick;
                glints.Quad(center - side, core, center + side, core, center + arm + side, tip, center + arm - side, tip);
            }
        }

        // 한자: 어둠이 깔리고, 한 글자씩 날아와 박히고, 다 박히면 한 번 부푼다
        private void EvaluateKanji(float ms)
        {
            float before = kanjiMs - 120f;
            SetAlpha(dark, darkAlpha * Progress(ms, before, 160f));

            float look = Ease(CameraCurve, Progress(ms, before, 2200f));
            var cameraAngles = new Vector2(Keys(look, CameraNod), Keys(look, CameraTurn));
            for (int i = 0; i < glyphs.Length; i++)
            {
                float start = kanjiMs + i * kanjiGapMs;
                if (ms < start)
                {
                    SetAlpha(glyphs[i], 0f);
                    continue;
                }

                // 왼쪽 글자와 오른쪽 글자는 서로 반대로 돌며 들어온다
                float mirror = i % 2 == 0 ? -1f : 1f;
                float slam = Ease(ArriveCurve, Progress(ms, start, 480f));
                SetAlpha(glyphs[i], Keys(slam, GlyphAlpha));
                glyphs[i].Set(new Vector3(Keys(slam, GlyphNod), mirror * Keys(slam, GlyphTurn), Keys(slam, GlyphDepth)), cameraAngles, _glyphHomes[i], KanjiPerspective, _unit);
            }

            float done = kanjiMs + (glyphs.Length - 1) * kanjiGapMs + 520f;
            float pulse = ms < done ? 1f : Keys(Ease(EaseOut, Progress(ms, done, 700f)), KanjiPulse);
            kanji.localScale = new Vector3(pulse, pulse, 1f);
        }

        // 흔들림 · 번쩍임 · 고리
        private void EvaluateEvents(float ms)
        {
            Vector2 offset = Vector2.zero;
            foreach (ShakeEvent shake in _shakes)
            {
                float progress = (ms - shake.At) / shake.Duration;
                if (progress < 0f || progress >= 1f) continue;

                float key = progress * (shake.Keys.Length - 1);
                int index = Mathf.FloorToInt(key);
                offset = Vector2.Lerp(shake.Keys[index], shake.Keys[index + 1], key - index);
            }
            stage.anchoredPosition = offset;

            float white = 0f;
            foreach (FlashEvent item in _flashes)
            {
                float progress = (ms - item.At) / item.Duration;
                if (progress >= 0f && progress < 1f)
                    white = Mathf.Max(white, item.Alpha * (1f - progress));
            }
            SetAlpha(flash, white);

            // 고리 칸은 일어난 순서대로 돌려 쓴다 — 같은 때에 겹치는 고리가 칸 수를 넘지 않는다
            foreach (UIRing ring in rings)
                SetAlpha(ring, 0f);
            for (int i = 0; i < _ringEvents.Count; i++)
            {
                RingEvent item = _ringEvents[i];
                float progress = (ms - item.At) / item.Duration;
                if (progress < 0f || progress >= 1f) continue;

                // 시안 .ci-ring: 0.1배 → 3배로 퍼지며 사라진다. 선 두께 6px 도 같이 커진다
                float spread = Ease(EaseOut, progress);
                float scale = 0.1f + 2.9f * spread;
                UIRing ring = rings[i % rings.Length];
                ring.color = item.Color;
                ring.rectTransform.anchoredPosition = item.Position;
                ring.Set(item.Radius * scale, 6f * _unit * scale);
                SetAlpha(ring, 1f - spread);
            }
        }

        private static float Progress(float ms, float start, float duration) => Mathf.Clamp01((ms - start) / duration);

        private static float Ease(Vector4 curve, float progress) => GaepanEase.CubicBezier(curve, progress);

        private static Vector2 Direction(float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        // (자리, 값) 쌍 사이를 곧게 이은 값. 범위 밖이면 처음 · 끝 값
        private static float Keys(float at, float[] pairs)
        {
            if (at <= pairs[0])
                return pairs[1];
            for (int i = 2; i < pairs.Length; i += 2)
            {
                if (at <= pairs[i])
                    return Mathf.Lerp(pairs[i - 1], pairs[i + 1], (at - pairs[i - 2]) / (pairs[i] - pairs[i - 2]));
            }
            return pairs[pairs.Length - 1];
        }

        // 같은 쌍을, 칸마다 빠르게 시작해 천천히 멈추게 잇는다 (시안 openEyes 의 1 − (1 − u)²)
        private static float KeysEased(float at, float[] pairs)
        {
            for (int i = 2; i < pairs.Length; i += 2)
            {
                if (at > pairs[i]) continue;

                float u = Mathf.Clamp01((at - pairs[i - 2]) / (pairs[i] - pairs[i - 2]));
                return Mathf.Lerp(pairs[i - 1], pairs[i + 1], 1f - (1f - u) * (1f - u));
            }
            return pairs[pairs.Length - 1];
        }

        // 꼭짓점을 다시 만들지 않고 진하기만 바꾼다
        private static void SetAlpha(Graphic graphic, float alpha)
        {
            graphic.canvasRenderer.SetAlpha(alpha);
        }
    }
}
