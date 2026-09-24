using System.Collections;
using System.Collections.Generic;
using Chapchu.Network;
using TMPro;
using Chapchu.UI.Components;
using Chapchu.UI.Interfaces;
using Chapchu.UI.Popup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.UI
{

    public class JoinRoomSubmitLogic : MonoBehaviour, ISubmitLogic
    {
        public void Init(TMP_InputField input)
        {
        }

        public void Execute(TMP_InputField input)
        {
            Debug.Log("Clicked Join");
            NetworkManager.Instance.JoinRoom(input.text);
        }
    }
}
