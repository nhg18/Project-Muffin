namespace Chapchu.Core
{
    /// <summary>Resources 폴더 경로 모음 (CODE_CONVENTION 5절). Resources.Load 에 넘기는 경로는 여기에만 쓴다. 뒤에 프리팹 이름을 붙인다.</summary>
    public static class ResourcePaths
    {
        /// <summary>전역 매니저 프리팹 (01-game-flow).</summary>
        public const string Bootstrap = "Bootstrap/";

        /// <summary>팝업 프리팹, 이름 = 팝업 타입명 (10-ui 8절).</summary>
        public const string Popups = "Popups/";

        /// <summary>카드별 연출 프리팹, 이름 = 연출 컴포넌트 이름. 재생할 때만 불러온다 (10-ui 5-1).</summary>
        public const string CardFx = "CardFx/";
    }
}
