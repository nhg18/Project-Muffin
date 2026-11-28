namespace Chapchu.Game
{
    // GameServer.Domain.cs — 1008 영역전개: 「영역」 상태 · 주인의 턴 시작 처리 (11-card-list 1008, 프로토타입 v3 domain)
    public partial class GameServer
    {
        // 영역의 턴 시작 처리(빼앗기 · 피해)를 화면이 보이는 시간 — 새 턴의 시간은 이 뒤에 흐른다.
        public const double DomainTickSeconds = 1.2;

        private int _domainOwner = -1; // 영역의 주인. -1 이면 영역 없음 — 판에 하나뿐이다
        private int _domainLeft;       // 남은 「주인의 실제 턴 시작」 횟수
        private int _domainTake;       // 턴 시작마다 한 사람에게서 빼앗는 장수
        private int _domainDamage;     // 턴 시작마다 한 사람에게 주는 피해

        /// <summary>지금 영역의 주인 (없으면 -1).</summary>
        internal int DomainOwner => _domainOwner;

        /// <summary>영역 시작 (StartDomainEffect). 남의 영역이 있으면 덮어쓰고, 내 영역이면 처음부터 다시 센다.</summary>
        internal void StartDomain(int user, int rounds, int take, int damage)
        {
            if (!IsInGame(user) || rounds <= 0)
                return;

            _domainOwner = user;
            _domainLeft = rounds;
            _domainTake = take;
            _domainDamage = damage;
            _events.Add(new DomainChanged(user, rounds, take, damage, false));
        }

        // actor 의 실제 턴이 시작됐다 — 영역의 주인이면 나를 뺀 생존자 모두에게서 빼앗고 피해를 준다. 처리가 있었으면 true.
        // 카드를 낸 것이 아니라 반응 시간이 없다. 사망 정리 · 함정 줄은 부른 쪽(AdvanceTurn)이 한다.
        private bool TickDomain(int actor)
        {
            if (_domainOwner != actor)
                return false;

            _domainLeft--;
            _events.Add(new DomainChanged(actor, _domainLeft, _domainTake, _domainDamage, true));
            foreach (int other in TurnOrderFrom(actor))
            {
                if (other == actor || !IsInGame(other) || _players[other].Life != LifeState.Alive)
                    continue;

                StealRandom(other, actor, _domainTake, actor);
                DealDamage(other, _domainDamage, actor);
            }

            if (_domainLeft <= 0)
                _domainOwner = -1;
            return true;
        }

        // actor 의 영역을 끝낸다 — 최종 사망 · 나감 (11-card-list 1008 「즉시 종료」).
        private void EndDomainOf(int actor)
        {
            if (_domainOwner != actor)
                return;

            _domainOwner = -1;
            _domainLeft = 0;
            _events.Add(new DomainChanged(actor, 0, _domainTake, _domainDamage, false));
        }

        // 게임 시작 · 1012 대폭발의 새 판 — 알리지 않는다 (화면은 새 판 알림에서 지운다)
        private void ResetDomain()
        {
            _domainOwner = -1;
            _domainLeft = 0;
            _domainTake = 0;
            _domainDamage = 0;
        }
    }
}
