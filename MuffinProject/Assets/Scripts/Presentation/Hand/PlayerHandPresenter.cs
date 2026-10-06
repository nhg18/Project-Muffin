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
    /// 카드 사용은 서버에 <see cref="IGameRequests.RequestPlayCard"/> 로 요청하고, 승인 결과(GameEvents.OnCardUsed)를 받아 손패에서 뺀다.
    /// 카드 효과(대상 · 체인 · 카운터) 판정은 서버(GameServer)가 한다 — 범위 밖.
    /// </summary>
    public class PlayerHandPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerHandView handView;

        [SerializeField] private CardDatabase cardDatabase;

        // 인터페이스는 인스펙터에 직렬화되지 않아 컴포넌트로 받고 Awake 에서 꺼낸다.
        [SerializeField] private MonoBehaviour server; // IGameRequests 를 구현한 컴포넌트를 연결한다.

        public PlayerHand playerHand = new PlayerHand();

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
        //  · OnDrawn 이 (actor, cardInstanceId, cardId) 로 바뀌었다. StartDrawEvent 시그니처를 맞추고 인스턴스 ID 를 카드에 보관한 뒤 다시 구독한다.
        //  · 카드 사용 결과는 서버 GameEvents.OnCardUsed 로 온다. 옛 OnCardPlayed → DiscardCard → RequestDiscard 경로 대신 그걸 받아 손패에서 뺀다.
        private void OnEnable()
        {
            // GameEvents.OnDrawn += StartDrawEvent;
            // GameEvents.OnCardPlayed += DiscardCard;
        }
        private void OnDisable()
        {
            // GameEvents.OnDrawn -= StartDrawEvent;
            // GameEvents.OnCardPlayed -= DiscardCard;
        }

        private void StartDrawEvent(int actorNumber, int cardid)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber) return;

            CardData data = cardDatabase.GetCard(cardid);

            CardPresenter cp = handView.DrawCard(data);
            cp.Setup(data, playerHand.GetHandCount(), this);
            playerHand.Add(new Card(data.id));
        }

        private void DiscardCard(int cardID, int index)
        {
            playerHand.DiscardCard(index);
            handView.DiscardCard(index);

            // 수정 필요(UI): 임시 버림 요청(RequestDiscard)과 OnCardPlayed 를 지웠다 (기능 5 전 정리 PR).
            //  드롭 → RequestPlayCard(cardInstanceId, 대상), 승인(OnCardUsed)을 받아 이 메서드로 손패에서 뺀다.
            // _requests?.RequestDiscard(cardID);
        }

        public bool IsHandMode()
        {
            return playerHand.isHandMode;
        }
        public void SetHandMode(bool setter)
        {
            playerHand.isHandMode = setter;
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
