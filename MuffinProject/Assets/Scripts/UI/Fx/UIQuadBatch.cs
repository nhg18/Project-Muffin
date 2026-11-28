using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 코드로 매 프레임 채우는 네모 · 세모 묶음 (UI 도형) — 별 수천 개처럼 오브젝트로 두기엔 많은 것을 메시 한 장으로 그린다.
    /// 쓰는 법: <see cref="Clear"/> → <see cref="Quad(Vector2, Vector2, Color32)"/> · <see cref="Line"/> · <see cref="Triangle"/> … → <see cref="Apply"/>.
    /// 좌표는 이 RectTransform 의 로컬(피벗 기준, y 위 +), 색은 넣은 꼭짓점 색 그대로 (Graphic.color 는 곱하지 않는다).
    /// 그림을 주면 모든 네모가 그 그림을 쓰고, 비우면 단색이다. 전체를 흐리게 할 때는 CanvasRenderer.SetAlpha.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIQuadBatch : MaskableGraphic
    {
        private static readonly Rect FullUv = new Rect(0f, 0f, 1f, 1f);

        [Tooltip("네모에 입힐 그림. 비우면 단색.")]
        [SerializeField] private Texture texture;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _indices = new List<int>();
        private Mesh _mesh;

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        /// <summary>네모에 입힐 그림을 바꾼다 (코드로 만든 그림용).</summary>
        public void SetTexture(Texture newTexture)
        {
            texture = newTexture;
            SetMaterialDirty();
        }

        /// <summary>넣어 둔 도형을 모두 비운다. 비운 채 <see cref="Apply"/> 하면 아무것도 그리지 않는다.</summary>
        public void Clear()
        {
            _vertices.Clear();
            _colors.Clear();
            _uvs.Clear();
            _indices.Clear();
        }

        /// <summary>가운데 · 크기로 바로 선 네모 하나.</summary>
        public void Quad(Vector2 center, Vector2 size, Color32 color) => Quad(center, size, color, FullUv);

        /// <summary>
        /// 스프라이트 칸 전체의 그림 좌표 (x 왼쪽 · y 아래 · z 오른쪽 · w 위) — 둘레의 빈 곳까지 든 칸이다.
        /// DataUtility.GetOuterUV 는 빈 곳을 잘라낸 칸을 준다 — 그림 안의 자리를 비율로 짚을 때는 이쪽을 쓴다. 아틀라스에 묶인 스프라이트에는 쓸 수 없다.
        /// </summary>
        public static Vector4 SpriteUv(Sprite sprite)
        {
            Rect rect = sprite.rect;
            Texture texture = sprite.texture;
            return new Vector4(rect.xMin / texture.width, rect.yMin / texture.height, rect.xMax / texture.width, rect.yMax / texture.height);
        }

        /// <summary>가운데 · 크기로 바로 선 네모 하나 — 그림의 uv 칸만 입힌다.</summary>
        public void Quad(Vector2 center, Vector2 size, Color32 color, Rect uv)
        {
            Vector2 half = size * 0.5f;
            int start = _vertices.Count;
            Add(new Vector2(center.x - half.x, center.y - half.y), color, new Vector2(uv.xMin, uv.yMin));
            Add(new Vector2(center.x - half.x, center.y + half.y), color, new Vector2(uv.xMin, uv.yMax));
            Add(new Vector2(center.x + half.x, center.y + half.y), color, new Vector2(uv.xMax, uv.yMax));
            Add(new Vector2(center.x + half.x, center.y - half.y), color, new Vector2(uv.xMax, uv.yMin));
            AddQuadIndices(start);
        }

        /// <summary>네 꼭짓점 · 꼭짓점마다 색으로 네모 하나 (a → b → c → d 둘레 순서).</summary>
        public void Quad(Vector2 a, Color32 colorA, Vector2 b, Color32 colorB, Vector2 c, Color32 colorC, Vector2 d, Color32 colorD)
        {
            int start = _vertices.Count;
            Add(a, colorA, Vector2.zero);
            Add(b, colorB, Vector2.zero);
            Add(c, colorC, Vector2.zero);
            Add(d, colorD, Vector2.zero);
            AddQuadIndices(start);
        }

        /// <summary>네 꼭짓점 · 꼭짓점마다 색과 그림 좌표로 네모 하나 (a → b → c → d 둘레 순서) — 그림의 일부에 그라데이션을 입힐 때.</summary>
        public void Quad(Vector2 a, Vector2 uvA, Color32 colorA, Vector2 b, Vector2 uvB, Color32 colorB, Vector2 c, Vector2 uvC, Color32 colorC, Vector2 d, Vector2 uvD, Color32 colorD)
        {
            int start = _vertices.Count;
            Add(a, colorA, uvA);
            Add(b, colorB, uvB);
            Add(c, colorC, uvC);
            Add(d, colorD, uvD);
            AddQuadIndices(start);
        }

        /// <summary>두 점을 잇는 굵은 선 하나.</summary>
        public void Line(Vector2 from, Vector2 to, float width, Color32 color)
        {
            Vector2 along = to - from;
            float length = along.magnitude;
            // 길이가 없으면 점 하나로
            if (length < 0.001f)
            {
                Quad(from, new Vector2(width, width), color);
                return;
            }

            Vector2 side = new Vector2(-along.y, along.x) * (width * 0.5f / length);
            Quad(from - side, color, from + side, color, to + side, color, to - side, color);
        }

        /// <summary>세 꼭짓점 · 꼭짓점마다 색으로 세모 하나.</summary>
        public void Triangle(Vector2 a, Color32 colorA, Vector2 b, Color32 colorB, Vector2 c, Color32 colorC)
        {
            int start = _vertices.Count;
            Add(a, colorA, Vector2.zero);
            Add(b, colorB, Vector2.zero);
            Add(c, colorC, Vector2.zero);
            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
        }

        /// <summary>넣은 도형을 화면에 올린다.</summary>
        public void Apply()
        {
            SetVerticesDirty();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_mesh == null) return;

            if (Application.isPlaying)
                Destroy(_mesh);
            else
                DestroyImmediate(_mesh);
        }

        // Graphic 의 VertexHelper 를 거치지 않고 모아 둔 목록을 그대로 메시로 올린다 — 꼭짓점이 만 개를 넘어도 한 번에
        protected override void UpdateGeometry()
        {
            if (_mesh == null)
                _mesh = new Mesh { name = nameof(UIQuadBatch), hideFlags = HideFlags.HideAndDontSave };
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_indices, 0, false);
            canvasRenderer.SetMesh(_mesh);
        }

        private void Add(Vector2 position, Color32 color, Vector2 uv)
        {
            _vertices.Add(position);
            _colors.Add(color);
            _uvs.Add(uv);
        }

        private void AddQuadIndices(int start)
        {
            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }
    }
}
