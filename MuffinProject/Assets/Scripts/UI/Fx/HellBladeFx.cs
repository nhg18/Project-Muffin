using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 「지옥참마도」 연출. 컷인(칼 뽑기 → 눈 → 絶 → 난도질 → 마지막 일섬 → 납도) 뒤, 대상 자리에서 마무리 칼질과 결과.
    /// 시간표(ms)는 아티팩트 「찹츄 카드 이펙트」 SPECIAL · FINISH 의 지옥참마도와 같다. 효과음은 아직 없다.
    /// </summary>
    public class HellBladeFx : CardFx
    {
        // 컷인 시간표 (ms, 시안 그대로)
        private const float FlurryStart = 1540f;
        private const float FinalCutStart = 3000f;
        private const float SheathStart = 3200f;
        private const float CutInEnd = 4150f;

        // 카메라 정사각형 안에서 화면 가운데에 올 점 (왼쪽 위 기준 비율, 시안 fx · fy)
        private static readonly Vector2 CamFocusLandscape = new Vector2(0.46f, 0.4f);
        private static readonly Vector2 CamFocusPortrait = new Vector2(0.47f, 0.4f);

        [Header("속도")]
        [Tooltip("1 = 시안 속도. 2 면 두 배 빠르다.")]
        [SerializeField] private float speed = 1f;
        [Tooltip("화면 흔들림 세기 배율. 0 이면 흔들지 않는다.")]
        [SerializeField] private float shakeScale = 1f;

        [Header("컷인 — 틀")]
        [SerializeField] private CanvasGroup cutIn;
        [SerializeField] private RectTransform shaker;
        [SerializeField] private CanvasGroup shot;
        [SerializeField] private RectTransform cam;

        [Header("컷인 — 장면")]
        [SerializeField] private Image drawFrame;
        [Tooltip("칼집에서 칼을 뽑는 프레임. 0 = 칼집에 들어감, 마지막 = 다 뽑힘.")]
        [SerializeField] private Sprite[] drawSprites;
        [SerializeField] private Image eyeCover;
        [SerializeField] private Image eyeOpen;
        [SerializeField] private Image eyeRed;
        [SerializeField] private Image tint;
        [SerializeField] private CanvasGroup eyeGlow;
        [SerializeField] private Image swordGlow;
        [SerializeField] private Image swordRing;
        [SerializeField] private Image swordStreak;
        [SerializeField] private Image swordSpark;

        [Header("컷인 — 위 겹")]
        [SerializeField] private Image black;
        [SerializeField] private Image kanji;
        [SerializeField] private CanvasGroup slashLayer;
        [SerializeField] private Image slashBackground;
        [SerializeField] private Image flash;

        [Header("마무리")]
        [SerializeField] private RectTransform finish;
        [SerializeField] private Image dimmer;
        [SerializeField] private Image finishFlash;

        [Header("찍어 내는 스프라이트")]
        [SerializeField] private Sprite slashBand;
        [SerializeField] private Sprite slashThin;
        [SerializeField] private Sprite boom;
        [SerializeField] private Sprite ring;
        [SerializeField] private Sprite glow;
        [SerializeField] private Sprite streak;

        [Header("색")]
        [Tooltip("난도질 배경. 110ms 마다 바뀐다 (가운데 색, 가장자리는 검정).")]
        [SerializeField] private Color[] flurryBackgrounds =
        {
            new Color32(0x4a, 0x06, 0x10, 0xff), new Color32(0x2a, 0x03, 0x08, 0xff),
            new Color32(0x5c, 0x0a, 0x14, 0xff), new Color32(0x1a, 0x02, 0x05, 0xff),
        };
        [Tooltip("마무리 「絶」 뒤 번짐 색.")]
        [SerializeField] private Color bloomColor = new Color32(0xff, 0x1a, 0x2a, 0xff);
        [Tooltip("마무리 파편 색. 무작위로 고른다.")]
        [SerializeField] private Color[] streakColors = { Color.white, new Color32(0xff, 0x2a, 0x3a, 0xff), new Color32(0xff, 0x6a, 0x6a, 0xff) };

        private int _frame;
        private float _start;

        private void Awake()
        {
            if (cutIn == null || shaker == null || shot == null || cam == null || drawFrame == null || eyeCover == null || eyeOpen == null
                || eyeRed == null || tint == null || eyeGlow == null || swordGlow == null || swordRing == null || swordStreak == null
                || swordSpark == null || black == null || kanji == null || slashLayer == null || slashBackground == null || flash == null
                || finish == null || dimmer == null || finishFlash == null)
                Debug.LogError($"[{nameof(HellBladeFx)}] 장면 참조 중 비어 있는 것이 있습니다.", this);

            if (drawSprites == null || drawSprites.Length < 2 || slashBand == null || slashThin == null || boom == null || ring == null
                || glow == null || streak == null)
                Debug.LogError($"[{nameof(HellBladeFx)}] 스프라이트 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            ResetState();
            FitCamera();
            yield return CutInRoutine();

            if (context.Targets == null || context.Targets.Count == 0)
                yield break;

            foreach (RectTransform target in context.Targets)
                StartCoroutine(FinishRoutine(context, target));
            yield return new WaitForSeconds(Seconds(1370f));
        }

        #region 컷인

        private IEnumerator CutInRoutine()
        {
            _start = Time.time;
            int lastFrame = drawSprites.Length - 1;

            shot.DOFade(1f, Seconds(160f)).SetLink(gameObject);
            cam.DOScale(1.08f, Seconds(900f)).SetEase(Ease.Linear).SetLink(gameObject);

            // 1) 엄지로 칼집을 밀어 칼날이 살짝
            yield return At(130f);
            PlayFrames(0, 2, 110f);
            SwordBurst(520f);

            // 2) 눈 팍
            yield return At(380f);
            eyeCover.DOFade(1f, Seconds(60f)).SetLink(gameObject);
            SetAlpha(eyeOpen, 1f);
            eyeOpen.rectTransform.localScale = new Vector3(1f, 0.1f, 1f);
            DOTween.Sequence().SetLink(gameObject)
                .Append(eyeOpen.rectTransform.DOScaleY(1.12f, Seconds(66f)))
                .Append(eyeOpen.rectTransform.DOScaleY(1f, Seconds(44f)));

            // 3) 쭉 뽑으며 칼날 번쩍 + 붉은 기운
            yield return At(470f);
            PlayFrames(2, lastFrame, 190f);
            yield return At(600f);
            tint.DOFade(1f, Seconds(100f)).SetLink(gameObject);
            eyeRed.DOFade(1f, Seconds(70f)).SetLink(gameObject);
            eyeGlow.transform.localScale = Vector3.one * 0.3f;
            eyeGlow.DOFade(1f, Seconds(200f)).SetLink(gameObject);
            eyeGlow.transform.DOScale(1.2f, Seconds(200f)).SetLink(gameObject);
            swordGlow.DOFade(1f, Seconds(120f)).SetLink(gameObject);
            SwordBurst(480f);
            Shake(shaker, 6f, 160f);
            cam.DOKill();
            DOTween.Sequence().SetLink(gameObject)
                .Append(cam.DOScale(1.13f, Seconds(110f)))
                .Append(cam.DOScale(1.1f, Seconds(110f)));

            // 4) 암전 → 絶
            yield return At(830f);
            black.DOFade(1f, Seconds(40f)).SetLink(gameObject);
            yield return At(950f);
            ShowKanji();
            Flash(flash, Color.white, 0.55f, 140f);
            Shake(shaker, 14f, 300f);
            Ring(shaker, Vector2.zero, Color.white, ShortSide(shaker) * 0.9f, 500f);

            // 5) 난도질
            yield return At(FlurryStart);
            SetAlpha(kanji, 0f);
            shot.alpha = 0f;
            slashLayer.alpha = 1f;
            StartCoroutine(FlurryRoutine(FinalCutStart - FlurryStart));

            // 6) 마지막 일섬 (히트스톱)
            yield return At(FinalCutStart);
            FinalCut();

            // 7) 원래 장면 → 칼집에 넣기
            yield return At(SheathStart + 60f);
            slashLayer.alpha = 0f;
            ClearSlashes();
            black.DOKill();
            SetAlpha(black, 0f);
            shot.alpha = 1f;
            tint.DOKill();
            SetAlpha(tint, 0f);
            eyeRed.DOKill();
            SetAlpha(eyeRed, 0.85f);
            eyeGlow.DOKill();
            eyeGlow.alpha = 0.85f;
            swordGlow.DOKill();
            SetAlpha(swordGlow, 0.7f);
            cam.DOKill();
            cam.localScale = Vector3.one * 1.1f;
            yield return At(SheathStart + 180f);
            PlayFrames(lastFrame, 0, 200f);
            yield return At(SheathStart + 350f);
            SwordBurst(560f);
            Shake(shaker, 5f, 140f);

            yield return At(CutInEnd - 220f);
            cutIn.DOFade(0f, Seconds(220f)).SetLink(gameObject);
            yield return At(CutInEnd);
        }

        // 칼이 뜰 때 칼집 이음새의 고리 · 줄 빛 · 별
        private void SwordBurst(float durationMs)
        {
            float duration = Seconds(durationMs);
            RectTransform ringRect = swordRing.rectTransform;
            RectTransform streakRect = swordStreak.rectTransform;
            RectTransform sparkRect = swordSpark.rectTransform;

            DOVirtual.Float(0f, 1f, duration, t =>
            {
                float a = Mathf.Clamp01(t / 0.35f);
                float b = Mathf.Clamp01((t - 0.35f) / 0.65f);
                ringRect.localScale = t < 0.35f
                    ? new Vector3(Mathf.Lerp(0.2f, 1f, a), Mathf.Lerp(0.09f, 0.45f, a), 1f)
                    : new Vector3(Mathf.Lerp(1f, 1.5f, b), Mathf.Lerp(0.45f, 0.68f, b), 1f);
                SetAlpha(swordRing, 1f - b);
            }).SetEase(Ease.OutCubic).SetLink(gameObject);

            DOVirtual.Float(0f, 1f, duration * 0.85f, t =>
            {
                float a = Mathf.Clamp01(t / 0.3f);
                float b = Mathf.Clamp01((t - 0.3f) / 0.7f);
                streakRect.localScale = t < 0.3f
                    ? new Vector3(Mathf.Lerp(0.05f, 1f, a), 1f, 1f)
                    : new Vector3(Mathf.Lerp(1f, 1.4f, b), Mathf.Lerp(1f, 0.15f, b), 1f);
                SetAlpha(swordStreak, 1f - b);
            }).SetLink(gameObject);

            DOVirtual.Float(0f, 1f, duration * 0.8f, t =>
            {
                float a = Mathf.Clamp01(t / 0.3f);
                float b = Mathf.Clamp01((t - 0.3f) / 0.7f);
                sparkRect.localScale = Vector3.one * (t < 0.3f ? Mathf.Lerp(0f, 1.3f, a) : Mathf.Lerp(1.3f, 0f, b));
                sparkRect.localRotation = Quaternion.Euler(0f, 0f, -90f * t);
                SetAlpha(swordSpark, 1f - b);
            }).SetLink(gameObject);
        }

        private void ShowKanji()
        {
            RectTransform rect = kanji.rectTransform;
            rect.localScale = Vector3.one * 1.6f;
            SetAlpha(kanji, 0f);
            DOTween.Sequence().SetLink(gameObject)
                .Append(rect.DOScale(0.94f, Seconds(140f)).SetEase(Ease.OutCubic))
                .Join(kanji.DOFade(1f, Seconds(140f)))
                .Append(rect.DOScale(1.02f, Seconds(84f)))
                .Append(rect.DOScale(0.98f, Seconds(336f)).SetEase(Ease.OutCubic));
        }

        // 가는 칼선은 매번, 굵은 칼선은 자주, 5번째마다 묵직한 칼선 + 번쩍
        private IEnumerator FlurryRoutine(float durationMs)
        {
            float start = Time.time;
            float lastSpawn = -999f;
            float lastBackground = -999f;
            int spawned = 0;
            RectTransform layer = (RectTransform)slashLayer.transform;

            while (true)
            {
                float elapsedMs = (Time.time - start) * 1000f * speed;
                if (elapsedMs > durationMs)
                    yield break;

                if (elapsedMs - lastBackground > 110f)
                {
                    lastBackground = elapsedMs;
                    slashBackground.color = flurryBackgrounds[Random.Range(0, flurryBackgrounds.Length)];
                }

                float gap = Mathf.Max(45f, 80f - elapsedMs / 40f);
                if (elapsedMs - lastSpawn > gap)
                {
                    lastSpawn = elapsedMs;
                    spawned++;
                    bool heavy = spawned % 5 == 0;

                    SpawnFlurrySlash(layer, false, false);
                    if (Random.value < 0.5f)
                        SpawnFlurrySlash(layer, false, false);

                    if (heavy || spawned % 2 == 0 || Random.value < 0.55f)
                    {
                        SpawnFlurrySlash(layer, true, heavy);
                        Shake(shaker, heavy ? 22f : 11f, 140f);
                        if (heavy)
                        {
                            Flash(flash, Color.white, 0.45f, 70f);
                            shaker.DOPunchScale(Vector3.one * 0.05f, Seconds(160f), 1, 0f).SetLink(gameObject);
                        }
                    }
                }

                yield return null;
            }
        }

        private void SpawnFlurrySlash(RectTransform layer, bool band, bool heavy)
        {
            Vector2 size = layer.rect.size;
            float diagonal = size.magnitude;
            Vector2 center = new Vector2(size.x * Random.Range(-0.3f, 0.3f), size.y * Random.Range(-0.3f, 0.3f));
            float angle = Random.Range(-69f, 69f) + (Random.value < 0.5f ? 0f : 180f);

            if (band)
            {
                float width = Random.Range(16f, 38f) * Mathf.Min(1.4f, diagonal / 1500f) * (heavy ? 1.7f : 1f);
                float draw = heavy ? 55f : Random.Range(60f, 100f);
                float life = Random.Range(240f, 380f) + (heavy ? 120f : 0f);
                Image slash = SpawnSlash(layer, slashBand, center, diagonal * Random.Range(0.9f, 1.4f), width * 3.4f, angle);
                DrawSlash(slash, draw, life);
                return;
            }

            float thinWidth = Random.Range(3f, 8f);
            float length = diagonal * Random.Range(0.5f, 1.1f);
            float thinDraw = Random.Range(50f, 90f);
            float thinLife = Random.Range(200f, 320f);
            DrawSlash(SpawnSlash(layer, slashThin, center, length, thinWidth * 4f, angle), thinDraw, thinLife);

            // 두 가닥: 조금 옆에 더 가는 칼선
            if (Random.value < 0.5f)
            {
                Vector2 side = Quaternion.Euler(0f, 0f, angle) * Vector2.down * thinWidth * 1.8f;
                DrawSlash(SpawnSlash(layer, slashThin, center + side, length, thinWidth * 2.4f, angle), thinDraw, thinLife);
            }
        }

        private void FinalCut()
        {
            ClearSlashes();
            slashBackground.color = Color.black;
            RectTransform layer = (RectTransform)slashLayer.transform;
            Vector2 size = layer.rect.size;

            // 왼쪽 아래 밖에서 오른쪽 위 밖까지
            float length = (size * 1.2f).magnitude;
            float angle = Mathf.Atan2(size.y, size.x) * Mathf.Rad2Deg;
            float thickness = Mathf.Min(size.x, size.y) * 0.1f * 3.4f;

            Image main = SpawnSlash(layer, slashBand, Vector2.zero, length, thickness, angle);
            main.DOFillAmount(1f, Seconds(60f)).SetEase(Ease.Linear).SetLink(main.gameObject);
            main.DOFade(0f, Seconds(140f)).SetDelay(Seconds(200f)).SetLink(main.gameObject);

            // 60~200ms 히트스톱: 빨강 · 파랑으로 어긋난 잔상
            Image red = SpawnSlash(layer, slashBand, new Vector2(-7f, 0f), length, thickness, angle);
            Image blue = SpawnSlash(layer, slashBand, new Vector2(7f, 0f), length, thickness, angle);
            red.color = new Color32(0xff, 0x6a, 0x8a, 0x00);
            blue.color = new Color32(0x7a, 0xd8, 0xff, 0x00);
            foreach (Image ghost in new[] { red, blue })
            {
                ghost.fillAmount = 1f;
                ghost.transform.SetAsFirstSibling();
                DOTween.Sequence().SetLink(ghost.gameObject)
                    .AppendInterval(Seconds(60f))
                    .Append(ghost.DOFade(0.9f, 0f))
                    .AppendInterval(Seconds(140f))
                    .Append(ghost.DOFade(0f, 0f));
            }
            slashBackground.transform.SetAsFirstSibling();

            DOVirtual.DelayedCall(Seconds(60f), () =>
            {
                Flash(flash, Color.white, 1f, 180f);
                Shake(shaker, 24f, 380f, 140f);
            }).SetLink(gameObject);
        }

        private void ClearSlashes()
        {
            Transform layer = slashLayer.transform;
            for (int i = layer.childCount - 1; i >= 0; i--)
            {
                Transform child = layer.GetChild(i);
                if (child == slashBackground.transform)
                    continue;
                child.DOKill();
                Destroy(child.gameObject);
            }
        }

        // 프레임 a → b, 끝으로 갈수록 느려진다
        private void PlayFrames(int from, int to, float durationMs)
        {
            DOVirtual.Float(0f, 1f, Seconds(durationMs), k =>
            {
                float eased = 1f - Mathf.Pow(1f - k, 2.2f);
                SetFrame(Mathf.RoundToInt(Mathf.Lerp(from, to, eased)));
            }).SetEase(Ease.Linear).SetLink(gameObject);
        }

        private void SetFrame(int index)
        {
            if (index == _frame)
                return;
            _frame = index;
            drawFrame.sprite = drawSprites[index];
        }

        // 시안의 카메라: 정사각형 장면을 화면보다 크게 깔고, 초점이 화면 가운데에 오게 한다
        private void FitCamera()
        {
            Vector2 screen = ((RectTransform)transform).rect.size;
            bool portrait = screen.x / screen.y < 1f;
            float side = portrait ? Mathf.Max(screen.x * 1.1f, screen.y * 0.9f) : Mathf.Max(screen.x * 1.12f, screen.y * 1.65f);
            Vector2 focus = portrait ? CamFocusPortrait : CamFocusLandscape;

            cam.anchorMin = cam.anchorMax = new Vector2(0.5f, 0.5f);
            cam.pivot = new Vector2(focus.x, 1f - focus.y);
            cam.sizeDelta = new Vector2(side, side);
            cam.anchoredPosition = Vector2.zero;

            float kanjiSize = Mathf.Min(ShortSide((RectTransform)transform) * 0.56f, 520f);
            kanji.rectTransform.sizeDelta = new Vector2(kanjiSize, kanjiSize);
        }

        private void ResetState()
        {
            cutIn.alpha = 1f;
            shot.alpha = 0f;
            slashLayer.alpha = 0f;
            eyeGlow.alpha = 0f;
            cam.localScale = Vector3.one;
            _frame = -1;
            SetFrame(0);

            foreach (Image image in new[] { eyeCover, eyeOpen, eyeRed, tint, swordGlow, swordRing, swordStreak, swordSpark, black, kanji, flash, dimmer, finishFlash })
                SetAlpha(image, 0f);
        }

        #endregion

        #region 마무리 — 대상 자리

        private IEnumerator FinishRoutine(CardFxContext context, RectTransform target)
        {
            Vector2 center = finish.InverseTransformPoint(target.position);

            dimmer.DOKill();
            DOTween.Sequence().SetLink(gameObject)
                .Append(dimmer.DOFade(0.88f, Seconds(93f)))
                .AppendInterval(Seconds(279f))
                .Append(dimmer.DOFade(0f, Seconds(248f)));

            // 짧은 칼질 7번
            for (int i = 0; i < 7; i++)
            {
                Vector2 jitter = new Vector2(Random.Range(-14f, 14f), Random.Range(-14f, 14f));
                Image slash = SpawnSlash(finish, slashBand, center + jitter, Random.Range(280f, 400f), Random.Range(6f, 11f) * 4f, Random.Range(-80f, 80f));
                CutSlash(slash, 240f);
                ShakeStage(context, 6f);
                yield return new WaitForSeconds(Seconds(45f));
            }

            // 큰 칼질 (시작 후 360ms)
            yield return new WaitForSeconds(Seconds(360f - 7 * 45f));
            CutSlash(SpawnSlash(finish, slashBand, center, 560f, 88f, 38f), 380f);
            Flash(finishFlash, Color.white, 0.9f, 120f);
            ShakeStage(context, 22f);
            if (context.Stage != null)
                context.Stage.DOPunchScale(Vector3.one * 0.05f, Seconds(300f), 1, 0f).SetLink(context.Stage.gameObject);

            // 絶 + 파편 + 번짐 + 고리, 이때 결과를 보인다 (시작 후 420ms)
            yield return new WaitForSeconds(Seconds(60f));
            Bloom(center, 340f, 450f);
            Ring(finish, center, Color.white, 780f, 450f);
            Streaks(center, 34);
            Boom(center + new Vector2(Random.Range(-30f, 30f), Random.Range(30f, 60f)));
            context.Impact?.Invoke(target);
        }

        private void Boom(Vector2 position)
        {
            Image image = NewImage("Boom", finish, boom, position, new Vector2(240f, 240f));
            RectTransform rect = image.rectTransform;
            rect.localScale = Vector3.one * 0.3f;
            rect.localRotation = Quaternion.Euler(0f, 0f, 20f);
            DOTween.Sequence().SetLink(image.gameObject)
                .Append(rect.DOScale(1.25f, Seconds(140f)).SetEase(Ease.OutBack))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, 4f), Seconds(330f)))
                .Append(rect.DOScale(1f, Seconds(190f)))
                .AppendInterval(Seconds(380f))
                .Append(image.DOFade(0f, Seconds(240f)))
                .Join(rect.DOScale(1.1f, Seconds(240f)))
                .Join(rect.DOAnchorPosY(position.y + 48f, Seconds(240f)))
                .OnComplete(() => Destroy(image.gameObject));
        }

        private void Streaks(Vector2 center, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                float distance = Random.Range(700f, 1300f) * 0.14f;
                float life = Random.Range(250f, 450f);
                Image image = NewImage("Streak", finish, streak, center, new Vector2(Random.Range(70f, 120f), Random.Range(8f, 14f)));
                image.color = streakColors[Random.Range(0, streakColors.Length)];
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
                image.rectTransform.DOAnchorPos(center + direction * distance, Seconds(life)).SetEase(Ease.OutExpo).SetLink(image.gameObject);
                image.DOFade(0f, Seconds(life * 0.55f)).SetDelay(Seconds(life * 0.45f)).SetLink(image.gameObject)
                    .OnComplete(() => Destroy(image.gameObject));
            }
        }

        private void Bloom(Vector2 center, float size, float lifeMs)
        {
            Image image = NewImage("Bloom", finish, glow, center, new Vector2(size, size));
            image.color = new Color(bloomColor.r, bloomColor.g, bloomColor.b, 0.85f);
            image.rectTransform.localScale = Vector3.one * 0.25f;
            image.rectTransform.DOScale(1.85f, Seconds(lifeMs)).SetEase(Ease.OutQuad).SetLink(image.gameObject);
            image.DOFade(0f, Seconds(lifeMs)).SetLink(image.gameObject).OnComplete(() => Destroy(image.gameObject));
        }

        #endregion

        #region 공용 조각

        private Image SpawnSlash(RectTransform parent, Sprite sprite, Vector2 center, float length, float thickness, float angle)
        {
            Image image = NewImage("Slash", parent, sprite, center, new Vector2(length, thickness));
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;
            return image;
        }

        // 머리가 draw 동안 끝까지 그어지고, 남은 시간 동안 사라진다
        private void DrawSlash(Image slash, float drawMs, float lifeMs)
        {
            DOTween.Sequence().SetLink(slash.gameObject)
                .Append(slash.DOFillAmount(1f, Seconds(drawMs)).SetEase(Ease.Linear))
                .Append(slash.DOFade(0f, Seconds(lifeMs - drawMs)))
                .OnComplete(() => Destroy(slash.gameObject));
        }

        // 마무리 칼질: 빠르게 그어지고 가늘어지며 사라진다
        private void CutSlash(Image slash, float durationMs)
        {
            float duration = Seconds(durationMs);
            DOTween.Sequence().SetLink(slash.gameObject)
                .Append(slash.DOFillAmount(1f, duration * 0.3f).SetEase(Ease.OutCubic))
                .Append(slash.DOFade(0f, duration * 0.7f))
                .Join(slash.rectTransform.DOScaleY(0.3f, duration * 0.7f))
                .OnComplete(() => Destroy(slash.gameObject));
        }

        private void Ring(RectTransform parent, Vector2 center, Color color, float size, float lifeMs)
        {
            Image image = NewImage("Ring", parent, ring, center, new Vector2(size, size));
            image.color = color;
            image.rectTransform.localScale = Vector3.one * 0.03f;
            image.rectTransform.DOScale(1f, Seconds(lifeMs)).SetEase(Ease.OutCubic).SetLink(image.gameObject);
            image.DOFade(0f, Seconds(lifeMs)).SetEase(Ease.InQuad).SetLink(image.gameObject).OnComplete(() => Destroy(image.gameObject));
        }

        private void Flash(Image image, Color color, float alpha, float durationMs)
        {
            image.DOKill();
            image.color = new Color(color.r, color.g, color.b, alpha);
            image.DOFade(0f, Seconds(durationMs)).SetLink(gameObject);
        }

        private void Shake(RectTransform target, float strength, float durationMs, float delayMs = 0f)
        {
            if (shakeScale <= 0f)
                return;
            target.DOKill(true);
            target.DOShakeAnchorPos(Seconds(durationMs), strength * shakeScale, 40, 90f, false, true).SetDelay(Seconds(delayMs)).SetLink(gameObject);
        }

        private void ShakeStage(CardFxContext context, float strength)
        {
            if (context.Stage == null)
                return;
            Shake(context.Stage, strength, 440f);
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private static float ShortSide(RectTransform rect) => Mathf.Min(rect.rect.width, rect.rect.height);

        private float Seconds(float ms) => ms / 1000f / speed;

        // 컷인 시작부터 ms 가 지날 때까지 기다린다
        private IEnumerator At(float ms)
        {
            while ((Time.time - _start) * 1000f * speed < ms)
                yield return null;
        }

        #endregion
    }
}
