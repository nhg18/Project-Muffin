using System;
using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대폭발」 3D 장면의 빛 상태 하나 — 맑음 · 착탄 · 낙진. 장면은 착탄 뒤 맑음 → 착탄, 낙진 장면 앞에서 → 낙진으로 섞는다 (시안 NUKE3D env 의 mix3).
    /// 색은 HDR (1 보다 밝을 수 있다).
    /// </summary>
    [Serializable]
    public struct NukeWeather
    {
        [Tooltip("해 빛.")]
        [ColorUsage(false, true)] public Color Sun;
        [Tooltip("하늘에서 오는 빛 — 그늘 쪽 밝기.")]
        [ColorUsage(false, true)] public Color SkyLight;
        [Tooltip("땅에서 튀어 오르는 빛.")]
        [ColorUsage(false, true)] public Color Ground;
        [Tooltip("안개 색.")]
        [ColorUsage(false, true)] public Color Fog;
        [Tooltip("안개 짙기 (1/m).")]
        public float FogDensity;
        [Tooltip("안개가 가장 짙을 때 (0~1).")]
        public float FogMax;
        [Tooltip("하늘 꼭대기 색.")]
        [ColorUsage(false, true)] public Color Zenith;
        [Tooltip("지평선 색.")]
        [ColorUsage(false, true)] public Color Horizon;
        [Tooltip("구름 — 해를 받는 쪽.")]
        [ColorUsage(false, true)] public Color CloudLit;
        [Tooltip("구름 — 그늘 쪽.")]
        [ColorUsage(false, true)] public Color CloudShade;
        [Tooltip("구름 덮개 (0~1).")]
        public float CloudCover;
        [Tooltip("강아지 색 보정 — 그림에 곱한다.")]
        [ColorUsage(false, true)] public Color Gain;
        [Tooltip("화면 색조 (후처리).")]
        [ColorUsage(false, true)] public Color Tint;

        /// <summary>시안 값 — 맑은 저녁.</summary>
        public static NukeWeather Clear => new NukeWeather
        {
            Sun = new Color(1.7f, 1.24f, 0.8f),
            SkyLight = new Color(0.5f, 0.56f, 0.76f),
            Ground = new Color(0.52f, 0.42f, 0.34f),
            Fog = new Color(0.98f, 0.74f, 0.52f),
            FogDensity = 0.0028f,
            FogMax = 0.9f,
            Zenith = new Color(0.2f, 0.36f, 0.72f),
            Horizon = new Color(1.02f, 0.76f, 0.52f),
            CloudLit = new Color(1.25f, 1.02f, 0.84f),
            CloudShade = new Color(0.5f, 0.48f, 0.62f),
            CloudCover = 0.5f,
            Gain = new Color(1.04f, 1f, 0.95f),
            Tint = new Color(1.03f, 1f, 0.96f),
        };

        /// <summary>시안 값 — 착탄 뒤 불바다.</summary>
        public static NukeWeather Blast => new NukeWeather
        {
            Sun = new Color(0.85f, 0.4f, 0.18f),
            SkyLight = new Color(0.46f, 0.31f, 0.25f),
            Ground = new Color(0.42f, 0.24f, 0.14f),
            Fog = new Color(0.62f, 0.33f, 0.16f),
            FogDensity = 0.0055f,
            FogMax = 0.96f,
            Zenith = new Color(0.26f, 0.13f, 0.1f),
            Horizon = new Color(0.9f, 0.42f, 0.16f),
            CloudLit = new Color(1.1f, 0.6f, 0.3f),
            CloudShade = new Color(0.3f, 0.17f, 0.13f),
            CloudCover = 0.68f,
            Gain = new Color(1.12f, 0.8f, 0.6f),
            Tint = new Color(1.08f, 0.93f, 0.82f),
        };

        /// <summary>시안 값 — 녹색 낙진.</summary>
        public static NukeWeather Fallout => new NukeWeather
        {
            Sun = new Color(0.42f, 0.5f, 0.24f),
            SkyLight = new Color(0.33f, 0.42f, 0.26f),
            Ground = new Color(0.2f, 0.26f, 0.13f),
            Fog = new Color(0.36f, 0.43f, 0.2f),
            FogDensity = 0.0042f,
            FogMax = 0.96f,
            Zenith = new Color(0.22f, 0.28f, 0.13f),
            Horizon = new Color(0.66f, 0.7f, 0.32f),
            CloudLit = new Color(0.5f, 0.58f, 0.3f),
            CloudShade = new Color(0.16f, 0.2f, 0.1f),
            CloudCover = 0.68f,
            Gain = new Color(0.84f, 0.98f, 0.74f),
            Tint = new Color(0.92f, 1.03f, 0.82f),
        };
    }
}
