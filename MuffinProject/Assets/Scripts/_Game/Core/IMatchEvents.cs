using System.Collections.Generic;

namespace Chapchu.Game.Core
{
    /// <summary>
    /// Match 가 판정 결과를 내보내는 창구. 화면 · 네트워크 쪽이 구현한다.
    /// 공개 정보(장수 · HP · 턴)와 비공개 정보(뽑은 카드 내용)를 메서드로 나눠, 받는 쪽이 누구에게 보낼지 헷갈리지 않게 한다.
    /// </summary>
    public interface IMatchEvents
    {
        /// <summary>게임 시작. 턴 순서대로 나열한 플레이어 ID.</summary>
        void OnGameStarted(IReadOnlyList<int> turnOrder);

        /// <summary>턴이 바뀌었다. deadline 은 Match 에 넣어 준 now 와 같은 단위의 마감 시각.</summary>
        void OnTurnChanged(int playerId, double deadline);

        void OnHpChanged(int playerId, int hp);

        void OnHandCountChanged(int playerId, int handCount);

        void OnPileCountChanged(int drawCount, int discardCount);

        /// <summary>
        /// 뽑은 카드. 비공개 — 뽑은 본인에게만 전달한다 (09-network.md 3절).
        /// 규칙값이 든 Card 대신 번호만 넘긴다. 화면은 cardId 로 그림을 찾고, 규칙값은 판정하는 쪽만 가진다.
        /// </summary>
        void OnCardDrawn(int playerId, int instanceId, int cardId);

        /// <summary>카드가 체인에 올라갔다. 공개 — 사용한 카드는 모두에게 보인다.</summary>
        void OnCardPlayed(int playerId, int instanceId, int cardId, IReadOnlyList<int> targets);

        /// <summary>반응 시간이 (다시) 시작됐다. deadline 은 now 와 같은 단위.</summary>
        void OnReactionStarted(double deadline);

        /// <summary>체인의 카드 한 장이 처리됐다. 무효화됐으면 효과 없이 버려진다.</summary>
        void OnCardResolved(int instanceId, bool isNegated);

        /// <summary>요청 거절. 요청한 본인에게만 전달한다. code 는 RejectCode.</summary>
        void OnRejected(int playerId, int code);
    }
}
