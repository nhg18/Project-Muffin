using System;
using TMPro;
using UI.Interfaces;
using Unity.VisualScripting;
using UnityEngine;

namespace UI.Components
{
    public class JoinRoomPopup : SubmitCancelPopup
    {
        [SerializeField] private TMP_InputField codeInput;
        private ISubmitLogic _submitLogic;

        private void Awake()
        {
            _submitLogic = GetComponent<ISubmitLogic>();
        }
        
        protected override void OnSubmit()
        {
            _submitLogic.Execute(codeInput);
        }

        protected override void OnCancel()
        {
            Close();
        }
    }
}
