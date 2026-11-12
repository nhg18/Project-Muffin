using UnityEngine;

namespace Chapchu.UI.Title
{
    /// <summary>로딩 링 — 시계 방향 등속 회전 (시안 .ldring, 1초 1바퀴)</summary>
    public class Spinner : MonoBehaviour
    {
        [Tooltip("한 바퀴 도는 시간 (초)")]
        [SerializeField] private float secondsPerTurn = 1f;

        private void Update()
        {
            transform.Rotate(0f, 0f, -360f / secondsPerTurn * Time.unscaledDeltaTime);
        }
    }
}
