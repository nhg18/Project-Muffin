using UnityEngine;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 디자인 영역(19.2 × 10.8 유닛 = 1920 × 1080 px, 100 px/unit)이 어떤 화면 비율에서도 통째로 보이게
    /// 직교 카메라 크기를 맞춘다. HUD 캔버스의 Expand 와 같은 규칙 (15-screen.md 3절).
    /// 폰(넓은 화면)은 좌우가, 태블릿(좁은 화면)은 위아래가 넓어진다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        private const float DesignWidth = 19.2f;
        private const float DesignHeight = 10.8f;

        private Camera _camera;

        // 해상도는 언제든 바뀔 수 있다(에디터 Game 뷰, 폴더블 펼침). 계산이 가벼워 매 프레임 확인한다.
        private void Update()
        {
            if (_camera == null) _camera = GetComponent<Camera>();

            float size = Mathf.Max(DesignHeight, DesignWidth / _camera.aspect) * 0.5f;
            if (!Mathf.Approximately(_camera.orthographicSize, size))
                _camera.orthographicSize = size;
        }
    }
}
