using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx
{
    /// <summary>
    /// 잉크 테 도형을 메시에 더하는 도우미 — 면(가운데에서 부채꼴) · 닫힌 테(선 가운데 기준, 꺾인 곳은 미터). 4 단계 공통 연출의 별 글자 · 입자가 같이 쓴다.
    /// 시안 「4단계 공통 연출」 은 캔버스에 fill() 뒤 stroke() — 테가 면 가장자리를 반쯤 덮는다. 같은 순서로 더한다.
    /// </summary>
    public static class InkMesh
    {
        // 꺾인 곳의 미터 길이 상한 — 선 두께의 반 대비 (캔버스 miterLimit 10 이면 별 끝도 뾰족하다. 너무 긴 끝만 자른다)
        private const float MiterLimit = 4f;

        /// <summary>점들로 둘러싼 면 — center 에서 부채꼴로 채운다 (가운데에서 모든 점이 보이는 모양만: 별 · 원 · 하트 · 둥근 사각형).</summary>
        public static void Fill(VertexHelper vh, Vector2 center, List<Vector2> points, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(center, color, Vector4.zero);
            for (int i = 0; i < points.Count; i++)
                vh.AddVert(points[i], color, Vector4.zero);
            for (int i = 0; i < points.Count; i++)
                vh.AddTriangle(start, start + 1 + i, start + 1 + (i + 1) % points.Count);
        }

        /// <summary>닫힌 테 — 선 가운데가 점들을 지난다. width 는 전체 두께.</summary>
        public static void Stroke(VertexHelper vh, List<Vector2> points, float width, Color32 color)
        {
            int n = points.Count;
            if (n < 2 || width <= 0f)
                return;

            float half = width / 2f;
            int start = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = points[(i - 1 + n) % n];
                Vector2 here = points[i];
                Vector2 next = points[(i + 1) % n];
                Vector2 n0 = Normal(prev, here);
                Vector2 n1 = Normal(here, next);
                Vector2 miter = (n0 + n1).normalized;
                float dot = Vector2.Dot(miter, n1);
                float length = dot > 1e-3f ? Mathf.Min(half / dot, half * MiterLimit) : half;
                if (miter == Vector2.zero)
                {
                    miter = n1;
                    length = half;
                }
                vh.AddVert(here + miter * length, color, Vector4.zero);
                vh.AddVert(here - miter * length, color, Vector4.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a = start + i * 2, b = start + (i + 1) % n * 2;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
            }
        }

        // 선분 a → b 의 바깥쪽 수직 (점이 반시계로 돌면 바깥, 시계로 돌면 안쪽 — 테는 양쪽으로 같은 두께라 어느 쪽이든 같다)
        private static Vector2 Normal(Vector2 a, Vector2 b)
        {
            Vector2 d = (b - a).normalized;
            return new Vector2(d.y, -d.x);
        }

        /// <summary>원 둘레의 점 (반시계, 3시에서 시작).</summary>
        public static void Circle(List<Vector2> points, Vector2 center, float radius, int segments)
        {
            points.Clear();
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
        }
    }
}
