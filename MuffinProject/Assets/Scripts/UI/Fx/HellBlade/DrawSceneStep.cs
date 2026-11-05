using System.Collections;
using Chapchu.UI.Fx.Common;
using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx.HellBlade
{
    /// <summary>
    /// 장면 「칼 뽑기」 — 장면이 나타나 천천히 다가가고, 엄지로 칼집을 밀어 칼날이 살짝 → 눈을 뜨며 칼을 뽑고 → 눈이 빨개지며 카메라가 튕긴다.
    /// 시안 SPECIAL 지옥참마도 0 ~ 830ms (그 뒤는 암전이 덮는다). 크기는 그림 칸 대비 배율 (가로, 세로).
    /// </summary>
    public class DrawSceneStep : CutInSceneStep
    {
        [Header("등장")]
        [Tooltip("장면이 나타나는 시간 (ms).")]
        [SerializeField] private float fadeInMs = 160f;
        [Tooltip("천천히 다가간 끝의 카메라 크기.")]
        [SerializeField] private float zoomScale = 1.08f;
        [Tooltip("다가가는 시간 (ms). 선형.")]
        [SerializeField] private float zoomMs = 900f;

        [Header("칼날 살짝")]
        [Tooltip("시작 시각 (ms).")]
        [SerializeField] private float peekAtMs = 130f;
        [Tooltip("여기까지 칼 프레임을 넘긴다.")]
        [SerializeField] private int peekFrame = 2;
        [SerializeField] private float peekMs = 110f;

        [Header("칼 뽑기 + 눈 뜨기 (같은 시각 · 시간 · 곡선)")]
        [Tooltip("시작 시각 (ms). 붉은 눈 시각에 끝나도록 잡는다.")]
        [SerializeField] private float drawAtMs = 550f;
        [Tooltip("칼이 다 뽑히고 눈이 튀어 열리는 시간 (ms).")]
        [SerializeField] private float drawMs = 50f;
        [Tooltip("끝으로 갈수록 느려지는 정도 (1 = 고르게). 칼 프레임과 눈 크기가 함께 쓴다.")]
        [SerializeField] private float easePower = 2.2f;
        [SerializeField] private Vector2 eyeFrom = new Vector2(1.04f, 0.05f);
        [SerializeField] private Vector2 eyePeak = new Vector2(1.08f, 1.3f);
        [SerializeField] private Vector2 eyeSquash = new Vector2(0.98f, 0.94f);
        [Tooltip("눈이 눌리는 시간 (ms).")]
        [SerializeField] private float squashMs = 55f;
        [Tooltip("눈이 제 크기로 돌아오는 시간 (ms).")]
        [SerializeField] private float settleMs = 60f;

        [Header("붉은 눈 + 카메라 튕김")]
        [Tooltip("시작 시각 (ms).")]
        [SerializeField] private float redAtMs = 600f;
        [Tooltip("눈이 빨개지는 시간 (ms).")]
        [SerializeField] private float redEyeMs = 70f;
        [Tooltip("붉은 기운이 끼는 시간 (ms).")]
        [SerializeField] private float tintMs = 100f;
        [Tooltip("튕길 때 카메라 크기.")]
        [SerializeField] private float zoomPeak = 1.13f;
        [SerializeField] private float zoomPeakMs = 110f;
        [Tooltip("튕긴 뒤 머무는 카메라 크기.")]
        [SerializeField] private float zoomSettle = 1.1f;
        [SerializeField] private float zoomSettleMs = 110f;

        public override void ShowStill()
        {
            SetVisible(true);
            Shot.alpha = 1f;
        }

        protected override IEnumerator PlaySceneRoutine()
        {
            float start = Time.time;
            SetVisible(true);
            Shot.alpha = 0f;
            Shot.DOFade(1f, Seconds(fadeInMs)).SetLink(gameObject);
            Cam.DOKill();
            Cam.DOScale(zoomScale, Seconds(zoomMs)).SetEase(Ease.Linear).SetTarget(Cam).SetLink(gameObject);

            yield return WaitUntilMs(start, peekAtMs);
            SwordFrames.Play(0, peekFrame, peekMs, easePower).SetLink(gameObject);

            yield return WaitUntilMs(start, drawAtMs);
            SwordFrames.Play(peekFrame, SwordFrames.LastFrame, drawMs, easePower).SetLink(gameObject);
            OpenEye();

            yield return WaitUntilMs(start, redAtMs);
            Tint.DOFade(1f, Seconds(tintMs)).SetLink(gameObject);
            RedEye.DOFade(1f, Seconds(redEyeMs)).SetLink(gameObject);
            Cam.DOKill();
            DOTween.Sequence().SetTarget(Cam).SetLink(gameObject)
                .Append(Cam.DOScale(zoomPeak, Seconds(zoomPeakMs)))
                .Append(Cam.DOScale(zoomSettle, Seconds(zoomSettleMs)));
        }

        // 감은 눈 → 뜬 눈: 세로로 튀어 열리고, 살짝 눌렸다 제 크기로. 빨간 눈도 같은 크기로 (어긋나 보이지 않게)
        private void OpenEye()
        {
            RectTransform eyeRect = Eye.rectTransform;
            Vector3 from = new Vector3(eyeFrom.x, eyeFrom.y, 1f);
            Vector3 peak = new Vector3(eyePeak.x, eyePeak.y, 1f);
            ShowEye(true);
            eyeRect.localScale = from;
            DOTween.Sequence().SetTarget(eyeRect).SetLink(gameObject)
                .Append(DOVirtual.Float(0f, 1f, Seconds(drawMs), k =>
                    eyeRect.localScale = Vector3.LerpUnclamped(from, peak, 1f - Mathf.Pow(1f - k, easePower))).SetEase(Ease.Linear))
                .Append(eyeRect.DOScale(new Vector3(eyeSquash.x, eyeSquash.y, 1f), Seconds(squashMs)).SetEase(Ease.InOutQuad))
                .Append(eyeRect.DOScale(Vector3.one, Seconds(settleMs)).SetEase(Ease.OutQuad))
                .OnUpdate(() => RedEye.rectTransform.localScale = eyeRect.localScale);
        }
    }
}
