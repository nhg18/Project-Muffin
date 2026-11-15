using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 한 프레임의 카메라 — 도시 · 강아지 · 입자가 같이 본다. 좌표는 시안 좌표 (유니티 월드 = 시안 좌표 + <see cref="Origin"/>).
    /// </summary>
    public struct NukeView
    {
        public Camera Camera;
        public Vector3 Origin;
        /// <summary>눈 위치 (시안 좌표).</summary>
        public Vector3 Eye;
        public float Yaw;
        public Vector3 Right;
        public Vector3 Up;
        public Vector3 Forward;
        /// <summary>초점 거리 (화면 단위) — 화면 높이 / 2 / tan(반 화각).</summary>
        public float Focal;
        /// <summary>화면 크기 (화면 단위).</summary>
        public Vector2 Size;
        /// <summary>시안 SHOT (1~12).</summary>
        public int Shot;
        /// <summary>빛이 나아가는 방향.</summary>
        public Vector3 LightDir;

        /// <summary>시안 P — 점을 화면 가운데 기준 좌표(위로 +)로. 카메라 뒤면 false.</summary>
        public bool Project(Vector3 point, out Vector2 position, out float scale)
        {
            Vector3 d = point - Eye;
            float z = Vector3.Dot(d, Forward);
            if (z < 0.2f)
            {
                position = Vector2.zero;
                scale = 0f;
                return false;
            }

            scale = Focal / z;
            position = new Vector2(Vector3.Dot(d, Right) * scale, Vector3.Dot(d, Up) * scale);
            return true;
        }

        /// <summary>시안 좌표 → 카메라 앞쪽 깊이.</summary>
        public float Depth(Vector3 point) => Vector3.Dot(point - Eye, Forward);
    }

    /// <summary>
    /// 「대소동」 3D 장면 (컷인 3 단계 16.4초) — 시안 NUKE3D 의 camera · env · frame 과 같다.
    /// 장면 시각마다 카메라(12 장면) · 빛 · 하늘 · 색을 정하고, 도시 · 강아지 · 입자를 이 카메라에만 그리게 한 뒤, 후처리 값을 넘긴다.
    /// 시간표 · 장면별 카메라 키 · 빛 상태(맑음 · 착탄 · 낙진) · 후처리 세기는 Inspector 에서 바꾼다 — 기본값은 시안 값.
    /// 유니티 조명 · 그림자는 쓰지 않는다 — 셰이더가 전역 값(_Nk…)으로 빛을 계산하고, 해 쪽 그림자 맵도 여기서 그린다.
    /// 재생하면 이 오브젝트를 UI 에서 떼어 <see cref="origin"/> 에 둔다 (게임 화면 카메라가 보지 않는 곳).
    /// </summary>
    public class NukeSceneFx : MonoBehaviour
    {
        private const float Deg = Mathf.Rad2Deg;
        private const string ViewName = "Nuke View";
        private const string ShadowName = "Nuke Shadow Map";

        private static readonly int OriginId = Shader.PropertyToID("_NkOrigin");
        private static readonly int FogCId = Shader.PropertyToID("_NkFogC");
        private static readonly int LdId = Shader.PropertyToID("_NkLd");
        private static readonly int SunCId = Shader.PropertyToID("_NkSunC");
        private static readonly int ShCId = Shader.PropertyToID("_NkShC");
        private static readonly int AmbId = Shader.PropertyToID("_NkAmb");
        private static readonly int GndId = Shader.PropertyToID("_NkGnd");
        private static readonly int PlId = Shader.PropertyToID("_NkPl");
        private static readonly int PlCId = Shader.PropertyToID("_NkPlC");
        private static readonly int FxId = Shader.PropertyToID("_NkFx");
        private static readonly int ZenId = Shader.PropertyToID("_NkZen");
        private static readonly int HorId = Shader.PropertyToID("_NkHor");
        private static readonly int SunSId = Shader.PropertyToID("_NkSunS");
        private static readonly int TimeId = Shader.PropertyToID("_NkTime");
        private static readonly int EmiId = Shader.PropertyToID("_NkEmi");
        private static readonly int GainId = Shader.PropertyToID("_NkGain");
        private static readonly int LvpId = Shader.PropertyToID("_NkLVP");
        private static readonly int ShadowMapId = Shader.PropertyToID("_NkShadowMap");
        private static readonly int SmTId = Shader.PropertyToID("_NkSmT");
        private static readonly int SkySunId = Shader.PropertyToID("_NkSkySun");
        private static readonly int CloudCId = Shader.PropertyToID("_NkCloudC");
        private static readonly int CloudSId = Shader.PropertyToID("_NkCloudS");
        private static readonly int GlowCId = Shader.PropertyToID("_NkGlowC");
        private static readonly int SkyKId = Shader.PropertyToID("_NkSkyK");
        private static readonly int CamRId = Shader.PropertyToID("_NkCamR");
        private static readonly int CamUId = Shader.PropertyToID("_NkCamU");
        private static readonly int CamFId = Shader.PropertyToID("_NkCamF");
        private static readonly int SunVId = Shader.PropertyToID("_NkSunV");

        [Header("그리는 것")]
        [Tooltip("장면을 찍는 카메라 (후처리 NukePostFx 가 붙어 있다).")]
        [SerializeField] private Camera sceneCamera;
        [Tooltip("장면 카메라의 후처리 (빛 번짐 · 초점 · 차폐 · 색).")]
        [SerializeField] private NukePostFx post;
        [Tooltip("도시 · 미사일 · 불덩이 · 잔해.")]
        [SerializeField] private NukeCityFx city;
        [Tooltip("강아지 떼.")]
        [SerializeField] private NukeDogsFx dogs;
        [Tooltip("연기 · 먼지 · 불티 · 낙진 입자.")]
        [SerializeField] private NukeParticlesFx particles;
        [Tooltip("멈칫한 강아지 머리 위 느낌표 (컷인 판 위, 장면과 같이 돈다).")]
        [SerializeField] private UIExclaimMarks exclaims;
        [Tooltip("카메라를 감싸는 하늘 공.")]
        [SerializeField] private Mesh skyMesh;
        [Tooltip("하늘 · 구름 머티리얼 (Chapchu/Nuke/Sky).")]
        [SerializeField] private Material skyMaterial;
        [Tooltip("해 쪽에서 본 깊이를 그리는 머티리얼 (Hidden/Chapchu/Nuke/ShadowDepth).")]
        [SerializeField] private Material shadowMaterial;

        [Header("설정")]
        [Tooltip("장면을 놓는 유니티 월드 자리 — 게임 화면 카메라가 보는 곳에서 멀리.")]
        [SerializeField] private Vector3 origin = new Vector3(0f, -6000f, 0f);
        [Tooltip("장면을 그리는 픽셀 수 상한 (시안 1.7M).")]
        [SerializeField] private float maxPixels = 1.7e6f;
        [Tooltip("계단 없애기 표본 수 (시안 4).")]
        [SerializeField] private int msaa = 4;
        [Tooltip("그림자 맵 한 변 (시안 2048).")]
        [SerializeField] private int shadowMapSize = 2048;
        [Tooltip("하늘 공 반지름 (m) — 카메라 먼 면(1100)보다 작게.")]
        [SerializeField] private float skyRadius = 900f;
        [Tooltip("강아지 · 잔해 배치 씨앗 — 0 이면 재생할 때마다 바뀐다 (시안과 같다).")]
        [SerializeField] private int seed;

        [Header("시간표 · 카메라")]
        [Tooltip("장면 시각 (초, 장면 시간축) — 12 장면이 바뀌는 때와 사이렌 · 착탄 · 낙진. 도시 · 강아지 · 입자도 따른다.")]
        [SerializeField] private NukeTimeline timeline = NukeTimeline.Default;
        [Tooltip("12 장면의 카메라 키 — 순서대로 시간표의 장면 1~12.")]
        [SerializeField] private NukeShot[] shots = NukeShot.Defaults();

        [Header("빛 · 하늘 · 색")]
        [Tooltip("착탄 전 — 맑은 저녁.")]
        [SerializeField] private NukeWeather clear = NukeWeather.Clear;
        [Tooltip("착탄 뒤 — 불바다.")]
        [SerializeField] private NukeWeather blast = NukeWeather.Blast;
        [Tooltip("낙진 장면 — 녹색 낙진.")]
        [SerializeField] private NukeWeather fallout = NukeWeather.Fallout;
        [Tooltip("착탄 뒤 맑음 → 착탄 빛으로 바뀌는 시간 (초).")]
        [SerializeField] private float blastBlendSeconds = 0.9f;
        [Tooltip("착탄 → 낙진 빛으로 바뀌는 시간 (초).")]
        [SerializeField] private float falloutBlendSeconds = 1.6f;
        [Tooltip("낙진 빛이 낙진 장면보다 먼저 바뀌기 시작하는 시간 (초).")]
        [SerializeField] private float falloutLeadSeconds = 0.4f;
        [Tooltip("그늘 색 (시안 SHADE).")]
        [SerializeField] private Color shade = new Color(0.66f, 0.74f, 0.95f);
        [Tooltip("하늘 · 안개에 비친 해 색.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color sunGlow = new Color(1.5f, 1.05f, 0.6f);
        [Tooltip("착탄 뒤 해 빛이 줄어드는 정도 (0~1).")]
        [SerializeField] private float sunGlowDim = 0.9f;
        [Tooltip("불덩이가 주위를 비추는 빛 색.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color fireballLight = new Color(3.4f, 1.5f, 0.5f);
        [Tooltip("착탄 뒤 하늘이 물드는 색.")]
        [ColorUsage(false, true)]
        [SerializeField] private Color fireballSky = new Color(1.5f, 0.6f, 0.18f);

        [Header("후처리")]
        [Tooltip("노출 · 빛 번짐 · 채도 · 대비 · 입자감 · 가장자리 어둡게 · 충격파 물결의 세기.")]
        [SerializeField] private NukeGrade grade = NukeGrade.Default;

        private RawImage _target;
        private RectTransform _frame;
        private float _canvasScale = 1f;
        private float _pixelScale = 1f;
        private RenderTexture _view;
        private RenderTexture _shadowMap;
        private CommandBuffer _shadowBuffer;
        private readonly List<float> _beats = new List<float>();
        private readonly List<UIExclaimMarks.Mark> _marks = new List<UIExclaimMarks.Mark>();
        private readonly List<Vector4> _heads = new List<Vector4>();
        private readonly float[] _cuts = new float[NukeShot.Count + 1];

        // 시안 camera() 가 정하는 값
        private Vector3 _eye;
        private float _yaw;
        private float _pitch;
        private float _focal;
        private float _roll;
        private float _zoom;
        private float _lightExtent;
        private float _radialBlur;
        private float _chroma;
        private float _dof;
        private float _focus;
        private bool _hasLightFocus;
        private Vector2 _lightFocus;
        private float _fog;
        private int _shot;

        /// <summary>장면 시간표 — 큰 글자 · 컷인 길이가 따른다.</summary>
        public NukeTimeline Timeline => timeline;

        private void Awake()
        {
            if (sceneCamera == null || post == null || city == null || dogs == null || particles == null || exclaims == null || skyMesh == null || skyMaterial == null || shadowMaterial == null)
                Debug.LogError($"[{nameof(NukeSceneFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            if (shots == null || shots.Length != NukeShot.Count)
                Debug.LogError($"[{nameof(NukeSceneFx)}] 장면 카메라 키(shots)는 {NukeShot.Count} 개여야 합니다.", this);
        }

        private void OnDestroy()
        {
            Release();
        }

        /// <summary>장면을 켠다 — UI 에서 떼어 먼 곳에 두고, 강아지 · 잔해를 씨앗으로 놓는다. view 에 장면이 그려지고, frame 이 화면 크기 · 기울기를 맡는다.</summary>
        public void Begin(RawImage view, RectTransform frame, float pixelScale)
        {
            _target = view;
            _frame = frame;
            _pixelScale = pixelScale;
            Canvas canvas = frame.GetComponentInParent<Canvas>();
            _canvasScale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;

            transform.SetParent(null, false);
            transform.SetPositionAndRotation(origin, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);

            int state = seed != 0 ? seed : Random.Range(1, 0xffff);
            dogs.Setup(ref state, timeline);
            city.Setup(ref state, timeline);
            particles.Setup(timeline, city.MissileTailLength);
            BuildCuts();
            BuildBeats();

            sceneCamera.enabled = true;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = Color.clear;
            sceneCamera.nearClipPlane = NukeMath.Near;
            sceneCamera.farClipPlane = NukeMath.Far;
            sceneCamera.depthTextureMode |= DepthTextureMode.Depth;
            sceneCamera.allowHDR = true;
            sceneCamera.allowMSAA = msaa > 1;

            RenderTextureFormat depthFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) ? RenderTextureFormat.RFloat : RenderTextureFormat.RHalf;
            _shadowMap = new RenderTexture(shadowMapSize, shadowMapSize, 24, depthFormat)
            {
                name = ShadowName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _shadowMap.Create();
            _shadowBuffer = new CommandBuffer { name = ShadowName };
        }

        /// <summary>실제로 흐른 시간(초)의 장면을 그리게 한다. 매 프레임 부른다.</summary>
        public void Draw(float real)
        {
            float t = timeline.SceneTime(real);
            Vector2 size = _frame.rect.size;
            if (size.x < 1f || size.y < 1f)
                return;

            EnsureTarget(size);
            Aim(t, Mathf.Min(size.y, size.x / 1.25f) * 1.05f);

            float cy = Mathf.Cos(_yaw);
            float sy = Mathf.Sin(_yaw);
            float cp = Mathf.Cos(_pitch);
            float sp = Mathf.Sin(_pitch);
            var view = new NukeView
            {
                Camera = sceneCamera,
                Origin = origin,
                Eye = _eye,
                Yaw = _yaw,
                Right = new Vector3(cy, 0f, -sy),
                Up = new Vector3(-sp * sy, cp, -sp * cy),
                Forward = new Vector3(cp * sy, sp, cp * cy),
                Focal = _focal,
                Size = size,
                Shot = _shot,
                LightDir = -NukeMath.SunDir,
            };

            sceneCamera.transform.SetPositionAndRotation(origin + _eye, Quaternion.Euler(-_pitch * Deg, _yaw * Deg, 0f));
            sceneCamera.fieldOfView = 2f * Mathf.Atan(size.y * 0.5f / _focal) * Deg;

            NukePostFx.Settings settings = Light(t, real, view);
            Graphics.DrawMesh(skyMesh, Matrix4x4.TRS(sceneCamera.transform.position, Quaternion.identity, Vector3.one * skyRadius), skyMaterial, gameObject.layer, sceneCamera, 0, null, false, false, false);

            // 해 쪽 그림자 맵 — 도시 · 강아지가 같은 명령 버퍼에 그린다
            Matrix4x4 lightView = LightView(view, out Matrix4x4 lightProjection);
            Shader.SetGlobalMatrix(LvpId, lightProjection * lightView);
            _shadowBuffer.Clear();
            _shadowBuffer.SetRenderTarget(_shadowMap);
            _shadowBuffer.ClearRenderTarget(true, true, Color.white);
            _shadowBuffer.SetViewProjectionMatrices(lightView * Matrix4x4.Translate(-origin), lightProjection);

            city.Draw(t, real, view, _shadowBuffer, shadowMaterial);
            particles.Clear();
            dogs.Draw(t, view, _shadowBuffer, shadowMaterial, particles);
            particles.Emit(t, real, view);
            particles.Draw(view);
            Graphics.ExecuteCommandBuffer(_shadowBuffer);

            post.Apply(settings);
            PlaceFrame(t, size);
            PlaceExclaims(t, view);
        }

        /// <summary>장면을 끈다 — 그림을 놓고 이 오브젝트를 지운다.</summary>
        public void End()
        {
            Release();
            Destroy(gameObject);
        }

        // 시안 camera(t) — 지금 장면의 카메라 키를 장면 진행만큼 섞는다
        private void Aim(float t, float baseFocal)
        {
            int index = ShotAt(t);
            ref readonly NukeShot shot = ref shots[index];
            float u = Mathf.Clamp01((t - _cuts[index]) / (_cuts[index + 1] - _cuts[index]));
            float k = Ease(shot.Ease, u);
            Vector3 eye = Vector3.LerpUnclamped(shot.Eye, shot.EyeEnd, Ease(shot.EyeEase, u));
            Vector3 target = Vector3.LerpUnclamped(shot.Target, shot.TargetEnd, k);
            switch (shot.Path)
            {
                case NukeShotPath.TurnToMissile:
                    target = Vector3.LerpUnclamped(shot.Target, timeline.MissileBase(t) + shot.TargetEnd, k);
                    break;
                case NukeShotPath.WatchMissile:
                    target += timeline.MissileBase(t);
                    break;
                case NukeShotPath.FollowMissile:
                    Vector3 missile = timeline.MissileBase(t);
                    timeline.MissileFrame(t, out Vector3 forward, out Vector3 side, out Vector3 up);
                    float angle = Mathf.LerpUnclamped(shot.OrbitAngle, shot.OrbitAngleEnd, k) * Mathf.Deg2Rad;
                    eye = missile + side * (eye.x + Mathf.Cos(angle) * shot.Orbit.x) + up * (eye.y + Mathf.Sin(angle) * shot.Orbit.y) + forward * eye.z;
                    target = missile + side * target.x + up * target.y + forward * target.z;
                    break;
            }

            eye.x += Mathf.Sin(t * shot.SwayRate) * shot.Sway;
            eye.y += Mathf.Abs(Mathf.Sin(t * shot.BobRate)) * shot.Bob;
            Look(eye, target);

            _shot = index + 1;
            _focal = baseFocal * Mathf.LerpUnclamped(shot.Zoom, shot.ZoomEnd, k);
            _roll = Mathf.LerpUnclamped(shot.Roll, shot.RollEnd, k) + Mathf.Sin(t * shot.WobbleRate) * shot.Wobble * u;
            _zoom = 1f;
            _radialBlur = Mathf.LerpUnclamped(shot.RadialBlur, shot.RadialBlurEnd, k);
            _chroma = shot.Chroma != null ? shot.Chroma.Evaluate(u) : 0f;
            _focus = shot.Focus;
            _dof = shot.Blur;
            _fog = shot.Fog;
            _lightExtent = shot.LightExtent;
            _hasLightFocus = shot.PinLight;
            _lightFocus = Vector2.Lerp(shot.LightCenter, shot.LightCenterEnd, k);

            float shake = shot.Shake != null ? shot.Shake.Evaluate(u) : 0f;
            if (shake <= 0f)
                return;
            _yaw += (Mathf.Sin(t * 57f) + Mathf.Sin(t * 23.7f) * 0.6f) * shake;
            _pitch += (Mathf.Cos(t * 49f) + Mathf.Sin(t * 31.3f) * 0.6f) * shake;
        }

        // 장면이 바뀌는 시각 — 장면 i 는 _cuts[i] ~ _cuts[i + 1]
        private void BuildCuts()
        {
            _cuts[0] = 0f;
            _cuts[1] = timeline.Siren;
            _cuts[2] = timeline.Sky;
            _cuts[3] = timeline.Hero;
            _cuts[4] = timeline.Run;
            _cuts[5] = timeline.Side;
            _cuts[6] = timeline.Up;
            _cuts[7] = timeline.Dive;
            _cuts[8] = timeline.SlowStart;
            _cuts[9] = timeline.Hit;
            _cuts[10] = timeline.Street;
            _cuts[11] = timeline.Fall;
            _cuts[12] = timeline.End;
        }

        private int ShotAt(float t)
        {
            for (int i = NukeShot.Count - 1; i > 0; i--)
            {
                if (t >= _cuts[i])
                    return i;
            }

            return 0;
        }

        private void Look(Vector3 from, Vector3 to)
        {
            _eye = from;
            Vector3 d = to - from;
            _yaw = Mathf.Atan2(d.x, d.z);
            _pitch = Mathf.Atan2(d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z));
        }

        // 시안 env(t, tr) — 빛 · 하늘 · 색. 셰이더 전역 값을 넣고 후처리 값을 돌려준다
        private NukePostFx.Settings Light(float t, float real, in NukeView view)
        {
            float p = t - timeline.Hit;
            float dk = NukeMath.Smooth(p / blastBlendSeconds);
            float gk = NukeMath.Smooth((t - timeline.Fall + falloutLeadSeconds) / falloutBlendSeconds);
            float hot = p > 0f ? Mathf.Exp(-p * 0.5f) : 0f;
            float near = NukeMath.Smooth((t - timeline.Run) / (timeline.Hit - timeline.Run));
            float pulse = p < 0f ? Pulse(t) : 0f;
            float hold = t >= timeline.SlowStart && p < 0f ? 1f : 0f;

            Vector3 sun = Mix3(clear.Sun, blast.Sun, fallout.Sun, dk, gk);
            Vector3 sunSky = Rgb(sunGlow) * (1f - dk * sunGlowDim);
            Vector3 amb = Mix3(clear.SkyLight, blast.SkyLight, fallout.SkyLight, dk, gk);
            Vector3 gnd = Mix3(clear.Ground, blast.Ground, fallout.Ground, dk, gk);
            Vector3 fog = Mix3(clear.Fog, blast.Fog, fallout.Fog, dk, gk);
            float fogDensity = _fog > 0f ? _fog : Mix3(clear.FogDensity, blast.FogDensity, fallout.FogDensity, dk, gk);
            var fogK = new Vector2(fogDensity, Mix3(clear.FogMax, blast.FogMax, fallout.FogMax, dk, gk));
            Vector3 zen = Mix3(clear.Zenith, blast.Zenith, fallout.Zenith, dk, gk);
            Vector3 hor = Mix3(clear.Horizon, blast.Horizon, fallout.Horizon, dk, gk);
            Vector3 cloudC = Mix3(clear.CloudLit, blast.CloudLit, fallout.CloudLit, dk, gk);
            Vector3 cloudS = Mix3(clear.CloudShade, blast.CloudShade, fallout.CloudShade, dk, gk);
            float cover = Mix3(clear.CloudCover, blast.CloudCover, fallout.CloudCover, dk, gk);
            float hole = p > 0f ? p * 120f : 0f;
            float rimK = p > 0f ? 1f - NukeMath.Smooth((p - 0.4f) / 2.2f) : 0f;
            float glow = hot * 0.4f;
            Vector4 pointLight = p > 0f
                ? new Vector4(NukeMath.Impact.x, Mathf.LerpUnclamped(8f, 50f, NukeMath.Smooth(p / 3f)), NukeMath.Impact.z, Mathf.LerpUnclamped(55f, 90f, NukeMath.Smooth(p / 2f)))
                : Vector4.zero;
            Vector3 pointColor = p > 0f ? Rgb(fireballLight) * (hot * (0.8f + 0.2f * Mathf.Sin(real * 31f)) + 0.05f * gk) : Vector3.zero;
            float emi = p > 0f ? (p < 0.25f && Mathf.Sin(real * 90f) > 0f ? 0.8f : 0f) : 0.95f;
            // 착탄 색 보정은 열기가 식을수록 줄어든다 (식은 뒤 40%)
            Vector3 gain = Mix3(clear.Gain, blast.Gain, fallout.Gain, dk * (0.4f + 0.6f * hot), gk);

            Shader.SetGlobalVector(OriginId, origin);
            Shader.SetGlobalVector(FogCId, fog);
            Shader.SetGlobalVector(LdId, view.LightDir);
            Shader.SetGlobalVector(SunCId, sun);
            Shader.SetGlobalVector(ShCId, Rgb(shade));
            Shader.SetGlobalVector(AmbId, amb);
            Shader.SetGlobalVector(GndId, gnd);
            Shader.SetGlobalVector(PlId, pointLight);
            Shader.SetGlobalVector(PlCId, pointColor);
            Shader.SetGlobalVector(FxId, fogK);
            Shader.SetGlobalVector(ZenId, zen);
            Shader.SetGlobalVector(HorId, hor);
            Shader.SetGlobalVector(SunSId, sunSky);
            Shader.SetGlobalFloat(TimeId, real);
            Shader.SetGlobalFloat(EmiId, emi);
            Shader.SetGlobalVector(GainId, gain);
            Shader.SetGlobalTexture(ShadowMapId, _shadowMap);
            Shader.SetGlobalFloat(SmTId, 1f / shadowMapSize);
            Shader.SetGlobalVector(SkySunId, sunSky);
            Shader.SetGlobalVector(CloudCId, cloudC);
            Shader.SetGlobalVector(CloudSId, cloudS);
            Shader.SetGlobalVector(GlowCId, Rgb(fireballSky));
            Shader.SetGlobalVector(SkyKId, new Vector4(cover, hole, rimK, glow));
            Shader.SetGlobalVector(CamRId, view.Right);
            Shader.SetGlobalVector(CamUId, view.Up);
            Shader.SetGlobalVector(CamFId, view.Forward);
            Vector3 sunDir = NukeMath.SunDir;
            Shader.SetGlobalVector(SunVId, new Vector3(Vector3.Dot(sunDir, view.Right), Vector3.Dot(sunDir, view.Up), -Vector3.Dot(sunDir, view.Forward) * 0.5f + 0.5f).normalized);

            var settings = new NukePostFx.Settings
            {
                Expo = 1f + (p > 0f ? grade.Flash * Mathf.Exp(-p * 5.5f) + grade.Afterglow * Mathf.Exp(-p * 1.2f) : 0f) - hold * grade.HoldDarken,
                Bloom = grade.Bloom + hot * grade.BloomHeat,
                Saturation = Mathf.LerpUnclamped(grade.Saturation, grade.SaturationNear, near) - hold * grade.HoldDesaturate - gk * grade.FalloutDesaturate,
                Contrast = grade.Contrast + near * grade.ContrastNear,
                Tint = Mix3(clear.Tint, blast.Tint, fallout.Tint, dk, gk),
                Grain = grade.Grain + gk * grade.FalloutGrain,
                Vignette = grade.Vignette + near * grade.VignetteNear + pulse * grade.VignettePulse + hold * grade.VignetteHold,
                VignetteColor = hold > 0f ? Vector3.zero : Vector3.LerpUnclamped(Rgb(grade.VignetteColor), Rgb(grade.VignetteNearColor), near * 0.7f + pulse * 0.3f),
                RadialBlur = _radialBlur,
                RadialCenter = new Vector2(0.5f, 0.5f),
                Chroma = _chroma,
                Fade = 0f,
                FadeColor = Rgb(grade.DustColor),
                Ripple = new Vector4(0.5f, 0.5f, 0f, 0f),
                AoK = grade.Occlusion,
                Focus = _focus,
                DofK = _dof,
                RayK = 0f,
                SunUV = new Vector2(0.5f, 0.5f),
                TanHalf = new Vector2(view.Size.x * 0.5f / view.Focal, view.Size.y * 0.5f / view.Focal),
                Time = real,
            };

            // 해가 화면 안이면 빛줄기
            if (view.Project(view.Eye + sunDir * 900f, out Vector2 sunAt, out _))
            {
                float u = sunAt.x / view.Size.x + 0.5f;
                float v = sunAt.y / view.Size.y + 0.5f;
                settings.SunUV = new Vector2(u, v);
                settings.RayK = grade.SunRays * (1f - dk) * (1f - NukeMath.Smooth((Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) - 0.55f) / 0.5f));
            }

            // 착탄 뒤 화면을 밀어내는 충격파 물결
            if (p > 0f && p < grade.RippleSeconds && view.Project(new Vector3(NukeMath.Impact.x, 8f, NukeMath.Impact.z), out Vector2 hitAt, out float hitScale))
                settings.Ripple = new Vector4(hitAt.x / view.Size.x + 0.5f, hitAt.y / view.Size.y + 0.5f, Mathf.Min(3f, 95f * p * hitScale / view.Size.y), (1f - p / grade.RippleSeconds) * grade.Ripple);

            if (view.Shot == 11)
            {
                float toWall = NukeMath.Flat(view.Eye.x - NukeMath.Impact.x, view.Eye.z - NukeMath.Impact.z) - NukeMath.DustSpeed * p;
                settings.Fade = NukeMath.Smooth((16f - toWall) / 16f) * 0.97f;
            }

            if (view.Shot == 12)
                settings.Fade = (1f - NukeMath.Smooth((t - timeline.Fall) / 0.9f)) * 0.97f;

            if (t < 0.3f)
            {
                settings.Fade = Mathf.Max(settings.Fade, 1f - t / 0.3f);
                settings.FadeColor = Vector3.zero;
            }

            return settings;
        }

        // 시안 D3R lightVP — 해 쪽 직교 투영. 카메라가 보는 곳 앞을 감싸고, 그림자 맵 한 칸 단위로 붙여 떨리지 않게 한다
        private Matrix4x4 LightView(in NukeView view, out Matrix4x4 projection)
        {
            float extent = _lightExtent;
            float depth = extent > 100f ? 560f : 340f;
            Vector3 f = view.LightDir;
            Vector3 fw = view.Forward;
            float reach = fw.y < -0.05f ? view.Eye.y / -fw.y : 16f;
            reach = Mathf.Clamp(reach, 6f, 24f);
            Vector3 c = _hasLightFocus ? new Vector3(_lightFocus.x, 0f, _lightFocus.y) : new Vector3(view.Eye.x + fw.x * reach, 0f, view.Eye.z + fw.z * reach);
            Vector3 s = new Vector3(-f.z, 0f, f.x).normalized;
            Vector3 u = Vector3.Cross(s, f);
            float texel = 2f * extent / shadowMapSize;
            float ds = Mathf.Round(Vector3.Dot(c, s) / texel) * texel;
            float du = Mathf.Round(Vector3.Dot(c, u) / texel) * texel;
            float df = Vector3.Dot(c, f);
            Vector3 center = s * ds + u * du + f * df;
            Vector3 eye = center - f * depth * 0.5f;

            var lightView = Matrix4x4.identity;
            lightView.SetRow(0, new Vector4(s.x, s.y, s.z, -Vector3.Dot(s, eye)));
            lightView.SetRow(1, new Vector4(u.x, u.y, u.z, -Vector3.Dot(u, eye)));
            lightView.SetRow(2, new Vector4(-f.x, -f.y, -f.z, Vector3.Dot(f, eye)));
            lightView.SetRow(3, new Vector4(0f, 0f, 0f, 1f));

            const float near = 1f;
            projection = Matrix4x4.identity;
            projection.m00 = 1f / extent;
            projection.m11 = 1f / extent;
            projection.m22 = -2f / (depth - near);
            projection.m23 = -(depth + near) / (depth - near);
            return lightView;
        }

        // 시안 BEATS — 달리기부터 착탄 직전까지 점점 빨라지는 심장 박동
        private void BuildBeats()
        {
            _beats.Clear();
            float t = timeline.Run + 0.1f;
            float interval = 0.66f;
            while (t < timeline.SlowStart - 0.05f)
            {
                _beats.Add(t);
                t += interval;
                interval = Mathf.Max(0.3f, interval * 0.93f);
            }
        }

        private float Pulse(float t)
        {
            float v = 0f;
            foreach (float b in _beats)
            {
                if (b > t)
                    break;
                float second = t > b + 0.13f ? 0.6f * Mathf.Exp(-(t - b - 0.13f) * 9f) : 0f;
                v = Mathf.Max(v, Mathf.Max(Mathf.Exp(-(t - b) * 9f), second));
            }

            return v;
        }

        // 장면 판을 기울이고 키운다 (시안 canvas 의 rotate · scale) — 기울여도 화면 모서리가 비지 않게
        private void PlaceFrame(float t, Vector2 size)
        {
            float angle = _roll * Mathf.Deg2Rad;
            float pulse = t < timeline.Hit ? Pulse(t) * 0.008f : 0f;
            float scale = (Mathf.Cos(angle) + Mathf.Max(size.x / size.y, size.y / size.x) * Mathf.Abs(Mathf.Sin(angle))) * (_zoom + pulse);
            _frame.localRotation = Quaternion.Euler(0f, 0f, -_roll);
            _frame.localScale = new Vector3(scale, scale, 1f);
        }

        // 시안 overlay — 멈칫한 강아지 머리 위 느낌표 (하늘을 보기 전까지)
        private void PlaceExclaims(float t, in NukeView view)
        {
            _marks.Clear();
            if (t < timeline.Hero && view.Shot <= 3)
            {
                dogs.CollectHeads(t, _heads);
                foreach (Vector4 head in _heads)
                {
                    float k = head.w;
                    if (!view.Project(head, out Vector2 at, out float scale))
                        continue;
                    float size = scale * 0.34f * NukeMath.EaseOut(k / 0.18f) * (1f + 0.2f * Mathf.Sin(Mathf.Clamp01(k / 0.3f) * Mathf.PI));
                    _marks.Add(new UIExclaimMarks.Mark
                    {
                        Position = at,
                        Size = size,
                        Line = Mathf.Max(1.5f * _pixelScale, size * 0.14f),
                        Alpha = 1f - NukeMath.Smooth((k - 1.1f) / 0.4f),
                    });
                }
            }

            exclaims.SetMarks(_marks);
        }

        private void EnsureTarget(Vector2 size)
        {
            float pixelsX = size.x * _canvasScale;
            float pixelsY = size.y * _canvasScale;
            float k = Mathf.Min(1f, Mathf.Sqrt(maxPixels / Mathf.Max(1f, pixelsX * pixelsY)));
            int width = Mathf.Max(64, Mathf.RoundToInt(pixelsX * k));
            int height = Mathf.Max(64, Mathf.RoundToInt(pixelsY * k));
            if (_view != null && _view.width == width && _view.height == height)
                return;

            if (_view != null)
            {
                sceneCamera.targetTexture = null;
                _view.Release();
                Destroy(_view);
            }

            RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            _view = new RenderTexture(width, height, 24, format)
            {
                name = ViewName,
                antiAliasing = Mathf.Max(1, msaa),
            };
            _view.Create();
            sceneCamera.targetTexture = _view;
            _target.texture = _view;
        }

        private void Release()
        {
            if (sceneCamera != null)
                sceneCamera.targetTexture = null;
            if (_target != null)
                _target.texture = null;
            if (_view != null)
            {
                _view.Release();
                Destroy(_view);
                _view = null;
            }

            if (_shadowMap != null)
            {
                _shadowMap.Release();
                Destroy(_shadowMap);
                _shadowMap = null;
            }

            if (_shadowBuffer != null)
            {
                _shadowBuffer.Release();
                _shadowBuffer = null;
            }
        }

        private static float Ease(NukeEase ease, float u)
        {
            switch (ease)
            {
                case NukeEase.InOut:
                    return NukeMath.EaseInOut(u);
                case NukeEase.Out:
                    return NukeMath.EaseOut(u);
                default:
                    return u;
            }
        }

        private static Vector3 Rgb(Color color) => new Vector3(color.r, color.g, color.b);

        // 시안 mix3(mix3(맑음, 착탄, dk), 낙진, gk)
        private static Vector3 Mix3(Color clearColor, Color blastColor, Color falloutColor, float dk, float gk)
            => Vector3.LerpUnclamped(Vector3.LerpUnclamped(Rgb(clearColor), Rgb(blastColor), dk), Rgb(falloutColor), gk);

        private static float Mix3(float clearValue, float blastValue, float falloutValue, float dk, float gk)
            => Mathf.LerpUnclamped(Mathf.LerpUnclamped(clearValue, blastValue, dk), falloutValue, gk);
    }
}
