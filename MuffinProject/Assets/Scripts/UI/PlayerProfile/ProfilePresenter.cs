using Chapchu.Network;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chapchu.UI.PlayerProfile
{

    public class ProfilePresenter : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField, FormerlySerializedAs("_view")] private ProfileView view;

        private void Start()
        {
            UpdateProfile();
        }

        private void UpdateProfile()
        {
            view.SetNicknameText(NetworkManager.Nickname);
        }
    }
}
