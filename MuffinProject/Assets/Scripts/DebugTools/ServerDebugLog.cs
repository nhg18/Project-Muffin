using System.Collections.Generic;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 멀티 테스트용 콘솔 드라이버. 화면(Presenter) 없이 GameEvents 를 전부 로그로 찍고, 키로 요청을 보낸다.
    /// TempGameScene 의 PunGameServer 오브젝트에 같이 붙인다. 규칙 판정은 하지 않는다 — 로그와 요청뿐.
    /// 키는 지금까지 만든 게임 흐름의 행동만 둔다 — D = 뽑기, E = 턴 종료, P = 손패 첫 카드를 다른 사람 1명에게, Esc = 나가기(들어온 로비로). 기능이 생기면 그 행동만 추가한다.
    /// </summary>
    public class ServerDebugLog : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour server; // IGameRequests 를 구현한 컴포넌트 (PunGameServer)

        private IGameRequests _requests;
        private readonly List<int> _myHand = new List<int>(); // P 키용 — 내 손패 인스턴스 ID (뽑은 순서)

        private int Me => PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

        private void Awake()
        {
            _requests = server as IGameRequests;
            if (_requests == null)
                Debug.LogError($"[{nameof(ServerDebugLog)}] server 에 {nameof(IGameRequests)} 를 구현한 컴포넌트를 연결해야 한다.", this);
        }

        private void OnEnable()
        {
            RoomEvents.OnLeftRoom += HandleLeftRoom;
            GameEvents.OnTurnChanged += OnTurnChanged;
            GameEvents.OnTurnDeadlineChanged += OnTurnDeadlineChanged;
            GameEvents.OnDrawn += OnDrawn;
            GameEvents.OnCardUsed += OnCardUsed;
            GameEvents.OnHpChanged += OnHpChanged;
            GameEvents.OnHandCountChanged += OnHandCountChanged;
            GameEvents.OnDeckCountChanged += OnDeckCountChanged;
            GameEvents.OnDiscardCountChanged += OnDiscardCountChanged;
            GameEvents.OnDeckRefilled += OnDeckRefilled;
            GameEvents.OnRequestRejected += OnRequestRejected;
        }

        private void OnDisable()
        {
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
            GameEvents.OnTurnChanged -= OnTurnChanged;
            GameEvents.OnTurnDeadlineChanged -= OnTurnDeadlineChanged;
            GameEvents.OnDrawn -= OnDrawn;
            GameEvents.OnCardUsed -= OnCardUsed;
            GameEvents.OnHpChanged -= OnHpChanged;
            GameEvents.OnHandCountChanged -= OnHandCountChanged;
            GameEvents.OnDeckCountChanged -= OnDeckCountChanged;
            GameEvents.OnDiscardCountChanged -= OnDiscardCountChanged;
            GameEvents.OnDeckRefilled -= OnDeckRefilled;
            GameEvents.OnRequestRejected -= OnRequestRejected;
        }

        private void Update()
        {
            // Esc = 나가기: 방을 나가고, 들어온 로비(Lobby / DebugLobby)로 돌아간다
            if (Input.GetKeyDown(KeyCode.Escape)) { Log("요청: 나가기"); NetworkManager.Instance.LeaveRoom(); }

            if (_requests == null) return;

            if (Input.GetKeyDown(KeyCode.D)) { Log("요청: 뽑기"); _requests.RequestDraw(); }
            if (Input.GetKeyDown(KeyCode.E)) { Log("요청: 턴 종료"); _requests.RequestEndTurn(); }
            if (Input.GetKeyDown(KeyCode.P)) PlayFirstCard();
        }

        private void PlayFirstCard()
        {
            if (_myHand.Count == 0) { Log("낼 카드가 없다"); return; }

            // 대상이 자동인 카드면 서버가 대상을 무시한다
            int target = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0].ActorNumber : Me;
            Log($"요청: 카드 #{_myHand[0]} → {target}");
            _requests.RequestPlayCard(_myHand[0], new[] { target });
        }

        // RoomPresenter 의 나가기와 같은 흐름 — 들어온 곳(SceneFlow.ReturnSceneAfterRoom)으로 돌아간다
        private void HandleLeftRoom() => SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);

        private void OnTurnChanged(int actor) => Log($"턴 → {actor}{(actor == Me ? " (내 턴)" : "")}");
        private void OnTurnDeadlineChanged(double deadline) => Log($"턴 마감 {deadline - PhotonNetwork.Time:F1}초 뒤");
        private void OnHpChanged(int actor, int hp) => Log($"체력 {actor}: {hp}");
        private void OnHandCountChanged(int actor, int count) => Log($"손패 장수 {actor}: {count}");
        private void OnDeckCountChanged(int count) => Log($"덱 잔여: {count}");
        private void OnDiscardCountChanged(int count) => Log($"버림 더미: {count}");
        private void OnDeckRefilled(int count) => Log($"덱 재생성 — 버림 더미를 섞어 {count}장");
        private void OnRequestRejected(int actor, string reason) => Log($"거절 {actor}: {reason}");

        private void OnDrawn(int actor, int cardInstanceId, int cardId)
        {
            // 비공개 — 주인에게만 와야 한다. 남의 것이 찍히면 규칙 위반.
            Log($"뽑은 카드 {actor}: #{cardInstanceId} card={cardId}{(actor == Me ? "" : "  ⚠ 남의 손패 내용이 왔다")}");
            if (actor == Me) _myHand.Add(cardInstanceId);
        }

        private void OnCardUsed(int actor, int cardInstanceId, int cardId, int[] targets)
        {
            Log($"카드 사용 {actor}: #{cardInstanceId} card={cardId} → [{string.Join(", ", targets)}]");
            if (actor == Me) _myHand.Remove(cardInstanceId);
        }

        private void Log(string msg) => Debug.Log($"[Server:{Me}] {msg}");
    }
}
