using System.Collections.Generic;
using Chapchu.Core;

namespace Chapchu.Game
{
    /// <summary>
    /// 게임 규칙의 원본. 방장 기기에서만 돌아가는 순수 C# 이다 (Photon · UnityEngine · GameEvents 를 참조하지 않는다).
    /// PunGameServer 가 요청을 받아 여기 메서드를 부르고, 결과는 _outbox 로만 내보낸다.
    /// 기능별 규칙은 GameServer.기능.cs (partial) 에 있다. 예: GameServer.Turn.cs
    /// </summary>
    public partial class GameServer
    {
        // ── 기능 추가하는 법 (예: 카드 뽑기) ──────────────────────────────────────────
        //
        // 1. 파일을 하나 만든다: GameServer.Deck.cs
        //        namespace Chapchu.Game { public partial class GameServer { ... } }
        //    한 클래스라 다른 파일의 필드 · 메서드(CurrentTurnActor, SetTurn, GetNextActor …)를 그대로 쓴다.
        //    규칙을 별도 클래스로 떼지 않는다. 게임 상태를 두루 봐야 해서 서로 참조가 꼬인다.
        //
        // 2. 원본 상태는 이 파일 아래 "원본 상태" 에 모은다. 예: private readonly Stack<CardInstance> _deck
        //    플레이어별 값(HP · 손패 · 함정)이 생기면 필드만 있는 PlayerState 클래스로 묶어
        //    Dictionary<int, PlayerState> 로 둔다. PlayerState 에는 로직 · 이벤트를 넣지 않는다.
        //
        // 3. 요청 메서드는 requester(요청한 사람의 actorNumber)를 첫 인자로 받고, 항상 검증 → 적용 → 내보내기 순서다.
        //        public void Draw(int requester)
        //        {
        //            // 검증: 실패하면 상태를 하나도 바꾸지 않고 거절만 보낸다
        //            if (requester != CurrentTurnActor) { _outbox.Reject(requester, "내 턴이 아닙니다."); return; }
        //
        //            // 적용: 원본은 여기(방장 메모리)에만 있다
        //            CardInstance card = _deck.Pop();
        //
        //            // 내보내기: 모두 봐도 되는 값은 상태로, 한 사람만 볼 값은 그 사람에게만
        //            _outbox.SetRoomState(RoomProps.DeckCount, _deck.Count);
        //            _outbox.SendDrawn(requester, card.InstanceId, card.CardId); // 새 종류의 결과면 IServerOutbox 에 메서드 추가
        //        }
        //
        // 4. PunGameServer 에 요청 RPC 와 결과 받기를 잇는다 (PunGameServer 맨 위 주석).
        //
        // 5. 멀티 테스트: DebugLobbyScene 을 메인 에디터 · ParrelSync 클론에서 Play → 입장 → 방장이 시작
        //    → TmpGameScene 으로 넘어간다 (DebugScript 의 Start In Tmp Game 이 켜져 있어야 한다).
        //    공개 값은 양쪽 모두, 비공개 값 · 거절은 요청한 쪽에만 와야 한다.
        //
        // 하지 말 것: 여기서 Debug.Log · PhotonNetwork · GameEvents 호출 / 클라가 보낸 actorNumber 를 요청자로 믿기.
        // ─────────────────────────────────────────────────────────────────────────────

        // ── 원본 상태 ──
        private readonly IServerOutbox _outbox;
        private readonly List<int> _turnOrder = new List<int>();

        public int CurrentTurnActor { get; private set; } = -1;

        public GameServer(IServerOutbox outbox)
        {
            _outbox = outbox;
        }

        // TODO: 순서를 무작위로 섞고 덱 · 손패 · HP 를 나눠 준다 (기능 1). 지금은 받은 순서 그대로 첫 사람부터.
        public void StartGame(IReadOnlyList<int> turnOrder)
        {
            if (turnOrder.Count == 0) return;

            _turnOrder.Clear();
            _turnOrder.AddRange(turnOrder);
            SetTurn(_turnOrder[0]);
        }
    }
}
