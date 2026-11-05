using System.Collections;
using Chapchu.UI.Fx.Common;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.HellBlade
{
    /// <summary>
    /// 컷인 장면 하나 — 그림 자체가 움직이는 부분 (장면 그림 · 칼 프레임 · 눈 · 붉은 눈 · 붉은 기운 · 카메라).
    /// 빛 · 번쩍 · 흔들림 같은 이펙트는 공통 이펙트 프리팹(Chapchu.UI.Fx.Common)으로 두고 이 장면 위에 겹친다. 장면 안의 시각은 장면 시작 기준 (ms).
    /// 장면이 시작하면 그림을 보이고, 장면 길이가 지나면 숨긴다.
    /// </summary>
    public abstract class CutInSceneStep : CardFxStep
    {
        [Header("장면 그림")]
        [Tooltip("장면 판. 이펙트 그림(칼집 빛 · 눈빛)도 이 안에 있어 같이 확대된다.")]
        [SerializeField] private CanvasGroup shot;
        [Tooltip("정사각형 장면을 담는 카메라. 확대 · 튕김은 장면이 한다.")]
        [SerializeField] private RectTransform cam;
        [Tooltip("움직이지 않는 장면 그림 (바탕색 · 흐린 장면 · 장면 · 눈 가림).")]
        [SerializeField] private Graphic[] painting;
        [SerializeField] private FramePlayer swordFrames;
        [Tooltip("감은 눈 → 뜬 눈으로 바뀌는 그림. 장면에 그려진 눈은 눈 가림이 가린다.")]
        [SerializeField] private Image eye;
        [SerializeField] private Sprite eyeOpenSprite;
        [Tooltip("감은 눈. 뜬 눈 그림과 같은 칸 크기(200×179)로 그려야 자리가 맞는다.")]
        [SerializeField] private Sprite eyeClosedSprite;
        [Tooltip("뜬 눈 위에 겹치는 빨간 눈.")]
        [SerializeField] private Image redEye;
        [Tooltip("장면 전체에 곱해지는 붉은 기운.")]
        [SerializeField] private Image tint;

        [Tooltip("장면 길이 (ms). 지나면 장면 그림을 숨긴다 — 앞 장면은 암전이 다 덮은 뒤로 잡는다. 0 = 숨기지 않는다.")]
        [SerializeField] private float durationMs;

        public override float DurationMs => durationMs;

        public override bool IsScene => true;

        protected CanvasGroup Shot => shot;
        protected RectTransform Cam => cam;
        protected FramePlayer SwordFrames => swordFrames;
        protected Image Eye => eye;
        protected Image RedEye => redEye;
        protected Image Tint => tint;

        protected virtual void Awake()
        {
            if (shot == null || cam == null || painting == null || swordFrames == null || eye == null || eyeOpenSprite == null
                || eyeClosedSprite == null || redEye == null || tint == null)
                Debug.LogError($"[{GetType().Name}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public override void ResetState()
        {
            base.ResetState();
            eye.rectTransform.DOKill();
            redEye.DOKill();
            tint.DOKill();
            SetVisible(false);
            swordFrames.ResetState();
            eye.sprite = eyeClosedSprite;
            eye.rectTransform.localScale = Vector3.one;
            // 빨간 눈을 뜬 눈과 같은 pivot 에서 커지게. 둘 다 앵커가 같은 칸에 늘어나 있어서 pivot 을 바꿔도 자리는 그대로다
            redEye.rectTransform.pivot = eye.rectTransform.pivot;
            redEye.rectTransform.localScale = Vector3.one;
        }

        public override void Play(CardFxContext context, RectTransform target)
        {
            StopRoutines();
            StartRoutine(SceneRoutine());
        }

        /// <summary>장면의 첫 모습을 움직이지 않고 보인다 (이펙트를 장면 위에서 볼 때).</summary>
        public abstract void ShowStill();

        /// <summary>장면을 처음부터. 끝나면 <see cref="CutInSceneStep"/> 이 장면 길이에 맞춰 숨긴다.</summary>
        protected abstract IEnumerator PlaySceneRoutine();

        /// <summary>장면 그림 · 칼 · 눈을 보이거나 숨긴다. 붉은 눈 · 붉은 기운은 숨길 때만 함께.</summary>
        protected void SetVisible(bool visible)
        {
            float alpha = visible ? 1f : 0f;
            foreach (Graphic graphic in painting)
                SetAlpha(graphic, alpha);
            SetAlpha(swordFrames.Image, alpha);
            SetAlpha(eye, alpha);
            if (visible)
                return;
            SetAlpha(redEye, 0f);
            SetAlpha(tint, 0f);
        }

        protected void ShowEye(bool open)
        {
            eye.sprite = open ? eyeOpenSprite : eyeClosedSprite;
        }

        private IEnumerator SceneRoutine()
        {
            float start = Time.time;
            StartRoutine(PlaySceneRoutine());
            if (durationMs <= 0f)
                yield break;
            yield return WaitUntilMs(start, durationMs);
            SetVisible(false);
        }
    }
}
