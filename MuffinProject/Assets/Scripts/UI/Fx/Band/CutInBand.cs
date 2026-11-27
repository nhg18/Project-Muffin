using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Band
{
    /// <summary>
    /// 띠 컷인의 띠 (시안 .ci-band · .ci-lines) — 카드 색 넣기 · 밀기 · 줄무늬 흐르기를 맡는다. 그림은 아래 조각이 그린다 (뒤에서 앞으로):
    /// 검은 테(시안 0 0 0 4px #000) → 면(c1, 마스크) → 면 안에 흐르는 가로 줄(<see cref="BandStreaks"/>) · 위아래 가장자리 그라데이션(Common ui_fade + c2).
    /// 시안의 번지는 빛 · 빛 테(glow)는 뺐고, 세로 줄무늬(.ci-lines)는 블라인드처럼 보여 띠를 따라 흐르는 가로 줄로 바꿨다.
    /// 면이 마스크라 테를 면과 한 도형으로 그리면 가로 줄이 테를 덮는다.
    /// 기울기(CSS skewY)는 조각마다 붙은 <see cref="BandSkew"/> 가 <see cref="Slope"/> 로 민다.
    /// </summary>
    public class CutInBand : MonoBehaviour
    {
        [Tooltip("기울기 (도) — 오른쪽이 올라간다 (시안 skewY(-8deg)).")]
        [SerializeField] private float skew = 8f;
        [Tooltip("가로 줄 색 = 띠 색을 흰색 쪽으로 이만큼.")]
        [SerializeField] private float streakLighten = 0.28f;

        [Header("조각")]
        [Tooltip("면 (시안 c1) — 가장자리 그라데이션 · 줄무늬를 자르는 마스크.")]
        [SerializeField] private UIShape face;
        [Tooltip("위아래 가장자리 그라데이션 (시안 c2 → c1 22%) — 색만 바꾼다.")]
        [SerializeField] private Image[] edges;
        [Tooltip("흐르는 가로 줄.")]
        [SerializeField] private BandStreaks streaks;

        private RectTransform _rect;
        private Vector2 _origin;
        private bool _hasOrigin;
        private float _slide;

        /// <summary>기울기 — 가로 1 에 세로로 오르는 양 (tan).</summary>
        public float Slope => Mathf.Tan(skew * Mathf.Deg2Rad);

        /// <summary>띠 너비 (화면 단위).</summary>
        public float Width => Rect.rect.width;


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

        /// <summary>가로 줄을 처음부터 흐르게 하거나(true) 감춘다(false).</summary>
        public void ShowStreaks(bool show)
        {
            streaks.gameObject.SetActive(show);
            if (show)
                streaks.Restart();
        }

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        private void Awake()
        {
            var missing = new List<string>();
            if (face == null)
                missing.Add(nameof(face));
            if (edges == null || edges.Length == 0 || Array.IndexOf(edges, null) >= 0)
                missing.Add(nameof(edges));
            if (streaks == null)
                missing.Add(nameof(streaks));
            if (missing.Count > 0)
                Debug.LogError($"[{nameof(CutInBand)}] 비어 있는 참조: {string.Join(", ", missing)}", this);
        }

        /// <summary>카드 색을 넣는다 — 가운데(c1) · 가장자리(c2).</summary>
        public void SetColors(Color middle, Color edge)
        {
            face.color = middle;
            foreach (Image image in edges)
                image.color = edge;
            streaks.color = Color.Lerp(middle, Color.white, streakLighten);
        }

        // 프리팹에 저장된 제자리 — 처음 밀 때 한 번 기억한다 (Awake 순서와 상관없이)
        private void KeepOrigin()
        {
            if (_hasOrigin)
                return;
            _origin = Rect.anchoredPosition;
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
