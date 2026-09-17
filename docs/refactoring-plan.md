# 리팩토링 플랜

**작성일**: 2026-09-17
**대상**: `MuffinProject/Assets/Scripts` 전체 (84개 `.cs`, 프로젝트 자체 코드 약 5,100줄)
**기준 문서**: [`systems/`](systems/)

---

## 결론 먼저

현재 코드의 가장 큰 문제는 스타일이나 중복이 아니라 **권한 구조**다.

> **마스터가 게임 상태(덱·손패·HP)를 소유하고 있지 않다.**
> 각 클라이언트가 자기 손패를 로컬에 들고, 카드 효과도 각자 실행한다.

기획서의 카드 59장 중 **약 18장이 "상대 손패를 조작"** 한다(A04, A05, A11, A18, T07…).
마스터가 손패를 모르면 이 카드들은 **원리적으로 구현할 수 없다.**
그 위에 UI를 더 얹거나 카드를 더 만드는 것은 나중에 전부 다시 만드는 일이 된다.

따라서 순서는 다음과 같다.

```
0. 정리 (인코딩·레거시·폴더)  →  1. 마스터 권한 기반  →  2. 카드 파이프라인
→  3. 체력/사망  →  4. 턴/승리  →  5. UI 재연결  →  6. 카드 대량 구현
```

**0단계와 1단계를 끝내기 전에는 새 카드나 새 UI를 만들지 않는다.**

---

# 1부. 문제점 진단

## A. 구조 문제 (가장 시급)

### A-1. 마스터가 게임 상태를 소유하지 않는다 🔴

| 상태 | 현재 원본 위치 | 있어야 할 곳 |
| --- | --- | --- |
| 손패 내용 | 각 클라이언트 로컬 (`PlayerHandPresenter.playerHand`) | 마스터 |
| 덱 | Room CustomProperties에 전체 배열 복제 | 마스터 (비공개) |
| HP | **아무 데도 없음** (변경하는 코드 자체가 없음) | 마스터 |
| 함정 슬롯 | 존재하지 않음 | 마스터 |

**영향**: 손패 조작 카드 18장, 함정 카드 17장 전부 구현 불가.

### A-2. 카드 효과를 모든 클라이언트가 각자 실행한다 🔴

`CardPlayManager.RPC_BroadcastPush`가 `RpcTarget.All`로 전파되고, 각 클라이언트가 자기 `chainList`에 추가한 뒤 각자 `CardData.PlayCard()`를 실행한다.

* `RequestCancelNext()`는 **로컬에서만** `isCanceled = true`로 바꾼다 → 클라이언트마다 무효화 결과가 달라진다.
* 난수를 쓰는 효과(무작위 N장 버림)는 클라이언트마다 다른 결과가 나온다.
* `RPC_RequestPush`의 `//유효성을 검사` 주석 아래에 검증 코드가 없다.

### A-3. 레거시와 신규 시스템이 동시에 존재한다 🔴

| 기능 | 레거시 | 신규 |
| --- | --- | --- |
| 게임 규칙 / 턴 | `Hands/GameRule.cs` | `Refactor/Turn/TurnManager.cs` |
| 카드 체인 | `Card/CardSystem.cs` | `Refactor/Card/CardPlayManager.cs` |
| 내 손패 | `Hands/PlayerHandsScripts.cs` | `Refactor/Hand/PlayerHand*` |
| 상대 손패 | `Hands/OtherPlayerHands.cs` | `Refactor/Hand/OtherPlayerHand*` |
| 카드 오브젝트 | `Card/CardScript.cs` + `ToWho` + `CardCondition` + `CardAbility` | `Refactor/Card/CardView/Model/Presenter` + `CardData` |
| 플레이어 UI | `UI/GameUI/GameUIManager` + `GamePlayerInfoUI` | `Refactor/Seat/SeatManager` + `PlayerSeat` |
| 씬 | `Scenes/GameScene.unity` | `Scenes/Refactor/RefactorGameScene.unity` |

같은 기능이 두 벌이고, 신규 쪽도 레거시(`ClickManager`)를 섞어 쓴다.
`ClickManager`는 두 씬 모두에서 쓰이며 `TrunEndButton`은 레거시 `GameRule`을 호출한다.

### A-4. 소스 파일 인코딩 깨짐 🔴

84개 중 **21개** 파일이 CP949로 저장되어 UTF-8에서 한글이 `��` 로 깨진다.

```
Card/CardSystem.cs                    Refactor/Card/CardData.cs
Card/CardScript.cs                    Refactor/Card/CardPlayManager.cs
Card/Ability/Card_Effect_Cancel.cs    Refactor/Card/CardPresenter.cs
Card/Ability/Card_Effect_Damage.cs    Refactor/Card/ChainItem.cs
Card/Who/ToWho.cs                     Refactor/Card/TargetSelectionManager.cs
Card/Condition/CardCondition.cs       Refactor/CardCondition/MyTurn_Condition.cs
Hands/GameRule.cs                     Refactor/CardEffect/DamageEffect.cs
Hands/OtherPlayerHands.cs             Refactor/Deck/DeckRecipe.cs
Hands/PlayerHandsScripts.cs           Refactor/Hand/PlayerHandPresenter.cs
Interface/PrototypeNetwork.cs         Refactor/Hand/PlayerHandView.cs
Refactor/Player/PlayerPresenter.cs
```

주석뿐 아니라 **사용자에게 보이는 문자열**도 깨져 있다.
`MyTurn_Condition.CheckCondition()`의 실패 사유 문자열이 깨진 상태로 UI에 노출될 수 있다.

### A-5. 네임스페이스 부재

`Core.Bootstrapper`와 `Network.*` 를 제외한 모든 스크립트가 전역 네임스페이스에 있다.
특히 `public enum Type { Action, Counter, Trap }` 은 **`System.Type`을 가린다.**
`Card`, `Deck`, `Singleton` 같은 일반적인 이름도 전부 전역에 있다.

### A-6. `Refactor` 폴더가 영구 구조가 되었다

작업 상태를 나타내는 이름이 실제 아키텍처 폴더가 되어, 리팩토링이 끝나도 이름이 남는다.

---

## B. 네트워크 / 동기화 문제

### B-1. 덱 전체를 Room CustomProperties로 동기화 🔴

`DeckPresenter.ExecuteDrawAndSync`가 드로우 **1장마다 덱 전체 카드 ID 배열**을 `SetCustomProperties`로 보낸다.
카드가 많을수록 트래픽이 커지고, 덱 내용이 전원에게 공개된다(비공개여야 함).

### B-2. 마스터 가드 누락 🔴

```csharp
[PunRPC]
private void RPC_RequestDrawToMaster(int requesterActorNumber)
{
    ExecuteDrawAndSync(requesterActorNumber);   // 마스터 검사 없음
}
```

### B-3. Photon 프로퍼티 키가 산재하고 서로 맞지 않는다 🔴

| 키 | 쓰는 곳 | 읽는 곳 |
| --- | --- | --- |
| `"PlayerHP"` | `GameRule`(레거시) | `PlayerInfoData`, `GameUIManager`(레거시) |
| `"HP"` | **없음** | `PlayerPresenter`(신규) |
| `"CardsCount"` | `GameRule`(레거시) | `OtherPlayerHands`, `GameUIManager` |
| `"HandCount"` | `PlayerHandPresenter`(신규) | `OtherPlayerHandPresenter`, `PlayerPresenter` |
| `"turn"` | `TurnManager` | `TurnManager` |
| `"RoomDeck"` | `DeckPresenter` | `DeckPresenter` |

**신규 씬에서 `"HP"`를 쓰는 코드가 없다. 즉 HP UI는 절대 갱신되지 않는다.**

### B-4. 반응 시간이 `Invoke` 하드코딩

`waitTimeforNextCard(4f) + 0.5f = 4.5초`. 기획은 **5초**이고, 카운터 등록 시 갱신되어야 하는데 갱신 로직이 없다.
`PhotonNetwork.Time` 기준도 아니라 클라이언트마다 타이밍이 다르다.

### B-5. `isResolutioning` 가드가 동작하지 않는다

```csharp
public void StartChainResolution()
{
    isResolutioning = true;
    if (PhotonNetwork.IsMasterClient) { ... }
    isResolutioning = false;     // 같은 프레임에 바로 해제 → 가드 의미 없음
}
```

### B-6. RPC 이름을 문자열로 전달

`CardPlayManager`는 `"RPC_RequestPush"` 문자열, `TurnManager`/`DeckPresenter`는 `nameof()`. 혼재.

### B-7. 초기 배분이 각 클라이언트 개별 요청

`DeckPresenter.Start()`에서 **모든 클라이언트가 각자** `RequestDrawCard()`를 7회 호출한다.
마스터가 `InitDeck()`을 끝내기 전에 다른 클라이언트의 RPC가 도착할 수 있다(경합).

---

## C. 명확한 버그

| # | 위치 | 내용 | 영향 |
| --- | --- | --- | --- |
| C-1 | `GameEvents.RaiseHandCountChanged` | `OnHandCountChanged` 대신 **`OnHPChanged`를 호출** | 손패 수 변경이 HP 이벤트로 전파됨 |
| C-2 | `TurnManager.GetNextActor(int currentActor)` | 인자를 무시하고 `CurrentTurnActor`와 비교 | 이탈 시 턴 계산 오류 |
| C-3 | `TurnManager.StartFirstTurn` | `PlayerList[0]` 고정 | 기획 "턴 순서 랜덤" 위반 |
| C-4 | `DeckPresenter.startHands` | `[SerializeField] public static int` — **static은 직렬화되지 않음** | Inspector 값이 무시됨 |
| C-5 | 초기 손패 수 | `GameStatus=7`(미사용) / `DeckPresenter=7`(사용) / `GameRule=1`(레거시) | 기획은 **5장** |
| C-6 | `Deck.Shuffle()` | **빈 함수** | 덱이 항상 같은 순서 |
| C-7 | `Deck.DrawAt(index)` | 범위 검사 없음, 덱 소진 재생성 없음 | 소진 시 게임 진행 불가 |
| C-8 | `CardView.OnPointerUp` | 카드 사용 후 손패 리스트에서 제거하지 않음 | **쓴 카드가 손에 남는다** |
| C-9 | `CardData.PlayCard` | `targets`가 비면 효과가 한 번도 실행 안 됨 | `TargetType.None` 카드 구현 불가 (A07) |
| C-10 | `CardData.PlayCard` | 대상별 순차 실행 | 기획 "다중 대상은 변경 전 HP 기준 일괄 계산" 위반 |
| C-11 | `CardPresenter.OnCardDropped` | `async void` | 예외가 삼켜짐 |
| C-12 | `TargetSelectionManager.SelectPlayer` | 취소 토큰 없는 `Task.Yield` 루프 | 오브젝트 파괴 후에도 계속 돎 |
| C-13 | `PlayerSeat.SetHpGauge` / `SetCardCountUI` | **호출하는 곳이 없음** | HP·손패 UI 미동작 |
| C-14 | `PlayerSeat.OnEnable` | 비활성 상태에서 `rect.width` 측정 | 게이지 최대폭이 0이 될 수 있음 |
| C-15 | `TurnUI.OnEnable` | `SeatManager.Instance` 접근 | `Awake` 순서 미보장 → null 위험 |
| C-16 | `SeatManager` + `HandSeatManager` | 둘 다 `Start()`에서 좌석 계산 | 실행 순서 의존 |
| C-17 | `OtherPlayerHandPresenter` | `handCount > realCount`(초과)는 보정 안 함 | 카드 소모 시 화면 불일치 |
| C-18 | `Card` 구조체 | `ID` 하나뿐 | 같은 카드 2장을 구분 불가 |
| C-19 | `TargetSelectionManager` / `ClickManager` | 자체 싱글톤 구현 | 프로젝트에 `Singleton<T>` 있는데 미사용 |
| C-20 | `PrototypeNetwork` | `MaxPlayers = 6`, `Screen.SetResolution(960,540)` | 기획(2~4인, 모바일) 위반 |
| C-21 | `ClickManager` | 취소 입력이 **마우스 우클릭** | 모바일에서 불가 |
| C-22 | 오타 | `ChainItem.caseter`, `TrunEndButton`, `Card_Effect.Excute` | — |
| C-23 | `OtherPlayerHandPresenter.curNum` | 사용되지 않는 static 필드 | 죽은 코드 |
| C-24 | `UIClickChecker` | 빈 `Start`/`Update`만 있는 빈 클래스 | 죽은 코드 |

---

## D. 중복 / 정리 대상

| 대상 | 내용 |
| --- | --- |
| `PlayerHandView.PutAwayMyCards` ↔ `OtherPlayerHandView.PutAwayMyCards` | **약 45줄 완전 동일** |
| `CardSystem` ↔ `CardPlayManager` | 체인 로직 거의 동일 |
| `GameUIManager`+`GamePlayerInfoUI` ↔ `SeatManager`+`PlayerSeat` | 좌석 UI 이중 구현 |
| `GameRule`(레거시)의 턴 로직 ↔ `TurnManager` | 턴 로직 이중 구현 |
| 빈 `Update()` | `HandSeatManager`, `UIClickChecker` 등 — 매 프레임 호출 비용 |
| 주석 처리된 대량 코드 | `ToWho.cs`(약 150줄), `Deck.AutoAssignIDs`, `PlayerHandPresenter.RefreshMyHandCount` 등 |

---

## E. 저장소 / 빌드 위생

| # | 문제 | 상세 |
| --- | --- | --- |
| E-1 | **ParrelSync 클론이 커밋됨** 🔴 | `MuffinProject_clone_0/` **2,991개 파일**. 로컬 작업 복제본이며 절대 커밋 대상이 아니다 |
| E-2 | `.gitignore`에 클론 패턴 없음 | `*_clone_*` 추가 필요 |
| E-3 | Photon SDK 973파일 커밋 | 팀 규모상 허용 가능하나, 데모/튜토리얼 씬(`Demos/`)은 제거 검토 |
| E-4 | `.gitattributes` 없음 | 인코딩·줄바꿈·씬 파일 병합 설정 부재 |
| E-5 | `.editorconfig` 없음 | IDE별 인코딩·들여쓰기 차이 |
| E-6 | 어셈블리 정의(`.asmdef`) 없음 | 전체 재컴파일. 스크립트 수가 늘수록 반복 시간 증가 |
| E-7 | 커밋 메시지 | "save commit", "bugFix" 등 내용 추적 불가 |

---

# 2부. 리팩토링 플랜

## 원칙

1. **한 번에 하나의 단계만** 진행한다. 각 단계 끝에서 게임이 실행 가능해야 한다.
2. 각 단계는 **별도 브랜치 + PR**. 씬 파일 충돌을 줄이기 위해 씬을 건드리는 작업은 한 사람만 한다.
3. 레거시는 **한 번에 지우지 않고** 신규가 기능을 대체한 시점에 지운다.
4. 기획 문서(`systems/`)와 다른 코드는 문서를 기준으로 고친다.

---

## Phase 0 — 정리 (선행 조건)

> 목표: 이후 모든 작업의 기반 정리. 게임 동작은 바뀌지 않는다.

| # | 작업 | 대상 | 비고 |
| --- | --- | --- | --- |
| 0-1 | 전체 `.cs` **UTF-8 (BOM) 재저장** | 21개 파일 | 깨진 한글은 의미를 복원해 다시 작성 |
| 0-2 | `.gitattributes` 추가 | 루트 | `*.cs text working-tree-encoding=UTF-8`, `*.unity merge=unity` |
| 0-3 | `.editorconfig` 추가 | 루트 | 인코딩·들여쓰기·줄바꿈 |
| 0-4 | `MuffinProject_clone_0/` **git에서 제거** | 2,991파일 | `git rm -r --cached` + `.gitignore`에 `*_clone_*` |
| 0-5 | 죽은 코드 제거 | `UIClickChecker`, `curNum`, 주석 처리된 대량 코드 | |
| 0-6 | 오타 수정 | `caseter`→`caster`, `TrunEndButton`→`TurnEndButton`, `Excute`→`Execute` | |
| 0-7 | 빈 `Update()` 제거 | `HandSeatManager` 등 | |

### Phase 0 산출물

* 한글이 깨지지 않는 소스
* 저장소에서 클론 디렉터리 제거

---

## Phase 1 — 폴더 · 네임스페이스 · 레거시 정리

> 목표: 코드가 어디에 있는지 한눈에 보이게 한다.

### 1-1. 레거시 제거 결정

| 대상 | 처리 |
| --- | --- |
| `Scripts/Hands/` (GameRule, PlayerHandsScripts, OtherPlayerHands) | **삭제** |
| `Scripts/Card/` (CardScript, CardSystem, ToWho, CardCondition, CardAbility, Card_Effect_*) | **삭제** |
| `Scripts/Interface/PrototypeNetwork.cs`, `TrunEndButton.cs` | **삭제** |
| `Scripts/UI/GameUI/` (GameUIManager, GamePlayerInfoUI) | **삭제** (PlayerSeat이 대체) |
| `Scripts/Data/PlayerInfoData.cs` | `HashtableExtensions`만 남기고 이동 |
| `Scenes/GameScene.unity`, `Scenes/Prototype_Lobby.unity` | **삭제** |
| `Prefab/PlayerCard_Prototype*.prefab`, `OthersCards.prefab` | **삭제** |
| `Scripts/Interface/ClickManager.cs` | 신규 입력 클래스로 재작성 후 삭제 |

> 삭제 전에 `RefactorGameScene`이 레거시를 참조하지 않는지 확인한다.
> 현재 `ClickManager`만 참조 중이다.

### 1-2. 폴더 구조 (제안)

```
Assets/Scripts/
  Core/            Singleton, Bootstrapper, ScenePaths, 상수(PlayerProps/RoomProps)
  Network/         PhotonConnection, PhotonRoom, NetworkManager, Events
  Game/
    Rules/         GameServer(마스터), GameState, ResolveChain
    Turn/          TurnManager, TurnTimer
    Deck/          Deck, DiscardPile, DeckService
    Hand/          Hand, HandService
    Traps/         TrapSlots
    Health/        HealthService, LifeState
    Cards/         CardData, CardDatabase, CardEffect/*, CardCondition/*
    Win/           WinChecker
  Presentation/
    Seat/          SeatManager, PlayerSeat
    Hand/          PlayerHandView, OtherPlayerHandView, HandLayout(공통)
    Card/          CardView, CardPresenter
    Deck/          DeckView
    Turn/          TurnUI
    Input/         GameInput
  UI/              Popup, Lobby, Room, NickName (현행 유지)
```

`Refactor/` 폴더는 소멸한다.

### 1-3. 네임스페이스 도입

`Muffin.Core` / `Muffin.Network` / `Muffin.Game.*` / `Muffin.Presentation.*` / `Muffin.UI`

### 1-4. 이름 변경

| 현재 | 변경 | 이유 |
| --- | --- | --- |
| `Type` (enum) | `CardType` | `System.Type` 가림 |
| `Card_Condition` | `CardCondition` | 컨벤션(언더스코어 금지) |
| `MyTurn_Condition` | `MyTurnCondition` | 동일 |
| `PlayerHandsScripts` | (삭제) | |
| `draw_A_Card` | `DrawCard` | 컨벤션 |
| `do_returnToOrigin` | `ReturnToOrigin` | 컨벤션 |
| `setHandMod` / `getHandMod` | `SetHandMode` / `IsHandMode` | 컨벤션 + 오타(Mod→Mode) |

### 1-5. 어셈블리 정의 추가

`Muffin.Core`, `Muffin.Network`, `Muffin.Game`, `Muffin.Presentation`, `Muffin.UI` — 컴파일 시간 단축.

---

## Phase 2 — 마스터 권한 기반 (핵심) 🔴

> 목표: **마스터가 게임 상태의 단일 소유자가 된다.**
> 기준: [`systems/09-network.md`](systems/09-network.md)

### 2-1. `GameServer` 도입 (마스터 전용)

마스터에서만 동작하는 클래스가 게임 상태 원본을 들고 있는다.

```
GameServer (MasterClient only)
 ├─ Deck          남은 카드 (인스턴스 단위, 비공개)
 ├─ DiscardPile   버림 더미
 ├─ Hands         Dictionary<actorNumber, List<CardInstance>>
 ├─ TrapSlots     Dictionary<actorNumber, CardInstance[3]>
 ├─ Healths       Dictionary<actorNumber, int>
 ├─ LifeStates    Dictionary<actorNumber, LifeState>
 ├─ TurnOrder     int[] (게임 시작 시 무작위 결정)
 └─ ResolveChain  처리 중인 카드 스택
```

* 비마스터 클라이언트에서는 이 클래스가 비활성이다.
* 모든 요청 RPC는 첫 줄에 `if (!PhotonNetwork.IsMasterClient) return;`.

### 2-2. `CardInstance` 도입

```csharp
public readonly struct CardInstance
{
    public readonly int InstanceId;   // 게임 내 고유
    public readonly int CardId;       // CardData.id
}
```

클라이언트가 카드를 사용할 때 `InstanceId`를 보낸다. 마스터는 소유권을 `InstanceId`로 검증한다.

### 2-3. 프로퍼티 키 상수화 + 정리

* `PlayerProps` / `RoomProps` 상수 클래스 신설
* `"PlayerHP"` / `"HP"` / `"CardsCount"` / `"HandCount"` 혼재 제거 → `hp`, `handCount`, `trapCount`, `lifeState`, `chapChu`
* `"RoomDeck"` **제거** (덱 전체 동기화 폐기). 대신 `deckCount` 만 공개

### 2-4. 요청-검증-전파 파이프라인

```csharp
// 클라이언트
photonView.RPC(nameof(RPC_RequestDraw), RpcTarget.MasterClient);

// 마스터
[PunRPC] void RPC_RequestDraw(PhotonMessageInfo info)
{
    if (!PhotonNetwork.IsMasterClient) return;
    if (!_server.CanDraw(info.Sender.ActorNumber, out string reason))
    {
        photonView.RPC(nameof(RPC_Rejected), info.Sender, reason);
        return;
    }
    var card = _server.Draw(info.Sender.ActorNumber);
    photonView.RPC(nameof(RPC_SyncDrawnCard), info.Sender, card.InstanceId, card.CardId); // 본인만
    photonView.RPC(nameof(RPC_OnDrawn), RpcTarget.All, info.Sender.ActorNumber);          // 전체
}
```

### 2-5. 초기화 순서 정리

`Phase 2` 완료 시 게임 시작은 **마스터 주도 1회 시퀀스**가 된다
([`systems/01-game-flow.md`](systems/01-game-flow.md) 3절).

* 덱 생성 → 인스턴스 ID 부여 → **셔플 구현** → HP 100 초기화 → **5장** 배분 → 턴 순서 **무작위** 결정

### Phase 2에서 해결되는 버그

B-1, B-2, B-3, B-7, C-3, C-4, C-5, C-6, C-7, C-18

---

## Phase 3 — 카드 처리 파이프라인

> 기준: [`systems/04-card.md`](systems/04-card.md)

### 3-1. 체인을 마스터 단독 소유로

* `chainList`를 `GameServer`로 이동. 클라이언트는 **표시용 사본**만 받는다.
* `isCanceled`는 마스터만 설정한다.
* `Invoke`/`CancelInvoke` 제거 → 마스터가 `PhotonNetwork.Time` 기준으로 **5초** 마감 시각을 계산해 브로드캐스트.

### 3-2. 반응 시간 상태 기계

```
Idle → CardRegistered(5초) → (카운터 등록 시 5초 재시작)
     → 마감 → ResolveChain(역순) → Idle
```

### 3-3. `EffectContext` 도입

효과가 실행에 필요한 정보를 한 객체로 받는다.

```csharp
public class EffectContext
{
    public int ResolveId;
    public int CasterActor;
    public int[] TargetActors;
    public GameServer Server;
    public bool IsCanceled;
}
```

* 효과는 `Execute(EffectContext)` 하나만 구현한다.
* 대상이 0개여도 1회 실행된다 (C-9 해결).
* 다중 대상은 **변경 전 HP를 먼저 읽어** 일괄 계산한다 (C-10 해결).

### 3-4. 효과 타입 정리

카드 59장에 스크립트 59개를 만들지 않는다.
[`systems/11-card-list.md`](systems/11-card-list.md) 5절의 **효과 타입 약 24종**을 ScriptableObject로 만들고 카드는 조합한다.

### 3-5. 대상 타입 확장

현재 `TargetType`은 6종뿐이라 아래를 표현할 수 없다.

* 조건부 대상: `찹츄 상태 전원`, `손패가 나보다 많은 전원`, `생존 전원`
* 카드 대상: `현재 처리 카드`, `현재 카운터`, `파괴될 내 함정`

### 3-6. 함정 슬롯 신규 구현

현재 **전혀 없다.** 카드 59장 중 17장(+7장)이 이것을 요구한다.

* 슬롯 3칸, 설치/발동/파괴/공개
* 개수는 공개, 종류는 비공개

### 3-7. 사용한 카드 손패에서 제거 (C-8)

마스터 승인 후 손패에서 제거 → 버림 더미로. 뷰도 함께 갱신.

---

## Phase 4 — 체력 · 사망

> 기준: [`systems/06-health.md`](systems/06-health.md)

1. HP **`int` 통일** (`PlayerModel`, `GameStatus`, `PlayerInfoData`)
2. 마스터 전용 `HealthService.ApplyDamage/ApplyHeal`
3. 피해 감소·무효화·전환을 모두 계산한 뒤 **한 번만** HP 반영
4. `LifeState` (Alive / DeathPending / Dead)
5. 위험 상태(1~20) / 사망 대기(5초) 사건 생성
6. 최종 사망 → 손패·함정 전부 버림 더미, 턴·대상에서 제외
7. HP 변경 기록(처리 ID 포함)

---

## Phase 5 — 턴 · 승리

> 기준: [`systems/03-turn.md`](systems/03-turn.md), [`systems/07-win-condition.md`](systems/07-win-condition.md)

1. 턴 순서 배열을 마스터가 **무작위**로 생성하고 고정 (C-2, C-3 해결)
2. **메인 행동 1회 제한** (현재 없음 — 무제한 드로우 가능)
3. 턴 타이머 (마스터 판정, 클라이언트는 표시만) — *제한 시간 확정 후*
4. 자동 제출 — *대상 결정 로직 확정 후*
5. 찹츄 상태 + 찹츄 버튼 + 승리 판정
6. 처치 승리 / 무승부 판정
7. 결과 화면 — *기획 확정 후*

> 3·4·7은 기획 미정 항목에 막혀 있다. 확정 전에는 구현하지 않는다.

---

## Phase 6 — UI 재연결

> 기준: [`systems/10-ui.md`](systems/10-ui.md)

1. `GameEvents` 재정의 — **모든 이벤트에 `actorNumber` 포함**
   (현재 `OnHPChanged(float)`는 누구의 HP인지 알 수 없다)
2. `GameEvents.RaiseHandCountChanged`의 잘못된 호출 수정 (C-1)
3. `PlayerSeat`의 HP 게이지 / 손패 장수 실제 연결 (C-13)
4. `PlayerSeat` 게이지 최대폭 측정 시점 수정 (C-14)
5. 좌석 계산을 `SeatManager` 한 곳으로 통합, `HandSeatManager`는 결과만 사용 (C-16)
6. `OnEnable`에서 다른 싱글톤 접근 제거 (C-15)
7. 손패 레이아웃 공통화 — `PutAwayMyCards` 중복 45줄 제거
8. 함정 슬롯 UI / 반응 타이머 UI 신규
9. 입력 재작성 — 우클릭 취소를 모바일 입력으로 (C-21)
10. `TargetSelectionManager`를 `Singleton<T>` 기반으로, 취소 토큰 적용 (C-12, C-19)

---

## Phase 7 — 카드 대량 구현

Phase 2~4가 끝나야 시작할 수 있다.

1. 카드 수치 N 확정 (기획)
2. MVP 6장 먼저 구현 → 구조 검증 ([`systems/11-card-list.md`](systems/11-card-list.md) 6절)
3. 효과 타입 24종 완성
4. 나머지 53장을 데이터(에셋)로 추가

---

# 3부. 우선순위 요약

| 순위 | 작업 | 이유 |
| --- | --- | --- |
| 🔴 1 | Phase 0 (인코딩, 클론 제거) | 반나절. 지금 안 하면 계속 누적 |
| 🔴 2 | Phase 2 (마스터 권한) | **카드 35장 이상이 여기 막혀 있다** |
| 🔴 3 | Phase 3 (카드 파이프라인 + 함정 슬롯) | 게임의 핵심 |
| 🟡 4 | Phase 1 (폴더/네임스페이스) | Phase 2와 함께 하면 충돌이 크다. **Phase 2 이후 권장** |
| 🟡 5 | Phase 4 (체력) | 승리 조건의 절반 |
| 🟡 6 | Phase 5 (턴/승리) | 게임이 끝나게 만든다 |
| 🟢 7 | Phase 6 (UI) | Phase 2~5 결과를 보여주는 단계 |
| 🟢 8 | Phase 7 (카드) | 마지막 |

> Phase 1(폴더 이동)과 Phase 2(로직 변경)를 동시에 하면 리뷰가 불가능해진다.
> **Phase 0 → Phase 2 → Phase 1 → Phase 3** 순서를 권장한다.

---

# 4부. 기획 확정이 필요한 항목 (구현을 막고 있음)

| # | 항목 | 막고 있는 Phase |
| --- | --- | --- |
| 1 | **카드별 수치 N / M** | Phase 7 전체 |
| 2 | 덱 구성 (카드별 매수, 총 장수) | Phase 2 (초기화) |
| 3 | 턴 제한 시간 확정 값 | Phase 5-3 |
| 4 | 자동 제출 시 대상 결정 로직 | Phase 5-4 |
| 5 | 연속 미제출 처리 (AFK vs 강제 퇴장) | Phase 5 |
| 6 | 턴 진행 방향 개념 | Phase 3 (A07, A10) |
| 7 | 방 시작 조건 / 나가기 처리 | 방 시스템 |
| 8 | 마스터 이탈 시 처리 | Phase 2 |
| 9 | 재접속 정책 | Phase 2 |
| 10 | 결과 화면 | Phase 5-7 |

---

# 5부. 하지 말아야 할 것

* Phase 2 이전에 **새 카드 에셋을 추가하지 않는다.** 전부 다시 만들게 된다.
* Phase 2 이전에 **함정 슬롯 UI를 만들지 않는다.** 데이터 구조가 없다.
* 레거시 코드를 "일단 고쳐서 쓰지" 않는다. 삭제 대상이다.
* 한 PR에서 폴더 이동과 로직 변경을 함께 하지 않는다.
* 씬 파일을 두 사람이 동시에 편집하지 않는다.
