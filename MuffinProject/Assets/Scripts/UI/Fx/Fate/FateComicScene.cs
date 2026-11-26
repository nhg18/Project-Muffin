using System;
using System.Collections;
using Chapchu.UI.Fx.Comic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Fate
{
    /// <summary>
    /// 운명 뒤집기(카드 1011) 만화 컷인 장면 — 두 사람의 HP 를 맞바꾼다 (시안 CI2.SC["운명 뒤집기"], 캐주얼: 동전 던지듯).
    /// 파랑 · 노랑 반반 물방울 바탕 위에서 반반 얼굴(카드 그림 스티커)이 통통 튀다가 0.46 초에 툭 튀어 올라 0.52 초 동안 한 바퀴 반 돌고 (바탕은 반 바퀴),
    /// 0.98 초에 찌그러지며 내려앉아 반짝이가 퍼진다. 위쪽 두 사람의 알약 「HP n」 표는 0.62 초부터 통통 튀며 자리를 바꾸고,
    /// 1.04 초에 내려앉으며 오른 쪽은 초록 바탕 ▲ · 내린 쪽은 분홍 바탕 ▼.
    /// 4 단계 = HP 를 바꾼 뒤 쓴 사람 자리에 초록 · 대상 자리에 빨강 「HP n」 터짐 글자 (시안 FXA 운명 뒤집기 s4).
    /// n 은 <see cref="CardFxContext.OnHpChanged"/> 로 받는다 — 정한 시간 안에 알림이 오지 않으면 그리지 않는다.
    /// 3 단계 표는 자리의 지금 HP(<see cref="CardFxSeat.Hp"/>)로 그린다.
    /// 얼굴은 <see cref="CardFxContext.Character"/> 의 얼굴 칸을 동그랗게 잘라 쓴다 (사람마다 그림이 생기기 전까지 모두 같다).
    /// </summary>
    public class FateComicScene : ComicScene
    {
        // 시안 pill(v) = "HP " + v · FXA s4 boomAt("HP " + u.hp)
        private const string HpFormat = "HP {0}";
        private const string ArrowUp = "▲";
        private const string ArrowDown = "▼";
        // 시안 물방울: 판 가운데 기준 y −1200 ~ 1200 · x −1600 ~ 1600 를 칸마다 (홀수 줄은 반 칸 밀림)
        private const float DotSpanY = 1200f;
        private const float DotSpanX = 1600f;
        // 시안 sparkle(): 뾰족한 네 꼭짓점 별 — 꼭짓점 8 개 (바깥 · 안 번갈아)
        private const int SparklePoints = 8;

        [Header("그림 (그리는 순서)")]
        [Tooltip("반반 바탕 + 물방울 + 가운데 선 — 회전 가운데(시안 640, 380)에 둔다, 장면이 돌린다.")]
        [SerializeField] private ComicSketch backdrop;
        [Tooltip("얼굴 발밑 그림자 (장면이 그린다) — 얼굴 발밑 자리에 둔다, 튀어 오르면 장면이 가로로 줄인다.")]
        [SerializeField] private ComicSketch faceShadow;
        [Tooltip("반반 얼굴 — 카드 그림 스티커. 동전처럼 돈다.")]
        [SerializeField] private ComicSticker face;
        [Tooltip("얼굴 둘레를 도는 흰 호 셋 (장면이 그린다).")]
        [SerializeField] private ComicSketch swirl;
        [Tooltip("내려앉을 때 퍼지는 반짝이 (장면이 그린다).")]
        [SerializeField] private ComicSketch sparkles;
        [Tooltip("쓴 사람 얼굴 (동그라미 안 RawImage).")]
        [SerializeField] private RawImage casterFace;
        [Tooltip("대상 얼굴 (동그라미 안 RawImage).")]
        [SerializeField] private RawImage targetFace;
        [Tooltip("쓴 사람 HP 알약 — 대상 자리로 건너간다.")]
        [SerializeField] private HpChip casterChip;
        [Tooltip("대상 HP 알약 — 쓴 사람 자리로 건너간다.")]
        [SerializeField] private HpChip targetChip;

        [Header("돌기 (시안: f = eio((t − 0.46) / 0.52) — 얼굴은 f · 3π, 바탕은 f · π)")]
        [Tooltip("돌기 시작 (초, 시안 0.46).")]
        [SerializeField] private float spinAt = 0.46f;
        [Tooltip("도는 시간 — 끝나는 때에 내려앉는다 (초, 시안 0.52).")]
        [SerializeField] private float spinTime = 0.52f;
        [Tooltip("얼굴이 도는 만큼 (π 배, 시안 3 — 한 바퀴 반).")]
        [SerializeField] private float faceTurns = 3f;
        [Tooltip("바탕이 도는 만큼 (π 배, 시안 1 — 반 바퀴).")]
        [SerializeField] private float boardTurns = 1f;
        [Tooltip("얼굴이 가장 얇아질 때 가로 배율 (시안 0.05).")]
        [SerializeField] private float minSquish = 0.05f;

        [Header("튀기 · 내려앉기 (시안: 돌기 전 |sin(t·14)|·10·t/0.46, 도는 동안 sin(f·π)·150, 내려앉은 뒤 exp(−10τ)·cos(30τ))")]
        [Tooltip("얼굴 발밑 자리 (설계 px, 시안 640, 742).")]
        [SerializeField] private Vector2 faceAt = new Vector2(640f, 742f);
        [Tooltip("돌기 전 통통 — 빠르기 · 높이 (시안 14 · 10 px).")]
        [SerializeField] private Vector2 hop = new Vector2(14f, 10f);
        [Tooltip("돌며 튀어 오르는 높이 (설계 px, 시안 150).")]
        [SerializeField] private float jumpHeight = 150f;
        [Tooltip("내려앉은 출렁임 — 잦아드는 빠르기 · 떨림 빠르기 (시안 10 · 30).")]
        [SerializeField] private Vector2 landWobble = new Vector2(10f, 30f);
        [Tooltip("내려앉을 때 찌그러짐 — 낮아지는 만큼 · 넓어지는 만큼 (시안 0.08 · 0.1).")]
        [SerializeField] private Vector2 landSquash = new Vector2(0.08f, 0.1f);
        [Tooltip("그림자 반지름 — 가로 · 세로 (설계 px, 시안 250 · 26).")]
        [SerializeField] private Vector2 shadowRadius = new Vector2(250f, 26f);
        [Tooltip("튀어 오른 만큼 그림자가 줄어드는 비 (설계 px, 시안 1 − up / 400).")]
        [SerializeField] private float shadowShrink = 400f;
        [Tooltip("그림자 색 (시안 rgba(35, 40, 107, .25)).")]
        [SerializeField] private Color shadowColor = new Color(35f / 255f, 40f / 255f, 107f / 255f, 0.25f);

        [Header("바탕 (시안: 왼쪽 반 #6f7bd6 · 오른쪽 반 #ffd34d, 64 칸 물방울 흰색 0.22, 가운데 선)")]
        [Tooltip("반 칸 크기 (설계 px, 시안 L 2400).")]
        [SerializeField] private float halfSize = 2400f;
        [Tooltip("왼쪽 반 색 (시안 #6f7bd6).")]
        [SerializeField] private Color leftColor = new Color32(0x6f, 0x7b, 0xd6, 0xff);
        [Tooltip("오른쪽 반 색 (시안 #ffd34d).")]
        [SerializeField] private Color rightColor = new Color32(0xff, 0xd3, 0x4d, 0xff);
        [Tooltip("물방울 칸 (설계 px, 시안 64).")]
        [SerializeField] private float dotStep = 64f;
        [Tooltip("물방울 반지름 (설계 px, 시안 9).")]
        [SerializeField] private float dotRadius = 9f;
        [Tooltip("물방울 색 (시안 rgba(255, 255, 255, .22)).")]
        [SerializeField] private Color dotColor = new Color(1f, 1f, 1f, 0.22f);
        [Tooltip("이 반지름 밖 물방울은 화면에 안 보여 그리지 않는다 (설계 px — 돌림 가운데에서 화면 모서리 + 흔들림).")]
        [SerializeField] private float dotReach = 820f;
        [Tooltip("가운데 선 색 (시안 #23286b).")]
        [SerializeField] private Color lineColor = new Color32(0x23, 0x28, 0x6b, 0xff);
        [Tooltip("가운데 선 두께 (설계 px, 시안 6).")]
        [SerializeField] private float lineWidth = 6f;

        [Header("흰 호 (시안: 0.5 초 ~ 내려앉기, (640, 420 − 튀어 오른 높이) 반지름 330, 시작 각 t·9 + 2.1i, 길이 0.5)")]
        [Tooltip("나오는 때 (초, 시안 0.5).")]
        [SerializeField] private float swirlAt = 0.5f;
        [Tooltip("가운데 (설계 px, 시안 640, 420).")]
        [SerializeField] private Vector2 swirlCenter = new Vector2(640f, 420f);
        [Tooltip("반지름 (설계 px, 시안 330).")]
        [SerializeField] private float swirlRadius = 330f;
        [Tooltip("호 수 (시안 3).")]
        [SerializeField] private int swirlCount = 3;
        [Tooltip("도는 빠르기 (라디안/초, 시안 9).")]
        [SerializeField] private float swirlSpeed = 9f;
        [Tooltip("호 사이 각 (라디안, 시안 2.1).")]
        [SerializeField] private float swirlStep = 2.1f;
        [Tooltip("호 길이 (라디안, 시안 0.5).")]
        [SerializeField] private float swirlLength = 0.5f;
        [Tooltip("선 굵기 (설계 px, 시안 7).")]
        [SerializeField] private float swirlWidth = 7f;
        [Tooltip("진하기 (시안 0.8).")]
        [SerializeField] private float swirlAlpha = 0.8f;

        [Header("반짝이 (시안: 내려앉고 0.5 초, 여덟 개가 240 → 400 퍼지며 30 → 6 작아진다)")]
        [Tooltip("사는 시간 (초, 시안 0.5).")]
        [SerializeField] private float sparkleTime = 0.5f;
        [Tooltip("개수 (시안 8).")]
        [SerializeField] private int sparkleCount = 8;
        [Tooltip("가운데 (설계 px, 시안 640, 430).")]
        [SerializeField] private Vector2 sparkleCenter = new Vector2(640f, 430f);
        [Tooltip("가운데에서 거리 — 처음 · 끝 (설계 px, 시안 240 · 400, eo).")]
        [SerializeField] private Vector2 sparkleSpread = new Vector2(240f, 400f);
        [Tooltip("세로로 눌린 비 (시안 0.7).")]
        [SerializeField] private float sparkleFlatten = 0.7f;
        [Tooltip("크기 — 처음 · 끝 (설계 px, 시안 30 · 6).")]
        [SerializeField] private Vector2 sparkleSize = new Vector2(30f, 6f);
        [Tooltip("첫 별의 각 (라디안, 시안 0.3).")]
        [SerializeField] private float sparkleTurn = 0.3f;
        [Tooltip("도는 빠르기 (사는 동안 라디안, 시안 4).")]
        [SerializeField] private float sparkleSpin = 4f;
        [Tooltip("별 허리 굵기 (반지름 비, 시안 0.18).")]
        [SerializeField] private float sparkleWaist = 0.18f;
        [Tooltip("번갈아 쓰는 색 (시안 짝수 #ffd34d · 홀수 #fff).")]
        [SerializeField] private Color[] sparkleColors = { new Color32(0xff, 0xd3, 0x4d, 0xff), Color.white };

        [Header("얼굴 (시안 avatar)")]
        [Tooltip("캐릭터 그림에서 얼굴 칸 (0 ~ 1, 그림 왼쪽 아래 기준) — chara 의 chara_face 영역.")]
        [SerializeField] private Rect faceArea = new Rect(0.0187f, 0.3479f, 0.825f, 0.6371f);
        [Tooltip("캐릭터 그림이 없을 때 얼굴 색 (시안 #bbb).")]
        [SerializeField] private Color noFaceColor = new Color32(0xbb, 0xbb, 0xbb, 0xff);

        [Header("HP 알약 (시안 pill: 0.62 초부터 0.42 초 동안 맞바꾼다 — 위로 170 · 아래로 102 휘며 1.15 배, 다 건너가면 통통 내려앉는다)")]
        [Tooltip("쓴 사람 알약 자리 (설계 px, 시안 230, 170 + 92).")]
        [SerializeField] private Vector2 chipLeft = new Vector2(230f, 262f);
        [Tooltip("대상 알약 자리 (설계 px, 시안 1050, 170 + 92).")]
        [SerializeField] private Vector2 chipRight = new Vector2(1050f, 262f);
        [Tooltip("맞바꾸기 시작 (초, 시안 0.62).")]
        [SerializeField] private float swapAt = 0.62f;
        [Tooltip("맞바꾸는 시간 (초, 시안 0.42).")]
        [SerializeField] private float swapTime = 0.42f;
        [Tooltip("건너갈 때 휘는 높이 (설계 px, 시안 170 — 쓴 사람 알약은 위로, 대상 알약은 아래로 × 0.6).")]
        [SerializeField] private float swapArc = 170f;
        [Tooltip("대상 알약이 아래로 휘는 비율 (시안 0.6).")]
        [SerializeField] private float swapDip = 0.6f;
        [Tooltip("건너가는 한가운데에서 커지는 만큼 (시안 0.15).")]
        [SerializeField] private float swapGrow = 0.15f;
        [Tooltip("내려앉은 출렁임 — 크기 · 잦아드는 빠르기 · 떨림 빠르기 (시안 0.15 · 9 · 30).")]
        [SerializeField] private Vector3 settleBounce = new Vector3(0.15f, 9f, 30f);
        [Tooltip("화살표가 나오는 때 — 내려앉고 back(τ / 이 시간, 지나침) 이 0.5 를 넘을 때 (초 · 지나침, 시안 0.2 · 2.6).")]
        [SerializeField] private Vector2 arrowPop = new Vector2(0.2f, 2.6f);
        [Tooltip("바탕 색 — 그대로 (시안 #fff).")]
        [SerializeField] private Color chipColor = Color.white;
        [Tooltip("바탕 색 — 올랐다 (시안 #c9f5dc).")]
        [SerializeField] private Color chipUpColor = new Color32(0xc9, 0xf5, 0xdc, 0xff);
        [Tooltip("바탕 색 — 내렸다 (시안 #ffd6d2).")]
        [SerializeField] private Color chipDownColor = new Color32(0xff, 0xd6, 0xd2, 0xff);
        [Tooltip("화살표 색 — 올랐다 ▲ (시안 #11a35a).")]
        [SerializeField] private Color arrowUpColor = new Color32(0x11, 0xa3, 0x5a, 0xff);
        [Tooltip("화살표 색 — 내렸다 ▼ (시안 #e0453a).")]
        [SerializeField] private Color arrowDownColor = new Color32(0xe0, 0x45, 0x3a, 0xff);
        [Tooltip("알약 너비 = 글자 폭 + 이만큼 (설계 px, 시안 50).")]
        [SerializeField] private float chipPadding = 50f;
        [Tooltip("알약 높이 = 글자 크기 × 이만큼 (시안 58 / 40).")]
        [SerializeField] private float chipHeight = 1.45f;
        [Tooltip("알약 테 두께 (설계 px, 시안 5).")]
        [SerializeField] private float chipLine = 5f;
        [Tooltip("알약 오른쪽 끝에서 화살표 가운데까지 (설계 px, 시안 26).")]
        [SerializeField] private float arrowGap = 26f;

        [Header("4 단계 (시안 FXA s4: boomAt 쓴 사람 「HP n」 #3ddc84 · 대상 「HP n」 #ff4f2e → 500ms)")]
        [Tooltip("쓴 사람 터짐 글자 색 (시안 #3ddc84).")]
        [SerializeField] private Color casterBoomColor = new Color32(0x3d, 0xdc, 0x84, 0xff);
        [Tooltip("대상 터짐 글자 색 (시안 #ff4f2e).")]
        [SerializeField] private Color targetBoomColor = new Color32(0xff, 0x4f, 0x2e, 0xff);
        [Tooltip("HP 결과 알림을 기다리는 최대 시간 (초). 안 오면 그리지 않고 끝낸다.")]
        [SerializeField] private float resultTimeout = 0.5f;
        [Tooltip("마지막 터짐 글자 뒤 기다림 (초, 시안 500ms).")]
        [SerializeField] private float postWait = 0.5f;

        private float _time;
        private float _up;
        private bool _swirling;
        private bool _sparkling;
        private int _casterHp;
        private int _targetHp;
        private CardFxContext _fx;
        private CardFxSeat _caster;
        private CardFxSeat _target;
        private int _shown;
        private float _lastShownAt;

        /// <summary>HP 알약 하나 — 그림자 · 알약 · 글자 · 화살표 (시안 pill(v, x, y, bg, fg, s, arrow)). 자리 · 크기는 root.</summary>
        [Serializable]
        private struct HpChip
        {
            public RectTransform root;
            public UIShape shadow;
            public UIShape box;
            public TMP_Text text;
            public TMP_Text arrow;
        }

        private float LandAt => spinAt + spinTime;

        private void Awake()
        {
            if (backdrop == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(backdrop)} 가 비었다");
            if (faceShadow == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(faceShadow)} 가 비었다");
            if (face == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(face)} 가 비었다");
            if (swirl == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(swirl)} 가 비었다");
            if (sparkles == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(sparkles)} 가 비었다");
            if (casterFace == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(casterFace)} 가 비었다");
            if (targetFace == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(targetFace)} 가 비었다");
            CheckChip(casterChip, nameof(casterChip));
            CheckChip(targetChip, nameof(targetChip));
        }

        public override void Begin(ComicContext context)
        {
            backdrop.Painter = PaintBackdrop;
            backdrop.Redraw();
            faceShadow.Painter = PaintFaceShadow;
            faceShadow.Redraw();
            swirl.Painter = PaintSwirl;
            sparkles.Painter = PaintSparkles;
            ComicKit.ShowFace(casterFace, context.Fx.Character, faceArea, noFaceColor);
            ComicKit.ShowFace(targetFace, context.Fx.Character, faceArea, noFaceColor);
            _casterHp = context.From != null ? context.From.Hp : 0;
            _targetHp = context.Target != null ? context.Target.Hp : 0;
            SetChip(casterChip, _casterHp);
            SetChip(targetChip, _targetHp);
        }

        public override void Draw(float t, ComicContext context)
        {
            _time = t;
            float landAt = LandAt;
            // 바탕이 동전과 함께 반 바퀴
            float f = Eio((t - spinAt) / spinTime);
            ComicKit.Turn((RectTransform)backdrop.transform, f * boardTurns * Mathf.PI);

            // 가운데 반반 얼굴 — 툭 튀어 올라 빙글빙글, 내려앉으며 찌그러진다
            float up = t < spinAt ? Mathf.Abs(Mathf.Sin(t * hop.x)) * hop.y * (t / spinAt)
                : t < landAt ? Mathf.Sin(f * Mathf.PI) * jumpHeight : 0f;
            float land = t > landAt ? Mathf.Exp(-(t - landAt) * landWobble.x) * Mathf.Cos((t - landAt) * landWobble.y) : 0f;
            float cos = Mathf.Cos(f * faceTurns * Mathf.PI);
            float squish = Mathf.Max(minSquish, Mathf.Abs(cos)) * (cos >= 0f ? 1f : -1f);
            float size = 1f - landSquash.x * land;
            var faceRect = (RectTransform)face.transform;
            ComicKit.Place(faceRect, faceAt.x, faceAt.y - up);
            faceRect.localScale = new Vector3(size * squish * (1f + landSquash.y * land), size, 1f);
            faceShadow.transform.localScale = new Vector3(1f - up / shadowShrink, 1f, 1f);
            _up = up;

            bool swirling = t > swirlAt && t < landAt;
            if (swirling || _swirling)
                swirl.Redraw();
            _swirling = swirling;
            bool sparkling = t > landAt && t < landAt + sparkleTime;
            if (sparkling || _sparkling)
                sparkles.Redraw();
            _sparkling = sparkling;

            // 양쪽 체력: 알약이 통통 튀며 자리를 바꾼다
            float swap = Eio((t - swapAt) / swapTime);
            if (swap <= 0f)
            {
                PlaceChip(casterChip, chipLeft, 1f, chipColor, 0);
                PlaceChip(targetChip, chipRight, 1f, chipColor, 0);
            }
            else if (swap >= 1f)
            {
                float settled = t - (swapAt + swapTime);
                bool arrows = ComicKit.Back(ComicKit.Cl(settled / arrowPop.x), arrowPop.y) > 0.5f;
                float bounce = 1f + settleBounce.x * Mathf.Exp(-settled * settleBounce.y) * Mathf.Sin(settled * settleBounce.z);
                // 대상 HP 가 쓴 사람 자리로 — 쓴 사람이 얻은 만큼 위(+) · 아래(−)
                int gain = Math.Sign(_targetHp - _casterHp);
                PlaceChip(targetChip, chipLeft, bounce, ChangeColor(gain), arrows ? gain : 0);
                PlaceChip(casterChip, chipRight, bounce, ChangeColor(-gain), arrows ? -gain : 0);
            }
            else
            {
                float arc = Mathf.Sin(swap * Mathf.PI) * swapArc, grow = 1f + swapGrow * Mathf.Sin(swap * Mathf.PI);
                PlaceChip(casterChip, new Vector2(ComicKit.Lerp(chipLeft.x, chipRight.x, swap), chipLeft.y - arc), grow, chipColor, 0);
                PlaceChip(targetChip, new Vector2(ComicKit.Lerp(chipRight.x, chipLeft.x, swap), chipRight.y + arc * swapDip), grow, chipColor, 0);
            }
        }

        public override IEnumerator PostRoutine(ComicContext context)
        {
            _fx = context.Fx;
            _caster = context.From;
            _target = context.Target;
            _shown = 0;
            _fx.OnHpChanged += HandleHpChanged;
            try
            {
                // 두 사람 HP 를 바꾼 알림 — 게임 화면은 공통 결과와 같은 순간에 보낸다
                for (float time = 0f; time < resultTimeout && _shown < 2; time += Time.deltaTime)
                    yield return null;
            }
            finally
            {
                _fx.OnHpChanged -= HandleHpChanged;
            }
            if (_shown == 0)
                yield break;
            for (float time = Time.time - _lastShownAt; time < postWait; time += Time.deltaTime)
                yield return null;
        }

        private void HandleHpChanged(CardFxSeat seat, int before, int after)
        {
            if (_fx.Layer == null || seat == null || seat.Anchor == null || _caster == _target)
                return;
            Color color;
            if (seat == _caster)
                color = casterBoomColor;
            else if (seat == _target)
                color = targetBoomColor;
            else
                return;
            FxLayer layer = _fx.Layer;
            layer.Boom(layer.BoomPoint(layer.CenterOf(seat.Anchor)), color, string.Format(HpFormat, after));
            _shown++;
            _lastShownAt = Time.time;
        }

        // 시안: 올랐으면 초록, 내렸으면 분홍, 같으면 흰색
        private Color ChangeColor(int change)
        {
            if (change > 0)
                return chipUpColor;
            return change < 0 ? chipDownColor : chipColor;
        }

        // 시안 pill: 너비 = 글자 폭 + 50, 높이 58, 양 끝이 둥근 알약 (UIShape 반지름이 높이의 반에서 멈춘다) — 화살표는 오른쪽 끝 + 26
        private void SetChip(HpChip chip, int hp)
        {
            ComicKit.FitTag(chip.text, chip.shadow.rectTransform, chip.box.rectTransform, string.Format(HpFormat, hp), chipPadding, chipHeight, chipLine);
            float width = chip.shadow.rectTransform.sizeDelta.x;
            Vector2 arrowAt = chip.arrow.rectTransform.anchoredPosition;
            chip.arrow.rectTransform.anchoredPosition = new Vector2(width / 2f + arrowGap, arrowAt.y);
            chip.arrow.gameObject.SetActive(false);
        }

        private void PlaceChip(HpChip chip, Vector2 at, float scale, Color fill, int arrow)
        {
            ComicKit.Place(chip.root, at.x, at.y);
            chip.root.localScale = new Vector3(scale, scale, 1f);
            if (chip.box.color != fill)
                chip.box.color = fill;
            bool showArrow = arrow != 0;
            if (chip.arrow.gameObject.activeSelf != showArrow)
                chip.arrow.gameObject.SetActive(showArrow);
            if (!showArrow)
                return;
            chip.arrow.text = arrow > 0 ? ArrowUp : ArrowDown;
            chip.arrow.color = arrow > 0 ? arrowUpColor : arrowDownColor;
        }

        // 시안: 판 가운데에서 왼쪽 반 파랑 · 오른쪽 반 노랑, 흰 물방울, 가운데 세로 선
        private void PaintBackdrop(ComicPen pen)
        {
            pen.BeginPath();
            pen.Rect(-halfSize, -halfSize, halfSize, halfSize * 2f);
            pen.Fill(leftColor);
            pen.BeginPath();
            pen.Rect(0f, -halfSize, halfSize, halfSize * 2f);
            pen.Fill(rightColor);

            float reach = dotReach + dotRadius;
            for (float gy = -DotSpanY; gy < DotSpanY; gy += dotStep)
            {
                // 시안 ((gy / 64) & 1) — 0 쪽으로 자른 줄 번호가 홀수면 반 칸 민다
                float start = -DotSpanX + (((int)(gy / dotStep) & 1) * dotStep / 2f);
                for (float gx = start; gx < DotSpanX; gx += dotStep)
                {
                    if (gx * gx + gy * gy > reach * reach)
                        continue;
                    pen.BeginPath();
                    pen.Arc(gx, gy, dotRadius, 0f, ComicKit.Tau);
                    pen.Fill(dotColor);
                }
            }

            pen.BeginPath();
            pen.MoveTo(0f, -halfSize);
            pen.LineTo(0f, halfSize);
            pen.Stroke(lineColor, lineWidth);
        }

        // 발밑 납작한 타원 — 가로 크기는 Draw 가 RectTransform 으로
        private void PaintFaceShadow(ComicPen pen)
        {
            pen.BeginPath();
            pen.Ellipse(0f, 0f, shadowRadius.x, shadowRadius.y, 0f, 0f, ComicKit.Tau);
            pen.Fill(shadowColor);
        }

        private void PaintSwirl(ComicPen pen)
        {
            if (!(_time > swirlAt && _time < LandAt))
                return;
            float y = swirlCenter.y - _up;
            pen.Alpha = swirlAlpha;
            for (int i = 0; i < swirlCount; i++)
            {
                float start = _time * swirlSpeed + i * swirlStep;
                pen.BeginPath();
                pen.Arc(swirlCenter.x, y, swirlRadius, start, start + swirlLength);
                pen.Stroke(Color.white, swirlWidth);
            }
        }

        private void PaintSparkles(ComicPen pen)
        {
            float k = (_time - LandAt) / sparkleTime;
            if (k <= 0f || k >= 1f || sparkleColors.Length == 0)
                return;
            float d = ComicKit.Lerp(sparkleSpread.x, sparkleSpread.y, ComicKit.Eo(k));
            for (int i = 0; i < sparkleCount; i++)
            {
                float a = (float)i / sparkleCount * ComicKit.Tau + sparkleTurn;
                Sparkle(pen, sparkleCenter.x + Mathf.Cos(a) * d, sparkleCenter.y + Mathf.Sin(a) * d * sparkleFlatten,
                    ComicKit.Lerp(sparkleSize.x, sparkleSize.y, k), k * sparkleSpin + i, 1f - k, sparkleColors[i % sparkleColors.Length]);
            }
        }

        // 시안 sparkle(x, y, r, rot, a, col): 뾰족한 네 꼭짓점 별
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

        private void CheckChip(HpChip chip, string field)
        {
            if (chip.root == null || chip.shadow == null || chip.box == null || chip.text == null || chip.arrow == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {field} 에 빈 칸이 있다 (root · shadow · box · text · arrow)");
        }

        // 시안 eio — 느리게 시작해 빨라졌다가 느리게 끝 (ease-in-out cubic). 틀 ComicKit 에서 빠져 장면에 둔다
        private static float Eio(float k)
        {
            k = ComicKit.Cl(k);
            return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
        }
    }
}
