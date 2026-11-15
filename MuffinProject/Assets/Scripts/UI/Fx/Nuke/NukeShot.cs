using System;
using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>「대소동」 장면 하나에서 카메라가 움직이는 방식.</summary>
    public enum NukeShotPath
    {
        /// <summary>눈 · 보는 곳이 시작 → 끝으로 간다 (시안 좌표).</summary>
        Move,
        /// <summary>눈은 시작 → 끝, 보는 곳은 시작(시안 좌표)에서 미사일 + 끝 값으로 돌아간다.</summary>
        TurnToMissile,
        /// <summary>눈은 시작 → 끝, 보는 곳은 미사일 + 시작 → 끝 값.</summary>
        WatchMissile,
        /// <summary>눈 · 보는 곳이 미사일 기준 (옆, 위, 앞) — 미사일을 따라간다. 둘레 반지름이 있으면 그 둘레를 돈다.</summary>
        FollowMissile,
    }

    /// <summary>장면 안에서 값이 바뀌는 빠르기 곡선.</summary>
    public enum NukeEase
    {
        /// <summary>고르게.</summary>
        Linear,
        /// <summary>양끝에서 느리게 (시안 eio).</summary>
        InOut,
        /// <summary>끝에서 느리게 (시안 eo).</summary>
        Out,
    }

    /// <summary>
    /// 「대소동」 3D 장면 하나의 카메라 키 — 시안 NUKE3D camera(t) 의 장면 하나와 같다.
    /// 장면이 시작하고 끝나는 때는 <see cref="NukeTimeline"/>, 장면 순서는 <see cref="NukeSceneFx"/> 의 shots 배열 순서 (12 개).
    /// </summary>
    [Serializable]
    public struct NukeShot
    {
        /// <summary>장면 수 — <see cref="NukeTimeline"/> 이 나누는 12 장면.</summary>
        public const int Count = 12;

        [Tooltip("장면 이름 (보기용).")]
        public string Name;
        [Tooltip("카메라가 움직이는 방식.")]
        public NukeShotPath Path;
        [Tooltip("눈이 움직이는 빠르기 곡선.")]
        public NukeEase EyeEase;
        [Tooltip("보는 곳 · 확대 · 기울기 · 미사일 둘레 각도 · 방사 흐림 · 그림자 맵 가운데가 바뀌는 빠르기 곡선.")]
        public NukeEase Ease;

        [Tooltip("눈 — 장면 시작 (m, 시안 좌표: x 오른쪽 · y 위 · z 앞). 미사일을 따라가면 미사일 기준 (옆, 위, 앞).")]
        public Vector3 Eye;
        [Tooltip("눈 — 장면 끝.")]
        public Vector3 EyeEnd;
        [Tooltip("보는 곳 — 장면 시작. 미사일을 보면 미사일에서 떨어진 만큼, 미사일을 따라가면 미사일 기준 (옆, 위, 앞).")]
        public Vector3 Target;
        [Tooltip("보는 곳 — 장면 끝.")]
        public Vector3 TargetEnd;

        [Tooltip("미사일 둘레를 도는 반지름 (옆, 위 — m). 0 이면 돌지 않는다.")]
        public Vector2 Orbit;
        [Tooltip("미사일 둘레 각도 — 장면 시작 (도, 0 = 옆 · 90 = 위).")]
        public float OrbitAngle;
        [Tooltip("미사일 둘레 각도 — 장면 끝 (도).")]
        public float OrbitAngleEnd;

        [Tooltip("눈이 위로 통통 튀는 높이 (m) — 달리며 찍는 손맛.")]
        public float Bob;
        [Tooltip("통통 튀는 빠르기 (rad/초).")]
        public float BobRate;
        [Tooltip("눈이 좌우로 흔들리는 폭 (m).")]
        public float Sway;
        [Tooltip("좌우로 흔들리는 빠르기 (rad/초).")]
        public float SwayRate;

        [Tooltip("확대 — 장면 시작 (초점 거리 배율, 클수록 좁고 크게).")]
        public float Zoom;
        [Tooltip("확대 — 장면 끝.")]
        public float ZoomEnd;
        [Tooltip("판 기울기 — 장면 시작 (도).")]
        public float Roll;
        [Tooltip("판 기울기 — 장면 끝 (도).")]
        public float RollEnd;
        [Tooltip("판이 좌우로 흔들리는 각도 (도, 장면 끝에서 — 장면이 흐를수록 커진다).")]
        public float Wobble;
        [Tooltip("판이 흔들리는 빠르기 (rad/초).")]
        public float WobbleRate;

        [Tooltip("카메라 떨림 — 가로축은 장면 진행 (0 = 시작, 1 = 끝).")]
        public AnimationCurve Shake;
        [Tooltip("색 번짐 — 가로축은 장면 진행 (0 = 시작, 1 = 끝).")]
        public AnimationCurve Chroma;
        [Tooltip("방사 흐림 — 장면 시작.")]
        public float RadialBlur;
        [Tooltip("방사 흐림 — 장면 끝.")]
        public float RadialBlurEnd;
        [Tooltip("초점이 맞는 거리 (m).")]
        public float Focus;
        [Tooltip("초점 밖 흐림 세기 (0 = 끔).")]
        public float Blur;
        [Tooltip("안개 짙기 (1/m) — 0 이면 빛 상태(맑음 · 착탄 · 낙진)의 값을 쓴다.")]
        public float Fog;

        [Tooltip("그림자 맵 반 폭 (m) — 넓게 찍는 장면은 크게.")]
        public float LightExtent;
        [Tooltip("그림자 맵 가운데를 아래 값에 고정한다 — 끄면 카메라 앞.")]
        public bool PinLight;
        [Tooltip("그림자 맵 가운데 (x, z) — 장면 시작.")]
        public Vector2 LightCenter;
        [Tooltip("그림자 맵 가운데 (x, z) — 장면 끝.")]
        public Vector2 LightCenterEnd;

        /// <summary>시안 값 — 12 장면.</summary>
        public static NukeShot[] Defaults()
        {
            var shots = new NukeShot[Count];

            shots[0] = Make("1 하늘에서 네거리로 내려오는 크레인", new Vector3(-3.6f, 46f, -74f), new Vector3(-2.8f, 5.2f, -8f), new Vector3(1f, 12f, 40f), new Vector3(0f, 3.2f, 27f), 0.9f, 0.9f);
            shots[0].EyeEase = NukeEase.InOut;
            shots[0].Ease = NukeEase.InOut;
            shots[0].LightExtent = 85f;
            shots[0].PinLight = true;
            shots[0].LightCenter = new Vector2(0f, -20f);
            shots[0].LightCenterEnd = new Vector2(0f, 16f);

            shots[1] = Make("2 강아지 눈높이 — 사이렌에 멈칫", new Vector3(3.5f, 0.82f, 8.4f), new Vector3(2.5f, 0.82f, 9.8f), new Vector3(-0.4f, 1.45f, 20f), new Vector3(-0.4f, 1.45f, 20f), 1.32f, 1.32f);
            shots[1].Roll = -3.5f;
            shots[1].RollEnd = -3.5f;
            shots[1].Shake = Steady(0.002f);
            shots[1].Focus = 9f;
            shots[1].Blur = 0.5f;

            shots[2] = Make("3 하늘을 올려다보며 미사일로", new Vector3(0.7f, 1.05f, 8.5f), new Vector3(0.7f, 1.05f, 8.5f), new Vector3(0f, 1.8f, 22f), Vector3.zero, 1f, 1.9f);
            shots[2].Path = NukeShotPath.TurnToMissile;
            shots[2].Ease = NukeEase.InOut;
            shots[2].RollEnd = 4f;

            shots[3] = Make("4 미사일 둘레를 도는 카메라", new Vector3(0f, 8f, -21f), new Vector3(0f, 8f, -21f), new Vector3(0f, 0f, 9f), new Vector3(0f, 0f, 9f), 1.05f, 1.05f);
            shots[3].Path = NukeShotPath.FollowMissile;
            shots[3].Orbit = new Vector2(13f, 3f);
            shots[3].OrbitAngle = 2.3f * Mathf.Rad2Deg;
            shots[3].OrbitAngleEnd = 0.9f * Mathf.Rad2Deg;
            shots[3].Roll = -7f;
            shots[3].RollEnd = 5f;
            shots[3].Shake = Steady(0.002f);
            shots[3].LightExtent = 120f;
            shots[3].PinLight = true;
            shots[3].LightCenter = new Vector2(0f, 30f);
            shots[3].LightCenterEnd = new Vector2(0f, 30f);

            shots[4] = Make("5 달아나는 강아지들을 앞에서 뒷걸음으로", new Vector3(0f, 1.02f, 4f), new Vector3(0f, 1.02f, -12.5f), new Vector3(0f, 3.4f, 30f), new Vector3(0f, 8.5f, 30f), 0.95f, 0.95f);
            shots[4].Bob = 0.05f;
            shots[4].BobRate = 13f;
            shots[4].Sway = 0.7f;
            shots[4].SwayRate = 1.1f;
            shots[4].Wobble = 1.5f;
            shots[4].WobbleRate = 2.3f;
            shots[4].Shake = AnimationCurve.Linear(0f, 0.0025f, 1f, 0.007f);
            shots[4].Focus = 8f;
            shots[4].Blur = 0.32f;

            shots[5] = Make("6 옆에서 나란히 달리기", new Vector3(-5.5f, 1.25f, 3.6f), new Vector3(-5.5f, 1.25f, -6f), new Vector3(0f, 0.95f, 8.4f), new Vector3(0f, 0.95f, -1.2f), 1.05f, 1.05f);
            shots[5].Bob = 0.03f;
            shots[5].BobRate = 12f;
            shots[5].Roll = -2f;
            shots[5].RollEnd = -2f;
            shots[5].Shake = Steady(0.003f);
            shots[5].Focus = 9.5f;
            shots[5].Blur = 0.42f;

            shots[6] = Make("7 네거리 바닥에서 올려다본 미사일", new Vector3(0.6f, 0.55f, 14f), new Vector3(0.6f, 0.55f, 14f), Vector3.zero, Vector3.zero, 3.3f, 2.2f);
            shots[6].Path = NukeShotPath.WatchMissile;
            shots[6].Roll = -9f;
            shots[6].RollEnd = 7f;
            shots[6].Shake = AnimationCurve.Linear(0f, 0.003f, 1f, 0.007f);
            shots[6].Chroma = Steady(0.004f);

            shots[7] = Make("8 미사일 뒤에 붙어 내리꽂힘", new Vector3(3.5f, 8f, -19f), new Vector3(3.5f, 8f, -19f), new Vector3(0f, 2f, 26f), new Vector3(0f, 2f, 26f), 0.92f, 0.68f);
            shots[7].Path = NukeShotPath.FollowMissile;
            shots[7].Roll = -4f;
            shots[7].RollEnd = 12f;
            shots[7].Shake = AnimationCurve.Linear(0f, 0.004f, 1f, 0.012f);
            shots[7].Chroma = AnimationCurve.Linear(0f, 0.004f, 1f, 0.014f);
            shots[7].RadialBlur = 0.04f;
            shots[7].RadialBlurEnd = 0.14f;
            shots[7].LightExtent = 110f;
            shots[7].PinLight = true;
            shots[7].LightCenter = new Vector2(0f, 30f);
            shots[7].LightCenterEnd = new Vector2(0f, 30f);

            shots[8] = Make("9 착탄 직전 느린 화면", new Vector3(2.5f, 0.6f, -45f), new Vector3(2.5f, 0.6f, -45f), new Vector3(0f, 2.4f, 30f), new Vector3(0f, 2.4f, 30f), 1.75f, 1.75f);
            shots[8].Focus = 9f;
            shots[8].Blur = 0.22f;

            shots[9] = Make("10 번쩍 — 멀리서 본 도시 전경", new Vector3(-196f, 86f, -232f), new Vector3(-184f, 80f, -218f), new Vector3(0f, 22f, 30f), new Vector3(0f, 44f, 30f), 1.95f, 1.95f);
            shots[9].Ease = NukeEase.Out;
            // 충격파가 0.9초 뒤(장면 1.4초 중) 카메라에 닿아 흔들린다
            shots[9].Shake = new AnimationCurve(new Keyframe(0f, 0f, 0f, float.PositiveInfinity), new Keyframe(0.9f / 1.4f, 0.006f, float.PositiveInfinity, 0f));
            shots[9].Chroma = Decay(0.006f, 3f * 1.4f);
            shots[9].Fog = 0.0015f;
            shots[9].LightExtent = 170f;
            shots[9].PinLight = true;
            shots[9].LightCenter = new Vector2(0f, 30f);
            shots[9].LightCenterEnd = new Vector2(0f, 30f);

            shots[10] = Make("11 큰길로 밀려오는 먼지 벽", new Vector3(1.6f, 1.25f, -64f), new Vector3(1.6f, 1.25f, -64f), new Vector3(0f, 5f, 30f), new Vector3(0f, 10f, 30f), 1.12f, 1.12f);
            // 0.004 + 0.05 × 진행² — 먼지 벽이 다가올수록 세게
            shots[10].Shake = new AnimationCurve(new Keyframe(0f, 0.004f, 0f, 0f), new Keyframe(1f, 0.054f, 0.1f, 0f));
            shots[10].Chroma = AnimationCurve.Linear(0f, 0f, 1f, 0.01f);

            shots[11] = Make("12 낙진 — 폐허 위로 크레인 업", new Vector3(2.2f, 1.7f, -64f), new Vector3(-1.4f, 3.4f, -86f), new Vector3(0f, 9f, 30f), new Vector3(0f, 35f, 30f), 1f, 0.7f);
            shots[11].EyeEase = NukeEase.Out;
            shots[11].Ease = NukeEase.Out;
            shots[11].Shake = Steady(0.002f);
            shots[11].LightExtent = 70f;
            shots[11].PinLight = true;
            shots[11].LightCenter = new Vector2(0f, -36f);
            shots[11].LightCenterEnd = new Vector2(0f, -36f);

            return shots;
        }

        // 움직임 키만 받고 나머지는 시안 기본값 (떨림 · 색 번짐 없음, 초점 20m, 그림자 맵 반 폭 48m)
        private static NukeShot Make(string name, Vector3 eye, Vector3 eyeEnd, Vector3 target, Vector3 targetEnd, float zoom, float zoomEnd)
        {
            return new NukeShot
            {
                Name = name,
                Eye = eye,
                EyeEnd = eyeEnd,
                Target = target,
                TargetEnd = targetEnd,
                Zoom = zoom,
                ZoomEnd = zoomEnd,
                Shake = Steady(0f),
                Chroma = Steady(0f),
                Focus = 20f,
                LightExtent = 48f,
            };
        }

        private static AnimationCurve Steady(float value) => AnimationCurve.Constant(0f, 1f, value);

        // start × e^(−rate × 진행) — 마디마다 기울기를 맞춘 곡선
        private static AnimationCurve Decay(float start, float rate)
        {
            float[] at = { 0f, 0.1f, 0.22f, 0.38f, 0.6f, 1f };
            var keys = new Keyframe[at.Length];
            for (int i = 0; i < at.Length; i++)
            {
                float value = start * Mathf.Exp(-rate * at[i]);
                keys[i] = new Keyframe(at[i], value, -rate * value, -rate * value);
            }

            return new AnimationCurve(keys);
        }
    }
}
