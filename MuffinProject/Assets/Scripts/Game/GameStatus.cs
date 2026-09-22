using Muffin.Core;

namespace Muffin.Game
{

    public class GameStatus : Singleton<GameStatus>
    {
        public int MaxHp = 100;
        public int StartHandCount = 5;
    }
}
