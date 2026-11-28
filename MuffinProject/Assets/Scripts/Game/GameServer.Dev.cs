using System.Collections.Generic;

namespace Chapchu.Game
{
    // GameServer.Dev.cs — 개발자 도구 (10-ui.md 12절). 개발 방(매치가 아닌 방)에서만 — 거르기는 전송 층(PunGameServer)이 한다.
    // 규칙을 바꾸지 않고 시험을 돕는 것만 둔다: 턴 시간 무제한 · 카드 한 장 손패로.
    public partial class GameServer
    {
        // 「무제한」 턴의 길이 — 사실상 끝나지 않는다. 화면의 남은 초는 이 값을 보고 숨는다
        public const double DevUnlimitedTurnSeconds = 99999;

        /// <summary>턴 시간 무제한 — 켜면 새 턴의 마감이 아주 멀다. 지금 턴에도 바로 적용한다 (마감을 다시 알린다).</summary>
        public bool UnlimitedTurnTime { get; private set; }

        private double TurnLength => UnlimitedTurnTime ? DevUnlimitedTurnSeconds : TurnTimeLimit;

        public IReadOnlyList<GameEvent> SetUnlimitedTurnTime(bool on)
        {
            if (UnlimitedTurnTime == on)
                return TakeEvents();

            UnlimitedTurnTime = on;
            if (!_gameOver && CurrentTurnActor != -1 && !_mainActionUsed)
            {
                _turnDeadline = _clock() + TurnLength;
                _events.Add(new TurnResumed(CurrentTurnActor, _turnDeadline));
            }
            return TakeEvents();
        }

        /// <summary>
        /// 카드 써 보기 — 그 종류의 카드 한 장을 덱(없으면 버림 더미)에서 찾아 actor 손패로 뽑아 준다. 판에 그 카드가 남아 있지 않으면 아무 일 없다.
        /// 뽑기와 같은 길(DrawOne)이라 손패 · 찹츄 판정 · 알림이 보통 뽑기와 같다. 메인 행동이 아니다.
        /// </summary>
        public IReadOnlyList<GameEvent> DevGiveCard(int actor, int cardId)
        {
            if (_gameOver || !IsInGame(actor) || _players[actor].Life != LifeState.Alive)
                return TakeEvents();

            int index = _deck.FindIndex(card => card.CardId == cardId);
            if (index < 0)
            {
                int inPile = _discardPile.FindIndex(card => card.CardId == cardId);
                if (inPile < 0)
                    return TakeEvents();

                _deck.Insert(0, _discardPile[inPile]);
                _discardPile.RemoveAt(inPile);
                index = 0;
            }

            Card picked = _deck[index];
            _deck.RemoveAt(index);
            _deck.Insert(0, picked);
            MarkHandsBefore();
            DrawOne(actor);
            SettleAfter(_clock());
            return TakeEvents();
        }
    }
}
