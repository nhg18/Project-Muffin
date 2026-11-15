using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 연출 레이어 — 카드 연출들이 같이 쓰는 파티클(불똥 · 번짐 · 고리 · 줄 빛) · 터짐 글자 · 판 흔들기.
    /// 값은 아티팩트 「찹츄 카드 이펙트」 의 PF · burst · bloom · ring · streaks · boomAt · arenaShake · shakeStage · zoomPunch 와 같다.
    /// 시안은 px 단위라 <see cref="PixelScale"/> 를 곱해 이 화면 단위로 바꾼다. 위치는 이 레이어의 로컬 좌표(가운데 0).
    /// 이 오브젝트에 따로 Canvas 를 둔다 — 연출이 매 프레임 움직여도 손패 · 이름표가 있는 Canvas 는 다시 계산되지 않게.
    /// </summary>
    public class FxLayer : MonoBehaviour
    {
        // 시안 PF 의 한 프레임 감속(drag)은 60fps 기준이다
        private const float DragFps = 60f;
        private const float MaxDeltaTime = 0.05f;

        [Tooltip("흔들고 확대할 판. 이 레이어는 판 밖에 있어야 같이 흔들리지 않는다.")]
        [SerializeField] private RectTransform stage;
        [Tooltip("시안의 1px 이 이 화면에서 몇 단위인지.")]
        [SerializeField] private float pixelScale = 1.5f;

        [Header("그림")]
        [Tooltip("빛을 더해서 그리는 머티리얼.")]
        [SerializeField] private Material additive;
        [SerializeField] private Sprite star;
        [SerializeField] private Sprite dot;
        [SerializeField] private Sprite glow;
        [SerializeField] private Sprite streak;
        [Tooltip("터짐 글자 뒤 별 모양 (흰색 — 색을 입힌다).")]
        [SerializeField] private Sprite starburst;
        [SerializeField] private TMP_FontAsset boomFont;
        [Tooltip("잉크(외곽선) 색.")]
        [SerializeField] private Color ink = new Color32(0x2b, 0x28, 0x56, 0xff);

        [Header("파편 (시안 burst) — 길이는 시안 px")]
        [Tooltip("파편 크기 범위 (시안 px, 최소 · 최대) — 부르는 쪽이 정하지 않았을 때.")]
        [SerializeField] private Vector2 burstSize = new Vector2(4f, 10f);
        [Tooltip("파편 수명 범위 (초, 최소 · 최대) — 부르는 쪽이 정하지 않았을 때.")]
        [SerializeField] private Vector2 burstLife = new Vector2(0.6f, 1.1f);
        [Tooltip("파편이 사라지기 시작하는 남은 초.")]
        [SerializeField] private float burstFade = 0.35f;
        [Tooltip("파편이 별일 확률 (나머지는 동그라미).")]
        [SerializeField] private float burstStarChance = 0.5f;
        [Tooltip("파편의 한 프레임(60fps) 감속 — 속도에 곱한다.")]
        [SerializeField] private float burstDrag = 0.94f;
        [Tooltip("파편의 처음 회전 상한 (라디안).")]
        [SerializeField] private float burstMaxRotation = 6f;
        [Tooltip("파편의 회전 속도 상한 (라디안/초) — −값 ~ +값에서 고른다.")]
        [SerializeField] private float burstMaxSpin = 8f;
        [Tooltip("별 · 동그라미 뒤 빛의 크기 — 파편 크기 대비.")]
        [SerializeField] private float sparkHaloScale = 6.4f;

        [Header("번짐 (시안 bloom)")]
        [Tooltip("처음 크기 — 정한 크기 대비.")]
        [SerializeField] private float bloomStartRatio = 0.25f;
        [Tooltip("수명 동안 커지는 크기 — 정한 크기 대비.")]
        [SerializeField] private float bloomGrowRatio = 1.6f;
        [Tooltip("가운데 흰 빛의 크기 — 번짐 크기 대비.")]
        [SerializeField] private float bloomCoreScale = 0.5f;
        [Tooltip("가운데 흰 빛의 투명도 — 번짐 투명도 대비.")]
        [SerializeField] private float bloomCoreAlpha = 0.9f;

        [Header("고리 (시안 ring)")]
        [Tooltip("고리의 처음 반지름 (시안 px).")]
        [SerializeField] private float ringStartRadius = 8f;
        [Tooltip("사라질 때 남는 선 두께의 하한 — 처음 두께 대비.")]
        [SerializeField] private float ringMinWidthRatio = 0.2f;
        [Tooltip("안쪽 흰 선 두께 — 바깥 선 대비.")]
        [SerializeField] private float ringInnerWidthRatio = 0.35f;

        [Header("줄 빛 (시안 streaks)")]
        [Tooltip("줄 빛 수명 범위 (초, 최소 · 최대).")]
        [SerializeField] private Vector2 streakLife = new Vector2(0.25f, 0.45f);
        [Tooltip("줄 빛이 사라지기 시작하는 남은 초.")]
        [SerializeField] private float streakFade = 0.25f;
        [Tooltip("줄 빛의 한 프레임(60fps) 감속 — 속도에 곱한다.")]
        [SerializeField] private float streakDrag = 0.88f;
        [Tooltip("줄 빛 두께 범위 (시안 px, 최소 · 최대).")]
        [SerializeField] private Vector2 streakWidth = new Vector2(3f, 6f);
        [Tooltip("줄 빛 길이 상한 (시안 px).")]
        [SerializeField] private float streakMaxLength = 160f;
        [Tooltip("줄 빛 길이 — 속도에 곱한다 (상한까지).")]
        [SerializeField] private float streakLengthPerSpeed = 0.12f;
        [Tooltip("흰 머리의 길이 — 줄 빛 길이 대비.")]
        [SerializeField] private float streakHeadLength = 0.3f;
        [Tooltip("흰 머리의 두께 — 줄 빛 두께 대비.")]
        [SerializeField] private float streakHeadWidth = 0.7f;

        [Header("터짐 글자 (시안 boomAt · .boom)")]
        [Tooltip("글자 크기 (시안 px). 그림을 쓸 때는 그림 칸 크기.")]
        [SerializeField] private float boomFontSize = 44f;
        [Tooltip("글자 외곽선 두께 (TMP outlineWidth).")]
        [SerializeField] private float boomOutlineWidth = 0.3f;
        [Tooltip("글자 둘레 안쪽 여백 (시안 px, 가로 · 세로) — 양쪽에 같이.")]
        [SerializeField] private Vector2 boomPadding = new Vector2(24f, 16f);
        [Tooltip("뒤 빛이 별 모양 밖으로 퍼지는 거리 (시안 px, 시안 drop-shadow 18px).")]
        [SerializeField] private float boomHaloSpread = 36f;
        [Tooltip("뒤 빛 투명도.")]
        [SerializeField] private float boomHaloAlpha = 0.8f;
        [Tooltip("튀어나왔다 사라지는 초 (시안 boomAt 950ms, ease-out).")]
        [SerializeField] private float boomSeconds = 0.95f;
        [Tooltip("마지막에 떠오르는 높이 — 터짐 글자 높이 대비.")]
        [SerializeField] private float boomRiseRatio = 0.2f;
        [Tooltip("키프레임 시점 (0 ~ 1). 아래 투명도 · 배율 · 각도와 개수가 같아야 한다.")]
        [SerializeField] private float[] boomKeys = { 0f, 0.18f, 0.35f, 0.75f, 1f };
        [Tooltip("키프레임마다 투명도.")]
        [SerializeField] private float[] boomAlphas = { 0f, 1f, 1f, 1f, 0f };
        [Tooltip("키프레임마다 배율.")]
        [SerializeField] private float[] boomScales = { 0.2f, 1.25f, 1f, 1f, 1.1f };
        [Tooltip("키프레임마다 각도 (도, 시안 rotate — + 가 시계 방향).")]
        [SerializeField] private float[] boomAngles = { -25f, 8f, -4f, -4f, -4f };

        [Header("판 흔들기 (시안 arenaShake · shakeStage)")]
        [Tooltip("회전 없이 흔드는 초 (시안 arenaShake 420ms).")]
        [SerializeField] private float shakeSeconds = 0.42f;
        [Tooltip("회전하며 흔드는 초 (시안 shakeStage 440ms).")]
        [SerializeField] private float shakeRotateSeconds = 0.44f;
        [Tooltip("좌우 자리 순서 — 흔들 양 대비.")]
        [SerializeField] private float[] shakeXRatios = { 0f, -1f, 1f, -0.6f, 0.6f, -0.3f, 0f };
        [Tooltip("홀수 단계에서 내려가는 양 — 흔들 양 대비.")]
        [SerializeField] private float shakeDownRatio = 0.4f;
        [Tooltip("짝수 단계에서 올라가는 양 — 흔들 양 대비.")]
        [SerializeField] private float shakeUpRatio = 0.3f;
        [Tooltip("회전하며 흔들 때 좌우로 기우는 각도 (도).")]
        [SerializeField] private float shakeAngle = 0.25f;

        [Header("판 확대 (시안 zoomPunch 300ms)")]
        [Tooltip("커지는 초.")]
        [SerializeField] private float punchInSeconds = 0.075f;
        [Tooltip("돌아오는 초.")]
        [SerializeField] private float punchOutSeconds = 0.225f;

        private readonly List<Particle> _particles = new List<Particle>();
        private readonly object _shakeId = new object();
        private readonly object _punchId = new object();
        private Vector2 _stageBase;

        /// <summary>시안 1px 이 이 화면에서 몇 단위인지.</summary>
        public float PixelScale => pixelScale;

        private enum Shape { Star, Circle, Glow, Streak, Ring }

        private class Particle
        {
            public Shape Shape;
            public RectTransform Root;
            public Graphic Main;
            public Graphic Halo;
            public Graphic Inner;
            public Color Color;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Gravity;
            public float Drag;
            public float Rotation;
            public float Spin;
            public float Size;
            public float SizeGrow;
            public float Radius;
            public float RadiusGrow;
            public float LineWidth;
            public float Life;
            public float MaxLife;
            public float Fade;
        }

        private void Awake()
        {
            if (stage == null || additive == null || star == null || dot == null || glow == null || streak == null || starburst == null
                || boomFont == null)
                Debug.LogError($"[{nameof(FxLayer)}] 참조 중 비어 있는 것이 있습니다.", this);
            if (stage != null)
                _stageBase = stage.anchoredPosition;
        }

        private void Update()
        {
            float dt = Mathf.Min(MaxDeltaTime, Time.deltaTime);
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                Particle p = _particles[i];
                p.Life -= dt;
                if (p.Life <= 0f)
                {
                    Destroy(p.Root.gameObject);
                    _particles.RemoveAt(i);
                    continue;
                }

                Step(p, dt);
                Draw(p);
            }
        }

        private void OnDisable()
        {
            foreach (Particle p in _particles)
            {
                if (p.Root != null)
                    Destroy(p.Root.gameObject);
            }
            _particles.Clear();
        }

        /// <summary>대상 RectTransform 의 가운데를 이 레이어의 로컬 좌표로.</summary>
        public Vector2 CenterOf(RectTransform target)
        {
            return transform.InverseTransformPoint(target.TransformPoint(target.rect.center));
        }

        /// <summary>시안 burst — 별 · 동그라미 파편이 튀고 떨어진다. 값은 시안 px 기준.</summary>
        public void Burst(Vector2 position, int count, Color[] colors, Vector2 speed, float gravity, Vector2? size = null, Vector2? life = null)
        {
            Vector2 sizeRange = size ?? burstSize;
            Vector2 lifeRange = life ?? burstLife;
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float velocity = Random.Range(speed.x, speed.y) * pixelScale;
                Shape shape = Random.value < burstStarChance ? Shape.Star : Shape.Circle;
                Particle p = NewParticle(shape, position, Pick(colors), Random.Range(lifeRange.x, lifeRange.y), burstFade);
                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * velocity;
                p.Gravity = -gravity * pixelScale;
                p.Drag = burstDrag;
                p.Rotation = Random.value * burstMaxRotation;
                p.Spin = Random.Range(-burstMaxSpin, burstMaxSpin);
                p.Size = Random.Range(sizeRange.x, sizeRange.y) * pixelScale;
            }
        }

        /// <summary>시안 bloom — 둥근 빛이 커지며 사라진다.</summary>
        public void Bloom(Vector2 position, Color color, float size = 200f, float life = 0.42f)
        {
            Particle p = NewParticle(Shape.Glow, position, color, life, life);
            p.Size = size * bloomStartRatio * pixelScale;
            p.SizeGrow = size * bloomGrowRatio / life * pixelScale;
        }

        /// <summary>시안 ring — 고리가 커지면서 가늘어진다.</summary>
        public void Ring(Vector2 position, Color color, float maxRadius = 140f, float life = 0.5f, float lineWidth = 10f)
        {
            Particle p = NewParticle(Shape.Ring, position, color, life, life);
            p.Radius = ringStartRadius * pixelScale;
            p.RadiusGrow = maxRadius / life * pixelScale;
            p.LineWidth = lineWidth * pixelScale;
        }

        /// <summary>시안 streaks — 사방으로 짧은 줄 빛이 뻗는다.</summary>
        public void Streaks(Vector2 position, int count, Color[] colors, float minSpeed = 700f, float maxSpeed = 1300f)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                Particle p = NewParticle(Shape.Streak, position, Pick(colors), Random.Range(streakLife.x, streakLife.y), streakFade);
                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minSpeed, maxSpeed) * pixelScale;
                p.Drag = streakDrag;
                p.Size = Random.Range(streakWidth.x, streakWidth.y) * pixelScale;
            }
        }

        /// <summary>시안 boomAt — 별 모양 바탕 위 글자가 튀어나왔다가 위로 사라진다.</summary>
        public void Boom(Vector2 position, Color color, string text)
        {
            RectTransform root = NewBoomRoot(position, color, out RectTransform body);
            TextMeshProUGUI label = NewRect("Text", body).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = boomFont;
            label.text = text;
            label.fontSize = boomFontSize * pixelScale;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.outlineWidth = boomOutlineWidth;
            label.outlineColor = ink;
            Stretch(label.rectTransform);

            // 시안 .boom: 글자 크기 + 안쪽 여백 16 · 24
            Vector2 textSize = label.GetPreferredValues(text);
            root.sizeDelta = new Vector2(textSize.x + boomPadding.x * 2f * pixelScale, boomFontSize * pixelScale + boomPadding.y * 2f * pixelScale);
            AnimateBoom(root, position);
        }

        /// <summary>글자 대신 그림(예: 絶)을 쓰는 boomAt.</summary>
        public void Boom(Vector2 position, Color color, Sprite glyph)
        {
            RectTransform root = NewBoomRoot(position, color, out RectTransform body);
            Image image = NewImage("Glyph", body, glyph, Color.white, false);
            Stretch(image.rectTransform, boomPadding.x * pixelScale, boomPadding.y * pixelScale);
            root.sizeDelta = new Vector2((boomFontSize + boomPadding.x * 2f) * pixelScale, (boomFontSize + boomPadding.y * 2f) * pixelScale);
            AnimateBoom(root, position);
        }

        /// <summary>시안 arenaShake(회전 없음, 420ms) · shakeStage(회전 있음, 440ms) — 판을 정해진 순서로 흔든다.</summary>
        public void ShakeStage(float amount, bool rotate)
        {
            if (stage == null)
                return;

            float a = amount * pixelScale;
            float[] xs = new float[shakeXRatios.Length];
            for (int n = 0; n < xs.Length; n++)
                xs[n] = a * shakeXRatios[n];
            DOTween.Kill(_shakeId);
            stage.anchoredPosition = _stageBase;
            stage.localRotation = Quaternion.identity;
            DOVirtual.Float(0f, 1f, rotate ? shakeRotateSeconds : shakeSeconds, t =>
            {
                float k = t * (xs.Length - 1);
                int i = Mathf.Min(xs.Length - 2, Mathf.FloorToInt(k));
                float f = k - i;
                // 시안은 y 가 아래로 + — 위로 + 인 유니티에서는 부호를 뒤집는다
                float y0 = i % 2 == 1 ? -a * shakeDownRatio : a * shakeUpRatio;
                float y1 = (i + 1) % 2 == 1 ? -a * shakeDownRatio : a * shakeUpRatio;
                stage.anchoredPosition = _stageBase + new Vector2(Mathf.Lerp(xs[i], xs[i + 1], f), Mathf.Lerp(y0, y1, f));
                if (rotate)
                    stage.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(i % 2 == 1 ? -shakeAngle : shakeAngle, (i + 1) % 2 == 1 ? -shakeAngle : shakeAngle, f));
            }).SetEase(Ease.OutQuad).SetId(_shakeId).SetLink(stage.gameObject).OnComplete(() =>
            {
                stage.anchoredPosition = _stageBase;
                stage.localRotation = Quaternion.identity;
            });
        }

        /// <summary>시안 zoomPunch — 판이 잠깐 커졌다 돌아온다 (300ms).</summary>
        public void PunchStage(float amount)
        {
            if (stage == null)
                return;

            DOTween.Kill(_punchId, true);
            stage.localScale = Vector3.one;
            DOTween.Sequence().SetId(_punchId).SetLink(stage.gameObject)
                .Append(stage.DOScale(1f + amount, punchInSeconds).SetEase(Ease.OutCubic))
                .Append(stage.DOScale(1f, punchOutSeconds).SetEase(Ease.OutCubic));
        }

        #region 파티클

        private Particle NewParticle(Shape shape, Vector2 position, Color color, float life, float fade)
        {
            RectTransform root = NewRect(shape.ToString(), transform);
            var p = new Particle { Shape = shape, Root = root, Color = color, Position = position, Life = life, MaxLife = life, Fade = fade, Drag = 1f };

            switch (shape)
            {
                case Shape.Star:
                    // 시안: 빛(glowy) 을 더하고, 별은 잉크 테두리로 그냥 그린다
                    p.Halo = NewImage("Halo", root, glow, color, true);
                    p.Main = NewImage("Star", root, star, color, false);
                    break;
                case Shape.Circle:
                    p.Halo = NewImage("Halo", root, glow, color, true);
                    p.Main = NewImage("Dot", root, dot, color, true);
                    break;
                case Shape.Glow:
                    p.Halo = NewImage("Glow", root, glow, color, true);
                    p.Main = NewImage("Core", root, glow, Color.white, true);
                    break;
                case Shape.Streak:
                    p.Main = NewImage("Streak", root, streak, color, true);
                    p.Main.rectTransform.pivot = new Vector2(1f, 0.5f);
                    p.Inner = NewImage("Head", root, streak, Color.white, true);
                    p.Inner.rectTransform.pivot = new Vector2(1f, 0.5f);
                    break;
                case Shape.Ring:
                    p.Main = NewRing("Ring", root, color);
                    p.Inner = NewRing("Inner", root, Color.white);
                    break;
            }

            _particles.Add(p);
            Draw(p);
            return p;
        }

        private static void Step(Particle p, float dt)
        {
            p.Radius += p.RadiusGrow * dt;
            p.Size += p.SizeGrow * dt;
            if (p.Shape == Shape.Ring || p.Shape == Shape.Glow)
                return;

            float drag = Mathf.Pow(p.Drag, dt * DragFps);
            p.Velocity = new Vector2(p.Velocity.x * drag, p.Velocity.y * drag + p.Gravity * dt);
            p.Position += p.Velocity * dt;
            p.Rotation += p.Spin * dt;
        }

        private void Draw(Particle p)
        {
            float alpha = Mathf.Min(1f, p.Life / p.Fade);
            p.Root.anchoredPosition = p.Position;

            switch (p.Shape)
            {
                case Shape.Star:
                case Shape.Circle:
                    SetSize(p.Halo, p.Size * sparkHaloScale, alpha);
                    SetSize(p.Main, p.Size * 2f, alpha);
                    // 시안은 라디안, 시계 방향 + — 유니티는 반시계 방향 +
                    p.Main.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -p.Rotation * Mathf.Rad2Deg);
                    break;
                case Shape.Glow:
                    SetSize(p.Halo, p.Size * 2f, alpha);
                    SetSize(p.Main, p.Size * bloomCoreScale, alpha * bloomCoreAlpha);
                    break;
                case Shape.Streak:
                {
                    // 시안: 진행 방향 뒤로 min(160, 속도 × 0.12) 길이, 머리는 흰색
                    float length = Mathf.Min(streakMaxLength * pixelScale, p.Velocity.magnitude * streakLengthPerSpeed);
                    Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg);
                    p.Main.rectTransform.localRotation = rotation;
                    p.Main.rectTransform.sizeDelta = new Vector2(length, p.Size);
                    SetAlpha(p.Main, alpha);
                    p.Inner.rectTransform.localRotation = rotation;
                    p.Inner.rectTransform.sizeDelta = new Vector2(length * streakHeadLength, p.Size * streakHeadWidth);
                    SetAlpha(p.Inner, alpha);
                    break;
                }
                case Shape.Ring:
                {
                    float width = p.LineWidth * Mathf.Max(ringMinWidthRatio, p.Life / p.MaxLife);
                    ((UIRing)p.Main).Set(p.Radius, width);
                    ((UIRing)p.Inner).Set(p.Radius, Mathf.Max(pixelScale, width * ringInnerWidthRatio));
                    SetAlpha(p.Main, alpha);
                    SetAlpha(p.Inner, alpha);
                    break;
                }
            }
        }

        #endregion

        #region 터짐 글자

        private RectTransform NewBoomRoot(Vector2 position, Color color, out RectTransform body)
        {
            RectTransform root = NewRect("Boom", transform);
            root.anchoredPosition = position;

            // 시안 .boom 의 drop-shadow(0 0 18px 색)
            Image halo = NewImage("Halo", root, glow, color, true);
            Stretch(halo.rectTransform, -boomHaloSpread * pixelScale, -boomHaloSpread * pixelScale);
            SetAlpha(halo, boomHaloAlpha);
            Image burst = NewImage("Starburst", root, starburst, color, false);
            Stretch(burst.rectTransform);

            body = root;
            return root;
        }

        // 시안 boomAt 키프레임 (950ms, ease-out): 0 → .18 → .35 → .75 → 1 — 값은 위 「터짐 글자」 필드
        private void AnimateBoom(RectTransform root, Vector2 position)
        {
            CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            // 마지막 키프레임에서만 떠오른다
            float[] rises = new float[boomKeys.Length];
            rises[rises.Length - 1] = root.sizeDelta.y * boomRiseRatio;

            DOVirtual.Float(0f, 1f, boomSeconds, t =>
            {
                int i = 0;
                while (i < boomKeys.Length - 2 && t > boomKeys[i + 1])
                    i++;
                float f = Mathf.InverseLerp(boomKeys[i], boomKeys[i + 1], t);
                group.alpha = Mathf.Lerp(boomAlphas[i], boomAlphas[i + 1], f);
                root.localScale = Vector3.one * Mathf.Lerp(boomScales[i], boomScales[i + 1], f);
                root.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(boomAngles[i], boomAngles[i + 1], f));
                root.anchoredPosition = position + new Vector2(0f, Mathf.Lerp(rises[i], rises[i + 1], f));
            }).SetEase(Ease.OutQuad).SetLink(root.gameObject).OnComplete(() => Destroy(root.gameObject));
        }

        #endregion

        #region 만들기

        private Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool light)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = true;
            if (light)
                image.material = additive;
            return image;
        }

        private UIRing NewRing(string name, Transform parent, Color color)
        {
            // 직접 만든 Graphic 은 CanvasRenderer 가 바로 붙지 않는다 — 먼저 붙여야 cullTransparentMesh 를 쓸 수 있다
            GameObject go = NewRect(name, parent).gameObject;
            go.AddComponent<CanvasRenderer>();
            UIRing ring = go.AddComponent<UIRing>();
            ring.color = color;
            ring.material = additive;
            ring.raycastTarget = false;
            ring.canvasRenderer.cullTransparentMesh = true;
            return ring;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void SetSize(Graphic graphic, float size, float alpha)
        {
            graphic.rectTransform.sizeDelta = new Vector2(size, size);
            SetAlpha(graphic, alpha);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private static Color Pick(Color[] colors) => colors[Random.Range(0, colors.Length)];

        #endregion
    }
}
