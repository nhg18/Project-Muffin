# 버전 규칙

**작성일**: 2026-09-24
**적용 위치**: Player Settings → `Version`(= `Application.version`) · Android `Bundle Version Code` · iOS `Build`
**표시 위치**: 타이틀 좌하단 `ver {Version} · Project ChapChu` ([`systems/12-title-ui.md`](systems/12-title-ui.md))

---

## 1. 형식

`MAJOR.MINOR.PATCH` — 정식 출시 전까지는 `0.x.y`.

| 자리 | 올리는 시점 | 예 |
| --- | --- | --- |
| **MINOR** | 마일스톤 게이트 통과 ([`development-plan.md`](development-plan.md) 3절). M0 = `0.1`, M1 = `0.2` … M5(MVP) = `0.6` | M1 게이트 통과 → `0.2.0` |
| **PATCH** | 같은 마일스톤 안에서 테스트 빌드를 다시 배포할 때 | `0.2.0` → `0.2.1` |
| **MAJOR** | 정식 출시 = `1.0.0` | — |

## 2. 빌드 번호

| 항목 | 규칙 |
| --- | --- |
| Android `Bundle Version Code` | 기기 · 스토어에 올리는 빌드마다 **+1**. 절대 줄이거나 재사용하지 않는다 (스토어가 거부) |
| iOS `Build` | Android 와 같은 숫자를 쓴다 |

## 3. 절차

1. 빌드를 만드는 사람이 Player Settings 에서 `Version` · 빌드 번호를 올린다.
2. `chore: 버전 0.2.0 (code 3)` 형식으로 **버전 변경만 따로** 커밋한다.

## 4. 현재 값

| Version | Bundle Version Code | 근거 |
| --- | --- | --- |
| `0.1.0` | 1 | M0 (계약 확정) 진행 중. 2026-09-24 `1.0` → `0.1.0` 으로 바로잡음 |
