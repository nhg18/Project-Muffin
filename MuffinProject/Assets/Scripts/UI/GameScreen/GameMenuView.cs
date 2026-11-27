using System;
using UnityEngine;
using UnityEngine.UI;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 오른쪽 위 설정(톱니) 버튼 → 설정 판(「게임 나가기」) → 나가기 확인 팝업 (10-ui.md 2-1 HUD v2 「설정」 · 08-room.md 6절).
    /// 설정 판은 화면 가운데 · 바탕 어둡게 (통합 화면 UI v3 .gset · .popdim). 바탕을 눌러도 닫히지 않고 닫기(×)로 닫는다.
    /// 열고 닫기는 표시만이라 여기서 처리하고, 「나가기」 확정과 열림 바뀜(설정 판 · 확인 팝업 중 하나라도 떠 있는지 — 봇전 멈춤)을 이벤트로 알린다. 배경 음악 · 효과음 토글은 다음 작업 (2026-11-07) — 게임에 소리가 생기면 넣는다.
    /// 기본: 설정 판 · 팝업 비활성.
    /// </summary>
    public class GameMenuView : MonoBehaviour
    {
        [SerializeField] private Button gearButton;
        [SerializeField] private Image gearFill;
        [Tooltip("설정 판이 열려 있는 동안 설정(톱니) 버튼 바탕색. 닫혀 있으면 흰색.")]
        [SerializeField] private Color gearOpenColor = new Color32(0xff, 0xc1, 0x2e, 0xFF);
        [Tooltip("설정 판 — 바탕 어둡게(PopDim) + 판을 함께 켜고 끈다.")]
        [SerializeField] private GameObject sheet;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button exitButton;
        [SerializeField] private GameObject confirm;
        [SerializeField] private Button confirmCancelButton;
        [SerializeField] private Button confirmExitButton;

        /// <summary>확인 팝업에서 「나가기」를 눌렀다.</summary>
        public event Action ExitConfirmed;

        /// <summary>설정 판이나 나가기 확인 팝업이 떴다(true) · 둘 다 닫혔다(false). 판 → 확인 팝업으로 바뀔 때는 알리지 않는다.</summary>
        public event Action<bool> OpenChanged;

        private bool _open;

        private void Awake()
        {
            if (gearButton == null || gearFill == null || sheet == null || closeButton == null || exitButton == null || confirm == null || confirmCancelButton == null || confirmExitButton == null)
                Debug.LogError($"[{nameof(GameMenuView)}] 참조 중 비어 있는 것이 있습니다.", this);
        }

        private void OnEnable()
        {
            gearButton.onClick.AddListener(HandleGearClicked);
            closeButton.onClick.AddListener(HandleCloseClicked);
            exitButton.onClick.AddListener(HandleExitClicked);
            confirmCancelButton.onClick.AddListener(HandleConfirmCancelClicked);
            confirmExitButton.onClick.AddListener(HandleExitConfirmed);
        }

        private void OnDisable()
        {
            gearButton.onClick.RemoveListener(HandleGearClicked);
            closeButton.onClick.RemoveListener(HandleCloseClicked);
            exitButton.onClick.RemoveListener(HandleExitClicked);
            confirmCancelButton.onClick.RemoveListener(HandleConfirmCancelClicked);
            confirmExitButton.onClick.RemoveListener(HandleExitConfirmed);
        }

        private void Start()
        {
            sheet.SetActive(false);
            confirm.SetActive(false);
            gearFill.color = Color.white;
        }

        // PC Esc 로도 설정 판을 닫는다 (10-ui.md 9절 「설정 버튼」). 확인 팝업은 버튼으로만 닫는다.
        private void Update()
        {
            if (sheet.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                CloseSheet();
        }

        private void HandleGearClicked()
        {
            bool open = !sheet.activeSelf;
            sheet.SetActive(open);
            gearFill.color = open ? gearOpenColor : Color.white;
            NotifyOpenChanged();
        }

        private void CloseSheet()
        {
            sheet.SetActive(false);
            gearFill.color = Color.white;
            NotifyOpenChanged();
        }

        private void HandleCloseClicked() => CloseSheet();

        // 설정 판을 닫고 확인 팝업으로 바꾼다 (시안). 바탕을 눌러도 닫히지 않는다.
        private void HandleExitClicked()
        {
            confirm.SetActive(true); // 판을 닫기 전에 — 사이에 「닫힘」 이 끼지 않게
            CloseSheet();
        }

        private void HandleConfirmCancelClicked()
        {
            confirm.SetActive(false);
            NotifyOpenChanged();
        }

        private void NotifyOpenChanged()
        {
            bool open = sheet.activeSelf || confirm.activeSelf;
            if (open == _open)
                return;

            _open = open;
            OpenChanged?.Invoke(open);
        }

        private void HandleExitConfirmed()
        {
            confirmExitButton.interactable = false; // 두 번 누르기 방지 — 씬이 바뀐다
            ExitConfirmed?.Invoke();
        }
    }
}
