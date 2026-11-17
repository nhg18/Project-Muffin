using Chapchu.UI.Fx;
using UnityEngine;

namespace Chapchu.UI.ResultScreen
{
    /// <summary>
    /// 「한 판 더」 버튼의 준비 점 하나 (시안 「결과화면 UI v3」 .dots i) — 빈 점 · 준비하면 그 사람 색 · 나가면 회색 가위표.
    /// </summary>
    public class ResultReadyDotView : MonoBehaviour
    {
        [SerializeField] private UIShape dot;
        [Tooltip("나간 사람의 가위표.")]
        [SerializeField] private GameObject cross;

        [SerializeField] private Color ink = new Color32(0x5E, 0x50, 0x47, 0xFF);
        [Tooltip("나간 사람의 테 · 가위표 색.")]
        [SerializeField] private Color goneColor = new Color32(0x9A, 0x91, 0x88, 0xFF);

        private void Awake()
        {
            if (dot == null || cross == null)
                Debug.LogError($"[{nameof(ResultReadyDotView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        /// <summary>color = 그 사람의 목걸이 색.</summary>
        public void Show(Color color, bool isReady, bool isGone)
        {
            dot.color = isGone ? Color.clear : isReady ? color : Color.white;
            dot.BorderColor = isGone ? goneColor : ink;
            cross.SetActive(isGone);
        }
    }
}
