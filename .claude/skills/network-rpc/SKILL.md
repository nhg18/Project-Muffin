---
name: network-rpc
description: 찹츄 네트워크 권한 규칙과 GameServer 구조. Photon RPC · CustomProperties · GameServer · PunGameServer · IGameRequests · IGameState · GameEvents · IServerOutbox · FakeGameServer 를 만들거나 고칠 때, 또는 "마스터/방장이 판정" 여부를 정할 때 반드시 먼저 읽는다.
---

# 네트워크 권한 규칙 · GameServer 구조

> 상세 규약은 [`docs/systems/09-network.md`](../../../docs/systems/09-network.md), 요청 · 규칙 추가 절차는 [`docs/gameserver-guide.md`](../../../docs/gameserver-guide.md).

## 권한 규칙

1. **게임 규칙 판정은 전부 마스터 클라이언트(방장)가 한다.** 클라이언트는 "요청"만 보낸다.
2. 클라이언트가 보내는 것은 **의도(Intent)** 뿐이다: `이 카드를, 이 대상에게 쓰겠다`.
3. 마스터는 요청을 **검증(소유권 · 턴 · 조건 · 대상 유효성)** 한 뒤에만 결과를 전파한다. 요청자는 `PhotonMessageInfo.Sender` 로 판단하고, 클라가 보낸 actorNumber 를 믿지 않는다.
4. HP, 덱, 손패, 함정 슬롯, 턴, 찹츄 상태의 **원본은 마스터 메모리**에 하나만 존재한다.
5. 클라이언트는 원본을 직접 수정하지 않는다. 마스터가 브로드캐스트한 결과만 반영한다.
6. 비공개 정보(내 손패 내용, 남의 함정 종류)는 **대상 지정 RPC로 해당 플레이어에게만** 보낸다. `RpcTarget.All` / `Others` 로 비공개 정보를 보내지 않는다.
7. `CustomProperties`는 "모두가 항상 봐도 되는 값"에만 사용한다(턴 주인, 손패 **장수**, HP, 생존 상태).
   카드 ID 배열처럼 큰 값이나 비공개 값은 CustomProperties에 넣지 않는다.
8. RPC 이름은 `nameof(RPC_Xxx)` 로 넘긴다 (hook 이 문자열 리터럴을 막는다).

## 코드 구조

```
UI ─ IGameRequests.RequestX() ─▶ PunGameServer ─▶ GameServer (방장, 규칙 원본)
                                                     │ IServerOutbox
UI ◀─ GameEvents 구독 ◀─ PunGameServer ◀─ CustomProperties(공개) · 대상 지정 RPC(비공개)
```

| 층 | 위치 | 규칙 |
| --- | --- | --- |
| 규칙 | `Game/Server/GameServer*.cs` | 규칙은 **여기에만** 둔다. 순수 C# — `UnityEngine` · `Photon` · `GameEvents` 참조 금지 (hook 이 막는다). 기능마다 `partial` 파일로 나눈다. 출력은 `IServerOutbox`로만 |
| 전송 | `Network/PunGameServer.cs` | 요청 RPC 수신 · 결과 전파 · `GameEvents` 발생. **규칙 판정 금지** |
| 표시 | `Presentation/` · `UI/` | `IGameRequests`로 요청하고 `IGameState` · `GameEvents`로 표시만 한다. 규칙 판정 금지 (표시용 조건만 예외) |
| 대역 | `DebugTools/FakeGameServer.cs` | Photon 없이 UI를 돌리는 로컬 서버. 약속 인터페이스가 바뀌면 함께 고친다 |

## 약속 파일

`GameEvents` · `IGameRequests` · `IGameState` · `PlayerProps` / `RoomProps` 는 로직 · UI 양쪽이 쓰는 약속이다. 바꾸는 PR 은 양쪽 리뷰를 받고, 기능 작업보다 먼저 작게 올린다.

`GameEvents` 이름 규칙: 전원에게 가는 이벤트는 `actorNumber` 를 첫 인자로 갖는다. 본인에게만 가는 이벤트(대상 지정 RPC)는 `OnMy*` (예: `OnMyDrawn` · `OnMyRequestRejected`)로 짓고 `actorNumber` 를 넣지 않는다 — 받는 사람이 곧 주인이다.

## 작업 전 확인

1. 이 로직은 방장이 판정해야 하는가, 로컬 연출인가?
2. 보내는 값이 공개인가 비공개인가? → CustomProperties / 대상 지정 RPC 선택
3. 새 결과 종류면 `IServerOutbox` 에 한 줄 추가, `PunGameServer` 에 전송 구현, `FakeGameServer` 도 맞춘다.
