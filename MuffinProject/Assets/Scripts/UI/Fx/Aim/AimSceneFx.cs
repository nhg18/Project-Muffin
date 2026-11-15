using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 목표 조준 3 단계의 코드로 그린 부분 (시안 AIMC.draw) — 영상 위 눈발, 그리고 총알부터:
    /// 회전하는 총알(옆) → 총알 뒤를 따라감 → 명중 → 뒤로 넘어가 눈밭에 풀썩 → 카메라가 물러남 + 필름 마무리(번쩍 · 물들이기 · 가장자리 어둡게 · 알갱이).
    /// 장면 좌표는 시안 그대로 1280×720 (아래로 +) 이고, <see cref="space"/> 를 화면을 덮게 늘린다 (시안 cover).
    /// 판 · 카메라 · 앞 나무는 왼쪽 위 기준(anchor · pivot (0, 1))으로 놓는다.
    /// 미리 그리는 판(<see cref="AimPlates"/>)은 <see cref="StartBuild"/> 에서 작업 스레드로 그리고, 다 되면 텍스처로 올린다.
    /// </summary>
    public class AimSceneFx : MonoBehaviour
    {
        private const float StageWidth = 1280f;
        private const float StageHeight = 720f;
        private const float Tau = Mathf.PI * 2f;
        private const float Pi = Mathf.PI;

        // 총알 (시안 L · Rr)
        private const float BulletLength = 470f;
        private const float BulletRadius = 58f;
        private const int BulletCurveSamples = 48;

        // 시안 shotHit 의 땅 자리 · 맞은 자리 · 표적 키
        private static readonly Vector2 GroundSpot = new Vector2(850f, 474f);
        private static readonly Vector2 ImpactSpot = new Vector2(846f, 432f);
        private const float DogHeight = 92f;

        private static readonly float[] BandStops = { 0f, 0.1f, 0.22f, 0.33f, 0.56f, 0.82f, 0.94f, 1f };
        private static readonly Color[] BandColors =
        {
            Hex(0x141c3c), Hex(0x56629c), Hex(0xeef1ff), Hex(0xa4ade0), Hex(0x56629c), Hex(0x1b2448), Hex(0x6f7cb8),
        };
        private static readonly float[] DiscStops = { 0f, 0.5f, 1f };
        private static readonly Color[] DiscColors = { Hex(0xfdfdff), Hex(0xa7acdc), Hex(0x23254e) };

        [Header("시간 (시안 AIM_T, 3 단계 시작부터 초)")]
        [Tooltip("영상이 끝나고 회전하는 총알 컷이 시작하는 때.")]
        [SerializeField] private float bulletAt = 3.65f;
        [Tooltip("총알 뒤를 따라가는 컷이 시작하는 때.")]
        [SerializeField] private float behindAt = 4.59f;
        [Tooltip("명중하는 때.")]
        [SerializeField] private float hitAt = 5.09f;
        [Tooltip("맞고 나서 눈밭에 내려앉기까지 (초).")]
        [SerializeField] private float landSeconds = 0.6f;
        [Tooltip("옆으로 번진 설원이 흘러가는 빠르기 (시안 px/초).")]
        [SerializeField] private float streakSpeed = 5200f;

        [Header("판")]
        [Tooltip("1280×720 장면 — 화면을 덮게 늘린다.")]
        [SerializeField] private RectTransform space;
        [SerializeField] private RawImage streak;
        [SerializeField] private RawImage sky;
        [SerializeField] private RawImage mountains;
        [SerializeField] private RawImage trees;
        [SerializeField] private RawImage ground;
        [Tooltip("카메라 — 장면 속 한 점을 화면 가운데에 두고 늘린다. 표적이 이 아래에 있다.")]
        [SerializeField] private RectTransform world;
        [Tooltip("눈밭에 머리가 파묻힐 때 땅 아래를 잘라 낸다.")]
        [SerializeField] private RectMask2D dogClip;
        [SerializeField] private AimDog dog;

        [Header("붓 (그리는 순서대로)")]
        [SerializeField] private AimPainter back;
        [SerializeField] private AimPainter middle;
        [Tooltip("더하기로 그리는 빛 (총알 꼬리 · 명중).")]
        [SerializeField] private AimPainter glow;
        [SerializeField] private AimPainter front;
        [Tooltip("더하기로 그리는 빛 (총알 뒷면 불빛).")]
        [SerializeField] private AimPainter frontGlow;
        [SerializeField] private AimPainter top;

        [Header("앞 나무 · 가지 (물러날 때 들어옴)")]
        [SerializeField] private RectTransform foreground;
        [SerializeField] private RawImage pineLeft;
        [SerializeField] private RawImage pineRight;
        [SerializeField] private RectTransform sprigA;
        [SerializeField] private RawImage sprigAImage;
        [SerializeField] private RectTransform sprigB;
        [SerializeField] private RawImage sprigBImage;

        [Header("필름 마무리")]
        [SerializeField] private Image flash;
        [Tooltip("곱하기로 살짝 푸르게 (#eef3fb).")]
        [SerializeField] private Image tint;
        [SerializeField] private AimPainter vignette;
        [Tooltip("알갱이 — 2배 곱하기 머티리얼.")]
        [SerializeField] private RawImage grain;
        [Tooltip("알갱이가 바뀌는 빠르기 (장/초).")]
        [SerializeField] private float grainFps = 24f;

        private readonly AimRandom _rng = new AimRandom(0);
        private readonly List<Vector2> _points = new List<Vector2>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Texture2D> _textures = new List<Texture2D>();
        private readonly float[] _edgeY = new float[BulletCurveSamples + 1];
        private readonly float[] _edgeX = new float[BulletCurveSamples + 1];
        private AimPainter[] _painters;
        private RawImage[] _plates;
        private Particle[] _hitParticles;
        private Particle[] _landParticles;
        private Task<AimPlates.Field> _fieldTask;
        private Task<AimPlates.Extras> _extrasTask;
        private AimPlates.Field _field;
        private Texture2D[] _grain;
        private float _wind;

        private struct Particle
        {
            public float A;
            public float B;
            public float C;
            public float D;
            public float E;
        }

        /// <summary>미리 그리는 판이 다 올라왔다 (총알 컷부터 쓴다).</summary>
        public bool IsBuilt { get; private set; }

        /// <summary>총알 컷이 시작하는 때 (초) — 그 앞은 영상.</summary>
        public float BulletAt => bulletAt;

        /// <summary>명중하는 때 (초).</summary>
        public float HitAt => hitAt;

        private void Awake()
        {
            if (space == null || streak == null || sky == null || mountains == null || trees == null || ground == null || world == null || dogClip == null || dog == null
                || back == null || middle == null || glow == null || front == null || frontGlow == null || top == null
                || foreground == null || pineLeft == null || pineRight == null || sprigA == null || sprigAImage == null || sprigB == null || sprigBImage == null
                || flash == null || tint == null || vignette == null || grain == null)
                Debug.LogError($"[{nameof(AimSceneFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            _painters = new[] { back, middle, glow, front, frontGlow, top, vignette };
            _plates = new[] { sky, mountains, trees, ground };
            BuildBulletEdge();
            BuildParticles();
        }

        private void Update()
        {
            if (IsBuilt || _fieldTask == null || !_fieldTask.IsCompleted || !_extrasTask.IsCompleted)
                return;
            FinishBuild();
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in _textures)
                Destroy(texture);
            _textures.Clear();
        }

        /// <summary>미리 그리는 판을 작업 스레드에서 그리기 시작한다 (한 번만).</summary>
        public void StartBuild()
        {
            if (_fieldTask != null)
                return;
            _fieldTask = Task.Run(AimPlates.BuildField);
            _extrasTask = Task.Run(AimPlates.BuildExtras);
        }

        /// <summary>표적 강아지 (시안 DOGPILE 키).</summary>
        public void SetTarget(string dogKey)
        {
            dog.SetKey(dogKey);
        }

        /// <summary>시안 AIMC.draw(t) — 3 단계 시작부터 t 초의 장면을 그린다.</summary>
        public void Draw(float t)
        {
            FitSpace();
            _wind = t;
            foreach (AimPainter painter in _painters)
                painter.Clear();

            bool film = t >= bulletAt;
            bool shotBullet = film && t < behindAt;
            bool shotBehind = film && t >= behindAt && t < hitAt;
            bool shotHit = film && t >= hitAt;
            Show(streak, shotBullet);
            SetActive(dogClip.gameObject, shotBehind || shotHit);
            Show(tint, film);
            Show(grain, film);
            if (!(shotBehind || shotHit))
            {
                foreach (RawImage plate in _plates)
                    Show(plate, false);
            }
            if (!shotHit)
                SetActive(foreground.gameObject, false);

            if (!film)
            {
                // 영상 위에는 옅은 눈발만
                Snow(top, t, 22, 3, 0.4f);
                Show(flash, false);
                return;
            }

            if (shotBullet)
                DrawBullet(t);
            else if (shotBehind)
                DrawBehind(t);
            else
                DrawHit(t);
            DrawFilm(t);
        }

        #region 판 만들기

        private void FinishBuild()
        {
            IsBuilt = true;
            if (_fieldTask.IsFaulted || _extrasTask.IsFaulted)
            {
                Debug.LogError($"[{nameof(AimSceneFx)}] 미리 그리는 판을 만들지 못했습니다: {_fieldTask.Exception ?? _extrasTask.Exception}", this);
                return;
            }

            _field = _fieldTask.Result;
            sky.texture = Upload(_field.Sky, "AimSky", TextureWrapMode.Clamp);
            mountains.texture = Upload(_field.Mountains, "AimMountains", TextureWrapMode.Clamp);
            trees.texture = Upload(_field.Trees, "AimTrees", TextureWrapMode.Clamp);
            ground.texture = Upload(_field.Ground, "AimGround", TextureWrapMode.Clamp);

            AimPlates.Extras extras = _extrasTask.Result;
            streak.texture = Upload(extras.Streak, "AimStreak", TextureWrapMode.Repeat);
            Texture2D pine = Upload(extras.PineFront, "AimPineFront", TextureWrapMode.Clamp);
            pineLeft.texture = pine;
            pineRight.texture = pine;
            sprigAImage.texture = Upload(extras.SprigA, "AimSprigA", TextureWrapMode.Clamp);
            sprigBImage.texture = Upload(extras.SprigB, "AimSprigB", TextureWrapMode.Clamp);
            FitCropped(pineLeft, extras.PineFront);
            FitCropped(pineRight, extras.PineFront);
            FitCropped(sprigAImage, extras.SprigA);
            FitCropped(sprigBImage, extras.SprigB);

            _grain = new Texture2D[extras.Grain.Length];
            for (int i = 0; i < _grain.Length; i++)
            {
                var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false) { name = "AimGrain" + i, wrapMode = TextureWrapMode.Repeat };
                texture.SetPixelData(extras.Grain[i], 0);
                texture.Apply(false, true);
                _textures.Add(texture);
                _grain[i] = texture;
            }
        }

        private Texture2D Upload(AimPlate plate, string textureName, TextureWrapMode wrap)
        {
            var texture = new Texture2D(plate.Width, plate.Rows, TextureFormat.RGBA32, false)
            {
                name = textureName,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixelData(plate.Rgba, 0);
            texture.Apply(false, true);
            plate.Rgba = null;
            _textures.Add(texture);
            return texture;
        }

        // 위아래 투명한 줄을 잘라 낸 판 — 프리팹에 놓인 원래 칸(판 전체) 안에서 남은 줄 자리로 줄인다
        private static void FitCropped(RawImage image, AimPlate plate)
        {
            RectTransform rect = image.rectTransform;
            Vector2 size = rect.sizeDelta;
            float k = size.y / plate.Height;
            rect.anchoredPosition += new Vector2(0f, -plate.Top * k);
            rect.sizeDelta = new Vector2(size.x, plate.Rows * k);
        }

        #endregion

        #region 컷

        // 시안 shotBullet — 회전하는 총알(옆), 옆으로 번진 설원이 쏜살같이 지나감
        private void DrawBullet(float t)
        {
            float lt = t - bulletAt;
            float u = lt / (behindAt - bulletAt);
            float spin = lt * Tau * 2.6f + 1f;
            float offset = (lt * streakSpeed) % StageWidth;
            streak.uvRect = new Rect(offset / StageWidth, 0f, 1f, 1f);

            float z = Mathf.Lerp(0.5f, 1.42f, AimEase.Out(u / 0.55f)) + u * 0.08f;
            float bx = Mathf.Lerp(560f, 690f, AimEase.Out(u / 0.5f)) + Mathf.Sin(lt * 31f) * 2f;
            float by = 368f + Mathf.Sin(lt * 23f) * 2.5f;

            // 뒤로 흐르는 눈발 — 총알 둘레에서 휘어 지나감
            _rng.Reset(88);
            for (int i = 0; i < 70; i++)
            {
                float sp = 0.6f + _rng.Next();
                float y0 = _rng.Next() * 720f;
                float x = 1500f - ((_rng.Next() * 1900f + lt * 4200f * sp) % 1900f);
                float dy = y0 - by;
                float side = dy == 0f ? 1f : Mathf.Sign(dy);
                float fall = Mathf.Exp(-Mathf.Abs(dy) / (130f * z));
                float near = Mathf.Exp(-Square((x - bx) / (330f * z))) * fall;
                float y = y0 + side * near * 70f * z;
                float length = (90f + _rng.Next() * 260f) * sp;
                Color color = Rgba(236, 240, 255, 0.12f + _rng.Next() * 0.38f);
                float width = 1f + _rng.Next() * 3.5f;
                float endY = y0 + side * Mathf.Exp(-Square((x + length - bx) / (330f * z))) * fall * 70f * z;
                _points.Clear();
                AimPainter.AddQuadratic(_points, new Vector2(x, y), new Vector2(x + length * 0.5f, y - side * near * 18f * z), new Vector2(x + length, endY), 12);
                back.Stroke(_points, width, color, true);
            }

            glow.Save();
            glow.Translate(bx, by);
            glow.Scale(z, z);
            DrawBulletTail(spin);
            glow.Restore();

            front.Save();
            front.Translate(bx, by);
            front.Scale(z, z);
            DrawSwirls(lt, spin);
            DrawBulletBody(spin);
            DrawShockWaves();
            front.Restore();
            Snow(front, t, 10, 91, 0.3f, -2600f, 60f, 7f, 0.06f);
        }

        // 시안 shotBehind — 총알 바로 뒤에서: 멀리 아주 작은 표적을 향해 빨려 들어감
        private void DrawBehind(float t)
        {
            float lt = t - behindAt;
            float u = lt / (hitAt - behindAt);
            float spin = lt * Tau * 2.6f;
            float z = 1.25f + AimEase.In(u) * 1.7f;
            const float cx = 850f;
            const float cy = 392f;
            PlaceField(cx, cy, z);
            PlaceCamera(cx, cy, z);
            dogClip.enabled = false;
            dog.SetClosedEyes(false);
            dog.Draw(GroundSpot, DogHeight, t, AimDog.Pose.Stand, 0f, 1f, 1f, 0f, 0f, 0f, Gust(2f));
            back.Save();
            ApplyCamera(back, cx, cy, z);
            back.Ellipse(new Vector2(GroundSpot.x, GroundSpot.y + DogHeight * 0.012f), DogHeight * 0.5f, DogHeight * 0.07f, 0f, Rgba(14, 21, 48, 0.6f));
            back.Restore();

            SpeedLines(middle, lt, 1f);

            // 총알 뒷모습: 불빛 속에서 돌아가는 바닥 테와 강선
            float br = Mathf.Lerp(118f, 7f, AimEase.Out(u * 0.92f));
            float bx = 640f + Mathf.Sin(lt * 26f) * (1f - u) * 3f;
            float by = Mathf.Lerp(430f, 360f + 38f * z, AimEase.Out(u));
            var center = new Vector2(bx, by);
            glow.Glow(center, br * 5f, Rgba(255, 180, 110, 0.5f * (1f - u * 0.4f)));
            for (int s = 0; s < 3; s++)
            {
                _points.Clear();
                for (int q = 0; q <= 30; q++)
                {
                    float d = q / 30f;
                    float a = spin * 1.6f + s * Tau / 3f + d * 2.2f;
                    float rr = br * (1.05f + d * 2.6f);
                    _points.Add(new Vector2(bx + Mathf.Cos(a) * rr, by + Mathf.Sin(a) * rr * 0.86f + d * d * 150f * (1f - u)));
                }
                glow.Stroke(_points, br * 0.2f, Rgba(255, 190 + s * 20, 130, 0.5f * (1f - u * 0.5f)), true);
            }

            front.RadialDisc(new Vector2(bx - br * 0.3f, by - br * 0.3f), br * 0.1f, center, br, DiscStops, DiscColors);
            Circle(front, center, br, Mathf.Max(1.5f, br * 0.07f), Hex(0x0c0d24));
            if (br > 14f)
            {
                Color rifling = Rgba(16, 18, 50, 0.7f);
                for (int i = 0; i < 6; i++)
                {
                    float a = spin + i * Tau / 6f;
                    front.Line(center + Polar(a, br * 0.62f), center + Polar(a + 0.25f, br * 0.97f), br * 0.07f, rifling, false);
                }
                Circle(front, center, br * 0.5f, br * 0.07f, rifling);
                frontGlow.Glow(center, br * 0.9f, Rgba(255, 214, 160, 0.8f));
            }
            if (u < 0.2f)
                top.Rect(0f, 0f, StageWidth, StageHeight, Rgba(236, 240, 255, (1f - u / 0.2f) * 0.55f));
        }

        // 시안 shotHit — 명중 → 뒤로 넘어가 풀썩 → 카메라가 천천히 물러남 (한 컷)
        private void DrawHit(float t)
        {
            float lt = t - hitAt;
            float lp = lt - landSeconds;
            float hs = Mathf.Max(0f, lt - 0.06f);   // 맞은 직후 두세 장은 그대로 멈춤

            // 카메라: 맞는 순간 살짝 당겨졌다가, 내려앉고 나면 천천히 뒤로 물러남
            float pull = AimEase.Smooth((lt - 0.72f) / 1.55f);
            float z = Mathf.Lerp(2.95f, 1.42f, pull) * (1f + 0.045f * Mathf.Exp(-lt * 10f));
            float cx = 850f - pull * 16f;
            float cy = 392f + (lp > 0f ? Mathf.Exp(-lp * 9f) * Mathf.Sin(lp * 34f) * 1.8f : 0f);
            PlaceField(cx, cy, z);
            PlaceCamera(cx, cy, z);

            // 표적: 움찔 → 뒤로 넘어가며 떠오름 → 등으로 풀썩(발이 하늘로) → 등을 대고 두어 번 흔들리다 멎음
            float gx = GroundSpot.x;
            float gy = GroundSpot.y;
            const float h = DogHeight;
            const float fallAngle = 2.62f;
            float p = Mathf.Clamp01(hs / (landSeconds - 0.06f));
            float hop = 4f * p * (1f - p) * 26f;
            float th = lt < 0.06f ? 0f : p < 1f ? fallAngle * (1f - Mathf.Pow(1f - p, 1.5f)) : fallAngle + 0.16f * Mathf.Exp(-lp * 5.5f) * Mathf.Sin(lp * 15f);
            float bodyX = gx + 26f * (1f - Square(1f - p));
            float bodyY = Mathf.Lerp(gy - 0.45f * h, gy - 0.31f * h, p) - hop;
            float fx = bodyX - Mathf.Sin(th) * 0.45f * h;
            float fy = bodyY + Mathf.Cos(th) * 0.45f * h;
            float sy = lt < 0.06f ? 0.9f
                : lp < 0f ? 1f + 0.05f * Mathf.Sin(p * Pi)
                : 1f - 0.16f * Mathf.Exp(-lp * 9f) * Mathf.Cos(lp * 17f) + Mathf.Sin(t * 1.6f) * 0.006f * Mathf.Clamp01(lp);
            float twitch = lp > 0.5f && lp < 1.2f ? Mathf.Sin((lp - 0.5f) / 0.7f * Pi * 4f) * 0.3f * (1f - (lp - 0.5f) / 0.7f) : 0f;   // 누운 뒤 다리가 한 번 파르르
            float spread = lt <= 0f ? 0f : lp < 0f ? 0.42f : 0.42f - 0.2f * Mathf.Clamp01(lp / 0.35f);
            float tail = Mathf.Lerp(0.2f, 1.35f, AimEase.Smooth(p * 1.3f)) + twitch * 0.5f;

            back.Save();
            ApplyCamera(back, cx, cy, z);
            back.Ellipse(new Vector2(bodyX + 9f * p, gy + 1.5f), Mathf.Lerp(44f, 62f, p) * (1f - hop / 90f), 6.4f, 0f, Rgba(14, 21, 48, 0.58f * (1f - hop / 60f)));
            back.Restore();

            // 눈밭이라 머리 쪽이 살짝 파묻힘: 바닥 아래는 잘라 내고 그 앞을 눈 둔덕으로 덮는다
            dogClip.enabled = p > 0.55f;
            dog.SetClosedEyes(true);
            dog.Draw(new Vector2(fx, fy), h, t, AimDog.Pose.Splay, th, 1f, sy, spread, twitch, tail, Gust(2f));
            float dx = bodyX - gx;

            middle.Save();
            ApplyCamera(middle, cx, cy, z);
            if (lp > 0f)
                DrawMound(bodyX + 17f, gy, AimEase.Out(lp / 0.18f));
            middle.Restore();

            // 맞은 자리: 작게 터지는 별 + 번지는 빛 (화면을 덮지 않는다)
            if (lt < 0.2f)
            {
                float q = lt / 0.2f;
                float e = AimEase.Out(q);
                glow.Save();
                ApplyCamera(glow, cx, cy, z);
                glow.Translate(ImpactSpot.x, ImpactSpot.y);
                glow.Glow(Vector2.zero, 22f + 34f * e, Rgba(255, 236, 206, 0.85f * (1f - q)));
                Color spark = Rgba(255, 250, 238, 1f - q);
                for (int i = 0; i < 9; i++)
                {
                    float an = i / 9f * Tau + 0.3f;
                    float r0 = 5f + e * 12f;
                    float r1 = r0 + (16f + (i % 3) * 9f) * (1f - q * 0.55f);
                    float wd = 0.17f * (1f - q * 0.6f);
                    glow.Triangle(Polar(an - wd, r0), Polar(an, r1), Polar(an + wd, r0), spark);
                }
                glow.Restore();
            }

            front.Save();
            ApplyCamera(front, cx, cy, z);
            DrawHitDust(t, lp, hs, gx + dx, gy);
            front.Restore();

            // 총알을 따라오던 속도선은 맞자마자 흩어지며 사라짐
            if (lt < 0.16f)
                SpeedLines(front, hitAt - behindAt + lt * 0.35f, 1f - lt / 0.16f);
            float horizon = 360f + (330f - cy) * z;
            float wind = AimEase.Smooth(lt / 0.5f);
            Powder(front, t, horizon + 70f, 740f, 16, 81, 0.42f * wind);
            Snow(front, t, 60, 33, 0.62f * wind);

            // 물러나면서 앞쪽 나무 · 가지가 화면 안으로 들어옴
            float fz = 1f + (z / 1.42f - 1f) * 1.35f;
            bool near = fz < 2.6f;
            SetActive(foreground.gameObject, near);
            if (near)
            {
                foreground.anchoredPosition = new Vector2(StageWidth / 2f * (1f - fz), -StageHeight / 2f * (1f - fz));
                foreground.localScale = new Vector3(fz, fz, 1f);
                sprigA.localRotation = Quaternion.Euler(0f, 0f, -(Pi + 0.55f + Gust(1.3f) * 0.07f) * Mathf.Rad2Deg);
                sprigB.localRotation = Quaternion.Euler(0f, 0f, -(0.5f + Gust(3f) * 0.06f) * Mathf.Rad2Deg);
            }
            Snow(top, t, 10, 47, 0.3f * wind, 420f, 260f, 6f, 0.03f);
        }

        // 시안 shotHit 의 털에서 털린 눈 · 내려앉을 때 눈가루 · 김 (카메라 좌표)
        private void DrawHitDust(float t, float lp, float hs, float landX, float gy)
        {
            if (hs > 0f && hs < 0.8f)
            {
                front.Alpha = Mathf.Clamp01(1.15f - hs / 0.7f);
                Color snowColor = Hex(0xeef1ff);
                foreach (Particle f in _hitParticles)
                {
                    float x = ImpactSpot.x + Mathf.Cos(f.A) * f.B * 150f * hs;
                    float y = ImpactSpot.y + Mathf.Sin(f.A) * f.B * 120f * hs + 270f * f.D * hs * hs;
                    if (y > gy + 3f)
                        continue;
                    front.Ellipse(new Vector2(x, y), f.C * (1f - hs * 0.5f), f.C * (1f - hs * 0.5f), 0f, snowColor);
                }
                front.Alpha = 1f;
            }

            Color dust = Rgba(226, 232, 255, 1f);
            if (lp > 0f && lp < 1.1f)
            {
                float q = lp / 1.1f;
                foreach (Particle f in _landParticles)
                {
                    float lq = Mathf.Clamp01((lp - f.E) / 0.95f);
                    if (lq <= 0f)
                        continue;
                    float e = AimEase.Out(lq);
                    dust.a = 0.44f * Mathf.Pow(1f - lq, 1.3f);
                    front.Glow(new Vector2(landX + f.A * (30f + f.B * e), gy - 3f - f.C * AimEase.Out(lq * 2.4f) * (1f - lq * 0.45f)), f.D * (0.45f + e * 1.25f), dust);
                }
                // 바닥을 따라 번지는 고리
                front.Save();
                front.Translate(landX, gy + 1f);
                front.Scale(1f, 0.16f);
                dust.a = 0.34f * Mathf.Pow(1f - q, 1.6f);
                front.Glow(Vector2.zero, 60f + AimEase.Out(lp / 0.45f) * 90f, dust);
                front.Restore();
            }

            // 김: 가늘게 올라와 바람에 휨
            float steam = Mathf.Clamp01((lp - 0.7f) / 0.9f);
            if (steam <= 0f)
                return;
            for (int i = 0; i < 12; i++)
            {
                float ph = (t * 0.42f + i / 12f) % 1f;
                dust.a = 0.26f * Mathf.Sin(ph * Pi) * steam;
                front.Glow(new Vector2(landX + 3f + ph * ph * 70f * Gust(0.5f) + Mathf.Sin(t * 2.2f + ph * 7f) * 5f * ph, gy - 28f - ph * 105f), 5f + ph * 19f, dust);
            }
        }

        // 시안 shotHit 의 눈 둔덕 — 파묻힌 머리 앞을 덮는다
        private void DrawMound(float mx, float gy, float m)
        {
            if (m < 0.001f)
                return;
            _points.Clear();
            var start = new Vector2(mx - 40f * m, gy + 4.5f);
            AimPainter.AddQuadratic(_points, start, new Vector2(mx - 24f * m, gy + 3.5f - 4.5f * m), new Vector2(mx - 6f * m, gy + 3f - 3.4f * m), 6);
            AimPainter.AddQuadratic(_points, _points[_points.Count - 1], new Vector2(mx + 8f * m, gy + 2f - 6f * m), new Vector2(mx + 22f * m, gy + 3f - 3f * m), 6);
            AimPainter.AddQuadratic(_points, _points[_points.Count - 1], new Vector2(mx + 34f * m, gy + 3f - 2f * m), new Vector2(mx + 42f * m, gy + 4.5f), 6);
            AimPainter.AddQuadratic(_points, _points[_points.Count - 1], new Vector2(mx, gy + 7.5f), start, 6);
            middle.Polygon(_points, Hex(0x7a85bd));
            Color shine = Rgba(190, 199, 240, 0.75f);
            middle.Ellipse(new Vector2(mx + 7f * m, gy + 0.6f - 3f * m), 11f * m, 1.5f, -0.05f, shine);
            middle.Ellipse(new Vector2(mx - 22f * m, gy + 1.6f - 2.2f * m), 8f * m, 1.2f, 0.06f, shine);
        }

        // 시안 draw 끝 — 컷이 바뀌는 순간의 번쩍 · 살짝 푸르게 · 가장자리 어둡게 · 필름 알갱이
        private void DrawFilm(float t)
        {
            float sinceCut = t - bulletAt;
            float flashAlpha = sinceCut >= 0f && sinceCut < 0.16f ? (1f - sinceCut / 0.16f) * 0.3f : 0f;
            flash.color = Rgba(255, 246, 226, flashAlpha);
            Show(flash, flashAlpha > 0f);
            vignette.Radial(new Vector2(StageWidth / 2f, StageHeight / 2f), StageHeight * 0.3f, StageWidth * 0.68f, Rgba(5, 9, 22, 0f), Rgba(5, 9, 22, 0.5f), 0f);

            if (_grain == null || _grain.Length == 0)
                return;
            int frame = Mathf.FloorToInt(_wind * grainFps);
            grain.texture = _grain[frame % _grain.Length];
            const float tile = 256f;
            grain.uvRect = new Rect((frame * 97 % 256) / tile, (frame * 57 % 256) / tile, StageWidth / tile, StageHeight / tile);
        }

        #endregion

        #region 총알

        // 시안 bullet 오른쪽 끝(뾰족한 머리) — 위쪽 곡선을 미리 표로 (y 가 −Rr → 0 으로 늘어난다)
        private void BuildBulletEdge()
        {
            float L = BulletLength;
            float r = BulletRadius;
            float xs = L * 0.08f;
            float xt = L / 2f;
            var p0 = new Vector2(xs, -r);
            var c0 = new Vector2(xs + L * 0.2f, -r);
            var c1 = new Vector2(xt - L * 0.1f, -r * 0.42f);
            var p1 = new Vector2(xt, 0f);
            for (int i = 0; i <= BulletCurveSamples; i++)
            {
                float s = i / (float)BulletCurveSamples;
                float k = 1f - s;
                Vector2 p = k * k * k * p0 + 3f * k * k * s * c0 + 3f * k * s * s * c1 + s * s * s * p1;
                _edgeX[i] = p.x;
                _edgeY[i] = p.y;
            }
        }

        private float BulletRight(float y)
        {
            float q = -Mathf.Abs(y);
            if (q <= _edgeY[0])
                return _edgeX[0];
            for (int i = 1; i <= BulletCurveSamples; i++)
            {
                if (q > _edgeY[i])
                    continue;
                float k = Mathf.InverseLerp(_edgeY[i - 1], _edgeY[i], q);
                return Mathf.Lerp(_edgeX[i - 1], _edgeX[i], k);
            }
            return _edgeX[BulletCurveSamples];
        }

        private static float BulletLeft(float y)
        {
            float a = Mathf.Abs(y);
            float xb = -BulletLength / 2f;
            float bevel = 0.94f * BulletRadius;
            return a > bevel ? xb + (a - bevel) / (BulletRadius - bevel) * BulletLength * 0.04f : xb;
        }

        // 시안 bullet — 길고 뾰족한 저격탄. 몸통의 강선 자국과 반짝임이 축을 따라 돌아간다
        private void DrawBulletBody(float spin)
        {
            float L = BulletLength;
            float r = BulletRadius;
            float xb = -L / 2f;
            float xs = L * 0.08f;
            float xt = L / 2f;

            // 금속 띠 (세로 그라데이션, 딱 끊기는 멈춤)
            for (int b = 0; b < BandColors.Length; b++)
            {
                float y0 = -r + BandStops[b] * 2f * r;
                float y1 = -r + BandStops[b + 1] * 2f * r;
                int rows = Mathf.Max(1, Mathf.CeilToInt((y1 - y0) / (r * 0.06f)));
                for (int i = 0; i < rows; i++)
                {
                    float ya = Mathf.Lerp(y0, y1, i / (float)rows);
                    float yb = Mathf.Lerp(y0, y1, (i + 1) / (float)rows);
                    front.Quad(new Vector2(BulletLeft(ya), ya), new Vector2(BulletRight(ya), ya), new Vector2(BulletRight(yb), yb), new Vector2(BulletLeft(yb), yb),
                        BandColors[b], BandColors[b], BandColors[b], BandColors[b]);
                }
            }

            // 강선 자국 6줄: 축을 따라 비스듬히 감기며, 돌아갈수록 위에서 아래로 넘어감
            for (int i = 0; i < 6; i++)
            {
                float a0 = spin + i * Tau / 6f;
                float facing = Mathf.Max(0f, Mathf.Cos(a0 + 1f));
                Color dark = Rgba(20, 22, 60, 0.25f + facing * 0.5f);
                Color light = Rgba(240, 243, 255, 0.1f + facing * 0.4f);
                _points.Clear();
                for (int k = 0; k <= 24; k++)
                {
                    float x = Mathf.Lerp(xb + L * 0.05f, xs + L * 0.16f, k / 24f);
                    float a = a0 + (x - xb) / L * 2.1f;
                    if (Mathf.Cos(a) < -0.05f)
                    {
                        StrokeRifling(dark, light);
                        continue;
                    }
                    float rr = r * (x > xs ? 1f - Square((x - xs) / (xt - xs)) * 0.5f : 1f);
                    _points.Add(new Vector2(x, Mathf.Sin(a) * rr * 0.985f));
                }
                StrokeRifling(dark, light);
            }

            // 돌아가는 반짝임 (금속 면의 얼룩이 넘어감)
            for (int i = 0; i < 3; i++)
            {
                float a = spin + i * Tau / 3f + 0.7f;
                float facing = Mathf.Cos(a);
                if (facing < 0f)
                    continue;
                float y = Mathf.Sin(a) * r * 0.8f;
                float xa = xs + L * 0.02f;
                float xLimit = xa + L * 0.34f;
                const int rows = 8;
                for (int k = 0; k < rows; k++)
                {
                    float ya = y - r * 0.16f + k * r * 0.32f / rows;
                    float yb = ya + r * 0.32f / rows;
                    float ra = Mathf.Min(xLimit, BulletRight(ya));
                    float rb = Mathf.Min(xLimit, BulletRight(yb));
                    if (ra <= xa && rb <= xa)
                        continue;
                    Color ca = Rgba(255, 255, 255, 0.5f * facing * (1f - Mathf.Abs(ya - y) / (r * 0.16f)));
                    Color cb = Rgba(255, 255, 255, 0.5f * facing * (1f - Mathf.Abs(yb - y) / (r * 0.16f)));
                    front.Quad(new Vector2(xa, ya), new Vector2(Mathf.Max(xa, ra), ya), new Vector2(Mathf.Max(xa, rb), yb), new Vector2(xa, yb), ca, ca, cb, cb);
                }
            }

            // 홈(캐널루어)과 바닥 테, 뜨거운 뒤쪽 불빛 반사
            Color groove = Rgba(16, 18, 50, 0.6f);
            front.Rect(xb + L * 0.17f, -r, L * 0.022f, r * 2f, groove);
            front.Rect(xb + L * 0.215f, -r, L * 0.012f, r * 2f, groove);
            front.Rect(xb + L * 0.193f, -r, L * 0.008f, r * 2f, Rgba(240, 243, 255, 0.35f));
            const int hotRows = 16;
            Color hot = Rgba(255, 190, 120, 0.75f);
            for (int k = 0; k < hotRows; k++)
            {
                float ya = Mathf.Lerp(-r, r, k / (float)hotRows);
                float yb = Mathf.Lerp(-r, r, (k + 1) / (float)hotRows);
                float la = BulletLeft(ya);
                float lb = BulletLeft(yb);
                Color ca = hot;
                ca.a *= 1f - (la - xb) / (L * 0.3f);
                Color cb = hot;
                cb.a *= 1f - (lb - xb) / (L * 0.3f);
                Color clear = Rgba(255, 190, 120, 0f);
                front.Quad(new Vector2(la, ya), new Vector2(xb + L * 0.3f, ya), new Vector2(xb + L * 0.3f, yb), new Vector2(lb, yb), ca, clear, clear, cb);
            }

            // 먹선 테두리
            _points.Clear();
            _points.Add(new Vector2(xb, -r * 0.94f));
            _points.Add(new Vector2(xb + L * 0.04f, -r));
            _points.Add(new Vector2(xs, -r));
            AimPainter.AddCubic(_points, new Vector2(xs, -r), new Vector2(xs + L * 0.2f, -r), new Vector2(xt - L * 0.1f, -r * 0.42f), new Vector2(xt, 0f), 16);
            AimPainter.AddCubic(_points, new Vector2(xt, 0f), new Vector2(xt - L * 0.1f, r * 0.42f), new Vector2(xs + L * 0.2f, r), new Vector2(xs, r), 16);
            _points.Add(new Vector2(xb + L * 0.04f, r));
            _points.Add(new Vector2(xb, r * 0.94f));
            front.Stroke(_points, Mathf.Max(2.5f, r * 0.085f), Hex(0x090f20), false, true);
        }

        // 강선 한 토막 — 어두운 홈 위에 살짝 위로 밝은 날
        private void StrokeRifling(Color dark, Color light)
        {
            if (_points.Count >= 2)
            {
                front.Stroke(_points, BulletRadius * 0.085f, dark, false);
                front.Save();
                front.Translate(0f, -BulletRadius * 0.05f);
                front.Stroke(_points, BulletRadius * 0.03f, light, false);
                front.Restore();
            }
            _points.Clear();
        }

        // 꼬리: 흰 심 → 주황, 나선으로 꼬이며 뒤로 풀림 (더하기)
        private void DrawBulletTail(float spin)
        {
            float half = BulletLength / 2f;
            float r = BulletRadius;
            float x0 = -half - 760f;
            float xm = x0 + 0.6f * 760f;
            float h0 = r * 0.25f;
            float h1 = r * 0.8f;
            float hm = Mathf.Lerp(h0, h1, 0.6f);
            Color c0 = Rgba(255, 150, 70, 0f);
            Color cm = Rgba(255, 170, 90, 0.32f);
            Color c1 = Rgba(255, 244, 220, 0.95f);
            glow.Quad(new Vector2(x0, -h0), new Vector2(xm, -hm), new Vector2(xm, hm), new Vector2(x0, h0), c0, cm, cm, c0);
            glow.Quad(new Vector2(xm, -hm), new Vector2(-half, -h1), new Vector2(-half, h1), new Vector2(xm, hm), cm, c1, c1, cm);

            for (int s = 0; s < 3; s++)
            {
                _points.Clear();
                _colors.Clear();
                Color tailEnd = Rgba(255, 140, 60, 0f);
                Color tailMiddle = Rgba(255, 180 + s * 20, 120, 0.5f);
                Color tailHead = Rgba(255, 250, 235, 0.95f);
                for (int k = 0; k <= 40; k++)
                {
                    float d = k / 40f * 820f;
                    float a = d * 0.021f - spin * 1.6f + s * Tau / 3f;
                    float amp = r * (0.5f + d / 820f * 1.5f);
                    _points.Add(new Vector2(-half - d, Mathf.Sin(a) * amp));
                    float g = 1f - d / 820f;
                    _colors.Add(g < 0.7f ? Color.Lerp(tailEnd, tailMiddle, g / 0.7f) : Color.Lerp(tailMiddle, tailHead, (g - 0.7f) / 0.3f));
                }
                glow.Stroke(_points, _colors, r * (0.2f - s * 0.035f), true);
            }
            glow.Glow(new Vector2(-half - 10f, 0f), r * 2.6f, Rgba(255, 226, 180, 0.85f));
            glow.Glow(new Vector2(-half - 120f, 0f), r * 4.2f, Rgba(255, 170, 100, 0.35f));
        }

        // 공기 소용돌이: 총알을 감고 뒤로 밀려나는 고리
        private void DrawSwirls(float lt, float spin)
        {
            float L = BulletLength;
            float r = BulletRadius;
            for (int i = 0; i < 7; i++)
            {
                float ph = (lt * 3.4f + i / 7f) % 1f;
                float x = Mathf.Lerp(L * 0.42f, -L * 0.9f, ph);
                float rr = r * (1.25f + ph * 1.5f);
                float a0 = spin * 1.3f + i * 1.7f;
                _points.Clear();
                AimPainter.AddArc(_points, new Vector2(x, 0f), rr * 0.26f, rr, a0, a0 + 2.2f, 18);
                front.Stroke(_points, 2.5f + ph * 4f, Rgba(236, 240, 255, 0.4f * Mathf.Sin(ph * Pi) * 0.55f), true);
            }
        }

        // 끝에서 갈라지는 충격파
        private void DrawShockWaves()
        {
            float L = BulletLength;
            float r = BulletRadius;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    _points.Clear();
                    AimPainter.AddQuadratic(_points, new Vector2(L / 2f + 14f + i * 26f, 0f), new Vector2(L / 2f - 40f + i * 20f, s * (r * 1.1f + i * 30f)),
                        new Vector2(L / 2f - 250f + i * 10f, s * (r * 2.1f + i * 60f)), 16);
                    front.Stroke(_points, 3f, Rgba(236, 240, 255, 0.5f * (0.5f - i * 0.22f)), true);
                }
            }
        }

        #endregion

        #region 공용 그리기

        // 시안 snow — 바람에 비스듬히 날리는 눈발 (r 이 크면 크고 흐릿한 알갱이)
        private void Snow(AimPainter painter, float t, int count, int seed, float alpha, float vx = 520f, float vy = 190f, float radius = 1.6f, float length = 0.03f)
        {
            if (alpha <= 0f)
                return;
            _rng.Reset(seed);
            for (int i = 0; i < count; i++)
            {
                float sp = 0.5f + _rng.Next();
                _rng.Next();
                float r = radius * (0.5f + _rng.Next() * 1.2f);
                float x = ((_rng.Next() * 1500f + t * vx * sp) % 1500f) - 110f + Mathf.Sin(t * 2f + i) * 8f;
                float y = ((_rng.Next() * 860f + t * vy * sp) % 860f) - 70f;
                float l = length * vx * sp;
                Color color = Rgba(236, 240, 255, alpha * (0.35f + _rng.Next() * 0.65f));
                painter.Line(new Vector2(x, y), new Vector2(x - l, y - l * vy / vx), r, color, true);
            }
        }

        // 시안 powder — 바닥을 스치는 눈가루 (양 끝이 투명한 휜 줄)
        private void Powder(AimPainter painter, float t, float y0, float y1, int count, int seed, float alpha)
        {
            if (alpha <= 0f)
                return;
            _rng.Reset(seed);
            for (int i = 0; i < count; i++)
            {
                float d = _rng.Next();
                float y = Mathf.Lerp(y0, y1, d * d);
                float sp = (0.6f + _rng.Next()) * (0.5f + d);
                float x = ((_rng.Next() * 1700f + t * 620f * sp) % 1700f) - 210f;
                float length = (120f + _rng.Next() * 260f) * (0.4f + d);
                float peak = alpha * (0.25f + _rng.Next() * 0.5f);
                float width = (1.5f + _rng.Next() * 5f) * (0.4f + d);
                _points.Clear();
                _colors.Clear();
                AimPainter.AddQuadratic(_points, new Vector2(x - length, y + Mathf.Sin(t * 3f + i) * 3f), new Vector2(x - length * 0.5f, y - 6f * Mathf.Sin(t * 2f + i * 2f)), new Vector2(x, y), 10);
                for (int k = 0; k <= 10; k++)
                {
                    float s = k / 10f;
                    _colors.Add(Rgba(230, 235, 255, s < 0.6f ? peak * s / 0.6f : peak * (1f - s) / 0.4f));
                }
                painter.Stroke(_points, _colors, width, true);
            }
        }

        // 시안 spdLines — 총알을 따라갈 때의 속도선 + 둘레 어둠 (a 로 옅게)
        private void SpeedLines(AimPainter painter, float lb, float alpha)
        {
            _rng.Reset(55);
            var center = new Vector2(640f, 372f);
            for (int i = 0; i < 70; i++)
            {
                float an = _rng.Next() * Tau;
                float first = _rng.Next();
                float ph = (first + lb * (2.4f + _rng.Next() * 2f)) % 1f;
                float r0 = 60f + ph * ph * 760f;
                float r1 = r0 + 40f + ph * 260f;
                Color color = Rgba(232, 236, 255, (0.1f + ph * 0.4f * _rng.Next()) * alpha);
                var dir = new Vector2(Mathf.Cos(an), Mathf.Sin(an) * 0.72f);
                painter.Line(center + dir * r0, center + dir * r1, 1f + ph * 5f, color, true);
            }
            painter.Radial(center, 80f, 720f, Rgba(8, 8, 26, 0f), Rgba(8, 8, 26, 0.6f * alpha), 760f);
        }

        private void Circle(AimPainter painter, Vector2 center, float radius, float width, Color color)
        {
            _points.Clear();
            AimPainter.AddArc(_points, center, radius, radius, 0f, Tau, 40);
            painter.Stroke(_points, width, color, false, true);
        }

        #endregion

        #region 카메라 · 판 자리

        // 시안 cover — 1280×720 장면이 화면을 덮게
        private void FitSpace()
        {
            Rect rect = ((RectTransform)transform).rect;
            float s = Mathf.Max(rect.width / StageWidth, rect.height / StageHeight);
            space.localScale = new Vector3(s, s, 1f);
        }

        // 시안 field / plate — 판마다 깊이가 달라 카메라가 움직일 때 겹마다 다르게 밀린다
        private void PlaceField(float cx, float cy, float z)
        {
            if (_field == null)
                return;
            PlacePlate(sky, _field.Sky, cx, cy, z, 0.25f);
            PlacePlate(mountains, _field.Mountains, cx, cy, z, 0.5f);
            PlacePlate(trees, _field.Trees, cx, cy, z, 0.8f);
            PlacePlate(ground, _field.Ground, cx, cy, z, 1f);
        }

        private static void PlacePlate(RawImage image, AimPlate plate, float cx, float cy, float z, float depth)
        {
            Show(image, true);
            float x = StageWidth / 2f - (plate.Width / 2f + (cx - plate.Width / 2f) * depth) * z;
            float y = StageHeight / 2f - (plate.Height / 2f + (cy - plate.Height / 2f) * depth) * z + plate.Top * z;
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(plate.Width * z, plate.Rows * z);
        }

        // 표적이 있는 장면 좌표 → 화면: (cx, cy) 를 가운데에, z 배
        private void PlaceCamera(float cx, float cy, float z)
        {
            world.anchoredPosition = new Vector2(StageWidth / 2f - cx * z, -(StageHeight / 2f - cy * z));
            world.localScale = new Vector3(z, z, 1f);
        }

        private static void ApplyCamera(AimPainter painter, float cx, float cy, float z)
        {
            painter.Translate(StageWidth / 2f, StageHeight / 2f);
            painter.Scale(z, z);
            painter.Translate(-cx, -cy);
        }

        #endregion

        #region 작은 도구

        private void BuildParticles()
        {
            // 시안 HITP · LANDP — 씨앗이 같아 매번 같은 모양
            var rng = new AimRandom(404);
            _hitParticles = new Particle[30];
            for (int i = 0; i < _hitParticles.Length; i++)
            {
                float a = -Pi * (rng.Next() * 1.3f - 0.15f);
                float v = 0.35f + rng.Next() * 0.65f;
                float s = 0.6f + rng.Next() * rng.Next() * 2.2f;
                float g = 0.7f + rng.Next() * 0.6f;
                _hitParticles[i] = new Particle { A = a, B = v, C = s, D = g };
            }
            rng.Reset(77);
            _landParticles = new Particle[24];
            for (int i = 0; i < _landParticles.Length; i++)
            {
                float d = 26f + rng.Next() * 84f;
                float h = 5f + rng.Next() * 24f;
                float r = 8f + rng.Next() * 13f;
                float o = rng.Next() * 0.1f;
                _landParticles[i] = new Particle { A = i % 2 == 1 ? 1f : -1f, B = d, C = h, D = r, E = o };
            }
        }

        // 시안 gust — 바람 세기(0~1): 세졌다 잦아들었다
        private float Gust(float phase)
        {
            return 0.55f + 0.3f * Mathf.Sin(_wind * 1.7f + phase) + 0.15f * Mathf.Sin(_wind * 4.3f + phase * 2.1f);
        }

        private static Vector2 Polar(float angle, float radius)
        {
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        private static float Square(float v)
        {
            return v * v;
        }

        private static void Show(Behaviour behaviour, bool visible)
        {
            if (behaviour.enabled != visible)
                behaviour.enabled = visible;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active)
                target.SetActive(active);
        }

        private static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, 1f);
        }

        private static Color Rgba(int r, int g, int b, float a)
        {
            return new Color(r / 255f, g / 255f, b / 255f, a);
        }

        #endregion
    }
}
