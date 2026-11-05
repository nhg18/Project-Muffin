using System;
using Chapchu.Cards;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 내 손패 카드 한 장 (10-ui.md 2-1 「손패」). 종류별 틀 이미지는 프리팹(행동 · 카운터 · 함정)이 갖고, 여기서는 CardData 의 이름 · 그림 · 설명만 입힌다.
    /// 포인터 진입 · 이탈 · 클릭을 이벤트로만 알린다 — 올리기 · 호버 판정은 HandView 가 한다 (10-ui.md 4절). 드래그는 이후 작업.
    /// 어느 장인지(InstanceId)만 기억한다.
    /// </summary>
    public class HandCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image artImage;
        [SerializeField] private TMP_Text descriptionText;

        public int InstanceId { get; private set; }

        /// <summary>포인터가 이 카드 위에 들어왔다.</summary>
        public event Action<HandCardView> OnHoverEntered;
        /// <summary>포인터가 이 카드에서 나갔다.</summary>
        public event Action<HandCardView> OnHoverExited;
        /// <summary>이 카드를 탭(클릭)했다.</summary>
        public event Action<HandCardView> OnClicked;

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

        public void OnPointerEnter(PointerEventData eventData) => OnHoverEntered?.Invoke(this);

        public void OnPointerExit(PointerEventData eventData) => OnHoverExited?.Invoke(this);

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);
    }
}
