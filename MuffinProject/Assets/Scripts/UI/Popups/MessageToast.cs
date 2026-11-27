using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 문구 한 줄짜리 토스트. PopupManager.ShowToast 로 띄운다. 모양은 로비 알림과 같은 말풍선 (통합 v3.25).
    /// </summary>
    public class MessageToast : ToastPopup
    {
        [SerializeField] private TMP_Text messageText;
        [Tooltip("뜰 때 튀어나오는 시간 (초) — 시안 popin .25s")]
        [SerializeField] private float popSeconds = 0.25f;

        public string Message
        {
            set => messageText.text = value;
        }

        private void Awake()
        {
            if (messageText == null)
                Debug.LogError($"[{nameof(MessageToast)}] {nameof(messageText)} 참조가 비어 있습니다.", this);
        }

        protected override void HandleOpened()
        {
            base.HandleOpened();
            transform.localScale = Vector3.zero;
            transform.DOScale(1f, popSeconds).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }
    }
}
