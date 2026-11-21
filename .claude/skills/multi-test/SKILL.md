---
name: multi-test
description: 찹츄 실행 확인 절차. 규칙 · 네트워크 · UI 코드를 바꾼 뒤 "직접 실행해서 확인"할 때, 또는 기능의 "끝났다는 기준"을 볼 때 먼저 읽는다. 열린 Unity 에디터와 ParrelSync 클론을 Unity CLI 로 조종해 규칙 계층 · UI 흐름 · 멀티 플레이를 돌리고 화면끼리 비교한다.
---

# 실행 확인 (멀티 테스트)

AI 가 직접 돌린다. Test Runner · EditMode 테스트는 쓰지 않는다. 확인은 플레이해서 콘솔 로그로 한다.

```bash
bash .claude/skills/multi-test/run.sh rules [인원=4] [턴=8]      # 규칙 계층만 — 네트워크 · 플레이 모드 없음, 몇 초
bash .claude/skills/multi-test/run.sh ui                          # 한 클라이언트 UI 흐름 — 1분
bash .claude/skills/multi-test/run.sh multi [클라이언트=2] [턴=4]  # 메인 + 클론 — 2~3분 (클론을 새로 열면 더)
bash .claude/skills/multi-test/run.sh full [클라이언트=2] [HP=20]   # 한 판 끝까지 — 체력을 낮춰 시작 → 사망 · 게임 종료 → 결과 화면 → 한 판 더 → 로비로
bash .claude/skills/multi-test/run.sh solo [HP=30]                 # 혼자 + 봇 3 — 혼자 하기 → 봇 제거 · 추가 · 난이도 → 한 판 끝까지 → 결과 화면 → 한 판 더 → 로비로
bash .claude/skills/multi-test/run.sh chapchu [클라이언트=2]        # 뽑기만 → 손패 10장 자동 선언 → 두 화면 표시(선언 연출 포함) → 찹츄 승리 → 결과 화면 (4~5분, SHOT_DIR 이 있으면 첫 선언 뒤 스크린샷)
bash .claude/skills/multi-test/run.sh timeout                     # 클론의 턴만 시간 초과 → 자동 뽑기 2번 → 3번째 강제 퇴장 → 클론은 로비 + 경고 팝업 · 메인은 결과 화면
bash .claude/skills/multi-test/run.sh skip                        # 반응 시간의 스킵 버튼 — 방장에게 카운터를 쥐여 줌 → 「스킵」 버튼 켜짐(남은 초 없음) → 누르면 버튼 · 빛 꺼짐 → 그 뒤 카운터 거절(코드 6)
bash .claude/skills/multi-test/run.sh counter cost                # 2021 으르렁 — 위와 같이 낸 뒤 대가: 고르기 판에 내 손패가 카드 칸으로 → 한 칸 누름 → 그 카드가 버려짐 (SHOT_DIR 이 있으면 cost_*.png)
bash .claude/skills/multi-test/run.sh counter                     # 실제 카운터 — 방장에게 2003 진짜 할거야? 를 쥐여 줌 → 클론이 행동 카드 → 방장이 화면에서 카운터 → 막힌 카드는 쓴 사람 손패로 · 두 화면 손패 장수 = 방장 값
bash .claude/skills/multi-test/run.sh migrate [fail|kick|room|result]            # 방장 이주 (09 9.1) — 메인 + 클론 + 봇 1. 방장(메인)이 나감 → 클론이 사본으로 이어받아 봇과 판을 잇는다 · 기록 「방장이 바뀌었어요」. fail = 사본 없이 나감 → 클론은 방으로 + 경고 팝업 · kick = 방장이 3턴 시간 초과로 강제 퇴장 → 이주 · room = 방 대기 중 방장 이탈 → 방 해체 · result = 결과 화면 방장 이탈 → 전원 로비
SHOT_DIR=<폴더> bash .claude/skills/multi-test/run.sh multi 2       # 에디터마다 게임 뷰 PNG — 처음 나온 카드의 3 단계 확대(_zoom) · 반응 시간(_reaction · _later) + 턴이 끝난 뒤. 화면 눈 확인용 (MultiTest.ArmCardShots · Shot)
```

종료 코드 `0` 통과 · `1` 실패 · `2` 준비 안 됨. 실패하면 출력의 `✗` 줄을 그대로 보고한다.

## 무엇을 언제

| 바꾼 것 | 돌릴 것 |
| --- | --- |
| `Scripts/Game/` 규칙 · 카드 효과 | `rules` → `multi 2` (효과를 새로 만들면 `rules` 의 Effects 시나리오에 한 줄 더한다) |
| `Scripts/Network/` · RPC | `multi 2` |
| 방장 이주 · 규칙의 원본 상태 필드를 더함 (`GameServer.Snapshot`) | `rules` (Migrate 시나리오 — 되살린 상태를 필드마다 견준다) → `migrate` · `migrate fail` |
| 타이틀 · 로비 · 방 · 팝업 UI | `ui` |
| 기능 하나를 마칠 때 (`development-plan.md` "끝났다는 기준" 은 4클론) | `multi 4` + 그 기능의 기준을 콘솔로 확인 |
| 사망 · 승리 · 게임 종료 · 방 이동 | `full 2` (기능을 마칠 때 `full 4`) |
| 반응 시간 · 스킵 버튼 (`GameServer.Chain` · `ReactionTimer*` · `HandPresenter`) | `rules` → `skip` → `multi 2` |
| 찹츄 자동 선언 · 찹츄 표시 (내 패널 · 좌석 · 턴 띠 · 선언 연출) | `chapchu 2` |
| 봇 (`Game/Bots` · `BotRoster` · 방의 봇 추가 · 제거) | `rules` (Bot 시나리오) → `solo` |

평소엔 2클라이언트, 기능을 마칠 때만 4클라이언트 (에디터 4개는 무겁다).

## 시나리오가 하는 일

* **rules** — `GameServer` 를 직접 만들어 시험 덱 `Deck_Default` 로 시작한다 (게임 덱은 `Deck_Main` — 카드 효과 시나리오가 그 실제 에셋도 본다). 시계를 직접 움직인다. 턴마다 효과 있는 행동 카드를 다음 사람에게 쓰고, 반응 마감 전엔 그대로 · 마감에 체력이 카드 에셋의 피해량만큼 줄고 · 후처리 시간 뒤 다음 턴인지 본다. 마지막에 남의 턴 요청이 거절되는지 본다.
  이어서 메모리 카드 3인으로 **체인 · 카운터 / 사망 · 빈 덱 피해 · 나감 / 찹츄 · 승리 · 종료 / 함정 / 봇 / 카드 효과 / 판 지표 집계** 시나리오. 판 지표(`Stats`, `07-win-condition` 11-4) = 가한 · 받은 피해(실제로 깎인 만큼 · 자기 피해 · 빈 덱) · 회복 · 킬(최종 사망 때) · 빼앗은 · 빼앗긴 카드 · 드로우 · 행동 · 카운터(성공) · 함정 사용 · 찹츄 선언 · 112 대소동을 넘어 유지 · 종료 요약에 실림 — 지표를 더하거나 세는 곳을 바꾸면 여기에 한 줄 더한다. 카드 효과 = 효과 종류마다 · 새 대상 타입 · 카운터 반응 조건과 아래 카드 표시(반환 · 대상 변경 · 전환 · 감소 · 사망 방지 · 함정 보호) · 「~한 후」 함정 (11-card-list 5-1). 체인 · 카운터: 무반응 처리 · 반응 시간(낼 사람 없으면 0초 · 5초 마감 · 모두 스킵 · 스킵 뒤 카운터 거절 · 고민 연장 1회 · 새 카운터에서 초기화) · 연출 중 카운터 거절 · 무효 · 카운터의 카운터 · 본인만 무효 · 거절 코드 6 ~ 9 · 처리 중 턴 시간 멈춤 · 낸 사람이 나감.
* **ui** — 타이틀(계정 로그인이 저장돼 있으면 자동 로그인, 게스트는 매번 터치 → (처음만 약관 동의) → 게스트 → 닉네임 → 시작 — 실제 UGS · Photon) → 로비 → 없는 방 코드(경고 팝업) → 토스트 → 방 만들기 → 방 로그 → 나가기 → 로비. 콘솔 에러 0 이면 통과. 각 화면의 `[SerializeField]` 누락은 `Awake` LogError 로 잡힌다.
* **multi** — 모두 `DebugLobbyScene` 에서 시작. 메인이 먼저 들어가 방장이 되고, 인원이 차면 시작한다. 한 클라이언트가 남의 턴에 카드를 내 본 뒤(거절), 턴마다 차례인 클라이언트가 행동 카드(없으면 뽑기)를 쓴다. 카드를 낸 턴은 체인 처리 · 후처리(약 13초) 동안 이어지므로 같은 턴에 다시 행동하지 않는다. 처음 카드가 나온 뒤 남의 턴 클라이언트가 반응 시간에 카운터 요청을 한 번 보낸다 — 손패에 카운터 카드가 있으면 **화면 경로**(손패의 그 카드의 사용을 청함, 더블클릭과 같은 길 — `HandPresenter`)로 내고 초록 빛이 켜졌는지도 찍는다, 없으면 요청을 직접 보내 거절(코드 8 — 아무도 낼 카운터가 없어 반응 시간이 0초로 닫힌 뒤면 코드 6). 낸 카드가 거절돼 손패에 남은 턴은 대신 뽑는다(그대로 두면 시간 초과가 쌓여 강제 퇴장). 끝에 모든 화면의 함정 칸 수 · 손패 장수가 방장 값과 같은지 본다(`TableUiExpect`). 이어서 메인이 나를 포함하는 카드의 대상 선택을 열어 내 패널이 가림판 위로 올라와 탭이 닿는지 · 고르면 원래대로 내려가는지 본다(`SelfPickOpen · SelfPickTap · SelfPickDone`, SHOT_DIR 이 있으면 `self_pick_*.png`). 판정은 클라이언트마다 `[GameDebugInput]` 턴 줄의 "모두의 손패 장수 · 체력" 부분이 순서까지 같은지 · 남의 턴 거절(코드 1) · 카운터 판정(코드 6 · 8) · 체인 처리 줄 · 콘솔 에러 0.

* **full** — multi 와 같이 입장 · 시작한 뒤 방장 규칙 원본의 체력을 낮추고(`SetAllHp`), 끝날 때까지 차례인 클라이언트가 행동한다(손패의 함정은 먼저 설치). 함정은 행동이 끝나면 방장이 자동 발동한다. 함정 설치는 **화면 경로**(손패의 함정 카드의 사용을 청함, 더블클릭과 같은 길 — `HandPresenter`)로 하고, 처음 설치한 뒤 모든 화면의 함정 칸 수 · 손패 장수가 방장 값과 같은지(`TableUiExpect`) · 내 칸을 누르면 크게 보이는지(`TrapPreview`) 본다 (SHOT_DIR 이 있으면 `trap_*.png`). 함정을 쓰려면 HP 를 한 방에 죽지 않게(예: 45). 판정: 모든 화면의 「게임 종료 — 유형 · 승자」 가 같음 · 턴 줄이 같음 · **결과 화면**(아래) · 전원 「한 판 더」 → 새 판(방 닫힌 채) → 체력 5 로 빨리 끝냄 → 결과 화면에서 마지막 클라이언트가 「로비로」 → 남은 화면에 「나감」 (2명이면 「한 판 더」 꺼짐 · 45초 마감에 스스로 로비로) → 전원 `DebugLobbyScene` · 콘솔 에러 0.

* **solo** — 메인 혼자 디버그 로비에서 「혼자 하기」(`NetworkManager.CreateSoloRoom`) → 봇 3 · 정원 1 · 비공개 확인 → 봇 2 제거 · 다시 추가(번호 2 재사용) → 봇 칸을 눌러 난이도(봇 1 어려움 · 봇 3 쉬움 — 방장 값 · 방 속성 · 꼬리표 글자와 너비) → 시작 → 봇이 그 난이도로 만들어졌는지 → 체력을 낮춰 끝까지(봇은 방장 기기에서 스스로) → 판정: 게임 종료 · `[BotDriver]` 이유 줄(`7-1 … 점`) · **결과 화면**(아래) · 봇 3 이 스스로 준비 → 「한 판 더」 → 새 판(봇 3 · 난이도 · 비공개 그대로 · 방 닫힌 채) → 체력 5 로 빨리 끝냄 → 결과 화면 「로비로」 → `DebugLobbyScene` · 콘솔 에러 0.

* **결과 화면** (`check_result` — full · solo · chapchu 가 함께 쓴다, `07-win-condition` 11절) — 전원 `ResultScene` · 화면에 그려진 결과 줄(`ResultSummary`: 제목 · 승리 방식 알약 · 순위 순으로 순위 · 이름 · HP · 탈락 줄 · 당한 사람 · 왕관 · 찹츄 마크 · 업적 배지 + 「판 기록」 의 지표 줄마다 값(1등은 `*`) → 업적)이 모든 화면에서 같음 · 공개 HP 가 시작 값(`ExpectHpReset`) · 방이 닫힌 채(`ExpectRoomClosed`). 준비 · 나감 · 버튼 글자는 `ResultState`, 버튼은 `ResultTap again|lobby|board`. 이어서 **이모트**(메인이 `ResultEmote 0` → 모든 화면의 말풍선 기록 `ArmBubbleLog` · `BubbleLog` 에 「메인 이름:GG」) · **판 기록**(`ResultTap board` → `ResultBoard close` 로 ✕) 을 본다 (`check_result` 셋째 인자 — solo 는 첫 결과 화면, full 은 둘째, chapchu 는 마지막. full 의 첫 결과 화면은 판정 확인 뒤 「한 판 더」 까지 45초 자동 이동 안에 눌러야 해서 뺐다). full 은 「한 판 더」 뒤 다른 사람의 「준비!」 말풍선도 본다. SHOT_DIR 이 있으면 `result*_<에디터>.png` · `result*_emote_*` · `result*_board_*` + 등장 연출(색종이) `result_intro_<에디터>_<초>.png` (`ArmResultShots` — 게임 중에 미리 걸어 둔다). 게임 끝 알림 뒤 2초면 결과 씬으로 넘어가므로 끝을 기다리는 반복은 「게임 화면 아님 (ResultScene)」 도 끝으로 본다.

* **BotArena** (run.sh 에 없는 진입점 — 봇 값을 맞출 때) — `unity command script.run --file <MultiTest.cs> --entry MultiTest.BotArena --args '["NEEE","200"]'`. 자리마다 난이도(E 쉬움 · N 보통 · H 어려움)로 봇끼리 게임 덱 N판 → 난이도별 승률 · 승리 방식 · 판 길이 · 카드별 사용 (`18-bot` 7-7 「난이도 확인」). 통과 · 실패 없이 숫자만 낸다. `rules` 의 Bot 시나리오 ⑤ 가 같은 방법으로 「보통 한 자리 승률: 쉬움 셋 사이 > 어려움 셋 사이」 를 본다.

## 사전 조건 · 스크립트가 지키는 것

* 메인 에디터(`MuffinProject/`)가 열려 있어야 한다. 없으면 종료 2 — 사람에게 열어 달라고 한다. 클론(`MuffinProject_clone_N`)은 없으면 스크립트가 연다.
* **저장 안 한 씬이 있으면 아무것도 하지 않고 종료 2.** 사람이 Unity 에서 하던 작업을 덮지 않는다. 씬을 열거나 저장하지 않는다 — 플레이 시작 씬(`playModeStartScene`)만 바꿨다가 되돌린다.
* 시작 전에 에디터마다 `AssetDatabase.Refresh` 와 컴파일 완료를 기다린다 (`MultiTest.cs` 를 부르기 전 — 새 API 를 쓰는 시험이 옛 코드로 컴파일되지 않게). 백그라운드 에디터는 새 파일을 스스로 가져오지 않기 때문이다.
* 끝나면(실패해도) 플레이 중지 · 시작 씬 되돌림 · Run In Background 원래 값.

## 파일

* `run.sh` — 순서 · 대기 · 판정.
* `MultiTest.cs` — 에디터 안에서 도는 진입점 (`unity command script.run --entry MultiTest.Xxx`). 프로젝트 밖이라 빌드에 안 들어간다.
  화면 버튼 대신 컴포넌트의 private 처리 함수를 리플렉션으로 부른다. **코드에서 그 함수 · 필드 이름이 바뀌면 파일 맨 위 "부르는 함수" 표만 고친다.** 이름이 틀리면 `MissingMethodException` 으로 바로 드러난다.
* 새 기능을 확인할 시나리오가 필요하면 `MultiTest.cs` 에 진입점을 더하고 `run.sh` 에 단계를 잇는다. 반환 문자열 규칙: `OK …` 다음 단계 · `WAIT …` 다시 시도 · `SKIP …` 해당 없음 · `FAIL …` 실패. `script.run` 은 부를 때마다 새로 컴파일돼 static 이 남지 않는다 — 부름 사이에 남길 값은 `SessionState` 에.

## 자주 막히는 곳

* **"Could not find RPC with index"** — RPC 를 새로 만들면 `PhotonServerSettings` 의 RpcList 가 메모리에서만 바뀐다. 메인에서 `AssetDatabase.SaveAssetIfDirty` 로 저장하고 클론에서 다시 가져온 뒤 돌린다. 이 파일은 커밋한다.
* **한 단계가 계속 WAIT** — 콘솔을 직접 본다: `unity command console.get --tail 50 --project-path <프로젝트> </dev/null`.
* **컴파일 실패로 종료 2** — 에디터가 Safe Mode 면 CLI 가 붙지 않는다. 컴파일 에러부터 고친다.
* 에디터를 막 열면 Unity Search 색인 에러(`ArgumentOutOfRangeException` … `SearchInit.IndexationOnStartup`)가 뜰 수 있다. 스택에 게임 · Photon 코드가 없는 에러는 "참고 · 에디터 내부 에러" 로만 찍고 판정에서 뺀다.
* 턴은 15초면 방장이 대신 뽑고 넘어간다 — 3번 연속이면 그 사람이 강제 퇴장된다 (봇 제외, `03-turn` 4절). `ExpireTurn` 은 기본이 「그냥 넘김」 이라 횟수를 세지 않는다 (`auto` · `others` 가 실제 시간 초과). `multi` 는 지금 턴 주인을 읽어서 행동하므로 영향 없지만, 손으로 단계를 이어 칠 때는 빨리 친다.
* 명령은 항상 `</dev/null` 을 붙인다 — 없으면 CLI 가 입력을 기다리며 멈춘다.
