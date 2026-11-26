using System;
using System.Collections;
using System.Collections.Generic;
using Chapchu.UI.Fx.Comic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Wait
{
    /// <summary>
    /// 기다려!(카드 1023) 만화 컷인 장면 — 다들 「찹츄!」 외치려는 순간 앞발 하나로 스읍! (시안 CI2.SC["기다려!"]).
    /// 분홍 꽃가루 속에서 나머지 사람들(얼굴 원)이 들썩이며 차례로 「찹츄!」 말풍선을 띄운다 → 0.36 초 쓴 사람의 큰 앞발이 아래에서 솟는다 →
    /// 0.5 초 판이 빨갛게 바뀌고(햇살 · 집중선) 흰 고리가 퍼진다 → 말풍선이 차례로 「…」 으로 줄고 얼굴마다 「승리 불가」 표가 붙는다.
    /// 4 단계 = 쓴 사람에게 「기다려!」 말풍선 + 0.5 초 (시안 FXA 기다려! s4, 「승리 불가」 상태 글자는 게임 화면 몫이라 뺐다).
    /// 연출 길이 2.86초 (3 단계 2.36초 + 4 단계 0.5초) — 카드 처리 시간(resolveSeconds)에 3 단계 실측 길이를 더해 두었다 (10-ui.md 5-1). 길이를 바꾸면 그 값도 같이 바꾼다.
    /// 얼굴 그림은 모두 <see cref="CardFxContext.Character"/> 의 얼굴 칸 — 자리마다 얼굴 그림이 생기면 그것으로 바꾼다.
    /// 기준 문서: docs/systems/11-card-list.md 1023 기다려! · docs/systems/10-ui.md 5-1 (3 · 4 단계 연출).
    /// </summary>
    public class WaitComicScene : ComicScene
    {
        // 시안 FXA 기다려! s4: vbubble(u, "기다려!")
        private const string BubbleText = "기다려!";
        // 시안 bubble: 꼬리 뿌리의 테를 덮는 안쪽 타원 = 테 안쪽 반 + 0.5 px 더 들인다
        private const float BubbleCoverInset = 0.5f;
        // 시안 paw 모양 자료 (손바닥 가운데 기준 설계 px — 조절할 값이 아니라 그림 모양이라 Inspector 에 열지 않는다)
        // 떨림: rng(77) 로 윤곽 점마다 J(·, 3), 팔 x 에 J(0, 4) 더
        private const int PawSeed = 77;
        private const float PawJitter = 3f;
        private const float PawArmJitter = 4f;
        // 팔: x = ±(104 + 18·sin·0.6), y 900 ↔ 120 (아홉 점)
        private const float PawArmX = 104f;
        private const float PawArmBulge = 18f * 0.6f;
        private const float PawArmTop = 120f;
        private const float PawArmBottom = 900f;
        // 손바닥 호: (cos a · 158, −sin a · 130 + 30), 왼쪽 a = π·1.02 → −0.5, 오른쪽 a = 0.48 → −0.5 (열한 점)
        private static readonly Vector2 PawPalm = new Vector2(158f, 130f);
        private const float PawPalmY = 30f;
        private const float PawPalmLeft = Mathf.PI * 1.02f;
        private const float PawPalmRight = 0.48f;
        private const float PawPalmSweep = 0.5f;
        // 발가락 넷 (x, y, 반지름) — 윤곽은 a0 → a1 (왼쪽 둘 π·1.05 → −π·0.15, 오른쪽 둘 π·1.15 → −π·0.05), 높이 1.08 배
        private static readonly Vector3[] PawToes =
        {
            new Vector3(-118f, -58f, 40f), new Vector3(-44f, -112f, 44f), new Vector3(44f, -112f, 44f), new Vector3(118f, -58f, 40f),
        };
        private const float PawToeTall = 1.08f;
        // 발가락 젤리 기울기 (크기는 반지름의 0.62 × 0.72, 6 아래)
        private static readonly float[] PawBeanTilt = { 0.4f, 0.12f, -0.12f, -0.4f };
        // 먹 그림자: (4, 6) 어긋나 1.035 × 1.02 배
        private static readonly Vector2 PawInkOffset = new Vector2(4f, 6f);
        private static readonly Vector2 PawInkScale = new Vector2(1.035f, 1.02f);
        // 큰 발바닥 젤리: 시작 + 3차 곡선 넷 (조절점 둘 + 끝)
        private static readonly Vector2[] PawPad =
        {
            new Vector2(0f, -4f),
            new Vector2(46f, -30f), new Vector2(104f, 10f), new Vector2(92f, 58f),
            new Vector2(84f, 96f), new Vector2(40f, 100f), new Vector2(0f, 88f),
            new Vector2(-40f, 100f), new Vector2(-84f, 96f), new Vector2(-92f, 58f),
            new Vector2(-104f, 10f), new Vector2(-46f, -30f), new Vector2(0f, -4f),
        };
        // 발가락 사이 갈라진 선 셋 (시작 x, y, 끝 x, y)
        private static readonly Vector4[] PawSplits =
        {
            new Vector4(-82f, -70f, -72f, -30f), new Vector4(0f, -92f, 0f, -48f), new Vector4(82f, -70f, 72f, -30f),
        };
        // 타원 · 젤리를 다각형으로 볼 때의 점 수 (그 안으로 자를 때)
        private const int ClipSides = 48;
        private const int ClipCurveSteps = 12;

        /// <summary>나머지 사람 한 명의 그림 — 얼굴 원 · 말풍선 · 「승리 불가」 표.</summary>
        [Serializable]
        public class Person
        {
            [Tooltip("얼굴 원 — 가운데가 피벗 (시안 avatar).")]
            public RectTransform face;
            [Tooltip("얼굴 그림 (얼굴 원 마스크 안).")]
            public RawImage picture;
            [Tooltip("얼굴을 어둡게 덮는 판 (얼굴 원 마스크 안, 색 = 시안 rgba(20, 10, 40)).")]
            public Graphic shade;
            [Tooltip("말풍선 — 가운데가 피벗, 배율로 튀어나온다.")]
            public RectTransform bubble;
            [Tooltip("말풍선 모양 (장면이 그린다).")]
            public ComicSketch bubbleShape;
            [Tooltip("「찹츄!」 글자.")]
            public TMP_Text shout;
            [Tooltip("「…」 글자 (막힌 뒤).")]
            public TMP_Text hush;
            [Tooltip("「승리 불가」 표 — 가운데가 피벗, 배율로 튀어나온다.")]
            public RectTransform tag;
            [Tooltip("표 상자 (장면이 그린다).")]
            public ComicSketch tagBox;
            [Tooltip("표 글자.")]
            public TMP_Text tagText;
        }

        [Header("그림 (그리는 순서)")]
        [Tooltip("막기 전 그림 묶음 — 분홍 바탕 · 집중선 · 꽃가루.")]
        [SerializeField] private GameObject calm;
        [Tooltip("꽃가루 (장면이 그린다).")]
        [SerializeField] private ComicSketch confetti;
        [Tooltip("막은 뒤 그림 묶음 — 빨간 바탕 · 햇살 · 집중선.")]
        [SerializeField] private GameObject stopped;
        [Tooltip("햇살 (장면이 그린다) — 가운데가 피벗, 돌린다.")]
        [SerializeField] private ComicSketch sunburst;
        [Tooltip("망점 — 색이 막기 전 · 뒤로 바뀐다.")]
        [SerializeField] private ComicHalftone tone;
        [Tooltip("나머지 사람들 (위부터, 시안 최대 3 명).")]
        [SerializeField] private Person[] people = new Person[3];
        [Tooltip("쓴 사람의 앞발 (장면이 그린다) — 손바닥 가운데가 피벗.")]
        [SerializeField] private ComicSketch paw;
        [Tooltip("앞발이 막을 때 퍼지는 흰 고리.")]
        [SerializeField] private UIRing pawRing;

        [Header("때 (초)")]
        [Tooltip("앞발이 막는 때 — 판이 바뀐다 (초, 시안 0.5).")]
        [SerializeField] private float hitAt = 0.5f;

        [Header("막기 전 (시안: 꽃가루 rng(4) 40 개, x + 30t, y + (160 + R·120)t, 14×8, 돌기 3 + R·4)")]
        [Tooltip("씨앗 (시안 rng(4)).")]
        [SerializeField] private int confettiSeed = 4;
        [Tooltip("꽃가루 수 (시안 40).")]
        [SerializeField] private int confettiCount = 40;
        [Tooltip("옆으로 흐르는 빠르기 (설계 px/초, 시안 30).")]
        [SerializeField] private float confettiDrift = 30f;
        [Tooltip("떨어지는 빠르기 — 최소 · 더하는 폭 (설계 px/초, 시안 160 · 120).")]
        [SerializeField] private Vector2 confettiFall = new Vector2(160f, 120f);
        [Tooltip("처음 높이 범위 — 판 높이 비율 (시안 1.2).")]
        [SerializeField] private float confettiSpread = 1.2f;
        [Tooltip("위아래로 되감는 여유 (설계 px, 시안 H + 40 으로 나누고 −20).")]
        [SerializeField] private float confettiWrap = 40f;
        [Tooltip("도는 빠르기 — 최소 · 더하는 폭 (라디안/초, 시안 3 · 4).")]
        [SerializeField] private Vector2 confettiSpin = new Vector2(3f, 4f);
        [Tooltip("꽃가루 크기 (설계 px, 시안 14×8).")]
        [SerializeField] private Vector2 confettiSize = new Vector2(14f, 8f);
        [Tooltip("꽃가루 색 — 차례로 (시안 #ff4f7a · #ffd23a · #5ac8fa · #7bd88f).")]
        [SerializeField] private Color[] confettiColors =
        {
            new Color32(0xff, 0x4f, 0x7a, 0xff),
            new Color32(0xff, 0xd2, 0x3a, 0xff),
            new Color32(0x5a, 0xc8, 0xfa, 0xff),
            new Color32(0x7b, 0xd8, 0x8f, 0xff),
        };

        [Header("막은 뒤 (시안: 햇살 24 갈래 rgba(255,255,255,.1), (t − 0.5)·0.15 로 돈다)")]
        [Tooltip("햇살 갈래 수 — 칠한 것 · 빈 것을 번갈아 (시안 24).")]
        [SerializeField] private int sunburstCount = 24;
        [Tooltip("햇살 길이 (설계 px, 시안 2400).")]
        [SerializeField] private float sunburstReach = 2400f;
        [Tooltip("햇살 도는 빠르기 (라디안/초, 시안 0.15).")]
        [SerializeField] private float sunburstSpin = 0.15f;
        [Tooltip("망점 — 막기 전 색 (알파 = 진하기, 시안 #ff4f7a · 0.16).")]
        [SerializeField] private Color calmTone = new Color(1f, 0x4f / 255f, 0x7a / 255f, 0.16f);
        [Tooltip("망점 — 막은 뒤 색 (알파 = 진하기, 시안 #5a0010 · 0.32).")]
        [SerializeField] private Color stoppedTone = new Color(0x5a / 255f, 0f, 0x10 / 255f, 0.32f);

        [Header("나머지 사람들 (시안: x = i%2 ? 960 : 1090, y 160 + i·175)")]
        [Tooltip("얼굴 x — 짝수 번째 · 홀수 번째 (설계 px, 시안 1090 · 960).")]
        [SerializeField] private Vector2 peopleX = new Vector2(1090f, 960f);
        [Tooltip("첫 사람 y · 한 사람마다 아래로 (설계 px, 시안 160 · 175).")]
        [SerializeField] private Vector2 peopleY = new Vector2(160f, 175f);
        [Tooltip("들썩이는 높이 (설계 px, 시안 |sin(t·14 + i·1.3)|·12).")]
        [SerializeField] private float bounce = 12f;
        [Tooltip("들썩이는 빠르기 (시안 14).")]
        [SerializeField] private float bounceSpeed = 14f;
        [Tooltip("사람마다 들썩임 어긋남 (라디안, 시안 1.3).")]
        [SerializeField] private float bouncePhase = 1.3f;
        [Tooltip("얼굴 칸 — 캐릭터 그림 안 비율 (왼쪽 아래 0, 그림 chara 의 chara_face 칸). 자리별 얼굴 그림이 생기기 전까지.")]
        [SerializeField] private Rect faceArea = new Rect(0.0187f, 0.3479f, 0.825f, 0.6371f);
        [Tooltip("얼굴 그림이 없을 때 색 (시안 #bbb).")]
        [SerializeField] private Color missingFace = new Color32(0xbb, 0xbb, 0xbb, 0xff);
        [Tooltip("막은 뒤 얼굴을 덮는 진하기 (시안 dark 0.12).")]
        [SerializeField] private float shadeAlpha = 0.12f;

        [Header("말풍선 (시안 bubble: 얼굴 + (−195, −44), 196×88 → 118×88, 꼬리 끝 얼굴 + (−48, −12))")]
        [Tooltip("말풍선이 나오기 시작하는 때 (초, 시안 0.08).")]
        [SerializeField] private float bubbleAt = 0.08f;
        [Tooltip("사람마다 늦게 (초, 시안 0.08).")]
        [SerializeField] private float bubbleGap = 0.08f;
        [Tooltip("튀어나오는 시간 (초, 시안 0.16).")]
        [SerializeField] private float bubbleTime = 0.16f;
        [Tooltip("튀어나올 때 지나치는 정도 (시안 back 2).")]
        [SerializeField] private float bubbleOvershoot = 2f;
        [Tooltip("말풍선 가운데 — 얼굴 가운데에서 (설계 px, 시안 −195, −44).")]
        [SerializeField] private Vector2 bubbleOffset = new Vector2(-195f, -44f);
        [Tooltip("꼬리 끝 — 얼굴 가운데에서 (설계 px, 시안 −48, −12).")]
        [SerializeField] private Vector2 tailOffset = new Vector2(-48f, -12f);
        [Tooltip("말풍선 크기 — 외칠 때 (설계 px, 시안 196×88).")]
        [SerializeField] private Vector2 shoutSize = new Vector2(196f, 88f);
        [Tooltip("말풍선 크기 — 막힌 뒤 (설계 px, 시안 118×88).")]
        [SerializeField] private Vector2 hushSize = new Vector2(118f, 88f);
        [Tooltip("말풍선 바탕 — 외칠 때 (시안 #fff).")]
        [SerializeField] private Color shoutFill = Color.white;
        [Tooltip("말풍선 바탕 — 막힌 뒤 (시안 #ececec).")]
        [SerializeField] private Color hushFill = new Color32(0xec, 0xec, 0xec, 0xff);
        [Tooltip("막힌 뒤로 바뀌는 때 — 막고 나서 (초, 시안 0.12, 사람마다 + 0.08).")]
        [SerializeField] private float hushAt = 0.12f;
        [Tooltip("테 두께 (설계 px, 시안 6).")]
        [SerializeField] private float bubbleLine = 6f;
        [Tooltip("꼬리 뿌리 — 반지름 비율 (시안 0.38).")]
        [SerializeField] private float tailRoot = 0.38f;
        [Tooltip("꼬리 뿌리 반폭 — 반지름 비율 (시안 0.08).")]
        [SerializeField] private float tailWidth = 0.08f;

        [Header("「승리 불가」 표 (시안 label: 얼굴 + (0, 72), 글자 폭 + 34 × 28·1.35, 기울기 −0.1 + i·0.07)")]
        [Tooltip("표가 붙기 시작하는 때 — 막고 나서 (초, 시안 0.22, 사람마다 + 0.08).")]
        [SerializeField] private float tagAt = 0.22f;
        [Tooltip("튀어나오는 시간 (초, 시안 0.14).")]
        [SerializeField] private float tagTime = 0.14f;
        [Tooltip("튀어나올 때 지나치는 정도 (시안 back 2.4).")]
        [SerializeField] private float tagOvershoot = 2.4f;
        [Tooltip("표 가운데 — 얼굴 가운데에서 (설계 px, 시안 0, 72).")]
        [SerializeField] private Vector2 tagOffset = new Vector2(0f, 72f);
        [Tooltip("표 기울기 — 첫 사람 · 사람마다 더하는 만큼 (라디안, 시안 −0.1 · 0.07).")]
        [SerializeField] private Vector2 tagTilt = new Vector2(-0.1f, 0.07f);
        [Tooltip("표 너비 = 글자 폭 + 이만큼 (설계 px, 시안 34).")]
        [SerializeField] private float tagPadding = 34f;
        [Tooltip("표 높이 = 글자 크기 × 이만큼 (시안 1.35).")]
        [SerializeField] private float tagHeight = 1.35f;
        [Tooltip("표 그림자 어긋남 (설계 px, 시안 5, 6).")]
        [SerializeField] private Vector2 tagShadowOffset = new Vector2(5f, 6f);
        [Tooltip("표 그림자 색 (시안 rgba(0,0,0,.3)).")]
        [SerializeField] private Color tagShadow = new Color(0f, 0f, 0f, 0.3f);
        [Tooltip("표 바탕 (시안 #d6282f).")]
        [SerializeField] private Color tagFill = new Color32(0xd6, 0x28, 0x2f, 0xff);
        [Tooltip("표 테 두께 (설계 px, 시안 5).")]
        [SerializeField] private float tagLine = 5f;

        [Header("앞발 (시안: 0.36 초부터 0.14 초에 y 1150 → 420 back 1.5, 기울기 −0.7 → −0.1 eo, 배율 1.12)")]
        [Tooltip("앞발이 솟기 시작하는 때 (초, 시안 0.36).")]
        [SerializeField] private float pawAt = 0.36f;
        [Tooltip("솟는 시간 (초, 시안 0.14).")]
        [SerializeField] private float pawTime = 0.14f;
        [Tooltip("솟을 때 지나치는 정도 (시안 back 1.5).")]
        [SerializeField] private float pawOvershoot = 1.5f;
        [Tooltip("손바닥 가운데 x (설계 px, 시안 420).")]
        [SerializeField] private float pawX = 420f;
        [Tooltip("손바닥 가운데 y — 처음 · 끝 (설계 px, 시안 1150 → 420).")]
        [SerializeField] private Vector2 pawY = new Vector2(1150f, 420f);
        [Tooltip("기울기 — 처음 · 끝 (라디안, 시안 −0.7 → −0.1).")]
        [SerializeField] private Vector2 pawTilt = new Vector2(-0.7f, -0.1f);
        [Tooltip("앞발 배율 (시안 1.12).")]
        [SerializeField] private float pawScale = 1.12f;
        [Tooltip("털 색 — 강아지 키 순서 na · choco · kongi · bori (시안 FUR).")]
        [SerializeField] private Color[] furColors =
        {
            new Color32(0xf5, 0xd6, 0xa8, 0xff),
            new Color32(0xa8, 0x95, 0x7a, 0xff),
            new Color32(0xf6, 0xd9, 0xb8, 0xff),
            new Color32(0xf6, 0xe6, 0xc8, 0xff),
        };
        [Tooltip("발바닥 젤리 색 (시안 #a8625c).")]
        [SerializeField] private Color padColor = new Color32(0xa8, 0x62, 0x5c, 0xff);
        [Tooltip("젤리 테 색 (시안 #6e3a36).")]
        [SerializeField] private Color padEdge = new Color32(0x6e, 0x3a, 0x36, 0xff);
        [Tooltip("젤리 위 분홍 빛 (시안 rgba(255,190,190,.45)).")]
        [SerializeField] private Color padBlush = new Color(1f, 190f / 255f, 190f / 255f, 0.45f);
        [Tooltip("큰 젤리 아래쪽 빗금 (시안 rgba(70,30,30,.25)).")]
        [SerializeField] private Color padHatch = new Color(70f / 255f, 30f / 255f, 30f / 255f, 0.25f);
        [Tooltip("발톱 색 (시안 #4a3a36).")]
        [SerializeField] private Color clawColor = new Color32(0x4a, 0x3a, 0x36, 0xff);
        [Tooltip("오른쪽 그늘 — 끝 색 (시안 rgba(120,70,30,.28), x −20 → 150 으로 짙어진다).")]
        [SerializeField] private Color shadeColor = new Color(120f / 255f, 70f / 255f, 30f / 255f, 0.28f);
        [Tooltip("그늘 연필 빗금 (시안 rgba(90,50,20,.32)).")]
        [SerializeField] private Color shadeHatch = new Color(90f / 255f, 50f / 255f, 20f / 255f, 0.32f);
        [Tooltip("왼쪽 위 밝은 빛 (시안 rgba(255,255,255,.18)).")]
        [SerializeField] private Color pawShine = new Color(1f, 1f, 1f, 0.18f);
        [Tooltip("앞발 테 두께 (설계 px, 시안 7).")]
        [SerializeField] private float pawLine = 7f;
        [Tooltip("털 삐침 선 두께 (설계 px, 시안 4).")]
        [SerializeField] private float furLine = 4f;

        [Header("흰 고리 (시안: 막고 0.32 초 동안 (420, 390) 에서 반지름 190 → 430 eo, 굵기 10·(1 − e) + 2, 진하기 1 − e)")]
        [Tooltip("고리가 사는 시간 (초, 시안 0.32).")]
        [SerializeField] private float ringLife = 0.32f;
        [Tooltip("고리 가운데 — 손바닥 가운데에서 위로 (설계 px, 시안 30).")]
        [SerializeField] private float ringRise = 30f;
        [Tooltip("반지름 — 처음 · 끝 (설계 px, 시안 190 → 430).")]
        [SerializeField] private Vector2 ringRadius = new Vector2(190f, 430f);
        [Tooltip("굵기 — 줄어드는 만큼 (설계 px, 시안 10).")]
        [SerializeField] private float ringWidth = 10f;
        [Tooltip("굵기 — 끝 (설계 px, 시안 2).")]
        [SerializeField] private float ringWidthMin = 2f;

        [Header("4 단계")]
        [Tooltip("4 단계 장식 길이 (초, 시안 500ms).")]
        [SerializeField] private float postWait = 0.5f;
        [Tooltip("「기다려!」 말풍선이 오르는 높이 (설계 px, 시안 vbubble −70).")]
        [SerializeField] private float bubbleRise = 70f;

        private bool[] _hushed;
        private int _count;
        private float _time;
        private float _tagWidth;
        private Color _fur;

        private void Awake()
        {
            if (calm == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(calm)} 가 비었다");
            if (confetti == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(confetti)} 가 비었다");
            if (stopped == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(stopped)} 가 비었다");
            if (sunburst == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(sunburst)} 가 비었다");
            if (tone == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(tone)} 가 비었다");
            if (paw == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(paw)} 가 비었다");
            if (pawRing == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(pawRing)} 가 비었다");
            for (int i = 0; i < people.Length; i++)
            {
                Person person = people[i];
                if (person == null || person.face == null || person.picture == null || person.shade == null || person.bubble == null || person.bubbleShape == null
                    || person.shout == null || person.hush == null || person.tag == null || person.tagBox == null || person.tagText == null)
                    Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(people)}[{i}] 그림(얼굴 · 말풍선 · 표)이 비었다");
            }

            confetti.Painter = PaintConfetti;
            sunburst.Painter = PaintSunburst;
            paw.Painter = PaintPaw;
            _hushed = new bool[people.Length];
            for (int i = 0; i < people.Length; i++)
            {
                int index = i;
                people[i].bubbleShape.Painter = pen => PaintBubble(pen, index);
                people[i].tagBox.Painter = PaintTag;
            }
        }

        public override void Begin(ComicContext context)
        {
            // 시안: max(1, min(3, others.length || 3)) — 낸 사람이 아닌 사람 수
            int others = context.Others;
            _count = others == 0 ? people.Length : Mathf.Clamp(others, 1, people.Length);
            Sprite art = context.Fx.Character;
            for (int i = 0; i < people.Length; i++)
            {
                Person person = people[i];
                bool shown = i < _count;
                person.face.gameObject.SetActive(shown);
                person.bubble.gameObject.SetActive(shown);
                person.tag.gameObject.SetActive(shown);
                if (shown)
                    ComicKit.ShowFace(person.picture, art, faceArea, missingFace);
            }

            // 시안 label: 상자 너비 = 글자 폭 + 34 (글자는 모두 같다)
            TMP_Text label = people[0].tagText;
            _tagWidth = label.GetPreferredValues(label.text).x + tagPadding;
            foreach (Person person in people)
                person.tagBox.Redraw();

            // 시안 FUR[P.me] — 쓴 사람의 강아지 털 색
            int key = 0;
            string fromKey = context.DogKeyOf(context.From);
            for (int i = 0; i < FxDog.Keys.Count; i++)
            {
                if (FxDog.Keys[i] == fromKey)
                    key = i;
            }
            _fur = furColors[Mathf.Min(key, furColors.Length - 1)];
            paw.Redraw();
            sunburst.Redraw();
        }

        public override void Draw(float t, ComicContext context)
        {
            _time = t;
            float hit = t - hitAt;
            bool ok = hit > 0f;

            if (calm.activeSelf == ok)
                calm.SetActive(!ok);
            if (stopped.activeSelf != ok)
                stopped.SetActive(ok);
            if (ok)
                ComicKit.Turn(sunburst.rectTransform, hit * sunburstSpin);
            else
                confetti.Redraw();
            Color toneColor = ok ? stoppedTone : calmTone;
            if (tone.color != toneColor)
                tone.color = toneColor;

            for (int i = 0; i < _count; i++)
                DrawPerson(people[i], i, t, hit);

            // 앞발 — 아래에서 솟아 막는다
            float pk = (t - pawAt) / pawTime;
            bool raised = pk > 0f;
            if (paw.gameObject.activeSelf != raised)
                paw.gameObject.SetActive(raised);
            float q = ComicKit.Cl(pk);
            float py = ComicKit.Lerp(pawY.x, pawY.y, ComicKit.Back(q, pawOvershoot));
            if (raised)
            {
                RectTransform pawRect = paw.rectTransform;
                ComicKit.Place(pawRect, pawX, py);
                ComicKit.Turn(pawRect, ComicKit.Lerp(pawTilt.x, pawTilt.y, ComicKit.Eo(q)));
                pawRect.localScale = new Vector3(pawScale, pawScale, 1f);
            }
            if (raised && ok && hit < ringLife)
            {
                float e = hit / ringLife;
                ComicKit.Place(pawRing.rectTransform, pawX, py - ringRise);
                pawRing.Set(ComicKit.Lerp(ringRadius.x, ringRadius.y, ComicKit.Eo(e)), ringWidth * (1f - e) + ringWidthMin);
                ComicKit.SetAlpha(pawRing, 1f - e);
            }
            else
            {
                ComicKit.SetAlpha(pawRing, 0f);
            }
        }

        public override IEnumerator PostRoutine(ComicContext context)
        {
            CardFxContext fx = context.Fx;
            if (fx.Layer != null && context.From != null && context.From.Anchor != null)
                fx.Layer.Bubble(context.From.Anchor, BubbleText, bubbleRise);
            for (float time = 0f; time < postWait; time += Time.deltaTime)
                yield return null;
        }

        private void DrawPerson(Person person, int index, float t, float hit)
        {
            bool ok = hit > 0f;
            float x = index % 2 == 0 ? peopleX.x : peopleX.y, y = peopleY.x + index * peopleY.y;
            float lift = ok ? 0f : Mathf.Abs(Mathf.Sin(t * bounceSpeed + index * bouncePhase)) * bounce;
            ComicKit.Place(person.face, x, y - lift);
            ComicKit.SetAlpha(person.shade, ok ? shadeAlpha : 0f);

            // 「찹츄!」 → 「…」 말풍선
            float bk = (t - bubbleAt - index * bubbleGap) / bubbleTime;
            if (bk > 0f)
            {
                float s = ComicKit.Back(bk, bubbleOvershoot);
                ComicKit.Place(person.bubble, x + bubbleOffset.x, y + bubbleOffset.y);
                person.bubble.localScale = new Vector3(s, s, 1f);
                bool hushed = ok && hit > hushAt + index * bubbleGap;
                if (_hushed[index] != hushed)
                {
                    _hushed[index] = hushed;
                    person.bubbleShape.Redraw();
                    person.shout.gameObject.SetActive(!hushed);
                    person.hush.gameObject.SetActive(hushed);
                }
            }
            else
            {
                person.bubble.localScale = Vector3.zero;
            }

            // 「승리 불가」 표
            float sk = (hit - tagAt - index * bubbleGap) / tagTime;
            float tagScale = sk > 0f ? ComicKit.Back(sk, tagOvershoot) : 0f;
            ComicKit.Place(person.tag, x + tagOffset.x, y + tagOffset.y);
            ComicKit.Turn(person.tag, tagTilt.x + index * tagTilt.y);
            person.tag.localScale = new Vector3(tagScale, tagScale, 1f);
        }

        private void PaintConfetti(ComicPen pen)
        {
            var random = new ComicRandom(confettiSeed);
            float wrap = ComicKit.Height + confettiWrap;
            for (int i = 0; i < confettiCount; i++)
            {
                float x = (random.Next() * ComicKit.Width + _time * confettiDrift) % ComicKit.Width;
                float y0 = random.Next() * ComicKit.Height * confettiSpread;
                float y = (y0 + _time * (confettiFall.x + random.Next() * confettiFall.y)) % wrap - confettiWrap / 2f;
                float r0 = random.Next() * ComicKit.Tau;
                float r = r0 + _time * (confettiSpin.x + random.Next() * confettiSpin.y);
                pen.Save();
                pen.Translate(x, y);
                pen.Rotate(r);
                pen.BeginPath();
                pen.Rect(-confettiSize.x / 2f, -confettiSize.y / 2f, confettiSize.x, confettiSize.y);
                pen.Fill(confettiColors[i % confettiColors.Length]);
                pen.Restore();
            }
        }

        // 시안 sunburst(x, y, n, a0, c1, c2) — 홀수 갈래만 칠한다 (짝수 갈래 색은 투명), 색 = Graphic.color
        private void PaintSunburst(ComicPen pen)
        {
            pen.BeginPath();
            for (int i = 1; i < sunburstCount; i += 2)
            {
                pen.MoveTo(0f, 0f);
                pen.Arc(0f, 0f, sunburstReach, (float)i / sunburstCount * ComicKit.Tau, (float)(i + 1) / sunburstCount * ComicKit.Tau);
                pen.ClosePath();
            }
            pen.Fill(Color.white);
        }

        // 시안 bubble(x, y, w, h, tx, ty, {fill}) — 이 말풍선 가운데 기준
        private void PaintBubble(ComicPen pen, int index)
        {
            bool hushed = _hushed[index];
            Vector2 size = hushed ? hushSize : shoutSize;
            Color fill = hushed ? hushFill : shoutFill;
            Vector2 tail = tailOffset - bubbleOffset;
            float w = size.x, h = size.y;
            pen.BeginPath();
            pen.Ellipse(0f, 0f, w / 2f, h / 2f, 0f, 0f, ComicKit.Tau);
            pen.Fill(fill);
            pen.Stroke(ComicKit.Ink, bubbleLine);

            float a = Mathf.Atan2(tail.y, tail.x);
            float bx = Mathf.Cos(a) * w * tailRoot, by = Mathf.Sin(a) * h * tailRoot;
            float nx = -Mathf.Sin(a) * w * tailWidth, ny = Mathf.Cos(a) * h * tailWidth;
            pen.BeginPath();
            pen.MoveTo(bx + nx, by + ny);
            pen.LineTo(tail.x, tail.y);
            pen.LineTo(bx - nx, by - ny);
            pen.ClosePath();
            pen.Fill(fill);
            pen.Stroke(ComicKit.Ink, bubbleLine);

            // 꼬리 뿌리의 테를 덮는다
            float inset = bubbleLine / 2f + BubbleCoverInset;
            pen.BeginPath();
            pen.Ellipse(0f, 0f, w / 2f - inset, h / 2f - inset, 0f, 0f, ComicKit.Tau);
            pen.Fill(fill);
        }

        // 시안 label — 그림자 + 바탕 + 먹 테 (표 가운데 기준)
        private void PaintTag(ComicPen pen)
        {
            if (_tagWidth <= 0f)
                return;
            float w = _tagWidth, h = people[0].tagText.fontSize * tagHeight;
            pen.BeginPath();
            pen.Rect(-w / 2f + tagShadowOffset.x, -h / 2f + tagShadowOffset.y, w, h);
            pen.Fill(tagShadow);
            pen.BeginPath();
            pen.Rect(-w / 2f, -h / 2f, w, h);
            pen.Fill(tagFill);
            pen.Stroke(ComicKit.Ink, tagLine);
        }

        // 시안 paw — 손바닥이 화면을 향한 큰 앞발 (손바닥 가운데 기준, 배율 · 기울기는 RectTransform). 모양 자료는 맨 위 Paw* 값.
        // 떨리는 윤곽 → 먹 그림자 · 털색 → 오른쪽 그늘 · 연필 빗금 · 왼쪽 빛 (윤곽 안만) → 먹 테 → 발가락 사이 선 · 털 삐침 · 발톱 → 젤리 넷 · 큰 젤리.
        // 떨림 난수는 시안과 같은 차례로 뽑는다 (rng(77) — 윤곽 · 빗금 · 갈라진 선 · 털 삐침 · 젤리).
        private void PaintPaw(ComicPen pen)
        {
            var random = new ComicRandom(PawSeed);
            float Jitter(float value, float spread) => value + (random.Next() - 0.5f) * spread;

            // 윤곽 점: 팔 왼쪽 → 손바닥 → 발가락 넷 → 손바닥 → 팔 오른쪽 (시안 add — 인자의 J 가 먼저, 그다음 x · y 의 J)
            var outline = new List<Vector2>();
            void Add(float x, float y)
            {
                float px = Jitter(x, PawJitter);
                float py = Jitter(y, PawJitter);
                outline.Add(new Vector2(px, py));
            }
            for (int i = 0; i <= 8; i++)
            {
                float v = i / 8f;
                float x = -PawArmX - PawArmBulge * Mathf.Sin(v * Mathf.PI) + Jitter(0f, PawArmJitter);
                Add(x, PawArmBottom - v * (PawArmBottom - PawArmTop));
            }
            for (int i = 0; i <= 10; i++)
            {
                float a = PawPalmLeft - i / 10f * PawPalmSweep;
                Add(Mathf.Cos(a) * PawPalm.x, -Mathf.Sin(a) * PawPalm.y + PawPalmY);
            }
            for (int k = 0; k < PawToes.Length; k++)
            {
                Vector3 toe = PawToes[k];
                float a0 = Mathf.PI * (k < 2 ? 1.05f : 1.15f), a1 = Mathf.PI * (k < 2 ? -0.15f : -0.05f);
                for (int i = 0; i <= 9; i++)
                {
                    float a = a0 + (a1 - a0) * i / 9f;
                    Add(toe.x + Mathf.Cos(a) * toe.z, toe.y - Mathf.Abs(Mathf.Sin(a)) * toe.z * PawToeTall);
                }
            }
            for (int i = 0; i <= 10; i++)
            {
                float a = PawPalmRight - i / 10f * PawPalmSweep;
                Add(Mathf.Cos(a) * PawPalm.x, -Mathf.Sin(a) * PawPalm.y + PawPalmY);
            }
            for (int i = 0; i <= 8; i++)
            {
                float v = i / 8f;
                float x = PawArmX + PawArmBulge * Mathf.Sin((1f - v) * Mathf.PI) + Jitter(0f, PawArmJitter);
                Add(x, PawArmTop + v * (PawArmBottom - PawArmTop));
            }

            // 먹 그림자(조금 크게 어긋나 테 두께가 고르지 않게) → 털색
            pen.Save();
            pen.Translate(PawInkOffset.x, PawInkOffset.y);
            pen.Scale(PawInkScale.x, PawInkScale.y);
            OutlinePath(pen, outline);
            pen.Fill(ComicKit.Ink);
            pen.Restore();
            OutlinePath(pen, outline);
            pen.Fill(_fur);

            // 그늘: 오른쪽으로 짙어지는 털색 + 연필 빗금 + 왼쪽 위 빛 (시안은 윤곽으로 잘라 그린다)
            Color shadeFrom = shadeColor;
            shadeFrom.a = 0f;
            OutlinePath(pen, outline);
            pen.FillLinear(-20f, 0f, 150f, 0f, shadeFrom, shadeColor);
            for (int i = 0; i < 26; i++)
            {
                float y = Jitter(-40f + i * 36f, 8f);
                var from = new Vector2(Jitter(60f, 10f), y + 40f);
                var to = new Vector2(Jitter(120f, 10f), y);
                StrokeInside(pen, from, to, outline, shadeHatch, 3f);
            }
            FillPolygon(pen, ClipPolygon(outline, EllipsePolygon(new Vector2(-70f, -20f), new Vector2(70f, 110f), -0.3f)), pawShine);

            // 테 먹선 한 번 더 (떨림)
            OutlinePath(pen, outline);
            pen.Stroke(ComicKit.Ink, pawLine);

            // 발가락 사이 갈라진 선
            foreach (Vector4 split in PawSplits)
            {
                pen.BeginPath();
                pen.MoveTo(split.x, split.y);
                var at = new Vector2(split.x, split.y);
                QuadTo(pen, ref at, new Vector2(Jitter(split.x, 8f), (split.y + split.w) / 2f), new Vector2(split.z, split.w));
                pen.Stroke(ComicKit.Ink, 5f);
            }

            // 털 삐침: 팔 양옆 · 손목
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 1 ? 1f : -1f;
                float y = Jitter(160f + i * 48f, 10f);
                float x = side * Jitter(106f, 6f);
                float tipX = x + side * (14f + random.Next() * 10f);
                float tipY = y - 10f - random.Next() * 8f;
                pen.BeginPath();
                pen.MoveTo(x, y);
                pen.LineTo(tipX, tipY);
                pen.LineTo(x + side * 2f, y - 18f);
                pen.Stroke(ComicKit.Ink, furLine);
            }

            // 발톱
            foreach (Vector3 toe in PawToes)
            {
                pen.BeginPath();
                var at = new Vector2(toe.x - 7f, toe.y - toe.z * 0.95f);
                pen.MoveTo(at.x, at.y);
                QuadTo(pen, ref at, new Vector2(toe.x, toe.y - toe.z * 1.45f), new Vector2(toe.x + 6f, toe.y - toe.z * 0.95f));
                pen.ClosePath();
                pen.Fill(clawColor);
            }

            // 발가락 젤리: 고르지 않은 콩 모양
            for (int k = 0; k < PawToes.Length; k++)
            {
                Vector3 toe = PawToes[k];
                float rx = toe.z * 0.62f, ry = toe.z * 0.72f;
                pen.Save();
                pen.Translate(toe.x, toe.y + 6f);
                pen.Rotate(PawBeanTilt[k]);
                pen.BeginPath();
                for (int i = 0; i <= 20; i++)
                {
                    float a = i / 20f * ComicKit.Tau;
                    float scale = 1f + 0.07f * Mathf.Sin(a * 3f + toe.x) + (random.Next() - 0.5f) * 0.04f;
                    float x = Mathf.Cos(a) * rx * scale, y = Mathf.Sin(a) * ry * scale;
                    if (i == 0)
                        pen.MoveTo(x, y);
                    else
                        pen.LineTo(x, y);
                }
                pen.ClosePath();
                pen.Fill(padColor);
                pen.Stroke(padEdge, 5f);
                pen.BeginPath();
                pen.Ellipse(-rx * 0.2f, -ry * 0.25f, rx * 0.45f, ry * 0.35f, 0f, 0f, ComicKit.Tau);
                pen.Fill(padBlush);
                pen.Restore();
            }

            // 큰 젤리 + 분홍 빛 · 아래쪽 빗금 (젤리 안만)
            pen.BeginPath();
            pen.MoveTo(PawPad[0].x, PawPad[0].y);
            for (int i = 1; i + 2 < PawPad.Length; i += 3)
                pen.BezierCurveTo(PawPad[i].x, PawPad[i].y, PawPad[i + 1].x, PawPad[i + 1].y, PawPad[i + 2].x, PawPad[i + 2].y);
            pen.ClosePath();
            pen.Fill(padColor);
            pen.Stroke(padEdge, 6f);
            List<Vector2> pad = PadPolygon();
            FillPolygon(pen, ClipPolygon(pad, EllipsePolygon(new Vector2(-30f, 22f), new Vector2(46f, 26f), -0.25f)), padBlush);
            for (int i = 0; i < 8; i++)
                StrokeInside(pen, new Vector2(-80f + i * 22f, 90f), new Vector2(-60f + i * 22f, 40f), pad, padHatch, 2f);
        }

        // 시안 path(): 점 사이 가운데를 지나는 2차 곡선으로 매끄럽게 한 바퀴
        private static void OutlinePath(ComicPen pen, List<Vector2> points)
        {
            pen.BeginPath();
            Vector2 at = points[0];
            pen.MoveTo(at.x, at.y);
            for (int i = 1; i < points.Count - 1; i++)
                QuadTo(pen, ref at, points[i], (points[i] + points[i + 1]) / 2f);
            Vector2 last = points[points.Count - 1];
            pen.LineTo(last.x, last.y);
            pen.ClosePath();
        }

        // 시안 quadraticCurveTo 를 같은 3차 곡선으로 (조절점 = 끝점 + ⅔·(2차 조절점 − 끝점))
        private static void QuadTo(ComicPen pen, ref Vector2 at, Vector2 control, Vector2 to)
        {
            Vector2 c1 = at + (control - at) * 2f / 3f, c2 = to + (control - to) * 2f / 3f;
            pen.BezierCurveTo(c1.x, c1.y, c2.x, c2.y, to.x, to.y);
            at = to;
        }

        private static void FillPolygon(ComicPen pen, List<Vector2> polygon, Color color)
        {
            if (polygon.Count < 3)
                return;
            pen.BeginPath();
            pen.MoveTo(polygon[0].x, polygon[0].y);
            for (int i = 1; i < polygon.Count; i++)
                pen.LineTo(polygon[i].x, polygon[i].y);
            pen.ClosePath();
            pen.Fill(color);
        }

        // 큰 젤리 곡선을 점으로 (빗금 · 빛을 젤리 안으로 자를 때)
        private static List<Vector2> PadPolygon()
        {
            var points = new List<Vector2>();
            for (int i = 1; i + 2 < PawPad.Length; i += 3)
            {
                Vector2 p0 = PawPad[i - 1], c1 = PawPad[i], c2 = PawPad[i + 1], p1 = PawPad[i + 2];
                for (int step = 0; step < ClipCurveSteps; step++)
                {
                    float t = (float)step / ClipCurveSteps, u = 1f - t;
                    points.Add(u * u * u * p0 + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * p1);
                }
            }
            return points;
        }

        private static List<Vector2> EllipsePolygon(Vector2 center, Vector2 radius, float tilt)
        {
            var points = new List<Vector2>(ClipSides);
            float cos = Mathf.Cos(tilt), sin = Mathf.Sin(tilt);
            for (int i = 0; i < ClipSides; i++)
            {
                float a = (float)i / ClipSides * ComicKit.Tau;
                float x = Mathf.Cos(a) * radius.x, y = Mathf.Sin(a) * radius.y;
                points.Add(center + new Vector2(x * cos - y * sin, x * sin + y * cos));
            }
            return points;
        }

        // 다각형(오목해도 된다)을 볼록 다각형 clip 안으로 자른다 (Sutherland–Hodgman — 시안 clip() 대신)
        private static List<Vector2> ClipPolygon(List<Vector2> subject, List<Vector2> clip)
        {
            Vector2 middle = Vector2.zero;
            foreach (Vector2 point in clip)
                middle += point;
            middle /= clip.Count;
            var output = new List<Vector2>(subject);
            for (int e = 0; e < clip.Count && output.Count > 0; e++)
            {
                Vector2 a = clip[e], b = clip[(e + 1) % clip.Count];
                float inside = Cross(b - a, middle - a);
                var input = output;
                output = new List<Vector2>(input.Count);
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 p = input[i], q = input[(i + 1) % input.Count];
                    float dp = Cross(b - a, p - a), dq = Cross(b - a, q - a);
                    bool pIn = dp * inside >= 0f, qIn = dq * inside >= 0f;
                    if (pIn)
                        output.Add(p);
                    if (pIn != qIn)
                        output.Add(p + (q - p) * (dp / (dp - dq)));
                }
            }
            return output;
        }

        // 선분 중 다각형 안에 든 조각만 긋는다 (시안 clip() 안의 stroke)
        private static void StrokeInside(ComicPen pen, Vector2 from, Vector2 to, List<Vector2> polygon, Color color, float width)
        {
            var cuts = new List<float> { 0f, 1f };
            Vector2 d = to - from;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], e = polygon[(i + 1) % polygon.Count] - a;
                float denominator = Cross(d, e);
                if (Mathf.Abs(denominator) < 1e-6f)
                    continue;
                float t = Cross(a - from, e) / denominator, u = Cross(a - from, d) / denominator;
                if (t > 0f && t < 1f && u >= 0f && u <= 1f)
                    cuts.Add(t);
            }
            cuts.Sort();
            for (int i = 0; i + 1 < cuts.Count; i++)
            {
                float t0 = cuts[i], t1 = cuts[i + 1];
                if (t1 - t0 < 1e-4f || !Contains(polygon, from + d * ((t0 + t1) / 2f)))
                    continue;
                Vector2 p = from + d * t0, q = from + d * t1;
                pen.BeginPath();
                pen.MoveTo(p.x, p.y);
                pen.LineTo(q.x, q.y);
                pen.Stroke(color, width);
            }
        }

        // 점이 다각형 안인가 (짝홀 규칙)
        private static bool Contains(List<Vector2> polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Vector2 a = polygon[i], b = polygon[j];
                if ((a.y > point.y) != (b.y > point.y) && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
