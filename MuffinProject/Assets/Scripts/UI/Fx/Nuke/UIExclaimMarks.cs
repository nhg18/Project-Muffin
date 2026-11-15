using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 느낌표 여러 개를 한 메시로 그리는 UI 도형 — 시안 NUKE3D overlay (흰 면 + 진한 테두리의 사다리꼴 막대와 점).
    /// 자리 · 크기는 <see cref="NukeSceneFx"/> 가 매 프레임 넣는다 (이 판 가운데 기준, 위로 +). 면 색 = Graphic.color.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIExclaimMarks : MaskableGraphic
    {
        /// <summary>느낌표 하나 — 자리는 아래 점 근처(시안 머리 위 점), 크기 s 는 막대 높이의 1 배 단위.</summary>
        public struct Mark
        {
            public Vector2 Position;
            public float Size;
            /// <summary>테두리 두께.</summary>
            public float Line;
            public float Alpha;
        }

        private const int CircleSegments = 20;

        // 시안 막대 (s 단위, 위로 +): 위 왼쪽 → 위 오른쪽 → 아래 오른쪽 → 아래 왼쪽
        private static readonly Vector2[] Bar =
        {
            new Vector2(-0.16f, 1.5f),
            new Vector2(0.2f, 1.5f),
            new Vector2(0.08f, 0.5f),
            new Vector2(-0.06f, 0.5f),
        };

        [Tooltip("테두리 색 (시안 #2b1e1a).")]
        [SerializeField] private Color lineColor = new Color32(0x2b, 0x1e, 0x1a, 0xff);

        private readonly List<Mark> _marks = new List<Mark>();
        private readonly Vector2[] _outer = new Vector2[CircleSegments];
        private readonly Vector2[] _inner = new Vector2[CircleSegments];

        /// <summary>이번 프레임의 느낌표들. 바뀔 때만 다시 그린다.</summary>
        public void SetMarks(List<Mark> marks)
        {
            if (marks.Count == 0 && _marks.Count == 0)
                return;
            _marks.Clear();
            _marks.AddRange(marks);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            foreach (Mark mark in _marks)
            {
                if (mark.Size <= 0f || mark.Alpha <= 0f)
                    continue;
                Color fill = color;
                fill.a *= mark.Alpha;
                Color ink = lineColor;
                ink.a *= mark.Alpha;
                float half = mark.Line * 0.5f;

                // 막대: 테두리 띠(바깥 → 안) + 안쪽 면 — 겹치지 않게 그려 흐려져도 테두리가 비치지 않는다
                // 막대 아래 끝의 반 폭(0.07 s)보다 안으로 밀면 뒤집히므로 그 전에 멈춘다
                float inset = Mathf.Min(half, 0.06f * mark.Size);
                for (int i = 0; i < Bar.Length; i++)
                {
                    Vector2 corner = mark.Position + Bar[i] * mark.Size;
                    Vector2 dir = Miter(Bar, i);
                    _outer[i] = corner + dir * half;
                    _inner[i] = corner - dir * inset;
                }

                AddBand(vh, Bar.Length, ink);
                AddFan(vh, Bar.Length, fill);

                // 점
                Vector2 center = mark.Position + new Vector2(0f, 0.18f * mark.Size);
                float r = 0.16f * mark.Size;
                float innerR = Mathf.Max(0f, r - half);
                for (int i = 0; i < CircleSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / CircleSegments;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    _outer[i] = center + dir * (r + half);
                    _inner[i] = center + dir * innerR;
                }

                AddBand(vh, CircleSegments, ink);
                AddFan(vh, CircleSegments, fill);
            }
        }

        // 꼭짓점 i 에서 두 변을 같은 거리만큼 밖으로 미는 방향 (길이 = 1 / sin(반각) — 캔버스 miter 와 같다)
        private static Vector2 Miter(Vector2[] polygon, int i)
        {
            int n = polygon.Length;
            Vector2 prev = polygon[(i + n - 1) % n];
            Vector2 next = polygon[(i + 1) % n];
            Vector2 at = polygon[i];
            Vector2 n1 = Outward(prev, at);
            Vector2 n2 = Outward(at, next);
            Vector2 sum = n1 + n2;
            float dot = Vector2.Dot(sum.normalized, n1);
            return sum.normalized / Mathf.Max(0.2f, dot);
        }

        // 시계 방향 다각형의 변 a → b 바깥 법선
        private static Vector2 Outward(Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            return new Vector2(-d.y, d.x);
        }

        private void AddBand(VertexHelper vh, int count, Color ink)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.AddVert(_outer[i], ink, Vector2.zero);
                vh.AddVert(_inner[i], ink, Vector2.zero);
            }

            for (int i = 0; i < count; i++)
            {
                int a = start + i * 2;
                int b = start + (i + 1) % count * 2;
                vh.AddTriangle(a, b, a + 1);
                vh.AddTriangle(b, b + 1, a + 1);
            }
        }

        private void AddFan(VertexHelper vh, int count, Color fill)
        {
            int start = vh.currentVertCount;
            for (int i = 0; i < count; i++)
                vh.AddVert(_inner[i], fill, Vector2.zero);
            for (int i = 1; i < count - 1; i++)
                vh.AddTriangle(start, start + i, start + i + 1);
        }
    }
}
