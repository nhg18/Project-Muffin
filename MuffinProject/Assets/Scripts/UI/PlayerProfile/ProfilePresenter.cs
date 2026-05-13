using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProfilePresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ProfileView _view;

    private void Start()
    {
        UpdateProfile();
    }

    private void UpdateProfile()
    {
        _view.SetNicknameText(NetworkManager.Nickname);
    }
}
