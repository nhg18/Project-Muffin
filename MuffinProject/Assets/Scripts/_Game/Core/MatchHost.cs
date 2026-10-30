using System;
using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// 판정하는 쪽. Match 를 들고 시계로 시간을 넣는다.
    /// 멀티면 방장 기기에만, 로컬이면 이 기기에 하나 있다. 요청은 전달 계층이 보낸 사람 ID 를 붙여 넘긴다.
    /// </summary>
    public class MatchHost
    {
        private readonly Match _match;
        private readonly Func<double> _clock;
        private bool _isStarted;

        /// <param name="clock">현재 시각. 멀티는 모두가 같은 서버 시간, 로컬은 이 기기 시간.</param>
        public MatchHost(IMatchEvents events, Func<double> clock)
        {
            _match = new Match(events);
            _clock = clock;
        }

        public void Start(IReadOnlyList<int> playerIds, IEnumerable<Card> cards, int seed)
        {
            _match.Start(playerIds, cards, seed, _clock());
            _isStarted = true;
        }

        /// <summary>매 프레임 부른다. 턴 마감을 확인한다.</summary>
        public void Tick()
        {
            if (_isStarted)
                _match.Tick(_clock());
        }

        // 시작 전에 온 요청은 판정할 상태가 없으므로 무시한다.
        public void Draw(int senderId)
        {
            if (_isStarted)
                _match.Draw(senderId, _clock());
        }

        public void PlayCard(int senderId, int instanceId, int[] targets)
        {
            if (_isStarted)
                _match.PlayCard(senderId, instanceId, targets, _clock());
        }
    }
}
