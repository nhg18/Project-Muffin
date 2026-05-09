using System;
using UnityEngine;
using UnityEngine.SceneManagement;

class NicknamePreseneter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private NicknameView _view;

    private CanvasGroup _canvasGroup;
    private const int MinLength = 2;
    private const int MaxLength = 12;

    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        HandleConnected(false);
    }

    private void OnEnable()
    {
        _view.OnSubmitClicked += HandleSubmit;
        ConnectionEvents.OnConnected += HandleConnected;
    }
    
    private void Start()
    {
        // 저장된 닉네임 불러오기
        LoadNickname();
    }

    private void OnDisable()
    {
        _view.OnSubmitClicked -= HandleSubmit;
        ConnectionEvents.OnConnected -= HandleConnected;
    }

    private void OnDestroy()
    {
        _view.OnSubmitClicked -= HandleSubmit;
        ConnectionEvents.OnConnected -= HandleConnected;
    }
    
    // Submit 로직 처리
    private void HandleSubmit()
    {
        string input = _view.InputText.Trim();

        _view.SetFeedback($"닉네임 '{input}' 이(가) 설정되었습니다!");
        // _view.SetSubmitInteractable(false);
        NetworkManager.Instance.SetNickname(input); // 네트워크 닉네임
        PlayerData.Nickname = input;                // PlayerData 클라이언트 내부 닉네임
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
    }
    
    private void LoadNickname()
    {
        string defaultName = String.Empty;

        if (_view.InputText == null) return;
        if (PlayerPrefs.HasKey(PlayerPrefsKeys.playerName))
        {
            defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);
            _view.SetInputText(defaultName);
        }
    }
    
    private void HandleConnected(bool connected)
    {
        Debug.Log("Connected");
        _canvasGroup.alpha = connected ? 1 : 0;
        _canvasGroup.interactable = connected;
    }
}