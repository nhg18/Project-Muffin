using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>강아지 자세 — 시안 DOGPILE.legAng 의 pose (다리 · 꼬리 움직임).</summary>
    public enum GaepanPose
    {
        Stand,
        Run,
        Slide,
        Fly,
        Flail,
        Tuck,
        Shake,
    }

    /// <summary>강아지 한 마리를 한 프레임 그릴 값 — 시안 DOGPILE.dog(k, X, Y, Z, o) 의 인자. 각은 라디안.</summary>
    public struct GaepanDogState
    {
        /// <summary>강아지 종류 — FxDog.Keys (na · choco · kongi · bori).</summary>
        public string Key;
        /// <summary>발바닥 자리 (눈 마을 좌표).</summary>
        public Vector3 Position;
        /// <summary>크기 (시안 s — 키 = s × 1.05).</summary>
        public float Size;
        /// <summary>바라보는 방향 (시안 yaw — 카메라를 보면 π).</summary>
        public float Yaw;
        public float Pitch;
        public float Roll;
        /// <summary>세로 늘림 (시안 sy).</summary>
        public float StretchY;
        public GaepanPose Pose;
        /// <summary>다리 · 꼬리 움직임의 위상 (시안 ph).</summary>
        public float Phase;
        /// <summary>다리를 섞을 둘째 자세 — <see cref="PoseBlend"/> 만큼 (시안 "flail>tuck:0.4").</summary>
        public GaepanPose PoseTo;
        /// <summary>둘째 자세의 몫 (0 = Pose 그대로, 1 = PoseTo).</summary>
        public float PoseBlend;
        /// <summary>발밑 동그란 그림자를 그리지 않는다 (시안 shadow:false — 던져졌거나 하늘에 있는 강아지).</summary>
        public bool NoBlob;
    }

    /// <summary>
    /// 눈 마을(개판 오분전)의 강아지 한 마리 — 카메라를 보는 그림, 해를 보는 그림자 그림(땅에 눕혀 그린다), 발밑 동그란 그림자.
    /// 시안 DOGPILE.dog(U3) · D3R.buildDogs 와 같다: 카메라 오른쪽 · (카메라 위 40% + 하늘 60%) 를 판으로 세우고, 해 쪽을 보는 판으로 한 번 더 그려 그림자를 만든다.
    /// </summary>
    public class GaepanDog : MonoBehaviour
    {
        // 시안: 강아지 키 = s × 1.05, 발밑 그림자는 높이 1.4 밑에서만
        private const float HeightPerSize = 1.05f;
        private const float BlobMaxHeight = 1.4f;
        private const string ColorProperty = "_Color";

        /// <summary>강아지 종류 하나의 그림.</summary>
        [Serializable]
        public struct Look
        {
            public string key;
            public Sprite body;
            public Sprite tail;
            public Sprite leg;
        }

        [Tooltip("카메라를 보는 그림.")]
        [SerializeField] private GaepanDogFigure figure;
        [Tooltip("해를 보는 그림 — 그림자 머티리얼로 땅에 눕혀 그린다.")]
        [SerializeField] private GaepanDogFigure shadow;
        [Tooltip("발밑 동그란 그림자 (반지름 1 원판).")]
        [SerializeField] private Transform blob;
        [SerializeField] private Renderer blobRenderer;
        [Tooltip("강아지 종류별 그림 (FxDog.Keys).")]
        [SerializeField] private Look[] looks;
        [Tooltip("판의 위쪽 = 카메라 위쪽을 이만큼, 나머지는 하늘 쪽 (시안 0.4).")]
        [SerializeField] private float cameraUpShare = 0.4f;
        [Tooltip("바라보는 방향이 카메라와 이만큼(sin) 넘게 오른쪽이면 그림을 뒤집는다 (시안 0.15).")]
        [SerializeField] private float flipThreshold = 0.15f;
        [Tooltip("몸 기울기에 들어가는 roll · pitch 비율 (시안 0.85 · 0.75).")]
        [SerializeField] private Vector2 tiltShare = new Vector2(0.85f, 0.75f);
        [Tooltip("발밑 그림자가 땅에서 뜨는 높이.")]
        [SerializeField] private float blobLift = 0.03f;
        [Tooltip("발밑 그림자 반지름 = 크기 × 이 값 × (1 − 높이 비율 × 0.5) (시안 0.5 × 1.15).")]
        [SerializeField] private float blobRadius = 0.575f;
        [Tooltip("발밑 그림자 진하기 (시안 0.24 × 3, 1 이 최대).")]
        [SerializeField] private float blobAlpha = 0.72f;

        private readonly float[] _legAngles = new float[4];
        private MaterialPropertyBlock _block;
        private string _key;

        private void Awake()
        {
            var missing = new List<string>();
            void Check(UnityEngine.Object value, string name)
            {
                if (value == null)
                    missing.Add(name);
            }

            Check(figure, nameof(figure));
            Check(shadow, nameof(shadow));
            Check(blob, nameof(blob));
            Check(blobRenderer, nameof(blobRenderer));
            if (looks == null || looks.Length == 0)
                missing.Add(nameof(looks));
            if (missing.Count > 0)
                Debug.LogError($"[{nameof(GaepanDog)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
            _block = new MaterialPropertyBlock();
        }

        /// <summary>한 프레임 그린다. 방향 · 땅 높이는 <paramref name="world"/> 의 지금 카메라 · 해에서 얻는다.</summary>
        public void Draw(in GaepanDogState state, GaepanWorld world)
        {
            SetKey(state.Key);
            transform.localPosition = state.Position;

            bool flip = Mathf.Sin(state.Yaw - world.CameraYaw) > flipThreshold;
            float roll = state.Roll * tiltShare.x + (flip ? 1f : -1f) * state.Pitch * tiltShare.y;
            bool slide = state.Pose == GaepanPose.Slide;
            bool calm = state.Pose == GaepanPose.Stand || state.Pose == GaepanPose.Shake;
            float tailAngle = Mathf.Sin(state.Phase * 2.2f) * (calm ? 0.18f : 0.4f);
            for (int i = 0; i < _legAngles.Length; i++)
            {
                float angle = LegAngle(state.Pose, state.Phase, i);
                _legAngles[i] = state.PoseBlend > 0f ? Mathf.Lerp(angle, LegAngle(state.PoseTo, state.Phase, i), state.PoseBlend) : angle;
            }

            float scale = state.Size * HeightPerSize / FxDog.Height;
            Vector3 right = world.CameraRight;
            Vector3 up = (world.CameraUp * cameraUpShare + Vector3.up * (1f - cameraUpShare)).normalized;
            figure.transform.rotation = Quaternion.LookRotation(Vector3.Cross(right, up), up);
            figure.transform.localScale = Vector3.one * scale;
            figure.Apply(roll, flip, state.StretchY, slide, tailAngle, _legAngles);

            Vector3 light = world.LightDirection;
            Vector3 lightRight = new Vector3(-light.z, 0f, light.x).normalized;
            shadow.transform.rotation = Quaternion.LookRotation(Vector3.Cross(lightRight, Vector3.up), Vector3.up);
            shadow.transform.localScale = Vector3.one * scale;
            shadow.Apply(roll, flip, state.StretchY, slide, tailAngle, _legAngles);

            float height = state.Position.y;
            bool showBlob = !state.NoBlob && height < BlobMaxHeight;
            if (blob.gameObject.activeSelf != showBlob)
                blob.gameObject.SetActive(showBlob);
            if (!showBlob)
                return;
            float radius = state.Size * blobRadius * (1f - height / BlobMaxHeight * 0.5f);
            float ground = GaepanWorld.GroundHeight(state.Position.x, state.Position.z);
            blob.localPosition = new Vector3(0f, ground + blobLift - height, 0f);
            blob.rotation = Quaternion.identity;
            blob.localScale = new Vector3(radius, 1f, radius * 0.5f);
            _block.SetColor(ColorProperty, new Color(1f, 1f, 1f, Mathf.Min(1f, blobAlpha * (1f - height / BlobMaxHeight * 0.6f))));
            blobRenderer.SetPropertyBlock(_block);
        }

        /// <summary>다리 각 (그림 좌표 라디안, + = 발이 앞으로) — 시안 DOGPILE.legAng. leg 는 FxDog.Legs 순서.</summary>
        public static float LegAngle(GaepanPose pose, float phase, int leg)
        {
            bool front = FxDog.LegFront[leg];
            float forward = front ? 1f : -1f;
            switch (pose)
            {
                case GaepanPose.Run:
                {
                    // 갤럽: 쭉 뻗을 땐 빠르게, 모을 땐 천천히
                    float p = phase + (front ? 0f : Mathf.PI) + (FxDog.LegFar[leg] ? 0.42f : 0f);
                    return (Mathf.Sin(p) + 0.24f * Mathf.Sin(2f * p)) * 0.58f + (front ? 0.1f : -0.08f);
                }
                case GaepanPose.Slide:
                    return forward * 1.2f + Mathf.Sin(phase * 0.9f + leg * 1.3f) * 0.1f;
                case GaepanPose.Fly:
                    return forward * 0.9f + Mathf.Sin(phase * 0.8f + leg * 1.1f) * 0.16f;
                case GaepanPose.Flail:
                    return Mathf.Sin(phase * 1.15f + leg * 1.7f) * 0.72f + forward * 0.18f;
                case GaepanPose.Tuck:
                    return forward * 0.45f;
                case GaepanPose.Shake:
                    return Mathf.Sin(phase * 3f + leg) * 0.08f;
                default:
                    return 0f;
            }
        }

        private void SetKey(string key)
        {
            if (key == _key)
                return;
            _key = key;
            Look look = looks[0];
            foreach (Look candidate in looks)
            {
                if (candidate.key == key)
                    look = candidate;
            }
            figure.SetSprites(look.body, look.tail, look.leg);
            shadow.SetSprites(look.body, look.tail, look.leg);
        }
    }
}
