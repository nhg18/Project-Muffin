using System;
using System.Collections.Generic;
using System.Linq;

namespace Chapchu.Game
{
    // GameServer.Health.cs — 체력 원본(PlayerState.Hp) 변경 · 사망 정리(사망 대기 능력 · 최종 사망) (06-health.md)
    public partial class GameServer
    {
        /// <summary>체력을 amount 만큼 줄인다. 카드 효과(Effects/)가 부른다. source = 피해를 준 사람 (없으면 -1) — 「피해 받은 후」 함정의 조건.</summary>
        internal void DealDamage(int actor, int amount, int source)
        {
            if (amount <= 0)
                return; // 최종 피해 0 이면 사건 자체가 없다 (06 4절)

            CountDamage(actor, source, -SetHp(actor, _players[actor].Hp - amount, source));
            RecordDamage(actor, source, amount);
        }

        // 체력은 여기서만 바꾼다 — 원본 변경과 일어난 일이 항상 같이 나간다. 0 미만 · 최대치 초과 불가 (06-health.md 2절).
        // source = 이 변화를 일으킨 카드의 주인 (없으면 -1). HP 가 0 이 되는 순간의 주인을 적어 둔다 — 결과 화면 「○○에게 당함」 (07 11-3 「킬」).
        // 자기 피해는 킬이 아니다. HP 가 다시 0 보다 커지면(사망을 면함) 지운다.
        // 돌려주는 값 = 실제로 변한 양 (늘면 +, 줄면 −) — 지표는 이 값으로 센다 (07 11-4).
        private int SetHp(int actor, int hp, int source = -1)
        {
            PlayerState player = _players[actor];
            int before = player.Hp;
            player.Hp = Math.Clamp(hp, 0, MaxHp);
            if (before <= 0 && player.Hp > 0)
                _handDirty.Add(actor); // HP 0 에서 살아났다 — 지금 10장이면 찹츄 선언 (07 4.1, PM 2026-11-19)
            if (player.Hp > 0)
                player.DownedBy = -1;
            else if (before > 0)
                player.DownedBy = source != actor ? source : -1;

            _events.Add(new HpChanged(actor, player.Hp));
            return player.Hp - before;
        }

        // 사망 정리 (06 3 · 8 · 9절, 04 9.2) — 처리가 끝날 때마다(체인 끝 · 함정 한 장 뒤 · 체인 밖 처리 뒤) 부른다. 기다림이 없다 — 그 자리에서 끝난다.
        // HP 0 인 사람: 사망 대기 능력(「본인 사망」 함정 — 3008 죽은 척)이 있으면 바로 발동한다 — 현재 턴 플레이어부터 턴 방향, 한 사람 안에서는 슬롯 순서.
        // 생존 능력으로 HP 가 1 이상이 되면 남은 사망 능력은 쓰지 않는다. 능력을 모두 처리한 뒤 남은 HP 0 인 사람을 함께 최종 사망 (06 11절).
        // 사망 대기 능력이 만든 사건으로는 함정 줄을 열지 않는다 (04 6절). at = 앞 연출이 끝나는 시각, 능력의 연출이 끝나는 시각을 돌려준다.
        private double SettleDeaths(double at)
        {
            int[] dying = TurnOrderFromCurrent().Where(a => IsInGame(a) && _players[a].Life == LifeState.Alive && _players[a].Hp <= 0).ToArray();
            if (dying.Length == 0)
                return at;

            foreach (int actor in dying)
                _players[actor].Life = LifeState.DeathPending; // 방장 안에서만 — 알리지 않는다 (06 13절)

            foreach (int actor in dying)
            {
                foreach (Card trap in _players[actor].Traps.Where(t => t.Trigger == TrapTrigger.SelfDeathPending).ToList())
                {
                    if (_players[actor].Hp > 0 || !_players[actor].Traps.Contains(trap))
                        break;
                    at = ResolveTrap(actor, trap, null, at);
                    if (_restartPending || _gameOver)
                        break;
                }
            }
            _afterRecords.Clear(); // 사망 대기 능력이 만든 일은 함정 줄을 열지 않는다

            bool died = false;
            foreach (int actor in dying)
            {
                PlayerState player = _players[actor];
                if (player.Life != LifeState.DeathPending)
                    continue; // 살아났다 (SurviveNow)

                if (player.Hp > 0)
                {
                    player.Life = LifeState.Alive;
                    _events.Add(new DeathAvoided(actor, player.Hp));
                    _handDirty.Add(actor); // 살아났다 — 지금 10장이면 찹츄 선언 (07 4.1, PM 2026-11-19)
                    continue;
                }

                died = true;
                player.Life = LifeState.Dead;
                AddStat(player.DownedBy, StatKind.Kills); // 킬은 최종 사망이 확정될 때 (07 11-3)
                player.OutTurn = TurnSerial; // 턴 밖(사망 대기)에 죽으면 그때 진행 중인 턴의 번호 (07 11-3)
                int[] discarded = DiscardAll(actor);
                _events.Add(new PlayerDied(actor, discarded, _discardPile.Count));
                EndWaitOf(actor); // 건 사람이 죽으면 「기다려」 즉시 종료 (11-card-list 1023)
                ClearChapchu(actor); // 죽은 사람의 찹츄 표시가 남지 않게 해제를 알린다 (07 9절)
            }

            if (died)
                CheckElimination();
            return at;
        }

        // 사망 · 나감 — 손패(와 설치 함정)를 전부 버림 더미로 (06 9절 · 01 6절). 버림 더미에 들어간 순서의 카드 종류를 돌려준다 — 버림 더미는 공개 (05 7절).
        private int[] DiscardAll(int actor)
        {
            PlayerState player = _players[actor];
            int[] cardIds = player.Hand.Concat(player.Traps).Select(c => c.CardId).ToArray();
            _discardPile.AddRange(player.Hand);
            _discardPile.AddRange(player.Traps);
            player.Hand.Clear();
            player.Traps.Clear();
            OnHandChanged(actor);
            return cardIds;
        }
    }
}
