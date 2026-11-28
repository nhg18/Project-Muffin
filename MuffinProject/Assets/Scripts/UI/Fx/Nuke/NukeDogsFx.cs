using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Chapchu.UI.Fx.Nuke
{
    /// <summary>
    /// 「대폭발」 강아지 18마리 — 시안 NUKE3D dogsAt(t) · D3R buildDogs 와 같다.
    /// 네거리를 어슬렁거리다(걷기) 사이렌에 멈칫하고(떨기), 달아나다 먼지 벽에 휩쓸려 날아가 구르고 미끄러진다.
    /// 강아지는 <see cref="FxDog"/> 부품(몸 · 꼬리 · 다리 넷)을 카메라 쪽으로 세운 판으로 그린다 — 먼 것부터 한 메시로.
    /// 발밑 둥근 그림자, 해 쪽 그림자 맵에 넣을 판(빛을 보고 선 판)도 같이 만든다.
    /// </summary>
    public class NukeDogsFx : MonoBehaviour
    {
        private const int DogCount = 18;
        private const int PartsPerDog = 6;
        private const int BodyPart = 0;
        private const int TailPart = 1;
        private const int LegPart = 2;

        private enum Pose
        {
            Stand,
            Walk,
            Shake,
            Run,
            Flail,
            Slide,
        }

        [Tooltip("강아지 판 (Chapchu/Nuke/Dog — 부품 그림 12장).")]
        [SerializeField] private Material dogMaterial;
        [Tooltip("발밑 둥근 그림자 (Chapchu/Nuke/Blob).")]
        [SerializeField] private Material blobMaterial;
        [Tooltip("강아지 높이 = 크기 × 이 값 (시안 hm).")]
        [SerializeField] private float heightPerSize = 1.05f;
        [Tooltip("미끄러질 때 몸을 이만큼(원본 px) 내린다 — 배를 땅에 댄 모습.")]
        [SerializeField] private float slideDrop = 70f;
        [Tooltip("판을 카메라 위쪽으로 이만큼 기울인다 (0 = 똑바로 세움, 1 = 화면과 나란히).")]
        [SerializeField] private float tiltToCamera = 0.4f;
        [Tooltip("달리는 강아지 발에서 이는 먼지 색.")]
        [SerializeField] private Color32 runDustColor = new Color32(196, 178, 150, 255);

        private NukeTimeline _timeline;
        private Dog[] _dogs;
        private readonly List<Sprite3> _sprites = new List<Sprite3>();
        private readonly List<Vector4> _shadows = new List<Vector4>();
        private readonly List<Vector3> _positions = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Vector2> _infos = new List<Vector2>();
        private readonly List<int> _indices = new List<int>();
        private Mesh _dogMesh;
        private Mesh _blobMesh;
        private Mesh _shadowMesh;

        private struct Dog
        {
            public int Key;
            public float X;
            public float Z;
            public float Size;
            public float Phase;
            public float Wander;
            public float NoticeAt;
            public float RunAt;
            public float VelocityX;
            public float VelocityZ;
            public float Cadence;
            public Vector3 Toss;
            public float Turn;
            public float StruckAt;
        }

        // 한 프레임의 강아지 판 (시안 SPR)
        private struct Sprite3
        {
            public int Key;
            public Vector3 Position;
            public float Size;
            public bool Flip;
            public float Rotation;
            public float StretchY;
            public Pose Pose;
            public float Phase;
            public float Depth;
        }

        private void Awake()
        {
            if (dogMaterial == null || blobMaterial == null)
                Debug.LogError($"[{nameof(NukeDogsFx)}] 참조 중 비어 있는 것이 있습니다.", this);
            _dogMesh = NewMesh("Nuke Dogs");
            _blobMesh = NewMesh("Nuke Dog Shadows");
            _shadowMesh = NewMesh("Nuke Dog Light Cards");
        }

        private void OnDestroy()
        {
            Destroy(_dogMesh);
            Destroy(_blobMesh);
            Destroy(_shadowMesh);
        }

        /// <summary>시간표를 받고, 강아지 18마리의 자리 · 성격을 씨앗으로 정한다 (시안 dogs — 난수 순서까지 같다).</summary>
        public void Setup(ref int seed, in NukeTimeline timeline)
        {
            _timeline = timeline;
            _dogs = new Dog[DogCount];
            for (int i = 0; i < DogCount; i++)
            {
                float x = NukeMath.Range(ref seed, -3.2f, 3.2f);
                float z = NukeMath.Range(ref seed, 13f, 27f);
                var dog = new Dog { Key = i % FxDog.Keys.Count, X = x, Z = z };
                dog.Size = NukeMath.Range(ref seed, 1.15f, 1.5f);
                dog.Phase = NukeMath.Range(ref seed, 0f, 6f);
                NukeMath.Next(ref seed); // 시안 hd (쓰지 않는 값)
                dog.Wander = NukeMath.Range(ref seed, 0.3f, 0.7f);
                dog.Wander *= NukeMath.Next(ref seed) < 0.5f ? 1f : -1f;
                dog.NoticeAt = timeline.Notice + NukeMath.Range(ref seed, 0f, 0.5f);
                dog.RunAt = timeline.Run + NukeMath.Range(ref seed, 0f, 0.45f) + (27f - z) * 0.012f;
                dog.VelocityX = x * 0.02f + NukeMath.Range(ref seed, -0.12f, 0.12f);
                dog.VelocityZ = -NukeMath.Range(ref seed, 6.6f, 9f);
                dog.Cadence = NukeMath.Range(ref seed, 11.5f, 14.5f);
                float tossX = NukeMath.Range(ref seed, -1f, 1f);
                float tossY = NukeMath.Range(ref seed, 7f, 12f);
                float tossZ = NukeMath.Range(ref seed, 4f, 8f);
                dog.Toss = new Vector3(tossX, tossY, tossZ);
                dog.Turn = NukeMath.Range(ref seed, -6f, 6f);
                dog.StruckAt = StruckTime(dog, timeline.Hit);
                _dogs[i] = dog;
            }
        }

        /// <summary>
        /// 장면 시각 t 의 강아지를 그린다. 달리는 강아지가 차는 먼지는 particles 에 넣고, 빛을 보는 판은 그림자 맵 명령 버퍼에 넣는다.
        /// </summary>
        public void Draw(float t, in NukeView view, CommandBuffer shadow, Material shadowMaterial, NukeParticlesFx particles)
        {
            _sprites.Clear();
            _shadows.Clear();
            Animate(t, view, particles);

            // 해 쪽 그림자 맵 — 빛을 보고 선 판 (땅 가까운 강아지만)
            Vector3 lightSide = new Vector3(-view.LightDir.z, 0f, view.LightDir.x).normalized;
            BeginMesh();
            foreach (Sprite3 sprite in _sprites)
            {
                if (sprite.Position.y < 3f)
                    AddDog(sprite, lightSide, Vector3.up, Vector3.zero);
            }

            EndMesh(_shadowMesh);
            Matrix4x4 toWorld = Matrix4x4.Translate(view.Origin);
            shadow.DrawMesh(_shadowMesh, toWorld, shadowMaterial, 0, 1);

            // 발밑 그림자
            BeginMesh();
            foreach (Vector4 s in _shadows)
                AddBlob(s);
            EndMesh(_blobMesh);
            Graphics.DrawMesh(_blobMesh, toWorld, blobMaterial, gameObject.layer, view.Camera, 0, null, false, false, false);

            // 강아지 판 — 먼 것부터
            for (int i = 0; i < _sprites.Count; i++)
            {
                Sprite3 sprite = _sprites[i];
                sprite.Depth = view.Depth(sprite.Position);
                _sprites[i] = sprite;
            }

            _sprites.RemoveAll(sprite => sprite.Depth <= -2f);
            _sprites.Sort((a, b) => b.Depth.CompareTo(a.Depth));
            Vector3 up = (view.Up * tiltToCamera + new Vector3(0f, 1f - tiltToCamera, 0f)).normalized;
            BeginMesh();
            foreach (Sprite3 sprite in _sprites)
                AddDog(sprite, view.Right, up, -view.Forward);
            EndMesh(_dogMesh);
            Graphics.DrawMesh(_dogMesh, toWorld, dogMaterial, gameObject.layer, view.Camera, 0, null, false, false, false);
        }

        /// <summary>멈칫한 강아지 머리 위 자리 (xyz, w = 멈칫한 뒤 지난 초) — 느낌표를 띄울 강아지만.</summary>
        public void CollectHeads(float t, List<Vector4> heads)
        {
            heads.Clear();
            foreach (Dog d in _dogs)
            {
                float k = t - d.NoticeAt;
                if (k < 0f || t > d.RunAt || k > 1.5f)
                    continue;
                float x = RunFromX(d);
                heads.Add(new Vector4(x, d.Size * 1.25f, d.Z, k));
            }
        }

        // 시안 dogsAt(t)
        private void Animate(float t, in NukeView view, NukeParticlesFx particles)
        {
            for (int n = 0; n < _dogs.Length; n++)
            {
                Dog d = _dogs[n];
                int i = n + 1;
                if (t < d.NoticeAt)
                {
                    // 어슬렁거리기
                    float x = d.X + Mathf.Sin(t * 0.8f + d.Phase) * d.Wander * 0.8f;
                    float dir = Mathf.Cos(t * 0.8f + d.Phase) * d.Wander > 0f ? Mathf.PI / 2f : -Mathf.PI / 2f;
                    float ph = t * 5f + d.Phase;
                    Add(d, view, new Vector3(x, Mathf.Abs(Mathf.Sin(ph)) * 0.03f, d.Z), dir, Pose.Walk, ph, 0f, 0f, 1f);
                }
                else if (t < d.RunAt)
                {
                    // 멈칫 → 하늘을 보고 떤다
                    float k = t - d.NoticeAt;
                    float hop = Mathf.Sin(Mathf.Clamp01(k / 0.22f) * Mathf.PI) * 0.35f;
                    float fear = NukeMath.Smooth((t - _timeline.Sky) / 1.2f);
                    float x = RunFromX(d) + Mathf.Sin(t * 38f) * 0.02f * fear;
                    Add(d, view, new Vector3(x, hop, d.Z), Mathf.PI + Mathf.Sin(d.Phase) * 0.4f, fear > 0.3f ? Pose.Shake : Pose.Stand, t * 14f,
                        -0.12f * NukeMath.Smooth(k / 0.3f), Mathf.Sin(t * 40f) * 0.03f * fear, 1f + hop * 0.1f - 0.05f * fear);
                }
                else
                {
                    float x0 = RunFromX(d);
                    float tt = t - d.RunAt;
                    float run = RunDistance(tt);
                    float sx = x0 + d.VelocityX * run;
                    float sz = d.Z + d.VelocityZ * run;
                    float struck = d.StruckAt;
                    if (t < struck)
                    {
                        // 달아나기 + 발에서 이는 먼지
                        float ph = t * d.Cadence + d.Phase;
                        float s = Mathf.Sin(ph - 0.6f);
                        float lift = Mathf.Max(0f, s);
                        float press = Mathf.Max(0f, -s);
                        Add(d, view, new Vector3(sx + Mathf.Sin(ph * 0.5f) * 0.06f, lift * lift * 0.27f, sz), Mathf.Atan2(d.VelocityX, d.VelocityZ), Pose.Run, ph,
                            Mathf.Cos(ph - 0.6f) * 0.1f, 0f, 1f + 0.06f * lift - 0.07f * press);
                        for (int j = 0; j < 2; j++)
                        {
                            float k = (t * 2.6f + NukeMath.Rn(i, j)) % 1f;
                            particles.Add(new Vector3(sx + (NukeMath.Rn(i, j + 3) - 0.5f) * 0.5f, 0.12f + k * 0.5f, sz + 0.5f + k * 1.5f), 0.3f + k * 0.55f, NukeMath.Rn(i, j + 5) * 6f,
                                runDustColor, 0.26f * (1f - k) * NukeMath.Smooth(k / 0.2f), 0f, NukeMath.Rn(i, j + 7));
                        }
                    }
                    else
                    {
                        // 먼지 벽에 휩쓸려 튀어 오르고 → 미끄러진다
                        float runStruck = RunDistance(struck - d.RunAt);
                        float bx = x0 + d.VelocityX * runStruck;
                        float bz = d.Z + d.VelocityZ * runStruck;
                        float k = t - struck;
                        Vector4 toss = Toss(0.4f, d.Toss.y, 13f, k);
                        float slide = Mathf.Min(k, 1.6f) - Mathf.Max(0f, Mathf.Min(k, 1.6f) - 0.9f) * 0.5f;
                        float x = bx + (d.VelocityX * 0.8f + d.Toss.x * 2f) * slide;
                        float z = bz + (d.VelocityZ * 0.9f - d.Toss.z) * slide;
                        if (toss.w < 3f)
                            Add(d, view, new Vector3(x, toss.x, z), d.Turn, Pose.Flail, t * 9f + d.Phase, d.Turn * 0.4f * toss.y, d.Turn * 0.8f * toss.y, 1f - toss.z * 0.22f);
                        else
                            Add(d, view, new Vector3(x + Mathf.Sin(t * 36f + d.Phase) * 0.02f, 0.02f, z), Mathf.PI / 2f * (d.Turn > 0f ? 1f : -1f), Pose.Slide, t * 3f, 0f, Mathf.Sin(t * 34f + d.Phase) * 0.03f, 0.94f);
                    }
                }
            }
        }

        // 시안 dog() — 판 하나와 (땅 가까우면) 발밑 그림자
        private void Add(in Dog d, in NukeView view, Vector3 position, float yaw, Pose pose, float phase, float pitch, float roll, float stretchY)
        {
            bool flip = Mathf.Sin(yaw - view.Yaw) > 0.15f;
            _sprites.Add(new Sprite3
            {
                Key = d.Key,
                Position = position,
                Size = d.Size,
                Flip = flip,
                Rotation = roll * 0.85f + (flip ? 1f : -1f) * pitch * 0.75f,
                StretchY = stretchY,
                Pose = pose,
                Phase = phase,
            });
            if (position.y < 1.4f)
                _shadows.Add(new Vector4(position.x, position.z, d.Size * 0.5f * (1f - position.y / 1.4f * 0.5f), Mathf.Min(1f, 0.72f * (1f - position.y / 1.4f * 0.6f))));
        }

        // 시안 D3R buildDogs — 꼬리 → 다리 넷 → 몸 순서로, 앞 판일수록 카메라 쪽으로 조금씩 띄운다
        private void AddDog(in Sprite3 d, Vector3 right, Vector3 up, Vector3 toward)
        {
            float height = d.Size * heightPerSize;
            float k = height / FxDog.Height;
            float drop = d.Pose == Pose.Slide ? slideDrop : 0f;
            Matrix2D baseMatrix = Matrix2D.Rotate(d.Rotation) * Matrix2D.Scale((d.Flip ? -1f : 1f) * k, k * d.StretchY) * Matrix2D.Translate(-FxDog.CenterX, -FxDog.Ground + drop);
            float offset = 0f;
            float step = height * 0.004f;
            int textureBase = d.Key * 3;

            float wag = Mathf.Sin(d.Phase * 2.2f) * (d.Pose == Pose.Stand || d.Pose == Pose.Shake ? 0.18f : 0.4f);
            Vector2 pivot = FxDog.TailPivot;
            Matrix2D tail = baseMatrix * Matrix2D.Translate(pivot.x, pivot.y) * Matrix2D.Rotate(wag) * Matrix2D.Translate(-pivot.x, -pivot.y);
            AddQuad(d.Position, tail, FxDog.Tail, textureBase + TailPart, right, up, toward * offset);
            offset += step;

            for (int i = 0; i < FxDog.Legs.Count; i++)
            {
                Vector2 at = FxDog.Legs[i];
                Matrix2D leg = baseMatrix * Matrix2D.Translate(at.x, at.y) * Matrix2D.Rotate(LegAngle(d.Pose, d.Phase, FxDog.LegFront[i], FxDog.LegFar[i], i));
                Vector4 box = new Vector4(-FxDog.LegPivot.x, -FxDog.LegPivot.y, FxDog.LegSize.x - FxDog.LegPivot.x, FxDog.LegSize.y - FxDog.LegPivot.y);
                AddQuad(d.Position, leg, box, textureBase + LegPart, right, up, toward * offset);
                offset += step;
            }

            AddQuad(d.Position, baseMatrix, FxDog.Body, textureBase + BodyPart, right, up, toward * offset);
        }

        // 원본 그림 좌표(아래로 +)의 칸을 판 위 네 점으로
        private void AddQuad(Vector3 origin, in Matrix2D matrix, Vector4 box, int texture, Vector3 right, Vector3 up, Vector3 lift)
        {
            int start = _positions.Count;
            AddCorner(origin, matrix, new Vector2(box.x, box.y), new Vector2(0f, 1f), texture, right, up, lift);
            AddCorner(origin, matrix, new Vector2(box.z, box.y), new Vector2(1f, 1f), texture, right, up, lift);
            AddCorner(origin, matrix, new Vector2(box.z, box.w), new Vector2(1f, 0f), texture, right, up, lift);
            AddCorner(origin, matrix, new Vector2(box.x, box.w), new Vector2(0f, 0f), texture, right, up, lift);
            AddQuadIndices(start);
        }

        private void AddCorner(Vector3 origin, in Matrix2D matrix, Vector2 point, Vector2 uv, int texture, Vector3 right, Vector3 up, Vector3 lift)
        {
            Vector2 local = matrix.Apply(point);
            _positions.Add(origin + right * local.x + up * -local.y + lift);
            _uvs.Add(uv);
            _infos.Add(new Vector2(texture, 1f));
        }

        // 발밑 그림자 — 납작한 타원 판 (x, z, 반지름, 알파)
        private void AddBlob(Vector4 s)
        {
            const float y = 0.03f;
            float r = s.z;
            float rz = r * 0.5f;
            int start = _positions.Count;
            _positions.Add(new Vector3(s.x - r, y, s.y - rz));
            _positions.Add(new Vector3(s.x + r, y, s.y - rz));
            _positions.Add(new Vector3(s.x + r, y, s.y + rz));
            _positions.Add(new Vector3(s.x - r, y, s.y + rz));
            _uvs.Add(new Vector2(0f, 0f));
            _uvs.Add(new Vector2(1f, 0f));
            _uvs.Add(new Vector2(1f, 1f));
            _uvs.Add(new Vector2(0f, 1f));
            for (int i = 0; i < 4; i++)
                _infos.Add(new Vector2(0f, s.w));
            AddQuadIndices(start);
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

        private void BeginMesh()
        {
            _positions.Clear();
            _uvs.Clear();
            _infos.Clear();
            _indices.Clear();
        }

        private void EndMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(_positions);
            mesh.SetUVs(0, _uvs);
            mesh.SetUVs(1, _infos);
            mesh.SetTriangles(_indices, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f);
        }

        // 시안 leg(pose, ph, lg, i) — 다리 각도
        private static float LegAngle(Pose pose, float phase, bool front, bool far, int i)
        {
            float f = front ? 1f : -1f;
            switch (pose)
            {
                case Pose.Run:
                    float p = phase + (front ? 0f : Mathf.PI) + (far ? 0.42f : 0f);
                    return (Mathf.Sin(p) + 0.24f * Mathf.Sin(2f * p)) * 0.58f + (front ? 0.1f : -0.08f);
                case Pose.Walk:
                    return Mathf.Sin(phase + (front ? 0f : Mathf.PI) + (far ? Mathf.PI : 0f)) * 0.28f;
                case Pose.Slide:
                    return f * 1.2f + Mathf.Sin(phase * 0.9f + i * 1.3f) * 0.1f;
                case Pose.Flail:
                    return Mathf.Sin(phase * 1.15f + i * 1.7f) * 0.72f + f * 0.18f;
                case Pose.Shake:
                    return Mathf.Sin(phase * 3f + i) * 0.08f;
                default:
                    return 0f;
            }
        }

        // 달아나기 시작한 자리 (멈칫한 자리)
        private static float RunFromX(in Dog d) => d.X + Mathf.Sin(d.NoticeAt * 0.8f + d.Phase) * d.Wander * 0.8f;

        // 달아나기 시작한 뒤 tt 초 동안 간 양 (처음 0.18초 몫은 속도를 붙이는 데 쓴다)
        private static float RunDistance(float tt) => tt - 0.18f * (1f - Mathf.Exp(-tt * 6f));

        // 먼지 벽(착탄점에서 36 m/s)이 달아나는 강아지를 따라잡는 때 — t = 착탄 + 그때 자리까지 거리 / 36 을 한 번 풀어 둔다
        private static float StruckTime(in Dog d, float hit)
        {
            float x0 = RunFromX(d);
            float struck = hit;
            for (int n = 0; n < 32; n++)
            {
                float run = RunDistance(struck - d.RunAt);
                float next = hit + NukeMath.Flat(x0 + d.VelocityX * run - NukeMath.Impact.x, d.Z + d.VelocityZ * run - NukeMath.Impact.z) / NukeMath.DustSpeed;
                if (Mathf.Abs(next - struck) < 1e-5f)
                    return next;
                struck = next;
            }

            return struck;
        }

        // 시안 toss — 튀어 올라 두 번 더 튄다 (높이, 돈 양, 눌림, 몇 번째 튐 — 3 이면 다 튐)
        private static Vector4 Toss(float y0, float vy, float g, float time)
        {
            float y = y0;
            float v = vy;
            float t = time;
            float spin = 0f;
            for (int n = 0; n < 3; n++)
            {
                float flight = (v + Mathf.Sqrt(v * v + 2f * g * y)) / g;
                float spinRate = n > 0 ? 0.35f : 1f;
                if (t < flight)
                    return new Vector4(y + v * t - 0.5f * g * t * t, spin + t * spinRate, n > 0 ? Mathf.Max(0f, 1f - t / 0.12f) : 0f, n);
                t -= flight;
                spin += flight * spinRate;
                y = 0f;
                v = (n > 0 ? 0.3f : 0.42f) * Mathf.Abs(v - g * flight);
            }

            return new Vector4(0f, spin, 0f, 3f);
        }

        private static Mesh NewMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        // 시안 2D 행렬 [a b c d e f] — x' = a·x + c·y + e, y' = b·x + d·y + f (원본 그림 좌표, 아래로 +)
        private readonly struct Matrix2D
        {
            private readonly float _a;
            private readonly float _b;
            private readonly float _c;
            private readonly float _d;
            private readonly float _e;
            private readonly float _f;

            private Matrix2D(float a, float b, float c, float d, float e, float f)
            {
                _a = a;
                _b = b;
                _c = c;
                _d = d;
                _e = e;
                _f = f;
            }

            public static Matrix2D Translate(float x, float y) => new Matrix2D(1f, 0f, 0f, 1f, x, y);

            public static Matrix2D Scale(float x, float y) => new Matrix2D(x, 0f, 0f, y, 0f, 0f);

            public static Matrix2D Rotate(float angle)
            {
                float c = Mathf.Cos(angle);
                float s = Mathf.Sin(angle);
                return new Matrix2D(c, s, -s, c, 0f, 0f);
            }

            public static Matrix2D operator *(Matrix2D m, Matrix2D n) => new Matrix2D(
                m._a * n._a + m._c * n._b,
                m._b * n._a + m._d * n._b,
                m._a * n._c + m._c * n._d,
                m._b * n._c + m._d * n._d,
                m._a * n._e + m._c * n._f + m._e,
                m._b * n._e + m._d * n._f + m._f);

            public Vector2 Apply(Vector2 p) => new Vector2(_a * p.x + _c * p.y + _e, _b * p.x + _d * p.y + _f);
        }
    }
}
