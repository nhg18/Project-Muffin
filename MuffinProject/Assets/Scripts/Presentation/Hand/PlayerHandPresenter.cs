using Photon.Pun;
using System.Threading.Tasks;
using UnityEngine;
using Chapchu.Game;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 내 손패 모델 · 뷰를 잇는다. 카드는 드로우 순서대로 쌓인다(정렬 없음, 05-deck.md 6절).
    /// 손패 장수(PlayerProps.HandCount)는 마스터(GameServer)만 기록한다 — 여기서는 더 이상 직접 쓰지 않는다.
    /// 카드 사용 · 버림도 서버에 <see cref="IGameRequests.RequestDiscard"/> 로 알려 장수만 마스터 권위로 갱신시킨다.
    /// 카드 효과(대상 · 체인 · 카운터) 판정은 서버(GameServer)가 한다 — 범위 밖.
    /// </summary>
    public class PlayerHandPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerHandView handView;

        // [SerializeField] private CardDatabase cardDatabase; — 수정 필요(UI): CardDatabase 삭제. DeckData 를 참조해 GetCard(cardId) 로 바꾼다 (씬 연결 포함)

        // 인터페이스는 인스펙터에 직렬화되지 않아 컴포넌트로 받고 Awake 에서 꺼낸다.
        [SerializeField] private MonoBehaviour server; // IGameRequests 를 구현한 컴포넌트를 연결한다.

        // 손패 모드(카드를 만질 수 있는 상태). 옛 PlayerHand · CardCollection(서버 Card 를 들던 클라 모델)은 삭제 — 손패 내용은 서버가 들고, 화면은 CardPresenter 들로 표시만 한다.
        private bool _isHandMode;

        private IGameRequests _requests;

        /// <summary>
        /// 드롭한 카드가 대상 선택 → 사용 요청까지 처리 중인가.
        /// 한 손패에서 한 번에 한 장만 처리한다 (10-ui.md §6). 처리 중에는 CardView · ClickManager 가 입력을 무시한다.
        /// 마스터의 반응 시간(5초)은 포함하지 않는다. 그 동안 카운터 카드를 내야 하기 때문.
        /// </summary>
        public bool IsCardPlayInProgress { get; private set; }

        private void Awake()
        {
            _requests = server as IGameRequests;

            if (_requests == null)
                Debug.LogError($"[{nameof(PlayerHandPresenter)}] server 에 {nameof(IGameRequests)} 를 구현한 컴포넌트를 연결해야 한다.", this);
        }

        // 수정 필요(UI) — develop → HeeGeon PR 에서 맞춘다 (PR #53 리뷰)
        //  · 화면은 서버의 Card 객체를 쓰지 않는다. 손패 한 장 = (cardInstanceId, CardData). CardPresenter 가 InstanceId 를 들고,
        //    여기는 instanceId → CardPresenter 사전을 둔다 (옛 PlayerHand · CardCollection 은 삭제됨).
        //  · OnDrawn → OnMyDrawn(cardInstanceId, cardId) 로 바뀌었다. 내 카드만 오므로 actor 검사가 없다. StartDrawEvent 는 그 시그니처에 맞춰 두었다 — 다시 구독한다.
        //    드롭 때는 RequestPlayCard(instanceId, targets) 로 그 번호를 보낸다.
        //  · 카드 사용 결과는 서버 GameEvents.OnCardUsed 로 온다. 옛 OnCardPlayed → DiscardCard → RequestDiscard 경로 대신 그걸 받아
        //    그 인스턴스 ID 의 카드를 손패에서 뺀다. 거절(OnMyRequestRejected)이면 잠갔던 카드를 제자리로.
        private void OnEnable()
        {
            // GameEvents.OnMyDrawn += StartDrawEvent;
            // GameEvents.OnCardPlayed += DiscardCard;
        }
        private void OnDisable()
        {
            // GameEvents.OnMyDrawn -= StartDrawEvent;
            // GameEvents.OnCardPlayed -= DiscardCard;
        }

        // 서버가 준 (인스턴스 ID, 종류 ID) 로 손패 한 장을 그린다. 표시 데이터는 종류 ID 로 DeckData.GetCard 에서 읽는다 (OnMyDrawn).
        // 수정 필요(UI): CardDatabase 삭제로 본문 주석 처리. DeckData 를 참조해 GetCard(cardId) 로 바꾸고 cardInstanceId 를 CardPresenter 에 보관한다.
        private void StartDrawEvent(int cardInstanceId, int cardId)
        {
            // CardData data = deck.GetCard(cardId);
            //
            // CardPresenter cp = handView.DrawCard(data);
            // cp.Setup(data, 손패 장수, this);
        }

        private void DiscardCard(int cardID, int index)
        {
            // playerHand.DiscardCard(index); — PlayerHand 삭제. 옛 OnCardPlayed 경로라 OnCardUsed 로 바꿀 때 함께 정리
            handView.DiscardCard(index);

            _requests?.RequestDiscard(cardID);
        }

        public bool IsHandMode()
        {
            return _isHandMode;
        }
        public void SetHandMode(bool setter)
        {
            _isHandMode = setter;
            GameEvents.RaiseHandModeChanged(setter);
        }

        /// <summary>
        /// 드롭된 카드의 처리(대상 선택 → 사용 요청)를 손패 단위로 직렬화한다.
        /// 이미 처리 중이면 두 번째 카드는 손패로 되돌린다.
        /// 카드는 처리 끝에 Destroy 되므로 잠금 해제는 카드가 아니라 손패가 책임진다.
        /// </summary>
        public async Task PlayCardAsync(CardPresenter card)
        {
            if (IsCardPlayInProgress)
            {
                Debug.LogWarning("[PlayerHandPresenter] 이미 카드 처리 중. 두 번째 카드는 손패로 되돌린다.");
                card.cardView.ReturnToOrigin();
                return;
            }

            IsCardPlayInProgress = true;
            handView.CancelAllInteractions(card.cardView);

            try
            {
                await card.PlayAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                IsCardPlayInProgress = false;
            }
        }

    }
}
