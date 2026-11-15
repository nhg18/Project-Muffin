# 버전 규칙

`MAJOR.MINOR.PATCH` — Player Settings `Version`. 타이틀 좌하단에 표시된다.

| 자리 | 올리는 시점 |
| --- | --- |
| MINOR | 점검 빌드마다 (`0.n.0`) |
| PATCH | 같은 점검 안에서 테스트 빌드를 다시 배포할 때 |
| MAJOR | 정식 출시 = `1.0.0` |

* Android `Bundle Version Code` 는 기기 · 스토어에 올리는 빌드마다 +1, 재사용 금지. iOS `Build` 도 같은 숫자.
* 버전 변경은 따로 커밋한다: `chore: 버전 0.2.0 (code 3)`.
* 버전이 다른 앱끼리는 매칭되지 않는다 — 접속할 때 이 값을 Photon `GameVersion` 으로 보낸다 (`PhotonConnection.Connect`). PATCH 만 달라도 나뉜다 (2026-11-12).
* 현재: `0.1.0` · code 1.
