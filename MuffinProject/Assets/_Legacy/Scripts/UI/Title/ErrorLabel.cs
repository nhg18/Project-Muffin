using TMPro;
using UnityEngine;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// 에러 문구. 문자열만 바꾸고 오브젝트는 끄지 않는다 — 자리(높이)를 유지해 버튼 위치가 움직이지 않게 한다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class ErrorLabel : MonoBehaviour
    {
        private TMP_Text _label;

        private void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        public void Show(string message) => _label.text = message;

        public void Clear() => _label.text = string.Empty;
    }
}
