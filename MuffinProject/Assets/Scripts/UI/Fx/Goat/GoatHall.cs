using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chapchu.UI.Fx.Goat
{
    /// <summary>
    /// 「GOAT」 컷인의 돌 회랑을 코드로 짓는다 — 시안 goat_cutin.js 의 scene() 을 그대로 옮겼다.
    /// 시안과 같은 난수를 같은 순서로 뽑아서 부서진 기둥 · 꽂힌 칼 · 구름 · 재가 시안과 같은 자리에 놓인다 (뽑는 순서를 바꾸면 자리가 달라진다).
    /// 좌표는 시안과 같다: x 오른쪽 · y 위 · z 는 카메라 쪽이 + (성문 −30, 회랑 끝 +46). 메시로 낼 때만 Unity 에 맞게 z 를 뒤집는다.
    /// 색은 꼭짓점 색, 스스로 빛나는 정도(창 · 문 뒤 빛)는 uv.x — 셰이더 「Goat Stone」 이 읽는다.
    /// </summary>
    internal sealed class GoatHall
    {
        // 시안 rng(41)
        private const uint Seed = 41;
        private const float Tau = 6.2832f;

        private static readonly Color Stone = new Color(0.3f, 0.31f, 0.34f);
        private static readonly Color DarkStone = new Color(0.17f, 0.18f, 0.2f);
        private static readonly Color LightStone = new Color(0.4f, 0.41f, 0.44f);
        private static readonly Color Iron = new Color(0.12f, 0.12f, 0.14f);
        private static readonly Color Window = new Color(0.3f, 0.4f, 0.55f);
        // 양옆 기둥 높이 — 높은 것(7 초과)은 머리돌을 얹고, 낮은 것은 부서져 둘레에 돌 조각이 흩어진다
        private static readonly float[] PillarHeights = { 9f, 4.2f, 11.5f, 3f, 8f, 5.5f, 10.5f };

        /// <summary>모닥불 자리 (시안 BF) — 칼이 꽂혀 있다.</summary>
        public static readonly Vector3 Bonfire = new Vector3(3.1f, 0f, 19f);

        /// <summary>회랑 · 성벽 · 탑 · 칼 한 장.</summary>
        public Mesh Hall;

        /// <summary>성문 한 짝 — 경첩이 x 0, 문이 +x 로 5. 오른쪽 문은 x 를 뒤집어 쓴다.</summary>
        public Mesh Door;

        /// <summary>먼 구름 (x, y, z, 크기).</summary>
        public Vector4[] Clouds;

        /// <summary>바닥 안개 (x, y, z, 크기) · (일렁임 위상, 알파).</summary>
        public Vector4[] Fog;
        public Vector2[] FogDrift;

        /// <summary>내리는 재 (x, 처음 높이, z) · (흔들림 위상, 빠르기).</summary>
        public Vector3[] Ash;
        public Vector2[] AshDrift;

        /// <summary>모닥불 불티 (처음 위상, 흔들림 위상, 빠르기).</summary>
        public Vector3[] Embers;

        /// <summary>발 구를 때 튀는 불티 (방향, 거리, 솟는 높이, 수명).</summary>
        public Vector4[] Burst;

        private uint _seed = Seed;

        public static GoatHall Build()
        {
            var hall = new GoatHall();
            hall.BuildHall();
            hall.BuildDoor();
            hall.Scatter();
            return hall;
        }

        // 시안 rng: 0 이상 1 미만
        private float Next()
        {
            _seed = unchecked(_seed * 1664525u + 1013904223u);
            return (float)(_seed / 4294967296.0);
        }

        private void BuildHall()
        {
            var g = new Geo();

            // 바닥 · 바닥 돌 이음매
            g.Box(0f, -1.5f, 8f, 11f, 3f, 84f, new Color(0.25f, 0.26f, 0.28f));
            for (float z = 46f; z > -28f; z -= 3.5f)
                g.Box((Next() - 0.5f) * 0.6f, 0.012f, z, 10.4f, 0.03f, 0.2f, new Color(0.15f, 0.16f, 0.18f));

            for (int side = -1; side <= 1; side += 2)
            {
                // 난간 턱 · 기둥
                g.Box(side * 5.7f, 0.5f, 8f, 0.9f, 1f, 84f, Stone);
                int index = 0;
                for (float z = 40f; z >= -20f; z -= 10f, index++)
                {
                    float height = PillarHeights[(index + (side > 0 ? 3 : 0)) % PillarHeights.Length];
                    g.Box(side * 5.7f, height / 2f, z, 1.6f, height, 1.6f, Stone, 0f, (Next() - 0.5f) * 0.15f);
                    if (height > 7f)
                    {
                        g.Box(side * 5.7f, height + 0.25f, z, 2.1f, 0.5f, 2.1f, LightStone);
                        continue;
                    }

                    for (int j = 0; j < 4; j++)
                    {
                        float x = side * (4.2f + Next() * 1.2f);
                        float rubbleZ = z + (Next() - 0.5f) * 4f;
                        float sizeX = 0.5f + Next() * 0.6f;
                        float sizeZ = 0.5f + Next() * 0.5f;
                        g.Box(x, 0.25f, rubbleZ, sizeX, 0.5f, sizeZ, Stone, 0f, Next() * 3f);
                    }
                }

                // 기둥 사이 석상 받침 · 창
                for (float z = 35f; z >= -15f; z -= 10f)
                {
                    g.Box(side * 5.7f, 2.1f, z, 0.95f, 2.2f, 0.85f, DarkStone);
                    g.Cylinder(side * 5.7f, 3.2f, z, 0.6f, 0f, 1.2f, 6, DarkStone);
                    g.Box(side * 5.15f, 1.9f, z, 0.08f, 2.4f, 0.05f, Iron);
                }

                // 성벽 · 문설주 · 버팀벽(뾰족 지붕) · 창 · 모서리 탑 · 문 옆 화로
                g.Box(side * 22.5f, 22f, -30f, 35f, 48f, 4f, Stone);
                g.Box(side * 5.25f, 6.5f, -27.85f, 0.5f, 13f, 0.4f, LightStone);
                foreach (float x in new[] { 9f, 19f, 30f })
                {
                    g.Box(side * x, 12f, -27f, 2.4f, 24f, 3f, LightStone);
                    g.Box(side * x, 27f, -27.5f, 1.8f, 6f, 2f, LightStone);
                    g.Cylinder(side * x, 30f, -27.5f, 1.1f, 0f, 5.5f, 4, DarkStone, 0f, 0.785f);
                }
                foreach (float x in new[] { 14f, 24.5f })
                    g.Box(side * x, 20f, -27.95f, 1.5f, 9f, 0.1f, Window, 0.45f);
                g.Cylinder(side * 36f, -10f, -31f, 6f, 5.4f, 72f, 8, Stone, 0f, 0.39f);
                g.Cylinder(side * 36f, 62f, -31f, 7f, 0f, 24f, 8, DarkStone, 0f, 0.39f);
                g.Cylinder(side * 7.3f, 0f, -25.4f, 0.28f, 0.28f, 2.6f, 6, Iron);
                g.Cylinder(side * 7.3f, 2.6f, -25.4f, 0.3f, 0.85f, 0.5f, 8, Iron);
            }

            // 성문 위 아치 (한 단씩 좁아진다)
            for (int y = 13; y < 22; y++)
            {
                float k = (y + 0.5f - 13f) / 9f;
                float halfWidth = 5f * (1f - Mathf.Pow(k, 1.7f));
                for (int side = -1; side <= 1; side += 2)
                {
                    g.Box(side * (halfWidth + (5f - halfWidth) / 2f), y + 0.5f, -30f, 5f - halfWidth + 0.02f, 1.02f, 4f, Stone);
                    g.Box(side * (halfWidth + 0.25f), y + 0.5f, -27.85f, 0.5f, 1.02f, 0.4f, LightStone);
                }
            }

            // 문 위 벽 · 창 셋 · 박공 · 가운데 첨탑
            g.Box(0f, 34f, -30f, 10f, 24f, 4f, Stone);
            foreach (float x in new[] { -2.4f, 0f, 2.4f })
                g.Box(x, 29f + (x != 0f ? 0f : 1.2f), -27.95f, 1.1f, 6f, 0.1f, Window, 0.5f);
            for (int i = 0; i < 9; i++)
                g.Box(0f, 46.5f + i, -30f, 40f - i * 4.4f, 1f, 4f, Stone);
            g.Box(0f, 56f, -31f, 7f, 10f, 5f, Stone);
            g.Cylinder(0f, 61f, -31f, 5f, 0f, 28f, 4, DarkStone, 0f, 0.785f);

            // 문 안쪽 바닥 · 문 뒤의 빛 (스스로 빛난다 — 문이 열리면 드러난다)
            g.Box(0f, -0.5f, -40f, 16f, 1f, 18f, DarkStone);
            g.Box(0f, 12f, -44.5f, 30f, 30f, 0.2f, new Color(0.76f, 0.83f, 0.92f), 1.25f);

            // 길에 꽂힌 칼 열여섯 자루 (가운데 길은 비운다) · 모닥불 받침과 칼
            for (int i = 0; i < 16; i++)
            {
                float x = (Next() - 0.5f) * 9f;
                if (Mathf.Abs(x) < 1.7f)
                    x += x < 0f ? -1.7f : 1.7f;
                float z = 32f - Next() * 52f;
                float height = 1.5f + Next() * 0.9f;
                float turn = Next() * 3f;
                AddSword(g, x, z, height, turn, (Next() - 0.5f) * 0.5f);
            }
            g.Cylinder(Bonfire.x, 0f, Bonfire.z, 1f, 0.55f, 0.3f, 7, new Color(0.07f, 0.06f, 0.06f));
            AddSword(g, Bonfire.x, Bonfire.z, 2.4f, 0.5f, 0.1f);

            Hall = g.ToMesh("GoatHall");
        }

        // 땅에 꽂힌 칼 — 날 · 날 가운데 골 · 가드 · 손잡이 · 자루 끝. tilt 만큼 기울이고 turn 만큼 돌려 (x, z) 에 꽂는다
        private static void AddSword(Geo into, float x, float z, float height, float turn, float tilt)
        {
            var sword = new Geo();
            Color hilt = new Color(0.1f, 0.09f, 0.09f);
            float blade = height * 0.84f;
            sword.Box(0f, blade / 2f, 0f, 0.18f, blade, 0.035f, new Color(0.36f, 0.37f, 0.42f));
            sword.Box(0f, blade * 0.5f, 0f, 0.05f, blade * 0.92f, 0.05f, new Color(0.52f, 0.54f, 0.6f));
            sword.Box(0f, blade + 0.035f, 0f, 0.5f, 0.07f, 0.09f, hilt);
            for (int side = -1; side <= 1; side += 2)
                sword.Box(side * 0.26f, blade - 0.03f, 0f, 0.07f, 0.14f, 0.09f, hilt);
            sword.Box(0f, blade + 0.07f + (height - blade - 0.1f) / 2f, 0f, 0.065f, height - blade - 0.1f, 0.065f, new Color(0.22f, 0.14f, 0.09f));
            sword.Box(0f, height, 0f, 0.14f, 0.12f, 0.12f, hilt);

            float cosTilt = Mathf.Cos(tilt), sinTilt = Mathf.Sin(tilt), cosTurn = Mathf.Cos(turn), sinTurn = Mathf.Sin(turn);
            Vector3 Turn(Vector3 v)
            {
                float tiltedX = v.x * cosTilt - v.y * sinTilt;
                float tiltedY = v.x * sinTilt + v.y * cosTilt;
                return new Vector3(tiltedX * cosTurn + v.z * sinTurn, tiltedY, -tiltedX * sinTurn + v.z * cosTurn);
            }

            // 땅에 0.14 묻는다
            var place = new Vector3(x, -0.14f, z);
            for (int i = 0; i < sword.Positions.Count; i++)
                into.Add(Turn(sword.Positions[i]) + place, Turn(sword.Normals[i]), sword.Colors[i], sword.Emissive[i].x);
        }

        private void BuildDoor()
        {
            var d = new Geo();
            Color wood = new Color(0.13f, 0.11f, 0.1f);
            Color band = new Color(0.24f, 0.24f, 0.27f);
            d.Box(2.5f, 11f, 0f, 5f, 22f, 0.7f, wood);
            // 쇠 띠 넷 · 띠마다 못 다섯
            foreach (float y in new[] { 2.5f, 8f, 13.5f, 19f })
            {
                d.Box(2.5f, y, 0.4f, 5f, 0.6f, 0.12f, band);
                for (int i = 0; i < 5; i++)
                    d.Cylinder(0.5f + i * 0.9f, y - 0.2f, 0.5f, 0.15f, 0.15f, 0.4f, 6, band);
            }
            foreach (float x in new[] { 1.2f, 3.8f })
                d.Box(x, 11f, 0.38f, 0.3f, 22f, 0.1f, band);
            // 문고리
            d.Cylinder(4.2f, 9f, 0.45f, 0.55f, 0.55f, 0.2f, 10, band);
            Door = d.ToMesh("GoatDoor");
        }

        // 떠다니는 것들의 자리 — 회랑 다음에 뽑는다 (시안과 같은 순서: 구름 → 안개 → 재 → 불티 → 튀는 불티)
        private void Scatter()
        {
            Clouds = new Vector4[18];
            for (int i = 0; i < Clouds.Length; i++)
            {
                float x = (Next() - 0.5f) * 360f;
                float y = 40f + Next() * 70f;
                float z = -160f - Next() * 100f;
                Clouds[i] = new Vector4(x, y, z, 34f + Next() * 40f);
            }

            Fog = new Vector4[70];
            FogDrift = new Vector2[Fog.Length];
            for (int i = 0; i < Fog.Length; i++)
            {
                float x = (Next() - 0.5f) * 30f;
                float y = 0.3f + Next() * 1.6f;
                float z = -27f + Next() * 72f;
                Fog[i] = new Vector4(x, y, z, 4f + Next() * 4.5f);
                float phase = Next() * 6.28f;
                FogDrift[i] = new Vector2(phase, 0.06f + Next() * 0.07f);
            }

            Ash = new Vector3[150];
            AshDrift = new Vector2[Ash.Length];
            for (int i = 0; i < Ash.Length; i++)
            {
                float x = (Next() - 0.5f) * 26f;
                float y = Next() * 15f;
                Ash[i] = new Vector3(x, y, -28f + Next() * 60f);
                float phase = Next() * 6.28f;
                AshDrift[i] = new Vector2(phase, 0.4f + Next() * 0.8f);
            }

            Embers = new Vector3[26];
            for (int i = 0; i < Embers.Length; i++)
            {
                float start = Next();
                float phase = Next() * 6.28f;
                Embers[i] = new Vector3(start, phase, 0.5f + Next());
            }

            Burst = new Vector4[46];
            for (int i = 0; i < Burst.Length; i++)
            {
                float angle = Next() * 6.28f;
                float distance = 0.4f + Next() * 2.2f;
                float rise = 0.5f + Next() * 1.2f;
                Burst[i] = new Vector4(angle, distance, rise, Next());
            }
        }

        // 시안 Geo — 세모를 모은다. 면마다 법선 하나 (각진 돌)
        private sealed class Geo
        {
            // 상자의 면 여섯 (꼭짓점 번호)
            private static readonly int[,] BoxFaces = { { 4, 5, 6, 7 }, { 1, 0, 3, 2 }, { 5, 1, 2, 6 }, { 0, 4, 7, 3 }, { 7, 6, 2, 3 }, { 0, 1, 5, 4 } };

            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Color32> Colors = new List<Color32>();
            public readonly List<Vector2> Emissive = new List<Vector2>();

            private readonly Vector3[] _corners = new Vector3[8];

            public void Add(Vector3 position, Vector3 normal, Color32 color, float emissive)
            {
                Positions.Add(position);
                Normals.Add(normal);
                Colors.Add(color);
                Emissive.Add(new Vector2(emissive, 0f));
            }

            private void Triangle(Vector3 p, Vector3 q, Vector3 r, Color32 color, float emissive)
            {
                Vector3 normal = Vector3.Cross(q - p, r - p).normalized;
                Add(p, normal, color, emissive);
                Add(q, normal, color, emissive);
                Add(r, normal, color, emissive);
            }

            private void Quad(Vector3 p, Vector3 q, Vector3 r, Vector3 s, Color32 color, float emissive)
            {
                Triangle(p, q, r, color, emissive);
                Triangle(p, r, s, color, emissive);
            }

            /// <summary>가운데 · 크기의 상자. turn = y 축으로 돌리는 각(라디안).</summary>
            public void Box(float cx, float cy, float cz, float sizeX, float sizeY, float sizeZ, Color color, float emissive = 0f, float turn = 0f)
            {
                float hx = sizeX / 2f, hy = sizeY / 2f, hz = sizeZ / 2f, cos = Mathf.Cos(turn), sin = Mathf.Sin(turn);
                Vector3 Corner(float x, float y, float z) => new Vector3(cx + x * cos + z * sin, cy + y, cz - x * sin + z * cos);

                _corners[0] = Corner(-hx, -hy, -hz);
                _corners[1] = Corner(hx, -hy, -hz);
                _corners[2] = Corner(hx, hy, -hz);
                _corners[3] = Corner(-hx, hy, -hz);
                _corners[4] = Corner(-hx, -hy, hz);
                _corners[5] = Corner(hx, -hy, hz);
                _corners[6] = Corner(hx, hy, hz);
                _corners[7] = Corner(-hx, hy, hz);
                for (int face = 0; face < 6; face++)
                    Quad(_corners[BoxFaces[face, 0]], _corners[BoxFaces[face, 1]], _corners[BoxFaces[face, 2]], _corners[BoxFaces[face, 3]], color, emissive);
            }

            /// <summary>
            /// 세로 기둥 — 아래 반지름 bottomRadius, 위 반지름 topRadius (0 이면 뿔), 옆면 sides 개. 위는 막고 아래는 열려 있다. phase = 첫 모서리의 각.
            /// </summary>
            public void Cylinder(float cx, float bottom, float cz, float bottomRadius, float topRadius, float height, int sides, Color color, float emissive = 0f, float phase = 0f)
            {
                float top = bottom + height;
                var apex = new Vector3(cx, top, cz);
                for (int i = 0; i < sides; i++)
                {
                    float a0 = phase + (float)i / sides * Tau, a1 = phase + (float)(i + 1) / sides * Tau;
                    var p0 = new Vector3(cx + Mathf.Cos(a0) * bottomRadius, bottom, cz + Mathf.Sin(a0) * bottomRadius);
                    var p1 = new Vector3(cx + Mathf.Cos(a1) * bottomRadius, bottom, cz + Mathf.Sin(a1) * bottomRadius);
                    if (topRadius <= 0f)
                    {
                        Triangle(p1, p0, apex, color, emissive);
                        continue;
                    }

                    var p2 = new Vector3(cx + Mathf.Cos(a1) * topRadius, top, cz + Mathf.Sin(a1) * topRadius);
                    var p3 = new Vector3(cx + Mathf.Cos(a0) * topRadius, top, cz + Mathf.Sin(a0) * topRadius);
                    Quad(p1, p0, p3, p2, color, emissive);
                    Triangle(apex, p2, p3, color, emissive);
                }
            }

            /// <summary>Unity 메시로 — z 를 뒤집는다 (시안은 카메라 쪽이 +z, Unity 는 카메라가 보는 쪽이 +z).</summary>
            public Mesh ToMesh(string name)
            {
                int count = Positions.Count;
                var positions = new Vector3[count];
                var normals = new Vector3[count];
                var indices = new int[count];
                for (int i = 0; i < count; i++)
                {
                    positions[i] = new Vector3(Positions[i].x, Positions[i].y, -Positions[i].z);
                    normals[i] = new Vector3(Normals[i].x, Normals[i].y, -Normals[i].z);
                    indices[i] = i;
                }

                var mesh = new Mesh { name = name, indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.vertices = positions;
                mesh.normals = normals;
                mesh.SetColors(Colors);
                mesh.SetUVs(0, Emissive);
                mesh.triangles = indices;
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
