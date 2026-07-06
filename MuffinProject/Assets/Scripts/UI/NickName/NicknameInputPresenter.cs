using System;
using System.Collections;
using System.Collections.Generic;
using Network;
using UI.NickName;
using Unity.VisualScripting;
using UnityEngine;

public class NicknameInputPresenter : MonoBehaviour
{
    [SerializeField] private NicknameInputView view;

    private void Awake()
    {
        view.TextChanged += UpdateCount;
        view.ButtonClicked += Submit;
    }

    private void Submit()
    {
        var nickname = view.Nickname;

        if (!NicknameValidator.Validate(nickname))
        {
            return;
        }
        
        NetworkManager.Instance.SetNickname(nickname);
    }

    private void UpdateCount()
    {
        var countText = $"{view.Nickname.Length} / {NicknameValidator.MAX_LENGTH}";
        view.SetCountText(countText);
    }
}
