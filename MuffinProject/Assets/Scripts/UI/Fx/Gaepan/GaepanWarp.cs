using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 첫 장면(번쩍)의 눈송이 워프 — 흰 줄들이 카메라 쪽으로 날아든다 (시안 DOGPILE.SC.flash).
    /// 시안은 줄을 3D 마을보다 뒤 캔버스에 그려 땅 · 나무에 가려지고 하늘에만 보인다 — 여기서도 하늘 다음, 마을보다 먼저 깊이 없이 그린다 (머티리얼 Render Queue).
    /// 줄 굵기는 가까운 끝의 화면 굵기로 끝까지 같고, 끝은 둥글다.
    /// </summary>
    public class GaepanWarp : MonoBehaviour
    {
        private const int CapSegments = 6;
        private const float GoldenAngle = 2.39996f;

        [SerializeField] private MeshFilter filter;
        [SerializeField] private MeshRenderer meshRenderer;
        [Tooltip("줄 수.")]
        [SerializeField] private int count = 60;
        [Tooltip("줄이 다가오는 빠르기 (초당 깊이 칸 비율).")]
        [SerializeField] private float speed = 2.4f;
        [Tooltip("줄이 나타나는 깊이 범위 (카메라 앞, 가까움 · 길이).")]
        [SerializeField] private Vector2 depth = new Vector2(0.5f, 20f);
        [Tooltip("줄 길이 (앞뒤).")]
        [SerializeField] private float length = 1.4f;
        [Tooltip("가까운 끝의 굵기.")]
        [SerializeField] private float width = 0.05f;
        [Tooltip("줄이 모이는 높이.")]
        [SerializeField] private float centerHeight = 1.2f;
        [SerializeField] private Color color = new Color(1f, 1f, 1f, 0.9f);

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();
        private Mesh _mesh;

        private void Awake()
        {
            if (filter == null)
                Debug.LogError($"[{nameof(GaepanWarp)}] {nameof(filter)} 참조가 비어 있습니다.", this);
            if (meshRenderer == null)
                Debug.LogError($"[{nameof(GaepanWarp)}] {nameof(meshRenderer)} 참조가 비어 있습니다.", this);
            _mesh = new Mesh { name = "GaepanWarp" };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;
            meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }

        /// <summary>t 초(장면 처음부터)의 줄들을 그린다. camera = 카메라 자리 · 앞 방향 (눈 마을 좌표).</summary>
        public void Draw(float t, Vector3 cameraPosition, Vector3 cameraForward)
        {
            meshRenderer.enabled = true;
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            for (int i = 0; i < count; i++)
            {
                float angle = i * GoldenAngle;
                float z = GaepanWorld.Fraction(0.9f - t * speed + i * 0.37f) * depth.y + depth.x;
                float radius = 0.4f + (i % 7) * 0.5f;
                var near = new Vector3(Mathf.Cos(angle) * radius, centerHeight + Mathf.Sin(angle) * radius, cameraPosition.z + z);
                AddLine(near, near + Vector3.forward * length, cameraPosition, cameraForward);
            }
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }

        /// <summary>줄을 감춘다.</summary>
        public void Hide()
        {
            meshRenderer.enabled = false;
        }

        // 카메라를 보는 띠 + 둥근 끝. 먼 끝은 깊이만큼 굵게 해서 화면에서 굵기가 같게
        private void AddLine(Vector3 a, Vector3 b, Vector3 eye, Vector3 forward)
        {
            float depthA = Vector3.Dot(a - eye, forward), depthB = Vector3.Dot(b - eye, forward);
            if (depthA <= 0.01f || depthB <= 0.01f)
                return;
            float halfA = width / 2f, halfB = halfA * depthB / depthA;
            Vector3 along = (b - a).normalized;
            Vector3 sideA = Vector3.Cross(along, a - eye).normalized, sideB = Vector3.Cross(along, b - eye).normalized;
            Color32 c = color;
            int start = _vertices.Count;
            _vertices.Add(a - sideA * halfA);
            _vertices.Add(a + sideA * halfA);
            _vertices.Add(b + sideB * halfB);
            _vertices.Add(b - sideB * halfB);
            for (int i = 0; i < 4; i++)
                _colors.Add(c);
            _triangles.Add(start);
            _triangles.Add(start + 1);
            _triangles.Add(start + 2);
            _triangles.Add(start);
            _triangles.Add(start + 2);
            _triangles.Add(start + 3);
            AddCap(a, sideA, -Across(along, a - eye), halfA, c);
            AddCap(b, sideB, Across(along, b - eye), halfB, c);
        }

        // 화면에서 본 줄 방향 (보는 방향 성분을 뺀 것) — 둥근 끝이 화면에서 둥글게
        private static Vector3 Across(Vector3 along, Vector3 ray)
        {
            Vector3 r = ray.normalized;
            return (along - r * Vector3.Dot(along, r)).normalized;
        }

        private void AddCap(Vector3 center, Vector3 side, Vector3 outward, float radius, Color32 c)
        {
            int start = _vertices.Count;
            _vertices.Add(center);
            _colors.Add(c);
            for (int i = 0; i <= CapSegments; i++)
            {
                float angle = i * Mathf.PI / CapSegments;
                _vertices.Add(center + (side * Mathf.Cos(angle) + outward * Mathf.Sin(angle)) * radius);
                _colors.Add(c);
            }
            for (int i = 0; i < CapSegments; i++)
            {
                _triangles.Add(start);
                _triangles.Add(start + 1 + i);
                _triangles.Add(start + 2 + i);
            }
        }
    }
}
