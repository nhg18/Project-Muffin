using UnityEngine;
using UnityEngine.Rendering;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대소동」 도시 · 미사일 · 불덩이 · 잔해를 장면 카메라에만 그린다 — 시안 NUKE3D meshes(t, tr) 와 같다.
    /// 착탄 뒤에는 먼지 벽(36 m/s)이 닿는 차례로 건물이 기울고 주저앉고, 나무 · 가로등 · 소품이 휘고, 차가 날아간다.
    /// 그림자를 드리우는 것은 그림자 맵 명령 버퍼에도 같이 넣는다.
    /// </summary>
    public class NukeCityFx : MonoBehaviour
    {
        private const int ChunkCount = 54;
        private const int NearChunks = 30;

        private static readonly int MpAId = Shader.PropertyToID("_MpA");
        private static readonly int MpBId = Shader.PropertyToID("_MpB");
        private static readonly int EmiSelfId = Shader.PropertyToID("_EmiSelf");

        [Tooltip("도시 배치 · 메시.")]
        [SerializeField] private NukeCityData data;
        [Tooltip("보통 물체 (재질 0).")]
        [SerializeField] private Material objectMaterial;
        [Tooltip("땅 — 길 · 차선 (재질 3).")]
        [SerializeField] private Material groundMaterial;
        [Tooltip("보도 판 (재질 4).")]
        [SerializeField] private Material paveMaterial;
        [Tooltip("소품 크기 — propMeshes 순서 (소화전 · 쓰레기통 · 벤치 · 우체통 · 표지판 · 정류장).")]
        [SerializeField] private float[] propScales = { 0.7f, 0.9f, 1.2f, 1f, 1.6f, 2.2f };
        [Tooltip("가로등 · 신호등 크기.")]
        [SerializeField] private Vector2 lampSignalScale = new Vector2(4.6f, 4.2f);
        [Tooltip("버스 · 다른 차 크기.")]
        [SerializeField] private Vector2 busCarScale = new Vector2(3.2f, 1.3f);
        [Tooltip("미사일 크기.")]
        [SerializeField] private float missileScale = 7.5f;
        [Tooltip("10번 장면(도시 전경)에서 카메라와 이 거리(m) 안의 건물은 그리지 않는다 — 전경을 가리지 않게.")]
        [SerializeField] private float wideShotClear = 175f;

        private MaterialPropertyBlock _block;
        private Chunk[] _chunks;
        private NukeView _view;
        private CommandBuffer _shadow;
        private Material _shadowMaterial;

        private struct Chunk
        {
            public int Mesh;
            public Vector3 Dir;
            public float Speed;
            public float Scale;
            public float Spin;
            public float SpinSpeed;
        }

        private void Awake()
        {
            if (data == null || objectMaterial == null || groundMaterial == null || paveMaterial == null)
                Debug.LogError($"[{nameof(NukeCityFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            _block = new MaterialPropertyBlock();
        }

        /// <summary>착탄 때 튀는 잔해 54개를 씨앗으로 정한다 (시안 chunks — 강아지 다음 난수).</summary>
        public void Setup(ref int seed)
        {
            _chunks = new Chunk[ChunkCount];
            for (int i = 0; i < ChunkCount; i++)
            {
                float a = NukeMath.Range(ref seed, 0f, Mathf.PI * 2f);
                float e = NukeMath.Range(ref seed, 0.25f, 1.25f);
                bool far = i > NearChunks;
                _chunks[i] = new Chunk
                {
                    Mesh = i & 1,
                    Dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * Mathf.Cos(e)),
                    Speed = far ? NukeMath.Range(ref seed, 30f, 58f) : NukeMath.Range(ref seed, 14f, 34f),
                    Scale = NukeMath.Range(ref seed, 0.5f, far ? 1.6f : 3.2f),
                    Spin = NukeMath.Range(ref seed, 0f, 6f),
                    SpinSpeed = NukeMath.Range(ref seed, -7f, 7f),
                };
            }
        }

        /// <summary>장면 시각 t · 실제 시각 real 의 도시를 그린다. 그림자를 드리우는 것은 shadow 에도 넣는다.</summary>
        public void Draw(float t, float real, in NukeView view, CommandBuffer shadow, Material shadowMaterial)
        {
            _view = view;
            _shadow = shadow;
            _shadowMaterial = shadowMaterial;
            float p = t - NukeMath.Hit;
            Vector3 impact = NukeMath.Impact;

            Put(data.ground, groundMaterial, new Vector3(0f, 0f, 30f), 0f, 0f, 0f, 560f, 1f, false);
            foreach (Vector2 b in data.blocks)
                Put(data.block, paveMaterial, new Vector3(b.x, 0f, b.y), 0f, 0f, 0f, 1f, 1f, true);

            DrawBuildings(p, real);
            DrawStreet(p, real);
            DrawCars(t, p);

            // 미사일 · 꽁무니 불꽃
            if (t > NukeMath.MissileStart && p < 0f)
            {
                Vector3 m = NukeMath.MissilePos(t);
                Vector3 d = NukeMath.MissileDir(t);
                float pitch = -Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f));
                float yaw = Mathf.Atan2(d.x, d.z);
                Put(data.missile, objectMaterial, m, yaw, pitch, t * 7f, missileScale, 1f, true);
                float flame = missileScale * (1f + 0.04f * Mathf.Sin(real * 23f));
                Put(data.flame, objectMaterial, m - d * missileScale * 0.98f, yaw, pitch, t * 13f, flame, 1f + 0.06f * Mathf.Sin(real * 17f), false, 2.4f);
            }

            if (p < 0f)
                return;

            // 불덩이 · 그을린 자리 · 튀는 잔해
            float fireball = NukeMath.EaseOut(p / 0.22f) * (1f - NukeMath.Smooth((p - 0.25f) / 1f));
            if (fireball > 0.02f)
                Put(data.fireball, objectMaterial, new Vector3(impact.x, 4f + p * 10f, impact.z), 0f, 0f, 0f, 24f * fireball, 1f, false, 14f * (1f - NukeMath.Smooth((p - 0.3f) / 0.9f)) + 2f);
            Put(data.scorch, objectMaterial, new Vector3(impact.x, 0.07f, impact.z), 0f, 0f, 0f, 22f * NukeMath.EaseOut(p / 0.5f), 1f, false);
            foreach (Chunk c in _chunks)
            {
                float y = 2f + c.Dir.y * c.Speed * p - 9.8f * p * p * 0.5f;
                if (y < 0f)
                    continue;
                float rot = c.Spin + c.SpinSpeed * p;
                Put(data.chunkMeshes[c.Mesh], objectMaterial, new Vector3(impact.x + c.Dir.x * c.Speed * p * 1.3f, y, impact.z + c.Dir.z * c.Speed * p * 1.3f), rot, rot * 0.7f, rot * 1.3f, c.Scale, 1f, false);
            }
        }

        // 건물 — 착탄점 가까이부터 먼지 벽이 닿는 차례로 무너진다
        private void DrawBuildings(float p, float real)
        {
            foreach (NukeCityData.Building b in data.buildings)
            {
                float x = b.X;
                float y = 0f;
                float pitch = 0f;
                float roll = 0f;
                float sy = 1f;
                if (_view.Shot == 10 && NukeMath.Flat(b.X - _view.Eye.x, b.Z - _view.Eye.z) < wideShotClear)
                    continue;

                if (p > 0f && !b.Far)
                {
                    float k = p - b.Arrive;
                    float lean = b.Away - b.Yaw;
                    float fast = p - b.Dist / 95f;
                    if (b.Dist < 34f)
                    {
                        float v = NukeMath.Smooth(p / 0.55f);
                        if (v >= 1f)
                            continue;
                        sy = 1f - v * 0.96f;
                        x += Mathf.Sin(real * 70f + b.X) * 0.3f * (1f - v);
                    }
                    else if (b.Dist < 82f)
                    {
                        if (k > 0f)
                        {
                            float u = NukeMath.EaseOut(k / (1.5f + b.R1));
                            float tip = u * (0.3f + b.R2 * 0.45f);
                            pitch = Mathf.Cos(lean) * tip;
                            roll = -Mathf.Sin(lean) * tip;
                            sy = 1f - u * (0.4f + b.R1 * 0.34f);
                            y = -u * 2f;
                            x += Mathf.Sin(real * 60f + b.Z) * 0.2f * (1f - u);
                        }
                        else if (fast > 0f)
                        {
                            x += Mathf.Sin(real * 64f + b.Z) * 0.07f;
                        }
                    }
                    else if (b.Dist < 150f && k > 0f)
                    {
                        float u = NukeMath.EaseOut(k / 1.6f);
                        float tip = u * (0.03f + b.R2 * 0.13f) * (1f - (b.Dist - 82f) / 90f);
                        pitch = Mathf.Cos(lean) * tip;
                        roll = -Mathf.Sin(lean) * tip;
                        y = -u * 0.5f;
                        x += Mathf.Sin(real * 58f + b.Z) * 0.12f * (1f - u);
                    }
                }

                _block.Clear();
                _block.SetVector(MpAId, b.MpA);
                _block.SetVector(MpBId, b.MpB);
                _block.SetFloat(EmiSelfId, -1f);
                Matrix4x4 matrix = Matrix(new Vector3(x, y, b.Z), b.Yaw, pitch, roll, 1f, sy);
                Graphics.DrawMesh(data.buildingMeshes[b.Variant], matrix, objectMaterial, gameObject.layer, _view.Camera, 0, _block, false, false, false);
                if (!b.Far)
                    _shadow.DrawMesh(data.buildingMeshes[b.Variant], matrix, _shadowMaterial, 0, 0);

                // 무너진 자리의 잔해 더미
                if (p > 0f && b.Dist >= 34f && b.Dist < 82f && p - b.Arrive > 0.5f)
                    Put(data.rubble, objectMaterial, new Vector3(b.X + Mathf.Sin(b.Away) * 3f, 0f, b.Z + Mathf.Cos(b.Away) * 3f), b.R1 * 6f, 0f, 0f, b.Width * 0.55f * NukeMath.EaseOut((p - b.Arrive - 0.5f) / 1.2f), 1f, false);
            }
        }

        // 나무 · 가로등 · 신호등 · 소품 — 먼지 벽이 지나가면 착탄점 반대쪽으로 휜다
        private void DrawStreet(float p, float real)
        {
            foreach (NukeCityData.Prop tree in data.trees)
            {
                Bend(p, tree.X, tree.Z, out float k, out float a, out float d);
                if (d < 30f && p > 0.2f)
                    continue;
                float sway = Mathf.Sin(real * 1.7f + tree.X + tree.Z) * 0.02f;
                Put(data.treeMeshes[tree.Mesh], objectMaterial, new Vector3(tree.X, 0.2f, tree.Z), tree.Yaw, Mathf.Cos(a - tree.Yaw) * k * 1.25f + sway, -Mathf.Sin(a - tree.Yaw) * k * 1.25f + sway * 0.6f, tree.Scale * 2.2f, 1f, true);
            }

            foreach (NukeCityData.Prop lamp in data.lamps)
            {
                Bend(p, lamp.X, lamp.Z, out float k, out float a, out float d);
                if (d < 26f && p > 0.2f)
                    continue;
                float lean = a - lamp.Yaw;
                Put(data.lamp, objectMaterial, new Vector3(lamp.X, 0.2f, lamp.Z), lamp.Yaw, Mathf.Cos(lean) * k * 1.1f, -Mathf.Sin(lean) * k * 1.1f, lampSignalScale.x, 1f, true);
            }

            foreach (NukeCityData.Prop signal in data.signals)
            {
                Bend(p, signal.X, signal.Z, out float k, out float a, out float d);
                if (d < 26f && p > 0.2f)
                    continue;
                float lean = a - signal.Yaw;
                Put(data.signal, objectMaterial, new Vector3(signal.X, 0.2f, signal.Z), signal.Yaw, Mathf.Cos(lean) * k, -Mathf.Sin(lean) * k, lampSignalScale.y, 1f, true);
            }

            foreach (NukeCityData.Prop prop in data.props)
            {
                Bend(p, prop.X, prop.Z, out float k, out float a, out float d);
                if (d < 40f && p > 0.2f)
                    continue;
                float lean = a - prop.Yaw;
                Put(data.propMeshes[prop.Mesh], objectMaterial, new Vector3(prop.X + Mathf.Sin(a) * k * 6f, 0.2f, prop.Z + Mathf.Cos(a) * k * 6f), prop.Yaw, Mathf.Cos(lean) * k * 1.5f, -Mathf.Sin(lean) * k * 1.5f, propScales[prop.Mesh], 1f, true);
            }
        }

        // 차 — 사이렌에 달리던 차는 선다. 먼지 벽이 닿으면 날아가 구르고 뒤집힌다
        private void DrawCars(float t, float p)
        {
            foreach (NukeCityData.Car car in data.cars)
                PutCar(car, p, car.X, car.Z, car.Yaw);

            float stopStart = NukeMath.Siren + 0.15f;
            float stopEnd = stopStart + 0.9f;
            float tt = Mathf.Min(t, stopEnd);
            float run = tt < stopStart ? tt : stopStart + (tt - stopStart) - (tt - stopStart) * (tt - stopStart) / (2f * (stopEnd - stopStart));
            foreach (NukeCityData.Car mover in data.movers)
                PutCar(mover, p, mover.X + mover.Speed * run, mover.Z, mover.Speed > 0f ? Mathf.PI / 2f : -Mathf.PI / 2f);
        }

        private void PutCar(in NukeCityData.Car car, float p, float x, float z, float yaw)
        {
            Mesh mesh = data.carMeshes[car.Mesh];
            bool bus = car.Mesh == data.carMeshes.Length - 1;
            float scale = bus ? busCarScale.x : busCarScale.y;
            float d = NukeMath.Flat(x -NukeMath.Impact.x, z - NukeMath.Impact.z);
            float k = p - d / NukeMath.DustSpeed;
            if (k <= 0f || p <= 0f)
            {
                Put(mesh, objectMaterial, new Vector3(x, 0f, z), yaw, 0f, 0f, scale, 1f, true);
                return;
            }

            if (d < 26f)
                return;
            float power = Mathf.Clamp(1.4f - d / 90f, 0.12f, 1f);
            float a = Mathf.Atan2(x - NukeMath.Impact.x, z - NukeMath.Impact.z);
            float vy = (7f + car.Rand.x * 9f) * power;
            float flight = vy / 5.5f;
            float kk = Mathf.Min(k, flight);
            float height = Mathf.Max(0f, vy * kk - 5.5f * kk * kk);
            float after = Mathf.Min(1f, Mathf.Max(0f, k - flight));
            float distance = (10f + car.Rand.y * 14f) * power * (kk + after * 0.5f * (1f - 0.5f * after));
            bool landed = k >= flight;
            bool upsideDown = car.Rand.z > 0.5f;
            float landY = landed ? (upsideDown ? 1.35f : 0.9f) : 0f;
            float pitch = landed ? (upsideDown ? Mathf.PI : Mathf.PI / 2f) : car.Spin.y * kk * power;
            Put(mesh, objectMaterial, new Vector3(x + Mathf.Sin(a) * distance, height + landY, z + Mathf.Cos(a) * distance), yaw + car.Spin.x * kk * power, pitch, car.Spin.z * kk * power, scale, 1f, true);
        }

        // 시안 bend — 먼지 벽이 지나간 정도(k) · 착탄점에서 본 방향(a) · 거리(d)
        private static void Bend(float p, float x, float z, out float k, out float a, out float d)
        {
            if (p <= 0f)
            {
                k = 0f;
                a = 0f;
                d = float.MaxValue;
                return;
            }

            d = NukeMath.Flat(x -NukeMath.Impact.x, z - NukeMath.Impact.z);
            k = NukeMath.EaseOut((p - d / NukeMath.DustSpeed) / 0.4f) * Mathf.Clamp01(1.5f - d / 120f);
            a = Mathf.Atan2(x - NukeMath.Impact.x, z - NukeMath.Impact.z);
        }

        // 물체 하나를 장면 카메라에 그린다 (emission 음수 = 장면의 발광 값)
        private void Put(Mesh mesh, Material material, Vector3 position, float yaw, float pitch, float roll, float scale, float stretchY, bool castShadow, float emission = -1f)
        {
            if (scale <= 0f)
                return;
            Matrix4x4 matrix = Matrix(position, yaw, pitch, roll, scale, stretchY);
            _block.Clear();
            _block.SetVector(MpAId, Vector4.zero);
            _block.SetVector(MpBId, Vector4.zero);
            _block.SetFloat(EmiSelfId, emission);
            Graphics.DrawMesh(mesh, matrix, material, gameObject.layer, _view.Camera, 0, _block, false, false, false);
            if (castShadow)
                _shadow.DrawMesh(mesh, matrix, _shadowMaterial, 0, 0);
        }

        // 시안 T · RY · RX · RZ · S
        private Matrix4x4 Matrix(Vector3 position, float yaw, float pitch, float roll, float scale, float stretchY)
        {
            Quaternion rotation = Quaternion.Euler(pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, roll * Mathf.Rad2Deg);
            return Matrix4x4.TRS(_view.Origin + position, rotation, new Vector3(scale, scale * stretchY, scale));
        }
    }
}
