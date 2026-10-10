using Photon.Pun;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Chapchu.Game;
using Chapchu.Game.Cards;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 내 손패 모델 · 뷰를 잇는다. 카드는 드로우 순서대로 쌓인다(정렬 없음, 05-deck.md 6절).
    /// 손패 장수(PlayerProps.HandCount)는 마스터(GameServer)만 기록한다 — 여기서는 더 이상 직접 쓰지 않는다.
    /// 카드 사용은 <see cref="IGameRequests.RequestPlayCard"/> 로 요청만 하고, 손패에서 빼는 것은 승인 결과 <see cref="GameEvents.OnCardUsed"/> 를 받은 뒤다 (04-card.md 4절).
    /// 카드 효과(대상 · 체인 · 카운터) 판정은 서버(GameServer)가 한다 — 범위 밖.
    /// </summary>
    public class PlayerHandPresenter : MonoBehaviour
    {
        [SerializeField] private PlayerHandView handView;

        // 종류 ID → CardData 조회. 게임에 나오는 카드는 전부 덱에서 나오므로 덱이 사전 역할을 한다 (DeckData.GetCard).
        [SerializeField] private DeckData deck;

        // 인터페이스는 인스펙터에 직렬화되지 않아 컴포넌트로 받고 Awake 에서 꺼낸다.
        [SerializeField] private MonoBehaviour server; // IGameRequests 를 구현한 컴포넌트를 연결한다.

        // 손패의 카드 Presenter 목록. 드로우 순이며 handView.Hands 와 같은 순서를 유지한다.
        // 손패 내용의 원본은 서버가 들고, 여기는 화면 표시용 사본이다 (옛 PlayerHand · CardCollection 은 삭제됨).
        private readonly List<CardPresenter> _cards = new List<CardPresenter>();

        // 손패 모드(카드를 만질 수 있는 상태). 로컬 UI 상태.
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

        private void OnEnable()
        {
            GameEvents.OnMyDrawn += HandleMyDrawn;
            GameEvents.OnCardUsed += HandleCardUsed;
        }
        private void OnDisable()
        {
            GameEvents.OnMyDrawn -= HandleMyDrawn;
            GameEvents.OnCardUsed -= HandleCardUsed;
        }

        // 내가 뽑은 카드 (OnMyDrawn 은 나에게만 온다). 표시 데이터는 종류 ID 로 덱에서 읽는다.
        private void HandleMyDrawn(int cardInstanceId, int cardId)
        {
            CardData data = deck.GetCard(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[{nameof(PlayerHandPresenter)}] 덱에 없는 카드 종류 cardId={cardId}", this);
                return;
            }

            CardView cardView = handView.DrawCard();
            CardPresenter cp = cardView.GetComponent<CardPresenter>();
            cp.Setup(data, cardInstanceId, this);

            _cards.Add(cp);
        }

        /// <summary>드롭한 카드의 사용을 서버에 요청한다. 판정 · 손패 제거는 서버 결과(OnCardUsed)를 따른다.</summary>
        public void RequestPlayCard(int cardInstanceId, int[] targetActorNumbers)
        {
            _requests?.RequestPlayCard(cardInstanceId, targetActorNumbers);
        }

        // 방장이 승인한 카드 사용 (전원에게 온다). 내 카드면 인스턴스 ID 로 찾아 손패에서 뺀다.
        // 손패 장수는 서버가 PlayerProps.HandCount 로 따로 올리므로 여기서 통보하지 않는다.
        private void HandleCardUsed(int actorNumber, int cardInstanceId, int cardId, int[] targetActorNumbers)
        {
            if (PhotonNetwork.LocalPlayer.ActorNumber != actorNumber) return;

            int index = _cards.FindIndex(c => c.CardInstanceId == cardInstanceId);
            if (index < 0)
            {
                Debug.LogWarning($"[{nameof(PlayerHandPresenter)}] 승인된 카드가 손패에 없다. instanceId={cardInstanceId}", this);
                return;
            }

            _cards.RemoveAt(index);
            handView.DiscardCard(index);
        }

        public bool IsHandMode()
        {
            return _isHandMode;
        }

        /// <summary>HandMode 의 유일한 진입점. 상태 갱신 → 뷰 연출 → 카드들에 알림 (ClickManager 가 입력을 받아 부른다).</summary>
        public void SetHandMode(bool isHandMode)
        {
            _isHandMode = isHandMode;

            if (isHandMode) handView.HandsUp();
            else handView.HandsDown();

            GameEvents.RaiseHandModeChanged(isHandMode);
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
