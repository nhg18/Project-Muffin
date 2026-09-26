using Chapchu.Game;
using UnityEngine;

namespace Chapchu.Presentation
{
    /// <summary>
    /// 턴 뷰와 서버를 잇는다. 서버는 <see cref="IGameRequests"/> · <see cref="IGameState"/> 로만 알고,
    /// 결과는 <see cref="GameEvents"/> 로만 받는다. 서버가 가짜(FakeGameServer)인지 진짜인지 모른다.
    /// </summary>
    [RequireComponent(typeof(TurnView))]
    public class TurnPresenter : MonoBehaviour
    {
        // 인터페이스는 인스펙터에 직렬화되지 않아 컴포넌트로 받고 Awake 에서 꺼낸다.
        // IGameRequests (· IGameState) 를 구현한 컴포넌트를 연결한다.
        [SerializeField] private MonoBehaviour server;

        private TurnView _view;
        private IGameRequests _requests;
        private IGameState _state;

        private void Awake()
        {
            _view = GetComponent<TurnView>();
            _requests = server as IGameRequests;
            _state = server as IGameState;

            if (_requests == null)
                Debug.LogError($"[{nameof(TurnPresenter)}] server 에 {nameof(IGameRequests)} 를 구현한 컴포넌트를 연결해야 한다.", this);
        }

        private void OnEnable()
        {
            _view.EndTurnRequested += HandleEndTurnRequested;
            GameEvents.OnTurnChanged += HandleTurnChanged;
            GameEvents.OnRequestRejected += HandleRequestRejected;
        }

        private void OnDisable()
        {
            _view.EndTurnRequested -= HandleEndTurnRequested;
            GameEvents.OnTurnChanged -= HandleTurnChanged;
            GameEvents.OnRequestRejected -= HandleRequestRejected;
        }

        // 켜지기 전에 지나간 이벤트는 못 받으므로 현재 상태를 한 번 읽어 그린다.
        private void Start()
        {
            _view.SetTurnActor(_state != null ? _state.CurrentTurnActor : -1);
        }

        private void HandleEndTurnRequested() => _requests?.RequestEndTurn();

        private void HandleTurnChanged(int actorNumber)
        {
            Debug.Log($"[GameEvent] TurnChanged {actorNumber}");
            _view.SetTurnActor(actorNumber);
        }

        private void HandleRequestRejected(int actorNumber, string reason)
        {
            Debug.LogWarning($"[GameEvent] RequestRejected {actorNumber} {reason}");
        }
    }
}
