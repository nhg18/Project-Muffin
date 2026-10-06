using System.Collections.Generic;
using Chapchu.Core;
using Chapchu.Game;
using Chapchu.Game.Cards;
using Chapchu.Network;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chapchu.DebugTools
{
    /// <summary>
    /// 멀티 테스트용 키 입력. UI 없이 키로 요청을 보낸다 — 결과 로그는 방장 콘솔(ServerConsoleLog)이 찍는다.
    /// TempGameScene 의 PunGameServer 오브젝트에 같이 붙인다. 규칙 판정은 하지 않는다.
    /// 키는 지금까지 만든 게임 흐름의 행동만 둔다 — D = 뽑기, P = 손패 첫 행동 카드를 다른 사람 1명에게,
    /// C = 손패 첫 카운터 카드로 체인 맨 위에 반응, Esc = 나가기(들어온 로비로).
    /// </summary>
    public class ServerDebugLog : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour server; // IGameRequests 를 구현한 컴포넌트 (PunGameServer)

        private IGameRequests _requests;
        private CardDatabase _cards; // 손패 카드가 행동인지 카운터인지 보려고
        private readonly List<CardInstance> _myHand = new List<CardInstance>(); // P · C 키용 — 내 손패 (뽑은 순서)
        private int _chainTop = -1; // C 키용 — 마지막으로 체인에 올라간 카드. 체인이 끝나도 남겨 둔다 (늦은 반응 = 반응 시간 끝 거절 확인용)

        private int Me => PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

        private void Awake()
        {
            _requests = server as IGameRequests;
            _cards = (server as PunGameServer)?.CardDatabase;
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
            if (Input.GetKeyDown(KeyCode.Escape)) { Log($"나가기 → {SceneFlow.ReturnSceneAfterRoom}"); NetworkManager.Instance.LeaveRoom(); }

            if (_requests == null) return;

            if (Input.GetKeyDown(KeyCode.D)) { Log("뽑기"); _requests.RequestDraw(); }
            if (Input.GetKeyDown(KeyCode.P)) PlayFirstCard();
            if (Input.GetKeyDown(KeyCode.C)) CounterWithFirstCard();
        }

        private void PlayFirstCard()
        {
            if (!TryFindCard(CardType.Action, out CardInstance card)) { Log("낼 행동 카드가 없다"); return; }

            // 대상이 자동인 카드면 서버가 대상을 무시한다
            int target = PhotonNetwork.PlayerListOthers.Length > 0 ? PhotonNetwork.PlayerListOthers[0].ActorNumber : Me;
            Log($"카드 {card.CardId} (#{card.InstanceId}) → P{target}");
            _requests.RequestPlayCard(card.InstanceId, new[] { target });
        }

        // 반응할 카드가 없어도 보낸다 — 거절(반응할 카드 없음 · 반응 시간 끝)도 서버가 판정한다
        private void CounterWithFirstCard()
        {
            if (!TryFindCard(CardType.Counter, out CardInstance card)) { Log("낼 카운터 카드가 없다"); return; }

            Log($"카운터 {card.CardId} (#{card.InstanceId}) → 체인 카드 #{_chainTop}");
            _requests.RequestCounter(card.InstanceId, _chainTop);
        }

        private bool TryFindCard(CardType type, out CardInstance card)
        {
            int index = _myHand.FindIndex(c => _cards == null || _cards.GetCard(c.CardId)?.type == type);
            card = index >= 0 ? _myHand[index] : default;
            return index >= 0;
        }

        // RoomPresenter 의 나가기와 같은 흐름 — 들어온 곳(SceneFlow.ReturnSceneAfterRoom)으로 돌아간다
        private void HandleLeftRoom() => SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);

        // P · C 키가 낼 카드를 알도록 내 손패와 체인 맨 위를 따라간다
        private void HandleDrawn(int actor, int cardInstanceId, int cardId)
        {
            if (actor == Me) _myHand.Add(new CardInstance(cardInstanceId, cardId));
        }

        private void HandleCardUsed(int actor, int cardInstanceId, int cardId, int[] targets)
        {
            _chainTop = cardInstanceId;
            if (actor == Me) _myHand.RemoveAll(c => c.InstanceId == cardInstanceId);
        }

        private void Log(string msg) => Debug.Log($"[키 P{Me}] {msg}");
    }
}
