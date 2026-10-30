using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.EventSystems;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 덱 뒷면 · 드로우 버튼 뷰. 버튼 탭을 이벤트로만 알린다.
    /// 뽑을 수 있는지 스스로 판정하지 않는다 — 안 되면 서버가 거절로 알려준다.
    /// </summary>
    public class DeckView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Button DrawButton;
        public event Action DrawRequested;

        private void OnEnable()
        {
            if (DrawButton != null)
                DrawButton.onClick.AddListener(HandleDrawButtonClicked);
        }

        private void OnDisable()
        {
            if (DrawButton != null)
                DrawButton.onClick.RemoveListener(HandleDrawButtonClicked);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            HandleDrawButtonClicked();
        }

        private void HandleDrawButtonClicked() => DrawRequested?.Invoke();
    }
}
