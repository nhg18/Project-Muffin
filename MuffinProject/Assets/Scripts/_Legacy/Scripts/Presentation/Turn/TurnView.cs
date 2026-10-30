using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 턴 표시 · 턴 종료 버튼 뷰. 값을 받아 표시하고, 버튼 탭을 이벤트로만 알린다.
    /// 누구 턴인지 판정하지 않는다 — 누를 수 있는지는 서버가 거절로 알려준다.
    /// 턴 종료 버튼은 개발용 디버그 입력이다 (03-turn.md 8절).
    /// </summary>
    public class TurnView : MonoBehaviour
    {
        private const string NotStartedText = "시작 전";

        [SerializeField] private TMP_Text turnText;
        [SerializeField] private Button endTurnButton;

        public event Action EndTurnRequested;

        private void OnEnable()
        {
            endTurnButton.onClick.AddListener(HandleEndTurnClicked);
        }

        private void OnDisable()
        {
            endTurnButton.onClick.RemoveListener(HandleEndTurnClicked);
        }

        /// <summary>현재 턴 주인 표시. -1 이면 시작 전.</summary>
        public void SetTurnActor(int actorNumber)
        {
            turnText.text = actorNumber < 0 ? NotStartedText : $"플레이어 {actorNumber}의 턴";
        }

        private void HandleEndTurnClicked() => EndTurnRequested?.Invoke();
    }
}
