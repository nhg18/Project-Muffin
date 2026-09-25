using TMPro;
using UnityEngine;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 문구 한 줄짜리 토스트. PopupManager.ShowToast 로 띄운다.
    /// </summary>
    public class MessageToast : ToastPopup
    {
        [SerializeField] private TMP_Text messageText;

        public string Message
        {
            set => messageText.text = value;
        }
    }
}
