using UnityEngine;

namespace Chapchu.UI
{
    /// <summary>
    /// 이 RectTransform 을 기기의 Safe Area(노치 · 펀치홀 · 홈 인디케이터 바깥)에 맞춘다.
    /// 캔버스 바로 아래의 가장자리 UI 컨테이너에 하나만 붙인다. 배경 · 중앙 묶음에는 붙이지 않는다 (15-screen.md 7절).
    /// 앵커를 Safe Area 비율로 두므로 Canvas Scaler 방식과 무관하게 동작한다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        // 회전 · 분할 화면 등으로 Safe Area 가 바뀌는 이벤트가 없어 값이 달라졌을 때만 다시 맞춘다.
        private void Update()
        {
            if (Screen.safeArea != _applied)
                Apply();
        }

        private void Apply()
        {
            Rect safeArea = Screen.safeArea;
            _applied = safeArea;

            Vector2 min = safeArea.position;
            Vector2 max = safeArea.position + safeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
