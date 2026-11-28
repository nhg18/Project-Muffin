using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 개판 오분전 처형 장면의 눈 소품 메시 — 바위 · 눈더미 · 눈덩이 (시안 D3R rockGeo2 · heapGeo · clodGeo, 모양 값은 시안과 같다).
    /// 정점 색 + 법선만 가진 메시라 Gaepan Toon 셰이더로 그린다. 만든 메시는 부른 쪽이 지운다 (GaepanWorld).
    /// </summary>
    public static class GaepanSnowMeshes
    {
        private static readonly Color32 Snow = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
        private static readonly Color32 RockDark = new Color32(0x7C, 0x6A, 0x5E, 0xFF);
        private static readonly Color32 RockLight = new Color32(0x8E, 0x7B, 0x6C, 0xFF);
        // 바위: 이 높이(단위 공 y) 위는 눈, 0 위는 밝은 돌
        private const float RockSnowLine = 0.42f;
        // 공 모양 소품의 반지름 (x · y · z) — 조금 납작하다
        private static readonly Vector3 LumpRadius = new Vector3(1f, 0.78f, 1f);
        // 눈더미: 둘레 칸 수 · 반지름 칸 수 · 가장자리 끝 (반지름 1 밖으로 자락이 깔린다)
        private const int HeapSides = 44, HeapRings = 14;
        private const float HeapReach = 1.14f;

        /// <summary>눈 덮인 바위 (반지름 1).</summary>
        public static Mesh BuildRock()
        {
            var builder = new Builder();
            builder.Sphere(LumpRadius, 14, 10, 0.12f, 1.7f, unit => unit.y > RockSnowLine ? Snow : unit.y > 0f ? RockLight : RockDark);
            return builder.ToMesh("GaepanRock");
        }

        /// <summary>눈덩이 (반지름 1) — 눈더미 겉에 박히고, 터질 때 알갱이로 날린다.</summary>
        public static Mesh BuildClod()
        {
            var builder = new Builder();
            builder.Sphere(LumpRadius, 9, 6, 0.28f, 2.3f, _ => Snow);
            return builder.ToMesh("GaepanClod");
        }

        /// <summary>눈더미 (반지름 1, 높이 0.6쯤) — 울퉁불퉁한 봉우리와 바닥에 깔린 자락.</summary>
        public static Mesh BuildHeap()
        {
            var builder = new Builder();
            var grid = new Vector3[HeapRings + 1, HeapSides + 1];
            for (int k = 0; k <= HeapRings; k++)
            {
                float r = (float)k / HeapRings * HeapReach;
                for (int i = 0; i <= HeapSides; i++)
                {
                    float a = (float)i / HeapSides * 6.283f;
                    float spread = r * (1f + 0.1f * Mathf.Sin(a * 4f + 0.5f) + 0.06f * Mathf.Sin(a * 9f));
                    grid[k, i] = new Vector3(Mathf.Cos(a) * spread, HeapHeight(r, a), Mathf.Sin(a) * spread);
                }
            }

            var ids = new int[HeapRings + 1, HeapSides + 1];
            for (int k = 0; k <= HeapRings; k++)
            {
                for (int i = 0; i <= HeapSides; i++)
                {
                    Vector3 next = grid[k, (i + 1) % HeapSides], previous = grid[k, (i + HeapSides - 1) % HeapSides];
                    Vector3 outer = grid[Mathf.Min(HeapRings, k + 1), i], inner = grid[Mathf.Max(0, k - 1), i];
                    Vector3 normal = Vector3.Cross(outer - inner, next - previous);
                    if (normal.y < 0f)
                        normal = -normal;
                    if (k == 0)
                        normal = Vector3.up;
                    ids[k, i] = builder.Vertex(grid[k, i], normal, Snow);
                }
            }
            for (int k = 0; k < HeapRings; k++)
            {
                for (int i = 0; i < HeapSides; i++)
                {
                    builder.Triangle(ids[k, i], ids[k + 1, i], ids[k, i + 1]);
                    builder.Triangle(ids[k, i + 1], ids[k + 1, i], ids[k + 1, i + 1]);
                }
            }
            return builder.ToMesh("GaepanHeap");
        }

        // 시안 heapGeo 의 H: 가운데서 r 만큼 떨어진 곳의 높이 (r 1 밖은 땅 밑으로 내려가는 자락)
        private static float HeapHeight(float r, float a)
        {
            if (r >= 1f)
                return -(r - 1f) * 0.5f;
            float dome = 0.6f * Mathf.Pow(1f - Mathf.Pow(r, 1.5f), 1.4f) * (1f + r * (0.3f * Mathf.Sin(a * 3f + 1f) + 0.16f * Mathf.Sin(a * 7f + 2f)));
            return dome + 0.045f * Mathf.Sin(a * 5f + r * 11f) * (1f - r) * Mathf.Min(1f, r * 3f);
        }

        // 시안 Geo: 정점(자리 · 법선 · 색)과 삼각형을 모은다. 삼각형은 법선 쪽이 앞면이 되게 돌린다
        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Color32> _colors = new List<Color32>();
            private readonly List<int> _triangles = new List<int>();

            public int Vertex(Vector3 position, Vector3 normal, Color32 color)
            {
                _vertices.Add(position);
                _normals.Add(normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up);
                _colors.Add(color);
                return _vertices.Count - 1;
            }

            public void Triangle(int a, int b, int c)
            {
                Vector3 face = Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]);
                if (Vector3.Dot(face, _normals[a] + _normals[b] + _normals[c]) < 0f)
                {
                    _triangles.Add(a);
                    _triangles.Add(c);
                    _triangles.Add(b);
                    return;
                }
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }

            // 시안 sphere: 울퉁불퉁한 공 — bump = 울퉁불퉁한 정도, seed = 무늬. colorOf 는 단위 공 위의 점으로 색을 정한다
            public void Sphere(Vector3 radius, int sides, int rings, float bump, float seed, Func<Vector3, Color32> colorOf)
            {
                int first = _vertices.Count;
                for (int j = 0; j <= rings; j++)
                {
                    float theta = (float)j / rings * Mathf.PI;
                    for (int i = 0; i <= sides; i++)
                    {
                        float phi = (float)i / sides * Mathf.PI * 2f;
                        var unit = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                        float r = 1f + bump * Mathf.Sin(unit.x * 5.1f * seed + unit.y * 3.7f) * Mathf.Cos(unit.z * 4.3f + seed);
                        Vertex(Vector3.Scale(unit, radius) * r, new Vector3(unit.x / radius.x, unit.y / radius.y, unit.z / radius.z), colorOf(unit));
                    }
                }
                for (int j = 0; j < rings; j++)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        int a = first + j * (sides + 1) + i, b = a + sides + 1;
                        Triangle(a, b, a + 1);
                        Triangle(a + 1, b, b + 1);
                    }
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetColors(_colors);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
