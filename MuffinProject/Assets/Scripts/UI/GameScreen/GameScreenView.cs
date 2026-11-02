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
        [Tooltip("좌석 인덱스 1 · 2 · 3 순서 (02-player.md 3절). 0번(나)은 myPanel.")]
        [SerializeField] private OpponentSeatView[] opponentSeats = new OpponentSeatView[3];
        [SerializeField] private MyPanelView myPanel;
        [SerializeField] private DeckView deck;
        [SerializeField] private DiscardPileView discardPile;
        [Tooltip("내 손패가 들어갈 영역. 카드 배치는 이후 작업 (10-ui.md 4절).")]
        [SerializeField] private RectTransform handArea;
        [SerializeField] private VictoryOverlayView victoryOverlay;

        public RoomCodeView RoomCode => roomCode;
        public HistoryView History => history;
        public OpponentSeatView[] OpponentSeats => opponentSeats;
        public MyPanelView MyPanel => myPanel;
        public DeckView Deck => deck;
        public DiscardPileView DiscardPile => discardPile;
        public RectTransform HandArea => handArea;
        public VictoryOverlayView VictoryOverlay => victoryOverlay;

        private void Awake()
        {
            if (roomCode == null || history == null || myPanel == null || deck == null || discardPile == null || handArea == null || victoryOverlay == null)
                Debug.LogError($"[{nameof(GameScreenView)}] 영역 뷰 참조 중 비어 있는 것이 있습니다.", this);

            for (int i = 0; i < opponentSeats.Length; i++)
            {
                if (opponentSeats[i] == null)
                    Debug.LogError($"[{nameof(GameScreenView)}] {nameof(opponentSeats)}[{i}] 참조가 비어 있습니다.", this);
            }
        }
    }
}
