using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Bot.cs — 봇(Bots/BotDriver)이 읽는 값 (18-bot.md 3절). 봇은 규칙을 바꾸지 않고 읽기만 한다 — 행동은 사람과 같은 요청 메서드로.
    public partial class GameServer
    {
        /// <summary>턴이 시작될 때마다 1 씩 는다 — 봇이 「새 턴」 을 알아보는 데 쓴다.</summary>
        internal int TurnSerial { get; private set; }

        internal bool MainActionUsed => _mainActionUsed;

        /// <summary>체인 · 함정 시간 · 사망 대기 등으로 메인 행동을 할 수 없는 때 (코드 18) · 게임 끝.</summary>
        internal bool IsBusy => _gameOver || IsPaused(_clock());

        internal PlayerState StateOf(int actor) => _players[actor];

        internal bool InGame(int actor) => _players.ContainsKey(actor) && IsInGame(actor);

        /// <summary>게임에 남은 사람 (턴 순서대로).</summary>
        internal IEnumerable<int> ActorsInGame => _turnOrder.Where(IsInGame);
    }
}
