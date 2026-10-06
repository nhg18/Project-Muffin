# GameServer 확장 가이드

**작성일**: 2026-09-26
**대상**: 로직 담당 (A) — 요청 · 규칙을 추가할 때
**상위 규약**: [`systems/09-network.md`](systems/09-network.md) (권한 · RPC 명명) · [`CLAUDE.md`](../CLAUDE.md) 11절

---

## 1. 구조

```
[UI]     IGameRequests.RequestX()
           │  photonView.RPC(RPC_RequestX, MasterClient)
[방장]   PunGameServer.RPC_RequestX(info) → GameServer.X(info.Sender.ActorNumber)   ← 규칙은 여기만
           │  IServerOutbox
           ├─ SetRoomState / SetPlayerState → CustomProperties   (공개 상태)
           └─ Reject(actor) · 비공개 결과    → 대상 지정 RPC       (한 사람에게만)
[각 클라] PunGameServer.OnRoomPropertiesUpdate · RPC_On*/RPC_Reject* → GameEvents.Raise*
```

| 파일 | 역할 |
| --- | --- |
| `Game/Server/GameServer.cs` · `GameServer.기능.cs` | 규칙 원본. 순수 C# — `UnityEngine` · `Photon` · `GameEvents` 참조 금지. `partial` 로 기능마다 파일을 나눈다 (예: `GameServer.Turn.cs`). 규칙을 별도 클래스로 떼지 않는다 |
| `Game/Server/IServerOutbox.cs` | GameServer 의 출구. GameServer 가 Photon 을 모르게 한다 |
| `Network/PunGameServer.cs` | Photon 전송. 요청 RPC · 결과 전파 · GameEvents 발생. **규칙 금지** |

---

## 2. 기능 추가 — 예: 드로우

고치는 곳: **`PunGameServer` + `GameServer.Deck.cs`(새 partial 파일)** (새 종류의 결과면 `IServerOutbox` 한 줄 추가). 플레이어별 값이 생기면 필드만 있는 `PlayerState` 로 묶는다.

> 덱 · 손패 코드는 모양만 보여주는 예시다. 규칙은 `docs/systems/` 의 `확정` 값을 따른다.

```csharp
// PunGameServer — 요청 보내기 · 받기 (09-network 명명: RPC_Request*)
public void RequestDraw() => photonView.RPC(nameof(RPC_RequestDraw), RpcTarget.MasterClient);

[PunRPC]
private void RPC_RequestDraw(PhotonMessageInfo info)
{
    if (!PhotonNetwork.IsMasterClient) return;
    _server.Draw(info.Sender.ActorNumber);
}
```

```csharp
// GameServer.Deck.cs — 검증 → 적용 → 내보내기
public void Draw(int requester)
{
    if (requester != CurrentTurnActor) { _outbox.Reject(requester, "내 턴이 아닙니다."); return; }
    if (_deck.Count == 0) { _outbox.Reject(requester, "덱이 비었습니다."); return; }

    CardInstance card = _deck.Pop();
    _hands[requester].Add(card);

    _outbox.SetRoomState(RoomProps.DeckCount, _deck.Count);                            // 공개
    _outbox.SetPlayerState(requester, PlayerProps.HandCount, _hands[requester].Count); // 공개
    _outbox.SendDrawnCard(requester, card.InstanceId, card.CardId);                    // 비공개 — 새 출구
}
```

```csharp
// IServerOutbox — 새 결과 종류
void SendDrawnCard(int actorNumber, int cardInstanceId, int cardId);

// PunGameServer — 출구 구현 + 받기
void IServerOutbox.SendDrawnCard(int actorNumber, int cardInstanceId, int cardId)
{
    var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
    if (player != null) photonView.RPC(nameof(RPC_OnDrawn), player, cardInstanceId, cardId);
}

[PunRPC]
private void RPC_OnDrawn(int cardInstanceId, int cardId)
    => GameEvents.RaiseDrawn(PhotonNetwork.LocalPlayer.ActorNumber, cardId);

// 공개 상태는 콜백에 한 줄씩
public override void OnRoomPropertiesUpdate(Hashtable changedProps)
{
    if (changedProps.TryGetValue(RoomProps.TurnActor, out object actor)) GameEvents.RaiseTurnChanged((int)actor);
    if (changedProps.TryGetValue(RoomProps.DeckCount, out object deck))  GameEvents.RaiseDeckCountChanged((int)deck);
}
```

`HandCount` 같은 플레이어 상태를 처음 받을 때 `OnPlayerPropertiesUpdate` 를 같은 모양으로 추가한다.

---

## 3. 규칙

| 하지 말 것 | 대신 |
| --- | --- |
| `PunGameServer` 에 검증 · 계산 쓰기 | `GameServer` 에만 |
| 클라가 보낸 actorNumber 를 요청자로 믿기 | `info.Sender.ActorNumber` |
| 비공개 정보를 props · `RpcTarget.All`/`Others` 로 보내기 | 대상 지정 RPC (`CLAUDE.md` 11-6) |
| 카드 ID 배열처럼 큰 값을 props 에 넣기 | RPC (`09-network` 6.2) |
| 검증 실패 후 상태를 일부 바꾸기 | 검증을 전부 끝낸 뒤 적용 |
| 씬에 `PunGameServer` 와 옛 `TurnManager` 를 같이 두기 | 둘 다 `turnActor` 를 써서 `TurnChanged` 가 두 번 뜬다. 교체 시 `TurnManager` 를 뺀다 |

---

## 4. 현재 상태 (2026-10-06)

| 항목 | 상태 |
| --- | --- |
| 턴 넘김 | 자동만 — 20초 마감 · 뽑기 · 체인 처리 끝 · 나감 (`AdvanceTurn`). 체인 중에는 20초 마감이 멈춘다. 턴 종료 요청은 없음 (2026-10-06 삭제) |
| 게임 시작 | 턴 순서 무작위 · 덱 섞기 · 카드 번호 · 5장씩 배분 · 체력 100 (방장 기록). 손패 · 체력 원본은 `PlayerState` |
| 드로우 | 구현 — 내 턴 · 덱 재생성 · 뽑으면 턴 종료. 뽑은 카드는 주인에게만 |
| 버림 | 클라 요청 없음 — 승인된 카드 사용만 버림 더미로 간다. 임시 버림 요청(`RequestDiscard`)은 2026-10-06 삭제 |
| 카드 내기 | 구현 — 행동 카드 (`GameServer.PlayCard`) → 체인 · 반응 5초 → 맨 위부터 처리 (`GameServer.Chain.cs`). 카운터 (`GameServer.Counter`, 본인만 무효 C05) |
| 함정 · 찹츄 선언 | 빈 메서드 |
| 다음 턴 | 시작 때 섞은 순서 기준. 나간 사람은 순서에서 빠지고, 턴 주인이었으면 다음 사람에게 넘긴다 (`RemoveFromTurnOrder`). 생존자만 고르기는 기능 7 |
| 방장 교체 | 새 방장의 `GameServer` 는 비어 있다 (`09-network` 9절 **미정**) |
| 씬 배치 | `Scenes/TempGameScene` 의 `GameServer` 오브젝트 (`PhotonView` 씬 ID 1 + `PunGameServer` + `ServerDebugLog`). `DeckPresenter` · `PlayerHandPresenter` · `TurnPresenter` 의 `server` 가 여기 연결. `GameScene` 은 아직 옛 배치 |

---

## 5. 멀티 테스트 — TempGameScene

기존 디버그 입장 흐름(`DebugLobbyScene` → `RoomScene` → 시작)을 그대로 쓰고, 넘어가는 씬만 `TempGameScene` 으로 바꾼다.

1. `DebugLobbyScene` 의 `DebugScript` 에서 **Start In Tmp Game** 이 켜져 있는지 본다 (기본 켜짐. 끄면 예전처럼 `GameScene`).
2. 메인 에디터와 ParrelSync 클론 양쪽에서 `DebugLobbyScene` 을 열고 Play → 입장 버튼.
3. 대기실에서 방장이 **시작** → 전원 `TempGameScene` 으로 넘어가고 방장이 바로 게임을 시작한다.
4. 진짜 UI(턴 표시) · 디버그 키(`ServerDebugLog`)로 요청하고, 결과는 에디터마다 화면과 Console 로 확인한다.
   * 공개 값(턴 · HP · 장수)은 **양쪽 모두** 바뀌어야 한다.
   * 비공개 값(뽑은 카드 · 거절)은 **요청한 쪽에만** 와야 한다.

UI 가 아직 없는 요청은 UI 담당이 뷰를 붙일 때 같이 확인한다. 그 전에 UI 는 `FakeGameServer` 로 혼자 작업한다.

**테스트 방식** (2026-10-06): Test Runner(EditMode) 는 쓰지 않는다. 로직은 이 멀티 실행으로 함께 확인한다.
* **방장 콘솔만 보면 된다.** `ServerConsoleLog` 가 서버의 출구(`IServerOutbox`)를 감싸 판 전체를 찍는다 — 턴마다 `── 턴 n · P 차례 ──` + 덱 · 버림 · 사람마다 체력 · 손패 장수[카드 ID] **한 줄** 요약, 체인에 올라감 `⛓ 체인: 카드 1(P1→P2) ← 카드 2(P2) · 반응 5.0초`, 처리 `✔`(효과) · `⊘`(무효), 뽑기 `+`, 거절 `✖`(노란색), 덱 재생성 `↻`.
* `ServerDebugLog` 는 키 입력만 — D = 뽑기, P = 손패의 첫 행동 카드를 다른 사람 1명에게, C = 손패의 첫 카운터 카드로 체인 맨 위에 반응, Esc = 나가기(돌아갈 씬을 로그에 찍음).
* 그 밖의 정상 흐름 로그(접속 · 입장 · 클릭 · 호버 등)는 지웠다. 실패 · 끊김 · 방장 교체만 경고(노란색)로 남는다. 키는 만든 기능의 행동만 추가한다.
* 사용자 입력(컨트롤러) 영역이 아직 없으니 확인용 로그는 `Start` 에서 찍는다.
* 알려진 문제: 카드를 드롭 영역에 놓으면 `MyTurnCondition` 이 옛 `TurnManager.Instance`(이 씬에 없음)를 읽어 NRE — `plan-a-logic.md` 수정 10.
