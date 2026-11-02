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
        [SerializeField] private DeckView deck;
        [Tooltip("내 손패가 들어갈 영역. 카드 배치는 이후 작업 (10-ui.md 4절).")]
        [SerializeField] private RectTransform handArea;
        [SerializeField] private VictoryOverlayView victoryOverlay;

        /// <summary>좌상단 방 코드</summary>
        public RoomCodeView RoomCode => roomCode;
        /// <summary>왼쪽 히스토리</summary>
        public HistoryView History => history;
        /// <summary>좌하단 내 패널</summary>
        public MyPanelView MyPanel => myPanel;
        /// <summary>덱 더미</summary>
        public DeckView Deck => deck;
        /// <summary>내 손패 영역</summary>
        public RectTransform HandArea => handArea;
        /// <summary>승리 오버레이</summary>
        public VictoryOverlayView VictoryOverlay => victoryOverlay;

        private void Awake()
        {
            if (roomCode == null || history == null || myPanel == null || deck == null || handArea == null || victoryOverlay == null)
                Debug.LogError($"[{nameof(GameScreenView)}] 영역 뷰 참조 중 비어 있는 것이 있습니다.", this);
        }
    }
}
