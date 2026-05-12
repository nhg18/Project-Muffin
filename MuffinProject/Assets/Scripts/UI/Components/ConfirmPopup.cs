using System;
using UI.Interfaces;
using UnityEngine.UI;

namespace UI.Components
{
    public class ConfirmPopup : BasePopup
    {
        private Button _submitButton;
        private Button _cancelButton;
        private IButtonLogic _submitButtonLogic;
        private IButtonLogic _cancelButtonLogic;

        public override void OnShow()
        {
            
        }

        public override void OnHide()
        {
            
        }

        private void Awake()
        {
            _submitButton = GetComponentInChildren<Button>();
            _cancelButton = GetComponentInChildren<Button>();
            _submitButtonLogic = _submitButton.GetComponent<IButtonLogic>();
            _cancelButtonLogic = _cancelButton.GetComponent<IButtonLogic>();
        }

        private void OnEnable()
        {
            _submitButton.onClick.AddListener(HandleSubmit);
            _cancelButton.onClick.AddListener(HandleCancel);
        }

        private void OnDisable()
        {
            _submitButton.onClick.RemoveListener(HandleSubmit);
            _cancelButton.onClick.RemoveListener(HandleCancel);
        }

        private void OnDestroy()
        {
            _submitButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.RemoveAllListeners();
        }

        private void HandleSubmit()
        {
            _submitButtonLogic.Execute();
        }

        private void HandleCancel()
        {
            _cancelButtonLogic.Execute();
        }
    }
}