using System.Collections;
using System.Collections.Generic;
using Muffin.Network;
using TMPro;
using Muffin.UI.Components;
using Muffin.UI.Interfaces;
using Muffin.UI.Popup;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Muffin.UI
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
