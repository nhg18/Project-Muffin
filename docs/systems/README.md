# 시스템 기획서

코드를 작성할 때의 기준 문서다. 노션이 아니다. 작성 규칙은 [`../GDD_GUIDE.md`](../GDD_GUIDE.md).

| 파일 | 시스템 | 분류 |
| --- | --- | --- |
| [00-overview.md](00-overview.md) | 개요 · 용어 · 표기 | — |
| [01-game-flow.md](01-game-flow.md) | 게임 흐름 · 씬 | Core |
| [02-player.md](02-player.md) | 플레이어 · 좌석 | Core |
| [03-turn.md](03-turn.md) | 턴 | Core |
| [04-card.md](04-card.md) | 카드 처리 (사용 · 체인 · 반응) | Core |
| [05-deck.md](05-deck.md) | 덱 · 손패 · 버림 더미 | Core |
| [06-health.md](06-health.md) | 체력 · 사망 | Core |
| [07-win-condition.md](07-win-condition.md) | 승리 (찹츄 · 최후 생존) | Core |
| [08-room.md](08-room.md) | 방 · 로비 규칙 | MVP |
| [09-network.md](09-network.md) | 네트워크 권한 · 동기화 | Core |
| [10-ui.md](10-ui.md) | 인게임 UI | Core |
| [11-card-list.md](11-card-list.md) | 카드 목록 (행동 27 · 카운터 15 · 함정 17) | Core |
| [12-title-ui.md](12-title-ui.md) | 타이틀 화면 | MVP |
| [13-friend.md](13-friend.md) | 친구 | Optional |
| [14-lobby-ui.md](14-lobby-ui.md) | 로비 화면 | MVP |
| [15-screen.md](15-screen.md) | 화면 규격 (방향 · 해상도 · 스케일) | MVP |
| [16-room-ui.md](16-room-ui.md) | 방 화면 | MVP |

**문서가 없는 시스템**: 인게임 화면 배치 · 로그인 · 재접속 · 설정 / 프로필.

## 전역 미정

| # | 항목 | 문서 |
| --- | --- | --- |
| 1 | 연속 미제출 강제 퇴장 턴 수 N (2~3) | 03 |
| 2 | 플레이어 속성표의 이름 없는 4개 항목 | 02 |
| 3 | 재접속 허용 시간 · 좌석 · 손패 복원 | 02 · 08 |
| 4 | 로그인 전체 | 없음 |
| 5 | 카드별 수치 N | 11 |
| 6 | 덱 구성 (카드별 매수 · 총 장수) | 05 |
| 7 | 턴 방향 (A07 "방향 반전") | 03 |
| 8 | 타이틀 사운드 · 메뉴 버튼 동작 | 12 · 14 |
