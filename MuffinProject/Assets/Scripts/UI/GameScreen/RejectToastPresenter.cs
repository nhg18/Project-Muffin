using Chapchu.Network.Events;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 방장이 내 요청을 거절해도 화면에는 아무것도 띄우지 않는다 — 토스트 · 소리 없이 막기만 한다 (10-ui.md 8-2, PM 2026-11-28).
    /// 거절된 카드는 손패 자리로 돌아가 있고 뽑기는 일어나지 않는다. 여기서는 개발 로그만 남긴다.
    /// </summary>
    public class RejectToastPresenter : MonoBehaviour
    {
        private void OnEnable()
        {
            GameEvents.OnMyRequestRejected += HandleMyRequestRejected;
        }

        private void OnDisable()
        {
            GameEvents.OnMyRequestRejected -= HandleMyRequestRejected;
        }

        private void HandleMyRequestRejected(int code)
        {
            Debug.Log($"[{nameof(RejectToastPresenter)}] 요청 거절 — 코드 {code} (화면에는 띄우지 않는다)");
        }
    }
}
