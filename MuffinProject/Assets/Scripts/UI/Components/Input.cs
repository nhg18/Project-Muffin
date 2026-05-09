using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Components
{
    [RequireComponent(typeof(ISubmitLogic))]
    public class Input : MonoBehaviour
    {
        private TMP_InputField _Input;
        private Button _submitButton;
        private ISubmitLogic _submitLogic;
    
        private void Awake()
        {
            _Input = GetComponentInChildren<TMP_InputField>();
            _submitButton = GetComponentInChildren<Button>();
            _submitLogic = GetComponent<ISubmitLogic>();
        }

        private void OnEnable()
        {
            _submitButton.onClick.AddListener(HandleSubmit);
        }

        private void Start()
        {
            _submitLogic.Init(_Input);
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
            _submitLogic.Execute(_Input);
        }
    }
}
