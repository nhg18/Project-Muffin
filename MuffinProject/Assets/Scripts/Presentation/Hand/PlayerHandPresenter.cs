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

        // 수정 필요(UI) — Card MVP(CardModel · CardPresenter · CardView)는 고치지 않고 이 파일 · PlayerHandView 만으로 할 수 있다.
        //  1. 뽑기: OnMyDrawn 을 다시 구독한다. 인스턴스 ID 는 여기 Dictionary<int, CardPresenter>(instanceId → 화면 카드)로 든다.
        //  2. 내기: PlayCardAsync 에서 card.PlayAsync() 대신 card.SelectPlayer() 로 대상을 받고
        //     _requests.RequestPlayCard(instanceId, targets) 를 보낸다. 보낸 카드는 _pending 으로 기억 — 승인 전에 지우지 않는다.
        //     TargetType.None 카드는 SelectPlayer 가 빈 목록을 주므로, 뽑을 때 받은 CardData.targetType 을 보고 빈 배열로 바로 요청한다.
        //  3. 승인: OnCardUsed(전원에게 옴)를 구독해 사전에 있는 instanceId 일 때만 그 장을 지운다
        //     (PlayerHandView.DiscardCard(index) → RemoveCard(CardPresenter) 처럼 카드로 지우게 바꾼다).
        //  4. 거절: OnMyRequestRejected 를 구독해 _pending 카드를 cardView.ReturnToOrigin() 으로 제자리에 둔다.
        //  5. 정리: 옛 OnCardPlayed · DiscardCard · RequestDiscard 경로를 지운다 (GameEvents.OnCardPlayed 도 함께).
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
        // 수정 필요(UI): CardDatabase 삭제로 본문 주석 처리. DeckData 를 참조해 GetCard(cardId) 로 바꾸고 cardInstanceId 는 위 사전에 보관한다.
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
