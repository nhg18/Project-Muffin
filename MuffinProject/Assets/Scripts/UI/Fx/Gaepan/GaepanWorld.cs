using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 개판 오분전의 3D 눈 마을 — 「강아지 마을 얼음 연못」 (시안 D3R). 마을(땅 · 연못 · 개집 · 울타리 · 가로등 · 눈사람 · 설산 · 숲)은 프리팹에 놓여 있고,
    /// 장면마다 바뀌는 강아지 · 눈뭉치 · 나무는 풀에서 꺼내 그린다 (시안처럼 매 프레임 「이번 프레임에 그릴 것」을 다시 부른다: BeginFrame → Dog · Puff · Tree → EndFrame).
    /// 카메라는 렌더 텍스처에 그리고, 화면에는 연출 판의 RawImage 로 보인다. 눈송이 · 번쩍임 · 제목 · 도장은 덧그림 카메라가 같은 텍스처 위에 그린다 —
    /// 시안처럼 연출 전체가 한 장으로 흐려지게 (겹친 글자 겹 · 테두리가 흐려질 때 비치지 않게).
    /// 게임 화면의 3D 카메라에 걸리지 않게 시작할 때 캔버스 밖 먼 곳으로 떼어 놓는다 — 이 오브젝트는 연출이 지워질 때 같이 지운다.
    /// 좌표는 시안과 같다 (x 오른쪽 · y 위 · z 앞, 1 = 개집 폭 0.6배쯤). 빛 · 안개 · 화면 값은 셰이더 전역(GaepanCommon)으로 넣는다.
    /// </summary>
    public class GaepanWorld : MonoBehaviour
    {
        // 시안 D3R: 연못 · 울타리 줄 · 마을 평지 (groundH · blocked)
        private const float PondX = 14f, PondZ = 6f, PondRadiusX = 31f, PondRadiusZ = 10.5f, PondDip = 0.04f, PondMargin = -0.25f;
        private const float FlatMinX = -30f, FlatMaxX = 50f, FlatMinZ = -20f, FlatMaxZ = 48f, HillRamp = 50f;
        private const float FenceMinZ = 28.5f, FenceMaxZ = 31.5f, FenceMinX = -17f, FenceMaxX = 49f;
        private const float OccupiedMargin = 1.8f;
        private const float ReferenceHeight = 1080f;
        private static readonly int LightDirId = Shader.PropertyToID("_GpLightDir");
        private static readonly int SunId = Shader.PropertyToID("_GpSun");
        private static readonly int ShadeId = Shader.PropertyToID("_GpShade");
        private static readonly int FogId = Shader.PropertyToID("_GpFog");
        private static readonly int FogRangeId = Shader.PropertyToID("_GpFogRange");
        private static readonly int ViewId = Shader.PropertyToID("_GpView");

        [Tooltip("눈 마을을 찍는 카메라 — 렌더 텍스처에 그린다.")]
        [SerializeField] private Camera worldCamera;
        [Tooltip("눈송이 · 번쩍임 · 제목 · 도장 캔버스(Screen Space - Camera)를 같은 렌더 텍스처에 덧그리는 카메라 — 눈 마을 카메라 다음에 그린다.")]
        [SerializeField] private Camera overlayCamera;
        [Tooltip("렌더 텍스처를 보여 줄 연출 판의 그림.")]
        [SerializeField] private RawImage view;
        [SerializeField] private GaepanSky sky;
        [Tooltip("풀에서 꺼낸 강아지 · 눈뭉치 · 나무를 두는 곳.")]
        [SerializeField] private Transform actors;
        [Tooltip("떼어 놓을 자리 — 게임 화면 카메라(먼 쪽 1000)가 닿지 않는 곳.")]
        [SerializeField] private Vector3 farAway = new Vector3(0f, -5000f, 0f);

        [Header("풀")]
        [SerializeField] private GaepanDog dogPrefab;
        [SerializeField] private MeshRenderer puffPrefab;
        [Tooltip("장면마다 다시 심는 전나무 (메시는 treeMeshes 중 하나로 바꾼다).")]
        [SerializeField] private MeshFilter treePrefab;
        [Tooltip("전나무 넷 (시안 tree0 ~ tree3).")]
        [SerializeField] private Mesh[] treeMeshes;
        [SerializeField] private int dogCount = 13;
        [SerializeField] private int puffCount = 70;
        [SerializeField] private int treeCount = 44;

        [Header("마을")]
        [Tooltip("나무를 심지 않는 자리 (x, z, 반지름) — 개집 · 가로등 · 눈사람 (시안 OCC).")]
        [SerializeField] private Vector3[] occupied;

        [Header("빛 (시안 D3R)")]
        [Tooltip("빛이 나아가는 방향 (해는 왼쪽 뒤에서 낮게).")]
        [SerializeField] private Vector3 lightDirection = new Vector3(0.55f, -0.47f, 0.69f);
        [SerializeField] private Color sun = new Color(1f, 0.95f, 0.86f);
        [SerializeField] private Color shade = new Color(0.66f, 0.74f, 0.95f);

        [Header("카메라 (시안 DOGPILE.play · D3R.viewProj)")]
        [Tooltip("시안이 화면을 맞추는 가로세로비 범위 — 이 칸의 높이 × focalShare 가 초점 거리.")]
        [SerializeField] private Vector2 aspectRange = new Vector2(0.75f, 1.9f);
        [SerializeField] private float focalShare = 1.05f;
        [SerializeField] private float nearClip = 0.12f;
        [SerializeField] private float farClip = 280f;
        [Tooltip("렌더 텍스처 계단 없애기 (MSAA 배수).")]
        [SerializeField] private int antiAliasing = 4;

        private readonly List<GaepanDog> _dogs = new List<GaepanDog>();
        private readonly List<MeshRenderer> _puffs = new List<MeshRenderer>();
        private readonly List<MeshFilter> _trees = new List<MeshFilter>();
        private int _dogsUsed, _puffsUsed, _treesUsed;
        private int _dogsShown, _puffsShown, _treesShown;
        private RenderTexture _texture;
        private Rect _box;
        private float _focal;
        private float _pixelScale = 1f;
        private float _pitch;
        private Color _fog;
        private Vector2 _fogRange;
        private GaepanSky.GroundLayer _ground;

        /// <summary>카메라 방위 (시안 C.yaw, 라디안).</summary>
        public float CameraYaw { get; private set; }

        public Vector3 CameraRight => worldCamera.transform.right;

        public Vector3 CameraUp => worldCamera.transform.up;

        /// <summary>빛이 나아가는 방향 (정규화).</summary>
        public Vector3 LightDirection { get; private set; }

        private void Awake()
        {
            if (worldCamera == null || overlayCamera == null || view == null || sky == null || actors == null || dogPrefab == null || puffPrefab == null
                || treePrefab == null || treeMeshes == null || treeMeshes.Length == 0)
                Debug.LogError($"[{nameof(GaepanWorld)}] 참조 중 비어 있는 것이 있습니다.", this);
            LightDirection = lightDirection.normalized;
            // 캔버스 아래에서는 크기가 캔버스 배율을 따라가고 게임 화면 카메라에 걸릴 수 있다 — 장면 맨 위, 먼 곳으로
            transform.SetParent(null, false);
            transform.SetPositionAndRotation(farAway, Quaternion.identity);
            transform.localScale = Vector3.one;
            worldCamera.enabled = false;
            overlayCamera.enabled = false;
        }

        private void OnDestroy()
        {
            ReleaseTexture();
        }

        /// <summary>재생 준비 — 렌더 텍스처를 만들어 카메라 · 화면에 잇고, 풀을 채우고, 구름을 새로 만든다.</summary>
        public void Begin()
        {
            int width = Mathf.Max(Screen.width, 2), height = Mathf.Max(Screen.height, 2);
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                ReleaseTexture();
                _texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "GaepanWorld", antiAliasing = Mathf.Max(1, antiAliasing) };
                _texture.Create();
            }
            worldCamera.targetTexture = _texture;
            overlayCamera.targetTexture = _texture;
            view.texture = _texture;
            FitCamera(width, height);
            Fill(_dogs, dogCount, () => Instantiate(dogPrefab, actors));
            Fill(_puffs, puffCount, () => Instantiate(puffPrefab, actors));
            Fill(_trees, treeCount, () => Instantiate(treePrefab, actors));
            sky.BuildClouds();
            worldCamera.enabled = true;
            overlayCamera.enabled = true;
        }

        /// <summary>재생이 끝나면 카메라를 멈추고 렌더 텍스처를 내린다.</summary>
        public void End()
        {
            worldCamera.enabled = false;
            overlayCamera.enabled = false;
            view.texture = null;
            ReleaseTexture();
        }

        /// <summary>카메라를 놓는다 — 시안 C (x, y, z, yaw, pitch). pitch 는 + 가 위를 본다.</summary>
        public void SetCamera(Vector3 position, float yaw, float pitch)
        {
            CameraYaw = yaw;
            _pitch = pitch;
            worldCamera.transform.localPosition = position;
            worldCamera.transform.localRotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f);
        }

        /// <summary>이 장면의 안개 (시안 FOG · fogNear · fogFar).</summary>
        public void SetFog(Color color, Vector2 range)
        {
            _fog = color;
            _fogRange = range;
        }

        /// <summary>이 장면의 지평선 아래 땅 · 아지랑이 (시안 ground — 없으면 하늘만).</summary>
        public void SetGround(GaepanSky.GroundLayer ground)
        {
            _ground = ground;
        }

        /// <summary>이번 프레임에 그릴 것을 처음부터 다시 부른다.</summary>
        public void BeginFrame()
        {
            _dogsUsed = 0;
            _puffsUsed = 0;
            _treesUsed = 0;
        }

        /// <summary>부르지 않은 풀 오브젝트를 숨기고, 빛 · 안개 · 하늘 값을 넣는다.</summary>
        public void EndFrame()
        {
            _dogsShown = Hide(_dogs, _dogsUsed, _dogsShown);
            _puffsShown = Hide(_puffs, _puffsUsed, _puffsShown);
            _treesShown = Hide(_trees, _treesUsed, _treesShown);

            Shader.SetGlobalVector(LightDirId, LightDirection);
            Shader.SetGlobalColor(SunId, sun);
            Shader.SetGlobalColor(ShadeId, shade);
            Shader.SetGlobalColor(FogId, _fog);
            Shader.SetGlobalVector(FogRangeId, _fogRange);
            Shader.SetGlobalVector(ViewId, new Vector4(_focal, _pixelScale, transform.position.y, 0f));
            float horizon = _box.height / 2f + Mathf.Tan(_pitch) * _focal;
            sky.Frame(worldCamera.transform.localPosition, _box, horizon, _pixelScale, _ground);
        }

        /// <summary>강아지 하나 (시안 dog). 풀이 모자라면 그리지 않는다.</summary>
        public void Dog(in GaepanDogState state)
        {
            if (_dogsUsed >= _dogs.Count)
                return;
            GaepanDog dog = _dogs[_dogsUsed];
            _dogsUsed = Show(dog.gameObject, _dogsUsed, _dogsShown);
            dog.Draw(state, this);
        }

        /// <summary>눈뭉치 하나 (시안 puff — 3D 에서는 투명도 대신 크기로 사라진다). alpha 가 아주 작으면 그리지 않는다.</summary>
        public void Puff(Vector3 position, float radius, float alpha, float seed)
        {
            if (alpha < 0.06f || _puffsUsed >= _puffs.Count)
                return;
            MeshRenderer puff = _puffs[_puffsUsed];
            _puffsUsed = Show(puff.gameObject, _puffsUsed, _puffsShown);
            Transform t = puff.transform;
            t.localPosition = position;
            t.localRotation = Quaternion.Euler(0f, (seed * 2.3f + position.x) * Mathf.Rad2Deg, seed * Mathf.Rad2Deg);
            t.localScale = Vector3.one * (radius * Mathf.Pow(Mathf.Min(1f, alpha), 0.55f));
        }

        /// <summary>전나무 하나 (시안 tree — 연못 · 소품 자리 · 울타리 줄이면 심지 않는다). 모양 · 방향은 자리로 정해진다.</summary>
        public void Tree(float x, float z, float height)
        {
            if (Blocked(x, z) || _treesUsed >= _trees.Count)
                return;
            MeshFilter tree = _trees[_treesUsed];
            _treesUsed = Show(tree.gameObject, _treesUsed, _treesShown);
            float hash = Fraction(x * 0.37f + z * 0.11f);
            Mesh mesh = treeMeshes[Mathf.Min((int)(hash * treeMeshes.Length), treeMeshes.Length - 1)];
            if (tree.sharedMesh != mesh)
                tree.sharedMesh = mesh;
            Transform t = tree.transform;
            t.localPosition = new Vector3(x, GroundHeight(x, z) - 0.1f, z);
            t.localRotation = Quaternion.Euler(0f, hash * 6.28f * Mathf.Rad2Deg, (Fraction(x * 1.7f) - 0.5f) * 0.07f * Mathf.Rad2Deg);
            t.localScale = Vector3.one * height;
        }

        /// <summary>땅 높이 — 마을 평지는 0, 연못은 살짝 파였고, 바깥은 언덕 (시안 D3R.groundH).</summary>
        public static float GroundHeight(float x, float z)
        {
            float dx = Mathf.Max(0f, Mathf.Max(FlatMinX - x, x - FlatMaxX));
            float dz = Mathf.Max(0f, Mathf.Max(FlatMinZ - z, z - FlatMaxZ));
            float k = SmoothStep(0f, HillRamp, Mathf.Sqrt(dx * dx + dz * dz));
            float hill = k * (6f + 3.4f * Mathf.Sin(x * 0.043f + 1.3f) * Mathf.Cos(z * 0.051f - 0.4f) + 1.8f * Mathf.Sin(x * 0.11f + z * 0.07f));
            return hill - (InPond(x, z) > 0f ? PondDip : 0f);
        }

        /// <summary>연못 안쪽 정도 (0 = 가장자리, + 안쪽) — 시안 pondIn.</summary>
        public static float InPond(float x, float z)
        {
            float px = (x - PondX) / PondRadiusX, pz = (z - PondZ) / PondRadiusZ;
            return 1f - px * px - pz * pz;
        }

        /// <summary>소수 부분 (시안 fr).</summary>
        public static float Fraction(float x) => x - Mathf.Floor(x);

        // 시안 D3R.blocked: 연못 둘레 · 소품 자리 · 울타리 줄
        private bool Blocked(float x, float z)
        {
            if (InPond(x, z) > PondMargin)
                return true;
            foreach (Vector3 spot in occupied)
            {
                if (Mathf.Sqrt((x - spot.x) * (x - spot.x) + (z - spot.y) * (z - spot.y)) < spot.z + OccupiedMargin)
                    return true;
            }
            return z > FenceMinZ && z < FenceMaxZ && x > FenceMinX && x < FenceMaxX;
        }

        // 시안 DOGPILE.play: 가로세로비를 범위로 묶은 칸, 초점 거리 = 칸 높이 × 1.05. 세로 시야각은 화면 높이로
        private void FitCamera(int width, int height)
        {
            float aspect = Mathf.Clamp((float)width / height, aspectRange.x, aspectRange.y);
            float boxWidth = Mathf.Min(width, height * aspect);
            float boxHeight = boxWidth / aspect;
            _box = new Rect((width - boxWidth) / 2f, (height - boxHeight) / 2f, boxWidth, boxHeight);
            _focal = boxHeight * focalShare;
            _pixelScale = boxHeight / ReferenceHeight;
            worldCamera.fieldOfView = 2f * Mathf.Atan(height / (2f * _focal)) * Mathf.Rad2Deg;
            worldCamera.nearClipPlane = nearClip;
            worldCamera.farClipPlane = farClip;
        }

        private void ReleaseTexture()
        {
            if (_texture == null)
                return;
            if (worldCamera != null && worldCamera.targetTexture == _texture)
                worldCamera.targetTexture = null;
            if (overlayCamera != null && overlayCamera.targetTexture == _texture)
                overlayCamera.targetTexture = null;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        private static void Fill<T>(List<T> pool, int count, System.Func<T> create) where T : Component
        {
            while (pool.Count < count)
            {
                T item = create();
                item.gameObject.SetActive(false);
                pool.Add(item);
            }
        }

        // 쓴 수가 보이는 수를 넘으면 켠다 — 매 프레임 SetActive 를 다시 부르지 않게
        private static int Show(GameObject item, int used, int shown)
        {
            if (used >= shown && !item.activeSelf)
                item.SetActive(true);
            return used + 1;
        }

        private static int Hide<T>(List<T> pool, int used, int shown) where T : Component
        {
            for (int i = used; i < shown; i++)
                pool[i].gameObject.SetActive(false);
            return used;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
