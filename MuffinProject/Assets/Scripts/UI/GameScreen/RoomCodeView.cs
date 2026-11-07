using TMPro;
using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 좌상단 방 코드 꼬리표 — `ROOM` 라벨 + 코드 4자 (10-ui.md 2절 · 2-1 「방 코드」). 값을 받아 표시만 한다.
    /// 방 이름은 프레젠터가 읽어서 넘긴다 — 이 뷰는 Photon 을 모른다.
    /// </summary>
    public class RoomCodeView : MonoBehaviour
    {
        [SerializeField] private TMP_Text codeText;

        private void Awake()
        {
            if (codeText == null)
                Debug.LogError($"[{nameof(RoomCodeView)}] {nameof(codeText)} 참조가 비어 있습니다.", this);
        }

        /// <summary>방 코드 4자. 방 밖이면 "----".</summary>
        public void SetRoomCode(string code) => codeText.text = code;
    }
}
