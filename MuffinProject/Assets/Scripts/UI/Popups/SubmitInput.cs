using TMPro;
using UI.Interfaces;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Components
{
    [RequireComponent(typeof(ISubmitLogic))]
    public class SubmitInput : MonoBehaviour
    {
        private TMP_InputField _input;
        private Button _submitButton;
        private ISubmitLogic _submitLogic;
    
        private void Awake()
        {
            _input = GetComponentInChildren<TMP_InputField>();
            _submitButton = GetComponentInChildren<Button>();
            _submitLogic = GetComponent<ISubmitLogic>();
        }

        private void OnEnable()
        {
            _submitButton.onClick.AddListener(HandleSubmit);
        }

        private void Start()
        {
            _submitLogic.Init(_input);
        }

        private void OnDisable()
        {
            _submitButton.onClick.RemoveListener(HandleSubmit);
        }

        private void OnDestroy()
        {
            _submitButton.onClick.RemoveAllListeners();
        }

        private void HandleSubmit()
        {
            _submitLogic.Execute(_input);
        }
    }
}
