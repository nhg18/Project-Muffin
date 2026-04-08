using System;
using Photon.Pun;
using UnityEngine;

public class ConnectionService
{
    /// <summary>
    /// 포톤 네트워크 접속 전 환경 세팅 함수
    /// </summary>
    public void SetupPhotonNetwork()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    /// <summary>
    /// 네트워크 접속 함수
    /// </summary>
    public void Connect()
    {
        PhotonNetwork.ConnectUsingSettings();
    }

    /// <summary>
    /// 네트워크 접속시 초기 닉네임 설정
    /// 기존에 설정해둔 닉네임이 있다면 불러와서 적용
    /// Connect 이후에 사용해야 함
    /// </summary>
    public void SetupInitNickname()
    {
        // PlayerPrefs 저장된 닉네임이 존재하면 닉네임 설정
        if (!PlayerPrefs.HasKey(PlayerPrefsKeys.playerName)) return;
        String defaultName = PlayerPrefs.GetString(PlayerPrefsKeys.playerName);

        // 연결 체크
        if (!PhotonNetwork.IsConnected) return;
        SetNickname(defaultName);
    }

    /// <summary>
    /// 닉네임 설정 함수
    /// </summary>
    /// <param name="nickname">닉네임</param>
    public void SetNickname(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogError("Nickname cannot be null or empty");
            return;
        }
        
        PhotonNetwork.NickName = nickname;
        PlayerPrefs.SetString(PlayerPrefsKeys.playerName, nickname);
    }
}