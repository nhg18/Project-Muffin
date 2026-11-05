using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Fx.Common
{
    /// <summary>
    /// 칼선을 찍는 판 (난도질 · 일섬 프리팹의 루트). 바탕 그림 하나와 찍어 낸 칼선들. 판째 투명도로 켜고 끈다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SlashLayer : MonoBehaviour
    {
        [Tooltip("칼선 뒤 바탕 (가운데 색, 가장자리 검정).")]
        [SerializeField] private Image background;
        [Tooltip("칼선을 찍는 칸. 바탕 위, 번쩍임 아래에 둔다.")]
        [SerializeField] private RectTransform container;
        [Tooltip("칼선을 더해서 그리는 머티리얼 (겹치면 밝아진다).")]
        [SerializeField] private Material additive;

        private CanvasGroup _group;

        /// <summary>칼선 뒤 바탕.</summary>
        public Image Background => background;

        /// <summary>판 전체 투명도.</summary>
        public float Alpha
        {
            get => Group.alpha;
            set => Group.alpha = value;
        }

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            if (background == null || container == null || additive == null)
                Debug.LogError($"[{nameof(SlashLayer)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>칼선 하나를 통째로 찍는다 (원본 영상: 한쪽에서 늘어나면 날아가는 물체처럼 보인다). angle 은 반시계 +.</summary>
        public Image Spawn(Sprite sprite, Vector2 center, float length, float thickness, float angle)
        {
            var go = new GameObject("Slash", typeof(RectTransform), typeof(Image));
            go.layer = gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(container, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(length, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.material = additive;
            image.raycastTarget = false;
            image.canvasRenderer.cullTransparentMesh = true;
            return image;
        }

        /// <summary>찍어 낸 칼선을 모두 지운다.</summary>
        public void Clear()
        {
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                child.DOKill();
                Destroy(child.gameObject);
            }
        }

        /// <summary>판을 끄고 칼선을 지운다.</summary>
        public void ResetState()
        {
            Alpha = 0f;
            Clear();
        }
    }
}
