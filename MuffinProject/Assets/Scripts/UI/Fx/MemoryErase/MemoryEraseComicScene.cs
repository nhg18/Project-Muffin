using System;
using Chapchu.UI.Fx.Comic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.MemoryErase
{
    /// <summary>
    /// 기억 삭제(카드 1013) 만화 컷인 장면 — 불빛 한 번에 다들 손패를 버리고 새로 다섯 장 (시안 CI2.SC["기억 삭제"]).
    /// 어두운 회색 판(옅은 세로줄)에 검은 양복 강아지(카드 그림 스티커)가 기억 지우개를 들고 튀어 들어온다 → 0.36 초부터 렌즈가 반짝 →
    /// 0.56 초 그림 속 불빛이 켜지며 화면이 한 번 하얗게 끊기고, 그림 뒤로 단색 빛줄기 · 앞으로 고리가 퍼진다 → 나머지 사람들(얼굴 원 + 손패 다섯 장)이 어두워지며 어지럽고,
    /// 손패가 작아지며 빛가루로 흩어진 뒤 새 다섯 장이 위에서 떨어진다.
    /// 시안의 반투명 번쩍 · 둥근 빛 · 흐려지는 줄기 · 고리 · 빛가루는 회보라 얼룩으로 오래 남아, 진하기는 그대로 두고 크기 · 굵기를 줄여 없앤다.
    /// 4 단계 장식은 없다 (시안 FXA 기억 삭제 s4 = 손패 옮기기뿐 — 게임 화면 몫).
    /// 연출 길이 2.56초 (3 단계) — 카드 처리 시간(resolveSeconds)에 3 단계 실측 길이를 더해 두었다 (10-ui.md 5-1). 길이를 바꾸면 그 값도 같이 바꾼다.
    /// 얼굴 그림은 모두 <see cref="CardFxContext.Character"/> 의 얼굴 칸 — 자리마다 얼굴 그림이 생기면 그것으로 바꾼다.
    /// 기준 문서: docs/systems/11-card-list.md 1013 기억 삭제 · docs/systems/10-ui.md 5-1 (3 · 4 단계 연출).
    /// </summary>
    public class MemoryEraseComicScene : ComicScene
    {
        private const float StripeMargin = 500f;

        /// <summary>나머지 사람 한 명의 그림 — 얼굴 원 · 손패 · 빛가루.</summary>
        [Serializable]
        public class Person
        {
            [Tooltip("얼굴 원 — 가운데가 피벗 (시안 avatar).")]
            public RectTransform face;
            [Tooltip("얼굴 그림 (얼굴 원 마스크 안).")]
            public RawImage picture;
            [Tooltip("얼굴을 어둡게 덮는 판 (얼굴 원 마스크 안, 색 = 시안 rgba(20, 10, 40)).")]
            public Graphic shade;
            [Tooltip("어지러움 소용돌이 (장면이 그린다) — 얼굴 가운데.")]
            public ComicSketch dizzy;
            [Tooltip("원래 손패 다섯 장 (왼쪽부터) — 흐려지며 흩어진다.")]
            public CanvasGroup[] oldHand = new CanvasGroup[5];
            [Tooltip("새 손패 다섯 장 (왼쪽부터) — 위에서 떨어진다.")]
            public ComicCard[] newHand = new ComicCard[5];
            [Tooltip("흩어지는 빛가루 (장면이 그린다, FxAdditive).")]
            public ComicSketch dust;
        }

        [Header("그림 (그리는 순서)")]
        [Tooltip("배경의 옅은 세로줄 (장면이 그린다).")]
        [FormerlySerializedAs("backgroundLights")]
        [SerializeField] private ComicSketch backgroundStripes;
        [Tooltip("쓰는 쪽 — 불 꺼진 카드 그림 스티커 (시안 E02N).")]
        [SerializeField] private ComicSticker agent;
        [Tooltip("렌즈 반짝 (장면이 그린다, 색 = 시안 #eaf6ff).")]
        [SerializeField] private ComicSketch lensSparkle;
        [Tooltip("불 켜진 카드 그림 — 스티커 위에 겹친다 (시안 SPA.e02).")]
        [SerializeField] private Image litArt;
        [Tooltip("빛줄기 (장면이 그린다) — 쓰는 쪽 그림 뒤.")]
        [SerializeField] private ComicSketch flashLight;
        [Tooltip("퍼지는 고리 둘 (색 = 시안 #ecf7ff).")]
        [SerializeField] private UIRing[] flashRings = new UIRing[2];
        [Tooltip("나머지 사람들 (위부터, 시안 최대 3 명).")]
        [SerializeField] private Person[] people = new Person[3];
        [Tooltip("화면 번쩍 (색 = 흰색, 알파는 장면이 0 · 1 로).")]
        [SerializeField] private Graphic whiteOut;

        [Header("배경 세로줄 (시안: x = −40 부터 46 마다 폭 3, 흰색 진하기 0.06)")]
        [Tooltip("첫 줄 x (설계 px, 시안 −40).")]
        [SerializeField] private float stripeStart = -40f;
        [Tooltip("줄 간격 (설계 px, 시안 46).")]
        [SerializeField] private float stripeGap = 46f;
        [Tooltip("줄 폭 (설계 px, 시안 3).")]
        [SerializeField] private float stripeWidth = 3f;
        [Tooltip("줄 색 (알파 = 진하기, 시안 흰색 0.06).")]
        [SerializeField] private Color stripeColor = new Color(1f, 1f, 1f, 0.06f);

        [Header("쓰는 쪽 (시안: 0.26 초에 −200 → 470 으로 튀어 들어온다, back 1.6)")]
        [Tooltip("튀어 들어오는 시간 (초, 시안 0.26).")]
        [SerializeField] private float enterTime = 0.26f;
        [Tooltip("튀어 들어올 때 지나치는 정도 (시안 back 1.6).")]
        [SerializeField] private float enterOvershoot = 1.6f;
        [Tooltip("들어오기 전 x (설계 px, 시안 −200).")]
        [SerializeField] private float agentFromX = -200f;
        [Tooltip("자리 x (설계 px, 시안 470).")]
        [SerializeField] private float agentX = 470f;
        [Tooltip("자리 y — 스티커 기준점 (설계 px, 시안 760).")]
        [SerializeField] private float agentY = 760f;
        [Tooltip("스티커 기준점 — 그림 px (시안 ax 340 · ay 640).")]
        [SerializeField] private Vector2 stickerAnchor = new Vector2(340f, 640f);
        [Tooltip("그림 배율 (시안 그림 640 을 높이 600 으로).")]
        [SerializeField] private float artScale = 600f / 640f;
        [Tooltip("불빛 가운데 — 그림 px (시안 L = 130, 325).")]
        [SerializeField] private Vector2 lightInArt = new Vector2(130f, 325f);
        [Tooltip("렌즈 — 그림 px (시안 LN = 167, 292).")]
        [SerializeField] private Vector2 lensInArt = new Vector2(167f, 292f);

        [Header("렌즈 반짝 (켜기 직전)")]
        [Tooltip("반짝이기 시작하는 때 (초, 시안 0.36).")]
        [SerializeField] private float sparkleAt = 0.36f;
        [Tooltip("커지는 시간 (초, 시안 0.2).")]
        [SerializeField] private float sparkleTime = 0.2f;
        [Tooltip("반지름 — 처음 · 끝 (설계 px, 시안 6 → 30).")]
        [SerializeField] private Vector2 sparkleRadius = new Vector2(6f, 30f);
        [Tooltip("도는 빠르기 (라디안/초, 시안 t·6).")]
        [SerializeField] private float sparkleSpin = 6f;
        [Tooltip("반짝이 꼭짓점 수 — 바깥 · 안쪽을 번갈아 (시안 8).")]
        [SerializeField] private int sparkleCorners = 8;
        [Tooltip("안쪽 꼭짓점 반지름 비율 (시안 0.18).")]
        [SerializeField] private float sparkleInner = 0.18f;

        [Header("불이 켜진다")]
        [Tooltip("불이 켜지는 때 (초, 시안 0.56).")]
        [SerializeField] private float flashAt = 0.56f;
        [Tooltip("켜진 그림이 보이는 동안 — 그 뒤 딱 꺼진다 (초). 시안은 0.08 초 뒤 exp(−·3.2) 로 흐려져 그림 속 보라 빛이 1 초 넘게 얼룩으로 남았다.")]
        [SerializeField] private float litHold = 0.3f;

        [Header("빛줄기 (시안: 12 갈래, 길이 120 → 760 eo(τ/0.4), 반각 0.035 + 0.02·(i%2)) — 단색, 가늘어져 사라진다")]
        [Tooltip("빛줄기가 다 뻗는 시간 (초, 시안 0.4).")]
        [FormerlySerializedAs("glowGrowTime")]
        [SerializeField] private float rayGrowTime = 0.4f;
        [Tooltip("빛줄기가 가늘어져 사라지기까지 (초).")]
        [SerializeField] private float rayLife = 0.6f;
        [Tooltip("빛줄기 수 (시안 12).")]
        [SerializeField] private int rayCount = 12;
        [Tooltip("도는 빠르기 (라디안/초, 시안 τ·0.6).")]
        [SerializeField] private float raySpin = 0.6f;
        [Tooltip("길이 — 처음 · 끝 (설계 px, 시안 120 → 760).")]
        [SerializeField] private Vector2 rayLength = new Vector2(120f, 760f);
        [Tooltip("반각 (라디안, 시안 0.035).")]
        [SerializeField] private float rayHalfWidth = 0.035f;
        [Tooltip("하나 걸러 더하는 반각 (라디안, 시안 0.02).")]
        [SerializeField] private float rayHalfWidthStep = 0.02f;
        [Tooltip("빛줄기 색 (시안 236,248,255).")]
        [SerializeField] private Color rayColor = new Color(236f / 255f, 248f / 255f, 1f, 1f);

        [Header("고리 (시안: 0.12 초 간격 둘, 0.5 초 동안 반지름 60 → 620) — 진하기 그대로, 굵기가 0 으로 줄어 사라진다")]
        [Tooltip("고리 사이 (초, 시안 0.12).")]
        [SerializeField] private float ringGap = 0.12f;
        [Tooltip("고리 하나가 사는 시간 (초, 시안 0.5 — 크게 퍼진 뒤 가는 선으로 오래 남아 줄였다).")]
        [SerializeField] private float ringLife = 0.36f;
        [Tooltip("반지름 — 처음 · 끝 (설계 px, 시안 60 → 620).")]
        [SerializeField] private Vector2 ringRadius = new Vector2(60f, 620f);
        [Tooltip("처음 굵기 — 0 으로 줄어든다 (설계 px, 시안 6 — 가는 선이 흠집처럼 보여 굵혔다).")]
        [SerializeField] private float ringWidth = 16f;

        [Header("화면 번쩍 — 시안은 0.34 초 동안 반투명으로 흐려져 회색 막이 남아, 짧게 한 번 하얗게 끊는다")]
        [Tooltip("온 화면이 하얀 동안 (초).")]
        [SerializeField] private float whiteOutTime = 0.08f;

        [Header("나머지 사람들 (시안: x 880 + (i%2)·150, y 150 + i·175)")]
        [Tooltip("첫 사람 얼굴 가운데 (설계 px, 시안 880, 150 — 폰 가로 화면은 판 위가 잘려(설계 약 97 부터 보임) 얼굴이 들어오게 내렸다).")]
        [SerializeField] private Vector2 peopleOrigin = new Vector2(880f, 165f);
        [Tooltip("하나 걸러 오른쪽으로 · 한 사람마다 아래로 (설계 px, 시안 150 · 175).")]
        [SerializeField] private Vector2 peopleStep = new Vector2(150f, 175f);
        [Tooltip("얼굴 칸 — 캐릭터 그림 안 비율 (왼쪽 아래 0, 그림 chara 의 chara_face 칸). 자리별 얼굴 그림이 생기기 전까지.")]
        [SerializeField] private Rect faceArea = new Rect(0.0187f, 0.3479f, 0.825f, 0.6371f);
        [Tooltip("얼굴 그림이 없을 때 색 (시안 #bbb).")]
        [SerializeField] private Color missingFace = new Color32(0xbb, 0xbb, 0xbb, 0xff);
        [Tooltip("불이 켜진 뒤 얼굴을 덮는 진하기 (시안 dark 0.15).")]
        [SerializeField] private float shadeAlpha = 0.15f;

        [Header("어지러움 (시안 avatar dizzy: 두 소용돌이 ±0.38r, −0.05r, 2.2 바퀴, 반지름 0.3r, 흰 선 3)")]
        [Tooltip("얼굴 반지름 (설계 px, 시안 56) — 얼굴 원 프리팹 크기와 같게.")]
        [SerializeField] private float faceRadius = 56f;
        [Tooltip("소용돌이 가운데 — 얼굴 반지름 비율 (시안 ±0.38, −0.05).")]
        [SerializeField] private Vector2 dizzyCenter = new Vector2(0.38f, -0.05f);
        [Tooltip("소용돌이 바퀴 수 (시안 2.2).")]
        [SerializeField] private float dizzyTurns = 2.2f;
        [Tooltip("소용돌이 점 간격 (라디안, 시안 0.25).")]
        [SerializeField] private float dizzyStep = 0.25f;
        [Tooltip("소용돌이 반지름 — 얼굴 반지름 비율 (시안 0.3).")]
        [SerializeField] private float dizzyRadius = 0.3f;
        [Tooltip("소용돌이 도는 빠르기 (시안 t·8).")]
        [SerializeField] private float dizzySpin = 8f;
        [Tooltip("소용돌이 선 굵기 (설계 px, 시안 3).")]
        [SerializeField] private float dizzyWidth = 3f;
        [Tooltip("소용돌이 선 색 (시안 #fff).")]
        [SerializeField] private Color dizzyColor = Color.white;

        [Header("손패 (시안: 얼굴 + (90 + u·22, 44 + |u|·4), 기울기 u·0.16, u = j − 2)")]
        [Tooltip("가운데 카드 — 얼굴 가운데에서 (설계 px, 시안 90, 44).")]
        [SerializeField] private Vector2 handOffset = new Vector2(90f, 44f);
        [Tooltip("카드 사이 x (설계 px, 시안 22).")]
        [SerializeField] private float handSpread = 22f;
        [Tooltip("가운데에서 하나 멀어질 때 내려가는 만큼 (설계 px, 시안 4).")]
        [SerializeField] private float handArch = 4f;
        [Tooltip("카드 사이 기울기 (라디안, 시안 0.16).")]
        [SerializeField] private float handFan = 0.16f;
        [Tooltip("손패가 흩어지기 시작하는 때 — 불 켜지고 나서 (초, 시안 0.15).")]
        [SerializeField] private float scatterAt = 0.15f;
        [Tooltip("손패가 다 사라지는 시간 (초, 시안 0.25).")]
        [SerializeField] private float scatterTime = 0.25f;
        [Tooltip("흩어지는 만큼 — 카드마다 옆으로 · 위로 (설계 px, 시안 40 · 60).")]
        [SerializeField] private Vector2 scatter = new Vector2(40f, 60f);
        [Tooltip("새 카드가 떨어지기 시작하는 때 — 불 켜지고 나서 (초, 시안 0.4).")]
        [SerializeField] private float dealAt = 0.4f;
        [Tooltip("새 카드 사이 (초, 시안 0.05).")]
        [SerializeField] private float dealGap = 0.05f;
        [Tooltip("새 카드가 떨어지는 시간 (초, 시안 0.2).")]
        [SerializeField] private float dealTime = 0.2f;
        [Tooltip("새 카드가 떨어지기 시작하는 높이 — 얼굴 가운데에서 (설계 px, 시안 −160).")]
        [SerializeField] private float dealFrom = -160f;
        [Tooltip("떨어질 때 back 세기 (시안 1.6, 1 에서 자른다).")]
        [SerializeField] private float dealOvershoot = 1.6f;

        [Header("빛가루 (시안: rng(i·31) 로 16 개, 거리 (τ − 0.15)·260·R, 반지름 8) — 단색 점, 남은 손패만큼 작아진다")]
        [Tooltip("사람마다 씨앗 배수 (시안 i·31).")]
        [SerializeField] private int dustSeedStep = 31;
        [Tooltip("빛가루 수 (시안 16).")]
        [SerializeField] private int dustCount = 16;
        [Tooltip("퍼지는 빠르기 (설계 px/초, 시안 260).")]
        [SerializeField] private float dustSpeed = 260f;
        [Tooltip("빛가루 처음 반지름 (설계 px, 시안 8).")]
        [SerializeField] private float dustRadius = 8f;
        [Tooltip("빛가루 색 (시안 200,200,255).")]
        [SerializeField] private Color dustColor = new Color(215f / 255f, 242f / 255f, 1f, 1f);

        private int _count;
        private float _flash;
        private float _remain;
        private float _dizzyPhase;
        private float _sparkleSize;
        private Vector2 _light;

        private void Awake()
        {
            if (backgroundStripes == null)
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(backgroundStripes)} 가 비었다");
            if (agent == null)
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(agent)} 가 비었다");
            if (lensSparkle == null)
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(lensSparkle)} 가 비었다");
            if (litArt == null)
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(litArt)} 가 비었다");
            if (flashLight == null)
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(flashLight)} 가 비었다");
            if (whiteOut == null)
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(whiteOut)} 가 비었다");
            for (int i = 0; i < flashRings.Length; i++)
            {
                if (flashRings[i] == null)
                    Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(flashRings)}[{i}] 가 비었다");
            }
            for (int i = 0; i < people.Length; i++)
                CheckPerson(i);

            backgroundStripes.Painter = PaintBackgroundStripes;
            lensSparkle.Painter = PaintSparkle;
            flashLight.Painter = PaintFlashLight;
            for (int i = 0; i < people.Length; i++)
            {
                int index = i;
                people[i].dizzy.Painter = PaintDizzy;
                people[i].dust.Painter = pen => PaintDust(pen, index);
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
                person.dust.gameObject.SetActive(shown);
                foreach (CanvasGroup card in person.oldHand)
                    card.gameObject.SetActive(shown);
                if (shown)
                    ComicKit.ShowFace(person.picture, art, faceArea, missingFace);
            }
            backgroundStripes.Redraw();
        }

        public override void Draw(float t, ComicContext context)
        {
            _flash = t - flashAt;
            bool lit = _flash > 0f;

            // 쓰는 쪽 — 불 꺼진 그림이 튀어 들어온다
            float x = ComicKit.Lerp(agentFromX, agentX, ComicKit.Back(t / enterTime, enterOvershoot));
            ComicKit.Place((RectTransform)agent.transform, x, agentY);
            ComicKit.Place(litArt.rectTransform, x, agentY);
            _light = ArtPoint(x, lightInArt);

            // 켜기 직전: 렌즈가 반짝
            if (!lit && t > sparkleAt)
            {
                float q = ComicKit.Cl((t - sparkleAt) / sparkleTime);
                RectTransform sparkleRect = lensSparkle.rectTransform;
                Vector2 lens = ArtPoint(x, lensInArt);
                ComicKit.Place(sparkleRect, lens.x, lens.y);
                ComicKit.Turn(sparkleRect, t * sparkleSpin);
                _sparkleSize = ComicKit.Lerp(sparkleRadius.x, sparkleRadius.y, q);
                lensSparkle.Redraw();
                ComicKit.SetAlpha(lensSparkle, 1f);
            }
            else
            {
                ComicKit.SetAlpha(lensSparkle, 0f);
            }

            // 켜는 순간 그림 속 불빛이 켜졌다가 딱 꺼진다
            ComicKit.SetAlpha(litArt, lit && _flash < litHold ? 1f : 0f);

            // 번쩍: 그림 뒤로 빛줄기, 앞으로 고리
            flashLight.Redraw();
            for (int r = 0; r < flashRings.Length; r++)
                DrawRing(flashRings[r], (_flash - r * ringGap) / ringLife);

            // 나머지 모두: 멍해지고 → 손패가 흩어지고 → 새 다섯 장
            _remain = !lit ? 1f : 1f - ComicKit.Cl((_flash - scatterAt) / scatterTime);
            _dizzyPhase = lit ? t * dizzySpin : 0f;
            for (int i = 0; i < _count; i++)
                DrawPerson(people[i], i);

            ComicKit.SetAlpha(whiteOut, lit && _flash < whiteOutTime ? 1f : 0f);
        }

        private void CheckPerson(int i)
        {
            Person person = people[i];
            if (person == null || person.face == null || person.picture == null || person.shade == null || person.dizzy == null || person.dust == null)
            {
                Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(people)}[{i}] 그림(얼굴 · 얼굴 그림 · 덮개 · 어지러움 · 빛가루)이 비었다");
                return;
            }
            for (int j = 0; j < person.oldHand.Length; j++)
            {
                if (person.oldHand[j] == null)
                    Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(people)}[{i}].{nameof(Person.oldHand)}[{j}] 가 비었다");
            }
            for (int j = 0; j < person.newHand.Length; j++)
            {
                if (person.newHand[j] == null)
                    Debug.LogError($"[{nameof(MemoryEraseComicScene)}] {name} 의 {nameof(people)}[{i}].{nameof(Person.newHand)}[{j}] 가 비었다");
            }
        }

        private void DrawPerson(Person person, int index)
        {
            Vector2 face = PersonAt(index);
            ComicKit.Place(person.face, face.x, face.y);
            ComicKit.SetAlpha(person.shade, _flash > 0f ? shadeAlpha : 0f);
            person.dizzy.Redraw();

            bool scattering = _flash > scatterAt;
            for (int j = 0; j < person.oldHand.Length; j++)
            {
                float u = j - (person.oldHand.Length - 1) / 2f;
                CanvasGroup card = person.oldHand[j];
                card.alpha = _remain > 0f ? 1f : 0f;
                if (_remain <= 0f)
                    continue;
                float dx = scattering ? u * scatter.x * (1f - _remain) : 0f;
                float dy = scattering ? -scatter.y * (1f - _remain) : 0f;
                var rect = (RectTransform)card.transform;
                ComicKit.Place(rect, face.x + handOffset.x + u * handSpread + dx, face.y + handOffset.y + Mathf.Abs(u) * handArch + dy);
                ComicKit.Turn(rect, u * handFan);
                rect.localScale = new Vector3(_remain, _remain, 1f);
            }

            for (int j = 0; j < person.newHand.Length; j++)
            {
                float u = j - (person.newHand.Length - 1) / 2f;
                float q = (_flash - dealAt - j * dealGap) / dealTime;
                GameObject card = person.newHand[j].gameObject;
                bool dealt = q > 0f;
                if (card.activeSelf != dealt)
                    card.SetActive(dealt);
                if (!dealt)
                    continue;
                float e = Mathf.Min(1f, ComicKit.Back(q, dealOvershoot));
                float rest = face.y + handOffset.y + Mathf.Abs(u) * handArch;
                var rect = (RectTransform)card.transform;
                ComicKit.Place(rect, face.x + handOffset.x + u * handSpread, ComicKit.Lerp(face.y + dealFrom, rest, e));
                ComicKit.Turn(rect, u * handFan);
            }

            person.dust.Redraw();
        }

        private Vector2 PersonAt(int index)
        {
            return new Vector2(peopleOrigin.x + index % 2 * peopleStep.x, peopleOrigin.y + index * peopleStep.y);
        }

        // 그림 px → 설계 좌표 (스티커 자리 x 에서)
        private Vector2 ArtPoint(float x, Vector2 inArt)
        {
            return new Vector2(x + (inArt.x - stickerAnchor.x) * artScale, agentY + (inArt.y - stickerAnchor.y) * artScale);
        }

        private void DrawRing(UIRing ring, float q)
        {
            if (q <= 0f || q >= 1f)
            {
                ComicKit.SetAlpha(ring, 0f);
                return;
            }
            ComicKit.Place(ring.rectTransform, _light.x, _light.y);
            ring.Set(ComicKit.Lerp(ringRadius.x, ringRadius.y, ComicKit.Eo(q)), ringWidth * (1f - q));
            ComicKit.SetAlpha(ring, 1f);
        }

        private void PaintBackgroundStripes(ComicPen pen)
        {
            if (stripeGap <= 0f)
                return;
            // 시안: 화면 위아래 밖 500 까지 (흔들려도 끝이 보이지 않게)
            pen.BeginPath();
            for (float x = stripeStart; x < ComicKit.Width - stripeStart; x += stripeGap)
                pen.Rect(x, -StripeMargin, stripeWidth, ComicKit.Height + StripeMargin * 2f);
            pen.Fill(stripeColor);
        }

        // 시안 sparkle — 가는 네 갈래 별 (색 · 진하기 = Graphic.color)
        private void PaintSparkle(ComicPen pen)
        {
            if (_sparkleSize <= 0f)
                return;
            pen.BeginPath();
            for (int i = 0; i < sparkleCorners; i++)
            {
                float angle = (float)i / sparkleCorners * ComicKit.Tau;
                float r = i % 2 == 1 ? _sparkleSize * sparkleInner : _sparkleSize;
                pen.LineTo(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            }
            pen.ClosePath();
            pen.Fill(Color.white);
        }

        // 빛줄기: 단색 쐐기가 뻗으며 돌고, 끝으로 갈수록 빨리 가늘어져 사라진다
        private void PaintFlashLight(ComicPen pen)
        {
            if (_flash <= 0f || _flash >= rayLife)
                return;
            float e = ComicKit.Eo(_flash / rayGrowTime), k = _flash / rayLife;
            float thin = 1f - k * k;
            float length = ComicKit.Lerp(rayLength.x, rayLength.y, e);
            pen.Save();
            pen.Translate(_light.x, _light.y);
            for (int i = 0; i < rayCount; i++)
            {
                float angle = (float)i / rayCount * ComicKit.Tau + _flash * raySpin;
                float half = (rayHalfWidth + rayHalfWidthStep * (i % 2)) * thin;
                pen.BeginPath();
                pen.MoveTo(0f, 0f);
                pen.LineTo(Mathf.Cos(angle - half) * length, Mathf.Sin(angle - half) * length);
                pen.LineTo(Mathf.Cos(angle + half) * length, Mathf.Sin(angle + half) * length);
                pen.ClosePath();
                pen.Fill(rayColor);
            }
            pen.Restore();
        }

        // 시안 avatar 의 dizzy — 두 눈 자리에 흰 소용돌이 (얼굴 가운데 기준)
        private void PaintDizzy(ComicPen pen)
        {
            if (_dizzyPhase <= 0f)
                return;
            float end = ComicKit.Tau * dizzyTurns;
            for (int side = -1; side <= 1; side += 2)
            {
                float cx = side * dizzyCenter.x * faceRadius, cy = dizzyCenter.y * faceRadius;
                pen.BeginPath();
                for (float a = 0f; a < end; a += dizzyStep)
                {
                    float r = a / end * faceRadius * dizzyRadius;
                    pen.LineTo(cx + Mathf.Cos(a + _dizzyPhase) * r, cy + Mathf.Sin(a + _dizzyPhase) * r);
                }
                pen.Stroke(dizzyColor, dizzyWidth);
            }
        }

        private void PaintDust(ComicPen pen, int index)
        {
            if (_flash <= scatterAt || _remain <= 0f)
                return;
            Vector2 center = PersonAt(index) + handOffset;
            var random = new ComicRandom(index * dustSeedStep);
            float radius = dustRadius * _remain;
            for (int j = 0; j < dustCount; j++)
            {
                float angle = random.Next() * ComicKit.Tau, distance = (_flash - scatterAt) * dustSpeed * random.Next();
                pen.BeginPath();
                pen.Arc(center.x + Mathf.Cos(angle) * distance, center.y + Mathf.Sin(angle) * distance, radius, 0f, ComicKit.Tau);
                pen.Fill(dustColor);
            }
        }
    }
}
