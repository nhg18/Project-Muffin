using System.Collections;
using Chapchu.Cards;
using Chapchu.Game.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Goat
{
    /// <summary>
    /// 「GOAT」(1067) 의 3 단계 연출 — 「카드 이펙트 v2」 시안의 GOAT 컷인(goat_cutin.js)을 그대로 옮겼다. 화면 전체를 덮는 9.2초짜리 장면이다.
    /// 달밤의 돌 회랑 끝에서 성문이 열리고(0.7 ~ 2.9초) → 빛을 등진 왕 강아지가 걸어 나와(~ 4.6초) → 발을 구르고(5초) → 「GOAT」 네 글자가 하나씩 박히고(5.83초부터 0.44초마다)
    /// → 능력 문구 둘(HP · 피해 배수)이 뜬다(7.75초). 장면은 셋이고(멀리서 → 옆에서 올려다봄 3.1초 → 정면 4.7초) 바뀔 때 검게 깜빡인다.
    /// 회랑은 3D(<see cref="GoatWorld"/> — 렌더 텍스처), 그 위의 왕 · 제목 · 문구 · 위아래 띠는 UI 다. 모두 시안 화면(1280 × 720) 크기의 판 위에 있고 판을 화면에 맞춰 키운다.
    /// 연출 길이 9.2초 — 카드 처리 시간(resolveSeconds)에 3 단계 길이를 더해 두었다.
    /// </summary>
    public class GoatFx : CardFx
    {
        /// <summary>장면 길이 (시안 T, 초).</summary>
        public const float Duration = 9.2f;

        // 시안 frame(): 제목 뒤 어두운 띠가 깔리는 시간 · 띠의 위 · 아래 (px) · 가장 짙은 곳의 알파
        private const float BandStart = 5.4f, BandEnd = 5.9f, BandTop = 30f, BandBottom = 340f, BandAlpha = 0.5f;
        // 왕: 키(시안 길이 단위) · 나타나는 시간 · 눈을 뜨는 시간 · 테두리 빛 번짐(멀리 · 가까이, px) · 망토 쏠림(평소 · 발 구를 때 더 · 가라앉는 시간)
        private const float KingHeight = 4.6f, KingShowStart = 1.8f, KingShowEnd = 2.8f, EyesStart = 3.75f, EyesEnd = 4.05f;
        private const float GlowFar = 14f, GlowNear = 26f, GlowNearTime = 3.1f, Sway = 0.06f, StompSway = 0.05f, StompSwaySeconds = 0.8f;
        // 문구: 뜨는 시간 · 가운데에서의 거리 · 밀려 들어오는 거리 · 글자 높이 · 밑줄 높이 · 밑줄 반 길이 (px)
        private const float CaptionStart = 7.75f, CaptionEnd = 8.25f, CaptionSide = 330f, CaptionSlide = 40f, CaptionY = 478f, CaptionLineY = 508f, CaptionLineHalf = 80f;
        // 장면이 바뀔 때 검게 깜빡이는 반 길이 · 처음과 끝의 검은 화면
        private const float CutHalf = 0.16f, FadeSeconds = 0.5f;
        private static readonly float[] Cuts = { 3.1f, 4.7f };
        private const string HpFormat = "HP {0}", DamageFormat = "피해 ×{0}";

        [Header("판")]
        [Tooltip("연출 전체 — 재생하는 동안만 보인다.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("시안 화면(1280 × 720) 크기의 판 — 화면에 맞춰 통째로 키운다. 16:9 가 아닌 화면은 남는 쪽이 검다.")]
        [SerializeField] private RectTransform box;
        [SerializeField] private GoatWorld world;
        [SerializeField] private GoatKing king;
        [SerializeField] private GoatTitle title;
        [Tooltip("제목 뒤에 깔리는 어두운 띠.")]
        [SerializeField] private UIQuadBatch band;
        [Tooltip("능력 문구 둘 (왼쪽 HP · 오른쪽 피해 배수) — 글자 · 밑줄.")]
        [SerializeField] private TMP_Text[] captions = new TMP_Text[0];
        [SerializeField] private Graphic[] captionLines = new Graphic[0];
        [Tooltip("장면이 바뀔 때의 깜빡임 · 처음과 끝의 검은 화면.")]
        [SerializeField] private Graphic blackout;

        [Header("그림 · 수치")]
        [Tooltip("캐릭터 그림을 받지 못했을 때 쓸 강아지.")]
        [SerializeField] private Sprite dog;
        [Tooltip("이 연출의 카드 — 문구의 수치(HP · 피해 배수)를 읽는다.")]
        [SerializeField] private CardData card;

        private void Awake()
        {
            if (group == null || box == null || world == null || king == null || title == null || band == null || blackout == null || dog == null || card == null
                || captions.Length != 2 || captionLines.Length != 2)
                Debug.LogError($"[{nameof(GoatFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            group.alpha = 0f;
        }

        private void OnDestroy()
        {
            // 회랑은 시작할 때 캔버스 밖으로 떼어 놓았다 — 같이 지운다
            if (world != null)
                Destroy(world.gameObject);
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            LoadSound(context);
            Prepare(context);
            StartSound();

            float start = Time.time;
            for (float t = 0f; t < Duration; t = Time.time - start)
            {
                Evaluate(t);
                yield return null;
            }

            group.alpha = 0f;
            world.End();
        }

        // 판을 화면에 맞추고, 그림 · 문구를 채운다
        private void Prepare(CardFxContext context)
        {
            Vector2 screen = ((RectTransform)transform).rect.size;
            float fit = Mathf.Min(screen.x / GoatWorld.DesignWidth, screen.y / GoatWorld.DesignHeight);
            box.localScale = new Vector3(fit, fit, 1f);
            // 렌더 텍스처는 판이 화면에서 차지하는 px 만큼
            Canvas canvas = GetComponentInParent<Canvas>();
            float pixels = fit * (canvas != null ? canvas.rootCanvas.scaleFactor : 1f);
            world.Begin(Mathf.RoundToInt(GoatWorld.DesignWidth * pixels), Mathf.RoundToInt(GoatWorld.DesignHeight * pixels));

            king.SetDog(context.Character != null ? context.Character : dog);
            title.Prepare();

            // 수치는 덱이 값 파일을 얹은 사본에서 (26-card-data 6-1) — 프리팹의 card 는 에셋이라 값이 낡을 수 있다
            CardData data = context.Card != null ? context.Card : card;
            if (card.id != context.CardId)
                Debug.LogError($"[{nameof(GoatFx)}] {name} 의 {nameof(card)} 는 카드 {card.id} 인데 낸 카드는 {context.CardId} 다", this);
            foreach (CardEffect effect in data.effects)
            {
                if (!(effect is StartGoatEffect goat))
                    continue;
                captions[0].text = string.Format(HpFormat, goat.hp);
                captions[1].text = string.Format(DamageFormat, goat.damageTimes);
            }

            // 띠: 위 · 아래는 투명, 가운데가 가장 짙다
            Color32 clear = new Color(0f, 0f, 0f, 0f), dark = new Color(0f, 0f, 0f, BandAlpha);
            float middle = (BandTop + BandBottom) / 2f;
            band.Clear();
            band.Quad(new Vector2(0f, -middle), dark, new Vector2(0f, -BandTop), clear, new Vector2(GoatWorld.DesignWidth, -BandTop), clear, new Vector2(GoatWorld.DesignWidth, -middle), dark);
            band.Quad(new Vector2(0f, -BandBottom), clear, new Vector2(0f, -middle), dark, new Vector2(GoatWorld.DesignWidth, -middle), dark, new Vector2(GoatWorld.DesignWidth, -BandBottom), clear);
            band.Apply();

            group.alpha = 1f;
            Evaluate(0f);
        }

        // 시각 t(초)의 화면 — 모든 것을 시각만으로 정한다 (시안 frame · overlay)
        private void Evaluate(float t)
        {
            world.Frame(t);

            band.canvasRenderer.SetAlpha(Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BandStart, BandEnd, t)));
            title.Draw(t);

            // 왕은 3D 장면에서 서 있는 자리 · 거리에 맞춰 그린다
            Vector4 spot = world.Project(new Vector3(0f, 0f, world.KingZ));
            if (spot.w > 0f)
            {
                float sinceStomp = t - GoatWorld.StompTime;
                float sway = Sway + (sinceStomp >= 0f ? StompSway * Mathf.Clamp01(1f - sinceStomp / StompSwaySeconds) : 0f);
                king.Draw(new Vector2(spot.x, spot.y), spot.z * KingHeight, t, world.WalkPhase, world.WalkAmount, sway, t < GlowNearTime ? GlowFar : GlowNear,
                    Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(EyesStart, EyesEnd, t)), Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(KingShowStart, KingShowEnd, t)));
            }
            else
            {
                king.Hide();
            }

            // 문구: 양옆에서 가운데 쪽으로 밀려 들어오며 뜨고, 밑줄이 가운데에서 양쪽으로 그어진다
            float show = EaseOut(Mathf.InverseLerp(CaptionStart, CaptionEnd, t));
            for (int i = 0; i < captions.Length; i++)
            {
                float x = GoatWorld.DesignWidth / 2f + (i == 0 ? -1f : 1f) * (CaptionSide + CaptionSlide * (1f - show));
                captions[i].rectTransform.anchoredPosition = new Vector2(x, -CaptionY);
                captions[i].alpha = show;
                RectTransform line = captionLines[i].rectTransform;
                line.anchoredPosition = new Vector2(x, -CaptionLineY);
                line.sizeDelta = new Vector2(CaptionLineHalf * 2f * show, line.sizeDelta.y);
                captionLines[i].canvasRenderer.SetAlpha(show);
            }

            float black = Mathf.Max(1f - t / FadeSeconds, (t - (Duration - FadeSeconds)) / FadeSeconds);
            foreach (float cut in Cuts)
                black = Mathf.Max(black, 1f - Mathf.Abs(t - cut) / CutHalf);
            blackout.canvasRenderer.SetAlpha(Mathf.Clamp01(black));
        }

        private static float EaseOut(float k) => 1f - Mathf.Pow(1f - k, 3f);
    }
}
