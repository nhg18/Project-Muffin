using Chapchu.UI.Fx;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 업적 배지 한 개 (시안 .badge) — 아이콘 원 · 업적 이름 · 값. 시상대 아래쪽에 최대 3개 쌓인다 (07-win-condition.md 11-2).
    /// 시상대 높이가 모자라면 작은 모양(시안 .tight)으로 그린다.
    /// </summary>
    public class ResultBadgeView : MonoBehaviour
    {
        [SerializeField] private LayoutElement size;
        [SerializeField] private HorizontalLayoutGroup row;
        [SerializeField] private UIShape box;
        [SerializeField] private LayoutElement circleSize;
        [SerializeField] private RectTransform icon;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text valueText;

        [Header("작은 모양 (시안 .podium.tight .badge)")]
        [SerializeField] private float tightHeight = 33f;
        [SerializeField] private float tightBorder = 3.5f;
        [Tooltip("안쪽 여백 — 왼쪽 · 오른쪽 (테두리 포함).")]
        [SerializeField] private Vector2 tightPadding = new Vector2(6.5f, 15.5f);
        [SerializeField] private float tightGap = 7f;
        [SerializeField] private float tightCircle = 24f;
        [SerializeField] private float tightIcon = 17f;
        [SerializeField] private float tightNameSize = 20f;
        [SerializeField] private float tightValueSize = 15f;

        [Header("등장 연출 (시안 badge)")]
        [Tooltip("튀어나오는 시간 (초).")]
        [SerializeField] private float popSeconds = 0.4f;
        [Tooltip("튀어나올 때 제 크기를 넘어가는 정도 (OutBack 이징의 overshoot).")]
        [SerializeField] private float popOvershoot = 2f;

        private void Awake()
        {
            if (size == null || row == null || box == null || circleSize == null || icon == null || iconImage == null || nameText == null || valueText == null)
                Debug.LogError($"[{nameof(ResultBadgeView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>배지 하나를 그린다. isTight = 시상대 높이가 모자라 작게.</summary>
        public void Show(Sprite sprite, string award, string value, bool isTight)
        {
            iconImage.sprite = sprite;
            nameText.text = award;
            valueText.text = value;
            if (!isTight)
                return;

            size.preferredHeight = tightHeight;
            row.padding.left = Mathf.RoundToInt(tightPadding.x);
            row.padding.right = Mathf.RoundToInt(tightPadding.y);
            row.spacing = tightGap;
            box.Set(UIShape.ShapeKind.RoundRect, tightHeight / 2f, tightBorder, box.BorderColor);
            circleSize.minWidth = tightCircle;
            circleSize.minHeight = tightCircle;
            circleSize.preferredWidth = tightCircle;
            circleSize.preferredHeight = tightCircle;
            icon.sizeDelta = new Vector2(tightIcon, tightIcon);
            nameText.fontSize = tightNameSize;
            valueText.fontSize = tightValueSize;
        }

        /// <summary>등장 연출 (시안 badge) — delay 초 뒤 튀어나온다.</summary>
        public void PlayIntro(float delay)
        {
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, popSeconds).SetDelay(delay).SetEase(Ease.OutBack, popOvershoot).SetLink(gameObject);
        }
    }
}
