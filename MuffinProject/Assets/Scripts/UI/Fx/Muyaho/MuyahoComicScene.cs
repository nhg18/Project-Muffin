using System;
using System.Collections.Generic;
using Chapchu.UI.Fx.Comic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Muyaho
{
    /// <summary>
    /// 무야호(카드 118) 만화 컷인 장면 — 손패가 제일 많은 사람이 나머지에게 한 장씩 준다 (시안 CI2.SC["무야호"]).
    /// 알래스카 하늘 · 겹겹 설산 · 햇살 · 눈발 위로 밈 사진이 튀어 올라 콩콩 뛰고, 0.32 초부터 「무~ 야~ 호~」 한 글자씩 찍힌 뒤 0.9 초 「!」.
    /// 0.75 초부터 오른쪽 아래 손패 부자 얼굴 + 「-n」, 그 왼쪽 나머지 사람(최대 셋)에게 카드가 한 장씩 날아가 「+1」.
    /// 4 단계는 손패 이동(게임 화면 몫)뿐이라 장식이 없다 (시안 FXA 무야호 s4 = moveCards).
    /// 손패 부자 = 카드의 대상 <see cref="ComicContext.Target"/> (손패가 가장 많은 1명, 낼 때 정해 공개 — 11-card-list 118). 받는 사람 = 대상을 뺀 모두.
    /// 얼굴은 <see cref="CardFxContext.Character"/> 의 얼굴 칸을 동그랗게 잘라 쓴다 (사람마다 그림이 생기기 전까지 모두 같다).
    /// 연출 길이 2.56초 (3 단계만) — 카드 처리 시간(resolveSeconds)은 게임 화면에 연출을 붙일 때 맞춘다 (PM 결정 2026-11-15).
    /// </summary>
    public class MuyahoComicScene : ComicScene
    {
        // 시안 label("-" + poor.length) · label("+1")
        private const string LossFormat = "-{0}";
        private const string GainText = "+1";

        [Header("그림 (그리는 순서)")]
        [Tooltip("눈부신 햇살 (더하기 머티리얼, 장면이 그린다).")]
        [SerializeField] private ComicSketch glow;
        [Tooltip("겹겹 설산 (장면이 그린다).")]
        [SerializeField] private ComicSketch mountains;
        [Tooltip("눈발 (장면이 그린다).")]
        [SerializeField] private ComicSketch snowfall;
        [Tooltip("밈 사진 한 장 — 자식 Frame(그림자 · 흰 틀 · 먹 테) + Picture. 장면이 옮기고 흔든다.")]
        [SerializeField] private RectTransform photo;
        [Tooltip("사진 틀 (장면이 그린다).")]
        [SerializeField] private ComicSketch photoFrame;
        [Tooltip("「무」 「야」 「호」 (찍히기) — 다 찍힌 자리가 물결의 가운데.")]
        [SerializeField] private ComicStamp[] syllables = new ComicStamp[3];
        [Tooltip("글자마다 뒤에 붙는 물결 꼬리 (장면이 그린다, 글자와 같은 순서).")]
        [SerializeField] private ComicSketch[] tildes = new ComicSketch[3];
        [Tooltip("「!」 (찍히기).")]
        [SerializeField] private ComicStamp bang;
        [Tooltip("손패 부자 얼굴 — 다 커진 자리.")]
        [SerializeField] private Portrait giver;
        [Tooltip("손패 부자 「-n」 표.")]
        [SerializeField] private Tag loss;
        [Tooltip("받는 사람 (최대 셋, 왼쪽부터) — 얼굴 · 날아오는 카드 · 「+1」 표.")]
        [SerializeField] private Receiver[] receivers = new Receiver[3];

        [Header("햇살 (시안 lighter + glow(1050, 120, 420, 0.6, 255,255,230))")]
        [Tooltip("가운데 (설계 px, 시안 1050, 120).")]
        [SerializeField] private Vector2 glowCenter = new Vector2(1050f, 120f);
        [Tooltip("반지름 (설계 px, 시안 420).")]
        [SerializeField] private float glowRadius = 420f;
        [Tooltip("가운데 색 · 진하기 (시안 rgba(255, 255, 230, 0.6)).")]
        [SerializeField] private Color glowColor = new Color(1f, 1f, 230f / 255f, 0.6f);

        [Header("설산 (시안 ridge(y, amp, seed, col, snow) 셋)")]
        [Tooltip("산 하나씩 뒤에서 앞으로 (시안 380·170·5 #9fc9f2 눈 · 520·120·8 #6fa6de 눈 · 640·60·12 #eef7ff).")]
        [SerializeField] private Ridge[] ridges =
        {
            new Ridge { baseY = 380f, height = 170f, seed = 5, color = new Color32(0x9f, 0xc9, 0xf2, 0xff), snow = true },
            new Ridge { baseY = 520f, height = 120f, seed = 8, color = new Color32(0x6f, 0xa6, 0xde, 0xff), snow = true },
            new Ridge { baseY = 640f, height = 60f, seed = 12, color = new Color32(0xee, 0xf7, 0xff, 0xff), snow = false },
        };
        [Tooltip("산줄기가 화면 밖으로 더 뻗는 만큼 (설계 px, 시안 200).")]
        [SerializeField] private float ridgeMargin = 200f;
        [Tooltip("산 바닥 — 화면 아래에서 더 (설계 px, 시안 50).")]
        [SerializeField] private float ridgeBottom = 50f;
        [Tooltip("봉우리 사이 — 최소 · 더하는 만큼 (설계 px, 시안 90 + R·120).")]
        [SerializeField] private Vector2 ridgeStep = new Vector2(90f, 120f);
        [Tooltip("눈 없는 산의 먹 테 굵기 (설계 px, 시안 5).")]
        [SerializeField] private float ridgeLine = 5f;
        [Tooltip("눈 덮개 모양 — 봉우리 기준 (설계 px, 시안). 두 번째 점이 꼭대기, 거기서 부채꼴로 나눠 산 모양 안만 칠한다.")]
        [SerializeField] private Vector2[] snowCap =
        {
            new Vector2(-60f, 55f), new Vector2(0f, -2f), new Vector2(60f, 55f), new Vector2(20f, 42f), new Vector2(0f, 58f), new Vector2(-24f, 40f),
        };

        [Header("눈발 (시안 rng(9) 40 개: x 에 t·40·(R + 0.5), y 에 t·(60 + R·80) 흘러간다)")]
        [Tooltip("씨앗 (시안 9).")]
        [SerializeField] private int snowSeed = 9;
        [Tooltip("눈송이 수 (시안 40).")]
        [SerializeField] private int snowCount = 40;
        [Tooltip("옆으로 흐르는 빠르기 (설계 px/초, 시안 40 × (R + 0.5)).")]
        [SerializeField] private float snowDrift = 40f;
        [Tooltip("떨어지는 빠르기 — 최소 · 더하는 만큼 (설계 px/초, 시안 60 + R·80).")]
        [SerializeField] private Vector2 snowFall = new Vector2(60f, 80f);
        [Tooltip("반지름 — 최소 · 더하는 만큼 (설계 px, 시안 2 + R·3).")]
        [SerializeField] private Vector2 snowRadius = new Vector2(2f, 3f);
        [Tooltip("진하기 (시안 0.8).")]
        [SerializeField] private float snowAlpha = 0.8f;

        [Header("사진 (시안: 0.28 초에 y 820 → 410 back 1.8, 0.3 초부터 |sin(9t)|·22 콩콩)")]
        [Tooltip("자리 x (설계 px, 시안 440).")]
        [SerializeField] private float photoX = 440f;
        [Tooltip("들어오기 전 · 자리 y (설계 px, 시안 820 · 410).")]
        [SerializeField] private Vector2 photoY = new Vector2(820f, 410f);
        [Tooltip("들어오는 시간 (초, 시안 0.28).")]
        [SerializeField] private float photoEnter = 0.28f;
        [Tooltip("들어올 때 지나치는 정도 (시안 back 1.8).")]
        [SerializeField] private float photoOvershoot = 1.8f;
        [Tooltip("콩콩 뛰기 시작 (초, 시안 0.3).")]
        [SerializeField] private float hopFrom = 0.3f;
        [Tooltip("콩콩 빠르기 (라디안/초, 시안 9).")]
        [SerializeField] private float hopSpeed = 9f;
        [Tooltip("콩콩 높이 (설계 px, 시안 22).")]
        [SerializeField] private float hopHeight = 22f;
        [Tooltip("기울기 (라디안, 시안 −0.06).")]
        [SerializeField] private float photoTilt = -0.06f;
        [Tooltip("흔들림 (라디안, 시안 0.025).")]
        [SerializeField] private float photoSway = 0.025f;
        [Tooltip("그림자 칸 — 사진 가운데 기준 (설계 px, 시안 −206, −206, 420, 424) · 진하기 0.28.")]
        [SerializeField] private Rect photoShadow = new Rect(-206f, -206f, 420f, 424f);
        [Tooltip("그림자 색 (시안 rgba(0, 0, 0, 0.28)).")]
        [SerializeField] private Color photoShadowColor = new Color(0f, 0f, 0f, 0.28f);
        [Tooltip("흰 틀 한 변 (설계 px, 시안 428).")]
        [SerializeField] private float photoSize = 428f;
        [Tooltip("먹 테 굵기 (설계 px, 시안 6).")]
        [SerializeField] private float photoLine = 6f;

        [Header("무~ 야~ 호~ (시안: 0.32 + 0.2i 초부터 0.18 초, 물결 sin(7t + 1.2i)·12)")]
        [Tooltip("첫 글자 (초, 시안 0.32).")]
        [SerializeField] private float syllableAt = 0.32f;
        [Tooltip("글자 사이 (초, 시안 0.2).")]
        [SerializeField] private float syllableGap = 0.2f;
        [Tooltip("찍히는 시간 (초, 시안 0.18).")]
        [SerializeField] private float syllableTime = 0.18f;
        [Tooltip("물결 빠르기 (라디안/초, 시안 7).")]
        [SerializeField] private float waveSpeed = 7f;
        [Tooltip("글자마다 물결 어긋남 (라디안, 시안 1.2).")]
        [SerializeField] private float wavePhase = 1.2f;
        [Tooltip("물결 높이 (설계 px, 시안 12).")]
        [SerializeField] private float waveHeight = 12f;

        [Header("물결 꼬리 (시안: 글자에서 (70, 40) 부터 80 길이, sin(1.2 바퀴 + 8t)·9, 먹 11 · 흰 5)")]
        [Tooltip("글자 가운데에서 꼬리 시작 (설계 px, 시안 70, 40).")]
        [SerializeField] private Vector2 tildeOffset = new Vector2(70f, 40f);
        [Tooltip("꼬리 길이 (설계 px, 시안 80).")]
        [SerializeField] private float tildeLength = 80f;
        [Tooltip("꼬리를 나누는 수 (시안 20).")]
        [SerializeField] private int tildeSegments = 20;
        [Tooltip("물결 수 (바퀴, 시안 1.2).")]
        [SerializeField] private float tildeCycles = 1.2f;
        [Tooltip("물결이 흐르는 빠르기 (라디안/초, 시안 8).")]
        [SerializeField] private float tildeSpeed = 8f;
        [Tooltip("물결 높이 (설계 px, 시안 9).")]
        [SerializeField] private float tildeWave = 9f;
        [Tooltip("먹 · 흰 선 굵기 (설계 px, 시안 11 · 5).")]
        [SerializeField] private Vector2 tildeWidth = new Vector2(11f, 5f);
        [Tooltip("나타나는 빠르기 (시안 cl(k·3)).")]
        [SerializeField] private float tildeFade = 3f;

        [Header("「!」")]
        [Tooltip("찍히기 시작 (초, 시안 0.9) — 마지막 「호」 꼬리도 이때부터.")]
        [SerializeField] private float bangAt = 0.9f;
        [Tooltip("찍히는 시간 (초, 시안 0.16).")]
        [SerializeField] private float bangTime = 0.16f;

        [Header("한 장씩 털린다 (시안 k0 = t − 0.95)")]
        [Tooltip("기준 시각 (초, 시안 0.95).")]
        [SerializeField] private float shareAt = 0.95f;
        [Tooltip("손패 부자 얼굴이 커지기 시작 — 기준에서 (초, 시안 −0.2). 이때부터 이 묶음을 그린다.")]
        [SerializeField] private float giverLead = -0.2f;
        [Tooltip("얼굴이 커지는 시간 (초, 시안 0.25 · 받는 사람 0.22).")]
        [SerializeField] private Vector2 portraitPop = new Vector2(0.25f, 0.22f);
        [Tooltip("얼굴이 커질 때 지나치는 정도 (시안 back 1.8).")]
        [SerializeField] private float portraitOvershoot = 1.8f;
        [Tooltip("「-n」 이 나오는 때 — 기준에서 (초, 시안 0.12) · 커지는 시간 (시안 0.18).")]
        [SerializeField] private Vector2 lossPop = new Vector2(0.12f, 0.18f);
        [Tooltip("「-n」 지나치는 정도 (시안 back 2).")]
        [SerializeField] private float lossOvershoot = 2f;
        [Tooltip("받는 사람 얼굴이 커지기 시작 — 기준에서 (초, 시안 −0.1) · 사람마다 늦게 (시안 0.04).")]
        [SerializeField] private Vector2 receiverLead = new Vector2(-0.1f, 0.04f);
        [Tooltip("카드가 떠나는 때 — 기준에서 (초, 시안 0.05) · 사람마다 늦게 (시안 0.09).")]
        [SerializeField] private Vector2 cardAt = new Vector2(0.05f, 0.09f);
        [Tooltip("카드가 날아가는 시간 (초, 시안 0.32).")]
        [SerializeField] private float cardFlight = 0.32f;
        [Tooltip("카드가 닿는 곳 — 받는 사람 얼굴에서 (설계 px, 시안 0, −60).")]
        [SerializeField] private Vector2 cardLanding = new Vector2(0f, -60f);
        [Tooltip("날아갈 때 휘는 높이 (설계 px, 시안 150).")]
        [SerializeField] private float cardArc = 150f;
        [Tooltip("날아가며 도는 바퀴 (시안 1.5).")]
        [SerializeField] private float cardTurns = 1.5f;
        [Tooltip("날아가는 · 닿은 카드 너비 (설계 px, 시안 58 · 40).")]
        [SerializeField] private Vector2 cardWidth = new Vector2(58f, 40f);
        [Tooltip("닿은 카드 자리 — 받는 사람 얼굴에서 (설계 px, 시안 26, −50).")]
        [SerializeField] private Vector2 cardRest = new Vector2(26f, -50f);
        [Tooltip("닿은 카드 기울기 (라디안, 시안 0.2).")]
        [SerializeField] private float cardRestTilt = 0.2f;
        [Tooltip("「+1」 이 커지는 시간 (초, 시안 0.16).")]
        [SerializeField] private float gainPop = 0.16f;
        [Tooltip("「+1」 지나치는 정도 (시안 back 2.2).")]
        [SerializeField] private float gainOvershoot = 2.2f;

        [Header("얼굴 · 표 (시안 avatar · label)")]
        [Tooltip("캐릭터 그림에서 얼굴 칸 (0 ~ 1, 그림 왼쪽 아래 기준) — chara 의 chara_face 영역.")]
        [SerializeField] private Rect faceArea = new Rect(0.0187f, 0.3479f, 0.825f, 0.6371f);
        [Tooltip("캐릭터 그림이 없을 때 얼굴 색 (시안 #bbb).")]
        [SerializeField] private Color noFaceColor = new Color32(0xbb, 0xbb, 0xbb, 0xff);
        [Tooltip("표 상자 너비 = 글자 폭 + 이만큼 (설계 px, 시안 34).")]
        [SerializeField] private float tagPadding = 34f;
        [Tooltip("표 상자 높이 = 글자 크기 × 이만큼 (시안 1.35).")]
        [SerializeField] private float tagHeight = 1.35f;
        [Tooltip("표 상자 테 두께 (설계 px, 시안 5).")]
        [SerializeField] private float tagLine = 5f;

        private readonly List<Vector2> _ridgePoints = new List<Vector2>();
        private readonly List<Vector2> _clipA = new List<Vector2>();
        private readonly List<Vector2> _clipB = new List<Vector2>();
        private readonly Vector2[] _tildeAt = new Vector2[3];
        private readonly float[] _tildeAlpha = new float[3];
        private float _time;
        private int _receiverCount;
        private Vector2 _giverAt;

        /// <summary>얼굴 하나 — 흰 바탕 + 먹 테 동그라미 안에 잘라 넣은 얼굴 (시안 avatar). 크기 · 자리는 root.</summary>
        [Serializable]
        private struct Portrait
        {
            public RectTransform root;
            public RawImage face;
        }

        /// <summary>표 하나 — 그림자 · 상자 · 글자 (시안 label). 자리 · 기울기 · 크기는 root.</summary>
        [Serializable]
        private struct Tag
        {
            public RectTransform root;
            public RectTransform shadow;
            public RectTransform box;
            public TMP_Text text;
        }

        /// <summary>카드를 받는 사람 한 명.</summary>
        [Serializable]
        private struct Receiver
        {
            public Portrait portrait;
            public ComicCard card;
            public Tag gain;
        }

        /// <summary>산줄기 하나 (시안 ridge(y, amp, seed, col, snow)).</summary>
        [Serializable]
        private struct Ridge
        {
            [Tooltip("봉우리가 내려올 수 있는 가장 아래 y (설계 px, 시안 y).")]
            public float baseY;
            [Tooltip("봉우리 높이 폭 (설계 px, 시안 amp).")]
            public float height;
            [Tooltip("씨앗 (시안 seed).")]
            public int seed;
            [Tooltip("산 색 (시안 col).")]
            public Color color;
            [Tooltip("봉우리마다 눈 덮개 (시안 snow). 눈 덮인 산은 먹 테를 긋지 않는다 — 시안이 마지막 눈 덮개(화면 밖)만 긋는다.")]
            public bool snow;
        }

        private void Awake()
        {
            if (glow == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(glow)} 가 비었다");
            if (mountains == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(mountains)} 가 비었다");
            if (snowfall == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(snowfall)} 가 비었다");
            if (photo == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(photo)} 가 비었다");
            if (photoFrame == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(photoFrame)} 가 비었다");
            if (bang == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(bang)} 가 비었다");
            if (syllables.Length != 3 || tildes.Length != 3)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(syllables)} · {nameof(tildes)} 는 셋씩이어야 한다");
            for (int i = 0; i < syllables.Length; i++)
            {
                if (syllables[i] == null)
                    Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(syllables)}[{i}] 가 비었다");
            }
            for (int i = 0; i < tildes.Length; i++)
            {
                if (tildes[i] == null)
                    Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(tildes)}[{i}] 가 비었다");
            }
            CheckPortrait(giver, nameof(giver));
            CheckTag(loss, nameof(loss));
            for (int i = 0; i < receivers.Length; i++)
            {
                CheckPortrait(receivers[i].portrait, $"{nameof(receivers)}[{i}]");
                CheckTag(receivers[i].gain, $"{nameof(receivers)}[{i}]");
                if (receivers[i].card == null)
                    Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {nameof(receivers)}[{i}] 카드가 비었다");
            }
        }

        public override void Begin(ComicContext context)
        {
            glow.Painter = PaintGlow;
            glow.Redraw();
            mountains.Painter = PaintMountains;
            mountains.Redraw();
            snowfall.Painter = PaintSnow;
            photoFrame.Painter = PaintPhotoFrame;
            photoFrame.Redraw();
            for (int i = 0; i < tildes.Length; i++)
            {
                int index = i;
                tildes[i].Painter = pen => PaintTilde(pen, index);
            }

            // 손패 부자 = 카드의 대상 (낼 때 정해 공개). 나머지는 턴 순서대로 셋까지
            CardFxSeat rich = context.Target;
            _receiverCount = 0;
            foreach (CardFxSeat seat in context.Fx.Seats)
            {
                if (seat != null && seat != rich && _receiverCount < receivers.Length)
                    _receiverCount++;
            }

            Sprite character = context.Fx.Character;
            ComicKit.ShowFace(giver.face, character, faceArea, noFaceColor);
            _giverAt = ComicKit.PositionOf(giver.root);
            SetTag(loss, string.Format(LossFormat, _receiverCount));
            for (int i = 0; i < receivers.Length; i++)
            {
                bool used = i < _receiverCount;
                receivers[i].portrait.root.gameObject.SetActive(used);
                receivers[i].card.gameObject.SetActive(used);
                receivers[i].gain.root.gameObject.SetActive(used);
                if (!used)
                    continue;
                ComicKit.ShowFace(receivers[i].portrait.face, character, faceArea, noFaceColor);
                SetTag(receivers[i].gain, GainText);
            }
        }

        public override void Draw(float t, ComicContext context)
        {
            _time = t;
            snowfall.Redraw();

            // 사진 한 장(밈 그림): 신나서 콩콩
            float hop = t > hopFrom ? Mathf.Abs(Mathf.Sin(t * hopSpeed)) * hopHeight : 0f;
            float enter = ComicKit.Back(t / photoEnter, photoOvershoot);
            ComicKit.Place(photo, photoX, ComicKit.Lerp(photoY.x, photoY.y, enter) - hop);
            ComicKit.Turn(photo, photoTilt + Mathf.Sin(t * hopSpeed) * photoSway);

            // 무~ 야~ 호~! 한 글자씩
            for (int i = 0; i < syllables.Length; i++)
            {
                float k = (t - syllableAt - i * syllableGap) / syllableTime;
                float wave = Mathf.Sin(t * waveSpeed + i * wavePhase) * waveHeight;
                syllables[i].Apply(k, new Vector2(0f, wave));
                _tildeAt[i] = syllables[i].RestPosition + new Vector2(0f, wave);
                bool tail = i < syllables.Length - 1 || t > bangAt;
                _tildeAlpha[i] = k > 0f && tail ? ComicKit.Cl(k * tildeFade) : 0f;
                tildes[i].Redraw();
            }
            bang.Apply((t - bangAt) / bangTime);

            // 손패 부자가 한 장씩 털린다
            float k0 = t - shareAt;
            bool sharing = k0 > giverLead;
            SetScale(giver.root, sharing ? ComicKit.Back((k0 - giverLead) / portraitPop.x, portraitOvershoot) : 0f);
            SetScale(loss.root, k0 > lossPop.x ? ComicKit.Back((k0 - lossPop.x) / lossPop.y, lossOvershoot) : 0f);
            for (int i = 0; i < _receiverCount; i++)
            {
                Receiver receiver = receivers[i];
                float pop = (k0 - receiverLead.x - i * receiverLead.y) / portraitPop.y;
                SetScale(receiver.portrait.root, sharing ? ComicKit.Back(pop, portraitOvershoot) : 0f);
                float q = (k0 - cardAt.x - i * cardAt.y) / cardFlight;
                PlaceCard(receiver.card, ComicKit.PositionOf(receiver.portrait.root), sharing ? q : 0f);
                SetScale(receiver.gain.root, sharing && q >= 1f ? ComicKit.Back((q - 1f) * cardFlight / gainPop, gainOvershoot) : 0f);
            }
        }

        // q = 날아간 정도 (0 이하 = 아직, 1 이상 = 닿음)
        private void PlaceCard(ComicCard card, Vector2 to, float q)
        {
            bool shown = q > 0f;
            if (card.gameObject.activeSelf != shown)
                card.gameObject.SetActive(shown);
            if (!shown)
                return;
            var rect = (RectTransform)card.transform;
            if (q < 1f)
            {
                float e = ComicKit.Eo(q);
                float x = ComicKit.Lerp(_giverAt.x, to.x + cardLanding.x, e);
                float y = ComicKit.Lerp(_giverAt.y, to.y + cardLanding.y, e) - Mathf.Sin(q * Mathf.PI) * cardArc;
                card.Width = cardWidth.x;
                ComicKit.Place(rect, x, y);
                ComicKit.Turn(rect, q * ComicKit.Tau * cardTurns);
                return;
            }
            card.Width = cardWidth.y;
            ComicKit.Place(rect, to.x + cardRest.x, to.y + cardRest.y);
            ComicKit.Turn(rect, cardRestTilt);
        }

        private static void SetScale(RectTransform target, float scale)
        {
            scale = Mathf.Max(0f, scale);
            target.localScale = new Vector3(scale, scale, 1f);
        }

        // 표 크기 맞추기는 틀로 모을 후보 — 이 작업에서는 틀(ComicKit)을 고치지 않는다
        private void SetTag(Tag tag, string text)
        {
            tag.text.text = text;
            float width = tag.text.GetPreferredValues(text).x + tagPadding;
            float height = tag.text.fontSize * tagHeight;
            tag.shadow.sizeDelta = new Vector2(width, height);
            // 시안 strokeRect 는 테 가운데가 상자 가장자리 — 테 반만큼 밖으로
            tag.box.sizeDelta = new Vector2(width + tagLine, height + tagLine);
        }

        private void PaintGlow(ComicPen pen)
        {
            pen.FillRadial(glowCenter.x, glowCenter.y, glowRadius, glowColor, new Color(glowColor.r, glowColor.g, glowColor.b, 0f));
        }

        private void PaintMountains(ComicPen pen)
        {
            float bottom = ComicKit.Height + ridgeBottom;
            foreach (Ridge ridge in ridges)
            {
                var random = new ComicRandom(ridge.seed);
                _ridgePoints.Clear();
                for (float x = -ridgeMargin; x < ComicKit.Width + ridgeMargin;)
                {
                    _ridgePoints.Add(new Vector2(x, ridge.baseY - random.Next() * ridge.height));
                    // 인스펙터에서 0 이하로 바꿔도 끝나게 — 적어도 1 설계 px 씩 나아간다
                    x += Mathf.Max(1f, ridgeStep.x + random.Next() * ridgeStep.y);
                }
                int peaks = _ridgePoints.Count;
                // 오른쪽 아래 모서리까지가 산 윗줄 — 눈 덮개를 자를 때도 쓴다
                _ridgePoints.Add(new Vector2(ComicKit.Width + ridgeMargin, bottom));

                pen.BeginPath();
                pen.MoveTo(-ridgeMargin, bottom);
                foreach (Vector2 p in _ridgePoints)
                    pen.LineTo(p.x, p.y);
                pen.ClosePath();
                pen.Fill(ridge.color);
                if (!ridge.snow)
                {
                    pen.Stroke(ComicKit.Ink, ridgeLine);
                    continue;
                }
                pen.BeginPath();
                for (int i = 0; i < peaks; i++)
                    AddSnowCap(pen, _ridgePoints[i]);
                pen.Fill(Color.white);
            }
        }

        // 봉우리 하나의 눈 덮개를 산 모양(윗줄 아래) 안만 하위 경로로 더한다 (시안 clip(산) + 덮개 fill).
        // 덮개를 꼭대기에서 부채꼴 세모로 나누고, 산 윗줄 조각마다 그 x 칸 · 그 선 아래로 자른다 — 볼록한 조각끼리라 자르기가 단순하다.
        private void AddSnowCap(ComicPen pen, Vector2 peak)
        {
            int n = snowCap.Length;
            Vector2 apex = peak + snowCap[1];
            for (int j = 2; j < n; j++)
            {
                Vector2 b = peak + snowCap[j], c = peak + snowCap[(j + 1) % n];
                float xMin = Mathf.Min(apex.x, Mathf.Min(b.x, c.x)), xMax = Mathf.Max(apex.x, Mathf.Max(b.x, c.x));
                for (int s = 0; s + 1 < _ridgePoints.Count; s++)
                {
                    Vector2 left = _ridgePoints[s], right = _ridgePoints[s + 1];
                    if (right.x <= xMin || left.x >= xMax)
                        continue;
                    float slope = (right.y - left.y) / (right.x - left.x);
                    _clipA.Clear();
                    _clipA.Add(apex);
                    _clipA.Add(b);
                    _clipA.Add(c);
                    Clip(_clipA, _clipB, 1f, 0f, left.x);
                    Clip(_clipB, _clipA, -1f, 0f, -right.x);
                    Clip(_clipA, _clipB, -slope, 1f, left.y - slope * left.x);
                    if (_clipB.Count < 3)
                        continue;
                    pen.MoveTo(_clipB[0].x, _clipB[0].y);
                    for (int v = 1; v < _clipB.Count; v++)
                        pen.LineTo(_clipB[v].x, _clipB[v].y);
                    pen.ClosePath();
                }
            }
        }

        // 볼록 다각형을 a·x + b·y ≥ c 쪽만 남긴다 (Sutherland–Hodgman)
        private static void Clip(List<Vector2> input, List<Vector2> output, float a, float b, float c)
        {
            output.Clear();
            for (int i = 0; i < input.Count; i++)
            {
                Vector2 p = input[i], q = input[(i + 1) % input.Count];
                float dp = a * p.x + b * p.y - c, dq = a * q.x + b * q.y - c;
                if (dp >= 0f)
                    output.Add(p);
                if (dp >= 0f != dq >= 0f)
                    output.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
            }
        }

        private void PaintSnow(ComicPen pen)
        {
            var random = new ComicRandom(snowSeed);
            pen.Alpha = snowAlpha;
            for (int i = 0; i < snowCount; i++)
            {
                float x0 = random.Next() * ComicKit.Width;
                float drift = snowDrift * (random.Next() + 0.5f);
                float y0 = random.Next() * ComicKit.Height;
                float fall = snowFall.x + random.Next() * snowFall.y;
                float r = snowRadius.x + random.Next() * snowRadius.y;
                pen.BeginPath();
                pen.Arc((x0 + _time * drift) % ComicKit.Width, (y0 + _time * fall) % ComicKit.Height, r, 0f, ComicKit.Tau);
                pen.Fill(Color.white);
            }
        }

        private void PaintPhotoFrame(ComicPen pen)
        {
            pen.BeginPath();
            pen.Rect(photoShadow.x, photoShadow.y, photoShadow.width, photoShadow.height);
            pen.Fill(photoShadowColor);
            pen.BeginPath();
            pen.Rect(-photoSize / 2f, -photoSize / 2f, photoSize, photoSize);
            pen.Fill(Color.white);
            pen.Stroke(ComicKit.Ink, photoLine);
        }

        private void PaintTilde(ComicPen pen, int index)
        {
            float alpha = _tildeAlpha[index];
            if (alpha <= 0f)
                return;
            Vector2 at = _tildeAt[index] + tildeOffset;
            pen.Alpha = alpha;
            pen.BeginPath();
            for (int s = 0; s <= tildeSegments; s++)
            {
                float u = (float)s / tildeSegments;
                float y = at.y + Mathf.Sin(u * ComicKit.Tau * tildeCycles + _time * tildeSpeed) * tildeWave;
                if (s == 0)
                    pen.MoveTo(at.x, y);
                else
                    pen.LineTo(at.x + u * tildeLength, y);
            }
            pen.Stroke(ComicKit.Ink, tildeWidth.x);
            pen.Stroke(Color.white, tildeWidth.y);
        }

        private void CheckPortrait(Portrait portrait, string field)
        {
            if (portrait.root == null || portrait.face == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {field} 얼굴에 빈 칸이 있다 (root · face)");
        }

        private void CheckTag(Tag tag, string field)
        {
            if (tag.root == null || tag.shadow == null || tag.box == null || tag.text == null)
                Debug.LogError($"[{nameof(MuyahoComicScene)}] {name} 의 {field} 표에 빈 칸이 있다 (root · shadow · box · text)");
        }
    }
}
