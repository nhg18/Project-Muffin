using System.Collections.Generic;
using Chapchu.Core;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 4 단계(후처리) 공통 연출 — 결과가 같으면 어느 카드든 같은 연출 (10-ui 5-1 「4 단계 공통 연출」).
    /// 아티팩트 「4단계 공통 연출」 (V2CdpFSigVdDAiP95CZiGJ, 게시본 1795328736-a07a) 의 boomAt · burst · pshake · arenaShake · chipPop ·
    /// hit · heal · gain · lose · zap · fallout 을 옮겼다 (miss 「없음」 은 게임이 알려 주는 일이 없어 뺐다). 터지는 글자 = 색 별 + 잉크 테, 입자도 잉크 테 — 빛 · 떨어진 그림자 · 반투명 없이 작아지며 사라진다.
    /// 수치가 있는 결과는 수치에 비례해 별 · 입자 · 흔들림이 커진다 (HP 1 ~ 50, 장수 1 ~ 6 — 넘으면 최대).
    /// 시안은 px 단위라 <see cref="pixelScale"/> 를 곱해 이 화면 단위로 바꾼다. 결과마다 효과음 하나 (SoundManager, 임시) — 피해 · 피폭은 양이 많을수록 크게.
    /// 이 오브젝트에 따로 Canvas 를 둔다 — 매 프레임 움직여도 HUD 가 있는 Canvas 는 다시 계산되지 않게.
    /// </summary>
    public class ResultFx : MonoBehaviour
    {
        private const string SeatShakeId = "ResultFx.Seat.";
        private const string ChipPopId = "ResultFx.Chip.";
        private const float MinHitVolume = 0.7f; // 피해 1 의 효과음 음량 — 최대 피해(hpRange.y)면 1

        // 시안 CSS ease-out = cubic-bezier(0, 0, .58, 1)
        private static readonly Vector4 EaseOut = new Vector4(0f, 0f, 0.58f, 1f);

        /// <summary>결과가 그려질 자리 하나 — 터짐 · 입자는 Body 가운데, 흔들 것들, 손패 장수 칩(없으면 null).</summary>
        public readonly struct Seat
        {
            public readonly RectTransform Body;
            public readonly RectTransform[] Shake;
            public readonly RectTransform Chip;
            public readonly Graphic ChipFace;

            public Seat(RectTransform body, RectTransform[] shake, RectTransform chip = null, Graphic chipFace = null)
            {
                Body = body;
                Shake = shake;
                Chip = chip;
                ChipFace = chipFace;
            }
        }

        [Tooltip("시안의 1px 이 이 화면에서 몇 단위인지 (1920 화면 = 시안 판 960 × 2, 시안 연출 페이지는 판보다 1.28 배 — 2 × 0.78).")]
        [SerializeField] private float pixelScale = 1.56f;
        [Tooltip("피해 때 흔들 판. 이 오브젝트는 판 밖에 있어야 같이 흔들리지 않는다.")]
        [SerializeField] private RectTransform stage;
        [Tooltip("입자를 그리는 자식 — 이 오브젝트를 가득 채운다.")]
        [SerializeField] private InkParticles particles;
        [Tooltip("터지는 글자 글꼴 (시안 --f-display = 태백체).")]
        [SerializeField] private TMP_FontAsset boomFont;
        [Tooltip("터지는 글자 머티리얼 — 흰 글자 + 잉크 테 (시안 -webkit-text-stroke).")]
        [SerializeField] private Material boomMaterial;
        [Tooltip("잉크(테) 색.")]
        [SerializeField] private Color ink = new Color32(0x2a, 0x24, 0x20, 0xff);

        [Header("세기 — 수치를 0 ~ 1 로 (시안 AMP · GROW)")]
        [Tooltip("HP 수치 범위 (최소 → 0, 최대 → 1).")]
        [SerializeField] private Vector2 hpRange = new Vector2(1f, 50f);
        [Tooltip("장수 범위 (최소 → 0, 최대 → 1).")]
        [SerializeField] private Vector2 cardRange = new Vector2(1f, 6f);
        [Tooltip("세기 0 → 1 일 때 별(글자) 배율.")]
        [SerializeField] private Vector2 starGrow = new Vector2(0.75f, 1.5f);
        [Tooltip("세기 0 → 1 일 때 입자 크기 배율.")]
        [SerializeField] private Vector2 sizeGrow = new Vector2(0.8f, 1.5f);
        [Tooltip("세기 0 → 1 일 때 입자 속도 배율.")]
        [SerializeField] private Vector2 speedGrow = new Vector2(0.85f, 1.3f);

        [Header("터지는 글자 (시안 boomAt · .boom)")]
        [Tooltip("글자 크기 (시안 px) — 세기에 따른 별 배율을 곱한다.")]
        [SerializeField] private float boomFontSize = 44f;
        [Tooltip("글자 둘레 여백 — 글자 크기 대비 (시안 padding .55em .36em).")]
        [SerializeField] private Vector2 boomPadding = new Vector2(0.55f, 0.36f);
        [Tooltip("별이 글자 칸 밖으로 나오는 폭 (시안 px, inset -4px).")]
        [SerializeField] private float boomStarOutset = 4f;
        [Tooltip("별 테 두께 (시안 px, stroke-width 4).")]
        [SerializeField] private float boomStarBorder = 4f;
        [Tooltip("자리 가운데에서 떨어지는 범위 (시안 px — 가로 ±x, 위로 y ~ z, 위로는 별 배율을 곱한다).")]
        [SerializeField] private Vector3 boomJitter = new Vector3(30f, 30f, 60f);
        [Tooltip("튀어나왔다 사라지는 초 (시안 950ms, ease-out).")]
        [SerializeField] private float boomSeconds = 0.95f;
        [Tooltip("키프레임 시점 (0 ~ 1). 아래 배율 · 각도 · 높이와 개수가 같아야 한다.")]
        [SerializeField] private float[] boomKeys = { 0f, 0.18f, 0.35f, 0.75f, 0.86f, 1f };
        [SerializeField] private float[] boomScales = { 0.2f, 1.25f, 1f, 1f, 1.12f, 0f };
        [Tooltip("키프레임마다 각도 (도, 시안 rotate — + 가 시계 방향).")]
        [SerializeField] private float[] boomAngles = { -25f, 8f, -4f, -4f, -4f, -4f };
        [Tooltip("키프레임마다 위로 뜨는 높이 — 글자 칸 높이 대비 (시안 translate -62% · -70%).")]
        [SerializeField] private float[] boomRises = { 0f, 0f, 0f, 0f, 0.12f, 0.2f };
        [Tooltip("별이 위로 잘리지 않게 — 칸 높이의 이 비율만큼은 화면 위 끝에서 떨어뜨린다.")]
        [SerializeField] private float boomTopMargin = 0.65f;

        [Header("입자 (시안 burst) — 길이는 시안 px")]
        [SerializeField] private Vector2 burstSize = new Vector2(4f, 10f);
        [Tooltip("수명 범위 (초).")]
        [SerializeField] private Vector2 burstLife = new Vector2(0.6f, 1.1f);
        [Tooltip("작아지기 시작하는 남은 초.")]
        [SerializeField] private float burstFade = 0.35f;
        [Tooltip("한 프레임(60fps) 감속 — 속도에 곱한다.")]
        [SerializeField] private float burstDrag = 0.94f;
        [Tooltip("처음 회전 상한 (라디안).")]
        [SerializeField] private float burstMaxRotation = 6f;
        [Tooltip("회전 속도 상한 (라디안/초).")]
        [SerializeField] private float burstMaxSpin = 8f;
        [Tooltip("입자 테 두께 (시안 px, lineWidth 2).")]
        [SerializeField] private float particleBorder = 2f;

        [Header("자리 흔들기 (시안 pshake) · 판 흔들기 (시안 arenaShake 420ms)")]
        [Tooltip("자리 흔들기 초 — 세기 배율 0 → 1.6 일 때 (시안 320 ~ 460ms).")]
        [SerializeField] private Vector2 seatShakeSeconds = new Vector2(0.32f, 0.46f);
        [Tooltip("키프레임마다 가로 이동 (시안 px, 0 · 20 · 40 · 60 · 80 · 100%) — 배율을 곱한다.")]
        [SerializeField] private float[] seatShakeXs = { 0f, -9f, 9f, -6f, 4f, 0f };
        [Tooltip("키프레임마다 각도 (도, + 가 시계 방향) — 배율을 곱한다.")]
        [SerializeField] private float[] seatShakeAngles = { 0f, -3f, 3f, 0f, 0f, 0f };
        [SerializeField] private float stageShakeSeconds = 0.42f;
        [Tooltip("좌우 순서 — 흔들 양 대비.")]
        [SerializeField] private float[] stageShakeXs = { 0f, -1f, 1f, -0.6f, 0.6f, -0.3f, 0f };
        [Tooltip("짝수 단계에서 올라가는 양 · 홀수 단계에서 내려가는 양 — 흔들 양 대비.")]
        [SerializeField] private Vector2 stageShakeYs = new Vector2(0.3f, 0.4f);

        [Header("손패 장수 칩 (시안 chipPop 420ms)")]
        [SerializeField] private float chipPopSeconds = 0.42f;
        [SerializeField] private float chipPopScale = 1.5f;
        [Tooltip("가장 커지는 시점 (0 ~ 1).")]
        [SerializeField] private float chipPopPeak = 0.35f;

        [Header("피해 (시안 hit)")]
        [SerializeField] private Color hitColor = new Color32(0xff, 0x4f, 0x2e, 0xff);
        [SerializeField] private Color[] hitColors = { new Color32(0xff, 0x4f, 0x2e, 0xff), new Color32(0xff, 0xd2, 0x3f, 0xff), Color.white };
        [Tooltip("입자 수 (세기 0 · 1).")]
        [SerializeField] private Vector2Int hitCount = new Vector2Int(12, 56);
        [SerializeField] private Vector2 hitSpeed = new Vector2(200f, 520f);
        [Tooltip("중력 (시안 px/s², 아래로 +).")]
        [SerializeField] private float hitGravity = 500f;
        [Tooltip("자리 흔들기 배율 (세기 0 · 1).")]
        [SerializeField] private Vector2 hitSeatShake = new Vector2(0.6f, 1.6f);
        [Tooltip("판 흔들 양 (시안 px, 세기 0 · 1).")]
        [SerializeField] private Vector2 hitStageShake = new Vector2(3f, 18f);

        [Header("회복 (시안 heal)")]
        [SerializeField] private Color healColor = new Color32(0x3d, 0xdc, 0x84, 0xff);
        [SerializeField] private Color[] healColors = { new Color32(0x3d, 0xdc, 0x84, 0xff), new Color32(0xff, 0x5c, 0x8a, 0xff), Color.white };
        [SerializeField] private Vector2Int healCount = new Vector2Int(10, 44);
        [SerializeField] private Vector2 healSpeed = new Vector2(120f, 320f);
        [Tooltip("중력 — − 면 위로 떠오른다.")]
        [SerializeField] private float healGravity = -80f;

        [Header("카드 얻기 · 잃기 (시안 gain · lose)")]
        [SerializeField] private Color gainColor = new Color32(0x1a, 0x8f, 0xe0, 0xff);
        [SerializeField] private Color[] gainColors = { new Color32(0x1a, 0x8f, 0xe0, 0xff), new Color32(0xff, 0xd2, 0x3f, 0xff), Color.white };
        [Tooltip("얻을 때 장수 칩이 잠깐 바뀌는 면 색.")]
        [SerializeField] private Color gainChipColor = new Color32(0xd6, 0xef, 0xff, 0xff);
        [SerializeField] private Vector2Int gainCount = new Vector2Int(8, 34);
        [SerializeField] private Vector2 gainSpeed = new Vector2(140f, 340f);
        [SerializeField] private float gainGravity = -60f;
        [SerializeField] private Color loseColor = new Color32(0x8b, 0x5c, 0xf6, 0xff);
        [SerializeField] private Color[] loseColors = { new Color32(0x8b, 0x5c, 0xf6, 0xff), Color.white };
        [SerializeField] private Color loseChipColor = new Color32(0xee, 0xe6, 0xff, 0xff);
        [SerializeField] private Vector2Int loseCount = new Vector2Int(6, 28);
        [SerializeField] private Vector2 loseSpeed = new Vector2(120f, 300f);
        [SerializeField] private float loseGravity = 700f;
        [SerializeField] private Vector2 loseSeatShake = new Vector2(0.6f, 1.4f);
        [Tooltip("카드 입자 크기 (시안 px).")]
        [SerializeField] private Vector2 cardParticleSize = new Vector2(5f, 9f);

        [Header("피폭 (시안 zap · fallout · .vign)")]
        [SerializeField] private Color zapColor = new Color32(0xd9, 0x46, 0xef, 0xff);
        [SerializeField] private Color[] zapColors = { new Color32(0xd9, 0x46, 0xef, 0xff), new Color32(0xf5, 0xd0, 0xfe, 0xff), Color.white };
        [SerializeField] private Vector2Int zapCount = new Vector2Int(8, 32);
        [SerializeField] private Vector2 zapSpeed = new Vector2(160f, 380f);
        [SerializeField] private float zapGravity = 300f;
        [Tooltip("화면 가장자리 번짐 색 (시안 rgba(217,70,239,.5)).")]
        [SerializeField] private Color vignetteColor = new Color(0.851f, 0.275f, 0.937f, 0.5f);
        [Tooltip("가장자리에서 투명해질 때까지의 폭 (시안 px — inset 140px 번짐 + 50px).")]
        [SerializeField] private float vignetteWidth = 120f;
        [SerializeField] private float vignetteSeconds = 0.8f;
        [Tooltip("가장 짙어지는 시점 (0 ~ 1).")]
        [SerializeField] private float vignettePeak = 0.3f;

        private static readonly InkParticles.ParticleShape[] StarCircle = { InkParticles.ParticleShape.Star, InkParticles.ParticleShape.Circle };
        private static readonly InkParticles.ParticleShape[] HeartStar = { InkParticles.ParticleShape.Heart, InkParticles.ParticleShape.Star };
        private static readonly InkParticles.ParticleShape[] CardStar = { InkParticles.ParticleShape.Card, InkParticles.ParticleShape.Star };
        private static readonly InkParticles.ParticleShape[] CardOnly = { InkParticles.ParticleShape.Card };

        private readonly object _stageShakeId = new object();
        private Vector2 _stageBase;
        private bool _stageShaking;
        private int _stageShakeFrame = -1;
        private float _stageShakeAmount;

        private void Awake()
        {
            if (stage == null || particles == null || boomFont == null || boomMaterial == null)
                Debug.LogError($"[{nameof(ResultFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            if (particles != null)
                particles.SetBorder(particleBorder * pixelScale, ink);
        }

        private void OnDisable()
        {
            DOTween.Kill(_stageShakeId, true);
            if (particles != null)
                particles.Clear();
        }

        #region 결과

        /// <summary>시안 hit — 「-n」 빨간 별 · 파편 · 자리 흔들림 · 판 흔들림. 모두 피해량에 비례. HP 숫자는 부르는 쪽이 바꾼다.</summary>
        public void Hit(Seat seat, int amount)
        {
            float k = Strength(amount, hpRange);
            SoundManager.Play(Sfx.Hit, Mathf.Lerp(MinHitVolume, 1f, k));
            Vector2 c = CenterOf(seat.Body);
            Boom(c, hitColor, "-" + amount, k);
            Burst(c, k, hitCount, hitColors, StarCircle, hitSpeed, hitGravity, burstSize);
            ShakeSeat(seat, Mathf.Lerp(hitSeatShake.x, hitSeatShake.y, k));
            ShakeStage(Mathf.Lerp(hitStageShake.x, hitStageShake.y, k));
        }

        /// <summary>시안 heal — 「+n」 초록 별 · 위로 떠오르는 하트 · 별.</summary>
        public void Heal(Seat seat, int amount)
        {
            float k = Strength(amount, hpRange);
            SoundManager.Play(Sfx.Heal);
            Vector2 c = CenterOf(seat.Body);
            Boom(c, healColor, "+" + amount, k);
            Burst(c, k, healCount, healColors, HeartStar, healSpeed, healGravity, burstSize);
        }

        /// <summary>시안 gain — 「+n장」 파란 별 · 카드 · 별 입자 · 장수 칩 톡. 장수는 부르는 쪽이 먼저 바꾼다.</summary>
        public void Gain(Seat seat, int count)
        {
            if (count <= 0)
                return;
            float k = Strength(count, cardRange);
            SoundManager.Play(Sfx.CardGain);
            Vector2 c = CenterOf(seat.Body);
            Boom(c, gainColor, "+" + count + "장", k);
            Burst(c, k, gainCount, gainColors, CardStar, gainSpeed, gainGravity, cardParticleSize);
            PopChip(seat, gainChipColor);
        }

        /// <summary>시안 lose — 「−n장」 보라 별 · 떨어지는 카드 입자 · 장수 칩 톡 · 자리 흔들림.</summary>
        public void Lose(Seat seat, int count)
        {
            if (count <= 0)
                return;
            float k = Strength(count, cardRange);
            SoundManager.Play(Sfx.CardLose);
            Vector2 c = CenterOf(seat.Body);
            Boom(c, loseColor, "−" + count + "장", k);
            Burst(c, k, loseCount, loseColors, CardOnly, loseSpeed, loseGravity, cardParticleSize);
            PopChip(seat, loseChipColor);
            ShakeSeat(seat, Mathf.Lerp(loseSeatShake.x, loseSeatShake.y, k));
        }

        /// <summary>시안 zap — 피폭 피해. 피해와 같은 짜임, 색만 자주.</summary>
        public void Zap(Seat seat, int amount)
        {
            float k = Strength(amount, hpRange);
            SoundManager.Play(Sfx.Zap, Mathf.Lerp(MinHitVolume, 1f, k));
            Vector2 c = CenterOf(seat.Body);
            Boom(c, zapColor, "-" + amount, k);
            Burst(c, k, zapCount, zapColors, StarCircle, zapSpeed, zapGravity, burstSize);
            ShakeSeat(seat, Mathf.Lerp(hitSeatShake.x, hitSeatShake.y, k));
            ShakeStage(Mathf.Lerp(hitStageShake.x, hitStageShake.y, k));
        }

        /// <summary>시안 fallout 의 .vign — 화면 가장자리가 자주로 번졌다 걷힌다 (0.8초) = 「판에서 오는 피해」. 피폭 피해(Zap)와 같이 부른다.</summary>
        public void FalloutGlow()
        {
            RectTransform rect = NewRect("FalloutGlow", transform);
            Stretch(rect);
            rect.SetAsFirstSibling(); // 입자 · 글자 뒤
            EdgeGlow glow = rect.gameObject.AddComponent<EdgeGlow>();
            glow.raycastTarget = false;
            glow.SetWidth(vignetteWidth * pixelScale);
            Color full = vignetteColor;
            glow.color = new Color(full.r, full.g, full.b, 0f);
            DOVirtual.Float(0f, 1f, vignetteSeconds, t =>
            {
                float a = t < vignettePeak ? t / vignettePeak : 1f - (t - vignettePeak) / (1f - vignettePeak);
                glow.color = new Color(full.r, full.g, full.b, full.a * a);
            }).SetEase(Ease.Linear).SetLink(rect.gameObject).OnComplete(() => Destroy(rect.gameObject));
        }

        #endregion

        #region 공용 — 터짐 · 입자 · 흔들기

        /// <summary>시안 boomAt — 손그림 색 별 위 흰 글자(잉크 테)가 튀어나왔다가 위로 작아지며 사라진다. strength 0 ~ 1 = 별 배율.</summary>
        public void Boom(Vector2 center, Color color, string text, float strength)
        {
            float m = Mathf.Lerp(starGrow.x, starGrow.y, strength);
            float fontSize = boomFontSize * m * pixelScale;

            RectTransform root = NewRect("Boom", transform);
            InkStar star = NewRect("Star", root).gameObject.AddComponent<InkStar>();
            star.raycastTarget = false;
            star.color = color;
            star.Set(boomStarBorder * pixelScale, ink);
            Stretch(star.rectTransform, -boomStarOutset * pixelScale);

            TextMeshProUGUI label = NewRect("Text", root).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = boomFont;
            label.fontSharedMaterial = boomMaterial;
            label.text = text;
            label.fontSize = fontSize;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            Stretch(label.rectTransform);

            // 시안 .boom: 글자 칸 + 여백 .55em · .36em, 줄 높이 1
            float width = label.GetPreferredValues(text).x + boomPadding.x * 2f * fontSize;
            float height = fontSize * (1f + boomPadding.y * 2f);
            root.sizeDelta = new Vector2(width, height);

            Vector2 position = center + new Vector2(Random.Range(-boomJitter.x, boomJitter.x), Random.Range(boomJitter.y, boomJitter.z) * m) * pixelScale;
            float top = ((RectTransform)transform).rect.yMax - height * boomTopMargin; // 큰 별이 화면 위로 잘리지 않게
            position.y = Mathf.Min(position.y, top);
            root.anchoredPosition = position;
            root.localScale = Vector3.one * boomScales[0];
            root.localRotation = Quaternion.Euler(0f, 0f, -boomAngles[0]);

            DOVirtual.Float(0f, 1f, boomSeconds, t =>
            {
                float p = FxLayer.CubicBezier(EaseOut, t);
                Key(boomKeys, p, out int i, out float f);
                root.localScale = Vector3.one * Mathf.Lerp(boomScales[i], boomScales[i + 1], f);
                root.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(boomAngles[i], boomAngles[i + 1], f));
                root.anchoredPosition = position + new Vector2(0f, Mathf.Lerp(boomRises[i], boomRises[i + 1], f) * height);
            }).SetEase(Ease.Linear).SetLink(root.gameObject).OnComplete(() => Destroy(root.gameObject));
        }

        // 시안 burst + pOpt — 입자 수 · 크기 · 속도가 세기에 비례. 값은 시안 px
        private void Burst(Vector2 position, float strength, Vector2Int count, Color[] colors, InkParticles.ParticleShape[] shapes,
            Vector2 speed, float gravity, Vector2 size)
        {
            int n = Mathf.RoundToInt(Mathf.Lerp(count.x, count.y, strength));
            float sm = Mathf.Lerp(sizeGrow.x, sizeGrow.y, strength);
            float vm = Mathf.Lerp(speedGrow.x, speedGrow.y, strength);
            for (int i = 0; i < n; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float velocity = Random.Range(speed.x, speed.y) * vm * pixelScale;
                particles.Add(new InkParticles.Particle
                {
                    Shape = shapes[Random.Range(0, shapes.Length)],
                    Color = colors[Random.Range(0, colors.Length)],
                    Position = position,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * velocity,
                    Gravity = gravity * pixelScale,
                    Drag = burstDrag,
                    Rotation = Random.value * burstMaxRotation,
                    Spin = Random.Range(-burstMaxSpin, burstMaxSpin),
                    Size = Random.Range(size.x, size.y) * sm * pixelScale,
                    Life = Random.Range(burstLife.x, burstLife.y),
                    Fade = burstFade,
                });
            }
        }

        // 시안 pshake — 자리를 좌우로 흔들고 기울였다 제자리로. multiplier 가 클수록 크고 길게 (시간은 배율 1.6 에서 최대)
        private void ShakeSeat(Seat seat, float multiplier)
        {
            if (seat.Shake == null)
                return;
            float seconds = Mathf.Lerp(seatShakeSeconds.x, seatShakeSeconds.y, Mathf.Min(1f, multiplier / 1.6f));
            foreach (RectTransform target in seat.Shake)
            {
                if (target == null)
                    continue;
                // 같은 자리를 연달아 흔들면 앞 흔들기를 제자리로 끝내고 다시 — 자리의 다른 트윈은 건드리지 않게 따로 이름을 붙인다
                string id = SeatShakeId + target.GetInstanceID();
                DOTween.Kill(id, true);
                Vector2 start = target.anchoredPosition;
                Quaternion rotation = target.localRotation;
                DOVirtual.Float(0f, 1f, seconds, t =>
                {
                    Steps(seatShakeXs.Length, t, out int i, out float f);
                    target.anchoredPosition = start + new Vector2(Mathf.Lerp(seatShakeXs[i], seatShakeXs[i + 1], f) * multiplier * pixelScale, 0f);
                    target.localRotation = rotation * Quaternion.Euler(0f, 0f, -Mathf.Lerp(seatShakeAngles[i], seatShakeAngles[i + 1], f) * multiplier);
                }).SetEase(Ease.Linear).SetId(id).SetLink(target.gameObject).OnKill(() =>
                {
                    target.anchoredPosition = start;
                    target.localRotation = rotation;
                });
            }
        }

        // 시안 arenaShake — 판을 정해진 순서로 흔든다 (420ms, ease-out). 한 프레임에 여럿이 맞으면(모두 피해 · 피폭) 가장 큰 것 하나만
        private void ShakeStage(float amount)
        {
            if (stage == null)
                return;
            if (_stageShakeFrame == Time.frameCount && amount <= _stageShakeAmount)
                return;
            _stageShakeFrame = Time.frameCount;
            _stageShakeAmount = amount;

            DOTween.Kill(_stageShakeId, true); // 제자리로 돌려놓고 다시
            _stageBase = stage.anchoredPosition;
            _stageShaking = true;
            float a = amount * pixelScale;
            DOVirtual.Float(0f, 1f, stageShakeSeconds, t =>
            {
                float p = FxLayer.CubicBezier(EaseOut, t);
                Steps(stageShakeXs.Length, p, out int i, out float f);
                // 시안은 y 가 아래로 + (짝수 −0.3a = 위, 홀수 +0.4a = 아래) — 위로 + 인 유니티에서는 부호를 뒤집는다
                float y0 = i % 2 == 1 ? -a * stageShakeYs.y : a * stageShakeYs.x;
                float y1 = (i + 1) % 2 == 1 ? -a * stageShakeYs.y : a * stageShakeYs.x;
                stage.anchoredPosition = _stageBase + new Vector2(Mathf.Lerp(stageShakeXs[i], stageShakeXs[i + 1], f) * a, Mathf.Lerp(y0, y1, f));
            }).SetEase(Ease.Linear).SetId(_stageShakeId).SetLink(stage.gameObject).OnKill(() =>
            {
                if (!_stageShaking)
                    return;
                _stageShaking = false;
                stage.anchoredPosition = _stageBase;
            });
        }

        // 시안 chipPop — 손패 장수 칩이 커졌다 돌아오고, 그 사이 면이 잠깐 결과 색 (420ms, ease-out)
        private void PopChip(Seat seat, Color flash)
        {
            RectTransform chip = seat.Chip;
            if (chip == null)
                return;
            string id = ChipPopId + chip.GetInstanceID();
            DOTween.Kill(id, true);
            Graphic face = seat.ChipFace;
            Color baseColor = face != null ? face.color : Color.white;
            Vector3 baseScale = chip.localScale;
            DOVirtual.Float(0f, 1f, chipPopSeconds, t =>
            {
                float p = FxLayer.CubicBezier(EaseOut, t);
                float f = p < chipPopPeak ? p / chipPopPeak : 1f - (p - chipPopPeak) / (1f - chipPopPeak);
                chip.localScale = baseScale * Mathf.Lerp(1f, chipPopScale, f);
                if (face != null)
                    face.color = Color.Lerp(baseColor, flash, f);
            }).SetEase(Ease.Linear).SetId(id).SetLink(chip.gameObject).OnKill(() =>
            {
                chip.localScale = baseScale;
                if (face != null)
                    face.color = baseColor;
            });
        }

        #endregion

        #region 도우미

        private float Strength(float value, Vector2 range) => Mathf.Clamp01((value - range.x) / (range.y - range.x));

        private Vector2 CenterOf(RectTransform target)
        {
            if (target == null)
                return Vector2.zero;
            return transform.InverseTransformPoint(target.TransformPoint(target.rect.center));
        }

        // 같은 간격 키프레임 n 개에서 t(0 ~ 1)가 있는 칸 i 와 칸 안 비율 f
        private static void Steps(int n, float t, out int i, out float f)
        {
            float k = Mathf.Clamp01(t) * (n - 1);
            i = Mathf.Min(n - 2, Mathf.FloorToInt(k));
            f = k - i;
        }

        // 키프레임 시점 keys 에서 t 가 있는 칸 i 와 칸 안 비율 f
        private static void Key(float[] keys, float t, out int i, out float f)
        {
            i = 0;
            while (i < keys.Length - 2 && t > keys[i + 1])
                i++;
            float span = keys[i + 1] - keys[i];
            f = span > 0f ? Mathf.Clamp01((t - keys[i]) / span) : 1f;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        #endregion
    }
}
