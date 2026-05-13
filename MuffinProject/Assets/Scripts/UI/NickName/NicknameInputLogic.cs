using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using TMPro;
using UI.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NicknameInputLogic : MonoBehaviour, ISubmitLogic
{
    public void Init(TMP_InputField input)
    {
        LoadNickname(input);
    }

    public void Execute(TMP_InputField input)
    {
        HandleSubmit(input.text.Trim());
    }
    
    // Submit 로직 처리
    private void HandleSubmit(string input)
    {
        NetworkManager.Instance.SetNickname(input); // 네트워크 닉네임
        PlayerData.Nickname = input;                // PlayerData 클라이언트 내부 닉네임
        SceneManager.LoadScene(ScenePaths.Get(SceneType.Lobby));
        
        string appId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime;

        Debug.Log(appId); // 41fa7c0d-7331-48f7-b45b-423c2fe53383
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
}
