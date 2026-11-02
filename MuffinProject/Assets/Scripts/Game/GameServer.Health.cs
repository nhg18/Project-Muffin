using System;

namespace Chapchu.Game
{
    // GameServer.Health.cs — 체력 원본(PlayerState.Hp) 변경 (06-health.md)
    public partial class GameServer
    {
        /// <summary>체력을 amount 만큼 줄인다. 카드 효과(Effects/)가 부른다.</summary>
        internal void DealDamage(int actor, int amount)
        {
            SetHp(actor, _players[actor].Hp - amount);
        }

        // 체력은 여기서만 바꾼다 — 원본 변경과 일어난 일이 항상 같이 나간다. 0 미만 · 최대치 초과 불가 (06-health.md 2절).
        private void SetHp(int actor, int hp)
        {
            PlayerState player = _players[actor];
            player.Hp = Math.Clamp(hp, 0, MaxHp);
            _events.Add(new HpChanged(actor, player.Hp));
        }
    }
}
