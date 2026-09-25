# 08. 방(Room) · 로비

**최종 수정일**: 2026-09-25
**분류**: MVP
**상태: 결정되지 않은 항목이 많다. 구현 전 5절을 먼저 확정해야 한다.**

---

## 1. 목적

게임 시작 전 플레이어들이 모여 대기하는 공간을 제공한다.

---

## 2. 핵심 수치

| 항목 | 값 | 구분 |
| --- | --- | --- |
| 최소 인원 | 2명 | 확정 (`NetworkManager.MinPlayers`) |
| 최대 인원 | 4명 | 확정 (`NetworkManager.MaxPlayers`) |
| room code | 랜덤 생성 코드 | 확정(생성됨) / **미정**(자릿수·규칙) |

> ⚠ 레거시 `PrototypeNetwork`는 `MaxPlayers = 6`으로 방을 만든다. 기획(2~4인) 위반.

---

## 3. 화면 구성 (노션 원본 = 명칭만 존재)

| 요소 | 설명 | 결정 상태 |
| --- | --- | --- |
| 플레이어 리스트 | 참가자 목록 | 명칭만 |
| room code | 방 참가 코드 | 명칭만 |
| 로그 | 방 이벤트 기록 | **내용 미정** |
| 설정 버튼 | 방 설정 | 명칭만 |
| 나가기 버튼 | 방 퇴장 | **처리 미정** |
| 시작 버튼 | 게임 시작 | **활성 조건 미정** |
| 친구 초대 버튼 | 친구 초대 | Optional |

---

## 4. 현재 동작 (코드 기준)

| 처리 | 동작 | 위치 |
| --- | --- | --- |
| 방 생성 | 랜덤 코드 생성 → `CreateRoom(code, maxPlayers=4, visible, open)` | `PhotonRoom.CreateRoom` |
| 코드 중복 (에러 32766) | 자동으로 다시 생성 시도 | `PhotonRoom.OnCreateRoomFailed` |
| 방 참가 | 코드를 `Trim().ToUpper()` 후 참가 | `PhotonRoom.JoinRoom` |
| 랜덤 매칭 | `JoinRandomRoom()` → 빈 방이 있으면 참가, 없으면(`OnJoinRandomFailed`) 새 방 생성 — **확정 (2026-09-25, MVP 포함)** | `PhotonRoom.JoinRandomRoom` · `OnJoinRandomFailed` |
| 참가 실패 | `RoomEvents.RaiseJoinRoomFailed` → 팝업 | `PhotonRoom` |
| 방 씬 이동 | `OnJoinedRoom` → Room 씬 로드 | `PhotonRoom` |
| 게임 시작 | 방장이 `PhotonNetwork.LoadLevel` | 레거시 `PrototypeNetwork.GameStart` |

---

## 5. 결정이 필요한 항목 (전부 미정)

| # | 항목 | 필요한 결정 |
| --- | --- | --- |
| 1 | 표시 조건 | 방장과 참가자에게 UI가 다르게 보이는가 |
| 2 | 시작 버튼 활성 조건 | 최소 인원(2명)만 충족하면 되는가, 전원 준비 완료가 필요한가 |
| 3 | room code 규칙 | 자릿수, 생성 방식, 만료 여부 |
| 4 | 로그 내용 | 입장 / 퇴장 / 설정 변경 중 무엇을 기록하는가 |
| 5 | 나가기 처리 | 방장이 나가면 **위임**인가 **방 폭파**인가 |
| 6 | 준비(Ready) 상태 | 존재하는가 |
| 7 | 방 설정 항목 | 무엇을 설정할 수 있는가 |
| 8 | 게임 중 방 공개 여부 | 시작 후 `IsOpen = false` 처리 여부 |

**2번과 5번은 구현을 직접 막고 있다. 우선 결정 필요.**

---

## 6. 상태 흐름 (제안)

```
Lobby ──(방 생성)──→ Room(방장)
Lobby ──(코드 입력/초대)──→ Room(참가자)
Lobby ──(랜덤 매칭)──→ Room(빈 방이 있으면 참가자, 없으면 방장)
Room  ──(시작 버튼)──→ GameScene
Room  ──(나가기)──→ Lobby
```

---

## 7. 예외

| 상황 | 처리 | 구분 |
| --- | --- | --- |
| 존재하지 않는 코드 | 참가 실패 팝업 | 확정 (구현됨) |
| 인원 초과 | 참가 실패 팝업 | 확정 (구현됨) |
| 이미 시작된 방 | 참가 불가 | **미정** (`IsOpen` 처리 여부) |
| 방장 이탈 | **미정** | 미정 |
| 연결 끊김 | **미정** | 미정 |

---

## 8. 현재 구현 상태

| 항목 | 상태 |
| --- | --- |
| Photon 연결 / 닉네임 (`PhotonConnection`) | 구현됨 |
| 방 생성 / 참가 / 나가기 (`PhotonRoom`) | 구현됨 |
| 랜덤 코드 생성 (`RandomCode`) | 구현됨 |
| 이벤트 전파 (`RoomEvents`, `ConnectionEvents`) | 구현됨 |
| 팝업 (`PopupManager`) | 구현됨 |
| 닉네임 입력/검증 | 구현됨 |
| 방 UI (`RoomPanel`, `RoomInfoPanel`) | 부분 구현 |
| 준비 상태 | 미구현 |
| 방 설정 | 미구현 |
| 시작 조건 검증 | 미구현 |
| 로그 | 미구현 |

> 네트워크 연결 계층(`Scripts/Network`, `Scripts/Events`, `Scripts/UI`)은 프로젝트에서 **가장 잘 정리된 부분**이다.
> 인게임 쪽 리팩토링 시 이 계층의 구조(`Manager` + 기능별 클래스 + 정적 이벤트)를 참고한다.

---

## 9. 미정 / 결정 필요

5절 전체 (8개 항목).

> 로비 **화면**(버튼 크기 · 배치)은 [`14-lobby-ui.md`](14-lobby-ui.md).
