using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.Fx.Gaepan
{
    /// <summary>
    /// 「5 바퀴 동안」 발바닥 도장 (시안 .gp-five · .dp-five) — 크게 돌며 내려찍힌다(gpStamp).
    /// 시안의 콩 부풀기(gpBean)는 지연이 0 으로 덮여 도장이 나오기 전에 끝나므로 보이지 않는다 — 콩은 처음부터 다 자란 모습.
    /// 크기는 화면 짧은 변(vmin) 비율이고, 화면 크기가 바뀌면 다시 놓는다. 길이 단위는 이 판의 단위 (시안 1px = 1).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GaepanStamp : MonoBehaviour
    {
        [Tooltip("크기 · 자리의 기준이 되는 판 (연출 전체).")]
        [SerializeField] private RectTransform frame;
        [SerializeField] private GaepanPaw paw;
        [Tooltip("가운데 「5」.")]
        [SerializeField] private TMP_Text number;
        [Tooltip("「바퀴 동안」 이름표 (알약 판 · 그림자 · 글자를 담는다). 알약 판은 UIShape — 반지름을 크게 두면 높이 절반으로 깎인다.")]
        [SerializeField] private RectTransform badge;
        [SerializeField] private TMP_Text badgeText;
        [SerializeField] private RectTransform badgeBox;
        [SerializeField] private RectTransform badgeShadow;

        [Header("자리 · 크기 (시안 vmin · px)")]
        [Tooltip("가로 화면에서 도장 가운데 (화면 왼쪽 · 위에서부터 비율).")]
        [SerializeField] private Vector2 placeLandscape = new Vector2(0.82f, 0.26f);
        [Tooltip("세로 화면에서 도장 가운데.")]
        [SerializeField] private Vector2 placePortrait = new Vector2(0.74f, 0.34f);
        [Tooltip("한 변 = clamp(x, y × vmin, z).")]
        [SerializeField] private Vector3 side = new Vector3(120f, 25f, 250f);
        [Tooltip("「5」 크기 = clamp(x, y × vmin, z).")]
        [SerializeField] private Vector3 numberSize = new Vector3(54f, 11.5f, 116f);
        [Tooltip("「5」 가운데 (한 변 비율, 위에서부터).")]
        [SerializeField] private Vector2 numberAt = new Vector2(0.5f, 0.63f);
        [Tooltip("이름표 글자 크기 = clamp(x, y × vmin, z).")]
        [SerializeField] private Vector3 badgeSize = new Vector3(17f, 3.6f, 36f);
        [Tooltip("이름표 가운데 (한 변 비율, 위에서부터).")]
        [SerializeField] private Vector2 badgeAt = new Vector2(0.5f, 1.04f);
        [Tooltip("이름표 안쪽 여백 (em — 왼오른쪽 · 위 · 아래).")]
        [SerializeField] private Vector3 badgePadding = new Vector3(0.6f, 0.05f, 0.12f);
        [Tooltip("이름표 테두리 두께 (px).")]
        [SerializeField] private float badgeBorder = 3f;
        [Tooltip("이름표 그림자가 아래로 밀린 양 (em).")]
        [SerializeField] private float badgeDrop = 0.12f;
        [Tooltip("글자 줄 높이 (em, CSS line-height normal).")]
        [SerializeField] private float lineHeight = 1.25f;
        [Tooltip("판 그림자가 아래로 밀린 양 (vmin, 시안 drop-shadow).")]
        [SerializeField] private float shadowDrop = 0.5f;

        [Header("찍기 (시안 gpStamp)")]
        [Tooltip("시작 시각 (초, 연출 처음부터).")]
        [SerializeField] private float stampDelay = 2.15f;
        [SerializeField] private float stampDuration = 0.45f;
        [SerializeField] private Vector4 stampCurve = new Vector4(0.2f, 1.5f, 0.4f, 1f);
        [Tooltip("가운데 키프레임 자리 (0 ~ 1).")]
        [SerializeField] private float stampSplit = 0.6f;
        [Tooltip("처음 · 가운데 · 끝 기울기 (도, 시계 방향 +).")]
        [SerializeField] private Vector3 stampTurn = new Vector3(40f, 6f, 12f);
        [Tooltip("처음 · 가운데 · 끝 크기.")]
        [SerializeField] private Vector3 stampScale = new Vector3(2.4f, 0.88f, 1f);

        private CanvasGroup _fade;
        private RectTransform _rect;
        private Vector2 _laidOut;
        private float _vmin = 1f;
        private float _side = 1f;

        private void Awake()
        {
            var missing = new List<string>();
            void Check(Object value, string name)
            {
                if (value == null)
                    missing.Add(name);
            }

            Check(frame, nameof(frame));
            Check(paw, nameof(paw));
            Check(number, nameof(number));
            Check(badge, nameof(badge));
            Check(badgeText, nameof(badgeText));
            Check(badgeBox, nameof(badgeBox));
            Check(badgeShadow, nameof(badgeShadow));
            if (missing.Count > 0)
                Debug.LogError($"[{nameof(GaepanStamp)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
            _fade = GetComponent<CanvasGroup>();
            _rect = (RectTransform)transform;
        }

        /// <summary>연출 처음부터 time 초의 모습 (찍기 전이면 감춘다).</summary>
        public void Evaluate(float time)
        {
            Vector2 size = frame.rect.size;
            if (size != _laidOut)
                Layout(size);

            Vector2 step = GaepanEase.TwoStep(stampCurve, (time - stampDelay) / stampDuration, stampSplit);
            float turn = step.x < 1f ? Mathf.LerpUnclamped(stampTurn.x, stampTurn.y, step.y) : Mathf.LerpUnclamped(stampTurn.y, stampTurn.z, step.y);
            float scale = step.x < 1f ? Mathf.LerpUnclamped(stampScale.x, stampScale.y, step.y) : Mathf.LerpUnclamped(stampScale.y, stampScale.z, step.y);
            _fade.alpha = step.x < 1f ? Mathf.Clamp01(step.y) : 1f;
            _rect.localRotation = Quaternion.Euler(0f, 0f, -turn);
            _rect.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>판 크기에 맞춰 도장 · 「5」 · 이름표를 놓는다.</summary>
        public void Layout(Vector2 frameSize)
        {
            if (_rect == null)
                _rect = (RectTransform)transform;
            _laidOut = frameSize;
            _vmin = Mathf.Min(frameSize.x, frameSize.y) / 100f;
            _side = Mathf.Clamp(side.y * _vmin, side.x, side.z);
            Vector2 place = frameSize.x >= frameSize.y ? placeLandscape : placePortrait;
            _rect.anchorMin = _rect.anchorMax = new Vector2(place.x, 1f - place.y);
            _rect.anchoredPosition = Vector2.zero;
            _rect.sizeDelta = new Vector2(_side, _side);
            paw.SetShadowDrop(shadowDrop * _vmin / _side * 100f);

            number.fontSize = Mathf.Clamp(numberSize.y * _vmin, numberSize.x, numberSize.z);
            number.rectTransform.anchoredPosition = At(numberAt);

            float em = Mathf.Clamp(badgeSize.y * _vmin, badgeSize.x, badgeSize.z);
            badgeText.fontSize = em;
            float textWidth = badgeText.GetPreferredValues(badgeText.text, 0f, 0f).x;
            var box = new Vector2(textWidth + badgePadding.x * 2f * em + badgeBorder * 2f, (lineHeight + badgePadding.y + badgePadding.z) * em + badgeBorder * 2f);
            badge.anchoredPosition = At(badgeAt);
            badge.sizeDelta = box;
            badgeBox.sizeDelta = box;
            badgeShadow.sizeDelta = box;
            badgeShadow.anchoredPosition = new Vector2(0f, -badgeDrop * em);
            // 글자 줄은 위 · 아래 여백이 다르다 — 가운데에서 그 차이 절반만큼 올린다
            badgeText.rectTransform.anchoredPosition = new Vector2(0f, (badgePadding.z - badgePadding.y) / 2f * em);
        }

        // 한 변 비율(왼쪽 위 기준) → 도장 가운데 기준 자리
        private Vector2 At(Vector2 ratio) => new Vector2((ratio.x - 0.5f) * _side, (0.5f - ratio.y) * _side);
    }
}
