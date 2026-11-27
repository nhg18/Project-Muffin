using System;
using Chapchu.Cards;
using Chapchu.Game;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 고르기 판 안의 카드 한 칸 — 「손패에서 1장」 고르기 (10-ui.md 5절 · 11-card-list 1-3 「처리 중 손패 고르기」).
    /// 테두리 색 = 카드 종류, 이름 · 그림(없으면 비움) · 문구. 칸 크기는 판이 정하고(장수가 많으면 작게) 안의 것은 비율로 따라간다. 누르면 그 장 번호를 알린다.
    /// </summary>
    public class ChoiceCardTile : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image art;
        [SerializeField] private TMP_Text descText;
        [Tooltip("행동 카드 종류 색.")]
        [SerializeField] private Color actionColor = new Color32(0x00, 0x9d, 0xff, 0xFF);
        [Tooltip("카운터 카드 종류 색.")]
        [SerializeField] private Color counterColor = new Color32(0x6f, 0xa4, 0x34, 0xFF);
        [Tooltip("함정 카드 종류 색.")]
        [SerializeField] private Color trapColor = new Color32(0xf6, 0x46, 0x12, 0xFF);

        public event Action<int> Clicked;

        public int InstanceId { get; private set; }

        private void Awake()
        {
            if (button == null || frame == null || nameText == null || art == null || descText == null)
            {
                Debug.LogError($"[{nameof(ChoiceCardTile)}] 참조 중 비어 있는 것이 있습니다.", this);
                return;
            }

            button.onClick.AddListener(() => Clicked?.Invoke(InstanceId));
        }

        public void Set(int instanceId, CardData data)
        {
            InstanceId = instanceId;
            frame.color = data.type == CardType.Counter ? counterColor : data.type == CardType.Trap ? trapColor : actionColor;
            nameText.text = data.cardName;
            descText.text = data.description;
            Sprite sprite = data.LoadArt();
            art.sprite = sprite;
            art.enabled = sprite != null;
        }
    }
}
