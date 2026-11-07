using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Health.cs — 체력 원본(PlayerState.Hp) 변경 · 사망 대기 · 최종 사망 (06-health.md)
    public partial class GameServer
    {
        // 확정 (06-health.md 2절).
        private const double DeathResponseTime = 5;

        private double _deathDeadline = -1; // 사망 대기 마감. 음수면 사망 대기 없음 — 있으면 게임이 멈춘다 (06 8절)

        private bool IsDeathPending => _deathDeadline >= 0;

        /// <summary>체력을 amount 만큼 줄인다. 카드 효과(Effects/)가 부른다. source = 피해를 준 사람 (없으면 -1) — 「피해 받은 후」 함정의 조건.</summary>
        internal void DealDamage(int actor, int amount, int source)
        {
            if (amount <= 0)
                return; // 최종 피해 0 이면 사건 자체가 없다 (06 4절)

            SetHp(actor, _players[actor].Hp - amount);
            RecordDamage(actor, source);
        }

        // 체력은 여기서만 바꾼다 — 원본 변경과 일어난 일이 항상 같이 나간다. 0 미만 · 최대치 초과 불가 (06-health.md 2절).
        private void SetHp(int actor, int hp)
        {
            PlayerState player = _players[actor];
            player.Hp = Math.Clamp(hp, 0, MaxHp);
            _events.Add(new HpChanged(actor, player.Hp));
        }

        // 체인 처리 · 빈 덱 피해 뒤에 부른다. HP 0 인 생존자 전원을 사망 대기로 (06 8 · 11절).
        // startsAt = HP 0 이 되는 연출이 끝나는 시각 — 대응 5초는 그 뒤부터 (06 8절).
        private void CheckDeathPending(double startsAt)
        {
            int[] dying = _turnOrder.Where(a => IsInGame(a) && _players[a].Life == LifeState.Alive && _players[a].Hp == 0).ToArray();
            if (dying.Length == 0)
                return;

            double deadline = startsAt + DeathResponseTime;
            _deathDeadline = Math.Max(_deathDeadline, deadline);
            foreach (int actor in dying)
            {
                _players[actor].Life = LifeState.DeathPending;
                _events.Add(new DeathPending(actor, startsAt, deadline));
            }
        }

        // 사망 대기 마감에 Tick 이 부른다. 대응 뒤에도 HP 0 인 사람을 함께 최종 사망 (06 9 · 11절).
        private void FinalizeDeaths()
        {
            _deathDeadline = -1;
            foreach (int actor in _turnOrder)
            {
                PlayerState player = _players[actor];
                if (player.Life != LifeState.DeathPending)
                    continue;

                if (player.HasLeft)
                {
                    player.Life = LifeState.Dead; // 대기 중에 나갔다 — 나감 처리(PlayerLeft)가 이미 끝났다
                    continue;
                }

                if (player.Hp > 0)
                {
                    player.Life = LifeState.Alive;
                    _events.Add(new DeathAvoided(actor, player.Hp));
                    continue;
                }

                player.Life = LifeState.Dead;
                DiscardAll(actor);
                _events.Add(new PlayerDied(actor, _discardPile.Count));
            }
        }

        // 사망 · 나감 — 손패(와 설치 함정)를 전부 버림 더미로 (06 9절 · 01 6절).
        private void DiscardAll(int actor)
        {
            PlayerState player = _players[actor];
            _discardPile.AddRange(player.Hand);
            _discardPile.AddRange(player.Traps);
            player.Hand.Clear();
            player.Traps.Clear();
            OnHandChanged(actor);
        }
    }
}
