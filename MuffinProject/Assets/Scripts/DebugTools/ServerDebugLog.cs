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
    /// 멀티 테스트용 키 입력. UI 없이 키로 요청을 보낸다 — 결과 로그는 방장 콘솔(ServerConsoleLog)이 찍는다.
    /// TempGameScene 의 PunGameServer 오브젝트에 같이 붙인다. 규칙 판정은 하지 않는다.
    /// 키는 지금까지 만든 게임 흐름의 행동만 둔다 — D = 뽑기, P = 손패 첫 카드를 다른 사람 1명에게, Esc = 나가기(들어온 로비로).
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
            GameEvents.OnDrawn += HandleDrawn;
            GameEvents.OnCardUsed += HandleCardUsed;
        }

        private void OnDisable()
        {
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
            GameEvents.OnDrawn -= HandleDrawn;
            GameEvents.OnCardUsed -= HandleCardUsed;
        }

        private void Update()
        {
            // Esc = 나가기: 방을 나가고, 들어온 로비(Lobby / DebugLobby)로 돌아간다
            if (Input.GetKeyDown(KeyCode.Escape)) { Log("나가기"); NetworkManager.Instance.LeaveRoom(); }

            if (_requests == null) return;

            if (Input.GetKeyDown(KeyCode.D)) { Log("뽑기"); _requests.RequestDraw(); }
            if (Input.GetKeyDown(KeyCode.P)) PlayFirstCard();
        }

        private void PlayFirstCard()
        {
            if (_myHand.Count == 0) { Log("낼 카드가 없다"); return; }

            // 대상이 자동인 카드면 서버가 대상을 무시한다
            int target = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0].ActorNumber : Me;
            Log($"카드 #{_myHand[0]} → P{target}");
            _requests.RequestPlayCard(_myHand[0], new[] { target });
        }

        // RoomPresenter 의 나가기와 같은 흐름 — 들어온 곳(SceneFlow.ReturnSceneAfterRoom)으로 돌아간다
        private void HandleLeftRoom() => SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);

        // P 키가 낼 카드를 알도록 내 손패 인스턴스 ID 만 따라간다
        private void HandleDrawn(int actor, int cardInstanceId, int cardId)
        {
            if (actor == Me) _myHand.Add(cardInstanceId);
        }

        private void HandleCardUsed(int actor, int cardInstanceId, int cardId, int[] targets)
        {
            if (actor == Me) _myHand.Remove(cardInstanceId);
        }

        private void Log(string msg) => Debug.Log($"[키 P{Me}] {msg}");
    }
}
