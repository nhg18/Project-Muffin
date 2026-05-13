using System.Collections;
using System.Collections.Generic;
using TMPro;
using UI.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RoomJoinSubmitLogic : MonoBehaviour, ISubmitLogic
{
    public void Init(TMP_InputField input)
    {
    }

    public void Execute(TMP_InputField input)
    {
        NetworkManager.Instance.JoinRoom(input.text.Trim());
        Debug.Log(input.text.Trim());
    }
}
