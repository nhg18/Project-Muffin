using System;
using Chapchu.UI.Fx.Comic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Drool
{
    /// <summary>
    /// 군침이 싹 도노(카드 1014) 만화 컷인 장면 — 남의 카드가 더 커 보이는 법 (시안 CI2.SC["군침이 싹 도노"]).
    /// 쓴 쪽 강아지가 눈을 반짝이며 군침을 흘리고, 왼쪽 위 상대 얼굴 창들 아래 카드가 0.72 · 0.88 · 1.04 초에 하나씩 입으로 날아들어 「냠!」 · 부스러기 ·
    /// 오른쪽 위 「+N」. 카드를 뺏긴 쪽 창엔 「-1」 표와 땀.
    /// 상대 창 수 = 낸 사람이 아닌 사람 수(<see cref="ComicContext.Others"/>, 많아야 셋 · 모르면 셋 — 시안은 늘 셋을 그린다), 얼굴은 <see cref="CardFxContext.Character"/> (사람마다 그림이 생기기 전까지 모두 같다).
    /// 4 단계 장식은 없다 (시안 FXA 군침이 싹 도노 s4 = 카드 옮기기뿐 — 게임 화면 몫).
    /// 연출 길이 2.46초 (3 단계만) — 카드 처리 시간(resolveSeconds)에 3 단계 실측 길이를 더해 두었다 (10-ui.md 5-1). 길이를 바꾸면 그 값도 같이 바꾼다.
    /// </summary>
    public class DroolComicScene : ComicScene
    {
        // 시안: 「+」 + 뺏은 장수
        private const string CountFormat = "+{0}";
        // 시안 군침 모양 (강아지 그림 px, L = 길이, r = 방울 반지름, s = 흔들림):
        // 왼쪽 입 끝 (−16, −4) → 조절점 (−20, 0.35L) · (−6 + s − r, 0.7L) → 방울 왼쪽 (s − r, L + 0.4r) → 방울 아래 반원 →
        // 조절점 (s + r, 0.6L) · (16, 0.3L) → 오른쪽 입 끝 (18, −4). 빛 = 타원 (s − 0.35r, L + 0.1r) 반지름 0.22r · 0.36r 기울기 0.4 + 점 (s + 0.35r, L + 0.75r) 반지름 0.12r
        private const float LipLeft = -16f;
        private const float LipRight = 18f;
        private const float LipY = -4f;
        private const float LeftPull = -20f;
        private const float LeftPullY = 0.35f;
        private const float LeftBend = -6f;
        private const float LeftBendY = 0.7f;
        private const float RightPullY = 0.6f;
        private const float RightBend = 16f;
        private const float RightBendY = 0.3f;
        private const float DropCenter = 0.4f;
        private const float ShineX = -0.35f;
        private const float ShineY = 0.1f;
        private const float ShineRadiusX = 0.22f;
        private const float ShineRadiusY = 0.36f;
        private const float ShineAngle = 0.4f;
        private const float DotX = 0.35f;
        private const float DotY = 0.75f;
        private const float DotRadius = 0.12f;
        // 시안 sparkle: 꼭짓점 여덟 (바깥 · 안쪽 번갈아)
        private const int SparklePoints = 8;
        // 시안 배경 반짝이: 반짝이는 때 · 기울기를 0 ~ 6 만큼 어긋낸다
        private const float SparklePhase = 6f;
        // 시안 over: 두 눈에 반짝
        private static readonly Vector2[] Eyes = { FxDog.EyeLeft, FxDog.EyeRight };

        /// <summary>상대 한 사람 — 얼굴 창(동그라미 · 얼굴 · 「-1」 표 · 땀)과 그 아래 카드 한 장 (날 때 잔상 없이 — 시안 잔상 셋은 반투명 카드가 겹쳐 보였다).</summary>
        [Serializable]
        private class Rival
        {
            [Tooltip("이 상대 묶음 — 상대가 없으면 끈다.")]
            public GameObject root;
            [Tooltip("얼굴 창 — 자리 = 창 가운데, 크기 = 튀어나오기 (시안 back).")]
            public RectTransform badge;
            [Tooltip("얼굴 그림 — 동그란 Mask 아래, 창을 꽉 채운다.")]
            public RawImage face;
            [Tooltip("「-1」 표 (상자 + 글자) — 카드가 날아가기 시작하면 켠다.")]
            public GameObject lostTag;
            [Tooltip("땀.")]
            public ComicSweat sweat;
            [Tooltip("카드.")]
            public ComicCard card;
        }

        [Header("그림 (그리는 순서)")]
        [Tooltip("빛 — 더하기 머티리얼 (장면이 그린다).")]
        [SerializeField] private ComicSketch glow;
        [Tooltip("집중선 (흰색).")]
        [SerializeField] private ComicFocusLines focusLines;
        [Tooltip("배경 반짝이 열 (장면이 그린다).")]
        [SerializeField] private ComicSketch sparkles;
        [Tooltip("쓴 쪽 강아지 (왼쪽을 봄).")]
        [SerializeField] private ComicDog eater;
        [Tooltip("강아지 얼굴 덧그림 — 눈 반짝 · 군침 (장면이 강아지 그림 좌표로 그린다).")]
        [SerializeField] private ComicSketch eaterFace;
        [Tooltip("상대 셋 — 왼쪽부터 (턴 순서).")]
        [SerializeField] private Rival[] rivals = new Rival[3];
        [Tooltip("씹을 때 부스러기 (장면이 그린다).")]
        [SerializeField] private ComicSketch crumbs;
        [Tooltip("「냠!」 글자 (찍히기) — 자리는 입에서 (215, −120).")]
        [SerializeField] private ComicStamp yum;
        [Tooltip("「+N」 글자.")]
        [SerializeField] private ComicWord count;
        [Tooltip("「+N」 찍히기 (같은 오브젝트).")]
        [SerializeField] private ComicStamp countStamp;

        [Header("때 (초)")]
        [Tooltip("카드를 하나씩 삼키는 때 — 상대 차례 (초, 시안 0.72 · 0.88 · 1.04).")]
        [SerializeField] private float[] chompAt = { 0.72f, 0.88f, 1.04f };

        [Header("빛 · 반짝이 (시안 glow(780, 380, 560, .5) · sparkle × 10)")]
        [Tooltip("빛 가운데 (설계 px, 시안 780, 380).")]
        [SerializeField] private Vector2 glowAt = new Vector2(780f, 380f);
        [Tooltip("빛 반지름 (설계 px, 시안 560).")]
        [SerializeField] private float glowRadius = 560f;
        [Tooltip("빛 색 · 진하기 (시안 rgba(255, 220, 245, .5)).")]
        [SerializeField] private Color glowColor = new Color32(0xff, 0xdc, 0xf5, 0x80);
        [Tooltip("반짝이 수 (시안 10).")]
        [SerializeField] private int sparkleCount = 10;
        [Tooltip("반짝이 씨앗 — 반짝이 i 는 이것 + i (시안 40).")]
        [SerializeField] private int sparkleSeed = 40;
        [Tooltip("반짝이 자리 범위 — 왼쪽 위 · 폭 높이 (설계 px, 시안 x 160 + 0 ~ 960, y 80 + 0 ~ 420).")]
        [SerializeField] private Rect sparkleArea = new Rect(160f, 80f, 960f, 420f);
        [Tooltip("반짝이 크기 — 최소 · 더하는 범위 (설계 px, 시안 10 + 0 ~ 16).")]
        [SerializeField] private Vector2 sparkleSize = new Vector2(10f, 16f);
        [Tooltip("반짝이 진하기 (시안 0.9).")]
        [SerializeField] private float sparkleAlpha = 0.9f;
        [Tooltip("반짝이는 빠르기 — 최소 · 더하는 범위 (시안 2 + 0 ~ 2).")]
        [SerializeField] private Vector2 twinkleSpeed = new Vector2(2f, 2f);
        [Tooltip("반짝이 안쪽 꼭짓점 반지름 비율 (시안 0.18).")]
        [SerializeField] private float sparkleWaist = 0.18f;

        [Header("쓴 쪽 강아지 (시안: (660, 880) 높이 760, 씹을 때 sx 1 + 0.05 · sy 1 − 0.07, 기울기 0.02·sin(3t))")]
        [Tooltip("씹을 때 가로로 커지는 만큼 (시안 0.05).")]
        [SerializeField] private float chompWiden = 0.05f;
        [Tooltip("씹을 때 세로로 줄어드는 만큼 (시안 0.07).")]
        [SerializeField] private float chompSquash = 0.07f;
        [Tooltip("씹고 나서 이 시간 안에만 씹는다 (초, 시안 0.3).")]
        [SerializeField] private float chompWindow = 0.3f;
        [Tooltip("씹음이 잦아드는 빠르기 (시안 exp(−q·14)).")]
        [SerializeField] private float chompDamping = 14f;
        [Tooltip("몸 흔들기 폭 · 빠르기 (라디안 · 시안 0.02 · 3).")]
        [SerializeField] private Vector2 sway = new Vector2(0.02f, 3f);

        [Header("눈 반짝 (강아지 그림 좌표, 시안 sparkle(눈 + (12, −22), 48 + 6·sin(9t), 1.5t, .95) + 흰 점 둘)")]
        [Tooltip("반짝이 자리 — 눈에서 (그림 px, 시안 12, −22).")]
        [SerializeField] private Vector2 eyeStarOffset = new Vector2(12f, -22f);
        [Tooltip("반짝이 크기 · 떨림 폭 (그림 px, 시안 48 · 6).")]
        [SerializeField] private Vector2 eyeStarSize = new Vector2(48f, 6f);
        [Tooltip("반짝이 떨림 빠르기 · 도는 빠르기 (시안 9 · 1.5).")]
        [SerializeField] private Vector2 eyeStarSpeed = new Vector2(9f, 1.5f);
        [Tooltip("반짝이 진하기 (시안 0.95).")]
        [SerializeField] private float eyeStarAlpha = 0.95f;
        [Tooltip("큰 흰 점 — 눈에서 (그림 px, 시안 −18, 16) · 반지름 11.")]
        [SerializeField] private Vector3 eyeDotBig = new Vector3(-18f, 16f, 11f);
        [Tooltip("작은 흰 점 — 눈에서 (그림 px, 시안 20, 26) · 반지름 6.")]
        [SerializeField] private Vector3 eyeDotSmall = new Vector3(20f, 26f, 6f);

        [Header("군침 (강아지 그림 좌표, 시안: 입 오른쪽 (−4, +2) 에서 길이 6 → 78, 방울 반지름 10 → 24)")]
        [Tooltip("자라기 시작 · 걸리는 시간 (초, 시안 0.05 · 0.5).")]
        [SerializeField] private Vector2 droolGrow = new Vector2(0.05f, 0.5f);
        [Tooltip("길이 처음 · 끝 (그림 px, 시안 6 · 78).")]
        [SerializeField] private Vector2 droolLength = new Vector2(6f, 78f);
        [Tooltip("방울 반지름 처음 · 끝 (그림 px, 시안 10 · 24).")]
        [SerializeField] private Vector2 droolRadius = new Vector2(10f, 24f);
        [Tooltip("씹을 때 줄어드는 비율 (시안 0.3).")]
        [SerializeField] private float droolChomp = 0.3f;
        [Tooltip("길이 출렁 폭 · 빠르기 (그림 px, 시안 5 · 6).")]
        [SerializeField] private Vector2 droolBob = new Vector2(5f, 6f);
        [Tooltip("방울 흔들림 폭 · 빠르기 (그림 px, 시안 6 · 4).")]
        [SerializeField] private Vector2 droolSwing = new Vector2(6f, 4f);
        [Tooltip("입에서 어긋남 (그림 px, 시안 −4, 2).")]
        [SerializeField] private Vector2 droolMouthOffset = new Vector2(-4f, 2f);
        [Tooltip("군침 색 (시안 rgba(205, 236, 255, .82)).")]
        [SerializeField] private Color droolColor = new Color32(0xcd, 0xec, 0xff, 0xd1);
        [Tooltip("군침 테 두께 (그림 px, 시안 6).")]
        [SerializeField] private float droolLine = 6f;

        [Header("상대 얼굴 창 (시안: (170 + 170i, 120 + 16i), back((t − 0.08 − 0.06i) / 0.25, 1.8))")]
        [Tooltip("첫 창 자리 · 다음 창까지 (설계 px, 시안 (170, 120) · (170, 16)) — 폰 가로 화면은 판 위가 잘려(설계 약 97 부터 보임) 창 · 「-1」 표가 들어오게 내렸다.")]
        [SerializeField] private Vector4 badgeLayout = new Vector4(170f, 175f, 170f, 16f);
        [Tooltip("튀어나오기 시작 · 창마다 늦게 · 걸리는 시간 (초, 시안 0.08 · 0.06 · 0.25).")]
        [SerializeField] private Vector3 badgePop = new Vector3(0.08f, 0.06f, 0.25f);
        [Tooltip("튀어나올 때 지나치는 정도 (시안 back 1.8).")]
        [SerializeField] private float badgeOvershoot = 1.8f;
        [Tooltip("얼굴 칸 — 캐릭터 그림 안 비율 (왼쪽 아래 0, 그림 chara 의 chara_face 칸). 자리별 얼굴 그림이 생기기 전까지.")]
        [SerializeField] private Rect faceArea = new Rect(0.0187f, 0.3479f, 0.825f, 0.6371f);
        [Tooltip("얼굴 그림이 없을 때 색 (시안 #bbb).")]
        [SerializeField] private Color missingFace = new Color32(0xbb, 0xbb, 0xbb, 0xff);
        [Tooltip("땀이 나는 때 — 그 상대 카드를 삼키기 이만큼 전 (초, 시안 0.05). 시안 sweat 가 진하기를 1 로 덮어써 바로 다 보인다.")]
        [SerializeField] private float sweatLead = 0.05f;

        [Header("카드 (시안: 창 + (8, 96) 에서 너비 74, 삼키기 0.3 초 전부터 입으로 — ei · 위로 120 · 크기 1 → 0.35 · 6 바퀴)")]
        [Tooltip("창에서 카드까지 (설계 px, 시안 8, 96).")]
        [SerializeField] private Vector2 cardOffset = new Vector2(8f, 96f);
        [Tooltip("카드 너비 (설계 px, 시안 74).")]
        [SerializeField] private float cardWidth = 74f;
        [Tooltip("기다리는 카드 기울기 — 첫 장 · 장마다 더함 (라디안, 시안 −0.12 · 0.1).")]
        [SerializeField] private Vector2 cardTilt = new Vector2(-0.12f, 0.1f);
        [Tooltip("기다리는 카드 출렁 폭 · 빠르기 (설계 px, 시안 4 · 4).")]
        [SerializeField] private Vector2 cardBob = new Vector2(4f, 4f);
        [Tooltip("날아가는 시간 (초, 시안 0.3).")]
        [SerializeField] private float flyTime = 0.3f;
        [Tooltip("날아오르는 높이 (설계 px, 시안 120).")]
        [SerializeField] private float flyArc = 120f;
        [Tooltip("입에 닿을 때 크기 (시안 0.35).")]
        [SerializeField] private float flyShrink = 0.35f;
        [Tooltip("날며 도는 빠르기 (라디안 / 진행, 시안 6).")]
        [SerializeField] private float flySpin = 6f;

        [Header("부스러기 (시안: 삼키고 0.45 초 — 10 조각, 위로 140 ~ 360 · 아래로 420τ²)")]
        [Tooltip("부스러기가 사는 시간 (초, 시안 0.45).")]
        [SerializeField] private float crumbLife = 0.45f;
        [Tooltip("조각 수 (시안 10).")]
        [SerializeField] private int crumbCount = 10;
        [Tooltip("부스러기 씨앗 — 카드 i 의 씨앗은 시작값 + i × 간격 (시안 i·7 + 1 → 시작 1 · 간격 7).")]
        [SerializeField] private Vector2Int crumbSeed = new Vector2Int(1, 7);
        [Tooltip("튀는 빠르기 — 최소 · 더하는 범위 (설계 px/초, 시안 140 + 0 ~ 220).")]
        [SerializeField] private Vector2 crumbSpeed = new Vector2(140f, 220f);
        [Tooltip("떨어지는 세기 (시안 420τ²).")]
        [SerializeField] private float crumbFall = 420f;
        [Tooltip("도는 빠르기 (라디안/초, 시안 9).")]
        [SerializeField] private float crumbSpin = 9f;
        [Tooltip("조각 모양 — 조각 가운데 기준 (설계 px, 시안 fillRect(−7, −4, 14, 9)).")]
        [SerializeField] private Rect crumbShape = new Rect(-7f, -4f, 14f, 9f);
        [Tooltip("조각 테 두께 (설계 px, 시안 2).")]
        [SerializeField] private float crumbLine = 2f;
        [Tooltip("조각 색 — 돌아가며 (시안 #6b8cff · #fff · #ffd23f).")]
        [SerializeField] private Color[] crumbColors = { new Color32(0x6b, 0x8c, 0xff, 0xff), Color.white, new Color32(0xff, 0xd2, 0x3f, 0xff) };

        [Header("글자")]
        [Tooltip("「냠!」 — 입에서 (설계 px, 시안 215, −120).")]
        [SerializeField] private Vector2 yumOffset = new Vector2(215f, -120f);
        [Tooltip("「냠!」 이 찍히는 시간 (초, 시안 0.14).")]
        [SerializeField] private float yumTime = 0.14f;
        [Tooltip("씹음이 이보다 크면 「냠!」 을 그린다 (시안 0.05).")]
        [SerializeField] private float yumThreshold = 0.05f;
        [Tooltip("「+N」 이 찍히는 시간 (초, 시안 0.18).")]
        [SerializeField] private float countTime = 0.18f;

        private int _rivalCount;
        private float _time;
        private float _chomp;
        private float _droolGrowth;
        private Vector2 _mouth;

        private void Awake()
        {
            if (glow == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(glow)} 가 비었다");
            if (focusLines == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(focusLines)} 가 비었다");
            if (sparkles == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(sparkles)} 가 비었다");
            if (eater == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(eater)} 가 비었다");
            if (eaterFace == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(eaterFace)} 가 비었다");
            if (crumbs == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(crumbs)} 가 비었다");
            if (yum == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(yum)} 가 비었다");
            if (count == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(count)} 가 비었다");
            if (countStamp == null)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(countStamp)} 가 비었다");
            if (chompAt.Length < rivals.Length)
                Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(chompAt)} 가 {chompAt.Length} 개다 — 상대 수({rivals.Length})만큼 있어야 한다");
            for (int i = 0; i < rivals.Length; i++)
            {
                Rival rival = rivals[i];
                if (rival == null || rival.root == null || rival.badge == null || rival.face == null
                    || rival.lostTag == null || rival.sweat == null || rival.card == null)
                {
                    Debug.LogError($"[{nameof(DroolComicScene)}] {name} 의 {nameof(rivals)}[{i}] 에 빈 참조가 있다");
                    continue;
                }
            }
        }

        public override void Begin(ComicContext context)
        {
            eater.Key = context.DogKeyOf(context.From);
            glow.Painter = PaintGlow;
            sparkles.Painter = PaintSparkles;
            eaterFace.Painter = PaintEaterFace;
            crumbs.Painter = PaintCrumbs;
            glow.Redraw();

            // 시안은 늘 셋 — 낸 사람이 아닌 사람 수만큼 (모르면 셋)
            int most = Mathf.Min(rivals.Length, chompAt.Length), others = context.Others;
            _rivalCount = others == 0 ? most : Mathf.Min(others, most);
            Sprite character = context.Fx.Character;
            for (int i = 0; i < rivals.Length; i++)
            {
                Rival rival = rivals[i];
                bool shown = i < _rivalCount;
                rival.root.SetActive(shown);
                if (shown)
                    ComicKit.ShowFace(rival.face, character, faceArea, missingFace);
            }
        }

        public override void Draw(float t, ComicContext context)
        {
            _time = t;
            _chomp = 0f;
            for (int i = 0; i < _rivalCount; i++)
            {
                float q = t - chompAt[i];
                if (q > 0f && q < chompWindow)
                    _chomp = Mathf.Max(_chomp, Mathf.Exp(-q * chompDamping));
            }
            _droolGrowth = ComicKit.Sm((t - droolGrow.x) / droolGrow.y);

            focusLines.SetTime(t);
            sparkles.Redraw();

            eater.SetTime(t);
            eater.ScaleX = 1f + chompWiden * _chomp;
            eater.ScaleY = 1f - chompSquash * _chomp;
            eater.Rotation = sway.x * Mathf.Sin(t * sway.y);
            eaterFace.Redraw();
            _mouth = eater.PointOf(FxDog.Mouth);

            for (int i = 0; i < _rivalCount; i++)
                DrawRival(i, t);

            crumbs.Redraw();
            DrawWords(t);
        }

        // 시안 sparkle(x, y, r, rot, a, col): 뾰족한 네 꼭짓점 별 (globalAlpha 를 a 로 둔다)
        private void Sparkle(ComicPen pen, float x, float y, float r, float rotation, float alpha, Color color)
        {
            if (alpha <= 0f || r <= 0f)
                return;
            pen.Save();
            pen.Translate(x, y);
            pen.Rotate(rotation);
            pen.Alpha = alpha;
            pen.BeginPath();
            for (int i = 0; i < SparklePoints; i++)
            {
                float angle = (float)i / SparklePoints * ComicKit.Tau, radius = i % 2 == 1 ? r * sparkleWaist : r;
                pen.LineTo(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            pen.ClosePath();
            pen.Fill(color);
            pen.Restore();
        }

        private void DrawRival(int i, float t)
        {
            Rival rival = rivals[i];
            float ax = badgeLayout.x + i * badgeLayout.z, ay = badgeLayout.y + i * badgeLayout.w;
            float e = ComicKit.Back((t - badgePop.x - i * badgePop.y) / badgePop.z, badgeOvershoot);
            rival.badge.gameObject.SetActive(e > 0f);
            if (e <= 0f)
            {
                rival.card.gameObject.SetActive(false);
                return;
            }
            ComicKit.Place(rival.badge, ax, ay);
            rival.badge.localScale = new Vector3(e, e, 1f);
            float at = chompAt[i];
            rival.lostTag.SetActive(t > at - flyTime);
            ComicKit.SetAlpha(rival.sweat, t > at - sweatLead ? 1f : 0f);

            float start = at - flyTime, q = (t - start) / flyTime;
            float sx = ax + cardOffset.x, sy = ay + cardOffset.y;
            bool flying = q >= 0f && q < 1f;
            rival.card.gameObject.SetActive(q < 1f);
            if (q < 0f)
            {
                PlaceCard(rival.card, sx, sy + Mathf.Sin(t * cardBob.y + i) * cardBob.x, cardWidth, cardTilt.x + i * cardTilt.y);
                return;
            }
            if (!flying)
                return;
            PlaceFlying(rival.card, sx, sy, q);
        }

        private void PlaceFlying(ComicCard card, float sx, float sy, float q)
        {
            float e = ComicKit.Ei(q);
            float x = ComicKit.Lerp(sx, _mouth.x, e), y = ComicKit.Lerp(sy, _mouth.y, e) - Mathf.Sin(q * Mathf.PI) * flyArc;
            PlaceCard(card, x, y, cardWidth * ComicKit.Lerp(1f, flyShrink, e), q * flySpin);
        }

        private static void PlaceCard(ComicCard card, float x, float y, float width, float angle)
        {
            var rect = (RectTransform)card.transform;
            ComicKit.Place(rect, x, y);
            ComicKit.Turn(rect, angle);
            card.Width = width;
        }

        private void DrawWords(float t)
        {
            // 「냠!」 — 가장 최근에 삼킨 카드부터 잰다
            float z = 0f;
            for (int i = 0; i < _rivalCount; i++)
            {
                if (t - chompAt[i] > 0f)
                    z = t - chompAt[i];
            }
            if (_chomp > yumThreshold)
            {
                yum.RestPosition = _mouth + yumOffset;
                yum.Apply(z / yumTime);
            }
            else
            {
                yum.Apply(0f);
            }

            int got = 0;
            for (int i = 0; i < _rivalCount; i++)
            {
                if (t >= chompAt[i])
                    got++;
            }
            if (got == 0)
            {
                countStamp.Apply(0f);
                return;
            }
            count.Text = string.Format(CountFormat, got);
            countStamp.Apply((t - chompAt[got - 1]) / countTime);
        }

        // 시안 glow — 더하기로 그린 둥근 빛
        private void PaintGlow(ComicPen pen)
        {
            Color rim = glowColor;
            rim.a = 0f;
            pen.FillRadial(glowAt.x, glowAt.y, glowRadius, glowColor, rim);
        }

        private void PaintSparkles(ComicPen pen)
        {
            for (int i = 0; i < sparkleCount; i++)
            {
                var random = new ComicRandom(sparkleSeed + i);
                float x = sparkleArea.x + random.Next() * sparkleArea.width, y = sparkleArea.y + random.Next() * sparkleArea.height;
                float speed = twinkleSpeed.x + random.Next() * twinkleSpeed.y;
                float k = Mathf.Max(0f, Mathf.Sin(_time * speed + random.Next() * SparklePhase));
                float size = (sparkleSize.x + random.Next() * sparkleSize.y) * k;
                Sparkle(pen, x, y, size, _time + random.Next() * SparklePhase, sparkleAlpha * k, Color.white);
            }
        }

        // 시안 dog 의 over: 강아지 그림 좌표로 눈 반짝 · 군침
        private void PaintEaterFace(ComicPen pen)
        {
            Vector2 ground = ComicKit.PositionOf((RectTransform)eater.transform);
            pen.Translate(ground.x, ground.y);
            eater.ApplyBodySpace(pen);

            foreach (Vector2 eye in Eyes)
            {
                float r = eyeStarSize.x + eyeStarSize.y * Mathf.Sin(_time * eyeStarSpeed.x);
                Sparkle(pen, eye.x + eyeStarOffset.x, eye.y + eyeStarOffset.y, r, _time * eyeStarSpeed.y, eyeStarAlpha, Color.white);
                pen.BeginPath();
                pen.Arc(eye.x + eyeDotBig.x, eye.y + eyeDotBig.y, eyeDotBig.z, 0f, ComicKit.Tau);
                pen.Fill(Color.white);
                pen.BeginPath();
                pen.Arc(eye.x + eyeDotSmall.x, eye.y + eyeDotSmall.y, eyeDotSmall.z, 0f, ComicKit.Tau);
                pen.Fill(Color.white);
            }

            float length = ComicKit.Lerp(droolLength.x, droolLength.y, _droolGrowth) * (1f - droolChomp * _chomp) + droolBob.x * Mathf.Sin(_time * droolBob.y);
            float mx = FxDog.MouthRight.x + droolMouthOffset.x, my = FxDog.MouthRight.y + droolMouthOffset.y;
            float r0 = ComicKit.Lerp(droolRadius.x, droolRadius.y, _droolGrowth), swing = droolSwing.x * Mathf.Sin(_time * droolSwing.y);
            float dropY = my + length + r0 * DropCenter;
            pen.BeginPath();
            pen.MoveTo(mx + LipLeft, my + LipY);
            pen.BezierCurveTo(mx + LeftPull, my + length * LeftPullY, mx + LeftBend + swing - r0, my + length * LeftBendY, mx + swing - r0, dropY);
            pen.Arc(mx + swing, dropY, r0, Mathf.PI, 0f, true);
            pen.BezierCurveTo(mx + swing + r0, my + length * RightPullY, mx + RightBend, my + length * RightBendY, mx + LipRight, my + LipY);
            pen.ClosePath();
            pen.Fill(droolColor);
            pen.Stroke(ComicKit.Ink, droolLine);
            pen.BeginPath();
            pen.Ellipse(mx + swing + r0 * ShineX, my + length + r0 * ShineY, r0 * ShineRadiusX, r0 * ShineRadiusY, ShineAngle, 0f, ComicKit.Tau);
            pen.Fill(Color.white);
            pen.BeginPath();
            pen.Arc(mx + swing + r0 * DotX, my + length + r0 * DotY, r0 * DotRadius, 0f, ComicKit.Tau);
            pen.Fill(Color.white);
        }

        private void PaintCrumbs(ComicPen pen)
        {
            for (int i = 0; i < _rivalCount; i++)
            {
                float z = _time - chompAt[i];
                if (z <= 0f || z > crumbLife)
                    continue;
                var random = new ComicRandom(crumbSeed.x + i * crumbSeed.y);
                for (int j = 0; j < crumbCount; j++)
                {
                    float angle = -Mathf.PI * random.Next(), speed = crumbSpeed.x + random.Next() * crumbSpeed.y;
                    pen.Save();
                    pen.Translate(_mouth.x + Mathf.Cos(angle) * speed * z, _mouth.y + Mathf.Sin(angle) * speed * z + crumbFall * z * z);
                    pen.Rotate(z * crumbSpin + j);
                    pen.Alpha = 1f - z / crumbLife;
                    pen.BeginPath();
                    pen.Rect(crumbShape.x, crumbShape.y, crumbShape.width, crumbShape.height);
                    pen.Fill(crumbColors[j % crumbColors.Length]);
                    pen.Stroke(ComicKit.Ink, crumbLine);
                    pen.Restore();
                }
            }
        }
    }
}
