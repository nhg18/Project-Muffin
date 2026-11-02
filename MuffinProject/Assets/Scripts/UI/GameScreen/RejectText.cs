using Chapchu.Game;

namespace Chapchu.UI.GameScreen
{
    /// <summary>거절 코드 → 화면 문구 (10-ui.md 8-2). 문구는 여기 한 곳에만 둔다.</summary>
    public static class RejectText
    {
        /// <summary>거절 코드(<see cref="RejectCode"/>)의 화면 문구. 모르는 코드는 번호를 그대로 보인다.</summary>
        public static string Get(int code) => code switch
        {
            RejectCode.NotYourTurn   => "내 턴이 아닙니다.",
            RejectCode.NotInHand     => "손패에 없는 카드입니다.",
            RejectCode.NoCardToDraw  => "뽑을 카드가 없습니다.",
            RejectCode.NotActionCard => "행동 카드만 낼 수 있습니다.",
            RejectCode.InvalidTarget => "대상이 올바르지 않습니다.",
            _                        => $"거절 (코드 {code})",
        };
    }
}
