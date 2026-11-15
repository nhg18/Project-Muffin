using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 입자 — 시안 NUKE3D parts(t, tr) 와 같다 (최대 3000개, 먼 것부터 한 메시).
    /// 미사일 비행운(나선) · 불덩이 · 기둥 · 도넛처럼 말려 오르는 버섯 갓 · 응결 고리 · 바닥을 훑는 먼지 벽 · 무너지는 건물 먼지 · 불똥 · 폐허의 불과 연기 · 낙진(재와 초록 불티).
    /// 판은 셰이더(Chapchu/Nuke/Particle)가 카메라 쪽으로 세우고, 장면 깊이로 가리며 가장자리를 부드럽게 한다.
    /// </summary>
    public class NukeParticlesFx : MonoBehaviour
    {
        private const int MaxParticles = 3000;
        private const int ContrailCount = 260;
        private const float Tau = Mathf.PI * 2f;

        // 폐허에서 타오르는 자리 (x, z)
        private static readonly Vector2[] Fires =
        {
            new Vector2(-9f, -52f), new Vector2(10f, -40f), new Vector2(-11f, -30f), new Vector2(9f, -64f), new Vector2(-8f, -12f),
            new Vector2(12f, -4f), new Vector2(3f, -22f), new Vector2(-4f, 8f), new Vector2(-19f, -44f), new Vector2(21f, -58f),
        };

        [Tooltip("입자 머티리얼 (Chapchu/Nuke/Particle).")]
        [SerializeField] private Material particleMaterial;
        [Tooltip("도시 배치 — 무너지는 건물의 먼지 자리.")]
        [SerializeField] private NukeCityData data;

        private readonly Vector3[] _position = new Vector3[MaxParticles];
        private readonly Vector4[] _shape = new Vector4[MaxParticles];
        private readonly Color[] _color = new Color[MaxParticles];
        private readonly Vector2[] _heat = new Vector2[MaxParticles];
        private readonly float[] _depth = new float[MaxParticles];
        private readonly int[] _order = new int[MaxParticles];
        private readonly Vector3[] _contrail = new Vector3[ContrailCount];
        private readonly List<Vector3> _vertices = new List<Vector3>(MaxParticles * 4);
        private readonly List<Vector4> _corners = new List<Vector4>(MaxParticles * 4);
        private readonly List<Color> _colors = new List<Color>(MaxParticles * 4);
        private readonly List<Vector2> _heats = new List<Vector2>(MaxParticles * 4);
        private readonly List<int> _indices = new List<int>(MaxParticles * 6);
        private Mesh _mesh;
        private int _count;

        private void Awake()
        {
            if (particleMaterial == null || data == null)
                Debug.LogError($"[{nameof(NukeParticlesFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            _mesh = new Mesh { name = "Nuke Particles" };
            _mesh.MarkDynamic();
        }

        private void OnDestroy()
        {
            Destroy(_mesh);
        }

        /// <summary>비행운이 남는 자리(미사일 꽁무니가 지나간 길)를 미리 잰다.</summary>
        public void Setup()
        {
            float step = (NukeMath.Hit - NukeMath.MissileStart) / ContrailCount;
            for (int i = 0; i < ContrailCount; i++)
                _contrail[i] = NukeMath.MissileTail(NukeMath.MissileStart + i * step);
        }

        /// <summary>이번 프레임의 입자를 비운다.</summary>
        public void Clear()
        {
            _count = 0;
        }

        /// <summary>입자 하나 (시안 pt) — 열 &gt; 0 불, 열 &lt; 0 색 그대로 빛남. 다 찼거나 안 보이면 버린다.</summary>
        public void Add(Vector3 position, float size, float rotation, Color32 color, float alpha, float heat, float seed)
        {
            if (_count >= MaxParticles || alpha <= 0.004f || size <= 0f)
                return;
            _position[_count] = position;
            _shape[_count] = new Vector4(0f, 0f, size, rotation);
            _color[_count] = new Color(color.r / 255f, color.g / 255f, color.b / 255f, alpha);
            _heat[_count] = new Vector2(heat, seed);
            _count++;
        }

        /// <summary>장면 시각 t · 실제 시각 real 의 입자를 만든다 (시안 parts).</summary>
        public void Emit(float t, float real, in NukeView view)
        {
            float p = t - NukeMath.Hit;
            Vector3 impact = NukeMath.Impact;

            // 비행운 — 나선이 하늘에 남는다
            if (t > NukeMath.MissileStart)
            {
                float step = (NukeMath.Hit - NukeMath.MissileStart) / ContrailCount;
                float blow = p > 0f ? 1f - NukeMath.Smooth((p - 0.1f) / 1.3f) : 1f;
                for (int i = 0; i < ContrailCount; i++)
                {
                    float born = NukeMath.MissileStart + i * step;
                    if (born > t)
                        break;
                    float age = t - born;
                    Vector3 m = _contrail[i];
                    float tu = Mathf.Min(age, 7f);
                    float a = 0.52f * Mathf.Exp(-age / 7f) * NukeMath.Smooth(age / 0.06f) * blow;
                    Add(new Vector3(m.x + (Rn(i, 1) - 0.5f) * tu * 1.5f, m.y + (Rn(i, 2) - 0.3f) * tu, m.z + (Rn(i, 3) - 0.5f) * tu * 1.5f),
                        1.1f + 2.6f * Mathf.Sqrt(tu), Rn(i, 4) * Tau + age * 0.12f, new Color32(248, 240, 230, 255), a, 0f, Rn(i, 5));
                }
            }

            if (p <= 0f)
                return;

            float capY = 6f + 84f * NukeMath.EaseOut(p / 4.6f);
            float ringR = 9f + 25f * NukeMath.EaseOut(p / 3.6f);
            float tubeR = 6f + 11f * NukeMath.EaseOut(p / 3.6f);
            float heatFade = Mathf.Exp(-p * 0.36f);

            // 불덩이
            for (int i = 0; i < 80; i++)
            {
                float a = Rn(i, 1) * Tau;
                float e = Rn(i, 2) * 1.45f;
                float r0 = (8f + 22f * NukeMath.EaseOut(p / 0.6f)) * (0.45f + 0.55f * Rn(i, 3)) + p * 2.5f;
                float y = 2f + Mathf.Sin(e) * r0 * 0.85f + p * p * 2.2f + p * 5f;
                Add(new Vector3(impact.x + Mathf.Cos(a) * Mathf.Cos(e) * r0, Mathf.Min(y, capY + tubeR), impact.z + Mathf.Sin(a) * Mathf.Cos(e) * r0),
                    8f + 9f * NukeMath.EaseOut(p / 0.8f) * (0.7f + 0.6f * Rn(i, 4)), Rn(i, 5) * Tau + p * 0.4f, new Color32(84, 66, 58, 255),
                    0.9f * (1f - NukeMath.Smooth((p - 2.6f) / 1.6f)), 1.7f * Mathf.Exp(-p * 0.75f), Rn(i, 6));
            }

            // 기둥
            for (int i = 0; i < 100; i++)
            {
                float h = Rn(i, 7);
                float ang = Rn(i, 8) * Tau + p * (0.5f + Rn(i, 9)) * 0.7f;
                float rad = (4.5f + 3.2f * Rn(i, 10)) * (1f + 1.6f * Mathf.Pow(1f - h, 4f)) * NukeMath.Smooth(p / 0.3f);
                Add(new Vector3(impact.x + Mathf.Cos(ang) * rad, 1f + h * capY * 0.98f, impact.z + Mathf.Sin(ang) * rad), 6f + 3.6f * Rn(i, 11), Rn(i, 12) * Tau + p * 0.5f,
                    new Color32(92, 74, 64, 255), 0.86f * NukeMath.Smooth((p - h * 0.6f) / 0.3f), 0.8f * heatFade * (1f - h * 0.55f), Rn(i, 13));
            }

            // 갓 — 도넛처럼 말려 올라간다
            for (int i = 0; i < 250; i++)
            {
                float ph = i / 250f * Tau + Rn(i, 14) * 0.3f;
                float th = Rn(i, 15) * Tau + p * 1.25f;
                float rm = tubeR * (0.6f + 0.55f * Rn(i, 16));
                float cr = ringR + rm * Mathf.Cos(th);
                float under = Mathf.Max(0f, -Mathf.Sin(th));
                float g = Rn(i, 19);
                Add(new Vector3(impact.x + Mathf.Cos(ph) * cr, capY + Mathf.Sin(th) * rm * 0.82f, impact.z + Mathf.Sin(ph) * cr), (7.5f + 6f * Rn(i, 17)) * (0.5f + 0.5f * NukeMath.EaseOut(p / 1.2f)),
                    Rn(i, 18) * Tau + p * 0.6f, Rgb(108f - 30f * g, 88f - 24f * g, 76f - 20f * g), 0.9f * NukeMath.Smooth(p / 0.4f),
                    heatFade * (0.2f + 0.95f * under) + 0.3f * under * NukeMath.Smooth((p - 1.5f) / 2f), Rn(i, 20));
            }

            for (int i = 0; i < 60; i++)
            {
                float a = Rn(i, 21) * Tau;
                float e = 0.25f + Rn(i, 22) * 1.3f;
                float r = ringR * 0.95f * Mathf.Cos(e) * (0.3f + 0.7f * Rn(i, 23));
                Add(new Vector3(impact.x + Mathf.Cos(a) * r, capY + tubeR * 0.35f + Mathf.Sin(e) * tubeR * 1.05f, impact.z + Mathf.Sin(a) * r), (9f + 6f * Rn(i, 24)) * (0.5f + 0.5f * NukeMath.EaseOut(p / 1.2f)),
                    Rn(i, 25) * Tau, new Color32(124, 104, 90, 255), 0.9f * NukeMath.Smooth(p / 0.5f), heatFade * 0.3f, Rn(i, 26));
            }

            // 응결 고리
            if (p > 0.15f && p < 2.6f)
            {
                float u = (p - 0.15f) / 2.45f;
                for (int i = 0; i < 70; i++)
                {
                    float a = i / 70f * Tau;
                    float r = 16f + 66f * NukeMath.EaseOut(u);
                    Add(new Vector3(impact.x + Mathf.Cos(a) * r, capY * 0.64f + Mathf.Sin(a * 5f) * 1.5f, impact.z + Mathf.Sin(a) * r), 5f + 5f * Rn(i, 27), Rn(i, 28) * 6f,
                        new Color32(255, 250, 244, 255), 0.34f * Mathf.Sin(Mathf.PI * u), 0f, Rn(i, 29));
                }
            }

            // 바닥을 훑는 먼지 벽
            for (int i = 0; i < 340; i++)
            {
                float a = i / 340f * Tau + Rn(i, 30) * 0.03f;
                float lag = Mathf.Pow(Rn(i, 31), 1.5f) * 40f;
                float r = NukeMath.DustSpeed * p - lag;
                if (r < 5f || r > 190f)
                    continue;
                float h = Rn(i, 32);
                float g = Rn(i, 33);
                Add(new Vector3(impact.x + Mathf.Cos(a) * r, 1.5f + h * h * (10f + 24f * NukeMath.Smooth(p / 1.5f)), impact.z + Mathf.Sin(a) * r), (6f + 8f * Rn(i, 34)) * (0.55f + 0.45f * NukeMath.Smooth(p / 0.6f)),
                    Rn(i, 35) * Tau + p * 0.3f, Rgb(150f - 40f * g, 128f - 34f * g, 104f - 28f * g),
                    0.8f * (1f - lag / 40f * 0.7f) * (1f - NukeMath.Smooth((r - 140f) / 50f)) * (1f - NukeMath.Smooth((p - 3.6f) / 1.8f) * 0.7f),
                    0.5f * Mathf.Exp(-lag / 7f) * (1f - h) * Mathf.Exp(-p * 0.45f), Rn(i, 36));
            }

            for (int i = 0; i < 56; i++)
            {
                float a = Rn(i, 37) * Tau;
                float r = 10f + Rn(i, 38) * 26f * NukeMath.EaseOut(p / 2.2f);
                Add(new Vector3(impact.x + Mathf.Cos(a) * r, 1.5f + Rn(i, 39) * 6f, impact.z + Mathf.Sin(a) * r), 8f + 4f * Rn(i, 40), Rn(i, 41) * 6f,
                    new Color32(96, 80, 70, 255), 0.7f * NukeMath.Smooth(p / 0.4f), heatFade * 0.5f, Rn(i, 42));
            }

            // 무너지는 건물의 먼지
            NukeCityData.Building[] buildings = data.buildings;
            for (int n = 0; n < buildings.Length; n++)
            {
                NukeCityData.Building b = buildings[n];
                int bi = n + 1;
                if (b.Dist < 34f || b.Dist >= 82f)
                    continue;
                float k = p - b.Arrive;
                if (k <= 0f || k > 4f)
                    continue;
                for (int j = 0; j < 6; j++)
                {
                    float a = Rn(bi, j) * Tau;
                    float r = b.Width * 0.5f * (0.5f + k * 0.7f);
                    float g = Rn(bi, j + 9);
                    Add(new Vector3(b.X + Mathf.Cos(a) * r, 1.5f + j * 2.2f * NukeMath.EaseOut(k / 1.6f) + Rn(bi, j + 20) * 2f, b.Z + Mathf.Sin(a) * r), 4.5f + 2.6f * k, Rn(bi, j + 30) * 6f + k * 0.3f,
                        Rgb(160f - 36f * g, 142f - 32f * g, 122f - 28f * g), 0.62f * (1f - k / 4f) * NukeMath.Smooth(k / 0.25f), 0f, Rn(bi, j + 40));
                }
            }

            // 불똥
            if (p < 3.4f)
            {
                for (int i = 0; i < 120; i++)
                {
                    float a = Rn(i, 43) * Tau;
                    float e = 0.15f + Rn(i, 44) * 1.2f;
                    float v = 22f + Rn(i, 45) * 46f;
                    float y = 3f + Mathf.Sin(e) * v * p - 5.5f * p * p;
                    if (y < 0f)
                        continue;
                    Add(new Vector3(impact.x + Mathf.Cos(a) * Mathf.Cos(e) * v * p, y, impact.z + Mathf.Sin(a) * Mathf.Cos(e) * v * p), 0.45f + 0.6f * Rn(i, 46), 0f,
                        new Color32(255, 170, 70, 255), 0.95f * (1f - p / 3.4f), -5f, 0f);
                }
            }

            // 폐허의 불 · 연기
            if (p > 1.9f)
            {
                float k0 = NukeMath.Smooth((p - 1.9f) / 0.8f);
                for (int j = 0; j < Fires.Length; j++)
                {
                    Vector2 f = Fires[j];
                    for (int i = 0; i < 5; i++)
                    {
                        float ph = (real * (0.8f + Rn(j, i) * 0.5f) + Rn(j, i + 5)) % 1f;
                        Add(new Vector3(f.x + (Rn(j, i + 10) - 0.5f) * 1.6f, 0.6f + ph * 3.6f, f.y + (Rn(j, i + 15) - 0.5f) * 1.6f), (1.5f - ph * 0.8f) * k0, Rn(j, i + 20) * 6f + real,
                            new Color32(90, 60, 40, 255), 0.9f * (1f - ph) * k0, 0.72f * (1f - ph * 0.8f), Rn(j, i + 25));
                    }

                    for (int i = 0; i < 5; i++)
                    {
                        float ph = (real * 0.22f + Rn(j, i + 30)) % 1f;
                        Add(new Vector3(f.x + ph * ph * 3f + (Rn(j, i + 35) - 0.5f), 3.5f + ph * 17f, f.y - ph * 2f), (2.2f + ph * 4.5f) * k0, Rn(j, i + 40) * 6f + real * 0.2f,
                            new Color32(52, 46, 42, 255), 0.6f * (1f - ph) * NukeMath.Smooth(ph / 0.1f) * k0, 0f, Rn(j, i + 45));
                    }
                }
            }

            // 낙진 — 재와 초록 불티가 카메라 둘레로 내린다
            float gk = NukeMath.Smooth((t - NukeMath.Fall + 0.3f) / 1.2f);
            if (gk <= 0f)
                return;
            Vector3 eye = view.Eye;
            for (int i = 0; i < 230; i++)
            {
                float x = eye.x + Wrap(Rn(i, 47) * 44f + Mathf.Sin(real * 0.6f + i) * 0.8f, 44f) - 22f;
                float y = Wrap(Rn(i, 48) * 26f - real * (0.7f + Rn(i, 49) * 0.9f), 26f) - 2f + eye.y * 0.5f;
                float z = eye.z + Wrap(Rn(i, 50) * 44f + real * 0.5f, 44f) - 6f;
                if (i % 3 != 0)
                    Add(new Vector3(x, y, z), 0.07f + Rn(i, 51) * 0.1f, 0f, new Color32(170, 255, 96, 255), 0.9f * gk, -(1.6f + 1.6f * Rn(i, 52)) * (0.7f + 0.3f * Mathf.Sin(real * 5f + i)), 0f);
                else
                    Add(new Vector3(x, y, z), 0.1f + Rn(i, 51) * 0.12f, Rn(i, 53) * 6f, new Color32(150, 146, 140, 255), 0.75f * gk, 0f, Rn(i, 54));
            }
        }

        /// <summary>모은 입자를 먼 것부터 한 메시로 그린다.</summary>
        public void Draw(in NukeView view)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                float depth = view.Depth(_position[i]);
                if (depth <= -2f)
                    continue;
                _depth[n] = -depth;
                _order[n] = i;
                n++;
            }

            Array.Sort(_depth, _order, 0, n);
            _vertices.Clear();
            _corners.Clear();
            _colors.Clear();
            _heats.Clear();
            _indices.Clear();
            for (int j = 0; j < n; j++)
            {
                int i = _order[j];
                Vector3 position = view.Origin + _position[i];
                Vector4 shape = _shape[i];
                for (int q = 0; q < 4; q++)
                {
                    _vertices.Add(position);
                    _corners.Add(new Vector4(q == 1 || q == 2 ? 1f : -1f, q >= 2 ? 1f : -1f, shape.z, shape.w));
                    _colors.Add(_color[i]);
                    _heats.Add(_heat[i]);
                }

                int start = j * 4;
                _indices.Add(start);
                _indices.Add(start + 1);
                _indices.Add(start + 2);
                _indices.Add(start);
                _indices.Add(start + 2);
                _indices.Add(start + 3);
            }

            _mesh.Clear();
            if (n == 0)
                return;
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _corners);
            _mesh.SetUVs(1, _heats);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_indices, 0, false);
            _mesh.bounds = new Bounds(view.Origin, Vector3.one * 4000f);
            Graphics.DrawMesh(_mesh, Matrix4x4.identity, particleMaterial, gameObject.layer, view.Camera, 0, null, false, false, false);
        }

        private static float Rn(int i, int k) => NukeMath.Rn(i, k);

        private static float Wrap(float v, float m) => (v % m + m) % m;

        private static Color32 Rgb(float r, float g, float b) => new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(r), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(g), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(b), 0, 255), 255);
    }
}
