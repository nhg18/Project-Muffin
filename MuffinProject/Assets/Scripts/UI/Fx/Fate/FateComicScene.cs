using System;
using System.Collections;
using Chapchu.UI.Fx.Comic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Fate
{
    /// <summary>
    /// 운명 뒤집기(카드 111) 만화 컷인 장면 — 두 사람의 HP 를 맞바꾼다 (시안 CI2.SC["운명 뒤집기"]).
    /// 잿빛 · 분홍빛 반반 바탕과 반반 얼굴(카드 그림 스티커)이 0.48 초부터 0.5 초 동안 동전처럼 반 바퀴 뒤집히고,
    /// 위쪽 두 사람의 「HP n」 표가 0.62 초부터 서로 자리를 바꾼다 → 0.78 초 「휘릭!」.
    /// 4 단계 = HP 를 바꾼 뒤 쓴 사람 자리에 초록 · 대상 자리에 빨강 「HP n」 터짐 글자 (시안 FXA 운명 뒤집기 s4).
    /// n 은 <see cref="CardFxContext.OnHpChanged"/> 로 받는다 — 정한 시간 안에 알림이 오지 않으면 그리지 않는다.
    /// 3 단계 표는 자리의 지금 HP(<see cref="CardFxSeat.Hp"/>)로 그린다.
    /// 얼굴은 <see cref="CardFxContext.Character"/> 의 얼굴 칸을 동그랗게 잘라 쓴다 (사람마다 그림이 생기기 전까지 모두 같다).
    /// 연출 길이 2.96초 (3 단계 2.46초 + 4 단계 0.5초) — 카드 처리 시간(resolveSeconds)은 게임 화면에 연출을 붙일 때 맞춘다 (PM 결정 2026-11-15).
    /// </summary>
    public class FateComicScene : ComicScene
    {
        // 시안 chip(v) = label("HP " + v) · FXA s4 boomAt("HP " + u.hp)
        private const string HpFormat = "HP {0}";

        [Header("그림 (그리는 순서)")]
        [Tooltip("반반 바탕 + 흩뿌린 점 — 회전 가운데에 둔다, 장면이 돌린다.")]
        [SerializeField] private ComicSketch backdrop;
        [Tooltip("집중선.")]
        [SerializeField] private ComicFocusLines focusLines;
        [Tooltip("반반 얼굴 — 카드 그림 스티커. 가로로 뒤집힌다.")]
        [SerializeField] private ComicSticker face;
        [Tooltip("돌아가는 흰 호 셋 (장면이 그린다).")]
        [SerializeField] private ComicSketch swoosh;
        [Tooltip("쓴 사람 얼굴 (동그라미 안 RawImage).")]
        [SerializeField] private RawImage casterFace;
        [Tooltip("대상 얼굴 (동그라미 안 RawImage).")]
        [SerializeField] private RawImage targetFace;
        [Tooltip("쓴 사람 HP 표 — 대상 자리로 건너간다.")]
        [SerializeField] private HpChip casterChip;
        [Tooltip("대상 HP 표 — 쓴 사람 자리로 건너간다.")]
        [SerializeField] private HpChip targetChip;
        [Tooltip("「휘릭!」 글자 (찍히기).")]
        [SerializeField] private ComicStamp whoosh;

        [Header("뒤집기 (시안: eio((t − 0.48) / 0.5) · π 만큼 돈다)")]
        [Tooltip("뒤집기 시작 (초, 시안 0.48).")]
        [SerializeField] private float flipAt = 0.48f;
        [Tooltip("뒤집는 시간 (초, 시안 0.5).")]
        [SerializeField] private float flipTime = 0.5f;
        [Tooltip("얼굴이 가장 얇아질 때 가로 배율 (시안 0.04).")]
        [SerializeField] private float minSquish = 0.04f;

        [Header("바탕 (시안: 왼쪽 반 #3b3f5c · 오른쪽 반 #ffc6d6, 점 60 개 진하기 0.25)")]
        [Tooltip("반 칸 크기 (설계 px, 시안 L 2400).")]
        [SerializeField] private float halfSize = 2400f;
        [Tooltip("왼쪽 반 색 (시안 #3b3f5c).")]
        [SerializeField] private Color shadeColor = new Color32(0x3b, 0x3f, 0x5c, 0xff);
        [Tooltip("오른쪽 반 색 (시안 #ffc6d6).")]
        [SerializeField] private Color blushColor = new Color32(0xff, 0xc6, 0xd6, 0xff);
        [Tooltip("점 씨앗 (시안 rng(3)).")]
        [SerializeField] private int dotSeed = 3;
        [Tooltip("점 수 (시안 60).")]
        [SerializeField] private int dotCount = 60;
        [Tooltip("점 진하기 (시안 0.25).")]
        [SerializeField] private float dotAlpha = 0.25f;
        [Tooltip("점까지 거리 — 최소 · 더하는 만큼 (설계 px, 시안 200 + R·900).")]
        [SerializeField] private Vector2 dotDistance = new Vector2(200f, 900f);
        [Tooltip("점 반지름 — 최소 · 더하는 만큼 (설계 px, 시안 4 + R·10).")]
        [SerializeField] private Vector2 dotRadius = new Vector2(4f, 10f);
        [Tooltip("왼쪽 반 점 색 (시안 #1d2036).")]
        [SerializeField] private Color shadeDot = new Color32(0x1d, 0x20, 0x36, 0xff);
        [Tooltip("오른쪽 반 점 색 (시안 #fff).")]
        [SerializeField] private Color blushDot = Color.white;

        [Header("흰 호 (시안: 0.6 ~ 1.1 초, 반지름 300 + 40i + 80k, 각 −0.9π ~ −0.3π 에 0.2i + 2k 를 더한다)")]
        [Tooltip("나오는 때 (초, 시안 0.6).")]
        [SerializeField] private float swooshAt = 0.6f;
        [Tooltip("사는 시간 (초, 시안 0.5).")]
        [SerializeField] private float swooshTime = 0.5f;
        [Tooltip("가운데 (설계 px, 시안 640, 380).")]
        [SerializeField] private Vector2 swooshCenter = new Vector2(640f, 380f);
        [Tooltip("호 수 (시안 3).")]
        [SerializeField] private int swooshCount = 3;
        [Tooltip("첫 반지름 (설계 px, 시안 300).")]
        [SerializeField] private float swooshRadius = 300f;
        [Tooltip("호 사이 반지름 (설계 px, 시안 40).")]
        [SerializeField] private float swooshGap = 40f;
        [Tooltip("커지는 만큼 (설계 px, 시안 80).")]
        [SerializeField] private float swooshGrow = 80f;
        [Tooltip("호 시작 · 끝 각 (π 배, 시안 −0.9 · −0.3).")]
        [SerializeField] private Vector2 swooshArc = new Vector2(-0.9f, -0.3f);
        [Tooltip("호마다 더하는 각 (라디안, 시안 0.2).")]
        [SerializeField] private float swooshStep = 0.2f;
        [Tooltip("도는 만큼 (라디안, 시안 2).")]
        [SerializeField] private float swooshSpin = 2f;
        [Tooltip("선 굵기 (설계 px, 시안 8).")]
        [SerializeField] private float swooshWidth = 8f;

        [Header("얼굴 (시안 avatar)")]
        [Tooltip("캐릭터 그림에서 얼굴 칸 (0 ~ 1, 그림 왼쪽 아래 기준) — chara 의 chara_face 영역.")]
        [SerializeField] private Rect faceArea = new Rect(0.0187f, 0.3479f, 0.825f, 0.6371f);
        [Tooltip("캐릭터 그림이 없을 때 얼굴 색 (시안 #bbb).")]
        [SerializeField] private Color noFaceColor = new Color32(0xbb, 0xbb, 0xbb, 0xff);

        [Header("HP 표 (시안: 0.62 초부터 0.42 초 동안 맞바꾼다 — 위로 170 · 아래로 102 휘어 건너가고, 1.04 초에 20 위에서 내려앉는다)")]
        [Tooltip("쓴 사람 표 자리 (설계 px, 시안 230, 170 + 92).")]
        [SerializeField] private Vector2 chipLeft = new Vector2(230f, 262f);
        [Tooltip("대상 표 자리 (설계 px, 시안 1050, 170 + 92).")]
        [SerializeField] private Vector2 chipRight = new Vector2(1050f, 262f);
        [Tooltip("맞바꾸기 시작 (초, 시안 0.62).")]
        [SerializeField] private float swapAt = 0.62f;
        [Tooltip("맞바꾸는 시간 (초, 시안 0.42).")]
        [SerializeField] private float swapTime = 0.42f;
        [Tooltip("건너갈 때 휘는 높이 (설계 px, 시안 170 — 쓴 사람 표는 위로, 대상 표는 아래로 × 0.6).")]
        [SerializeField] private float swapArc = 170f;
        [Tooltip("대상 표가 아래로 휘는 비율 (시안 0.6).")]
        [SerializeField] private float swapDip = 0.6f;
        [Tooltip("내려앉는 시간 (초, 시안 0.2).")]
        [SerializeField] private float settleTime = 0.2f;
        [Tooltip("내려앉을 때 지나치는 정도 (시안 back 2.4).")]
        [SerializeField] private float settleOvershoot = 2.4f;
        [Tooltip("내려앉기 전 높이 (설계 px, 시안 20).")]
        [SerializeField] private float settleRise = 20f;
        [Tooltip("글자 색 — 그대로 (시안 #2e1a78).")]
        [SerializeField] private Color chipColor = new Color32(0x2e, 0x1a, 0x78, 0xff);
        [Tooltip("글자 색 — 올랐다 (시안 #0a8f4a).")]
        [SerializeField] private Color chipUpColor = new Color32(0x0a, 0x8f, 0x4a, 0xff);
        [Tooltip("글자 색 — 내렸다 (시안 #d6282f).")]
        [SerializeField] private Color chipDownColor = new Color32(0xd6, 0x28, 0x2f, 0xff);
        [Tooltip("상자 너비 = 글자 폭 + 이만큼 (설계 px, 시안 34).")]
        [SerializeField] private float chipPadding = 34f;
        [Tooltip("상자 높이 = 글자 크기 × 이만큼 (시안 1.35).")]
        [SerializeField] private float chipHeight = 1.35f;
        [Tooltip("상자 테 두께 (설계 px, 시안 5).")]
        [SerializeField] private float chipLine = 5f;

        [Header("「휘릭!」")]
        [Tooltip("찍히기 시작 (초, 시안 0.78).")]
        [SerializeField] private float whooshAt = 0.78f;
        [Tooltip("찍히는 시간 (초, 시안 0.16).")]
        [SerializeField] private float whooshTime = 0.16f;

        [Header("4 단계 (시안 FXA s4: boomAt 쓴 사람 「HP n」 #3ddc84 · 대상 「HP n」 #ff4f2e → 500ms)")]
        [Tooltip("쓴 사람 터짐 글자 색 (시안 #3ddc84).")]
        [SerializeField] private Color casterBoomColor = new Color32(0x3d, 0xdc, 0x84, 0xff);
        [Tooltip("대상 터짐 글자 색 (시안 #ff4f2e).")]
        [SerializeField] private Color targetBoomColor = new Color32(0xff, 0x4f, 0x2e, 0xff);
        [Tooltip("HP 결과 알림을 기다리는 최대 시간 (초). 안 오면 그리지 않고 끝낸다.")]
        [SerializeField] private float resultTimeout = 0.5f;
        [Tooltip("마지막 터짐 글자 뒤 기다림 (초, 시안 500ms).")]
        [SerializeField] private float postWait = 0.5f;

        private float _swooshK;
        private int _casterHp;
        private int _targetHp;
        private CardFxContext _fx;
        private CardFxSeat _caster;
        private CardFxSeat _target;
        private int _shown;
        private float _lastShownAt;

        /// <summary>HP 표 하나 — 그림자 · 상자 · 글자 (시안 label(x, y, "HP n", {size: 40, col, bg: #fff})). 자리 · 기울기는 root.</summary>
        [Serializable]
        private struct HpChip
        {
            public RectTransform root;
            public RectTransform shadow;
            public RectTransform box;
            public TMP_Text text;
        }

        private void Awake()
        {
            if (backdrop == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(backdrop)} 가 비었다");
            if (focusLines == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(focusLines)} 가 비었다");
            if (face == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(face)} 가 비었다");
            if (swoosh == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(swoosh)} 가 비었다");
            if (casterFace == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(casterFace)} 가 비었다");
            if (targetFace == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(targetFace)} 가 비었다");
            CheckChip(casterChip, nameof(casterChip));
            CheckChip(targetChip, nameof(targetChip));
            if (whoosh == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {nameof(whoosh)} 가 비었다");
        }

        public override void Begin(ComicContext context)
        {
            backdrop.Painter = PaintBackdrop;
            backdrop.Redraw();
            swoosh.Painter = PaintSwoosh;
            ComicKit.ShowFace(casterFace, context.Fx.Character, faceArea, noFaceColor);
            ComicKit.ShowFace(targetFace, context.Fx.Character, faceArea, noFaceColor);
            _casterHp = context.From != null ? context.From.Hp : 0;
            _targetHp = context.Target != null ? context.Target.Hp : 0;
        }

        public override void Draw(float t, ComicContext context)
        {
            // 판이 통째로 반 바퀴 돈다
            float angle = Eio((t - flipAt) / flipTime) * Mathf.PI;
            ComicKit.Turn((RectTransform)backdrop.transform, angle);
            focusLines.SetTime(t);

            // 가운데 반반 얼굴 — 동전처럼 뒤집힌다
            float cos = Mathf.Cos(angle);
            float squish = Mathf.Max(minSquish, Mathf.Abs(cos));
            face.transform.localScale = new Vector3(cos >= 0f ? squish : -squish, 1f, 1f);

            float k = (t - swooshAt) / swooshTime;
            if ((k > 0f && k < 1f) || (_swooshK > 0f && _swooshK < 1f))
                swoosh.Redraw();
            _swooshK = k;

            // 양쪽 체력: 숫자가 서로 자리를 바꾼다
            float swap = Eio((t - swapAt) / swapTime);
            if (swap <= 0f)
            {
                PlaceChip(casterChip, chipLeft.x, chipLeft.y, _casterHp, chipColor);
                PlaceChip(targetChip, chipRight.x, chipRight.y, _targetHp, chipColor);
            }
            else if (swap >= 1f)
            {
                float rise = (1f - ComicKit.Back((t - swapAt - swapTime) / settleTime, settleOvershoot)) * settleRise;
                PlaceChip(targetChip, chipLeft.x, chipLeft.y - rise, _targetHp, ChangeColor(_targetHp, _casterHp));
                PlaceChip(casterChip, chipRight.x, chipRight.y - rise, _casterHp, ChangeColor(_casterHp, _targetHp));
            }
            else
            {
                float lift = Mathf.Sin(swap * Mathf.PI) * swapArc;
                PlaceChip(casterChip, ComicKit.Lerp(chipLeft.x, chipRight.x, swap), chipLeft.y - lift, _casterHp, chipColor);
                PlaceChip(targetChip, ComicKit.Lerp(chipRight.x, chipLeft.x, swap), chipRight.y + lift * swapDip, _targetHp, chipColor);
            }

            whoosh.Apply((t - whooshAt) / whooshTime);
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

        // 시안 cc(nv, ov): 올랐으면 초록, 내렸으면 빨강, 같으면 그대로
        private Color ChangeColor(int now, int old)
        {
            if (now > old)
                return chipUpColor;
            return now < old ? chipDownColor : chipColor;
        }

        private void PlaceChip(HpChip chip, float x, float y, int hp, Color color)
        {
            ComicKit.Place(chip.root, x, y);
            string text = string.Format(HpFormat, hp);
            if (chip.text.color != color)
                chip.text.color = color;
            if (chip.text.text == text)
                return;
            // 표 크기 맞추기는 틀로 모을 후보 — 이 작업에서는 틀(ComicKit)을 고치지 않는다
            chip.text.text = text;
            float width = chip.text.GetPreferredValues(text).x + chipPadding;
            float height = chip.text.fontSize * chipHeight;
            chip.shadow.sizeDelta = new Vector2(width, height);
            // 시안 strokeRect 는 테 가운데가 상자 가장자리 — 테 반만큼 밖으로
            chip.box.sizeDelta = new Vector2(width + chipLine, height + chipLine);
        }

        private void PaintBackdrop(ComicPen pen)
        {
            pen.BeginPath();
            pen.Rect(-halfSize, -halfSize, halfSize, halfSize * 2f);
            pen.Fill(shadeColor);
            pen.BeginPath();
            pen.Rect(0f, -halfSize, halfSize, halfSize * 2f);
            pen.Fill(blushColor);

            var random = new ComicRandom(dotSeed);
            pen.Alpha = dotAlpha;
            for (int i = 0; i < dotCount; i++)
            {
                float a = random.Next() * ComicKit.Tau;
                float d = dotDistance.x + random.Next() * dotDistance.y;
                float r = dotRadius.x + random.Next() * dotRadius.y;
                pen.BeginPath();
                pen.Arc(Mathf.Cos(a) * d, Mathf.Sin(a) * d, r, 0f, ComicKit.Tau);
                pen.Fill(Mathf.Cos(a) < 0f ? shadeDot : blushDot);
            }
        }

        private void PaintSwoosh(ComicPen pen)
        {
            float k = _swooshK;
            if (k <= 0f || k >= 1f)
                return;
            pen.Alpha = 1f - k;
            for (int i = 0; i < swooshCount; i++)
            {
                float turn = i * swooshStep + k * swooshSpin;
                pen.BeginPath();
                pen.Arc(swooshCenter.x, swooshCenter.y, swooshRadius + i * swooshGap + k * swooshGrow,
                    swooshArc.x * Mathf.PI + turn, swooshArc.y * Mathf.PI + turn);
                pen.Stroke(Color.white, swooshWidth);
            }
        }

        private void CheckChip(HpChip chip, string field)
        {
            if (chip.root == null || chip.shadow == null || chip.box == null || chip.text == null)
                Debug.LogError($"[{nameof(FateComicScene)}] {name} 의 {field} 에 빈 칸이 있다 (root · shadow · box · text)");
        }

        // 시안 eio — 느리게 시작해 빨라졌다가 느리게 끝 (ease-in-out cubic). 틀 ComicKit 에서 빠져 장면에 둔다
        private static float Eio(float k)
        {
            k = ComicKit.Cl(k);
            return k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;
        }
    }
}
