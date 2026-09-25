using UnityEngine;

namespace Chapchu.UI
{
    /// <summary>
    /// 가장자리 UI 컨테이너를 노치 · 펀치홀 · 홈 인디케이터 밖으로 밀어낸다 (15-screen.md 7절).
    /// 부모(캔버스) 전체를 덮는 RectTransform 에 붙이고, 가장자리 UI 를 자식으로 둔다. 배경 · 중앙 묶음에는 붙이지 않는다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void Start()
        {
            Apply();
        }

        // 화면 회전 · 해상도 변경 시 캔버스 크기가 바뀌면 다시 계산한다.
        private void OnRectTransformDimensionsChange()
        {
            if (_rect != null && Screen.safeArea != _applied)
                Apply();
        }

        private void Apply()
        {
            Rect safe = Screen.safeArea;
            if (Screen.width == 0 || Screen.height == 0) return;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width; min.y /= Screen.height;
            max.x /= Screen.width; max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
            _applied = safe;
        }
    }
}
