using System.Collections.Generic;
using Chapchu.Game.Effects;

namespace Chapchu.Game
{
    // GameServer.Choice.cs — 효과를 받는 사람이 고르는 효과 (119 목표 조준, 11-card-list 119).
    // 고르는 동안 체인 처리 · 게임 · 턴 시간이 멈춘다. 시간 안에 안 고르면 방장이 무작위. 고르는 사람이 나가면 그 효과는 불발 (사용자 2026-11-13).
    public partial class GameServer
    {
        private int _choiceActor = -1;    // 고르는 사람. -1 이면 없음
        private ChainEntry _choiceEntry;
        private DiscardOrDamageEffect _choiceEffect;
        private double _choiceDeadline;

        internal bool IsChoosing => _choiceActor != -1;
        internal int ChoiceActor => _choiceActor;
        internal double ChoiceDeadline => _choiceDeadline;

        /// <summary>효과가 고르기를 연다 (DiscardOrDamageEffect.Apply). 처리는 여기서 멈추고, 고르면 Advance 가 남은 처리를 이어 간다.</summary>
        internal void BeginChoice(ChainEntry entry, DiscardOrDamageEffect effect, int actor)
        {
            double now = _clock();
            _choiceActor = actor;
            _choiceEntry = entry;
            _choiceEffect = effect;
            _choiceDeadline = now + effect.seconds;
            _events.Add(new ChoiceStarted(actor, entry.User, entry.Card.CardId, _players[actor].Hand.Count, effect.amount, now, _choiceDeadline));
        }

        /// <summary>
        /// 고르기 요청 (119 — 0 손패 전부 버리기 · 1 피해 받기). 고르는 사람만, 고르는 중에만.
        /// </summary>
        public IReadOnlyList<GameEvent> Choose(int requester, int option)
        {
            if (_gameOver)
                return Reject(requester, RejectCode.GameOver);
            if (!IsChoosing || requester != _choiceActor || option < 0 || option > 1)
                return Reject(requester, RejectCode.NotChoosing);

            FinishChoice(option, false);
            return TakeEvents();
        }

        // 고른 것을 적용한다. 남은 처리(체인)는 Advance 가 이어 간다 — 그 피해의 사망 대기 · 함정 · 연출 시간만큼 다음 턴 미루기도 거기서 (04 8 · 9.3절).
        private void FinishChoice(int option, bool timedOut)
        {
            int actor = _choiceActor;
            ChainEntry entry = _choiceEntry;
            DiscardOrDamageEffect effect = _choiceEffect;
            ClearChoice();

            _events.Add(new ChoiceMade(actor, option, timedOut));
            effect.ApplyOption(this, entry, actor, option);
        }

        // 고르는 사람이 나갔다 — 그 효과만 불발 (사용자 2026-11-13). 남은 처리는 Advance 가 이어 간다.
        private void CancelChoiceOf(int actor)
        {
            if (_choiceActor != actor)
                return;
            ClearChoice();
            _events.Add(new ChoiceMade(actor, -1, false));
        }

        private void ClearChoice()
        {
            _choiceActor = -1;
            _choiceEntry = null;
            _choiceEffect = null;
            _choiceDeadline = 0;
        }

        /// <summary>봇이 고를 것 (18-bot 7-3, 프로토타입 v2).</summary>
        internal int BotChoiceOption(int bot) => _choiceEffect != null ? _choiceEffect.BotOption(this, bot) : 0;
    }
}
