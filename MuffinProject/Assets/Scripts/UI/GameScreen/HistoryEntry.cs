using Chapchu.Game;

namespace Chapchu.UI.GameScreen
{
    /// <summary>기록 항목의 종류 표시.</summary>
    public enum HistoryIcon
    {
        Card,     // 카드 1장 뽑기 등 — 카드 아이콘
        MiniCard, // 카드 사용 — 종류 색 띠가 있는 작은 카드
        Crown,    // 찹츄 선언 (기능 8)
    }

    /// <summary>
    /// 기록 한 줄 — 「누가 · 무엇을」 + 수치(HP 변화) 또는 턴 머리글. 표시 전용 사본이다 (10-ui.md 1절 #1).
    /// 카드 사용 · 함정 설치 · 함정 발동 줄은 왼쪽 「낸 카드 기록」 에도 보인다 (InList, 통합 화면 UI v3 .hist).
    /// </summary>
    public class HistoryEntry
    {
        /// <summary>턴 머리글이면 true — Header 만 쓴다.</summary>
        public bool IsHeader;
        public string Header;

        public int Actor;
        public string Text;
        /// <summary>HP 변화 수치 (예: −20). 비어 있으면 없음.</summary>
        public string Value;
        public HistoryIcon Icon;
        public CardType CardType;
        /// <summary>카드 사용이면 대상 — HP 변화를 이 줄의 수치로 붙일지 판단한다.</summary>
        public int[] Targets;

        /// <summary>「낸 카드 기록」 에 보이는 줄 — 카드 사용 · 함정 설치 · 함정 발동.</summary>
        public bool InList;
        /// <summary>카드 종류 ID — 그림 · 이름. 함정 설치는 종류가 비공개라 0.</summary>
        public int CardId;
        /// <summary>체인의 카드 한 장 — 효과 결과 · 무효를 이 줄에 붙일 때 찾는다.</summary>
        public int InstanceId;
        /// <summary>함정 설치 — 카드 뒷면 · 「함정 설치」.</summary>
        public bool IsTrapSet;
        /// <summary>함정 발동 — 결과가 없으면 「함정 발동」.</summary>
        public bool IsTrap;
        /// <summary>효과 결과 한 마디 (행동 금지 · 2장 뽑기 …). HP 변화는 Value. 둘 다 비면 기본 문구.</summary>
        public string Result;
        /// <summary>무효가 됐다 — 흐리게 + 줄.</summary>
        public bool Void;

        public static HistoryEntry HeaderOf(string header) => new HistoryEntry { IsHeader = true, Header = header };
    }
}
