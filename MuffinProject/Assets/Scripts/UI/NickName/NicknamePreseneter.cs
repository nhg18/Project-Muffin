using System;
using UnityEngine;
using UnityEngine.SceneManagement;

class NicknamePreseneter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NicknameView _view;

    private const int MinLength = 2;
    private const int MaxLength = 12;

    private void Awake()
    {
        if (_view == null)
        {
            _view = GetComponent<NicknameView>();
            Debug.LogError("_view is null");
        }
    }

    private void Start()
    {
        _view.OnSubmitClicked += HandleSubmit;

        // 저장된 닉네임 불러오기
        LoadNickname();
    }
    
    // Submit 로직 처리
    private void HandleSubmit()
    {
        string input = _view.InputText.Trim();

        _view.SetFeedback($"닉네임 '{input}' 이(가) 설정되었습니다!");
        // _view.SetSubmitInteractable(false);
        NetworkManager.Instance.SetNickname(input);
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
    }
    
    private void LoadNickname()
    {
        string defaultName = String.Empty;

        if (_view.InputText != null)
        {
            if (PlayerPrefs.HasKey(PlayerPrefsKeys.playerName))
            {
                defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);
                _view.SetInputText(defaultName);
                PlayerData.Nickname = defaultName;
            }
        }
    }

    private void OnDestroy()
    {
        _view.OnSubmitClicked -= HandleSubmit;
    }
}