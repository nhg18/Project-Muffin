using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Status.cs — 다음 턴까지 이어지는 상태: 행동 카드 금지(107 짖기) · 기다려(123 기다려!) (11-card-list 107 · 123, 03-turn.md 7절, 07 4.3)
    // 둘 다 「다음 턴」 은 실제로 오는 턴이다 — 스킵된 턴은 그 사람의 턴으로 세지 않는다 (03-turn.md 7절, 2026-11-13).
    public partial class GameServer
    {
        // 「기다려」 를 건 사람들 (건 순서). 비어 있지 않으면 아무도 찹츄로 이기지 않는다. 건 사람의 다음 실제 턴이 시작되기 전 · 최종 사망 · 나가면 빠진다.
        private readonly List<int> _waitCasters = new List<int>();

        internal bool IsWaiting => _waitCasters.Count > 0;

        /// <summary>actor 의 다음 실제 턴에 행동 카드를 금지한다 (107). 겹쳐도 1회.</summary>
        internal void MarkActionBlock(int actor)
        {
            PlayerState player = _players[actor];
            if (!IsInGame(actor) || player.BlockActionNextTurn)
                return;

            player.BlockActionNextTurn = true;
            _events.Add(new ActionBlockChanged(actor, ActionBlockOf(player)));
        }

        /// <summary>user 의 다음 실제 턴이 시작되기 전까지 「기다려」 (123). 이미 걸어 둔 사람이 다시 쓰면 그대로.</summary>
        internal void StartWait(int user)
        {
            if (!IsInGame(user) || _waitCasters.Contains(user))
                return;

            _waitCasters.Add(user);
            _events.Add(new WaitChanged(_waitCasters.ToArray()));
        }

        // 턴이 끝날 때 — 이번 턴의 행동 카드 금지를 푼다. 걸려 있던 다음 턴 금지는 남는다.
        private void EndTurnStatus(int actor)
        {
            if (actor < 0 || !_players.TryGetValue(actor, out PlayerState player) || !player.ActionBlocked)
                return;

            player.ActionBlocked = false;
            _events.Add(new ActionBlockChanged(actor, ActionBlockOf(player)));
        }

        // 실제 턴이 시작될 때 — 걸려 있던 행동 카드 금지가 이번 턴 금지가 된다.
        private void BeginTurnStatus(int actor)
        {
            PlayerState player = _players[actor];
            if (!player.BlockActionNextTurn)
                return;

            player.BlockActionNextTurn = false;
            player.ActionBlocked = true;
            _events.Add(new ActionBlockChanged(actor, ActionBlockOf(player)));
        }

        // actor 가 건 「기다려」 를 끝낸다 — 그 사람의 다음 실제 턴 시작 전 · 최종 사망 · 나감 (11-card-list 123 「즉시 종료」).
        private void EndWaitOf(int actor)
        {
            if (_waitCasters.Remove(actor))
                _events.Add(new WaitChanged(_waitCasters.ToArray()));
        }

        private static ActionBlock ActionBlockOf(PlayerState player) =>
            player.ActionBlocked ? ActionBlock.ThisTurn : player.BlockActionNextTurn ? ActionBlock.NextTurn : ActionBlock.None;

        /// <summary>디버그 로그용 — 상태가 있을 때만 「 · 금지 P2 · 기다려 P1」 꼴. 판정에 쓰지 않는다.</summary>
        private string DebugStatus()
        {
            string blocked = string.Join(" ", _players.Where(p => p.Value.ActionBlocked || p.Value.BlockActionNextTurn)
                .Select(p => $"P{p.Key}{(p.Value.ActionBlocked ? "" : "(다음)")}"));
            string wait = string.Join(" ", _waitCasters.Select(a => $"P{a}"));
            return (blocked.Length > 0 ? $" · 금지 {blocked}" : "") + (wait.Length > 0 ? $" · 기다려 {wait}" : "")
                + (_restartCount > 0 ? $" · 피폭 n{_restartCount} k{_falloutTurns}" : "");
        }
    }
}
