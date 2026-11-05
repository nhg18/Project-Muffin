using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Chapchu.UI.Fx.HellBlade;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 「지옥참마도」 연출의 시간표. 컷인(칼 뽑기 → 눈 → 絶 → 난도질 → 마지막 일섬 → 납도) 뒤, 대상 자리에서 마무리 칼질과 결과.
    /// 컷인은 이 카드만의 장면(<see cref="CutInSceneStep"/> — 그림 애니메이션 컷 하나)과, 그 위에 겹치는 공통 이펙트 프리팹(Prefab/UI/CardFx/Common)으로 이루어진다.
    /// 컷인 · 마무리 아래 켜진 조각을 시작 시각 순(같으면 장면 먼저, 그다음 계층 위부터)으로 재생한다 — 이펙트 프리팹을 놓으면 그 시각에 나오고, 끄면 빠진다.
    /// 시간 · 크기 · 색은 아티팩트 「찹츄 카드 이펙트」 의 SPECIAL · FINISH 지옥참마도와 같다. 효과음은 아직 없다.
    /// </summary>
    public class HellBladeFx : CardFx
    {
        // 조각 하나만 볼 때 끝난 뒤 사라지는 꼬리까지 기다린다
        private const float StepTailMs = 300f;
        // 화면의 이 비율보다 작은 그림을 한 점 이펙트로 본다
        private const float PointEffectRatio = 0.6f;
        private const string SceneGroup = "장면";
        private const string EffectGroup = "이펙트";
        private const string FinishGroup = "마무리";

        [Header("시간표")]
        [Tooltip("컷인 판. 이 아래의 장면 · 이펙트가 컷인 시간표가 된다.")]
        [SerializeField] private CanvasGroup cutIn;
        [Tooltip("마무리 판. 이 아래의 이펙트가 대상마다 한 번씩 재생된다 (시각은 마무리 시작 기준).")]
        [SerializeField] private RectTransform finish;
        [Tooltip("컷인이 끝나고 마무리가 시작할 때까지 (ms).")]
        [SerializeField] private float finishDelayMs = 80f;
        [Tooltip("마무리 시작부터 결과(피해 숫자 · 체력)를 보일 때까지 (ms).")]
        [SerializeField] private float impactMs = 420f;
        [Tooltip("결과를 보인 뒤 카드를 치울 때까지 (ms).")]
        [SerializeField] private float afterImpactMs = 900f;

        [Header("장면 판")]
        [SerializeField] private CanvasGroup shot;
        [Tooltip("정사각형 장면을 담는 카메라. 화면보다 크게 깔고 초점이 화면 가운데에 오게 맞춘다.")]
        [SerializeField] private RectTransform cam;
        [Tooltip("가로 화면에서 화면 가운데에 올 장면 속 점 (왼쪽 위 기준 비율).")]
        [SerializeField] private Vector2 focusLandscape = new Vector2(0.46f, 0.4f);
        [Tooltip("세로 화면에서 화면 가운데에 올 장면 속 점 (왼쪽 위 기준 비율).")]
        [SerializeField] private Vector2 focusPortrait = new Vector2(0.47f, 0.4f);
        [Tooltip("가로 화면: 장면 한 변 = max(화면 너비 × x, 화면 높이 × y).")]
        [SerializeField] private Vector2 sideLandscape = new Vector2(1.12f, 1.65f);
        [Tooltip("세로 화면: 장면 한 변 = max(화면 너비 × x, 화면 높이 × y).")]
        [SerializeField] private Vector2 sidePortrait = new Vector2(1.1f, 0.9f);

        [Header("최적화")]
        [Tooltip("판째 투명해지면 꺼서 안의 그림까지 그리지 않을 판 (화면 전체를 덮는 판이 많아서 모바일에서 크다).")]
        [SerializeField] private CanvasGroup[] hideWhenClear;

        private List<CardFxStep> _scenes;
        private List<CardFxStep> _cutIn;
        private List<CardFxStep> _finish;
        private List<CardFxStep> _all;

        public override IReadOnlyList<CardFxStep> Steps => _all;

        private void Awake()
        {
            if (cutIn == null || finish == null || shot == null || cam == null || hideWhenClear == null)
                Debug.LogError($"[{nameof(HellBladeFx)}] 참조 중 비어 있는 것이 있습니다.", this);

            List<CardFxStep> cutInSteps = Collect(cutIn.transform);
            _scenes = cutInSteps.Where(step => step is CutInSceneStep).ToList();
            List<CardFxStep> effects = cutInSteps.Where(step => !(step is CutInSceneStep)).ToList();
            _cutIn = _scenes.Concat(effects).OrderBy(step => step.StartMs).ToList();
            _finish = Collect(finish);
            _all = _scenes.Concat(effects).Concat(_finish).ToList();

            // 투명도 0 인 그림은 GPU 가 그리지 않게
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
                graphic.canvasRenderer.cullTransparentMesh = true;
        }

        // 판째 투명해진 판은 꺼서 안의 그림까지 그리지 않는다. 다시 보이면 켠다 (조각의 코루틴은 이 루트에서 돌아 멈추지 않는다)
        private void LateUpdate()
        {
            foreach (CanvasGroup group in hideWhenClear)
            {
                bool visible = group.alpha > 0f;
                if (group.gameObject.activeSelf != visible)
                    group.gameObject.SetActive(visible);
            }
        }

        public override string GroupOf(CardFxStep step)
        {
            if (_finish.Contains(step))
                return FinishGroup;
            return step is CutInSceneStep ? SceneGroup : EffectGroup;
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            ResetState();
            FitCamera();
            yield return TimelineRoutine(_cutIn, context, null);
            yield return new WaitForSeconds(finishDelayMs / 1000f);

            if (context.Targets == null || context.Targets.Count == 0)
                yield break;

            foreach (RectTransform target in context.Targets)
            {
                StartCoroutine(TimelineRoutine(_finish, context, target));
                StartCoroutine(ImpactRoutine(context, target));
            }
            yield return new WaitForSeconds((impactMs + afterImpactMs) / 1000f);
        }

        public override IEnumerator PlayStepRoutine(CardFxStep step, CardFxContext context, bool showScenery)
        {
            ResetState();
            FitCamera();
            // 이펙트를 장면 위에서 보고 싶을 때 — 그 시각의 장면 첫 모습을 뒤에 깐다. 아니면 한 점 이펙트를 대상 자리 가운데로
            if (showScenery && GroupOf(step) == EffectGroup)
                SceneAt(step.StartMs)?.ShowStill();
            else if (!step.IsScene && context.Targets != null && context.Targets.Count > 0)
                CenterOn(step.Visual, context.Targets[0]);

            if (_finish.Contains(step))
            {
                if (context.Targets != null)
                {
                    foreach (RectTransform target in context.Targets)
                        step.Play(context, target);
                }
            }
            else
            {
                step.Play(context, null);
            }

            yield return new WaitForSeconds((step.DurationMs + StepTailMs) / 1000f);
        }

        // 켜진 조각을 시작 시각 순으로 (같으면 계층 위부터 — OrderBy 는 순서를 지킨다)
        private static List<CardFxStep> Collect(Transform root)
        {
            return root.GetComponentsInChildren<CardFxStep>().Where(step => step.enabled).OrderBy(step => step.StartMs).ToList();
        }

        // 시작부터 조각마다 제 시각에 재생하고, 가장 늦게 끝나는 조각까지 기다린다
        private static IEnumerator TimelineRoutine(List<CardFxStep> steps, CardFxContext context, RectTransform target)
        {
            float start = Time.time;
            float end = 0f;
            foreach (CardFxStep step in steps)
            {
                while ((Time.time - start) * 1000f < step.StartMs)
                    yield return null;
                step.Play(context, target);
                end = Mathf.Max(end, step.StartMs + step.DurationMs);
            }

            while ((Time.time - start) * 1000f < end)
                yield return null;
        }

        private IEnumerator ImpactRoutine(CardFxContext context, RectTransform target)
        {
            yield return new WaitForSeconds(impactMs / 1000f);
            context.Impact?.Invoke(target);
        }

        // 화면보다 작은 그림(칼집 빛 · 눈빛처럼 장면 속 한 점에 붙은 이펙트)만 옮긴다. 화면을 덮는 판은 그대로
        private void CenterOn(RectTransform visual, RectTransform target)
        {
            Vector2 screen = ((RectTransform)transform).rect.size;
            Vector3 scale = visual.lossyScale;
            Vector3 rootScale = transform.lossyScale;
            Vector2 size = new Vector2(visual.rect.width * scale.x / rootScale.x, visual.rect.height * scale.y / rootScale.y);
            if (size.x >= screen.x * PointEffectRatio || size.y >= screen.y * PointEffectRatio)
                return;

            Vector3 from = visual.TransformPoint(visual.rect.center);
            Vector3 to = target.TransformPoint(target.rect.center);
            visual.position += to - from;
        }

        // 그 시각에 걸친 장면, 없으면 그 앞의 마지막 장면, 그것도 없으면 첫 장면
        private CutInSceneStep SceneAt(float ms)
        {
            CardFxStep found = _scenes.LastOrDefault(scene => scene.StartMs <= ms) ?? _scenes.FirstOrDefault();
            return found as CutInSceneStep;
        }

        // 모든 조각을 처음 상태로 — 장면 그림은 숨겨지고(장면이 재생될 때 나타난다), 판은 보이는 채로 둔다
        private void ResetState()
        {
            foreach (CardFxStep step in _all)
                step.ResetState();

            cutIn.alpha = 1f;
            shot.alpha = 1f;
            cam.localScale = Vector3.one;
        }

        // 시안의 카메라: 정사각형 장면을 화면보다 크게 깔고, 초점이 화면 가운데에 오게 한다
        private void FitCamera()
        {
            Vector2 screen = ((RectTransform)transform).rect.size;
            bool portrait = screen.x / screen.y < 1f;
            Vector2 sideScale = portrait ? sidePortrait : sideLandscape;
            float side = Mathf.Max(screen.x * sideScale.x, screen.y * sideScale.y);
            Vector2 focus = portrait ? focusPortrait : focusLandscape;

            cam.anchorMin = cam.anchorMax = new Vector2(0.5f, 0.5f);
            cam.pivot = new Vector2(focus.x, 1f - focus.y);
            cam.sizeDelta = new Vector2(side, side);
            cam.anchoredPosition = Vector2.zero;
        }
    }
}
