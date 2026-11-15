---
name: network-rpc
description: 찹츄 네트워크 권한 규칙. Photon RPC · CustomProperties · 방장 판정 코드를 만들거나 고칠 때, 또는 "방장이 판정할지 로컬 연출인지" 정할 때 먼저 읽는다.
---

# 네트워크 권한 규칙

> 규약 원문은 [`docs/systems/09-network.md`](../../../docs/systems/09-network.md).

1. 게임 규칙 판정은 전부 방장(마스터 클라이언트)이 한다. 클라이언트는 의도(`이 카드를, 이 대상에게`)만 요청한다.
2. 방장은 소유권 · 턴 · 조건 · 대상을 검증한 뒤에만 결과를 보낸다. 요청자는 `PhotonMessageInfo.Sender` 로 판단하고, 클라가 보낸 actorNumber 를 믿지 않는다.
3. 체력 · 덱 · 손패 · 함정 · 턴 · 찹츄 상태의 원본은 방장 메모리에 하나만 있다. 클라이언트는 받은 결과만 반영한다.
4. 비공개 정보(내 손패 내용, 남의 함정 종류)는 대상 지정 RPC 로 그 사람에게만 보낸다.
5. `CustomProperties` 는 모두가 봐도 되는 작은 값(턴 주인, 손패 장수, 체력, 생존)에만 쓴다. 키는 상수 클래스에 모은다.
6. 방장 전용 RPC 는 첫 줄에 `if (!PhotonNetwork.IsMasterClient) return;`. 요청 RPC 는 둘째 줄에 `if (!AllowRequest(info)) return;` (빈도 제한).
7. 아무 클라나 방 속성 · 남의 Player 속성 · 방 설정 · 방장 자리를 바꿀 수 있다 (`09-network` 13절). 그래서
   * 방장의 속성 쓰기는 `NetworkManager.SetRoomProps` · `SetPlayerProps` · `UpdateRoomOptions` 로만 — 남이 바꾸면 되돌린다. `SetCustomProperties` 를 직접 부르지 않는다.
   * 결과 RPC 는 `NetworkManager.TrustedMaster` 가 보낸 것만 받는다 (`PhotonNetwork.IsMasterClient` 가 아니라).
   * 화면이 속성에서 읽는 값은 범위 · 길이를 확인한다 (남이 잠깐 바꿨을 수 있다).
   * 방 전원 씬 이동은 `NetworkManager.LoadSceneForAll` (PUN 씬 동기화는 끈다).
