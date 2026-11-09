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

        /// <summary>체인 맨 위 카드. 없으면 null.</summary>
        internal ChainEntry ChainTop => _chain.Count > 0 ? _chain[_chain.Count - 1] : null;

        /// <summary>반응 시간이 열리는 시각 — 카드가 체인에 오를 때마다 바뀐다 (그 반응 창을 알아보는 값).</summary>
        internal double ReactionOpensAt => _reactionOpensAt;

        internal bool IsReactionOpen(double now) => _chain.Count > 0 && now >= _reactionOpensAt && now < _reactionDeadline;

        internal bool IsTrapWindowOpenAt(double now) => IsTrapWindowOpen && now >= _trapWindowOpensAt;

        internal double TrapWindowOpensAt => _trapWindowOpensAt;

        /// <summary>actor 의 「~한 후」 함정 trap 을 지금 쓸 사건이 있다 (04 6절).</summary>
        internal bool HasAfterTrigger(int actor, Card trap) => IsAfterTrigger(trap.Trigger) && FindAfterRecord(actor, trap) != null;

        internal double DeathDeadline => _deathDeadline;
    }
}
