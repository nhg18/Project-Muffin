using TMPro;
using UnityEngine;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 게임 씬 방 코드 뷰. 값을 받아 표시만 한다. 네트워크를 모른다 (RoomView 와 같은 구조).
    /// 기준 문서: docs/systems/10-ui.md 2절
    /// </summary>
    public class RoomCodeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text roomCodeText;

        public void SetRoomCode(string code) => roomCodeText.text = code;
    }
}
