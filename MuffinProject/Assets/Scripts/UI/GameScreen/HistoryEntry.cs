using Chapchu.Game;

namespace Chapchu.UI.GameScreen
{
    /// <summary>기록 항목 앞의 작은 그림 (10-ui.md 2-1 HUD v2 「기록」).</summary>
    public enum HistoryIcon
    {
        Card,     // 카드 1장 뽑기 등 — 카드 아이콘
        MiniCard, // 카드 사용 — 종류 색 띠가 있는 작은 카드
        Crown,    // 찹츄 선언 (기능 8)
    }

    /// <summary>
    /// 기록 한 줄 — 「누가 · 무엇을」 + 수치(HP 변화) 또는 턴 머리글. 표시 전용 사본이다 (10-ui.md 1절 #1).
    /// </summary>
    public class HistoryEntry
    {
        /// <summary>턴 머리글이면 true — Header 만 쓴다.</summary>
        public bool IsHeader;
        public string Header;

        public int Actor;
        public string Text;
        /// <summary>오른쪽 빨간 수치 (예: −20). 비어 있으면 없음.</summary>
        public string Value;
        public HistoryIcon Icon;
        public CardType CardType;
        /// <summary>카드 사용이면 대상 — HP 변화를 이 줄의 수치로 붙일지 판단한다.</summary>
        public int[] Targets;

        public static HistoryEntry HeaderOf(string header) => new HistoryEntry { IsHeader = true, Header = header };
    }
}
