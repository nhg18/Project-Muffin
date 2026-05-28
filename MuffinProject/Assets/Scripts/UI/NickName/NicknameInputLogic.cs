using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UI.Components;
using UI.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NicknameInputLogic : MonoBehaviour, ISubmitLogic
{
    [SerializeField] private int minLength = 2;
    [SerializeField] private int maxLength = 16;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text errorText;

    private string _errorMessage = string.Empty;
    private Coroutine _currentCoroutine;

    private void Start()
    {
        errorText.gameObject.SetActive(false);
    }

    public void Init(TMP_InputField input)
    {
        LoadNickname(input);
        UpdateCount(input.text);
        input.characterLimit = maxLength;
        input.onValueChanged.AddListener(UpdateCount);
    }

    public void Execute(TMP_InputField input)
    {
        HandleSubmit(input.text.Trim());
    }
    
    // Submit 로직 처리
    private void HandleSubmit(string text)
    {
        if (!ValidateNickname(text))
        {
            ShowError(_errorMessage);
            return;
        }

        NetworkManager.Instance.Connect();
        PopupManager.Instance.Show<LoadingPopup>();
        NetworkManager.Instance.SetNickname(text); // 네트워크 닉네임
    }

    private bool ValidateNickname(string nickname)
    {
        if (string.IsNullOrWhiteSpace(nickname))
        {
            _errorMessage = "닉네임을 입력해주세요.";
            return false;
        }
        
        nickname = nickname.Trim();
        
        if (nickname.Length < minLength || nickname.Length > maxLength)
        {
            _errorMessage = $"닉네임은 {minLength}~{maxLength}자여야 합니다.";
            return false;
        }
        
        // 공백 포함 여부
        if (nickname.Contains(" "))
        {
            _errorMessage = "공백은 사용할 수 없습니다.";
            return false;
        }
        
        // 허용 문자 검사
        Regex regex = new Regex(@"^[가-힣a-zA-Z0-9]+$");
        if (!regex.IsMatch(nickname))
        {
            _errorMessage = "한글, 영어, 숫자만 사용할 수 있습니다.";
            return false;
        }
        
        // 한글 자음 모음 포함
        Regex koreanJamoRegex = new Regex(@"[ㄱ-ㅎㅏ-ㅣ]");
        if (koreanJamoRegex.IsMatch(nickname))
        {
            _errorMessage = "유효하지 않은 닉네임입니다.";
            return false;
        }
        
        return true;
    }
    
    private void LoadNickname(TMP_InputField input)
    {
        string defaultName = string.Empty;

        if (PlayerPrefs.HasKey(PlayerPrefsKeys.playerName))
        {
            defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);
        }
        
        input.text = defaultName;
    }
    
    private void UpdateCount(string text)
    {
        countText.text = $"{text.Length} / {maxLength}";
    }

    public void ShowError(string message)
    {
        // 이전 타이머 제거
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
        }

        errorText.gameObject.SetActive(true);
        errorText.text = message;

        _currentCoroutine = StartCoroutine(HideErrorRoutine());
    }

    private IEnumerator HideErrorRoutine()
    {
        yield return new WaitForSeconds(10f);

        errorText.gameObject.SetActive(false);

        _currentCoroutine = null;
    }
}
