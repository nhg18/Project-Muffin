using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 「개판 오분전」 연출. 3 단계 = 시안 DOGPILE.play({mode:"cast"}) — 눈 마을(3D)에서 번쩍 → 강아지 무리 돌진 → 옆에서 본 배 깔고 썰매, 그 위로 제목 · 「5 바퀴 동안」 도장.
    /// 4 단계 장식은 없다 (개판 HUD · 이후 5 바퀴 시연은 게임 화면 몫).
    /// 장면 값은 시안 DOGPILE.SC.flash · charge · slide 와 같다. 강아지 · 나무 · 눈송이 자리는 재생마다 무작위 (시안 mk).
    /// </summary>
    public class GaepanFx : CardFx
    {
        // 시안 gait: 다리를 뻗을 때 뜨는 높이 · 코가 들리는 각 · 늘어나고 눌리는 양 (위상 0.6 늦게)
        private const float GaitLag = 0.6f, GaitLift = 0.27f, GaitNose = 0.1f, GaitStretch = 0.06f, GaitSquash = 0.07f;
        // 시안 spray: 눈뭉치 다섯이 a(0 ~ 1)를 따라 뒤로 1.6 · 위로 0.5 날며 커지고 사라진다
        private const int SprayPuffs = 5;
        private const float SprayRate = 3.2f, SprayReach = 1.6f, SprayRise = 0.5f, SprayHeight = 0.12f, SprayRadius = 0.16f, SprayGrow = 0.34f, SprayAlpha = 0.95f;

        [Header("판")]
        [Tooltip("연출 전체 — 나타나기 · 사라지기. 눈 마을 · 눈송이 · 제목 · 도장이 렌더 텍스처 한 장에 그려져 있어 한 장으로 흐려진다.")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private GaepanWorld world;
        [SerializeField] private GaepanWarp warp;
        [Tooltip("앞을 내리는 눈송이 (시안 앞 캔버스).")]
        [SerializeField] private GaepanSnowfall snowfall;
        [Tooltip("첫 장면의 흰 번쩍임.")]
        [SerializeField] private Graphic flash;
        [SerializeField] private GaepanTitle title;
        [SerializeField] private GaepanStamp stamp;

        [Header("시간표 (시안 DOGPILE.PLAN.cast, 초)")]
        [Tooltip("무리 돌진이 시작되는 시각.")]
        [SerializeField] private float chargeStart = 0.3f;
        [Tooltip("썰매가 시작되는 시각.")]
        [SerializeField] private float slideStart = 1.35f;
        [Tooltip("3 단계 길이.")]
        [SerializeField] private float duration = 3f;
        [Tooltip("나타나는 시간.")]
        [SerializeField] private float fadeIn = 0.12f;
        [Tooltip("끝에서 사라지는 시간.")]
        [SerializeField] private float fadeOut = 0.45f;
        [Tooltip("다 사라진 뒤 4 단계로 넘어갈 때까지 (ms).")]
        [SerializeField] private float afterMs = 80f;

        [Header("강아지 (시안 mk)")]
        [Tooltip("앞장서는 강아지 (시전자의 강아지 — 지금은 늘 이 값).")]
        [SerializeField] private string heroKey = "na";
        [Tooltip("무리 수 · 썰매 수.")]
        [SerializeField] private Vector2Int dogCounts = new Vector2Int(12, 8);
        [Tooltip("무리: 가운데서 좌우로 떨어진 거리 (최소 · 최대).")]
        [SerializeField] private Vector2 herdSpread = new Vector2(1.6f, 6.2f);
        [Tooltip("무리: 처음 깊이.")]
        [SerializeField] private Vector2 herdDepth = new Vector2(8f, 24f);
        [Tooltip("무리: 크기.")]
        [SerializeField] private Vector2 herdSize = new Vector2(1.25f, 1.6f);
        [Tooltip("무리: 빠르기.")]
        [SerializeField] private Vector2 herdSpeed = new Vector2(6f, 9f);
        [Tooltip("썰매: 카메라보다 뒤처진 거리.")]
        [SerializeField] private Vector2 sliderOffset = new Vector2(-9f, -1.5f);
        [Tooltip("썰매: 깊이.")]
        [SerializeField] private Vector2 sliderDepth = new Vector2(3.4f, 13f);
        [Tooltip("썰매: 크기.")]
        [SerializeField] private Vector2 sliderSize = new Vector2(1.15f, 1.45f);
        [Tooltip("썰매: 장면 동안 앞서거나 처지는 거리 (±).")]
        [SerializeField] private float sliderLag = 1.5f;
        [Tooltip("다리 움직임 처음 위상 범위.")]
        [SerializeField] private float phaseRange = 6f;

        [Header("나무 (시안 mk.trees · sideTrees)")]
        [SerializeField] private int treeCount = 44;
        [Tooltip("돌진: 좌우 범위 (가운데 길 폭 · 끝).")]
        [SerializeField] private Vector2 treeSpan = new Vector2(4.5f, 34f);
        [SerializeField] private Vector2 treeDepth = new Vector2(4f, 130f);
        [SerializeField] private Vector2 treeHeight = new Vector2(3.5f, 7f);
        [Tooltip("썰매: 옆 나무 x · z · 높이 범위.")]
        [SerializeField] private Vector2 sideTreeX = new Vector2(-10f, 90f);
        [SerializeField] private Vector2 sideTreeZ = new Vector2(14f, 60f);
        [SerializeField] private Vector2 sideTreeHeight = new Vector2(3f, 7f);

        [Header("카메라 (시안 C — 자리, 방위 · 높낮이 라디안)")]
        [SerializeField] private Vector3 flashCamera = new Vector3(0f, 1.2f, -2f);
        [Tooltip("돌진: 좌우 흔들림 · 높이 · 깊이.")]
        [SerializeField] private Vector3 chargeCamera = new Vector3(0.25f, 0.95f, 0f);
        [Tooltip("돌진: 내려다보는 각.")]
        [SerializeField] private float chargePitch = -0.04f;
        [Tooltip("썰매: 높이 · 깊이 · 옆으로 따라가는 빠르기.")]
        [SerializeField] private Vector3 slideCamera = new Vector3(1.15f, -3.2f, 13f);
        [Tooltip("썰매: 방위 · 높낮이.")]
        [SerializeField] private Vector2 slideAngles = new Vector2(0.05f, -0.06f);

        [Header("장면 색 (시안 FOG · ground · snowfall)")]
        [SerializeField] private Color flashFog = new Color(0.95f, 0.89f, 0.86f);
        [SerializeField] private Color hazeFog = new Color(0.96f, 0.87f, 0.83f);
        [Tooltip("안개 시작 · 끝 거리.")]
        [SerializeField] private Vector2 fogRange = new Vector2(30f, 150f);
        [SerializeField] private Color chargeGroundTop = new Color32(0xE2, 0xEE, 0xF8, 0xFF);
        [SerializeField] private Color chargeGroundBottom = Color.white;
        [SerializeField] private float chargeHaze = 0.8f;
        [SerializeField] private Color slideGroundTop = new Color32(0xE6, 0xF1, 0xFA, 0xFF);
        [SerializeField] private Color slideGroundBottom = new Color32(0xFD, 0xFE, 0xFF, 0xFF);
        [SerializeField] private float slideHaze = 0.7f;
        [Tooltip("번쩍임 처음 진하기 → 끝에는 이만큼 빠진다.")]
        [SerializeField] private float flashFade = 0.75f;
        [Tooltip("눈송이 진하기.")]
        [SerializeField] private float snowAlpha = 0.9f;
        [Tooltip("눈송이 빠르기 — 돌진 · 썰매.")]
        [SerializeField] private Vector2 snowSpeed = new Vector2(2.5f, 3f);

        private Runner[] _herd = new Runner[0];
        private Runner[] _sliders = new Runner[0];
        private Vector3[] _trees = new Vector3[0];
        private Vector3[] _sideTrees = new Vector3[0];

        // 시안 mk 의 강아지 하나 (무리: Offset = x, 썰매: Offset = dx · Speed = lag)
        private struct Runner
        {
            public string Key;
            public float Offset, Depth, Phase, Size, Speed;
        }

        private void Awake()
        {
            if (group == null || world == null || warp == null || snowfall == null || flash == null || title == null || stamp == null)
                Debug.LogError($"[{nameof(GaepanFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnDestroy()
        {
            // 눈 마을은 시작할 때 캔버스 밖으로 떼어 놓았다 — 같이 지운다
            if (world != null)
                Destroy(world.gameObject);
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            Scatter();
            world.Begin();
            float start = Time.time;
            for (float t = 0f; t < duration; t = Time.time - start)
            {
                Render(t);
                yield return null;
            }
            group.alpha = 0f;
            warp.Hide();
            world.End();
            yield return new WaitForSeconds(afterMs / 1000f);
        }

        // 시안 mk: 재생마다 새로 뽑는 무리 · 썰매 · 나무 · 눈송이
        private void Scatter()
        {
            IReadOnlyList<string> keys = FxDog.Keys;
            _herd = new Runner[dogCounts.x];
            for (int i = 0; i < _herd.Length; i++)
            {
                _herd[i] = new Runner
                {
                    Key = keys[i % keys.Count],
                    Offset = (i % 2 == 1 ? 1f : -1f) * Random.Range(herdSpread.x, herdSpread.y),
                    Depth = Random.Range(herdDepth.x, herdDepth.y),
                    Phase = Random.Range(0f, phaseRange),
                    Size = Random.Range(herdSize.x, herdSize.y),
                    Speed = Random.Range(herdSpeed.x, herdSpeed.y),
                };
            }
            _sliders = new Runner[dogCounts.y];
            for (int i = 0; i < _sliders.Length; i++)
            {
                _sliders[i] = new Runner
                {
                    Key = keys[(i + 1) % keys.Count],
                    Offset = Random.Range(sliderOffset.x, sliderOffset.y),
                    Depth = Random.Range(sliderDepth.x, sliderDepth.y),
                    Phase = Random.Range(0f, phaseRange),
                    Size = Random.Range(sliderSize.x, sliderSize.y),
                    Speed = Random.Range(-sliderLag, sliderLag),
                };
            }
            _trees = new Vector3[treeCount];
            for (int i = 0; i < _trees.Length; i++)
            {
                float x;
                do
                {
                    x = Random.Range(-treeSpan.y, treeSpan.y);
                }
                while (Mathf.Abs(x) < treeSpan.x);
                _trees[i] = new Vector3(x, Random.Range(treeDepth.x, treeDepth.y), Random.Range(treeHeight.x, treeHeight.y));
            }
            _sideTrees = new Vector3[treeCount];
            for (int i = 0; i < _sideTrees.Length; i++)
                _sideTrees[i] = new Vector3(Random.Range(sideTreeX.x, sideTreeX.y), Random.Range(sideTreeZ.x, sideTreeZ.y), Random.Range(sideTreeHeight.x, sideTreeHeight.y));
            snowfall.Scatter();
        }

        // 한 프레임 — 시안 play.frame: 지금 장면을 고르고, 장면 안 진행 u(0 ~ 1) 로 그린다
        private void Render(float t)
        {
            world.BeginFrame();
            if (t < chargeStart)
            {
                Flash(t, Mathf.Clamp01(t / chargeStart));
            }
            else
            {
                warp.Hide();
                flash.color = new Color(1f, 1f, 1f, 0f);
                if (t < slideStart)
                    Charge(t, Mathf.Clamp01((t - chargeStart) / (slideStart - chargeStart)));
                else
                    Slide(t, t - slideStart, Mathf.Clamp01((t - slideStart) / (duration - slideStart)));
            }
            world.EndFrame();
            title.Evaluate(t);
            stamp.Evaluate(t);
            float fade = (t - (duration - fadeOut)) / fadeOut;
            group.alpha = Mathf.Min(1f, t / fadeIn) * (fade > 0f ? 1f - GaepanEase.OutCubic(fade) : 1f);
        }

        // 시안 SC.flash: 하늘을 보며 눈송이 워프 + 흰 번쩍임이 빠진다
        private void Flash(float t, float u)
        {
            world.SetCamera(flashCamera, 0f, 0f);
            world.SetFog(flashFog, fogRange);
            world.SetGround(default);
            warp.Draw(t, flashCamera, Vector3.forward);
            flash.color = new Color(1f, 1f, 1f, 1f - GaepanEase.OutCubic(u) * flashFade);
            snowfall.Draw(t, 0f, 1f, 0f);
        }

        // 시안 SC.charge: 흔들리는 낮은 카메라로 무리가 달려든다 — 앞장선 강아지가 멀리서 코앞까지
        private void Charge(float t, float u)
        {
            float shake = Mathf.Sin(t * 40f) * 0.008f * u;
            var position = new Vector3(Mathf.Sin(t * 1.3f) * chargeCamera.x, chargeCamera.y + Mathf.Abs(Mathf.Sin(t * 15f)) * 0.03f * u, chargeCamera.z);
            float yaw = Mathf.Sin(t * 1.1f) * 0.05f + shake;
            world.SetCamera(position, yaw, chargePitch + shake);
            world.SetFog(hazeFog, fogRange);
            world.SetGround(new GaepanSky.GroundLayer { On = true, Top = chargeGroundTop, Bottom = chargeGroundBottom, Haze = chargeHaze });

            foreach (Vector3 tree in _trees)
                world.Tree(tree.x, tree.y, tree.z);
            foreach (Runner d in _herd)
            {
                float z = d.Depth - t * d.Speed * 0.55f, phase = t * (9.5f + d.Speed * 0.45f) + d.Phase;
                Vector3 gait = Gait(phase, 1f);
                Dog(d.Key, new Vector3(d.Offset + Mathf.Sin(phase * 0.5f) * 0.07f, gait.x, z), d.Size, Mathf.PI + Mathf.Sin(phase * 0.5f) * 0.06f, gait.y, 0f, gait.z, GaepanPose.Run, phase);
                Spray(d.Offset, z + 0.5f * d.Size, t, d.Phase, 0f, 1f, 0.8f);
            }
            float heroZ = Mathf.Lerp(15f, 1.9f, Mathf.Pow(u, 1.35f)), heroPhase = t * 13.5f;
            Vector3 heroGait = Gait(heroPhase, 1.15f);
            Dog(heroKey, new Vector3(Mathf.Sin(heroPhase * 0.5f) * 0.05f, heroGait.x, heroZ), 1.8f, Mathf.PI + Mathf.Sin(heroPhase * 0.5f) * 0.05f, heroGait.y, 0f, heroGait.z, GaepanPose.Run, heroPhase);
            Spray(0f, heroZ + 0.8f, t, 0.3f, 0f, 1f, 1.1f);
            snowfall.Draw(t, snowAlpha, snowSpeed.x, yaw);
        }

        // 시안 SC.slide: 옆에서 따라가는 카메라 — 배 깔고 미끄러지며 통통 튄다
        private void Slide(float t, float ts, float u)
        {
            var position = new Vector3(slideCamera.z * ts, slideCamera.x, slideCamera.y);
            world.SetCamera(position, slideAngles.x, slideAngles.y);
            world.SetFog(hazeFog, fogRange);
            world.SetGround(new GaepanSky.GroundLayer { On = true, Top = slideGroundTop, Bottom = slideGroundBottom, Haze = slideHaze });

            foreach (Vector3 tree in _sideTrees)
                world.Tree(tree.x, tree.y, tree.z);
            foreach (Runner d in _sliders)
            {
                float x = position.x + d.Offset + 5f + Mathf.Sin(t * 2f + d.Phase) * 0.3f + d.Speed * u;
                float bounce = Mathf.Sin(t * 5.2f + d.Phase), hop = Mathf.Pow(Mathf.Max(0f, bounce), 3f);
                float stretch = 1f + hop * 0.05f - Mathf.Pow(Mathf.Max(0f, -bounce), 6f) * 0.09f;
                Dog(d.Key, new Vector3(x, 0.02f + hop * 0.16f, d.Depth), d.Size, Mathf.PI / 2f + 0.28f + Mathf.Sin(t * 2.1f + d.Phase) * 0.08f, 0f,
                    Mathf.Sin(t * 5.2f + d.Phase - 0.7f) * 0.07f, stretch, GaepanPose.Slide, t * 6f + d.Phase);
                Spray(x - 0.7f * d.Size, d.Depth, t, d.Phase, -1f, 0f, 0.9f);
            }
            float heroX = position.x + 1.2f + u * 0.5f;
            float heroBounce = Mathf.Sin(t * 5.6f), heroHop = Mathf.Pow(Mathf.Max(0f, heroBounce), 3f);
            float heroStretch = 1f + heroHop * 0.05f - Mathf.Pow(Mathf.Max(0f, -heroBounce), 6f) * 0.08f;
            Dog(heroKey, new Vector3(heroX, 0.02f + heroHop * 0.14f, 1.1f), 1.4f, Mathf.PI / 2f + 0.5f, 0f, Mathf.Sin(t * 5.6f - 0.7f) * 0.06f, heroStretch, GaepanPose.Slide, t * 7f);
            Spray(heroX - 1f, 1.1f, t, 0.5f, -1f, 0f, 1.1f);
            snowfall.Draw(t, snowAlpha, snowSpeed.y, slideAngles.x);
        }

        private void Dog(string key, Vector3 position, float size, float yaw, float pitch, float roll, float stretch, GaepanPose pose, float phase)
        {
            world.Dog(new GaepanDogState
            {
                Key = key,
                Position = position,
                Size = size,
                Yaw = yaw,
                Pitch = pitch,
                Roll = roll,
                StretchY = stretch,
                Pose = pose,
                Phase = phase,
            });
        }

        // 시안 spray: 달리는 녀석 뒤로 눈보라 — (dx, dz) = 진행 반대 방향, k = 크기
        private void Spray(float x, float z, float t, float seed, float dx, float dz, float k)
        {
            for (int i = 0; i < SprayPuffs; i++)
            {
                float a = GaepanWorld.Fraction(t * SprayRate + (float)i / SprayPuffs + seed);
                var position = new Vector3(x + dx * a * SprayReach * k, SprayHeight + a * SprayRise * k, z + dz * a * SprayReach * k);
                world.Puff(position, (SprayRadius + a * SprayGrow) * k, (1f - a) * SprayAlpha, i + seed);
            }
        }

        // 시안 gait: (높이, 코 각, 세로 늘림)
        private static Vector3 Gait(float phase, float amplitude)
        {
            float s = Mathf.Sin(phase - GaitLag), air = Mathf.Max(0f, s), ground = Mathf.Max(0f, -s);
            return new Vector3(air * air * GaitLift * amplitude, Mathf.Cos(phase - GaitLag) * GaitNose * amplitude, 1f + (GaitStretch * air - GaitSquash * ground) * amplitude);
        }
    }
}
