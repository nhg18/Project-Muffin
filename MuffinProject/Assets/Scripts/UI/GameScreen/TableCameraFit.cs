using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 3D 층 카메라의 화각을 화면 비에 맞춘다 (10-ui.md 2-1 「3D 층 구현」 카메라 · 15-screen.md 3절).
    /// 16:9 이상으로 넓으면 세로 화각 고정(좌우가 더 보인다), 16:9 보다 좁으면(태블릿 4:3 · 16:10) 16:9 때의 가로 화각을 지키도록 세로 화각을 늘린다 — 좌우 좌석 · 함정 칸이 잘리거나 HUD 와 어긋나지 않게.
    /// 메인 카메라에 붙인다. 같은 자리 · 같은 화각을 써야 하는 자식 카메라(손패 · 앞 층 · 체인 층)도 같이 맞춘다. 창 크기가 바뀌면 다시 맞춘다.
    /// </summary>
    [DefaultExecutionOrder(-100)] // 화각으로 자리를 계산하는 화면(HandView 등)보다 먼저
    [RequireComponent(typeof(Camera))]
    public class TableCameraFit : MonoBehaviour
    {
        private const float ReferenceAspect = 16f / 9f; // 15-screen.md 3절 — 기준 1920 × 1080

        [Tooltip("16:9 이상일 때의 세로 화각 (도). 시안 perspective 1500px 을 캔버스 높이 1080 에 댄 값 (10-ui 2-1 「카메라」).")]
        [SerializeField] private float verticalFov = 40f;

        private Camera _camera;
        private Camera[] _cameras; // 나 + 자식 카메라
        private float _fittedAspect = -1f;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _cameras = GetComponentsInChildren<Camera>(true);
            Fit();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_camera.aspect, _fittedAspect))
                Fit();
        }

        private void Fit()
        {
            _fittedAspect = _camera.aspect;
            float fov = verticalFov;
            if (_fittedAspect > 0f && _fittedAspect < ReferenceAspect)
            {
                float halfWidth = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad) * ReferenceAspect; // 16:9 때의 가로 반폭 (거리 1 기준)
                fov = 2f * Mathf.Atan(halfWidth / _fittedAspect) * Mathf.Rad2Deg;
            }

            foreach (Camera target in _cameras)
                target.fieldOfView = fov;
        }
    }
}
