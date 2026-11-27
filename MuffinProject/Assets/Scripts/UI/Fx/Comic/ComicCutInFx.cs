using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Comic
{
    /// <summary>
    /// 만화 컷인(시안 CI2) 틀 — 카드 한 장의 3 단계. 화면 전체를 덮고, 장면(<see cref="ComicScene"/>)을 1280×720 설계 판에 그려 화면에 꽉 채운다 (시안 frame · play).
    /// 틀이 하는 것: 밑바탕 색 · 설계 판 꽉 채우기(가로 화면 — 짧은 쪽이 넘치게) · 처음 0.35 초 살짝 확대 1.05 → 1 · 판 흔들림(hits) · 위아래 레터박스 ·
    /// 왼쪽 아래 제목 + 한마디(<see cref="ComicTitleBlock"/>) · 종이결(<see cref="ComicGrain"/>) · 들어오기/나가기 색 닦기(<see cref="ComicWipe"/>).
    /// 반투명으로 겹치지 않는다 — 닦기 띠가 화면을 다 덮은 채로 나타나 걷히고, 끝에는 다 덮은 채로 잠깐 멈췄다 한 번에 사라진다 (시안의 0.09 초 나타나기 · 0.16 초 사라지기는 게임 화면 위에 장면이 유령처럼 겹쳐 뺐다).
    /// 3 단계 길이 = 장면 길이 + 0.16 초 (시안 SPECIAL_MS = len + 80ms, 그 뒤 special2 가 80ms 더 기다린다) — 덮고 멈춤 0.1 + 빈 틈 0.06.
    /// 4 단계 장식은 장면의 <see cref="ComicScene.PostRoutine"/>.
    /// 프리팹: <c>Prefab/UI/CardFx/Comic/ComicCutIn.prefab</c> (이 컴포넌트가 루트). 카드마다 그 변형 <c>Resources/CardFx/Card_&lt;ID&gt;.prefab</c> 의 Stage 아래에 장면을 하나 둔다 — 자세한 것은 <see cref="ComicScene"/>.
    /// 세로 화면(시안 NAR — 가운데 띠)은 만들지 않았다 (게임은 가로 화면만).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ComicCutInFx : CardFx
    {
        [Header("겹")]
        [Tooltip("장면 밑바탕 (화면 전체, 색 = 장면 bgCol).")]
        [SerializeField] private Image background;
        [Tooltip("설계 판 (1280×720, 가운데) — 꽉 채우기 · 확대 · 흔들림이 여기에 걸린다. 장면은 이 아래.")]
        [SerializeField] private RectTransform stage;
        [Tooltip("제목 + 한마디 (화면 왼쪽 아래 기준).")]
        [SerializeField] private ComicTitleBlock titleBlock;
        [Tooltip("종이결.")]
        [SerializeField] private ComicGrain grain;
        [Tooltip("먹 닦기 판 (1280×720, 가운데 — 꽉 채우기만 걸린다).")]
        [SerializeField] private RectTransform wipeSpace;
        [Tooltip("먹 닦기.")]
        [SerializeField] private ComicWipe wipe;

        [Header("움직임 (시안 frame · play)")]
        [Tooltip("처음 확대 (시안 1 + 0.05).")]
        [SerializeField] private float zoom = 0.05f;
        [Tooltip("확대가 풀리는 시간 (초, 시안 0.35).")]
        [SerializeField] private float zoomTime = 0.35f;
        [Tooltip("장면 끝에 닦기 띠가 화면을 다 덮은 채 멈춰 있는 시간 (초) — 그 뒤 한 번에 사라진다.")]
        [SerializeField] private float coverHold = 0.1f;
        [Tooltip("사라진 뒤 4 단계까지 빈 틈 (초).")]
        [SerializeField] private float endPause = 0.06f;
        [Tooltip("흔들림 하나가 이어지는 시간 (초, 시안 0.45).")]
        [SerializeField] private float shakeWindow = 0.45f;
        [Tooltip("흔들림이 잦아드는 빠르기 (시안 exp(−k·12)).")]
        [SerializeField] private float shakeDamping = 12f;
        [Tooltip("흔들림 빠르기 — 가로 · 세로 (시안 sin(t·93) · cos(t·81)).")]
        [SerializeField] private Vector2 shakeFrequency = new Vector2(93f, 81f);
        [Tooltip("세로 흔들림 비율 (시안 0.8).")]
        [SerializeField] private float shakeVertical = 0.8f;

        [Header("제목 자리 (시안 titleBlock)")]
        [Tooltip("보이는 화면 왼쪽에서 (설계 px, 시안 60).")]
        [SerializeField] private float titleLeft = 60f;
        [Tooltip("레터박스 위에서 (설계 px, 시안 74).")]
        [SerializeField] private float titleRise = 74f;
        [Tooltip("레터박스 높이 / 화면 높이 (시안 0.055) — 레터박스 오브젝트 앵커와 같게.")]
        [SerializeField] private float letterbox = 0.055f;

        private CanvasGroup _group;
        private ComicScene _scene;
        private ComicSticker[] _stickers;
        private ComicContext _context;

        public override IEnumerator PlayRoutine(CardFxContext context)
        {
            if (_scene == null)
            {
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 Stage 아래에 {nameof(ComicScene)} 이 없다");
                yield break;
            }
            _context = new ComicContext(context, _scene.Hits);
            _group.alpha = 0f;
            ApplyLook(context);
            _scene.Begin(_context);
            foreach (ComicSticker sticker in _stickers)
                sticker.StartBuild();
            foreach (ComicSticker sticker in _stickers)
            {
                // 꺼 둔 스티커는 만들지 않는다 — 기다리지 않는다
                while (sticker.isActiveAndEnabled && !sticker.IsReady)
                    yield return null;
            }

            float length = _scene.Length;
            float end = length + coverHold;
            float start = Time.time;
            _group.alpha = 1f;
            while (true)
            {
                float t = Time.time - start;
                if (t >= end)
                    break;
                Render(Mathf.Min(t, length), t);
                yield return null;
            }
            _group.alpha = 0f;
            yield return WaitRoutine(endPause);
        }

        public override IEnumerator PlayPostRoutine(CardFxContext context)
        {
            if (_scene == null)
                yield break;
            if (_context == null || _context.Fx != context)
                _context = new ComicContext(context, _scene.Hits);
            yield return _scene.PostRoutine(_context);
        }

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (background == null)
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 {nameof(background)} 가 비었다");
            if (titleBlock == null)
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 {nameof(titleBlock)} 가 비었다");
            if (grain == null)
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 {nameof(grain)} 가 비었다");
            if (wipeSpace == null)
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 {nameof(wipeSpace)} 가 비었다");
            if (wipe == null)
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 {nameof(wipe)} 가 비었다");
            if (stage == null)
            {
                Debug.LogError($"[{nameof(ComicCutInFx)}] {name} 의 {nameof(stage)} 가 비었다");
                _stickers = System.Array.Empty<ComicSticker>();
                return;
            }
            _scene = stage.GetComponentInChildren<ComicScene>(true);
            _stickers = stage.GetComponentsInChildren<ComicSticker>(true);
        }

        private void Start()
        {
            // 테 만들기는 오래 걸릴 수 있다 — 불려 오자마자 시작해 둔다 (Awake 에서는 아직 켜지기 전이라 못 한다)
            foreach (ComicSticker sticker in _stickers)
                sticker.StartBuild();
        }

        private void ApplyLook(CardFxContext context)
        {
            background.color = _scene.Background;
            wipe.Tint = _scene.Accent;
            string title = string.IsNullOrEmpty(_scene.Title) ? context.CardName : _scene.Title;
            titleBlock.SetTexts(title, _scene.Tagline, _scene.Accent, _scene.Deep, _scene.Light, !_scene.NoTitle, !_scene.NoTitle && !_scene.NoBand, _scene.PlainTag);
            foreach (ComicCard card in stage.GetComponentsInChildren<ComicCard>(true))
            {
                if (card.Back == null)
                    card.Back = context.CardBack;
            }
        }

        // t = 그릴 장면 시각 (끝에서 멈춘다), clock = 흐른 시간 (흔들림은 끝나도 이어진다 — 시안 shk(t))
        private void Render(float t, float clock)
        {
            Rect rect = ((RectTransform)transform).rect;
            float s = Mathf.Max(rect.width / ComicKit.Width, rect.height / ComicKit.Height);
            float z = 1f + zoom * (1f - ComicKit.Eo(t / zoomTime));
            Vector2 shake = ShakeAt(clock);
            stage.localScale = new Vector3(s * z, s * z, 1f);
            stage.anchoredPosition = new Vector2(shake.x * s, -shake.y * s);
            wipeSpace.localScale = new Vector3(s, s, 1f);
            var titleRect = (RectTransform)titleBlock.transform;
            titleRect.localScale = new Vector3(s, s, 1f);
            titleRect.anchoredPosition = new Vector2(titleLeft * s, rect.height * letterbox + titleRise * s);

            _scene.Draw(t, _context);
            titleBlock.Draw(t - _scene.TitleAt);
            grain.SetTime(t);
            wipe.Set(t, _scene.Length);
        }

        private Vector2 ShakeAt(float t)
        {
            float a = 0f;
            IReadOnlyList<Vector2> hits = _context.Hits;
            for (int i = 0; i < hits.Count; i++)
            {
                float k = t - hits[i].x;
                if (k > 0f && k < shakeWindow)
                    a += hits[i].y * Mathf.Exp(-k * shakeDamping);
            }
            if (a == 0f)
                return Vector2.zero;
            return new Vector2(Mathf.Sin(t * shakeFrequency.x) * a, Mathf.Cos(t * shakeFrequency.y) * a * shakeVertical);
        }

        private static IEnumerator WaitRoutine(float seconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
                yield return null;
        }
    }
}
