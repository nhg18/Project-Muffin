using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Chapchu.UI.PlayerProfile
{

    public class ProfileView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField, FormerlySerializedAs("_nicknameText")] private TMP_Text nicknameText;
        // [SerializeField] private Image _image;

        public void SetNicknameText(string nickname)
        {
            nicknameText.text = nickname;
        }
    }
}
