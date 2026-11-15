using TMPro;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 제목 「개판 오분전!」 의 글자 하나 — 스티커처럼 흰 바깥 테두리 → 짙은 밤색 선 → 두 톤 채움 (시안 .gp-title .ch).
    /// 세 겹은 같은 글자를 굵기 · 색만 바꿔 겹친 TMP 셋이다. 튀어나오기(gpPop) 뒤 살랑거리기(gpBob)를 시각으로 그린다.
    /// 길이 단위 em = 이 글자의 크기 (<see cref="Place"/> 로 받는다).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GaepanLetter : MonoBehaviour
    {
        [Tooltip("흰 바깥 테두리 겹 (시안 ::before — 굵기 · 그림자는 머티리얼).")]
        [SerializeField] private TMP_Text white;
        [Tooltip("짙은 밤색 선 겹 (시안 ::after).")]
        [SerializeField] private TMP_Text dark;
        [Tooltip("두 톤 채움 겹 (시안 b — 색은 머티리얼의 면 텍스처).")]
        [SerializeField] private TMP_Text fill;
        [Tooltip("기본 기울기 (시안 --r, 도 — 시계 방향 +).")]
        [SerializeField] private float tilt;
        [Tooltip("낱말 안에서 몇째 글자인지 (시안 --i).")]
        [SerializeField] private int order;
        [Tooltip("흰 겹 · 밤색 겹이 아래로 밀린 양 (em, 시안 translate — 아래 +).")]
        [SerializeField] private Vector2 whiteShift = new Vector2(0.02f, 0.06f);
        [SerializeField] private Vector2 darkShift = new Vector2(0.02f, 0.075f);

        [Header("튀어나오기 (시안 gpPop)")]
        [Tooltip("길이 (초).")]
        [SerializeField] private float popDuration = 0.5f;
        [Tooltip("글자마다 늦는 시간 (초).")]
        [SerializeField] private float popStagger = 0.075f;
        [Tooltip("곡선 (CSS cubic-bezier).")]
        [SerializeField] private Vector4 popCurve = new Vector4(0.2f, 1.7f, 0.4f, 1f);
        [Tooltip("가운데 키프레임 자리 (0 ~ 1).")]
        [SerializeField] private float popSplit = 0.65f;
        [Tooltip("처음 · 가운데 아래로 밀린 양 (em).")]
        [SerializeField] private Vector2 popDrop = new Vector2(0.45f, -0.06f);
        [Tooltip("처음 · 가운데 크기.")]
        [SerializeField] private Vector2 popScale = new Vector2(0.2f, 1.16f);
        [Tooltip("처음 · 가운데 더 기우는 각 (도).")]
        [SerializeField] private Vector2 popTurn = new Vector2(-25f, 6f);

        [Header("살랑거리기 (시안 gpBob)")]
        [Tooltip("튀어나오기 시작부터 살랑거리기 시작까지 (초).")]
        [SerializeField] private float bobDelay = 0.7f;
        [Tooltip("글자마다 늦는 시간 (초).")]
        [SerializeField] private float bobStagger = 0.11f;
        [Tooltip("한 번 가는 길이 (초) — 갔다 오기를 되풀이한다.")]
        [SerializeField] private float bobDuration = 0.9f;
        [SerializeField] private Vector4 bobCurve = new Vector4(0.42f, 0f, 0.58f, 1f);
        [Tooltip("올라가는 양 (em).")]
        [SerializeField] private float bobLift = 0.045f;
        [Tooltip("끝에서 기울기 = 기본 기울기 × 이 값.")]
        [SerializeField] private float bobTilt = -0.6f;

        private CanvasGroup _fade;
        private RectTransform _rect;
        private Vector2 _center;
        private float _em = 1f;

        private void Awake()
        {
            if (white == null)
                Debug.LogError($"[{nameof(GaepanLetter)}] {nameof(white)} 참조가 비어 있습니다.", this);
            if (dark == null)
                Debug.LogError($"[{nameof(GaepanLetter)}] {nameof(dark)} 참조가 비어 있습니다.", this);
            if (fill == null)
                Debug.LogError($"[{nameof(GaepanLetter)}] {nameof(fill)} 참조가 비어 있습니다.", this);
            _fade = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;
            _center = _rect.anchoredPosition;
        }

        /// <summary>글자의 너비 (크기 1 기준 — 글자 크기를 곱해 쓴다).</summary>
        public float Advance()
        {
            float size = fill.fontSize;
            return size > 0f ? fill.GetPreferredValues(fill.text, 0f, 0f).x / size : 1f;
        }

        /// <summary>글자 칸을 놓는다 — center = 칸 가운데 (부모 기준), width = 칸 너비, em = 글자 크기.</summary>
        public void Place(Vector2 center, float width, float em)
        {
            if (_rect == null)
                _rect = (RectTransform)transform;
            _center = center;
            _em = em;
            _rect.anchoredPosition = center;
            _rect.sizeDelta = new Vector2(width, em);
            PlaceLayer(white, whiteShift, em);
            PlaceLayer(dark, darkShift, em);
            PlaceLayer(fill, Vector2.zero, em);
        }

        /// <summary>낱말이 시작한 뒤 time 초의 모습 (시작 전이면 감춘다).</summary>
        public void Evaluate(float time)
        {
            float popStart = order * popStagger;
            Vector2 step = GaepanEase.TwoStep(popCurve, (time - popStart) / popDuration, popSplit);
            float drop, scale, turn, alpha;
            if (step.x < 1f)
            {
                drop = Mathf.LerpUnclamped(popDrop.x, popDrop.y, step.y);
                scale = Mathf.LerpUnclamped(popScale.x, popScale.y, step.y);
                turn = tilt + Mathf.LerpUnclamped(popTurn.x, popTurn.y, step.y);
                alpha = Mathf.Clamp01(step.y);
            }
            else
            {
                drop = Mathf.LerpUnclamped(popDrop.y, 0f, step.y);
                scale = Mathf.LerpUnclamped(popScale.y, 1f, step.y);
                turn = tilt + Mathf.LerpUnclamped(popTurn.y, 0f, step.y);
                alpha = 1f;
            }

            // 살랑거리기가 시작되면 모양(transform)은 그쪽이 덮는다 — 투명도는 튀어나오기 그대로
            float bob = time - bobDelay - order * bobStagger;
            if (bob >= 0f)
            {
                float cycles = bob / bobDuration;
                int round = Mathf.FloorToInt(cycles);
                float progress = cycles - round;
                float e = GaepanEase.CubicBezier(bobCurve, round % 2 == 0 ? progress : 1f - progress);
                drop = -bobLift * e;
                scale = 1f;
                turn = Mathf.Lerp(tilt, tilt * bobTilt, e);
            }

            _fade.alpha = alpha;
            _rect.anchoredPosition = _center + new Vector2(0f, -drop * _em);
            _rect.localScale = new Vector3(scale, scale, 1f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, -turn);
        }

        // 겹은 칸과 같은 크기, 왼쪽 위 맞춤 — 칸 위에서 첫 줄 위끝(ascender)까지가 글자 윗선이 된다 (CSS line-height 1)
        private static void PlaceLayer(TMP_Text layer, Vector2 shift, float em)
        {
            layer.fontSize = em;
            var rect = layer.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = new Vector2(shift.x, -shift.y) * em;
        }
    }
}
