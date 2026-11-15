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
    /// 기다려!(카드 123) 만화 컷인 장면 — 다들 「찹츄!」 외치려는 순간 앞발 하나로 스읍! (시안 CI2.SC["기다려!"]).
    /// 분홍 꽃가루 속에서 나머지 사람들(얼굴 원)이 들썩이며 차례로 「찹츄!」 말풍선을 띄운다 → 0.36 초 쓴 사람의 큰 앞발이 아래에서 솟는다 →
    /// 0.5 초 판이 빨갛게 바뀌고(햇살 · 집중선) 흰 고리가 퍼진다 → 「스읍!」 → 말풍선이 차례로 「…」 으로 줄고 얼굴마다 「승리 불가」 표가 붙는다.
    /// 4 단계 = 쓴 사람에게 「기다려!」 말풍선 + 0.5 초 (시안 FXA 기다려! s4, 「승리 불가」 상태 글자는 게임 화면 몫이라 뺐다).
    /// 연출 길이 2.86초 (3 단계 2.36초 + 4 단계 0.5초) — 카드 처리 시간(resolveSeconds)은 게임 화면에 연출을 붙일 때 맞춘다 (PM 결정 2026-11-15).
    /// 얼굴 그림은 모두 <see cref="CardFxContext.Character"/> 의 얼굴 칸 — 자리마다 얼굴 그림이 생기면 그것으로 바꾼다.
    /// </summary>
    public class WaitComicScene : ComicScene
    {
        // 시안 FXA 기다려! s4: vbubble(u, "기다려!")
        private const string BubbleText = "기다려!";
        // 시안 bubble: 꼬리 뿌리의 테를 덮는 안쪽 타원 = 테 안쪽 반 + 0.5 px 더 들인다
        private const float BubbleCoverInset = 0.5f;
        // 시안 paw 모양 자료 (손바닥 가운데 기준 설계 px — 조절할 값이 아니라 그림 모양이라 Inspector 에 열지 않는다)
        // 털 결: 2차 조절점 = 시작 + (d, 20), 끝 = 시작 + (0.4d, 50)
        private const float PawFurBend = 20f;
        private const float PawFurLength = 50f;
        private const float PawFurEnd = 0.4f;
        private const float PawPalmShineTilt = -0.3f;

        // 팔 테두리: 시작 · 3차 곡선(조절점 둘 + 끝) · 선 끝 · 3차 곡선(조절점 둘 + 끝)
        private static readonly Vector2[] PawArm =
        {
            new Vector2(-118f, 40f), new Vector2(-132f, 240f), new Vector2(-120f, 520f), new Vector2(-104f, 900f),
            new Vector2(104f, 900f), new Vector2(120f, 520f), new Vector2(132f, 240f), new Vector2(118f, 40f),
        };
        // 손바닥 타원 반지름
        private static readonly Vector2 PawPalm = new Vector2(165f, 150f);
        // 털 결 넷 (시작 x, 시작 y, 휘는 쪽 d)
        private static readonly Vector3[] PawFur =
        {
            new Vector3(-150f, 40f, 30f), new Vector3(150f, 40f, -30f), new Vector3(-118f, 260f, 24f), new Vector3(118f, 300f, -24f),
        };
        // 큰 발바닥: 시작 + 3차 곡선 넷 (조절점 둘 + 끝)
        private static readonly Vector2[] PawPad =
        {
            new Vector2(0f, -6f),
            new Vector2(60f, -28f), new Vector2(106f, 20f), new Vector2(92f, 62f),
            new Vector2(80f, 104f), new Vector2(30f, 96f), new Vector2(0f, 92f),
            new Vector2(-30f, 96f), new Vector2(-80f, 104f), new Vector2(-92f, 62f),
            new Vector2(-106f, 20f), new Vector2(-60f, -28f), new Vector2(0f, -6f),
        };
        // 발가락 넷 (x, y, 기울기) · 반지름 · 광택 (어긋남 x, y, 반지름 x, y) · 손바닥 광택 (x, y, 반지름 x, y)
        private static readonly Vector3[] PawToes =
        {
            new Vector3(-112f, -56f, 0.5f), new Vector3(-42f, -104f, 0.15f), new Vector3(42f, -104f, -0.15f), new Vector3(112f, -56f, -0.5f),
        };
        private static readonly Vector2 PawToe = new Vector2(32f, 40f);
        private static readonly Vector4 PawToeShine = new Vector4(-8f, -12f, 9f, 13f);
        private static readonly Vector4 PawPalmShine = new Vector4(-30f, 18f, 26f, 14f);

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
        [Tooltip("「스읍!」 글자 (찍히기).")]
        [SerializeField] private ComicStamp hushWord;

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
        [Tooltip("발바닥 색 (시안 #3a2626).")]
        [SerializeField] private Color padColor = new Color32(0x3a, 0x26, 0x26, 0xff);
        [Tooltip("발바닥 광택 (시안 rgba(255,255,255,.3)).")]
        [SerializeField] private Color padShine = new Color(1f, 1f, 1f, 0.3f);
        [Tooltip("앞발 테 두께 (설계 px, 시안 9).")]
        [SerializeField] private float pawLine = 9f;
        [Tooltip("털 결 선 두께 (설계 px, 시안 4).")]
        [SerializeField] private float furLine = 4f;
        [Tooltip("털 결 진하기 (시안 0.3).")]
        [SerializeField] private float furAlpha = 0.3f;

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

        [Header("「스읍!」")]
        [Tooltip("찍히기 시작하는 때 (초, 시안 0.52).")]
        [SerializeField] private float hushWordAt = 0.52f;
        [Tooltip("찍히는 시간 (초, 시안 0.16).")]
        [SerializeField] private float hushWordTime = 0.16f;

        [Header("4 단계")]
        [Tooltip("4 단계 장식 길이 (초, 시안 500ms).")]
        [SerializeField] private float postWait = 0.5f;
        [Tooltip("「기다려!」 말풍선이 오르는 높이 (설계 px, 시안 vbubble −70).")]
        [SerializeField] private float bubbleRise = 70f;

        private readonly List<bool> _hushed = new List<bool>();
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
            if (hushWord == null)
                Debug.LogError($"[{nameof(WaitComicScene)}] {name} 의 {nameof(hushWord)} 가 비었다");
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
            for (int i = 0; i < people.Length; i++)
            {
                int index = i;
                _hushed.Add(false);
                people[i].bubbleShape.Painter = pen => PaintBubble(pen, index);
                people[i].tagBox.Painter = PaintTag;
            }
        }

        public override void Begin(ComicContext context)
        {
            // 시안: max(1, min(3, others.length || 3)) — 낸 사람이 아닌 사람 수
            int others = 0;
            foreach (CardFxSeat seat in context.Fx.Seats)
            {
                if (seat != null && seat != context.From)
                    others++;
            }
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
                    ShowFace(person.picture, art);
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

            hushWord.Apply((t - hushWordAt) / hushWordTime);
        }

        public override IEnumerator PostRoutine(ComicContext context)
        {
            CardFxContext fx = context.Fx;
            if (fx.Layer != null && context.From != null && context.From.Anchor != null)
                fx.Layer.Bubble(context.From.Anchor, BubbleText, bubbleRise);
            for (float time = 0f; time < postWait; time += Time.deltaTime)
                yield return null;
        }

        // 시안 avatar 의 그림 — 얼굴 칸을 원 안에 채운다 (그림이 없으면 회색 면)
        private void ShowFace(RawImage picture, Sprite art)
        {
            if (art == null)
            {
                picture.texture = null;
                picture.color = missingFace;
                return;
            }
            Texture texture = art.texture;
            Rect rect = art.rect;
            picture.texture = texture;
            picture.color = Color.white;
            picture.uvRect = new Rect(
                (rect.x + faceArea.x * rect.width) / texture.width,
                (rect.y + faceArea.y * rect.height) / texture.height,
                faceArea.width * rect.width / texture.width,
                faceArea.height * rect.height / texture.height);
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
                float s = Mathf.Max(0f, ComicKit.Back(Mathf.Min(1f, bk), bubbleOvershoot));
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
            float tagScale = sk > 0f ? Mathf.Max(0f, ComicKit.Back(Mathf.Min(1f, sk), tagOvershoot)) : 0f;
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
        private void PaintPaw(ComicPen pen)
        {
            // 팔: 왼쪽 위 → (3차 곡선) 왼쪽 아래 → 오른쪽 아래 → (3차 곡선) 오른쪽 위
            pen.BeginPath();
            pen.MoveTo(PawArm[0].x, PawArm[0].y);
            pen.BezierCurveTo(PawArm[1].x, PawArm[1].y, PawArm[2].x, PawArm[2].y, PawArm[3].x, PawArm[3].y);
            pen.LineTo(PawArm[4].x, PawArm[4].y);
            pen.BezierCurveTo(PawArm[5].x, PawArm[5].y, PawArm[6].x, PawArm[6].y, PawArm[7].x, PawArm[7].y);
            pen.ClosePath();
            pen.Fill(_fur);
            pen.Stroke(ComicKit.Ink, pawLine);

            // 손바닥
            pen.BeginPath();
            pen.Ellipse(0f, 0f, PawPalm.x, PawPalm.y, 0f, 0f, ComicKit.Tau);
            pen.Fill(_fur);
            pen.Stroke(ComicKit.Ink, pawLine);

            // 털 결 — 시안 quadraticCurveTo(a + d, b + 20, a + 0.4d, b + 50) 를 같은 3차 곡선으로 (조절점 = 끝점 + ⅔·(2차 조절점 − 끝점))
            pen.Save();
            pen.Alpha = furAlpha;
            foreach (Vector3 fur in PawFur)
            {
                Vector2 from = new Vector2(fur.x, fur.y), bend = from + new Vector2(fur.z, PawFurBend), to = from + new Vector2(fur.z * PawFurEnd, PawFurLength);
                Vector2 c1 = from + (bend - from) * 2f / 3f, c2 = to + (bend - to) * 2f / 3f;
                pen.BeginPath();
                pen.MoveTo(from.x, from.y);
                pen.BezierCurveTo(c1.x, c1.y, c2.x, c2.y, to.x, to.y);
                pen.Stroke(ComicKit.Ink, furLine);
            }
            pen.Restore();

            // 큰 발바닥: 위 가운데에서 3차 곡선 넷으로 한 바퀴
            pen.BeginPath();
            pen.MoveTo(PawPad[0].x, PawPad[0].y);
            for (int i = 1; i + 2 < PawPad.Length; i += 3)
                pen.BezierCurveTo(PawPad[i].x, PawPad[i].y, PawPad[i + 1].x, PawPad[i + 1].y, PawPad[i + 2].x, PawPad[i + 2].y);
            pen.Fill(padColor);

            // 발가락 넷 + 광택
            foreach (Vector3 toe in PawToes)
            {
                pen.BeginPath();
                pen.Ellipse(toe.x, toe.y, PawToe.x, PawToe.y, toe.z, 0f, ComicKit.Tau);
                pen.Fill(padColor);
                pen.BeginPath();
                pen.Ellipse(toe.x + PawToeShine.x, toe.y + PawToeShine.y, PawToeShine.z, PawToeShine.w, toe.z, 0f, ComicKit.Tau);
                pen.Fill(padShine);
            }
            pen.BeginPath();
            pen.Ellipse(PawPalmShine.x, PawPalmShine.y, PawPalmShine.z, PawPalmShine.w, PawPalmShineTilt, 0f, ComicKit.Tau);
            pen.Fill(padShine);
        }
    }
}
