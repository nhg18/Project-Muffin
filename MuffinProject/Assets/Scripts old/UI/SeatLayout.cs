using UnityEngine;

namespace Chapchu.UI
{
    /// <summary>
    /// 인게임 좌석 3개(위 · 왼쪽 · 오른쪽)와 테이블의 배치를 화면 크기에 맞춰 다시 계산한다 (15-screen.md 5절).
    /// 좌석의 짧은 쪽(두께)은 셋이 항상 같고, 화면이 커지면 조금씩 두꺼워진다. 테이블은 좌석과 아래 줄을 뺀 나머지를 채운다.
    /// RectTransform 앵커는 축마다 따로 계산되어 좌우 좌석(가로)과 위 좌석(세로)의 두께를 한 값으로 묶을 수 없어 스크립트로 맞춘다.
    /// SafeArea 오브젝트에 붙인다. 좌석 · 테이블의 앵커 · 오프셋은 이 스크립트가 덮어쓴다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class SeatLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform seatTop;
        [SerializeField] private RectTransform seatLeft;
        [SerializeField] private RectTransform seatRight;
        [SerializeField] private RectTransform table;

        [Header("연출값 (1920×1080 기준)")]
        [Tooltip("기준 해상도에서의 좌석 두께")]
        [SerializeField, Min(0f)] private float baseThickness = 240f;
        [Tooltip("두께가 화면 배율을 따라가는 비율. 0 = 항상 고정, 1 = 완전 비례")]
        [SerializeField, Range(0f, 1f)] private float scaleRatio = 0.5f;
        [Tooltip("화면 가장자리 · 좌석 · 테이블 사이 간격")]
        [SerializeField, Min(0f)] private float gap = 8f;
        [Tooltip("아래 줄(MySeat · MyHand · ButtonArea)에 남겨 두는 높이. 320 + 간격 8 × 2")]
        [SerializeField, Min(0f)] private float bottomReserved = 336f;

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private void OnEnable()
        {
            Apply();
        }

        // 해상도 · 회전 · Safe Area 변화로 이 RectTransform 크기가 바뀌면 다시 계산한다.
        private void OnRectTransformDimensionsChange()
        {
            Apply();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // OnValidate 안에서 다른 RectTransform 을 바꾸면 SendMessage 경고가 나므로 한 프레임 미룬다.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                    Apply();
            };
        }
#endif

        private void Apply()
        {
            if (seatTop == null || seatLeft == null || seatRight == null || table == null)
                return;

            Rect area = ((RectTransform)transform).rect;
            if (area.width <= 0f || area.height <= 0f)
                return;

            // 화면 배율 = 가로 · 세로 배율의 평균. 두께는 그 배율을 scaleRatio 만큼만 따라간다.
            float scale = (area.width / ReferenceResolution.x + area.height / ReferenceResolution.y) * 0.5f;
            float thickness = baseThickness * (1f - scaleRatio + scaleRatio * scale);
            float inset = gap + thickness + gap;

            Place(seatLeft, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(gap, bottomReserved), new Vector2(gap + thickness, -gap));
            Place(seatRight, new Vector2(1f, 0f), new Vector2(1f, 1f),
                new Vector2(-(gap + thickness), bottomReserved), new Vector2(-gap, -gap));
            Place(seatTop, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(inset, -(gap + thickness)), new Vector2(-inset, -gap));
            Place(table, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(inset, bottomReserved), new Vector2(-inset, -inset));
        }

        // 값이 같으면 건드리지 않는다. 에디터에서 매번 씬이 dirty 되는 것을 막는다.
        private static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rt.anchorMin == anchorMin && rt.anchorMax == anchorMax &&
                rt.offsetMin == offsetMin && rt.offsetMax == offsetMax)
                return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
