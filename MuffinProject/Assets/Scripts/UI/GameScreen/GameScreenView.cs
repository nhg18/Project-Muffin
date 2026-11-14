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
        [SerializeField] private TurnStripView turnStrip;
        [SerializeField] private FeedView feed;
        [SerializeField] private LogSheetView log;
        [Tooltip("좌석 인덱스 1 · 2 · 3 순서 (02-player.md 3절).")]
        [SerializeField] private OpponentSeatView[] opponentSeats;
        [SerializeField] private MyPanelView myPanel;
        [SerializeField] private ChapchuButtonView chapchuButton;
        [SerializeField] private DeckView deck;
        [SerializeField] private TargetAskView targetAsk;
        [SerializeField] private GameMenuView menu;

        public RoomCodeView RoomCode => roomCode;
        public TurnStripView TurnStrip => turnStrip;
        public FeedView Feed => feed;
        public LogSheetView Log => log;
        public OpponentSeatView[] OpponentSeats => opponentSeats;
        public MyPanelView MyPanel => myPanel;
        public ChapchuButtonView ChapchuButton => chapchuButton;
        public DeckView Deck => deck;
        public TargetAskView TargetAsk => targetAsk;
        public GameMenuView Menu => menu;

        private void Awake()
        {
            if (roomCode == null || turnStrip == null || feed == null || log == null || opponentSeats == null || opponentSeats.Length != 3 || myPanel == null
                || chapchuButton == null || deck == null || targetAsk == null || menu == null)
                Debug.LogError($"[{nameof(GameScreenView)}] 영역 뷰 참조 중 비어 있는 것이 있습니다.", this);
        }
    }
}
