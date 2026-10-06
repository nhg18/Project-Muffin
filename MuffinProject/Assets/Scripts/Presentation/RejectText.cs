using Chapchu.Core;

namespace Chapchu.Presentation
{
    /// <summary>거절 코드 → 화면 문구. 문구는 여기 한 곳에만 둔다 (다국어를 넣으면 이 표를 바꾼다).</summary>
    public static class RejectText
    {
        public static string Get(int code)
        {
            switch (code)
            {
                case RejectCode.NotYourTurn:            return "내 턴이 아닙니다.";
                case RejectCode.NotInHand:              return "손패에 없는 카드입니다.";
                case RejectCode.NoCardToDraw:           return "뽑을 카드가 없습니다.";
                case RejectCode.NotActionCard:          return "행동 카드만 낼 수 있습니다.";
                case RejectCode.InvalidTarget:          return "대상이 올바르지 않습니다.";
                case RejectCode.ChainInProgress:        return "카드 처리 중에는 할 수 없습니다.";
                case RejectCode.NothingToCounter:       return "반응할 카드가 없습니다.";
                case RejectCode.ReactionClosed:         return "반응 시간이 끝났습니다.";
                case RejectCode.NotCounterCard:         return "카운터 카드가 아닙니다.";
                case RejectCode.TargetChanged:          return "다른 카드가 먼저 반응했습니다.";
                case RejectCode.CounterConditionNotMet: return "이 카드에는 반응할 수 없습니다.";
                default:                                return $"거절 (코드 {code})";
            }
        }
    }
}
