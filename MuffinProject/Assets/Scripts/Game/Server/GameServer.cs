using System;
using System.Collections.Generic;
using System.Linq;
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
        // 2. 원본 상태는 이 파일 아래 "원본 상태" 에 모은다. 예: private readonly Stack<Card> _deck
        //    플레이어별 값(HP · 손패 · 함정)이 생기면 필드만 있는 PlayerState 클래스로 묶어
        //    Dictionary<int, PlayerState> 로 둔다. PlayerState 에는 로직 · 이벤트를 넣지 않는다.
        //
        // 3. 요청 메서드는 requester(요청한 사람의 actorNumber)를 첫 인자로 받고, 항상 검증 → 적용 → 내보내기 순서다.
        //        public void Draw(int requester)
        //        {
        //            // 검증: 실패하면 상태를 하나도 바꾸지 않고 거절만 보낸다
        //            if (requester != CurrentTurnActor) { _outbox.Reject(requester, RejectCode.NotYourTurn); return; }
        //
        //            // 적용: 원본은 여기(방장 메모리)에만 있다
        //            Card card = _deck.Pop();
        //
        //            // 내보내기: 모두 봐도 되는 값은 상태로, 한 사람만 볼 값은 그 사람에게만
        //            _outbox.SetRoomState(RoomProps.DeckCount, _deck.Count);
        //            _outbox.SendDrawnCard(requester, card.InstanceId, card.CardId); // 새 종류의 결과면 IServerOutbox 에 메서드 추가
        //        }
        //
        // 4. PunGameServer 에 요청 RPC 와 결과 받기를 잇는다 (PunGameServer 맨 위 주석).
        //
        // 5. 멀티 테스트: DebugLobbyScene 을 메인 에디터 · ParrelSync 클론에서 Play → 입장 → 방장이 시작
        //    → TempGameScene 으로 넘어간다 (DebugScript 의 Start In Tmp Game 이 켜져 있어야 한다).
        //    공개 값은 양쪽 모두, 비공개 값 · 거절은 요청한 쪽에만 와야 한다.
        //
        // 하지 말 것: 여기서 Debug.Log · PhotonNetwork · GameEvents 호출 / 클라가 보낸 actorNumber 를 요청자로 믿기.
        // ─────────────────────────────────────────────────────────────────────────────

        // 확정(06-health.md 2절). 규칙 수치의 원본은 GameServer — 서버 밖(옛 GameStatus)은 이 값을 참조한다.
        public const int MaxHp = 100;

        // ── 원본 상태 ──
        private readonly IServerOutbox _outbox;
        private readonly Func<double> _clock; // 서버 시각(초). 턴 마감 계산에 쓴다 (09-network.md 8절)
        private readonly List<int> _turnOrder = new List<int>();
        private readonly Dictionary<int, PlayerState> _players = new Dictionary<int, PlayerState>(); // actorNumber → 원본

        public int CurrentTurnActor { get; private set; } = -1;

        public GameServer(IServerOutbox outbox, Func<double> clock)
        {
            _outbox = outbox;
            _clock = clock;
        }

        public void StartGame(IReadOnlyList<int> actors)
        {
            if (actors.Count == 0) return;

            _players.Clear();
            foreach (int actor in actors)
            {
                _players[actor] = new PlayerState();
                SetHp(actor, MaxHp);
            }
        }

        /// <summary>디버그 로그용 한 줄 요약 — 덱 · 버림, 사람마다 체력 · 손패 장수[카드 ID] (방장 콘솔). 판정에 쓰지 않는다.</summary>
        public string DebugState()
        {
            string players = string.Join(" │ ", _players.Select(p =>
                $"P{p.Key} ♥{p.Value.Hp} 손{p.Value.Hand.Count}[{string.Join(" ", p.Value.Hand.Select(c => c.CardId))}]{(p.Value.IsChapChu ? " 찹츄!" : "")}{(_turnOrder.Contains(p.Key) ? "" : " 나감")}"));

            return $"덱 {_deck.Count} · 버림 {_discardPile.Count} ║ {players}";
        }

        // 덱(GameServer.Deck.cs) · 턴 순서(GameServer.Turn.cs) 공용
        private static readonly Random _rng = new Random();

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
