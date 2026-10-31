# 코드 컨벤션

## 1. 이름

| 대상 | 규칙 | 예 |
| --- | --- | --- |
| `private` 필드 | `_` + camelCase | `_health` |
| `[SerializeField] private` 필드 | camelCase | `health` |
| `public` 필드 · 프로퍼티 · `const` · `static readonly` | PascalCase | `MaxHp` |
| 지역 변수 | camelCase | `player` |
| 메서드 · 클래스 · enum | PascalCase, 메서드는 동사로 시작 | `CreateRoom()` |
| 인터페이스 | `I` 접두사 | `IDamageable` |
| 이벤트 | `On` + 과거형, `event` 필수. 발생 함수는 `Raise` + 나머지 | `OnConnected` · `RaiseConnected()` |
| RPC 메서드 | `RPC_` 접두사 | `RPC_RequestPlayCard` |

`var` 는 오른쪽에서 타입이 보일 때만.

## 2. 선언 순서

`public` → `protected` → `private`.
Const → Static → `[SerializeField]` → private 필드 → 프로퍼티 → 이벤트 → Unity 생명주기 → public · protected · private 메서드.

## 3. 주석

public API 는 XML 주석. 그 밖에는 "왜 이렇게 했는지"가 필요할 때만.

## 4. 네임스페이스

루트는 `Chapchu`, 하위는 `Assets/Scripts/` 아래 폴더 경로를 따른다.
흔한 타입을 가리는 이름은 금지 — `Scripts/Debug/`(→ `DebugTools/`), `Scripts/UI/Room/`(Photon `Room` 을 가림), 전역 `enum Type`(→ `CardType`).

## 5. 금지 · 고정

* `Invoke("Method")` · `SendMessage` · 매 프레임 `GameObject.Find`.
* 체력 · 카드 장수 같은 규칙 수치는 `int`.
