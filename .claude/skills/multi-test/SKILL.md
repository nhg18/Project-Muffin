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
```

종료 코드 `0` 통과 · `1` 실패 · `2` 준비 안 됨. 실패하면 출력의 `✗` 줄을 그대로 보고한다.

## 무엇을 언제

| 바꾼 것 | 돌릴 것 |
| --- | --- |
| `Scripts/Game/` 규칙 · 카드 효과 | `rules` → `multi 2` |
| `Scripts/Network/` · RPC | `multi 2` |
| 타이틀 · 로비 · 방 · 팝업 UI | `ui` |
| 기능 하나를 마칠 때 (`development-plan.md` "끝났다는 기준" 은 4클론) | `multi 4` + 그 기능의 기준을 콘솔로 확인 |

평소엔 2클라이언트, 기능을 마칠 때만 4클라이언트 (에디터 4개는 무겁다).

## 시나리오가 하는 일

* **rules** — `GameServer` 를 직접 만들어 `Deck_Default` 로 시작한다. 턴마다 효과 있는 행동 카드를 다음 사람에게 쓰고, 체력이 카드 에셋의 피해량만큼 줄었는지 본다. 마지막에 남의 턴 요청이 거절되는지 본다.
* **ui** — 타이틀(닉네임 · 접속) → 로비 → 없는 방 코드(경고 팝업) → 토스트 → 방 만들기 → 방 로그 → 나가기 → 로비. 콘솔 에러 0 이면 통과. 각 화면의 `[SerializeField]` 누락은 `Awake` LogError 로 잡힌다.
* **multi** — 모두 `DebugLobbyScene` 에서 시작. 메인이 먼저 들어가 방장이 되고, 인원이 차면 시작한다. 한 클라이언트가 남의 턴에 카드를 내 본 뒤(거절), 턴마다 차례인 클라이언트가 행동 카드(없으면 뽑기)를 쓴다. 판정은 클라이언트마다 `[GameDebugInput]` 턴 줄의 "모두의 손패 장수 · 체력" 부분이 순서까지 같은지 · 거절이 1번 이상 · 콘솔 에러 0.

## 사전 조건 · 스크립트가 지키는 것

* 메인 에디터(`MuffinProject/`)가 열려 있어야 한다. 없으면 종료 2 — 사람에게 열어 달라고 한다. 클론(`MuffinProject_clone_N`)은 없으면 스크립트가 연다.
* **저장 안 한 씬이 있으면 아무것도 하지 않고 종료 2.** 사람이 Unity 에서 하던 작업을 덮지 않는다. 씬을 열거나 저장하지 않는다 — 플레이 시작 씬(`playModeStartScene`)만 바꿨다가 되돌린다.
* 시작 전에 에디터마다 `AssetDatabase.Refresh` 와 컴파일 완료를 기다린다. 백그라운드 에디터는 새 파일을 스스로 가져오지 않기 때문이다.
* 끝나면(실패해도) 플레이 중지 · 시작 씬 되돌림 · Run In Background 원래 값.

## 파일

* `run.sh` — 순서 · 대기 · 판정.
* `MultiTest.cs` — 에디터 안에서 도는 진입점 (`unity command script.run --entry MultiTest.Xxx`). 프로젝트 밖이라 빌드에 안 들어간다.
  화면 버튼 대신 컴포넌트의 private 처리 함수를 리플렉션으로 부른다. **코드에서 그 함수 · 필드 이름이 바뀌면 파일 맨 위 "부르는 함수" 표만 고친다.** 이름이 틀리면 `MissingMethodException` 으로 바로 드러난다.
* 새 기능을 확인할 시나리오가 필요하면 `MultiTest.cs` 에 진입점을 더하고 `run.sh` 에 단계를 잇는다. 반환 문자열 규칙: `OK …` 다음 단계 · `WAIT …` 다시 시도 · `SKIP …` 해당 없음 · `FAIL …` 실패.

## 자주 막히는 곳

* **"Could not find RPC with index"** — RPC 를 새로 만들면 `PhotonServerSettings` 의 RpcList 가 메모리에서만 바뀐다. 메인에서 `AssetDatabase.SaveAssetIfDirty` 로 저장하고 클론에서 다시 가져온 뒤 돌린다. 이 파일은 커밋한다.
* **한 단계가 계속 WAIT** — 콘솔을 직접 본다: `unity command console.get --tail 50 --project-path <프로젝트> </dev/null`.
* **컴파일 실패로 종료 2** — 에디터가 Safe Mode 면 CLI 가 붙지 않는다. 컴파일 에러부터 고친다.
* 에디터를 막 열면 Unity Search 색인 에러(`ArgumentOutOfRangeException` … `SearchInit.IndexationOnStartup`)가 뜰 수 있다. 스택에 게임 · Photon 코드가 없는 에러는 "참고 · 에디터 내부 에러" 로만 찍고 판정에서 뺀다.
* 턴은 20초면 저절로 넘어간다. `multi` 는 지금 턴 주인을 읽어서 행동하므로 영향 없지만, 손으로 단계를 이어 칠 때는 빨리 친다.
* 명령은 항상 `</dev/null` 을 붙인다 — 없으면 CLI 가 입력을 기다리며 멈춘다.
