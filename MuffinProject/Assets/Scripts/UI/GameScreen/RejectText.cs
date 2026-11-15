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
                case RejectCode.NotYourTurn:   return "내 턴이 아니에요.";
                case RejectCode.NotInHand:     return "손패에 없는 카드예요.";
                case RejectCode.NoCardToDraw:  return "뽑을 카드가 없어요.";
                case RejectCode.NotActionCard: return "행동 카드만 낼 수 있어요.";
                case RejectCode.InvalidTarget: return "그 대상에게는 쓸 수 없어요.";
                case RejectCode.ActionBlocked: return "이번 턴에는 행동 카드를 쓸 수 없어요.";
                case RejectCode.NotChoosing:   return "고르는 시간이 끝났어요.";
                default:                       return $"거절 (코드 {code})";
            }
        }
    }
}
