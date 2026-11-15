using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 띠 컷인의 띠 (시안 .ci-band · .ci-lines) — 카드 색 넣기 · 밀기 · 줄무늬 흐르기를 맡는다. 그림은 아래 조각이 그린다 (뒤에서 앞으로):
    /// 번지는 빛(<see cref="UIShape"/> 번짐, 시안 0 0 60px glow) → 빛 테(시안 0 0 0 7px glow) → 검은 테(시안 0 0 0 4px #000) → 면(c1, 마스크)
    /// → 면 안에 위아래 가장자리 그라데이션(Common ui_fade + c2) · 세로 줄무늬(BandStripe 프리팹).
    /// 테 · 빛은 면 밖 고리라 판째 흐려질 때(CanvasGroup) 면 너머로 비치지 않는다. 면이 마스크라 테를 면과 한 도형으로 그리면 줄무늬가 테를 덮는다.
    /// 번지는 빛은 그 바깥 절반만 그린다 — 테 두께 · 번짐 = blur (blur × 2 번짐의 면 밖 절반과 같다).
    /// 기울기(CSS skewY)는 조각마다 붙은 <see cref="BandSkew"/> 가 <see cref="Slope"/> 로 민다. 줄무늬는 세로 그대로라 기울이지 않고 면 마스크가 자른다.
    /// 줄무늬 판은 띠 왼쪽 끝에서 16 주기 — 띠 너비 약 3500(화면 너비 약 2900, 21:9 까지)을 덮는다.
    /// </summary>
    public class CutInBand : MonoBehaviour
    {
        [Tooltip("기울기 (도) — 오른쪽이 올라간다 (시안 skewY(-8deg)).")]
        [SerializeField] private float skew = 8f;
        [Tooltip("무늬 한 주기 — 줄무늬 프리팹 한 칸 너비 (시안 156px).")]
        [SerializeField] private float stripePeriod = 234f;

        [Header("조각")]
        [Tooltip("면 (시안 c1) — 가장자리 그라데이션 · 줄무늬를 자르는 마스크.")]
        [SerializeField] private UIShape face;
        [Tooltip("위아래 가장자리 그라데이션 (시안 c2 → c1 22%) — 색만 바꾼다.")]
        [SerializeField] private Image[] edges;
        [Tooltip("빛 테 · 번지는 빛 (시안 glow) — 테 색만 바꾸고 투명도는 그림 값 그대로.")]
        [SerializeField] private UIShape[] glows;
        [Tooltip("줄무늬 판 (시안 .ci-lines) — 왼쪽으로 밀어 흐르게 한다.")]
        [SerializeField] private RectTransform lines;

        private RectTransform _rect;
        private Vector2 _origin;
        private Vector2 _linesOrigin;
        private bool _hasOrigin;
        private float _slide;
        private float _stripeShift;

        /// <summary>기울기 — 가로 1 에 세로로 오르는 양 (tan).</summary>
        public float Slope => Mathf.Tan(skew * Mathf.Deg2Rad);

        /// <summary>띠 너비 (화면 단위).</summary>
        public float Width => Rect.rect.width;

        /// <summary>무늬 한 주기 (화면 단위).</summary>
        public float StripePeriod => stripePeriod;

        /// <summary>기울이기 전 가로로 민 거리 (화면 단위, 왼쪽 −). 시안 translateX — 띠가 제 기울기를 따라 움직인다.</summary>
        public float Slide
        {
            get => _slide;
            set
            {
                KeepOrigin();
                _slide = value;
                Rect.anchoredPosition = _origin + new Vector2(value, Slope * value);
            }
        }

        /// <summary>줄무늬를 왼쪽으로 민 거리 (화면 단위). 한 주기마다 같은 모양.</summary>
        public float StripeShift
        {
            get => _stripeShift;
            set
            {
                KeepOrigin();
                _stripeShift = value;
                lines.anchoredPosition = _linesOrigin + Vector2.left * value;
            }
        }

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        private void Awake()
        {
            var missing = new List<string>();
            if (face == null)
                missing.Add(nameof(face));
            if (edges == null || edges.Length == 0 || Array.IndexOf(edges, null) >= 0)
                missing.Add(nameof(edges));
            if (glows == null || glows.Length == 0 || Array.IndexOf(glows, null) >= 0)
                missing.Add(nameof(glows));
            if (lines == null)
                missing.Add(nameof(lines));
            if (missing.Count > 0)
                Debug.LogError($"[{nameof(CutInBand)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
        }

        /// <summary>카드 색을 넣는다 — 가운데(c1) · 가장자리(c2) · 빛(glow).</summary>
        public void SetColors(Color middle, Color edge, Color glow)
        {
            face.color = middle;
            foreach (Image image in edges)
                image.color = edge;
            foreach (UIShape shape in glows)
                shape.BorderColor = new Color(glow.r, glow.g, glow.b, shape.BorderColor.a);
        }

        // 프리팹에 저장된 제자리 — 처음 밀 때 한 번 기억한다 (Awake 순서와 상관없이)
        private void KeepOrigin()
        {
            if (_hasOrigin)
                return;
            _origin = Rect.anchoredPosition;
            _linesOrigin = lines.anchoredPosition;
            _hasOrigin = true;
        }

#if UNITY_EDITOR
        // Inspector 에서 기울기를 바꾸면 조각을 다시 그린다
        private void OnValidate()
        {
            foreach (BandSkew piece in GetComponentsInChildren<BandSkew>(true))
                piece.GetComponent<Graphic>().SetVerticesDirty();
        }
#endif
    }
}
