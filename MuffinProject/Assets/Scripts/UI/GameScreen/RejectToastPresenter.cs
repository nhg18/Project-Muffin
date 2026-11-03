using Chapchu.Network.Events;
using Chapchu.UI.Popups;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 방장이 내 요청을 거절하면 사유를 토스트로 띄운다 (10-ui.md 8절). 드로우 · 카드 사용 등 요청 종류와 무관하게 한 곳에서 받는다.
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
            if (PopupManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(RejectToastPresenter)}] {nameof(PopupManager)} 가 없어 거절 코드 {code} 를 띄우지 못했습니다.", this);
                return;
            }

            PopupManager.Instance.ShowToast(RejectText.Get(code));
        }
    }
}
