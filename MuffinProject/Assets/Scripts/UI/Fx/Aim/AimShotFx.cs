using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 「목표 조준」 연출 (시안 aimShot · FXA 목표 조준).
    /// 3 단계: 화면을 덮는 저격 장면 — 앞부분(달밤 전경 → 얼굴 → 방아쇠)은 영상, 총알부터는 코드로 그린 장면(<see cref="AimSceneFx"/>),
    /// 위에 이름표(<see cref="AimTagFx"/>) · 명중 제목(<see cref="AimHitTitleFx"/>) · 화면 흔들기, 끝에 가라앉으며 위아래 띠가 닫힌다.
    /// 4 단계 장식: 쓴 사람 위 말풍선 「목표 조준!」 + 판 흔들기. 고르기 창 · 피해 · 손패 이동은 게임 화면 몫이라 넣지 않는다.
    /// </summary>
    public class AimShotFx : CardFx
    {
        private const string BubbleText = "목표 조준!";
        // 시안 shake 의 키 (translate(k·세기, ±k·세기))
        private static readonly float[] ShakeKeys = { 0f, -1f, 0.8f, -0.6f, 0.45f, -0.25f, 0f };
        private const float ShakeUp = 0.5f;
        private const float ShakeDown = -0.45f;
        private const float HoldEpsilon = 0.0001f;
        private const float Percent = 0.01f;

        [Serializable]
        private struct Shake
        {
            [Tooltip("시작 (3 단계 시작부터 초).")]
            public float at;
            [Tooltip("세기 (시안 px).")]
            public float amount;
            [Tooltip("길이 (초).")]
            public float seconds;
        }

        [Header("판")]
        [SerializeField] private CanvasGroup root;
        [Tooltip("흔들리는 판 (영상 · 장면 · 마무리 어둠). 위아래 띠 · 글자는 흔들리지 않는다.")]
        [SerializeField] private RectTransform inner;
        [SerializeField] private RawImage video;
        [SerializeField] private VideoPlayer player;
        [SerializeField] private AimSceneFx scene;
        [Tooltip("마무리: 가라앉는 어둠 · 가운데 가로 빛 (화면 좌표).")]
        [SerializeField] private AimPainter ending;
        [SerializeField] private RectTransform barTop;
        [SerializeField] private RectTransform barBottom;
        [SerializeField] private AimTagFx nameTag;
        [SerializeField] private AimHitTitleFx hitTitle;

        [Header("시간 (시안 AIM_T, 3 단계 시작부터 초)")]
        [Tooltip("이름표가 나오는 때.")]
        [SerializeField] private float tagAt = 0.02f;
        [Tooltip("명중 제목이 나오는 때 (hit + .66).")]
        [SerializeField] private float hitTitleAt = 5.75f;
        [Tooltip("마무리(가라앉기)가 시작하는 때 (len).")]
        [SerializeField] private float closeAt = 6.45f;
        [Tooltip("장면은 이 때에서 멈춘다 (end).")]
        [SerializeField] private float drawUntil = 8.61f;
        [Tooltip("화면 흔들기 (시안 cues 의 shake).")]
        [SerializeField] private Shake[] shakes =
        {
            new Shake { at = 3.1f, amount = 9f, seconds = 0.26f },
            new Shake { at = 5.11f, amount = 8f, seconds = 0.24f },
            new Shake { at = 5.69f, amount = 4f, seconds = 0.2f },
            new Shake { at = 6.04f, amount = 5f, seconds = 0.15f },
        };

        [Header("나타남 · 사라짐")]
        [Tooltip("판이 나타나는 시간 (초, 시안 transition .25s).")]
        [SerializeField] private float fadeIn = 0.25f;
        [Tooltip("판이 사라지는 시간 (초, 시안 .out .35s).")]
        [SerializeField] private float fadeOut = 0.35f;
        [Tooltip("사라지기 시작하고 끝날 때까지 (초, 시안 setTimeout(fin, 380)).")]
        [SerializeField] private float finishDelay = 0.38f;
        [Tooltip("영상 준비를 기다리는 가장 긴 시간 (초).")]
        [SerializeField] private float videoTimeout = 1.5f;
        [Tooltip("영상이 장면 시간보다 이만큼(초) 앞서면 장면 시간을 영상에 맞춘다 (시안의 소리 맞추기 .12). 멈칫한 뒤 영상이 따라잡는 만큼 장면도 따라간다.")]
        [SerializeField] private float videoFollow = 0.12f;

        [Header("마무리 (close 부터 ms)")]
        [Tooltip("어둠이 다 차는 시간.")]
        [SerializeField] private float darkenMs = 1500f;
        [Tooltip("어둠 — 안쪽 · 바깥 알파 (다 찼을 때).")]
        [SerializeField] private Vector2 darkAlpha = new Vector2(0.12f, 0.8f);
        [SerializeField] private Color darkColor = new Color32(5, 7, 15, 255);
        [Tooltip("어둠 가운데 (화면 비율, 위에서) · 안 반지름(높이 비율) · 바깥 반지름(대각선 비율).")]
        [SerializeField] private Vector4 darkShape = new Vector4(0.5f, 0.6f, 0.1f, 0.55f);
        [Tooltip("위아래 띠: 처음 높이 · 늘어나는 높이 (화면 높이 %), 늦춤 · 길이 (ms), 굽음.")]
        [SerializeField] private Vector2 barHeight = new Vector2(8f, 42f);
        [SerializeField] private Vector3 barTiming = new Vector3(450f, 1050f, 2.2f);
        [Tooltip("가운데 가로 빛: 시작 · 길이 · 끝 (ms).")]
        [SerializeField] private Vector3 lineTiming = new Vector3(1380f, 120f, 1750f);
        [SerializeField] private Color lineColor = new Color(200f / 255f, 215f / 255f, 1f, 0.8f);
        [Tooltip("가운데 가로 빛 두께 (시안 px).")]
        [SerializeField] private float lineHeight = 2f;
        [Tooltip("사라지기 시작 (ms).")]
        [SerializeField] private float fadeOutMs = 1950f;

        [Header("4 단계 장식 (FXA 목표 조준 s4)")]
        [Tooltip("말풍선 높이 (시안 px, 위로 +).")]
        [SerializeField] private float bubbleRise = 80f;
        [Tooltip("판 흔들기 세기 (시안 arenaShake).")]
        [SerializeField] private float stageShake = 6f;
        [Tooltip("장식 뒤 기다림 (초).")]
        [SerializeField] private float postWait = 0.5f;

        private RenderTexture _texture;
        private float _pixelScale = 1f;
        private bool _videoStarted;

        private void Awake()
        {
            if (root == null || inner == null || video == null || player == null || scene == null || ending == null || barTop == null || barBottom == null
                || nameTag == null || hitTitle == null)
                Debug.LogError($"[{nameof(AimShotFx)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnDestroy()
        {
            ReleaseVideo();
        }

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            _pixelScale = context.Layer != null ? context.Layer.PixelScale : 1f;
            CardFxSeat caster = context.CasterSeat;
            CardFxSeat target = context.MainTarget;
            scene.SetTarget(DogKeyOf(context, target));
            scene.StartBuild();
            nameTag.Prepare(caster != null ? caster.Name : string.Empty, target != null ? target.Name : string.Empty);
            hitTitle.Prepare(target != null ? target.Name : string.Empty);
            root.alpha = 0f;
            ending.Clear();
            SetBars(barHeight.x);

            // 영상 준비 (시안 loadeddata, 늦으면 그냥 시작)
            yield return PrepareVideo();

            float since = 0f;
            float time = 0f;
            float outAt = -1f;
            while (true)
            {
                root.alpha = outAt < 0f
                    ? AimEase.Bezier(AimEase.CssEase, Mathf.Clamp01(since / fadeIn))
                    : 1f - AimEase.Bezier(AimEase.CssEase, Mathf.Clamp01((since - outAt) / fadeOut));
                time = SyncVideo(time);
                scene.Draw(Mathf.Min(time, drawUntil));
                nameTag.Draw(time - tagAt, since);
                hitTitle.Draw(time - hitTitleAt);
                inner.anchoredPosition = ShakeAt(time);
                float closing = (time - closeAt) * 1000f;
                DrawClosing(closing);
                if (outAt < 0f && closing > fadeOutMs)
                    outAt = since;
                if (outAt >= 0f && since - outAt >= finishDelay)
                    break;

                yield return null;
                since += Time.deltaTime;
                time += Time.deltaTime;
                // 미리 그리는 판이 아직이면 총알 직전(영상 끝 장면)에서 기다린다
                if (!scene.IsBuilt && time >= scene.BulletAt)
                    time = scene.BulletAt - HoldEpsilon;
            }

            root.alpha = 0f;
            inner.anchoredPosition = Vector2.zero;
            ReleaseVideo();
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            if (context.Layer != null)
            {
                if (context.CasterSeat != null && context.CasterSeat.Anchor != null)
                    context.Layer.Bubble(context.CasterSeat.Anchor, BubbleText, bubbleRise);
                context.Layer.ShakeStage(stageShake, false);
            }
            yield return new WaitForSeconds(postWait);
        }

        #region 영상

        private IEnumerator PrepareVideo()
        {
            video.enabled = false;
            _videoStarted = false;
            if (player.clip == null)
                yield break;
            _texture = new RenderTexture((int)player.clip.width, (int)player.clip.height, 0) { name = "AimShotVideo" };
            player.targetTexture = _texture;
            video.texture = _texture;
            player.Prepare();
            for (float waited = 0f; !player.isPrepared && waited < videoTimeout; waited += Time.deltaTime)
                yield return null;
        }

        // 시안 frame — 총알 전까지 영상을 틀고, 총알부터는 숨긴다. 돌려주는 값은 영상에 맞춘 장면 시간.
        // 시안은 영상을 장면 시계로 찾아가지만(currentTime), Unity 영상은 찾기가 느려 그림이 멈춘다 —
        // 대신 영상이 앞서면(멈칫한 뒤 따라잡을 때) 장면 시계를 영상에 맞춘다 (시안이 소리에 맞추는 방식).
        private float SyncVideo(float time)
        {
            if (!player.isPrepared)
                return time;
            bool filmed = time >= scene.BulletAt;
            bool holding = !scene.IsBuilt && time >= scene.BulletAt - HoldEpsilon * 2f;
            if (filmed || holding)
            {
                if (player.isPlaying)
                    player.Pause();
                if (filmed)
                    video.enabled = false;
                return time;
            }
            if (!_videoStarted)
            {
                player.Play();
                _videoStarted = true;
            }
            player.playbackSpeed = Time.timeScale; // 배속(timeScale)을 영상에도 — 영상은 스스로 실제 시간으로 흐른다
            if (player.frame < 0)
                return time;
            video.enabled = true;
            float clock = (float)player.time;
            return player.isPlaying && clock - time > videoFollow ? clock : time;
        }

        private void ReleaseVideo()
        {
            if (player != null)
            {
                player.Stop();
                player.targetTexture = null;
            }
            if (video != null)
                video.texture = null;
            if (_texture == null)
                return;
            _texture.Release();
            Destroy(_texture);
            _texture = null;
        }

        #endregion

        #region 흔들기 · 마무리

        // 시안 shake — 가장 늦게 시작한 흔들기가 이긴다 (시안 animate 겹침)
        private Vector2 ShakeAt(float time)
        {
            for (int s = shakes.Length - 1; s >= 0; s--)
            {
                Shake shake = shakes[s];
                float k = (time - shake.at) / shake.seconds;
                if (k < 0f || k >= 1f)
                    continue;
                float f = k * (ShakeKeys.Length - 1);
                int i = Mathf.Min(Mathf.FloorToInt(f), ShakeKeys.Length - 2);
                float u = f - i;
                float x0 = ShakeKeys[i] * shake.amount;
                float x1 = ShakeKeys[i + 1] * shake.amount;
                float y0 = (i % 2 == 1 ? ShakeUp : ShakeDown) * ShakeKeys[i] * shake.amount;
                float y1 = ((i + 1) % 2 == 1 ? ShakeUp : ShakeDown) * ShakeKeys[i + 1] * shake.amount;
                return new Vector2(Mathf.Lerp(x0, x1, u), -Mathf.Lerp(y0, y1, u)) * _pixelScale;
            }
            return Vector2.zero;
        }

        // 시안 frame 의 마무리 — 화면이 천천히 가라앉고(아래쪽 가운데가 덜 어둡게) 위아래 띠가 닫히다 가운데 가로 빛 한 번
        private void DrawClosing(float ms)
        {
            if (ms < 0f)
                return;
            float k1 = Mathf.Clamp01(ms / darkenMs);
            float close = Mathf.Pow(Mathf.Clamp01((ms - barTiming.x) / barTiming.y), barTiming.z);
            SetBars(barHeight.x + barHeight.y * close);

            ending.Clear();
            Rect rect = ending.rectTransform.rect;
            float w = rect.width;
            float h = rect.height;
            var center = new Vector2(w * darkShape.x, h * darkShape.y);
            float outer = Mathf.Sqrt(w * w + h * h) * darkShape.w;
            Color inside = darkColor;
            inside.a = darkAlpha.x * k1;
            Color outside = darkColor;
            outside.a = darkAlpha.y * k1;
            ending.Radial(center, h * darkShape.z, outer, inside, outside, outer * 1.2f);

            float line = Mathf.Clamp01((ms - lineTiming.x) / lineTiming.y);
            if (line <= 0f || ms >= lineTiming.z)
                return;
            Color color = lineColor;
            color.a *= Mathf.Sin(line * Mathf.PI);
            float thick = lineHeight * _pixelScale;
            ending.Rect(w * line / 2f, h / 2f - thick / 2f, w * (1f - line), thick, color);
        }

        private void SetBars(float percent)
        {
            float height = ((RectTransform)transform).rect.height * percent * Percent;
            barTop.sizeDelta = new Vector2(barTop.sizeDelta.x, height);
            barBottom.sizeDelta = new Vector2(barBottom.sizeDelta.x, height);
        }

        #endregion

        // 표적 강아지 — 나는 나(na), 다른 사람은 자리 순서대로 초코 · 콩이 · 보리 (시안 시험장 기본 = 초코)
        private static string DogKeyOf(CardFxContext context, CardFxSeat seat)
        {
            if (seat == null || seat.IsMe)
                return FxDog.Keys[0];
            int index = 0;
            foreach (CardFxSeat other in context.Seats)
            {
                if (other == seat)
                    break;
                if (!other.IsMe)
                    index++;
            }
            return FxDog.Keys[1 + index % (FxDog.Keys.Count - 1)];
        }
    }
}
