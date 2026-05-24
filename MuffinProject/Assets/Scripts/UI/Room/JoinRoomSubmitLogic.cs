using System.Collections;
using System.Collections.Generic;
using TMPro;
using UI.Components;
using UI.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

public class JoinRoomSubmitLogic : MonoBehaviour, ISubmitLogic
{
    public void Init(TMP_InputField input)
    {
    }

    public void Execute(TMP_InputField input)
    {
        PopupManager.Instance.Show<LoadingPopup>();
        NetworkManager.Instance.JoinRoom(input.text.Trim());
    }
}
