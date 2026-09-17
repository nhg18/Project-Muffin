# Project-Muffin

**찹츄 : 치와와개판오분전언찹츄찹찹츄**
울산대학교 GG동아리에서 개발하는 2~4인 모바일 파티 카드 대전 게임 (Unity 2022.3 / Photon PUN2)

---

## 문서

| 문서 | 내용 |
| --- | --- |
| [CLAUDE.md](CLAUDE.md) | **개발 지침** — 코드를 쓰기 전에 읽는다 |
| [docs/systems/](docs/systems/) | **시스템 기획서 — 코드 작성의 기준 문서** |
| [docs/refactoring-plan.md](docs/refactoring-plan.md) | 현재 코드 문제점 진단 + 리팩토링 계획 |
| [docs/CODE_CONVENTION.md](docs/CODE_CONVENTION.md) | 코드 컨벤션 |
| [docs/GDD_GUIDE.md](docs/GDD_GUIDE.md) | 기획 문서 작성 지침 |

> ⚠ **게임 규칙과 수치의 기준은 노션이 아니라 `docs/systems/` 입니다.**
> 노션에서 결정된 내용은 `docs/systems/`에 반영된 뒤에 구현 대상이 됩니다.

## 프로젝트 구조

```
MuffinProject/           Unity 프로젝트 루트
  Assets/Scripts/        자체 코드
  Assets/Cards/          카드 ScriptableObject
  Assets/Scenes/         씬
docs/                    문서
```
