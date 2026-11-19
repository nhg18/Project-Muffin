using System.Collections;
using Chapchu.UI.Fx;
using Chapchu.UI.Fx.Common;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 찹츄 선언 연출 (10-ui.md 5-2 #1 · 7절 — 프로토타입 v2 bigMoment): 화면 가운데 집중선 + 큰 말풍선 「찹츄!」 + 가운데 묶음 흔들림.
    /// 모든 화면에 나온다. 입력을 막지 않고 연출 큐도 세우지 않는다 — 앞 선언이 아직 나오는 중일 때만 그것이 끝나길 기다린다. 기본은 비활성.
    /// </summary>
    public class ChapchuShoutView : MonoBehaviour
    {
        [Tooltip("집중선 — 연출마다 다시 뽑는다.")]
        [SerializeField] private UISpeedLines lines;
        [SerializeField] private CanvasGroup linesGroup;
        [Tooltip("큰 말풍선 — 글자 길이에 맞춰 넓어진다 (ContentSizeFitter).")]
        [SerializeField] private RectTransform bubble;
        [SerializeField] private CanvasGroup bubbleGroup;
        [SerializeField] private TMP_Text label;
        [Tooltip("흔들 판 — 가운데 묶음(Center).")]
        [SerializeField] private FxShaker shaker;

        [Header("말풍선 (시안 fxShout 1.5초)")]
        [Tooltip("말풍선이 나왔다 사라지기까지의 초.")]
        [SerializeField] private float shoutSeconds = 1.5f;
        [Tooltip("튀어나오는 구간이 끝나는 때 (전체에 대한 비율) — 0.2배 → 넘친 크기.")]
        [SerializeField] private float popEnd = 0.2f;
        [Tooltip("제 크기로 돌아오는 때 (비율).")]
        [SerializeField] private float settleEnd = 0.32f;
        [Tooltip("사라지기 시작하는 때 (비율).")]
        [SerializeField] private float fadeStart = 0.82f;
        [Tooltip("처음 크기 배율 · 넘친 크기 배율 · 사라질 때 크기 배율.")]
        [SerializeField] private float startScale = 0.2f;
        [SerializeField] private float popScale = 1.14f;
        [SerializeField] private float endScale = 1.04f;
        [Tooltip("처음 · 넘쳤을 때 · 돌아왔을 때 Z 회전 (도). 시계 방향 기울기가 음수 — 시안 −10° · 3° · −2° 의 부호를 뒤집은 값.")]
        [SerializeField] private float startAngle = 10f;
        [SerializeField] private float popAngle = -3f;
        [SerializeField] private float settleAngle = 2f;
        [Tooltip("흰 글자 둘레 잉크 외곽선 두께 (TMP outline, 0~1) — 시안 92 글자에 잉크 선 12.")]
        [SerializeField] private float outline = 0.25f;
        [SerializeField] private Color outlineColor = new Color32(0x2a, 0x24, 0x20, 0xFF);
        [Tooltip("말풍선이 이보다 넓으면(긴 이름) 이 폭에 맞춰 줄인다.")]
        [SerializeField] private float maxWidth = 1760f;

        [Header("집중선 (시안 fxSpeed 1초)")]
        [SerializeField] private float linesSeconds = 1f;
        [Tooltip("집중선이 다 나타나는 때 · 사라지기 시작하는 때 (비율).")]
        [SerializeField] private float linesInEnd = 0.12f;
        [SerializeField] private float linesOutStart = 0.6f;

        [Header("흔들림 (시안 fxShake 9 · 520 → × 2)")]
        [Tooltip("처음 흔들리는 폭 — 점점 줄어든다.")]
        [SerializeField] private float shakeAmount = 18f;
        [SerializeField] private float shakeMilliseconds = 520f;
        [Tooltip("세로 흔들림 = 가로의 이 비율.")]
        [SerializeField] private float shakeVertical = 0.3f;

        private const int ShakeSteps = 8;

        private bool _playing;

        private void Awake()
        {
            if (lines == null || linesGroup == null || bubble == null || bubbleGroup == null || label == null || shaker == null)
                Debug.LogError($"[{nameof(ChapchuShoutView)}] 참조가 비어 있습니다.", this);
        }

        /// <summary>text 를 외친다. 앞 연출이 아직 나오는 중이면 끝난 뒤에 시작한다 — 시작한 뒤에는 기다리지 않는다.</summary>
        public IEnumerator PlayRoutine(string text)
        {
            while (_playing)
                yield return null;
            Play(text);
        }

        /// <summary>바로 거둔다 (새 판 · 화면을 떠날 때).</summary>
        public void Hide()
        {
            DOTween.Kill(this);
            _playing = false;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            DOTween.Kill(this);
            _playing = false;
        }

        private void Play(string text)
        {
            _playing = true;
            gameObject.SetActive(true);

            label.text = text;
            label.outlineWidth = outline;
            label.outlineColor = outlineColor;
            LayoutRebuilder.ForceRebuildLayoutImmediate(bubble);
            float fit = Mathf.Min(1f, maxWidth / Mathf.Max(1f, bubble.rect.width));

            lines.Shuffle();
            linesGroup.alpha = 0f;
            ApplyBubble(0f, fit);

            // 시안: 불투명도 0 → 12% 에 1 → 60% 까지 1 → 0 (선형)
            DOVirtual.Float(0f, 1f, linesSeconds, t =>
                    linesGroup.alpha = t < linesInEnd ? t / linesInEnd : t < linesOutStart ? 1f : 1f - (t - linesOutStart) / (1f - linesOutStart))
                .SetEase(Ease.Linear).SetId(this).SetLink(gameObject);

            // 시안은 전체 진행에 ease-out 하나를 건다 (구간마다가 아니라)
            DOVirtual.Float(0f, 1f, shoutSeconds, t => ApplyBubble(t, fit))
                .SetEase(Ease.OutQuad).SetId(this).SetLink(gameObject)
                .OnComplete(() =>
                {
                    _playing = false;
                    gameObject.SetActive(false);
                });

            // 시안 fxShake — 좌우로 번갈아, 폭이 한 걸음마다 1/8 씩 줄어 제자리로
            var keys = new Vector2[ShakeSteps + 1];
            for (int i = 0; i < ShakeSteps; i++)
            {
                float amount = shakeAmount * (1f - i / (float)ShakeSteps);
                keys[i] = new Vector2((i % 2 == 1 ? -1f : 1f) * amount, (i % 3 - 1) * amount * shakeVertical);
            }
            keys[ShakeSteps] = Vector2.zero;
            shaker.ShakeKeys(keys, shakeMilliseconds);
        }

        // 시안 fxShout: 0.2배 · −10° · 투명 → 20% 에 1.14배 · 3° · 불투명 → 32% 에 1배 · −2° → 82% 에 1배 · 0° → 1.04배 · 투명
        private void ApplyBubble(float t, float fit)
        {
            float scale, angle, alpha = 1f;
            if (t < popEnd)
            {
                float k = t / popEnd;
                scale = Mathf.Lerp(startScale, popScale, k);
                angle = Mathf.Lerp(startAngle, popAngle, k);
                alpha = k;
            }
            else if (t < settleEnd)
            {
                float k = (t - popEnd) / (settleEnd - popEnd);
                scale = Mathf.Lerp(popScale, 1f, k);
                angle = Mathf.Lerp(popAngle, settleAngle, k);
            }
            else if (t < fadeStart)
            {
                scale = 1f;
                angle = Mathf.Lerp(settleAngle, 0f, (t - settleEnd) / (fadeStart - settleEnd));
            }
            else
            {
                float k = (t - fadeStart) / (1f - fadeStart);
                scale = Mathf.Lerp(1f, endScale, k);
                angle = 0f;
                alpha = 1f - k;
            }

            bubble.localScale = Vector3.one * (scale * fit);
            bubble.localRotation = Quaternion.Euler(0f, 0f, angle);
            bubbleGroup.alpha = alpha;
        }
    }
}
