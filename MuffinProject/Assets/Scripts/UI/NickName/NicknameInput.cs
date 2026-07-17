using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Network;
using TMPro;
using UI.NickName;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NicknameInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text errorText;

    private void Awake()
    {
        inputField.characterLimit = NicknameValidator.MaxLength;
        errorText.text = "";
        UpdateCount("");
    }

    private void OnEnable()
    {
        inputField.onValueChanged.AddListener(UpdateCount);
        submitButton.onClick.AddListener(Submit);
    }

    private void OnDisable()
    {
        inputField.onValueChanged.RemoveListener(UpdateCount);
        submitButton.onClick.RemoveListener(Submit);
    }

    private void UpdateCount(string inputText)
    {
        var newText = $"{inputText.Length} / {NicknameValidator.MaxLength}";
        countText.text = newText;
    }
    
    private void Submit()
    {
        var nickname = inputField.text;
        var valid = NicknameValidator.Validate(nickname);
        if (valid != NicknameValidationResult.Valid)
        {
            var errorMsg = NicknameValidator.GetErrorMessage(valid);
            HandleError(errorMsg);
            Debug.LogWarning("Nickname is invalid");
            return;
        }
        
        NetworkManager.Instance.SetNickname(nickname);

        LoadScene();
    }

    private void HandleError(string errorMsg)
    {
        errorText.text = errorMsg;
    }

    private async void LoadScene()
    {
        try
        {
            submitButton.interactable = false;
            // 팝업 호출
            var op = SceneManager.LoadSceneAsync(ScenePaths.Get(SceneType.Lobby));
        
            if (op == null)
            {
                Debug.LogError($"Scene {ScenePaths.Get(SceneType.Lobby)} not found");
                return;
            }
        
            op.allowSceneActivation = false;
        
            while (op.progress < 0.9f)
                await Task.Yield();
        
            // 팝업 닫기
            op.allowSceneActivation = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"씬 로드 실패: {e}");
        }
    }
}
