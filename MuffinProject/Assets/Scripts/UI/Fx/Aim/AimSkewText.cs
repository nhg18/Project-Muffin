using TMPro;
using UnityEngine;

namespace Chapchu.UI.Fx.Aim
{
    /// <summary>
    /// 시안 transform: skewX — 글자를 옆으로 기울인다 (위가 오른쪽으로). 글자 칸 가운데 높이를 기준으로 정점을 민다.
    /// RectTransform 으로는 기울일 수 없어 TMP 가 메시를 만든 직후(OnPreRenderText) 고친다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    public class AimSkewText : MonoBehaviour
    {
        [Tooltip("기울기 (도, 시안 skewX 의 반대 부호 — 9 면 skewX(-9deg)).")]
        [SerializeField] private float angle = 9f;

        private TMP_Text _text;

        private void OnEnable()
        {
            _text = GetComponent<TMP_Text>();
            _text.OnPreRenderText += Skew;
            _text.ForceMeshUpdate();
        }

        private void OnDisable()
        {
            if (_text == null)
                return;
            _text.OnPreRenderText -= Skew;
            _text.ForceMeshUpdate();
        }

        private void Skew(TMP_TextInfo info)
        {
            float k = Mathf.Tan(angle * Mathf.Deg2Rad);
            float middle = _text.rectTransform.rect.center.y;
            for (int m = 0; m < info.meshInfo.Length; m++)
            {
                Vector3[] vertices = info.meshInfo[m].vertices;
                if (vertices == null)
                    continue;
                int count = info.meshInfo[m].vertexCount;
                for (int i = 0; i < count; i++)
                    vertices[i].x += k * (vertices[i].y - middle);
            }
        }
    }
}
