using System;
using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 3D 장면의 시간표 (초, 장면 시간축) — 시안 NUKE3D 의 T · HOLD 와 같다. 장면 · 도시 · 강아지 · 입자가 같이 본다.
    /// 장면 시간은 착탄 직전 느린 화면 동안 거의 멈춘다 (<see cref="SceneTime"/>). 미사일은 <see cref="MissileStart"/> 에 나타나 <see cref="Hit"/> 에 땅에 닿는다.
    /// 값은 <see cref="NukeSceneFx"/> 의 Inspector 에서 바꾼다.
    /// </summary>
    [Serializable]
    public struct NukeTimeline
    {
        [Tooltip("사이렌 — 차가 서고, 2 장면(강아지 눈높이)이 시작한다.")]
        public float Siren;
        [Tooltip("강아지가 사이렌을 알아채기 시작하는 때 (마리마다 0~0.5초 늦게).")]
        public float Notice;
        [Tooltip("3 장면(하늘을 올려다봄) — 강아지가 겁먹기 시작한다.")]
        public float Sky;
        [Tooltip("4 장면(미사일 둘레를 돎) — 강아지 머리 위 느낌표는 여기까지.")]
        public float Hero;
        [Tooltip("5 장면(앞에서 뒷걸음) — 강아지가 달아나기 시작한다 (마리마다 조금씩 늦게). 심장 박동도 여기서 시작.")]
        public float Run;
        [Tooltip("6 장면 — 옆에서 나란히 달린다.")]
        public float Side;
        [Tooltip("7 장면 — 바닥에서 미사일을 올려다본다.")]
        public float Up;
        [Tooltip("8 장면 — 미사일 뒤에 붙어 내리꽂힌다.")]
        public float Dive;
        [Tooltip("착탄 — 번쩍하고 10 장면(멀리서 본 도시)이 시작한다. 미사일이 이때 땅에 닿는다.")]
        public float Hit;
        [Tooltip("11 장면 — 큰길로 먼지 벽이 밀려온다.")]
        public float Street;
        [Tooltip("12 장면 — 낙진. 빛이 녹색으로 바뀐다.")]
        public float Fall;
        [Tooltip("장면 끝 — 12 장면 크레인이 여기서 멈춘다.")]
        public float End;
        [Tooltip("미사일이 하늘에 나타나는 때.")]
        public float MissileStart;
        [Tooltip("착탄 직전 느린 화면(9 장면)의 실제 길이 (초).")]
        public float Hold;
        [Tooltip("느린 화면 동안 흐르는 장면 시간 (초) — 착탄 이만큼 전에 느린 화면이 시작한다.")]
        public float HoldScene;

        /// <summary>시안 값.</summary>
        public static NukeTimeline Default => new NukeTimeline
        {
            Siren = 1.6f,
            Notice = 1.9f,
            Sky = 2.6f,
            Hero = 3.5f,
            Run = 4.4f,
            Side = 6.2f,
            Up = 7.4f,
            Dive = 8.2f,
            Hit = 9.8f,
            Street = 11.2f,
            Fall = 12.4f,
            End = 15.4f,
            MissileStart = 1.2f,
            Hold = 0.9f,
            HoldScene = 0.1f,
        };

        /// <summary>느린 화면이 시작하는 장면 시각.</summary>
        public float SlowStart => Hit - HoldScene;

        /// <summary>실제 흐른 시간 → 장면 시간 (느린 화면 동안 <see cref="Hold"/> 초에 <see cref="HoldScene"/> 초만 흐른다).</summary>
        public float SceneTime(float real)
        {
            if (real < SlowStart)
                return real;
            if (real < SlowStart + Hold)
                return SlowStart + (real - SlowStart) * (HoldScene / Hold);
            return real - (Hold - HoldScene);
        }

        /// <summary>장면 시각 → 그 장면이 보이는 실제 시각 (<see cref="SceneTime"/> 의 거꾸로).</summary>
        public float RealTime(float sceneTime)
        {
            if (sceneTime < SlowStart)
                return sceneTime;
            if (sceneTime < Hit)
                return SlowStart + (sceneTime - SlowStart) * (Hold / HoldScene);
            return sceneTime + (Hold - HoldScene);
        }

        /// <summary>미사일이 길을 얼마나 왔나 (0~1).</summary>
        public float MissileProgress(float t) => Mathf.Pow(Mathf.Clamp01((t - MissileStart) / (Hit - MissileStart)), 1.35f);

        /// <summary>미사일 길의 중심 (나선 없이).</summary>
        public Vector3 MissileBase(float t) => NukeMath.Bezier(MissileProgress(t));

        /// <summary>미사일 길의 방향 · 옆 · 위 (시안 mFrame).</summary>
        public void MissileFrame(float t, out Vector3 forward, out Vector3 side, out Vector3 up)
            => NukeMath.MissileFrame(MissileProgress(t), out forward, out side, out up);

        /// <summary>미사일 위치 — 제 축으로 돌며 작게 원을 그린다.</summary>
        public Vector3 MissilePos(float t) => NukeMath.MissilePos(MissileProgress(t), t);

        /// <summary>미사일 코가 보는 방향.</summary>
        public Vector3 MissileDir(float t) => NukeMath.MissileDir(MissileProgress(t), t);

        /// <summary>미사일 꽁무니 (비행운이 나오는 곳) — length 는 몸 가운데에서 꽁무니까지 (<see cref="NukeCityFx.MissileTailLength"/>).</summary>
        public Vector3 MissileTail(float t, float length) => MissilePos(t) - MissileDir(t) * length;
    }
}
