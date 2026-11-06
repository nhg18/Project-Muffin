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
        [Tooltip("좌석 인덱스 1 · 2 · 3 순서 (02-player.md 3절).")]
        [SerializeField] private OpponentSeatView[] opponentSeats;
        [SerializeField] private MyPanelView myPanel;
        [SerializeField] private DeckView deck;
        [Tooltip("카드를 놓으면 사용 요청이 되는 영역 — 손패 영역 위 전체 (10-ui.md 2-1 「드롭 영역」). 그래픽은 없고 Rect 만 쓴다.")]
        [SerializeField] private RectTransform dropZone;
        [SerializeField] private VictoryOverlayView victoryOverlay;

        public RoomCodeView RoomCode => roomCode;
        public HistoryView History => history;
        public OpponentSeatView[] OpponentSeats => opponentSeats;
        public MyPanelView MyPanel => myPanel;
        public DeckView Deck => deck;
        public RectTransform DropZone => dropZone;
        public VictoryOverlayView VictoryOverlay => victoryOverlay;

        private void Awake()
        {
            if (roomCode == null || history == null || opponentSeats == null || opponentSeats.Length != 3 || myPanel == null || deck == null
                || dropZone == null || victoryOverlay == null)
                Debug.LogError($"[{nameof(GameScreenView)}] 영역 뷰 참조 중 비어 있는 것이 있습니다.", this);
        }
    }
}
