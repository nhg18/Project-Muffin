using System.Collections;
using System.Collections.Generic;
using Chapchu.Network;
using UnityEngine;

namespace Chapchu.UI.PlayerProfile
{

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
}
