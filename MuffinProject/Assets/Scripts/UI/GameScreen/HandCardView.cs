using Chapchu.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 카드 한 장 (10-ui.md 2-1 「손패」). 종류별 틀 이미지는 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명만 입힌다.
    /// 어느 장인지(InstanceId)만 기억한다 — 판정 · 입력은 없다 (입력은 이후 작업).
    /// </summary>
    public class HandCardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image artImage;
        [SerializeField] private TMP_Text descriptionText;

        public int InstanceId { get; private set; }

        private void Awake()
        {
            if (nameText == null || artImage == null || descriptionText == null)
                Debug.LogError($"[{nameof(HandCardView)}] 이름 · 그림 · 설명 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void Setup(int instanceId, CardData data)
        {
            InstanceId = instanceId;
            nameText.text = data.cardName;
            descriptionText.text = data.description;

            // 그림이 아직 없는 카드가 있다 — 그림 칸만 비워 둔다.
            artImage.sprite = data.cardImage;
            artImage.enabled = data.cardImage != null;

            gameObject.name = $"HandCard_{instanceId}_{data.id}";
        }
    }
}
