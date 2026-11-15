using System.Collections;
using Chapchu.UI.Fx.Common;
using DG.Tweening;
using UnityEngine;

namespace Chapchu.UI.Fx.HellBlade
{
    /// <summary>
    /// 장면 「납도」 — 일섬 뒤 붉은 기운이 빠진 장면으로 단번에 돌아오고, 빨간 눈이 남은 채 칼을 칼집에 넣는다.
    /// 시안 SPECIAL 지옥참마도 3260ms ~ 컷인 끝.
    /// </summary>
    public class SheathSceneStep : CutInSceneStep
    {
        [Tooltip("카메라 크기 (칼 뽑기 장면이 튕긴 뒤 머문 크기).")]
        [SerializeField] private float camScale = 1.1f;
        [Tooltip("남는 빨간 눈 투명도.")]
        [Range(0f, 1f)]
        [SerializeField] private float redEyeAlpha = 0.85f;

        [Header("칼 넣기")]
        [Tooltip("시작 시각 (ms).")]
        [SerializeField] private float sheatheAtMs = 120f;
        [Tooltip("칼이 다 들어가는 시간 (ms).")]
        [SerializeField] private float sheatheMs = 200f;
        [Tooltip("끝으로 갈수록 느려지는 정도 (1 = 고르게).")]
        [SerializeField] private float easePower = 2.2f;

        protected override IEnumerator PlaySceneRoutine(float start, float offsetMs)
        {
            ShowStart();
            if (offsetMs > sheatheAtMs)
            {
                SwordFrames.Show(0);
                yield break;
            }

            yield return WaitUntilMs(start, sheatheAtMs);
            SwordFrames.Play(SwordFrames.LastFrame, 0, sheatheMs, easePower).SetLink(gameObject);
        }

        // 칼은 다 뽑힌 채, 눈은 뜬 채, 빨간 눈만 남고 붉은 기운은 없다
        private void ShowStart()
        {
            SetVisible(true);
            Shot.alpha = 1f;
            SwordFrames.Show(-1);
            ShowEye(true);
            Eye.rectTransform.localScale = Vector3.one;
            RedEye.DOKill();
            SetAlpha(RedEye, redEyeAlpha);
            Tint.DOKill();
            SetAlpha(Tint, 0f);
            Cam.DOKill();
            Cam.localScale = Vector3.one * camScale;
        }
    }
}
