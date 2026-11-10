using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 함정 칸 하나 (10-ui.md 2-1 「함정 칸」, 시안 .slot) — 테이블 면에 누운 빈 칸, 설치되면 뒷면 카드. 종류는 보이지 않는다 (04-card.md 6절).
    /// 내 칸은 콜라이더가 있어 탭을 알린다 (설치한 함정 크게 보기). 상대 칸은 콜라이더가 없어 탭이 오지 않는다.
    /// </summary>
    public class TrapSlotView : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("빈 칸 — 반투명 흰 바탕.")]
        [SerializeField] private GameObject empty;
        [Tooltip("설치된 칸 — 뒷면 카드.")]
        [SerializeField] private GameObject filled;

        public event Action<TrapSlotView> OnClicked;

        private void Awake()
        {
            if (empty == null || filled == null)
                Debug.LogError($"[{nameof(TrapSlotView)}] {nameof(empty)} · {nameof(filled)} 참조 중 비어 있는 것이 있습니다.", this);
        }

        public void SetFilled(bool isFilled)
        {
            empty.SetActive(!isFilled);
            filled.SetActive(isFilled);
        }

        public void OnPointerClick(PointerEventData eventData) => OnClicked?.Invoke(this);
    }
}
