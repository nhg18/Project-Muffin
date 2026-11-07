using System;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 오른쪽 위 설정(톱니) 버튼 → 설정 서랍(「게임 나가기」) → 나가기 확인 팝업 (10-ui.md 2-1 HUD v2 「설정」 · 08-room.md 6절).
    /// 서랍 열고 닫기는 표시만이라 여기서 처리하고, 「나가기」 확정만 이벤트로 알린다. 배경 음악 · 효과음 토글은 다음 작업 (2026-11-07).
    /// 기본: 서랍 · 팝업 비활성.
    /// </summary>
    public class GameMenuView : MonoBehaviour
    {
        [SerializeField] private Button gearButton;
        [SerializeField] private Image gearFill;
        [SerializeField] private Color gearOpenColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [SerializeField] private GameObject sheet;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject confirm;
        [SerializeField] private Button confirmCancelButton;
        [SerializeField] private Button confirmExitButton;

        /// <summary>확인 팝업에서 「나가기」를 눌렀다.</summary>
        public event Action ExitConfirmed;

        private void Awake()
        {
            if (gearButton == null || gearFill == null || sheet == null || closeButton == null || exitButton == null || confirm == null || confirmCancelButton == null || confirmExitButton == null)
                Debug.LogError($"[{nameof(GameMenuView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            gearButton.onClick.AddListener(ToggleSheet);
            closeButton.onClick.AddListener(CloseSheet);
            exitButton.onClick.AddListener(OpenConfirm);
            confirmCancelButton.onClick.AddListener(CloseConfirm);
            confirmExitButton.onClick.AddListener(HandleExitConfirmed);
        }

        private void OnDisable()
        {
            gearButton.onClick.RemoveListener(ToggleSheet);
            closeButton.onClick.RemoveListener(CloseSheet);
            exitButton.onClick.RemoveListener(OpenConfirm);
            confirmCancelButton.onClick.RemoveListener(CloseConfirm);
            confirmExitButton.onClick.RemoveListener(HandleExitConfirmed);
        }

        private void Start()
        {
            sheet.SetActive(false);
            confirm.SetActive(false);
            gearFill.color = Color.white;
        }

        private void ToggleSheet()
        {
            bool open = !sheet.activeSelf;
            sheet.SetActive(open);
            gearFill.color = open ? gearOpenColor : Color.white;
        }

        private void CloseSheet()
        {
            sheet.SetActive(false);
            gearFill.color = Color.white;
        }

        // 서랍을 닫고 확인 팝업으로 바꾼다 (시안). 바탕을 눌러도 닫히지 않는다.
        private void OpenConfirm()
        {
            CloseSheet();
            confirm.SetActive(true);
        }

        private void CloseConfirm() => confirm.SetActive(false);

        private void HandleExitConfirmed()
        {
            confirmExitButton.interactable = false; // 두 번 누르기 방지 — 씬이 바뀐다
            ExitConfirmed?.Invoke();
        }
    }
}
