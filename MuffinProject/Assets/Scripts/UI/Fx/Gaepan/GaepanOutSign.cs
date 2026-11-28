using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 처형 장면 끝의 「(이름) 탈락!」 알약 (시안 .gp-out · .dp-out) — 크게 기울어 있다가 제자리로 찍힌다(dpOut).
    /// 크기는 화면 짧은 변(vmin) 비율이고, 화면 크기가 바뀌면 다시 놓는다. 길이 단위는 이 판의 단위 (시안 1px = 1).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GaepanOutSign : MonoBehaviour
    {
        private const string TextFormat = "<color=#{0}>{1}</color> 탈락!";
        private const string TextWithoutName = "탈락!";

        [Tooltip("크기 · 자리의 기준이 되는 판 (연출 전체).")]
        [SerializeField] private RectTransform frame;
        [SerializeField] private TMP_Text label;
        [Tooltip("분홍 알약 판 (UIShape — 반지름을 크게 두면 높이 절반으로 깎인다).")]
        [SerializeField] private RectTransform box;
        [Tooltip("알약 둘레의 흰 띠.")]
        [SerializeField] private RectTransform ring;
        [Tooltip("아래로 밀린 잉크 그림자.")]
        [SerializeField] private RectTransform shadow;

        [Header("자리 · 크기 (시안 vmin · em)")]
        [Tooltip("알약 가운데 (화면 왼쪽 · 위에서부터 비율).")]
        [SerializeField] private Vector2 place = new Vector2(0.5f, 0.3f);
        [Tooltip("글자 크기 = clamp(x, y × vmin, z).")]
        [SerializeField] private Vector3 fontSize = new Vector3(30f, 11f, 120f);
        [Tooltip("안쪽 여백 (em — 왼오른쪽 · 위 · 아래).")]
        [SerializeField] private Vector3 padding = new Vector3(0.5f, 0.08f, 0.14f);
        [Tooltip("테두리 두께 (em).")]
        [SerializeField] private float border = 0.06f;
        [Tooltip("흰 띠 두께 (em).")]
        [SerializeField] private float ringWidth = 0.07f;
        [Tooltip("그림자가 아래로 밀린 양 (em).")]
        [SerializeField] private float shadowDrop = 0.12f;
        [Tooltip("글자 줄 높이 (em).")]
        [SerializeField] private float lineHeight = 1.1f;
        [Tooltip("이름 색.")]
        [SerializeField] private Color nameColor = new Color32(0xFF, 0xE2, 0x7A, 0xFF);

        [Header("찍기 (시안 dpOut)")]
        [SerializeField] private float stampDuration = 0.42f;
        [SerializeField] private Vector4 stampCurve = new Vector4(0.2f, 1.4f, 0.4f, 1f);
        [Tooltip("처음 · 끝 기울기 (도, 시계 반대 방향 +).")]
        [SerializeField] private Vector2 stampTurn = new Vector2(30f, 10f);
        [Tooltip("처음 · 끝 크기.")]
        [SerializeField] private Vector2 stampScale = new Vector2(3f, 1f);

        private CanvasGroup _fade;
        private RectTransform _rect;
        private Vector2 _laidOut;

        private void Awake()
        {
            var missing = new List<string>();
            void Check(Object value, string name)
            {
                if (value == null)
                    missing.Add(name);
            }

            Check(frame, nameof(frame));
            Check(label, nameof(label));
            Check(box, nameof(box));
            Check(ring, nameof(ring));
            Check(shadow, nameof(shadow));
            if (missing.Count > 0)
                Debug.LogError($"[{nameof(GaepanOutSign)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
            _fade = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;
            _fade.alpha = 0f;
        }

        /// <summary>탈락한 사람 이름을 넣는다. 비면 「탈락!」 만.</summary>
        public void SetName(string victimName)
        {
            // 이름이 글자 꾸밈 태그로 읽히지 않게 꺾쇠는 뺀다 (시안도 같다)
            string plain = string.IsNullOrEmpty(victimName) ? string.Empty : victimName.Replace("<", string.Empty).Replace(">", string.Empty);
            label.text = plain.Length == 0 ? TextWithoutName : string.Format(TextFormat, ColorUtility.ToHtmlStringRGB(nameColor), plain);
            _laidOut = Vector2.zero;
        }

        /// <summary>찍히기 시작한 뒤 time 초의 모습 (음수면 감춘다).</summary>
        public void Evaluate(float time)
        {
            if (time < 0f)
            {
                _fade.alpha = 0f;
                return;
            }

            Vector2 size = frame.rect.size;
            if (size != _laidOut)
                Layout(size);
            float progress = Mathf.Clamp01(time / stampDuration);
            float eased = GaepanEase.CubicBezier(stampCurve, progress);
            _fade.alpha = Mathf.Clamp01(eased);
            _rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(stampTurn.x, stampTurn.y, eased));
            float scale = Mathf.LerpUnclamped(stampScale.x, stampScale.y, eased);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }

        // 판 크기에 맞춰 글자 · 알약 · 흰 띠 · 그림자를 놓는다
        private void Layout(Vector2 frameSize)
        {
            _laidOut = frameSize;
            float vmin = Mathf.Min(frameSize.x, frameSize.y) / 100f;
            float em = Mathf.Clamp(fontSize.y * vmin, fontSize.x, fontSize.z);
            _rect.anchorMin = _rect.anchorMax = new Vector2(place.x, 1f - place.y);
            _rect.anchoredPosition = Vector2.zero;

            label.fontSize = em;
            float textWidth = label.GetPreferredValues(label.text, 0f, 0f).x;
            var size = new Vector2(textWidth + (padding.x + border) * 2f * em, (lineHeight + padding.y + padding.z + border * 2f) * em);
            Vector2 outer = size + Vector2.one * (ringWidth * 2f * em);
            _rect.sizeDelta = size;
            box.sizeDelta = size;
            ring.sizeDelta = outer;
            shadow.sizeDelta = outer;
            shadow.anchoredPosition = new Vector2(0f, -shadowDrop * em);
            // 글자 줄은 위 · 아래 여백이 다르다 — 가운데에서 그 차이 절반만큼 올린다
            label.rectTransform.anchoredPosition = new Vector2(0f, (padding.z - padding.y) / 2f * em);
        }
    }
}
