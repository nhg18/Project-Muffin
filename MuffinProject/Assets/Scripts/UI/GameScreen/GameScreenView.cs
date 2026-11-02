using UnityEngine;

namespace Chapchu.UI.GameScreen
{
    /// <summary>
    /// 인게임 화면 루트 — 영역별 뷰를 한 곳에 모아 둔다 (10-ui.md 2절). 이후 프레젠터가 여기서 뷰를 꺼내
    /// GameEvents 값을 넣고, 뷰의 입력 이벤트를 요청으로 잇는다. 이 클래스는 아무것도 판정 · 구독하지 않는다.
    /// </summary>
    public class GameScreenView : MonoBehaviour
    {
        [SerializeField] private RoomCodeView roomCode;
        [SerializeField] private HistoryView history;
        [SerializeField] private MyPanelView myPanel;
        [Tooltip("내 손패가 들어갈 영역. 카드 배치는 이후 작업 (10-ui.md 4절).")]
        [SerializeField] private RectTransform handArea;
        [SerializeField] private VictoryOverlayView victoryOverlay;

        public RoomCodeView RoomCode => roomCode;
        public HistoryView History => history;
        public MyPanelView MyPanel => myPanel;
        public RectTransform HandArea => handArea;
        public VictoryOverlayView VictoryOverlay => victoryOverlay;

        private void Awake()
        {
            if (roomCode == null || history == null || myPanel == null || handArea == null || victoryOverlay == null)
                Debug.LogError($"[{nameof(GameScreenView)}] 영역 뷰 참조 중 비어 있는 것이 있습니다.", this);
        }
    }
}
