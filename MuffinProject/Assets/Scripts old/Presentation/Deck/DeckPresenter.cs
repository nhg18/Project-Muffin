using Chapchu.Game;
using UnityEngine;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 덱 뷰와 서버를 잇는다. 서버는 <see cref="IGameRequests"/> 로만 알고,
    /// 결과(덱 잔여 장수)는 <see cref="GameEvents"/> 로만 받는다. 서버가 가짜인지 진짜인지 모른다.
    /// 뽑은 카드 내용은 여기서 다루지 않는다 — 손패 쪽(PlayerHandPresenter)이 GameEvents.OnMyDrawn 을 직접 구독한다.
    /// </summary>
    public class DeckPresenter : MonoBehaviour
    {
        [SerializeField] private DeckView deckView; // 인스펙터에서 할당
        // 인터페이스는 인스펙터에 직렬화되지 않아 컴포넌트로 받고 Awake 에서 꺼낸다.
        // IGameRequests 를 구현한 컴포넌트를 연결한다.
        [SerializeField] private MonoBehaviour server;

        private IGameRequests _requests;

        private void Awake()
        {
            _requests = server as IGameRequests;

            if (_requests == null)
                Debug.LogError($"[{nameof(DeckPresenter)}] server 에 {nameof(IGameRequests)} 를 구현한 컴포넌트를 연결해야 한다.", this);
        }

        private void OnEnable()
        {
            deckView.DrawRequested += HandleDrawRequested;
            // 수정 필요(UI): OnRequestRejected → OnMyRequestRejected(거절 코드 int) 로 바뀌었다. 내 거절만 오므로
            //   핸들러를 (int code) 로 맞추고 문구는 RejectText.Get(code) 로 → 다시 구독.
            // GameEvents.OnMyRequestRejected += HandleRequestRejected;
        }

        private void OnDisable()
        {
            deckView.DrawRequested -= HandleDrawRequested;
            // GameEvents.OnMyRequestRejected -= HandleRequestRejected;
        }

        private void HandleDrawRequested() => _requests?.RequestDraw();

        private void HandleRequestRejected(int actorNumber, string reason)
        {
            Debug.LogWarning($"[GameEvent] RequestRejected {actorNumber} {reason}");
        }
    }
}
