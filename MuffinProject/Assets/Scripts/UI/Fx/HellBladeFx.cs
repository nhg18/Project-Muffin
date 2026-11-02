using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 「지옥참마도」 연출. 컷인(칼 뽑기 → 눈 → 絶 → 난도질 → 마지막 일섬 → 납도) 뒤, 대상 자리에서 마무리 칼질과 결과.
    /// 시간 · 크기 · 색은 아티팩트 「찹츄 카드 이펙트」 의 SPECIAL · FINISH 지옥참마도와 같다. 효과음은 아직 없다.
    /// </summary>
    public class HellBladeFx : CardFx
    {
        // 컷인 시간표 (ms)
        private const float FlurryStart = 1540f;
        private const float FinalCutStart = 3000f;
        private const float SheathStart = 3200f;
        private const float CutInEnd = 4150f;
        // 시안 special(): 컷인이 끝나고 80ms 뒤 마무리
        private const float FinishDelay = 80f;
        // 시안 ACT: 마무리 시작 420ms 뒤 피해, 그 뒤 900ms 기다렸다가 카드를 치운다
        private const float ImpactAt = 420f;
        private const float AfterImpact = 900f;

        // 카메라 정사각형 안에서 화면 가운데에 올 점 (왼쪽 위 기준 비율)
        private static readonly Vector2 CamFocusLandscape = new Vector2(0.46f, 0.4f);
        private static readonly Vector2 CamFocusPortrait = new Vector2(0.47f, 0.4f);

        [Header("컷인 — 틀")]
        [SerializeField] private CanvasGroup cutIn;
        [Tooltip("컷인 안에서 흔들리는 것들. 화면보다 넉넉하게 커서 흔들려도 가장자리가 비지 않는다.")]
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
        [Tooltip("칼날이 번쩍일 때 위에서 아래로 훑는 빛.")]
        [SerializeField] private Image swordGleam;
        [SerializeField] private Image swordRing;
        [SerializeField] private Image swordStreak;
        [SerializeField] private Image swordSpark;

        [Header("컷인 — 위 겹")]
        [SerializeField] private Image black;
        [SerializeField] private Image kanji;
        [Tooltip("絶 이 나타날 때의 흐림 · 밝기(시안 blur 8 · brightness 3 → 1)를 흉내 내는 번짐.")]
        [SerializeField] private Image kanjiGlow;
        [Tooltip("絶 과 함께 퍼지는 흰 고리 (시안 ringAt).")]
        [SerializeField] private Image kanjiRing;
        [SerializeField] private CanvasGroup slashLayer;
        [SerializeField] private Image slashBackground;
        [SerializeField] private Image flash;

        [Header("마무리")]
        [SerializeField] private RectTransform finish;
        [SerializeField] private Image dimmer;
        [SerializeField] private Image finishFlash;

        [Header("찍어 내는 그림")]
        [Tooltip("빛 · 칼선을 더해서 그리는 머티리얼 (겹치면 밝아진다).")]
        [SerializeField] private Material additive;
        [SerializeField] private Sprite slashBand;
        [SerializeField] private Sprite slashThin;
        [Tooltip("마무리 칼질 (시안 .ci-slash).")]
        [SerializeField] private Sprite finishSlash;
        [Tooltip("마무리 터짐 글자 絶.")]
        [SerializeField] private Sprite boomGlyph;

        [Header("색")]
        [Tooltip("난도질 배경. 110ms 마다 바뀐다 (가운데 색, 가장자리는 검정).")]
        [SerializeField] private Color[] flurryBackgrounds =
        {
            new Color32(0x4a, 0x06, 0x10, 0xff), new Color32(0x2a, 0x03, 0x08, 0xff),
            new Color32(0x5c, 0x0a, 0x14, 0xff), new Color32(0x1a, 0x02, 0x05, 0xff),
        };
        [Tooltip("마무리 짧은 칼질 색. 무작위로 고른다.")]
        [SerializeField] private Color[] finishSlashColors = { new Color32(0xff, 0x2a, 0x3a, 0xff), new Color32(0xff, 0x00, 0x22, 0xff), new Color32(0xff, 0x6a, 0x6a, 0xff) };
        [SerializeField] private Color boomColor = new Color32(0xe0, 0x00, 0x1a, 0xff);
        [SerializeField] private Color bloomColor = new Color32(0xff, 0x1a, 0x2a, 0xff);
        [SerializeField] private Color[] streakColors = { Color.white, new Color32(0xff, 0x2a, 0x3a, 0xff), new Color32(0xff, 0x6a, 0x6a, 0xff) };

        private int _frame;
        private float _start;

        private void Awake()
        {
            if (cutIn == null || shaker == null || shot == null || cam == null || drawFrame == null || eyeCover == null || eyeOpen == null
                || eyeRed == null || tint == null || eyeGlow == null || swordGlow == null || swordGleam == null || swordRing == null
                || swordStreak == null || swordSpark == null || black == null || kanji == null || kanjiGlow == null || kanjiRing == null
                || slashLayer == null || slashBackground == null || flash == null || finish == null || dimmer == null || finishFlash == null)
                Debug.LogError($"[{nameof(HellBladeFx)}] 장면 참조 중 비어 있는 것이 있습니다.", this);

            if (drawSprites == null || drawSprites.Length < 2 || additive == null || slashBand == null || slashThin == null
                || finishSlash == null || boomGlyph == null)
                Debug.LogError($"[{nameof(HellBladeFx)}] 그림 참조 중 비어 있는 것이 있습니다.", this);

            // 투명도 0 인 그림은 GPU 가 그리지 않게 (화면 전체를 덮는 판이 많아서 모바일에서 크다)
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
                graphic.canvasRenderer.cullTransparentMesh = true;
        }

        // 그룹째 투명해진 판(컷인 · 장면 · 난도질 · 눈빛)은 꺼서 안의 그림까지 그리지 않는다. 다시 보이면 켠다
        private void LateUpdate()
        {
            HideWhenClear(cutIn);
            HideWhenClear(shot);
            HideWhenClear(slashLayer);
            HideWhenClear(eyeGlow);
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            ResetState();
            FitCamera();
            yield return CutInRoutine();
            yield return new WaitForSeconds(FinishDelay / 1000f);

            if (context.Targets == null || context.Targets.Count == 0)
                yield break;

            foreach (RectTransform target in context.Targets)
                StartCoroutine(FinishRoutine(context, target));
            yield return new WaitForSeconds((ImpactAt + AfterImpact) / 1000f);
        }

        #region 컷인

        private IEnumerator CutInRoutine()
        {
            _start = Time.time;
            int lastFrame = drawSprites.Length - 1;

            // 장면이 밝게(brightness 2) 들어와 원래 밝기로
            shot.DOFade(1f, Seconds(160f)).SetLink(gameObject);
            Flash(flash, Color.white, 0.5f, 160f);
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
            SetAlpha(swordGlow, 1f);
            SweepGleam(300f);
            SwordBurst(480f);
            Shake(6f, 160f);
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
            Shake(14f, 300f);

            // 5) 난도질
            yield return At(FlurryStart);
            kanji.DOKill();
            kanjiGlow.DOKill();
            SetAlpha(kanji, 0f);
            SetAlpha(kanjiGlow, 0f);
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
            Shake(5f, 140f);

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

        // 칼날 빛 위를 위에서 아래로 훑는 하얀 띠 (시안 sw-gleam, 300ms)
        private void SweepGleam(float durationMs)
        {
            RectTransform rect = swordGleam.rectTransform;
            float height = ((RectTransform)rect.parent).rect.height;
            rect.anchoredPosition = new Vector2(0f, height * 1.2f);
            SetAlpha(swordGleam, 1f);
            DOTween.Sequence().SetLink(gameObject)
                .Append(rect.DOAnchorPosY(-height * 1.2f, Seconds(durationMs * 0.7f)))
                .Append(swordGleam.DOFade(0f, 0f))
                .AppendInterval(Seconds(durationMs * 0.3f));
        }

        // 絶: 크게 흐린 채 내려앉으며 번쩍 → 살짝 튕긴다 (560ms). 흰 고리가 퍼진다 (500ms)
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

            RectTransform glowRect = kanjiGlow.rectTransform;
            glowRect.localScale = Vector3.one;
            SetAlpha(kanjiGlow, 1f);
            kanjiGlow.DOFade(0f, Seconds(560f)).SetEase(Ease.OutCubic).SetLink(gameObject);

            RectTransform ringRect = kanjiRing.rectTransform;
            ringRect.localScale = Vector3.one * 0.1f;
            SetAlpha(kanjiRing, 1f);
            ringRect.DOScale(3f, Seconds(500f)).SetEase(Ease.OutQuad).SetLink(gameObject);
            kanjiRing.DOFade(0f, Seconds(500f)).SetEase(Ease.OutQuad).SetLink(gameObject);
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
                float elapsedMs = (Time.time - start) * 1000f;
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

                    SpawnFlurrySlash(false, false);
                    if (Random.value < 0.5f)
                        SpawnFlurrySlash(false, false);

                    if (heavy || spawned % 2 == 0 || Random.value < 0.55f)
                    {
                        SpawnFlurrySlash(true, heavy);
                        Shake(heavy ? 22f : 11f, 140f);
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

        // 시안 mkSlash: 화면 가운데 60% 안 아무 곳, 거의 수평 ±69°.
        // 원본 영상처럼 화면 끝에서 끝까지 이어지게 길이를 대각선의 1.6~2배로 — 끝이 화면 안에 보이면 날아가는 물체처럼 보인다
        private void SpawnFlurrySlash(bool band, bool heavy)
        {
            RectTransform layer = (RectTransform)slashLayer.transform;
            Vector2 screen = ScreenSize();
            float diagonal = screen.magnitude;
            Vector2 center = new Vector2(screen.x * Random.Range(-0.3f, 0.3f), screen.y * Random.Range(-0.3f, 0.3f));
            float angle = Random.Range(-69f, 69f) + (Random.value < 0.5f ? 0f : 180f);

            if (band)
            {
                float width = Random.Range(16f, 38f) * Mathf.Min(1.4f, diagonal / 1500f) * (heavy ? 1.7f : 1f);
                float draw = heavy ? 55f : Random.Range(60f, 100f);
                float life = Random.Range(240f, 380f) + (heavy ? 120f : 0f);
                DrawSlash(SpawnSlash(layer, slashBand, center, diagonal * Random.Range(1.6f, 2f), width * 3.4f, angle), draw, life);
                return;
            }

            float thinWidth = Random.Range(3f, 8f);
            float length = diagonal * Random.Range(1.6f, 2f);
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
            Vector2 screen = ScreenSize();

            // 왼쪽 아래 밖에서 오른쪽 위 밖까지
            float length = (screen * 1.2f).magnitude;
            float angle = Mathf.Atan2(screen.y, screen.x) * Mathf.Rad2Deg;
            float thickness = Mathf.Min(screen.x, screen.y) * 0.1f * 3.4f;

            Image main = SpawnSlash(layer, slashBand, Vector2.zero, length, thickness, angle);
            DOTween.Sequence().SetLink(main.gameObject)
                .AppendInterval(Seconds(200f))
                .Append(main.DOFade(0f, Seconds(140f)));

            // 60~200ms 히트스톱: 빨강 · 파랑으로 어긋난 잔상
            Image red = SpawnSlash(layer, slashBand, new Vector2(-7f, 0f), length, thickness, angle);
            Image blue = SpawnSlash(layer, slashBand, new Vector2(7f, 0f), length, thickness, angle);
            red.color = new Color32(0xff, 0x6a, 0x8a, 0x00);
            blue.color = new Color32(0x7a, 0xd8, 0xff, 0x00);
            foreach (Image ghost in new[] { red, blue })
            {
                ghost.transform.SetAsFirstSibling();
                DOTween.Sequence().SetLink(ghost.gameObject)
                    .AppendInterval(Seconds(60f))
                    .Append(ghost.DOFade(1f, 0f))
                    .AppendInterval(Seconds(140f))
                    .Append(ghost.DOFade(0f, 0f));
            }
            slashBackground.transform.SetAsFirstSibling();

            DOVirtual.DelayedCall(Seconds(60f), () =>
            {
                Flash(flash, Color.white, 1f, 180f);
                // 시안: 140ms 뒤 380ms 동안 정해진 방향으로 흔들린다
                DOVirtual.DelayedCall(Seconds(140f), () => ShakeKeys(new[]
                {
                    new Vector2(-24f, 14f), new Vector2(20f, -18f), new Vector2(-12f, 8f), new Vector2(6f, -4f), Vector2.zero,
                }, 380f)).SetLink(gameObject);
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
            Vector2 screen = ScreenSize();
            bool portrait = screen.x / screen.y < 1f;
            float side = portrait ? Mathf.Max(screen.x * 1.1f, screen.y * 0.9f) : Mathf.Max(screen.x * 1.12f, screen.y * 1.65f);
            Vector2 focus = portrait ? CamFocusPortrait : CamFocusLandscape;

            cam.anchorMin = cam.anchorMax = new Vector2(0.5f, 0.5f);
            cam.pivot = new Vector2(focus.x, 1f - focus.y);
            cam.sizeDelta = new Vector2(side, side);
            cam.anchoredPosition = Vector2.zero;

            float shortSide = Mathf.Min(screen.x, screen.y);
            float kanjiSize = Mathf.Min(shortSide * 0.56f, 520f);
            kanji.rectTransform.sizeDelta = new Vector2(kanjiSize, kanjiSize);
            // 시안 ringAt(…, 30): 지름 30vmin
            kanjiRing.rectTransform.sizeDelta = Vector2.one * shortSide * 0.3f;
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

            foreach (Image image in new[] { eyeCover, eyeOpen, eyeRed, tint, swordGlow, swordGleam, swordRing, swordStreak, swordSpark, black, kanji, kanjiGlow, kanjiRing, flash, dimmer, finishFlash })
                SetAlpha(image, 0f);
        }

        #endregion

        #region 마무리 — 대상 자리 (시안 FINISH 지옥참마도)

        private IEnumerator FinishRoutine(CardFxContext context, RectTransform target)
        {
            FxLayer layer = context.Layer;
            float px = layer.PixelScale;
            Vector2 center = finish.InverseTransformPoint(target.TransformPoint(target.rect.center));
            Vector2 layerCenter = layer.CenterOf(target);

            // 어두워짐 (620ms, .88): 15% 에 다 어두워지고 60% 부터 밝아진다
            DOTween.Sequence().SetLink(gameObject)
                .Append(dimmer.DOFade(0.88f, 0.093f))
                .AppendInterval(0.279f)
                .Append(dimmer.DOFade(0f, 0.248f));

            // 짧은 칼질 7번 (45ms 간격)
            for (int i = 0; i < 7; i++)
            {
                Vector2 jitter = new Vector2(Random.Range(-14f, 14f), Random.Range(-14f, 14f)) * px;
                Color color = finishSlashColors[Random.Range(0, finishSlashColors.Length)];
                CutSlash(center + jitter, Random.Range(280f, 400f) * px, Random.Range(6f, 11f) * px, Random.Range(-80f, 80f), color, 240f);
                layer.ShakeStage(6f, true);
                yield return new WaitForSeconds(0.045f);
            }

            // 큰 칼질 (360ms)
            yield return new WaitForSeconds(0.360f - 7 * 0.045f);
            CutSlash(center, 560f * px, 22f * px, -38f, finishSlashColors[0], 380f);
            Flash(finishFlash, Color.white, 0.9f, 120f);
            layer.ShakeStage(22f, true);
            layer.PunchStage(0.05f);

            // 絶 + 줄 빛 + 번짐 + 고리 (420ms) — 이때 결과를 보인다
            yield return new WaitForSeconds(0.060f);
            layer.Boom(layerCenter + new Vector2(Random.Range(-30f, 30f), Random.Range(30f, 60f)) * px, boomColor, boomGlyph);
            layer.Streaks(layerCenter, 34, streakColors);
            layer.Bloom(layerCenter, bloomColor, 340f, 0.45f);
            layer.Ring(layerCenter, Color.white, 280f, 0.45f, 14f);
            context.Impact?.Invoke(target);
        }

        // 시안 slashDOM: 왼쪽부터 드러나고(25%), 잠시 머물다(50%), 가늘어지며 사라진다 — 전체 ease-out. angle 은 시안 값(시계 방향 +)
        private void CutSlash(Vector2 center, float length, float width, float angle, Color color, float durationMs)
        {
            // 그림은 칼선 두께의 약 1/0.08 높이 (바깥 번짐 포함)
            Image image = NewImage("Slash", finish, finishSlash, center, new Vector2(length * 1.15f, width / 0.08f));
            image.material = additive;
            image.color = color;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.fillAmount = 0f;

            RectTransform rect = image.rectTransform;
            DOVirtual.Float(0f, 1f, durationMs / 1000f, t =>
            {
                image.fillAmount = Mathf.Clamp01(t / 0.25f);
                float fade = Mathf.Clamp01((t - 0.5f) / 0.5f);
                SetAlpha(image, 1f - fade);
                rect.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.1f, fade), 1f);
            }).SetEase(Ease.OutQuad).SetLink(image.gameObject).OnComplete(() => Destroy(image.gameObject));
        }

        #endregion

        #region 공용 조각

        // 칼선은 통째로 한 번에 나타난다 (원본 영상: 한쪽에서 늘어나면 날아가는 물체처럼 보인다)
        private Image SpawnSlash(RectTransform parent, Sprite sprite, Vector2 center, float length, float thickness, float angle)
        {
            Image image = NewImage("Slash", parent, sprite, center, new Vector2(length, thickness));
            image.material = additive;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return image;
        }

        // 그어지는 시간(draw) 동안 그대로 있다가 남은 시간 동안 사라진다
        private void DrawSlash(Image slash, float drawMs, float lifeMs)
        {
            DOTween.Sequence().SetLink(slash.gameObject)
                .AppendInterval(Seconds(drawMs))
                .Append(slash.DOFade(0f, Seconds(lifeMs - drawMs)))
                .OnComplete(() => Destroy(slash.gameObject));
        }

        private void Flash(Image image, Color color, float alpha, float durationMs)
        {
            image.DOKill();
            image.color = new Color(color.r, color.g, color.b, alpha);
            image.DOFade(0f, Seconds(durationMs)).SetEase(Ease.OutQuad).SetLink(gameObject);
        }

        // 시안 shakeC: 무작위 8곳을 거쳐 제자리로 (선형)
        private void Shake(float amount, float durationMs)
        {
            var keys = new Vector2[9];
            for (int i = 0; i < 8; i++)
                keys[i] = new Vector2(Random.Range(-amount, amount), Random.Range(-amount, amount));
            keys[8] = Vector2.zero;
            ShakeKeys(keys, durationMs);
        }

        private void ShakeKeys(Vector2[] keys, float durationMs)
        {
            shaker.DOKill();
            shaker.anchoredPosition = Vector2.zero;
            DOVirtual.Float(0f, 1f, Seconds(durationMs), t =>
            {
                // 시안은 0 에서 시작해 keys 를 차례로 지난다 (y 아래 + → 유니티 위 +)
                float k = t * keys.Length;
                int i = Mathf.Min(keys.Length - 1, Mathf.FloorToInt(k));
                Vector2 from = i == 0 ? Vector2.zero : keys[i - 1];
                Vector2 to = keys[i];
                Vector2 offset = Vector2.Lerp(from, to, k - i);
                shaker.anchoredPosition = new Vector2(offset.x, -offset.y);
            }).SetEase(Ease.Linear).SetTarget(shaker).SetLink(gameObject);
        }

        private Vector2 ScreenSize() => ((RectTransform)transform).rect.size;

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
            image.canvasRenderer.cullTransparentMesh = true;
            return image;
        }

        private static void HideWhenClear(CanvasGroup group)
        {
            bool visible = group.alpha > 0f;
            if (group.gameObject.activeSelf != visible)
                group.gameObject.SetActive(visible);
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private static float Seconds(float ms) => ms / 1000f;

        // 컷인 시작부터 ms 가 지날 때까지 기다린다
        private IEnumerator At(float ms)
        {
            while ((Time.time - _start) * 1000f < ms)
                yield return null;
        }

        #endregion
    }
}
