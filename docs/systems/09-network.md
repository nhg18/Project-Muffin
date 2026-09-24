# 09. 네트워크 권한 · 동기화 규약

**최종 수정일**: 2026-09-24
**분류**: Core
**전제**: Photon PUN2. 마스터 클라이언트(방장)가 서버 역할을 겸한다.

---

## 1. 목적

"누가 무엇을 판정하고, 무엇을 어떻게 전파하는가"를 하나의 규칙으로 고정한다.
카드·체력·덱·턴 문서의 모든 규칙은 이 문서의 권한 모델 위에서 동작한다.

---

## 2. 핵심 규칙 (한 문장씩)

1. **게임 규칙 판정은 전부 마스터가 한다.** 클라이언트는 요청만 보낸다.
2. **원본 상태는 마스터 메모리에 하나만 존재한다.** 다른 클라이언트의 값은 전부 사본이다.
3. **클라이언트는 원본을 직접 수정하지 않는다.** 마스터의 브로드캐스트 결과만 반영한다.
4. **비공개 정보는 소유자에게만 보낸다.**
5. **연출은 로컬, 판정은 마스터.** 연출을 먼저 보여주고 결과가 다르면 되돌리는 방식(예측)은 MVP에서 쓰지 않는다.

---

## 3. 권한 표 (Authority Matrix)

| 데이터 | 원본 소유 | 공개 범위 | 전송 방식 |
| --- | --- | --- | --- |
| 덱 (남은 카드 목록·순서) | 마스터 | **비공개** | 전송 안 함. 잔여 **장수**만 공개 |
| 덱 잔여 장수 | 마스터 | 전체 공개 | Room CustomProperties |
| 버림 더미 | 마스터 | 전체 공개 | 카드가 버려질 때 이벤트로 전파 |
| 손패 **내용** | 마스터 | **소유자만** | 소유자 지정 RPC |
| 손패 **장수** | 마스터 | 전체 공개 | Player CustomProperties |
| 함정 슬롯 **내용** | 마스터 | **소유자만** | 소유자 지정 RPC |
| 함정 슬롯 **개수** | 마스터 | 전체 공개 | Player CustomProperties |
| HP | 마스터 | 전체 공개 | Player CustomProperties |
| 생존 상태 (Alive/DeathPending/Dead) | 마스터 | 전체 공개 | Player CustomProperties |
| 찹츄 상태 | 마스터 | 전체 공개 | Player CustomProperties |
| 현재 턴 주인 | 마스터 | 전체 공개 | Room CustomProperties |
| 턴 진행 방향 | 마스터 | 전체 공개 | Room CustomProperties (**미정** — `03-turn` 참고) |
| 처리 체인 | 마스터 | 전체 공개 | 체인 변화 시 RPC 브로드캐스트 |
| 반응 타이머 시작 시각 | 마스터 | 전체 공개 | RPC(`PhotonNetwork.Time` 기준) |
| 좌석 배치 | **각 클라이언트 로컬 계산** | 로컬 | 전송 안 함 (`02-player` 참고) |
| 카드 호버/드래그/연출 | 각 클라이언트 | 로컬 | 전송 안 함 |

---

## 4. 요청-검증-전파 흐름

모든 플레이어 행동은 동일한 3단계를 거친다.

```
[클라이언트] 의도 전송 (Request)
      ↓ RpcTarget.MasterClient
[마스터]     검증 (Validate)
      ↓ 실패 → 요청자에게만 거절 사유 전송, 상태 변경 없음
      ↓ 성공
[마스터]     상태 변경 (Apply) — 마스터 메모리의 원본만 변경
      ↓ RpcTarget.All / 대상 지정
[전체]       결과 반영 + 연출
```

### 4.1 마스터가 검증하는 항목 (공통)

| 검증 | 실패 시 |
| --- | --- |
| 요청자가 실제로 그 카드를 소유하는가 (손패/함정 슬롯에 있는가) | 거절 |
| 요청 시점이 유효한가 (턴, 반응 시간 내, 메인 행동 미사용 등) | 거절 |
| 대상이 유효한가 (존재 · 생존 · 카드가 요구하는 조건) | 거절 |
| 카드의 사용 조건을 만족하는가 | 거절 |
| 이미 처리된 요청인가 (처리 ID 중복) | 무시 |

> 거절된 요청은 **상태를 전혀 변경하지 않는다.** 카드는 원래 위치를 유지한다.

### 4.2 동시 요청

* 함정 발동, 카운터 사용처럼 여러 명이 동시에 요청할 수 있는 행동은 **마스터 도착 순서로 1개만 승인**한다.
* 늦게 도착한 요청은 거절하고, 해당 카드는 원래 위치를 유지한다.
* 마스터는 승인 여부를 요청자에게 개별 응답한다.

---

## 5. 처리 ID (Resolve ID)

* 마스터는 카드 처리 1건마다 증가하는 **처리 ID**를 발급한다.
* 모든 상태 변경 브로드캐스트에 처리 ID를 포함한다.
* 클라이언트는 이미 처리한 ID를 다시 받으면 무시한다 (중복 적용 방지).
* HP 변경 기록에도 처리 ID를 남긴다 (`06-health` 참고).

---

## 6. CustomProperties 사용 규칙

### 6.1 쓸 수 있는 것

* 모두가 항상 봐도 되는 **작은 값**: HP(int), 손패 장수(int), 함정 개수(int), 생존 상태(byte), 찹츄 상태(bool), 턴 주인(int)
* 늦게 들어온 클라이언트가 현재 상태를 복원해야 하는 값

### 6.2 쓰면 안 되는 것

* 카드 ID **배열** 등 크기가 큰 값
* 비공개 정보 (손패 내용, 함정 종류)
* 프레임 단위로 바뀌는 값

> ⚠ 현재 코드 `DeckPresenter`는 드로우마다 덱 전체 카드 ID 배열을 Room CustomProperties로 동기화한다. 이 규칙 위반이며 제거 대상이다.

### 6.3 키 상수

문자열 리터럴 금지. 상수 클래스에 모은다.

```csharp
public static class PlayerProps
{
    public const string Hp        = "hp";
    public const string HandCount = "handCount";
    public const string TrapCount = "trapCount";
    public const string LifeState = "lifeState";
    public const string ChapChu   = "chapChu";
}

public static class RoomProps
{
    public const string TurnActor    = "turnActor";
    public const string DeckCount    = "deckCount";
    public const string TurnDirection = "turnDir";
}
```

> ⚠ 현재 코드에는 `"HP"`, `"PlayerHP"`, `"HandCount"`, `"CardsCount"`, `"isChapChu"`, `"turn"`, `"RoomDeck"` 이 혼재한다.
> `PlayerPresenter`는 `"HP"`를 읽고 `PlayerInfoData`는 `"PlayerHP"`를 읽는데 **어느 쪽도 쓰는 코드가 없어 HP UI가 갱신되지 않는다.**

---

## 7. 카드 인스턴스 ID

* 카드는 **종류 ID(`CardId`)** 와 **인스턴스 ID(`CardInstanceId`)** 를 구분한다.
* `CardId`: 카드 종류. `CardData.id`. 같은 카드는 값이 같다.
* `CardInstanceId`: 게임 시작 시 덱을 만들 때 카드 1장마다 부여하는 고유 번호. 게임 내내 유지된다.
* 클라이언트가 "이 카드를 쓰겠다"고 보낼 때는 **`CardInstanceId`** 를 보낸다.

**이유**: 같은 카드를 2장 들고 있을 때 `CardId`만으로는 어느 장인지 특정할 수 없다.
손패 훔치기(A04), 강제 버림(A05), 손패 교환(A11) 같은 카드는 인스턴스 단위 식별이 필수다.

---

## 8. 시간 동기화

* 반응 시간(5초)과 턴 제한 시간은 `PhotonNetwork.Time` (서버 시각) 기준으로 계산한다.
* 마스터는 "이 처리의 반응 마감 시각"을 절대 시각으로 브로드캐스트한다.
* 각 클라이언트는 남은 시간을 로컬에서 계산해 표시만 한다.
* **제한 시간 판정은 마스터만 한다.** 클라이언트 타이머가 0이 되어도 자체적으로 아무것도 확정하지 않는다.
* 마감 시각 이후 도착한 요청은 마스터가 거절한다.

---

## 9. 마스터 클라이언트 이전

| 상황 | 처리 | 구분 |
| --- | --- | --- |
| 마스터가 나감 | Photon이 다음 클라이언트를 마스터로 승격 | 확정(Photon 동작) |
| 승격된 새 마스터가 게임 상태를 복원 | **미정 — 결정 필요** | 미정 |

> 마스터 메모리에만 있던 덱 순서·손패 내용은 마스터 교체 시 소실된다.
> MVP에서는 **"마스터가 나가면 게임 종료"** 로 처리하는 것을 제안한다. (제안)

---

## 10. RPC 규약

```csharp
// 클라이언트 → 마스터
photonView.RPC(nameof(RPC_RequestPlayCard), RpcTarget.MasterClient, cardInstanceId, targets);

// 마스터 → 전체
photonView.RPC(nameof(RPC_OnCardPlayed), RpcTarget.All, resolveId, actorNumber, cardId, targets);

// 마스터 → 특정 플레이어 (비공개 정보)
photonView.RPC(nameof(RPC_SyncMyHand), targetPlayer, cardInstanceIds, cardIds);
```

| 규칙 | 내용 |
| --- | --- |
| 메서드명 | `RPC_` 접두사 |
| 이름 전달 | `nameof()` 사용, 문자열 리터럴 금지 |
| 마스터 전용 RPC | 첫 줄에 `if (!PhotonNetwork.IsMasterClient) return;` |
| 요청자 식별 | `PhotonMessageInfo.Sender` 사용. 클라이언트가 보낸 actorNumber를 신뢰하지 않는다 |
| 명명 | 요청은 `RPC_Request*`, 결과 전파는 `RPC_On*`, 개별 응답은 `RPC_Reject*` |

---

## 11. 예외 처리

| 상황 | 처리 | 구분 |
| --- | --- | --- |
| 반응 시간 내에 도착하지 않은 요청 | 거절 | 확정 |
| 조건 미충족 요청 | 거절, 카드 원위치 유지 | 확정 |
| 처리 중 요청자가 사망 | 이미 승인된 카드는 끝까지 처리 | 확정 |
| 처리 중 대상이 사망 | 대상 유지, 해당 효과만 불발 | 확정 |
| 연결 끊김 | 미제출로 판정 | 확정 |
| 재접속 시 상태 복원 | **미정 — 결정 필요** | 미정 |
| 서버 접속 제한 시간 | 접속 시작 후 **15초** 안에 마스터 서버 접속이 끝나지 않으면 끊고 `ClientTimeout` 으로 알림 | 확정 |
| 인터넷 없음 (접속 전) | Photon 을 호출하지 않고 `ExceptionOnConnect` 로 알림 (PUN 이 연결 실패 시 보내는 값과 동일) | 확정 |
| 접속 실패 후 재시도 | 자동 재시도 없음. UI 가 사유를 표시하고 사용자가 재시도(`NetworkManager.Connect`) | 제안 |

---

## 12. 현재 구현 상태

| 항목 | 상태 |
| --- | --- |
| 마스터 경유 카드 요청 (`CardPlayManager.RPC_RequestPush`) | 있음. **검증 로직 없음** (`isResolutioning` 체크만) |
| 마스터 소유 덱 | 없음. 덱 배열을 Room Property로 전체 동기화 |
| 마스터 소유 손패 | **없음.** 손패는 각 클라이언트 로컬에만 존재 |
| 마스터 소유 HP | 없음. HP를 변경하는 코드 자체가 없음 |
| 처리 ID | 없음 |
| 카드 인스턴스 ID | 없음 (`Card` 구조체가 `ID` 하나만 보유) |
| 시간 동기화 | 없음 (`Invoke` 하드코딩 4.5초, 기획 5초와 불일치) |
| RPC `nameof` 사용 | 혼재 (`TurnManager`/`DeckPresenter`는 사용, `CardPlayManager`는 문자열) |
| 마스터 가드 | 일부 누락 (`RPC_RequestDrawToMaster`) |

---

## 13. 미정 / 결정 필요

1. 마스터 교체 시 게임 상태 복원 방식 (또는 "마스터 이탈 = 게임 종료" 확정)
2. 재접속 허용 시간과 복원 범위 (손패·함정·HP·턴)
3. 턴 진행 방향을 Room Property로 둘지 여부 (A07 카드가 요구)
