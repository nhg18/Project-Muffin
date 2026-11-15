using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 연출의 시간표 — 시안 NUKE3D (16.4초).
    /// 3 단계: 컷인 판이 나타나고 위아래 검은 띠가 들어온 뒤, 3D 장면(<see cref="NukeSceneFx"/> — 네거리 · 강아지 · 미사일 · 폭발 · 버섯구름 · 낙진)을 화면에 얹고,
    /// 13.2초에 큰 글자(<see cref="NukeTitleFx"/>)가 내려앉는다. 끝나기 0.26초 전부터 컷인이 사라진다.
    /// 4 단계 장식: 쓴 사람 위 말풍선 「대소동!」 + 판 흔들기 (시안 FXA 대소동 s4 의 앞부분 — 손패 · 체력 · 피폭 표시는 공통 결과가 그린다).
    /// 연출 길이 16.4초 — 카드 처리 시간(resolveSeconds)은 게임 화면에 연출을 붙일 때 맞춘다 (PM 결정 2026-11-15).
    /// 3 단계 = 시안 「카드 이펙트 v2」 게시본 1794734628-e725 의 NUKE3D — 11-card-list 의 「컷인 넣지 않음」은 PM 지시(2026-11-15: 확정 시안을 먼저 구현하고 기획 문서는 뒤에 고친다)로 따르지 않는다.
    /// </summary>
    public class NukeFx : CardFx
    {
        private const string BubbleText = "대소동!";

        [Header("그림")]
        [Tooltip("컷인 판 전체 (투명도 · 쿵 흔들림).")]
        [SerializeField] private CanvasGroup cutIn;
        [Tooltip("3D 장면 화면을 담는 판 — 장면이 기울이고 확대한다.")]
        [SerializeField] private RectTransform frame;
        [Tooltip("3D 장면이 그려지는 화면.")]
        [SerializeField] private RawImage view;
        [Tooltip("3D 장면 (재생하면 UI 에서 떨어져 먼 곳에 놓인다).")]
        [SerializeField] private NukeSceneFx scene;
        [Tooltip("큰 글자 「대소동」.")]
        [SerializeField] private NukeTitleFx title;
        [Tooltip("위 검은 띠.")]
        [SerializeField] private RectTransform topBar;
        [Tooltip("아래 검은 띠.")]
        [SerializeField] private RectTransform bottomBar;

        [Header("시간 (ms, 3 단계 시작 기준)")]
        [Tooltip("컷인 판이 나타나는 시간.")]
        [SerializeField] private float fadeInMs = 180f;
        [Tooltip("검은 띠가 들어오는 시간.")]
        [SerializeField] private float barsMs = 260f;
        [Tooltip("큰 글자가 시작하는 때 (시안 (T.fall + HOLD - .1) × 1000).")]
        [SerializeField] private float titleMs = 13200f;
        [Tooltip("끝에 컷인 판이 사라지는 시간.")]
        [SerializeField] private float fadeOutMs = 260f;
        [Tooltip("3 단계 길이 (시안 SPECIAL_MS 16400).")]
        [SerializeField] private float lengthMs = 16400f;

        [Tooltip("시작 전 보이지 않는 첫 프레임에 미리 그려 둘 장면 시각 (실제 초) — 셰이더 · 메시 준비로 긴 프레임이 시간표 밖에서 지나가게 (시안은 장면을 미리 읽은 뒤 시작한다).")]
        [SerializeField] private float[] warmupSeconds = { 5f, 10.8f, 14f };

        [Header("크기")]
        [Tooltip("검은 띠 높이 = 화면 높이 × 이 값 (10vh).")]
        [SerializeField] private float barHeightRatio = 0.1f;

        [Header("4 단계 장식")]
        [Tooltip("말풍선 높이 — 쓴 사람 자리 가운데에서 위로 (시안 px).")]
        [SerializeField] private float bubbleDy = 80f;
        [Tooltip("판 흔들기 양 (시안 arenaShake 8).")]
        [SerializeField] private float stageShake = 8f;
        [Tooltip("장식 뒤 기다리는 시간 (ms).")]
        [SerializeField] private float postWaitMs = 450f;

        private void Awake()
        {
            if (cutIn == null || frame == null || view == null || scene == null || title == null || topBar == null || bottomBar == null)
                Debug.LogError($"[{nameof(NukeFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        // 장면은 재생 중 UI 에서 떨어져 있다 — 연출이 중간에 지워져도 같이 지운다
        private void OnDestroy()
        {
            if (scene != null && scene.transform.parent == null)
                Destroy(scene.gameObject);
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            Vector2 screen = ((RectTransform)transform).rect.size;
            float pixelScale = context.Layer != null ? context.Layer.PixelScale : 1f;
            var cut = (RectTransform)cutIn.transform;
            float barHeight = barHeightRatio * screen.y;
            topBar.sizeDelta = new Vector2(0f, barHeight);
            bottomBar.sizeDelta = new Vector2(0f, barHeight);

            cutIn.alpha = 0f;
            cutIn.gameObject.SetActive(true);
            title.Begin(screen, pixelScale);
            scene.Begin(view, frame, pixelScale);
            foreach (float seconds in warmupSeconds)
                scene.Draw(seconds);
            scene.Draw(0f);
            yield return null;

            // 시안처럼 실제 흐른 시간으로 — Time.time 은 긴 프레임(셰이더 준비 · 에디터 멈춤)마다 0.33초에서 잘려 장면이 뒤로 밀린다
            double last = Time.realtimeSinceStartupAsDouble;
            float ms = 0f;
            while (ms < lengthMs)
            {
                float fadeOutAt = lengthMs - fadeOutMs;
                cutIn.alpha = ms < fadeOutAt ? NukeMath.Clamp01(ms / fadeInMs) : 1f - NukeMath.Clamp01((ms - fadeOutAt) / fadeOutMs);
                float slide = (1f - NukeMath.CssEase(0f, 0f, 0.58f, 1f, ms / barsMs)) * barHeight;
                topBar.anchoredPosition = new Vector2(0f, slide);
                bottomBar.anchoredPosition = new Vector2(0f, -slide);
                cut.anchoredPosition = title.Tick(ms - titleMs);
                scene.Draw(ms / 1000f);
                yield return null;
                double now = Time.realtimeSinceStartupAsDouble;
                ms += (float)(now - last) * 1000f * Time.timeScale;
                last = now;
            }

            cut.anchoredPosition = Vector2.zero;
            scene.End();
            cutIn.gameObject.SetActive(false);
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            if (context.Layer != null && context.Caster != null)
            {
                context.Layer.Bubble(context.Caster, BubbleText, bubbleDy);
                context.Layer.ShakeStage(stageShake, false);
            }

            yield return new WaitForSeconds(postWaitMs / 1000f);
        }
    }
}
