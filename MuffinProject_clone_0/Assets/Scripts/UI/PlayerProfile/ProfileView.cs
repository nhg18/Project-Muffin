using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProfileView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text _nicknameText;
    // [SerializeField] private Image _image;

    public void SetNicknameText(string nickname)
    {
        _nicknameText.text = nickname;
    }
}
