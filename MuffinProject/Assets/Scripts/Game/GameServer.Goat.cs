using System;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Goat.cs — 1067 GOAT: 최대 HP 가 늘고 남에게 주는 피해가 배가 되는 상태 (11-card-list 1067 · 06-health 2절, 프로토타입 v3 goat)
    public partial class GameServer
    {
        /// <summary>화면이 받아들이는 HP 값의 위쪽 끝 — 방 · Player 속성을 거를 때 쓴다 (09-network.md 13절). 판정에는 쓰지 않는다.</summary>
        public const int HpLimit = 999;

        /// <summary>HP 막대의 가득 찬 값 — HP 가 100 을 넘으면 막대는 가득, 숫자만 그 값을 보인다 (10-ui 2-1).</summary>
        public static int HpBarMax(int hp) => Math.Max(MaxHp, hp);

        /// <summary>GOAT 시작 (StartGoatEffect). HP 0 이면 불발 (06 15절). 이미 GOAT 면 처음부터 다시.</summary>
        internal void StartGoat(int user, int rounds, int hp, int damageTimes)
        {
            if (!IsInGame(user) || rounds <= 0)
                return;

            PlayerState player = _players[user];
            if (player.Life != LifeState.Alive || player.Hp <= 0)
                return;

            player.GoatTurns = rounds;
            player.GoatMaxHp = Math.Max(MaxHp, hp);
            player.GoatDamageTimes = Math.Max(1, damageTimes);
            if (player.Hp < player.GoatMaxHp)
                SetHp(user, player.GoatMaxHp); // HP 설정 — 회복이 아니다 (회복 → 피해 전환 · 「회복한 후」 가 걸리지 않는다)
            _events.Add(new GoatChanged(GoatActors()));
        }

        // actor 의 실제 턴이 시작됐다 — GOAT 를 1 줄이고 0 이면 끝. HP 는 그대로 둔다 (100 을 넘는 HP 는 깎이기만 한다).
        private void TickGoat(int actor)
        {
            PlayerState player = _players[actor];
            if (player.GoatTurns <= 0 || --player.GoatTurns > 0)
                return;

            _events.Add(new GoatChanged(GoatActors()));
        }

        // actor 의 GOAT 를 끝낸다 — 최종 사망 · 나감.
        private void EndGoatOf(int actor)
        {
            PlayerState player = _players[actor];
            if (player.GoatTurns <= 0)
                return;

            player.GoatTurns = 0;
            _events.Add(new GoatChanged(GoatActors()));
        }

        // 그 사람의 HP 가 올라갈 수 있는 끝 — GOAT 동안은 GOAT 의 최대 HP. 아니면 100, 100 을 넘겨 남은 HP 는 지금 값이 끝이다 (06 2절).
        private int HpCapOf(PlayerState player) => player.GoatTurns > 0 ? player.GoatMaxHp : Math.Max(MaxHp, player.Hp);

        /// <summary>actor 의 HP 가 지금 올라갈 수 있는 끝 — 회복 효과 · 봇 점수의 「최대 HP」 (06 2절). 100 을 박아 쓰지 않는다.</summary>
        internal int HpCap(int actor) => HpCapOf(_players[actor]);

        /// <summary>actor 가 최대 HP 까지 모자란 양 — 「HP 를 모두 채운다」(3012 편안)의 회복량.</summary>
        internal int MissingHp(int actor) => Math.Max(0, HpCap(actor) - _players[actor].Hp);

        /// <summary>by 가 actor 에게 주는 피해 amount 를 지금 읽을 값 — by 가 GOAT 면 배로. 자기 피해 · 주인 없는 피해(-1)는 그대로.</summary>
        internal int DamageFrom(int by, int actor, int amount) =>
            by != actor && _players.TryGetValue(by, out PlayerState giver) && giver.GoatTurns > 0 ? amount * giver.GoatDamageTimes : amount;

        /// <summary>그 사람이 지금 GOAT 인가 (1067).</summary>
        internal bool IsGoat(int actor) => _players.TryGetValue(actor, out PlayerState player) && player.GoatTurns > 0;

        private int[] GoatActors() => _turnOrder.Where(actor => _players[actor].GoatTurns > 0).ToArray();
    }
}
