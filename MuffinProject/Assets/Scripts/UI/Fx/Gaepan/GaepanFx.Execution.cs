using System.Collections;
using System.Collections.Generic;
using Chapchu.Game;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    // GaepanFx.Execution.cs — 처형 장면 (시안 DOGPILE.play({mode:"exec"})): 5 바퀴 뒤 판정 팻말 다음에 강아지 뭉치가 꼴찌를 덮친다 (10-ui 5-1 「개판 처형 장면」).
    // 번쩍 → 돌진 → 썰매(3 단계와 같은 장면, 시간만 다르다) → 언덕(바위 위 꼴찌에게 눈사태) → 올려다본 하늘 → 뭉치(쏟아지는 강아지 · 눈더미) → 터짐 + 「탈락!」.
    // 장면 값은 시안 DOGPILE.SC.hill · sky · clump · burst 와 같다 (3D 판). 시안의 빛기둥 · 눈가루 안개는 없다 — 눈 마을은 불투명한 눈뭉치만 그린다.
    public partial class GaepanFx
    {
        // 시안 hill: 장면의 이만큼에서 눈사태가 바위를 덮친다
        private const float AvalancheAt = 0.55f;
        private const int AvalanchePuffs = 14;
        // 시안 snowDog · burst · pop: 눈더미 겉의 눈덩이 수, 터질 때 둥글게 퍼지는 눈뭉치 수, 강아지 하나가 떨어질 때 튀는 알갱이 수
        private const int HeapClods = 20, BurstPuffs = 12, PopGrains = 14;
        private const float BurstPuffRadius = 0.85f;
        // 한꺼번에 떨어지는 강아지 수 (알갱이 풀 크기)
        private const int PopsAtOnce = 6;
        // 시안 CEN: 뭉치 가운데 깊이
        private const float ClumpDepth = 10f;
        // 시안 sky: 장면의 이만큼까지 솟고, 그 뒤는 내리꽂는다
        private const float DiveAt = 0.45f;
        private const string ExecutionSoundSuffix = "_Exec";
        // 방장 시간과의 허용 차 (초)
        private const float DurationTolerance = 0.01f;
        // 시안 hill: 바위 자리 (x, 꼴찌가 선 높이, z) 와 바위 셋 (dx, dz, 반지름)
        private static readonly Vector3 RockSpot = new Vector3(3f, 1.18f, 18f);
        private static readonly Vector3[] RockPieces = { new Vector3(-0.7f, 0.25f, 0.85f), new Vector3(0.6f, 0.35f, 0.75f), new Vector3(0f, -0.25f, 1f) };

        [Header("처형 장면 (시안 DOGPILE.PLAN.exec, 초)")]
        [Tooltip("「탈락!」 알약.")]
        [SerializeField] private GaepanOutSign outSign;
        [Tooltip("무리 돌진이 시작되는 시각.")]
        [SerializeField] private float execChargeStart = 0.35f;
        [Tooltip("썰매가 시작되는 시각.")]
        [SerializeField] private float execSlideStart = 1.5f;
        [Tooltip("언덕(바위 위 꼴찌에게 눈사태)이 시작되는 시각.")]
        [SerializeField] private float hillStart = 2.4f;
        [Tooltip("올려다본 하늘이 시작되는 시각.")]
        [SerializeField] private float skyStart = 3.3f;
        [Tooltip("뭉치(쏟아지는 강아지 · 눈더미)가 시작되는 시각.")]
        [SerializeField] private float clumpStart = 4.4f;
        [Tooltip("터짐이 시작되는 시각.")]
        [SerializeField] private float burstStart = 5.4f;
        [Tooltip("처형 장면 길이 — 방장의 GameServer.GaepanExecutionSeconds 와 같아야 한다 (끝나면 꼴찌의 HP 가 0 이 된다).")]
        [SerializeField] private float execDuration = 6.75f;
        [Tooltip("「탈락!」 이 찍히는 시각.")]
        [SerializeField] private float outAt = 5.75f;

        [Header("처형 장면 강아지 · 눈 (시안 mk)")]
        [Tooltip("언덕을 달려 오르는 수 · 눈사태에 튕겨 나는 수.")]
        [SerializeField] private Vector2Int hillCounts = new Vector2Int(10, 10);
        [Tooltip("하늘을 나는 수.")]
        [SerializeField] private int flyerCount = 18;
        [Tooltip("꼴찌 위로 쏟아지는 수 · 터질 때 튕겨 나는 수.")]
        [SerializeField] private Vector2Int clumpCounts = new Vector2Int(16, 10);
        [Tooltip("터질 때 날리는 눈 알갱이 수 · 눈 조각 수.")]
        [SerializeField] private Vector2Int burstCounts = new Vector2Int(120, 40);
        [Tooltip("눈 알갱이 크기 배수 — 시안의 점 입자를 눈덩이 메시로 그려서 조금 키운다.")]
        [SerializeField] private float grainScale = 1.8f;
        [Tooltip("꼴찌 크기 — 바위 위 · 뭉치 가운데 · 터진 뒤.")]
        [SerializeField] private Vector3 victimSize = new Vector3(1.55f, 1.3f, 1.5f);
        [Tooltip("하늘에서 내리꽂는 앞장선 강아지 크기.")]
        [SerializeField] private float heroDiveSize = 2.2f;
        [Tooltip("다 쌓인 눈더미 반지름.")]
        [SerializeField] private float heapRadius = 3.2f;

        [Header("처형 장면 색 (시안 FOG · ground · snowfall)")]
        [SerializeField] private Color skyFog = new Color(0.62f, 0.74f, 0.9f);
        [Tooltip("하늘 장면의 안개 시작 · 끝 거리.")]
        [SerializeField] private Vector2 skyFogRange = new Vector2(80f, 200f);
        [SerializeField] private Color hillGroundTop = new Color32(0xEA, 0xF3, 0xFB, 0xFF);
        [SerializeField] private Color clumpGroundTop = new Color32(0xE9, 0xF3, 0xFB, 0xFF);
        [SerializeField] private Color execGroundBottom = Color.white;
        [SerializeField] private float execHaze = 0.6f;
        [Tooltip("눈송이 진하기 · 빠르기 — 언덕.")]
        [SerializeField] private Vector2 hillSnow = new Vector2(0.6f, 1.5f);
        [Tooltip("눈송이 진하기 · 빠르기 — 뭉치 · 터짐.")]
        [SerializeField] private Vector2 clumpSnow = new Vector2(0.8f, 1f);
        [Tooltip("눈사태 번쩍임 처음 진하기.")]
        [SerializeField] private float avalancheFlash = 0.9f;
        [Tooltip("터짐 번쩍임이 빠지는 시간 (초).")]
        [SerializeField] private float burstFlashSeconds = 0.18f;

        private string _victim;
        private Climber[] _climbers = new Climber[0];
        private Thrown[] _flung = new Thrown[0];
        private Flyer[] _flyers = new Flyer[0];
        private Faller[] _fallers = new Faller[0];
        private Thrown[] _burstDogs = new Thrown[0];
        private Shard[] _shards = new Shard[0];
        private float _heapRadius; // 뭉치 장면에서 마지막으로 그린 눈더미 반지름 — 터질 때 여기서부터 부푼다

        // 언덕을 달려 오르는 강아지 (시안 mk.hillers)
        private struct Climber
        {
            public string Key;
            public float Delay, Lane, Size;
        }

        // 튕겨 나는 강아지 (시안 mk.flung · burstDogs)
        private struct Thrown
        {
            public string Key;
            public Vector3 Velocity;
            public float Spin, Size;
        }

        // 하늘을 나는 강아지 (시안 mk.flyers)
        private struct Flyer
        {
            public string Key;
            public float X, Z, Height, Speed, Size, Spin, Phase;
        }

        // 꼴찌 위로 떨어지는 강아지 (시안 mk.rain)
        private struct Faller
        {
            public string Key;
            public float Angle, Radius, Start, Size, Turn, Lift;
        }

        // 터질 때 날리는 눈 조각 (시안 mk.shards)
        private struct Shard
        {
            public Vector3 Direction;
            public float Speed, Size;
        }

        /// <summary>
        /// 처형 장면을 처음부터 끝까지 재생한다. hero = 뭉치를 이끄는 강아지(개판을 건 사람), victim = 꼴찌의 강아지 (FxDog.Keys), victimName = 「탈락!」 앞에 붙는 이름.
        /// 소리는 Resources/CardFxSound/Card_{cardId}_Exec (없으면 소리 없이). 끝나도 지우지 않는다 — 부른 쪽이 지운다.
        /// </summary>
        public IEnumerator PlayExecutionRoutine(int cardId, string hero, string victim, string victimName)
        {
            _execution = true;
            _hero = hero;
            _victim = victim;
            _chargeAt = execChargeStart;
            _slideAt = execSlideStart;
            _slideEnd = hillStart;
            _length = execDuration;
            title.gameObject.SetActive(false);
            stamp.gameObject.SetActive(false);
            outSign.SetName(victimName);

            // 무리에는 꼴찌와 같은 강아지를 넣지 않는다 (시안 mk)
            var pack = new List<string>();
            foreach (string key in FxDog.Keys)
            {
                if (key != victim)
                    pack.Add(key);
            }
            if (pack.Count == 0)
                pack.AddRange(FxDog.Keys);
            Scatter(pack);
            ScatterExecution(pack);

            // 한 장면에 그리는 수 — 강아지(무리 + 앞장선 · 꼴찌), 눈뭉치(눈보라 · 눈사태), 눈 소품(바위 셋 또는 눈더미 + 눈덩이), 알갱이
            int openingDogs = Mathf.Max(dogCounts.x, dogCounts.y);
            int dogs = Mathf.Max(Mathf.Max(openingDogs, hillCounts.x + hillCounts.y), Mathf.Max(flyerCount, Mathf.Max(clumpCounts.x, clumpCounts.y))) + 1;
            int puffs = Mathf.Max((openingDogs + 1) * SprayPuffs, Mathf.Max(hillCounts.x * SprayPuffs + AvalanchePuffs, BurstPuffs));
            int props = Mathf.Max(RockPieces.Length, HeapClods + 1);
            int grains = Mathf.Max(burstCounts.x + burstCounts.y, PopGrains * PopsAtOnce);
            world.Begin(dogs, puffs, treeCount, props, grains);
            LoadSound(cardId, ExecutionSoundSuffix);
            float start = Time.time;
            StartSound();
            for (float t = 0f; t < _length; t = Time.time - start)
            {
                Render(t);
                yield return null;
            }
            group.alpha = 0f;
            warp.Hide();
            world.End();
        }

        // 방장은 처형 장면 길이만큼 기다린 뒤 꼴찌의 HP 를 0 으로 한다 — 길이가 다르면 장면 중에 판이 넘어간다
        private void CheckExecutionDuration()
        {
            if (Mathf.Abs(execDuration - (float)GameServer.GaepanExecutionSeconds) > DurationTolerance)
                Debug.LogError($"[{nameof(GaepanFx)}] 처형 장면 길이 {execDuration:0.00}초가 GameServer.GaepanExecutionSeconds {GameServer.GaepanExecutionSeconds:0.00}초와 다릅니다.", this);
        }

        // 시안 mk: 재생마다 새로 뽑는 처형 장면의 강아지 · 눈 조각. pack = 무리에 넣을 강아지 종류
        private void ScatterExecution(IReadOnlyList<string> pack)
        {
            _climbers = new Climber[hillCounts.x];
            for (int i = 0; i < _climbers.Length; i++)
                _climbers[i] = new Climber { Key = pack[(i + 2) % pack.Count], Delay = i * 0.06f + Random.Range(0f, 0.04f), Lane = Random.Range(-2.2f, 2.2f), Size = Random.Range(1.2f, 1.5f) };
            _flung = new Thrown[hillCounts.y];
            for (int i = 0; i < _flung.Length; i++)
            {
                _flung[i] = new Thrown
                {
                    Key = pack[i % pack.Count],
                    Velocity = new Vector3(Random.Range(-7f, 7f), Random.Range(8f, 14f), Random.Range(-12f, -2f)),
                    Spin = Random.Range(-6f, 6f),
                    Size = Random.Range(1.1f, 1.4f),
                };
            }
            _flyers = new Flyer[flyerCount];
            for (int i = 0; i < _flyers.Length; i++)
            {
                _flyers[i] = new Flyer
                {
                    Key = pack[(i + 3) % pack.Count],
                    X = Random.Range(-9f, 9f),
                    Z = Random.Range(3f, 24f),
                    Height = Random.Range(-6f, 12f),
                    Speed = Random.Range(5f, 10f),
                    Size = Random.Range(1.1f, 1.6f),
                    Spin = Random.Range(-1.5f, 1.5f),
                    Phase = Random.Range(0f, phaseRange),
                };
            }
            _fallers = new Faller[clumpCounts.x];
            for (int i = 0; i < _fallers.Length; i++)
            {
                _fallers[i] = new Faller
                {
                    Key = pack[i % pack.Count],
                    Angle = Random.Range(0f, 6.28f),
                    Radius = Random.Range(0.3f, 1.4f),
                    Start = 0.02f + i * 0.024f + Random.Range(0f, 0.02f),
                    Size = Random.Range(1.2f, 1.6f),
                    Turn = Random.Range(-1f, 1f),
                    Lift = Random.Range(0f, 0.5f),
                };
            }
            _burstDogs = new Thrown[clumpCounts.y];
            for (int i = 0; i < _burstDogs.Length; i++)
            {
                float a = Random.Range(0f, 6.28f);
                _burstDogs[i] = new Thrown
                {
                    Key = pack[(i + 1) % pack.Count],
                    Velocity = new Vector3(Mathf.Cos(a) * Random.Range(4f, 9f), Random.Range(5f, 11f), Mathf.Sin(a) * Random.Range(4f, 9f)),
                    Spin = Random.Range(-7f, 7f),
                    Size = Random.Range(1.1f, 1.4f),
                };
            }
            _shards = new Shard[burstCounts.y];
            for (int i = 0; i < _shards.Length; i++)
            {
                float a = Random.Range(0f, 6.28f), e = Random.Range(-0.2f, 1.2f);
                _shards[i] = new Shard
                {
                    Direction = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e) + 0.3f, Mathf.Sin(a) * Mathf.Cos(e)),
                    Speed = Random.Range(5f, 13f),
                    Size = Random.Range(0.18f, 0.5f),
                };
            }
            _heapRadius = heapRadius;
        }

        // 썰매 다음 장면들 — 시안 play.frame 의 장면 고르기
        private void RenderExecution(float t)
        {
            if (t < skyStart)
                Hill(t, Mathf.Clamp01((t - hillStart) / (skyStart - hillStart)));
            else if (t < clumpStart)
                Sky(t, Mathf.Clamp01((t - skyStart) / (clumpStart - skyStart)));
            else if (t < burstStart)
                Clump(t, Mathf.Clamp01((t - clumpStart) / (burstStart - clumpStart)));
            else
                Burst(t, t - burstStart, Mathf.Clamp01((t - burstStart) / (execDuration - burstStart)));
            outSign.Evaluate(t - outAt);
        }

        // 시안 SC.hill: 바위 위에서 떨고 있는 꼴찌에게 무리가 달려 올라 — 눈사태가 덮치고 꼴찌 · 강아지들이 튕겨 난다
        private void Hill(float t, float u)
        {
            float k = Mathf.Clamp01((u - AvalancheAt) / (1f - AvalancheAt));
            float shake = k > 0f && k < 0.35f ? (1f - k / 0.35f) * 0.05f : 0f;
            float glide = InOutCubic(u);
            var camera = new Vector3(Mathf.Lerp(-2.5f, 0.5f, glide), Mathf.Lerp(6f, 4.2f, u), Mathf.Lerp(-4f, 4f, glide));
            float yaw = Look(camera, new Vector3(RockSpot.x, 0.8f, RockSpot.z), new Vector2(Mathf.Sin(t * 50f), Mathf.Cos(t * 47f)) * shake);
            world.SetFog(hazeFog, fogRange);
            world.SetGround(new GaepanSky.GroundLayer { On = true, Top = hillGroundTop, Bottom = execGroundBottom, Haze = execHaze });

            foreach (Vector3 tree in _trees)
            {
                float x = tree.x * 0.7f + 3f, z = tree.y * 0.5f + 4f;
                // 달려 오르는 길은 비운다
                if (z < 24f && Mathf.Abs(x - Mathf.Lerp(-1f, 3f, Mathf.Clamp01((z - 1f) / 17f))) < 5.5f)
                    continue;
                world.Tree(x, z, tree.z);
            }

            if (k < 0.3f)
            {
                foreach (Vector3 piece in RockPieces)
                    world.Prop(GaepanProp.Rock, new Vector3(RockSpot.x + piece.x, piece.z * 0.42f, RockSpot.z + piece.y), piece.z, 1f, piece.z * 5f, 0f);
            }

            if (k <= 0f)
            {
                // 겁에 질려 두리번거리다 굳는다
                float fear = Mathf.Clamp01((u - 0.25f) / 0.3f);
                world.Dog(new GaepanDogState
                {
                    Key = _victim,
                    Position = RockSpot,
                    Size = victimSize.x,
                    Yaw = FaceCamera(camera, RockSpot.x, RockSpot.z) + Mathf.Sin(t * 5f) * 0.35f * (1f - fear),
                    Roll = Mathf.Sin(t * 40f) * 0.05f * (0.4f + fear),
                    StretchY = 1f - 0.06f * fear,
                    Pose = GaepanPose.Shake,
                    Phase = t * 14f,
                    NoBlob = true,
                });
            }
            else
            {
                float fly = k * 1.1f;
                world.Dog(new GaepanDogState
                {
                    Key = _victim,
                    Position = new Vector3(RockSpot.x - 1.5f * fly, 1.2f + 13f * fly - 4.9f * fly * fly, RockSpot.z + 2f * fly),
                    Size = victimSize.x,
                    Pitch = fly * 2.2f,
                    Roll = fly * (7f - 2f * fly),
                    StretchY = 1f + 0.08f * Mathf.Sin(fly * 9f),
                    Pose = GaepanPose.Flail,
                    Phase = t * 9f,
                    NoBlob = true,
                });
            }

            foreach (Climber d in _climbers)
            {
                float progress = u / AvalancheAt * 1.1f - d.Delay;
                if (progress < 0f || progress > 1f)
                    continue;
                float z = Mathf.Lerp(1f, RockSpot.z - 1f, progress), x = Mathf.Lerp(-1f, RockSpot.x, progress) + d.Lane * (1f - progress);
                float phase = t * (12.5f + d.Size) + d.Delay * 9f;
                Vector3 gait = Gait(phase, 1f);
                Dog(d.Key, new Vector3(x, gait.x, z), d.Size, Mathf.Atan2(RockSpot.x + 1f - d.Lane, RockSpot.z - 2f), gait.y, 0f, gait.z, GaepanPose.Run, phase);
                Spray(x, z - 0.5f, t, d.Delay * 7f, 0f, -1f, 0.8f);
            }

            if (k > 0f)
            {
                float spread = GaepanEase.OutCubic(k);
                for (int i = 0; i < AvalanchePuffs; i++)
                {
                    float a = (float)i / AvalanchePuffs * 6.28f;
                    var position = new Vector3(RockSpot.x + Mathf.Cos(a) * 5.5f * spread, 0.4f + Mathf.Abs(Mathf.Sin(a * 2f)) * 2.4f * spread, RockSpot.z + Mathf.Sin(a) * 4f * spread);
                    world.Puff(position, 0.7f + 0.9f * spread, 1f - k, i);
                }
                float fly = k * 1.1f;
                foreach (Thrown d in _flung)
                {
                    Vector3 toss = Toss(1f, d.Velocity.y, 11.8f, fly);
                    world.Dog(new GaepanDogState
                    {
                        Key = d.Key,
                        Position = new Vector3(RockSpot.x + d.Velocity.x * fly, toss.x, RockSpot.z + d.Velocity.z * fly),
                        Size = d.Size,
                        Yaw = d.Spin,
                        Pitch = d.Spin * 0.4f * toss.y,
                        Roll = d.Spin * 0.8f * toss.y,
                        StretchY = 1f - toss.z * 0.22f,
                        Pose = GaepanPose.Flail,
                        Phase = t * 8f + d.Spin,
                        NoBlob = true,
                    });
                }
                flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - k * 3.5f) * avalancheFlash);
            }
            snowfall.Draw(t, hillSnow.x, hillSnow.y, yaw);
        }

        // 시안 SC.sky: 땅에서 올려다본 하늘 — 튕겨 오른 강아지들이 날고, 앞장선 강아지가 솟았다가 카메라로 내리꽂는다
        private void Sky(float t, float u)
        {
            world.SetCamera(Vector3.zero, Mathf.Sin(t * 0.8f) * 0.08f, Mathf.Lerp(0.75f, 0.95f, u));
            world.SetFog(skyFog, skyFogRange);
            world.SetGround(default);

            foreach (Flyer d in _flyers)
            {
                world.Dog(new GaepanDogState
                {
                    Key = d.Key,
                    Position = new Vector3(d.X + Mathf.Sin(t * 2f + d.Phase) * 0.5f, d.Height + 8f + d.Speed * u * 1.4f, d.Z),
                    Size = d.Size,
                    Yaw = d.Phase + t * d.Spin * 0.5f,
                    Pitch = -0.5f + Mathf.Sin(t * 1.5f + d.Phase) * 0.4f,
                    Roll = Mathf.Sin(t * 1.1f + d.Phase) * 0.5f + d.Spin * u * 0.5f,
                    StretchY = 1f,
                    Pose = GaepanPose.Fly,
                    Phase = t * 5f + d.Phase,
                    NoBlob = true,
                });
            }

            var hero = new GaepanDogState { Key = _hero, Size = heroDiveSize, Yaw = Mathf.PI, StretchY = 1f, Pose = GaepanPose.Fly, Phase = t * 6f, NoBlob = true };
            if (u < DiveAt)
            {
                hero.Position = new Vector3(0f, Mathf.Lerp(-3f, 7.2f, GaepanEase.OutCubic(u / DiveAt)), 9f);
                hero.Pitch = -1.1f;
                hero.Roll = Mathf.Sin(t * 6f) * 0.12f;
            }
            else
            {
                float k = (u - DiveAt) / (1f - DiveAt), dive = k * k * k;
                hero.Position = new Vector3(Mathf.Sin(t * 20f) * k * 0.08f, Mathf.Lerp(7.2f, 1.4f, dive), Mathf.Lerp(9f, 1.8f, dive));
                hero.Pitch = Mathf.Lerp(-1.1f, 0.65f, GaepanEase.OutCubic(k * 2.5f));
            }
            world.Dog(hero);
            snowfall.Draw(t, 0f, 1f, 0f);
        }

        // 시안 SC.clump: 꼴찌 둘레를 도는 카메라 — 강아지들이 와르르 쏟아져 쌓이고, 그 위로 눈더미가 부푼다
        private void Clump(float t, float u)
        {
            float angle = -0.5f + u * 0.9f, distance = Mathf.Lerp(6.5f, 10f, InOutCubic(u));
            var camera = new Vector3(Mathf.Sin(angle) * distance, Mathf.Lerp(3.2f, 3.4f, u), ClumpDepth - Mathf.Cos(angle) * distance);
            float yaw = Look(camera, new Vector3(0f, Mathf.Lerp(1.1f, 1.8f, u), ClumpDepth), Vector2.zero);
            ClumpScenery();

            float grow = Mathf.Clamp01((u - 0.6f) / 0.3f), pile = GaepanEase.OutCubic((u - 0.38f) / 0.22f), buried = Mathf.Clamp01(1f - (u - 0.56f) * 6f);
            if (u < 0.5f)
            {
                world.Dog(new GaepanDogState
                {
                    Key = _victim,
                    Position = new Vector3(0f, 0f, ClumpDepth),
                    Size = victimSize.y,
                    Yaw = Mathf.PI + Mathf.Sin(t * 4.5f) * 0.9f,
                    Roll = Mathf.Sin(t * 33f) * 0.04f,
                    StretchY = 1f,
                    Pose = GaepanPose.Shake,
                    Phase = t * 14f,
                });
            }

            for (int i = 0; i < _fallers.Length; i++)
            {
                Faller d = _fallers[i];
                float k = Mathf.Clamp01((u - d.Start) / 0.22f);
                if (k <= 0f || buried <= 0f)
                    continue;
                float order = (float)i / _fallers.Length;
                float landing = 0.15f + order * 2.1f + d.Lift * 0.3f, radius = d.Radius * (1f - order * 0.7f);
                float height = Mathf.Lerp(13f, landing, k * k * k), air = (1f - k) * (1f - k);
                // 닿은 뒤: 눌렸다 펴지고 한 번 통 튄다
                float landed = Mathf.Max(0f, u - d.Start - 0.22f);
                float squash = k < 1f ? 0f : Mathf.Exp(-landed * 9f) * Mathf.Cos(landed * 24f);
                float bounce = k < 1f ? 0f : Mathf.Abs(Mathf.Sin(landed * 15f)) * Mathf.Exp(-landed * 8f) * 0.3f;
                var spot = new Vector3(Mathf.Cos(d.Angle) * radius, height, ClumpDepth + Mathf.Sin(d.Angle) * radius);
                world.Dog(new GaepanDogState
                {
                    Key = d.Key,
                    Position = spot + Vector3.up * bounce,
                    Size = d.Size * Mathf.Min(1f, buried * 1.4f),
                    Yaw = d.Angle * 3f,
                    Pitch = d.Turn * (0.6f + air * 3.4f),
                    Roll = d.Turn * (0.9f + air * 5f),
                    StretchY = k < 1f ? 1f + 0.07f * (1f - air) : 1f - 0.26f * squash,
                    Pose = GaepanPose.Flail,
                    PoseTo = GaepanPose.Tuck,
                    PoseBlend = k < 1f ? 0f : Mathf.Clamp01(landed / 0.16f),
                    Phase = t * 9f + d.Angle,
                    NoBlob = k <= 0.85f,
                });
                if (landed > 0f && landed < 0.14f)
                    Pop(spot, landed / 0.14f, i + 1, 0.9f);
            }

            if (pile > 0f)
            {
                _heapRadius = Mathf.Lerp(1.7f * pile, heapRadius, GaepanEase.OutCubic(grow)) * (1f + 0.02f * Mathf.Sin(t * 16f) * grow);
                SnowHeap(_heapRadius, t);
            }
            snowfall.Draw(t, clumpSnow.x, clumpSnow.y, yaw);
        }

        // 시안 SC.burst: 눈더미가 펑 터진다 — 눈 알갱이 · 강아지들이 사방으로 튀고, 꼴찌는 한 번 튀어 올랐다 털썩 내려앉아 어질어질
        private void Burst(float t, float ts, float u)
        {
            float angle = 0.4f + u * 0.25f, distance = Mathf.Lerp(8.8f, 10.5f, u);
            float shake = ts < 0.3f ? (1f - ts / 0.3f) * 0.04f : 0f;
            var camera = new Vector3(Mathf.Sin(angle) * distance, 3.2f, ClumpDepth - Mathf.Cos(angle) * distance);
            float yaw = Look(camera, new Vector3(0f, 1.8f, ClumpDepth), new Vector2(Mathf.Sin(t * 55f), Mathf.Cos(t * 51f)) * shake);
            ClumpScenery();

            float ring = GaepanEase.OutCubic(ts / 0.6f), ringAlpha = 1f - Mathf.Clamp01(ts / 0.6f);
            for (int i = 0; i < BurstPuffs; i++)
            {
                float a = (float)i / BurstPuffs * 6.28f;
                var position = new Vector3(Mathf.Cos(a) * (1.5f + 3.5f * ring), 0.4f + Mathf.Abs(Mathf.Sin(a * 1.7f)) * 1.8f * ring, ClumpDepth + Mathf.Sin(a) * (1.5f + 3f * ring));
                // 시안은 반투명 안개(반지름 1.15) — 여기 눈뭉치는 불투명이라 앞쪽 것이 튀어나오는 꼴찌를 가린다. 작게, 더 빨리 줄인다
                world.Puff(position, BurstPuffRadius, ringAlpha * ringAlpha, i);
            }

            // 눈더미가 알갱이로 터진다 — 알갱이마다 방향 · 빠르기 · 크기가 정해져 있다
            float grainAlpha = Mathf.Clamp01(1.6f - ts);
            for (int i = 0; i < burstCounts.x; i++)
            {
                float h1 = Hash(i, 1.3f), h2 = Hash(i, 2.9f), h3 = Hash(i, 4.7f), h4 = Hash(i, 7.1f);
                float a = h1 * 6.283f, e = 0.15f + h2 * 1.3f, speed = (3f + h3 * 10f) * (1f - 0.3f * h4), travel = ts * (1f - 0.25f * ts * h4);
                float y = 1.3f + Mathf.Sin(e) * speed * ts - 4.9f * ts * ts;
                if (y < 0f)
                    continue;
                var position = new Vector3(Mathf.Cos(a) * Mathf.Cos(e) * speed * travel, y + 0.02f, ClumpDepth + Mathf.Sin(a) * Mathf.Cos(e) * speed * travel);
                world.Grain(position, (0.02f + 0.035f * h4 * h4) * grainScale, grainAlpha);
            }
            float shrink = 1f - Mathf.Clamp01(ts / 0.9f) * 0.3f;
            foreach (Shard s in _shards)
            {
                var position = new Vector3(s.Direction.x * s.Speed * ts, Mathf.Max(0.05f, 1.5f + s.Direction.y * s.Speed * ts - 4.9f * ts * ts), ClumpDepth + s.Direction.z * s.Speed * ts);
                world.Grain(position, s.Size * 0.2f * shrink * grainScale, 1f);
            }

            float drift = ts < 0.9f ? ts : 0.9f + (ts - 0.9f) * 0.45f;
            foreach (Thrown d in _burstDogs)
            {
                Vector3 toss = Toss(1f, d.Velocity.y, 10.8f, ts);
                world.Dog(new GaepanDogState
                {
                    Key = d.Key,
                    Position = new Vector3(d.Velocity.x * drift, toss.x, ClumpDepth + d.Velocity.z * drift),
                    Size = d.Size,
                    Yaw = d.Spin,
                    Pitch = d.Spin * 0.45f * toss.y,
                    Roll = d.Spin * 0.8f * toss.y,
                    StretchY = 1f - toss.z * 0.22f,
                    Pose = GaepanPose.Flail,
                    Phase = t * 9f + d.Spin,
                    NoBlob = toss.x >= 0.4f,
                });
            }

            if (ts > 0.05f)
            {
                bool airborne = ts < 0.5f;
                float jump = airborne ? Mathf.Sin(Mathf.Clamp01((ts - 0.05f) / 0.45f) * Mathf.PI) * 1.3f : 0f;
                float thud = airborne ? 0f : Mathf.Exp(-(ts - 0.5f) * 9f) * Mathf.Cos((ts - 0.5f) * 26f);
                world.Dog(new GaepanDogState
                {
                    Key = _victim,
                    Position = new Vector3(0f, jump, ClumpDepth),
                    Size = victimSize.z,
                    Yaw = FaceCamera(camera, 0f, ClumpDepth) + (airborne ? 0f : Mathf.Sin(t * 5f) * 0.25f),
                    Roll = airborne ? (ts - 0.05f) * 9f : Mathf.Sin(t * 9f) * 0.06f,
                    StretchY = 1f - 0.2f * thud,
                    Pose = airborne ? GaepanPose.Flail : GaepanPose.Shake,
                    Phase = t * 10f,
                });
            }
            if (ts < 0.16f)
                SnowHeap(_heapRadius * (1f + ts * 2.5f), t);
            flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - ts / burstFlashSeconds));
            snowfall.Draw(t, clumpSnow.x, clumpSnow.y, yaw);
        }

        // 뭉치 · 터짐의 배경 — 안개 · 땅 · 둘레에 둥글게 선 나무
        private void ClumpScenery()
        {
            world.SetFog(hazeFog, fogRange);
            world.SetGround(new GaepanSky.GroundLayer { On = true, Top = clumpGroundTop, Bottom = execGroundBottom, Haze = execHaze });
            foreach (Vector3 tree in _trees)
            {
                float a = tree.y * 0.3f, r = 14f + tree.y * 0.25f;
                world.Tree(Mathf.Cos(a) * r, ClumpDepth + Mathf.Sin(a) * r, tree.z);
            }
        }

        // 시안 snowDog(3D): 눈더미 + 겉에 박힌 눈덩이
        private void SnowHeap(float radius, float t)
        {
            float width = radius * 1.25f, stretch = 1f + 0.04f * Mathf.Sin(t * 11f);
            world.Prop(GaepanProp.Heap, new Vector3(0f, -0.03f, ClumpDepth), width, stretch, 0.7f, 0f);
            for (int i = 0; i < HeapClods; i++)
            {
                // 앞 열둘은 봉우리 위, 나머지는 자락 둘레
                float a = i * 2.4f, reach = i < 12 ? 0.25f + 0.6f * Hash(i, 3f) : 1f + 0.25f * Hash(i, 3f), size = (0.07f + 0.09f * Hash(i, 5f)) * width;
                float height = reach < 1f ? 0.6f * Mathf.Pow(1f - Mathf.Pow(reach, 1.5f), 1.4f) * width * stretch : 0f;
                world.Prop(GaepanProp.Clod, new Vector3(Mathf.Cos(a) * reach * width, height + size * 0.2f, ClumpDepth + Mathf.Sin(a) * reach * width), size, 1f, i * 1.7f, i * 0.9f);
            }
        }

        // 시안 pop: 눈더미에 강아지가 떨어질 때 툭 튀는 알갱이 — a = 진행(0 ~ 1), k = 크기
        private void Pop(Vector3 at, float a, float seed, float k)
        {
            float alpha = Mathf.Min(1f, (1f - a) * 2f);
            for (int i = 0; i < PopGrains; i++)
            {
                float h1 = Hash(i, seed), h2 = Hash(i, seed + 2.3f), h3 = Hash(i, seed + 4.1f);
                float direction = h1 * 6.283f, speed = (0.5f + h2 * 1.6f) * k;
                var position = new Vector3(at.x + Mathf.Cos(direction) * speed * a, Mathf.Max(0.02f, at.y + (1.2f + h3 * 2.4f) * k * a * (1f - a * 1.1f)), at.z + Mathf.Sin(direction) * speed * a);
                world.Grain(position, (0.014f + 0.02f * h3) * grainScale, alpha);
            }
        }

        // 시안 look: from 에서 target 을 본다 (shake = 방위 · 높낮이에 더할 흔들림). 돌려주는 값 = 방위 — 눈송이 흐름에 쓴다
        private float Look(Vector3 from, Vector3 target, Vector2 shake)
        {
            Vector3 d = target - from;
            float yaw = Mathf.Atan2(d.x, d.z) + shake.x;
            world.SetCamera(from, yaw, Mathf.Atan2(d.y, Mathf.Sqrt(d.x * d.x + d.z * d.z)) + shake.y);
            return yaw;
        }

        // 시안 faceCam: (x, z) 에서 카메라를 보는 방위
        private static float FaceCamera(Vector3 camera, float x, float z) => Mathf.Atan2(camera.x - x, camera.z - z);

        // 시안 eio: 천천히 시작해 빨라졌다 천천히 멈춘다
        private static float InOutCubic(float u) => u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;

        // 시안 hh: 번호 · 씨앗으로 정해지는 0 ~ 1 — 프레임이 바뀌어도 같은 값
        private static float Hash(int index, float seed)
        {
            double value = System.Math.Sin(index * 127.1 + seed * 311.7) * 43758.5453;
            return (float)(value - System.Math.Floor(value));
        }

        // 시안 toss: 던져진 뒤 포물선 → 땅에 닿으면 두 번 통통 튀고 멈춘다. (높이, 구른 시간 — 회전에 쓴다, 닿은 뒤 눌림)
        private static Vector3 Toss(float height, float speed, float gravity, float time)
        {
            float y = height, v = speed, t = time, spin = 0f;
            for (int bounce = 0; bounce < 3; bounce++)
            {
                float landing = (v + Mathf.Sqrt(v * v + 2f * gravity * y)) / gravity;
                float roll = bounce > 0 ? 0.35f : 1f;
                if (t < landing)
                    return new Vector3(y + v * t - 0.5f * gravity * t * t, spin + t * roll, bounce > 0 ? Mathf.Max(0f, 1f - t / 0.12f) : 0f);
                t -= landing;
                spin += landing * roll;
                v = (bounce > 0 ? 0.3f : 0.42f) * Mathf.Abs(v - gravity * landing);
                y = 0f;
            }
            return new Vector3(0f, spin, 0f);
        }
    }
}
