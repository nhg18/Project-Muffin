using System.Collections;
using System.Collections.Generic;
using Muffin.Network;
using UnityEngine;

namespace Muffin.UI.PlayerProfile
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
