using System;
using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 3D 장면 후처리의 세기 — 시안 NUKE3D env 가 정하는 노출 · 빛 번짐 · 채도 · 대비 · 입자감 · 가장자리 어둡게 · 충격파 물결.
    /// 「착탄 가까이」 는 달아나기 시작부터 착탄까지 0 → 1, 「느린 화면」 은 착탄 직전 느린 화면 동안, 「박동」 은 그동안 뛰는 심장 박동.
    /// </summary>
    [Serializable]
    public struct NukeGrade
    {
        [Tooltip("번쩍할 때 더하는 노출 — 착탄 순간에 가장 세고 금방 줄어든다.")]
        public float Flash;
        [Tooltip("번쩍 뒤 남는 노출 — 천천히 줄어든다.")]
        public float Afterglow;
        [Tooltip("느린 화면 동안 빼는 노출.")]
        public float HoldDarken;
        [Tooltip("빛 번짐 — 평소.")]
        public float Bloom;
        [Tooltip("빛 번짐 — 착탄 열기만큼 더한다.")]
        public float BloomHeat;
        [Tooltip("채도 — 평소.")]
        public float Saturation;
        [Tooltip("채도 — 착탄 가까이.")]
        public float SaturationNear;
        [Tooltip("느린 화면 동안 빼는 채도.")]
        public float HoldDesaturate;
        [Tooltip("낙진 때 빼는 채도.")]
        public float FalloutDesaturate;
        [Tooltip("대비 — 평소.")]
        public float Contrast;
        [Tooltip("착탄 가까이 더하는 대비.")]
        public float ContrastNear;
        [Tooltip("필름 입자감 — 평소.")]
        public float Grain;
        [Tooltip("낙진 때 더하는 입자감.")]
        public float FalloutGrain;
        [Tooltip("가장자리 어둡게 — 평소.")]
        public float Vignette;
        [Tooltip("착탄 가까이 더하는 가장자리 어둡게.")]
        public float VignetteNear;
        [Tooltip("심장 박동마다 더하는 가장자리 어둡게.")]
        public float VignettePulse;
        [Tooltip("느린 화면 동안 더하는 가장자리 어둡게 (이때는 검게).")]
        public float VignetteHold;
        [Tooltip("가장자리 색 — 평소.")]
        public Color VignetteColor;
        [Tooltip("가장자리 색 — 착탄 가까이 · 박동 때 (붉게).")]
        public Color VignetteNearColor;
        [Tooltip("틈새 그늘(차폐) 세기.")]
        public float Occlusion;
        [Tooltip("먼지 벽 · 낙진이 화면을 덮는 색.")]
        public Color DustColor;
        [Tooltip("해가 화면 안일 때 빛줄기 세기 (착탄 뒤에는 사라진다).")]
        public float SunRays;
        [Tooltip("충격파 물결 세기.")]
        public float Ripple;
        [Tooltip("충격파 물결이 이어지는 시간 (초, 착탄 뒤).")]
        public float RippleSeconds;

        /// <summary>시안 값.</summary>
        public static NukeGrade Default => new NukeGrade
        {
            Flash = 9f,
            Afterglow = 0.5f,
            HoldDarken = 0.12f,
            Bloom = 0.5f,
            BloomHeat = 0.5f,
            Saturation = 1.08f,
            SaturationNear = 0.92f,
            HoldDesaturate = 0.45f,
            FalloutDesaturate = 0.12f,
            Contrast = 1.07f,
            ContrastNear = 0.06f,
            Grain = 0.03f,
            FalloutGrain = 0.02f,
            Vignette = 0.3f,
            VignetteNear = 0.25f,
            VignettePulse = 0.1f,
            VignetteHold = 0.5f,
            VignetteColor = new Color(0.02f, 0.02f, 0.05f),
            VignetteNearColor = new Color(0.35f, 0.02f, 0.03f),
            Occlusion = 0.85f,
            DustColor = new Color(0.6f, 0.44f, 0.3f),
            SunRays = 0.26f,
            Ripple = 0.9f,
            RippleSeconds = 2.2f,
        };
    }
}
