using Chapchu.Game;

namespace Chapchu.UI.GameScreen
{
    /// <summary>거절 코드 → 화면 문구 (10-ui.md 8-2). 문구는 여기 한 곳에만 둔다.</summary>
    public static class RejectText
    {
        public static string Get(int code)
        {
            switch (code)
            {
                case RejectCode.NotYourTurn:   return "내 턴이 아닙니다.";
                case RejectCode.NotInHand:     return "손패에 없는 카드입니다.";
                case RejectCode.NoCardToDraw:  return "뽑을 카드가 없습니다.";
                case RejectCode.NotActionCard: return "행동 카드만 낼 수 있습니다.";
                case RejectCode.InvalidTarget: return "대상이 올바르지 않습니다.";
                case RejectCode.ActionBlocked: return "이번 턴에는 행동 카드를 쓸 수 없습니다.";
                case RejectCode.NotChoosing:   return "고르는 시간이 끝났습니다.";
                default:                       return $"거절 (코드 {code})";
            }
        }
    }
}
