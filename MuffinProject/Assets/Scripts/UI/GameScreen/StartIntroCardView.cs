using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 게임 시작 연출의 턴 순서 카드 한 장 (프로토타입 v2 .icard) — 뒷면으로 섞이다가 뒤집히면 앞면에 순서 번호 · 캐릭터 · 이름, 내 카드는 「나」 딱지.
    /// GameStartIntroView 가 틀(꺼 둔 것)을 복제해 쓴다. 표시만.
    /// </summary>
    public class StartIntroCardView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [Tooltip("뒷면 — 카드 뒷면 그림.")]
        [SerializeField] private GameObject back;
        [Tooltip("앞면 — 위 34% 가 그 사람 색인 흰 카드.")]
        [SerializeField] private GameObject front;
        [Tooltip("앞면 위쪽 색 띠 — 그 사람 목걸이 색. 자식 그림(아래 모서리를 메우는 네모)도 같은 색이 된다.")]
        [SerializeField] private Graphic colorBand;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private Image character;
        [SerializeField] private Image accessory;
        [SerializeField] private TMP_Text nameText;
        [Tooltip("이름 앞 점 — 그 사람 색.")]
        [SerializeField] private Graphic nameDot;
        [Tooltip("내 카드 딱지 「나」.")]
        [SerializeField] private GameObject meTag;

        public RectTransform Rect => (RectTransform)transform;
        public CanvasGroup Group => group;

        private void Awake()
        {
            if (group == null || back == null || front == null || colorBand == null || numberText == null || character == null || accessory == null
                || nameText == null || nameDot == null || meTag == null)
                Debug.LogError($"[{nameof(StartIntroCardView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void Set(int number, string displayName, Color color, Sprite body, Sprite accessorySprite, bool isMe)
        {
            numberText.text = number.ToString();
            nameText.text = displayName;
            foreach (Graphic part in colorBand.GetComponentsInChildren<Graphic>(true))
                part.color = color; // 둥근 위 띠 + 아래 모서리를 메우는 네모
            nameDot.color = color;
            character.sprite = body;
            character.enabled = body != null;
            accessory.sprite = accessorySprite;
            accessory.enabled = accessorySprite != null;
            meTag.SetActive(isMe);
            ShowFront(false);
        }

        public void ShowFront(bool shown)
        {
            back.SetActive(!shown);
            front.SetActive(shown);
        }
    }
}
