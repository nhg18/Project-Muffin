using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Goat
{
    /// <summary>
    /// 「GOAT」 컷인의 3D 장면 — 달밤의 돌 회랑 끝에서 성문이 열리고 왕이 걸어 나온다 (시안 goat_cutin.js 의 scene().frame(t) 중 3D 쪽).
    /// 회랑 · 문은 시작할 때 코드로 짓고(<see cref="GoatHall"/>), 매 프레임 <see cref="Frame"/> 이 그 시각의 카메라 · 빛 · 문 · 떠다니는 것(구름 · 안개 · 재 · 불 · 빛줄기)을 놓는다.
    /// 카메라는 렌더 텍스처에 그리고, 화면에는 연출 판의 RawImage 로 보인다. 왕 강아지 · 제목은 3D 가 아니라 그 위의 UI 다 — 왕이 설 자리는 <see cref="Project"/> 로 알려 준다.
    /// 게임 화면의 3D 카메라에 걸리지 않게 시작할 때 캔버스 밖 먼 곳으로 떼어 놓는다 — 이 오브젝트는 연출이 지워질 때 같이 지운다.
    /// 좌표 · 수치는 시안 그대로 (x 오른쪽 · y 위 · z 는 카메라 쪽이 +). Unity 에 놓을 때만 z 를 뒤집는다 (<see cref="Flip"/>).
    /// </summary>
    public class GoatWorld : MonoBehaviour
    {
        /// <summary>시안 화면 크기 — 왕이 설 자리를 이 크기의 px 로 알려 준다.</summary>
        public const float DesignWidth = 1280f, DesignHeight = 720f;

        /// <summary>왕이 발을 구르는 순간 (시안 T0, 초) — 연기 고리가 퍼지고 불티가 튄다.</summary>
        public const float StompTime = 5f;

        private const float Tau = 6.2832f;
        // 문: 열리기 시작 · 다 열림 (초) · 다 열린 각(라디안) · 문 한 짝의 폭
        private const float OpenStart = 0.7f, OpenEnd = 2.9f, OpenAngle = 1.3f, DoorWidth = 5f, DoorZ = -29.4f;
        // 왕: 걷기 시작 · 끝 (초) · 처음 z · 멈추는 z · 걸음 빠르기
        private const float WalkStart = 1.6f, WalkEnd = 4.6f, KingFromZ = -36f, KingToZ = -22f, StepRate = 2.05f;
        // 장면 셋: 회랑 끝에서 다가감 → 왕 옆에서 올려다봄 → 정면
        private const float SideShotTime = 3.1f, FrontShotTime = 4.7f;
        private const float NearClip = 0.1f, FarClip = 900f;

        private static readonly int AmbientId = Shader.PropertyToID("_GoatAmbient");
        private static readonly int SunDirId = Shader.PropertyToID("_GoatSunDir");
        private static readonly int SunId = Shader.PropertyToID("_GoatSun");
        private static readonly int FogId = Shader.PropertyToID("_GoatFog");
        private static readonly int EmissiveId = Shader.PropertyToID("_GoatEmissive");
        private static readonly int LightPosId = Shader.PropertyToID("_GoatLightPos");
        private static readonly int LightColorId = Shader.PropertyToID("_GoatLightColor");
        private static readonly int HorizonId = Shader.PropertyToID("_GoatHorizon");
        private static readonly Comparison<Smoke> FarFirst = (a, b) => b.Distance.CompareTo(a.Distance);

        [Tooltip("회랑을 찍는 카메라 — 렌더 텍스처에 그린다.")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("렌더 텍스처를 보여 줄 연출 판의 그림.")]
        [SerializeField] private RawImage view;
        [Tooltip("회랑 메시를 넣을 곳 (시작할 때 코드로 짓는다).")]
        [SerializeField] private MeshFilter hall;
        [Tooltip("성문 두 짝 — 경첩 자리에 놓여 있다. 오른쪽은 x 를 뒤집어 놓았다.")]
        [SerializeField] private MeshFilter doorLeft;
        [SerializeField] private MeshFilter doorRight;
        [Tooltip("연기 · 구름 · 재 (보통 섞기, 먼 것부터).")]
        [SerializeField] private MeshFilter smoke;
        [Tooltip("달 · 문 빛줄기 · 불 · 불티 (더하기).")]
        [SerializeField] private MeshFilter glow;
        [Tooltip("떼어 놓을 자리 — 게임 화면 카메라(먼 쪽 1000)가 닿지 않는 곳.")]
        [SerializeField] private Vector3 farAway = new Vector3(4000f, -5000f, 0f);
        [SerializeField] private int antiAliasing = 4;

        [Header("빛 · 안개 (시안 frame)")]
        [SerializeField] private Color ambient = new Color(0.075f, 0.085f, 0.105f);
        [Tooltip("달빛이 오는 쪽 (시안 좌표).")]
        [SerializeField] private Vector3 moonFrom = new Vector3(-0.35f, 0.75f, 0.55f);
        [SerializeField] private Color moon = new Color(0.2f, 0.23f, 0.28f);
        [SerializeField] private Color fog = new Color(0.125f, 0.135f, 0.15f);
        [SerializeField] private float fogDensity = 0.0125f;
        [Tooltip("문이 닫혀 있을 때 창 · 문 뒤 빛의 세기 (다 열리면 1).")]
        [SerializeField] private float closedEmissive = 0.12f;
        [Tooltip("문 뒤에서 쏟아지는 빛 (다 열렸을 때).")]
        [ColorUsage(false, true)]
        [SerializeField] private Color gateLight = new Color(7.5f, 9f, 11.5f);
        [Tooltip("모닥불 · 문 옆 화로의 빛 (일렁임을 곱한다).")]
        [ColorUsage(false, true)]
        [SerializeField] private Color bonfireLight = new Color(3.4f, 1.5f, 0.35f);
        [ColorUsage(false, true)]
        [SerializeField] private Color torchLight = new Color(3.2f, 1.4f, 0.4f);
        [Tooltip("왕 앞을 비추는 빛 (다 열렸을 때).")]
        [ColorUsage(false, true)]
        [SerializeField] private Color kingLight = new Color(1.3f, 1.5f, 1.9f);

        /// <summary>왕이 서 있는 z (시안 dz) — 문 안쪽에서 걸어 나온다.</summary>
        public float KingZ { get; private set; }

        /// <summary>걸음 위상 (시안 wph) — 발을 디딜 때마다 π.</summary>
        public float WalkPhase { get; private set; }

        /// <summary>걷는 정도 0 ~ 1 (시안 wk) — 걷기 시작 · 끝에서 0.</summary>
        public float WalkAmount { get; private set; }

        private readonly Vector4[] _lightPositions = new Vector4[4];
        private readonly Vector4[] _lightColors = new Vector4[4];
        private readonly List<Smoke> _smokes = new List<Smoke>();
        private GoatHall _hall;
        private Batch _smokeBatch, _glowBatch;
        private RenderTexture _texture;
        private Vector3 _right, _up;
        private float _zoom;

        // 연기 한 점 — 먼 것부터 그리려고 모아 둔다
        private struct Smoke
        {
            public Vector3 Position;
            public float Size;
            public Color Color;
            public float Distance;
        }

        private void Awake()
        {
            if (worldCamera == null || view == null || hall == null || doorLeft == null || doorRight == null || smoke == null || glow == null)
                Debug.LogError($"[{nameof(GoatWorld)}] 참조 중 비어 있는 것이 있습니다.", this);

            // 캔버스 아래에서는 크기가 캔버스 배율을 따라가고 게임 화면 카메라에 걸릴 수 있다 — 장면 맨 위, 먼 곳으로
            transform.SetParent(null, false);
            transform.SetPositionAndRotation(farAway, Quaternion.identity);
            transform.localScale = Vector3.one;
            worldCamera.enabled = false;

            _hall = GoatHall.Build();
            hall.sharedMesh = _hall.Hall;
            doorLeft.sharedMesh = _hall.Door;
            doorRight.sharedMesh = _hall.Door;
            _smokeBatch = new Batch("GoatSmoke");
            _glowBatch = new Batch("GoatGlow");
            smoke.sharedMesh = _smokeBatch.Mesh;
            glow.sharedMesh = _glowBatch.Mesh;
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            if (_hall == null)
                return;

            Destroy(_hall.Hall);
            Destroy(_hall.Door);
            Destroy(_smokeBatch.Mesh);
            Destroy(_glowBatch.Mesh);
        }

        /// <summary>재생 준비 — 화면에 보일 크기(px)의 렌더 텍스처를 만들어 카메라 · 화면에 잇는다.</summary>
        public void Begin(int width, int height)
        {
            width = Mathf.Max(width, 2);
            height = Mathf.Max(height, 2);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                ReleaseTexture();
                _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "GoatWorld", antiAliasing = Mathf.Max(1, antiAliasing) };
                _texture.Create();
            }

            worldCamera.targetTexture = _texture;
            worldCamera.aspect = DesignWidth / DesignHeight;
            worldCamera.nearClipPlane = NearClip;
            worldCamera.farClipPlane = FarClip;
            view.texture = _texture;
            worldCamera.enabled = true;
        }

        /// <summary>재생이 끝나면 카메라를 멈추고 렌더 텍스처를 내린다.</summary>
        public void End()
        {
            worldCamera.enabled = false;
            view.texture = null;
            ReleaseTexture();
        }

        /// <summary>시각 t(초)의 장면을 놓는다.</summary>
        public void Frame(float t)
        {
            float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OpenStart, OpenEnd, t));
            float swing = open * OpenAngle;
            // 두 문 사이로 벌어진 틈의 반 폭
            float gap = DoorWidth * (1f - Mathf.Cos(swing)) + 0.02f;
            float walk = Mathf.InverseLerp(WalkStart, WalkEnd, t);
            KingZ = Mathf.Lerp(KingFromZ, KingToZ, Mathf.SmoothStep(0f, 1f, walk));
            WalkPhase = (KingZ - KingFromZ) * StepRate;
            WalkAmount = Mathf.Clamp01(Mathf.Sin(Mathf.PI * walk) * 4f);

            // 흔들림: 문이 덜컹 · 열리는 동안 · 다 열려 부딪힘 · 발 구르기 · 글자가 박힐 때마다
            float shake = 0.07f * Mathf.Clamp01(1f - Mathf.Abs(t - 0.85f) / 0.4f)
                + 0.05f * Mathf.InverseLerp(0.9f, 2.6f, t) * (t < OpenEnd ? 1f : 0f)
                + 0.16f * Mathf.Clamp01(1f - Mathf.Abs(t - OpenEnd) / 0.3f)
                + 0.22f * Mathf.Clamp01(1f - (t - StompTime) / 0.35f) * (t >= StompTime ? 1f : 0f);
            for (int i = 0; i < GoatTitle.Letters; i++)
            {
                float land = GoatTitle.LandTime(i);
                if (t >= land)
                    shake += 0.34f * Mathf.Clamp01(1f - (t - land) / 0.3f);
            }

            Vector3 eye, at;
            float fieldOfView;
            if (t < SideShotTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, SideShotTime, t));
                eye = new Vector3(Mathf.Lerp(2.1f, 0.6f, k), Mathf.Lerp(0.8f, 1.15f, k), Mathf.Lerp(28f, 15f, k));
                at = new Vector3(0f, Mathf.Lerp(6.5f, 9f, k), -30f);
                fieldOfView = 58f;
            }
            else if (t < FrontShotTime)
            {
                float k = Mathf.InverseLerp(SideShotTime, FrontShotTime, t);
                eye = new Vector3(-3.4f, 0.45f, KingZ + Mathf.Lerp(7.2f, 6.2f, k));
                at = new Vector3(-0.1f, 2.75f, KingZ);
                fieldOfView = 40f;
                // 발을 디딜 때마다 쿵
                shake += 0.035f * Mathf.Pow(Mathf.Abs(Mathf.Sin(WalkPhase)), 6f) * WalkAmount;
            }
            else
            {
                float k = EaseOut(Mathf.InverseLerp(FrontShotTime, GoatFx.Duration, t));
                eye = new Vector3(0f, 0.55f, Mathf.Lerp(-13f, -14f, k));
                at = new Vector3(0f, 4.3f, -22f);
                fieldOfView = 60f;
            }
            eye.x += Mathf.Sin(t * 67f) * shake;
            eye.y += Mathf.Sin(t * 89f + 1f) * shake;

            Transform cameraTransform = worldCamera.transform;
            cameraTransform.localPosition = Flip(eye);
            cameraTransform.localRotation = Quaternion.LookRotation(Flip(at) - Flip(eye), Vector3.up);
            worldCamera.fieldOfView = fieldOfView;
            _right = cameraTransform.localRotation * Vector3.right;
            _up = cameraTransform.localRotation * Vector3.up;
            _zoom = 1f / Mathf.Tan(fieldOfView * Mathf.Deg2Rad / 2f);

            doorLeft.transform.localRotation = Quaternion.Euler(0f, -swing * Mathf.Rad2Deg, 0f);
            doorRight.transform.localRotation = Quaternion.Euler(0f, swing * Mathf.Rad2Deg, 0f);

            // 불빛 일렁임
            float flicker = 0.82f + 0.16f * Mathf.Sin(t * 19f) + 0.1f * Mathf.Sin(t * 31f);
            SetLight(0, new Vector3(0f, 8f, -26.5f), gateLight * open);
            SetLight(1, GoatHall.Bonfire + new Vector3(0f, 1.3f, 0f), bonfireLight * flicker);
            SetLight(2, new Vector3(0f, 3.4f, -24.6f), torchLight * flicker);
            SetLight(3, new Vector3(0f, 3f, KingZ + 5f), kingLight * open);
            Shader.SetGlobalVectorArray(LightPosId, _lightPositions);
            Shader.SetGlobalVectorArray(LightColorId, _lightColors);
            Shader.SetGlobalColor(AmbientId, ambient);
            Shader.SetGlobalVector(SunDirId, Flip(moonFrom).normalized);
            Shader.SetGlobalColor(SunId, moon);
            Shader.SetGlobalVector(FogId, new Vector4(fog.r, fog.g, fog.b, fogDensity));
            Shader.SetGlobalFloat(EmissiveId, closedEmissive + (1f - closedEmissive) * open);
            // 지평선의 화면 높이 (−1 ~ 1) — 카메라가 올려다볼수록 내려간다
            Vector3 forward = (at - eye).normalized;
            Shader.SetGlobalFloat(HorizonId, -forward.y / Mathf.Sqrt(1f - forward.y * forward.y) * _zoom);

            Float(t, open, gap, flicker, eye);
        }

        /// <summary>
        /// 시안 좌표의 한 점이 화면 어디에 보이는지 — (x, y) = 시안 화면(1280 × 720, 왼쪽 위 기준 · y 아래 +) px, z = 그 거리에서 길이 1 이 몇 px 인지, w = 카메라에서의 거리(0 이하면 뒤).
        /// <see cref="Frame"/> 다음에 부른다.
        /// </summary>
        public Vector4 Project(Vector3 point)
        {
            Transform cameraTransform = worldCamera.transform;
            Vector3 local = Quaternion.Inverse(cameraTransform.localRotation) * (Flip(point) - cameraTransform.localPosition);
            if (local.z <= 0f)
                return new Vector4(0f, 0f, 0f, local.z);

            float x = local.x * _zoom / (DesignWidth / DesignHeight) / local.z;
            float y = local.y * _zoom / local.z;
            return new Vector4((x * 0.5f + 0.5f) * DesignWidth, (1f - (y * 0.5f + 0.5f)) * DesignHeight, DesignHeight / 2f * _zoom / local.z, local.z);
        }

        // 떠다니는 것들 — 시안 frame().sprites
        private void Float(float t, float open, float gap, float flicker, Vector3 eye)
        {
            _smokes.Clear();
            _glowBatch.Clear();

            // 먼 구름 · 달(알맹이 + 달무리)
            foreach (Vector4 cloud in _hall.Clouds)
                AddSmoke(cloud, cloud.w, new Color(0.07f, 0.075f, 0.085f, 0.6f), eye);
            var moonAt = new Vector3(-70f, 95f, -260f);
            Disc(_glowBatch, moonAt, 9f, new Color(0.85f, 0.9f, 1f, 0.55f));
            Disc(_glowBatch, moonAt, 46f, new Color(0.6f, 0.68f, 0.8f, 0.16f));

            // 바닥 안개 — 옆으로 흐른다
            for (int i = 0; i < _hall.Fog.Length; i++)
            {
                Vector4 f = _hall.Fog[i];
                Vector2 drift = _hall.FogDrift[i];
                AddSmoke(new Vector3((f.x + t * 0.5f + 15f) % 30f - 15f, f.y + Mathf.Sin(t * 0.4f + drift.x) * 0.15f, f.z), f.w, new Color(0.52f, 0.55f, 0.6f, drift.y), eye);
            }

            if (open > 0f)
            {
                // 문틈으로 밀려 나오는 김
                for (int i = 0; i < 26; i++)
                {
                    float phase = (i / 26f + t * 0.11f) % 1f, side = Mathf.Sin(i * 12.9f) * 0.5f;
                    AddSmoke(new Vector3(side * (gap + phase * 9f), 0.9f + Mathf.Abs(Mathf.Sin(i * 7.3f)) * 2.6f, -28.5f + phase * 24f), 2.6f + phase * 4f,
                        new Color(0.74f, 0.8f, 0.88f, 0.3f * open * (1f - phase)), eye);
                }

                // 바닥에 깔리는 빛 · 문틈에서 뻗는 빛줄기 다섯 · 문 안의 빛
                _glowBatch.Quad(Flip(new Vector3(-gap, 0.06f, -27.6f)), Flip(new Vector3(gap, 0.06f, -27.6f)), Flip(new Vector3(gap * 3.2f + 2f, 0.06f, 14f)), Flip(new Vector3(-gap * 3.2f - 2f, 0.06f, 14f)),
                    new Color(0.75f, 0.84f, 1f, 0.22f * open), Batch.Shaft);
                for (int i = -2; i <= 2; i++)
                {
                    float x = i / 2f * gap * 0.9f;
                    _glowBatch.Quad(Flip(new Vector3(x, 0f, -28f)), Flip(new Vector3(x, 21f, -28f)), Flip(new Vector3(x * 3.4f, 26f, 9f)), Flip(new Vector3(x * 3.4f, 0f, 9f)),
                        new Color(0.72f, 0.82f, 1f, 0.045f * open), Batch.Shaft);
                }
                var gate = new Vector3(0f, 10f, -28.6f);
                Disc(_glowBatch, gate, 4f + 9f * open, new Color(0.75f, 0.84f, 1f, 0.26f * open));
                Disc(_glowBatch, gate, 24f, new Color(0.55f, 0.66f, 0.85f, 0.13f * open));
            }

            // 문이 열리는 동안 위에서 떨어지는 돌가루
            if (t > OpenStart && t < 3.6f)
            {
                float amount = Mathf.InverseLerp(OpenStart, 1.1f, t) * (1f - Mathf.InverseLerp(3f, 3.6f, t));
                for (int i = 0; i < 30; i++)
                {
                    float phase = (i / 30f + t * 0.45f) % 1f;
                    AddSmoke(new Vector3(Mathf.Sin(i * 5.1f) * 5.2f, 22f - phase * 20f, -27.6f + Mathf.Sin(i * 3.3f) * 0.5f), 0.5f + phase * 0.9f, new Color(0.5f, 0.5f, 0.52f, 0.3f * (1f - phase) * amount), eye);
                }
            }

            // 내리는 재
            for (int i = 0; i < _hall.Ash.Length; i++)
            {
                Vector3 a = _hall.Ash[i];
                Vector2 drift = _hall.AshDrift[i];
                AddSmoke(new Vector3(a.x + Mathf.Sin(t * 0.6f + drift.x) * 0.7f, 15f - (a.y + t * 0.55f * drift.y) % 15f, a.z), 0.045f, new Color(0.8f, 0.8f, 0.82f, 0.7f), eye);
            }

            // 모닥불 (불꽃 넷 + 빛무리) · 불티
            Vector3 fire = GoatHall.Bonfire;
            for (int j = 0; j < 4; j++)
                Disc(_glowBatch, new Vector3(fire.x + Mathf.Sin(t * 9f + j * 2f) * 0.1f, 0.5f + j * 0.36f + Mathf.Sin(t * 13f + j) * 0.07f, fire.z), 0.8f - j * 0.16f, new Color(1f, 0.5f + 0.12f * j, 0.14f, 0.85f * flicker));
            Disc(_glowBatch, new Vector3(fire.x, 1f, fire.z), 4.5f, new Color(1f, 0.45f, 0.12f, 0.14f * flicker));
            foreach (Vector3 ember in _hall.Embers)
            {
                float phase = (ember.x + t * 0.22f * ember.z) % 1f;
                Disc(_glowBatch, new Vector3(fire.x + Mathf.Sin(t * 1.3f + ember.y) * (0.2f + phase * 0.9f), 0.6f + phase * 6f, fire.z + Mathf.Cos(t + ember.y) * 0.3f), 0.045f, new Color(1f, 0.55f, 0.18f, 1f - phase));
            }

            // 문 옆 화로 둘
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < 3; j++)
                    Disc(_glowBatch, new Vector3(side * 7.3f + Mathf.Sin(t * 9f + j * 2f + side) * 0.08f, 3.2f + j * 0.3f, -25.4f), 0.55f - j * 0.13f, new Color(1f, 0.5f + 0.12f * j, 0.14f, 0.7f * flicker));
            }

            // 발 구르기: 퍼지는 연기 고리 · 튀는 불티
            if (t >= StompTime)
            {
                float since = t - StompTime, spread = EaseOut(Mathf.Clamp01(since / 1.1f));
                for (int i = 0; i < 40; i++)
                {
                    float angle = i / 40f * Tau;
                    AddSmoke(new Vector3(Mathf.Cos(angle) * (1f + spread * 11f), 0.5f + spread * 0.5f, KingToZ + Mathf.Sin(angle) * (1f + spread * 11f)), 1.6f + spread * 2.6f,
                        new Color(0.7f, 0.75f, 0.82f, 0.34f * (1f - spread)), eye);
                }
                foreach (Vector4 b in _hall.Burst)
                {
                    float life = since / (1.6f + b.w * 1.6f);
                    if (life < 1f)
                        Disc(_glowBatch, new Vector3(Mathf.Cos(b.x) * b.y * (1f + life * 0.8f), 0.2f + life * (3f + b.z * 5f), KingToZ + Mathf.Sin(b.x) * b.y * (1f + life * 0.8f)), 0.06f, new Color(1f, 0.5f, 0.16f, 1f - life));
                }
            }

            _smokes.Sort(FarFirst);
            _smokeBatch.Clear();
            foreach (Smoke s in _smokes)
                Disc(_smokeBatch, s.Position, s.Size, s.Color);
            _smokeBatch.Apply();
            _glowBatch.Apply();
        }

        private void AddSmoke(Vector3 position, float size, Color color, Vector3 eye)
        {
            _smokes.Add(new Smoke { Position = position, Size = size, Color = color, Distance = (position - eye).sqrMagnitude });
        }

        // 카메라를 마주 보는 둥근 빛 — 가운데 · 반 크기 (시안 좌표)
        private void Disc(Batch batch, Vector3 center, float size, Color color)
        {
            Vector3 c = Flip(center), right = _right * size, up = _up * size;
            batch.Quad(c - right - up, c + right - up, c + right + up, c - right + up, color, Batch.Round);
        }

        private void SetLight(int index, Vector3 position, Color color)
        {
            _lightPositions[index] = transform.TransformPoint(Flip(position));
            _lightColors[index] = color;
        }

        private void ReleaseTexture()
        {
            if (_texture == null)
                return;

            if (worldCamera != null && worldCamera.targetTexture == _texture)
                worldCamera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        // 시안 좌표 → 이 오브젝트 안의 Unity 좌표
        private static Vector3 Flip(Vector3 v) => new Vector3(v.x, v.y, -v.z);

        private static float EaseOut(float k) => 1f - Mathf.Pow(1f - k, 3f);

        // 매 프레임 다시 채우는 네모 묶음 (셰이더 「Goat Sprite」) — uv.xy = 네모 안 자리, uv.z = 모양
        private sealed class Batch
        {
            public const float Round = 0f, Shaft = 1f;

            public readonly Mesh Mesh;

            private readonly List<Vector3> _positions = new List<Vector3>();
            private readonly List<Vector3> _uvs = new List<Vector3>();
            private readonly List<Color> _colors = new List<Color>();
            private readonly List<int> _indices = new List<int>();

            public Batch(string name)
            {
                Mesh = new Mesh { name = name };
                Mesh.MarkDynamic();
            }

            public void Clear()
            {
                _positions.Clear();
                _uvs.Clear();
                _colors.Clear();
                _indices.Clear();
            }

            /// <summary>네모 하나 (a → b → c → d 둘레 순서). 빛줄기는 a · b 쪽이 밝고 c · d 쪽으로 사라진다.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, float shape)
            {
                int start = _positions.Count;
                float near = shape == Round ? -1f : 0f;
                _positions.Add(a);
                _positions.Add(b);
                _positions.Add(c);
                _positions.Add(d);
                _uvs.Add(new Vector3(-1f, near, shape));
                _uvs.Add(new Vector3(1f, near, shape));
                _uvs.Add(new Vector3(1f, 1f, shape));
                _uvs.Add(new Vector3(-1f, 1f, shape));
                for (int i = 0; i < 4; i++)
                    _colors.Add(color);
                _indices.Add(start);
                _indices.Add(start + 1);
                _indices.Add(start + 2);
                _indices.Add(start);
                _indices.Add(start + 2);
                _indices.Add(start + 3);
            }

            public void Apply()
            {
                Mesh.Clear();
                Mesh.SetVertices(_positions);
                Mesh.SetUVs(0, _uvs);
                Mesh.SetColors(_colors);
                Mesh.SetTriangles(_indices, 0, false);
                // 카메라가 어디를 보든 잘리지 않게
                Mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
            }
        }
    }
}
