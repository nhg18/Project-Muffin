using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.Popups
{
    /// <summary>
    /// 경고 · 에러 안내. 확인을 누르면 OnConfirmed 를 알린 뒤 스스로 닫힌다.
    /// </summary>
    public class WarningPopup : Popup
    {
        [SerializeField] private TMP_Text mainText;
        [SerializeField] private TMP_Text subText;
        [SerializeField] private Button okButton;

        public string MainText
        {
            set => mainText.text = value;
        }

        public string SubText
        {
            set => subText.text = value;
        }

        public event Action OnConfirmed;

        private void OnEnable()
        {
            okButton.onClick.AddListener(HandleOkClicked);
        }

        private void OnDisable()
        {
            okButton.onClick.RemoveListener(HandleOkClicked);
        }

        private void HandleOkClicked()
        {
            OnConfirmed?.Invoke();
            Close();
        }
    }
}
