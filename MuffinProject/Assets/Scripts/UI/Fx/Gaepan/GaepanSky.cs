using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 눈 마을의 하늘 — 지평선 그라데이션 · 해무리(GaepanSky 셰이더, 화면 판 둘)와 크레용 테두리 뭉게구름.
    /// 구름은 재생마다 무작위로 다시 만들고, 방향에 고정돼 카메라를 따라 흐른다 (카메라 자리를 따라다니는 판 위, 아주 먼 곳).
    /// 시안 DOGPILE.paintedSky · ground · mk().clouds 와 같다.
    /// </summary>
    public class GaepanSky : MonoBehaviour
    {
        private const int CloudBlobs = 6;
        private const int CircleSegments = 28;
        private static readonly int SkyBoxId = Shader.PropertyToID("_GpSkyBox");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_GpSkyHorizon");
        private static readonly int GroundTopId = Shader.PropertyToID("_GpGroundTop");
        private static readonly int GroundBottomId = Shader.PropertyToID("_GpGroundBottom");

        [Tooltip("카메라 자리를 따라다니는 판 (돌지 않는다). 구름이 이 아래 있다.")]
        [SerializeField] private Transform anchor;
        [SerializeField] private MeshFilter clouds;

        [Header("구름 (시안 mk().clouds)")]
        [Tooltip("구름 수.")]
        [SerializeField] private int cloudCount = 16;
        [Tooltip("이 번호 밑의 구름은 지평선 가까이(낮은 고도), 나머지는 높이.")]
        [SerializeField] private int lowClouds = 9;
        [Tooltip("낮은 구름 고도 (라디안, 최소 · 최대).")]
        [SerializeField] private Vector2 lowElevation = new Vector2(0.06f, 0.32f);
        [Tooltip("높은 구름 고도 (라디안, 최소 · 최대).")]
        [SerializeField] private Vector2 highElevation = new Vector2(0.45f, 1.05f);
        [Tooltip("구름 방위 (라디안, 정면 0).")]
        [SerializeField] private Vector2 azimuth = new Vector2(-1.5f, 1.5f);
        [Tooltip("구름 크기 (시안 r — 900 거리에서의 반지름).")]
        [SerializeField] private Vector2 cloudRadius = new Vector2(38f, 75f);
        [Tooltip("시안이 구름을 그리는 거리.")]
        [SerializeField] private float sourceDistance = 900f;
        [Tooltip("여기서 구름을 놓는 거리 — 카메라 먼 쪽 자르기보다 가깝게. 각 크기는 시안과 같다.")]
        [SerializeField] private float drawDistance = 250f;
        [Tooltip("테두리 두께 / 반지름.")]
        [SerializeField] private float strokeShare = 0.05f;
        [SerializeField] private Color stroke = new Color32(96, 112, 160, 140);
        [Tooltip("구름 속 세로 그라데이션 — 위 · 60% · 아래.")]
        [SerializeField] private Color fillTop = new Color32(0xFF, 0xFD, 0xF9, 0xFF);
        [SerializeField] private Color fillMiddle = new Color32(0xF4, 0xEE, 0xF4, 0xFF);
        [SerializeField] private Color fillBottom = new Color32(0xD9, 0xD3, 0xE6, 0xFF);
        [Tooltip("볕 받은 자리 (왼쪽 위).")]
        [SerializeField] private Color highlight = new Color32(255, 226, 196, 115);

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();
        private Mesh _mesh;

        /// <summary>지평선 아래 땅 · 아지랑이 층 (시안 ground(x, top, bottom, haze)). 꺼 두면 하늘만.</summary>
        public struct GroundLayer
        {
            public bool On;
            public Color Top;
            public Color Bottom;
            /// <summary>아지랑이(흰색) 알파.</summary>
            public float Haze;
        }

        private void Awake()
        {
            if (anchor == null || clouds == null)
                Debug.LogError($"[{nameof(GaepanSky)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }

        /// <summary>구름을 무작위로 다시 만든다 (재생마다).</summary>
        public void BuildClouds()
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "GaepanClouds" };
                _mesh.MarkDynamic();
                clouds.sharedMesh = _mesh;
            }
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            var blobs = new Vector3[CloudBlobs];
            for (int i = 0; i < cloudCount; i++)
            {
                float a = Random.Range(azimuth.x, azimuth.y);
                float e = i < lowClouds ? Random.Range(lowElevation.x, lowElevation.y) : Random.Range(highElevation.x, highElevation.y);
                float r = Random.Range(cloudRadius.x, cloudRadius.y);
                for (int j = 0; j < CloudBlobs; j++)
                    blobs[j] = new Vector3((j - 2.5f) * Random.Range(0.32f, 0.42f), -Mathf.Abs(Mathf.Sin(j * 1.3f)) * Random.Range(0.15f, 0.45f), Random.Range(0.35f, 0.62f));
                AddCloud(new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)), r * drawDistance / sourceDistance, blobs);
            }
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * drawDistance * 2.5f);
        }

        /// <summary>
        /// 한 프레임 — 구름 판을 카메라 자리로 옮기고 하늘 셰이더 값을 넣는다.
        /// box = 시안 가로세로비 칸 (렌더 텍스처 px, 왼쪽 위 기준), horizon = 칸 위에서 지평선까지 (px), pixelScale = 화면 px / 시안 px.
        /// </summary>
        public void Frame(Vector3 cameraLocalPosition, Rect box, float horizon, float pixelScale, GroundLayer ground)
        {
            anchor.localPosition = cameraLocalPosition;
            Shader.SetGlobalVector(SkyBoxId, new Vector4(box.x, box.y, box.width, box.height));
            Shader.SetGlobalVector(SkyHorizonId, new Vector4(horizon, ground.Haze, ground.On ? 1f : 0f, pixelScale));
            Shader.SetGlobalColor(GroundTopId, ground.Top);
            Shader.SetGlobalColor(GroundBottomId, ground.Bottom);
        }

        // 시안 paintedSky 의 구름 하나: 테두리 → 속(세로 그라데이션) → 볕 받은 자리. 캔버스 좌표(아래가 +)를 구름 판 위로 옮긴다
        private void AddCloud(Vector3 direction, float radius, Vector3[] blobs)
        {
            Vector3 center = direction * drawDistance;
            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 up = Vector3.Cross(direction, right);
            float line = radius * strokeShare;
            foreach (Vector3 b in blobs)
                AddRing(center, right, up, new Vector2(b.x, b.y) * radius, b.z * radius - line / 2f, b.z * radius + line / 2f, stroke);
            foreach (Vector3 b in blobs)
                AddDisc(center, right, up, new Vector2(b.x, b.y) * radius, b.z * radius - line / 2f, radius, null);
            foreach (Vector3 b in blobs)
                AddDisc(center, right, up, new Vector2(b.x - b.z * 0.25f, b.y - b.z * 0.3f) * radius, b.z * radius * 0.55f, radius, highlight);
        }

        private void AddRing(Vector3 center, Vector3 right, Vector3 up, Vector2 at, float inner, float outer, Color32 color)
        {
            int start = _vertices.Count;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                _vertices.Add(Point(center, right, up, at + dir * Mathf.Max(inner, 0f)));
                _vertices.Add(Point(center, right, up, at + dir * outer));
                _colors.Add(color);
                _colors.Add(color);
            }
            for (int i = 0; i < CircleSegments; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % CircleSegments * 2;
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(a + 1);
                _triangles.Add(b);
                _triangles.Add(b + 1);
                _triangles.Add(a + 1);
            }
        }

        // color 가 없으면 구름 속 그라데이션 (캔버스 y −r ~ 0.5r 에 걸쳐 위 · 60% · 아래)
        private void AddDisc(Vector3 center, Vector3 right, Vector3 up, Vector2 at, float radius, float cloudRadius, Color? color)
        {
            int start = _vertices.Count;
            _vertices.Add(Point(center, right, up, at));
            _colors.Add(color ?? Fill(at.y, cloudRadius));
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / CircleSegments;
                Vector2 p = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Max(radius, 0f);
                _vertices.Add(Point(center, right, up, p));
                _colors.Add(color ?? Fill(p.y, cloudRadius));
            }
            for (int i = 0; i < CircleSegments; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + 1 + i);
                _triangles.Add(start + 1 + (i + 1) % CircleSegments);
            }
        }

        private Color32 Fill(float canvasY, float cloudRadius)
        {
            float t = Mathf.Clamp01((canvasY + cloudRadius) / (cloudRadius * 1.5f));
            return t < 0.6f ? Color.Lerp(fillTop, fillMiddle, t / 0.6f) : Color.Lerp(fillMiddle, fillBottom, (t - 0.6f) / 0.4f);
        }

        // 캔버스 좌표(아래가 +) → 구름 판 위 점
        private static Vector3 Point(Vector3 center, Vector3 right, Vector3 up, Vector2 canvas) => center + right * canvas.x - up * canvas.y;
    }
}
