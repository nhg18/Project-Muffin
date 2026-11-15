using TMPro;
using UnityEngine;

namespace Chapchu.UI.Title
{
    /// <summary>
    /// "ver {Player Settings 의 Version} · Project ChapChu". 버전 규칙은 docs/versioning.md
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class VersionLabel : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<TMP_Text>().text = $"ver {Application.version} · Project ChapChu";
        }
    }
}
