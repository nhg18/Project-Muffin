# 코드 컨벤션 (Code Convention)

> 출처: 노션 「코드 컨벤션(Code Convention)」 페이지를 저장소로 이관한 문서.
> 이후 변경은 **이 파일**을 기준으로 한다.

---

## 1. 명명 규칙

### 필드 변수

| 접근 지정자 | 규칙 | 예시 |
| --- | --- | --- |
| `private` | `_` + camelCase | `_health`, `_player` |
| `[SerializeField] private` | camelCase | `health`, `player` |
| `public` | PascalCase | `Health`, `Player` |
| `const` | PascalCase | `MaxHp`, `DefaultSpeed` |
| `static readonly` | PascalCase | `DefaultColor` |

### 프로퍼티

PascalCase.

```csharp
public int Hp { get; set; }
public string Nickname { get; }
```

### 함수 내 지역 변수

camelCase.

### var 사용

타입이 명확할 때만 사용한다.

```csharp
var player = new Player();                          // OK
Dictionary<string, Player> players = new();         // 타입 불명확 → 명시
```

### 메서드

PascalCase. 영어 동사로 시작하고 기능을 명확히 표현한다. 하나의 역할만 수행한다. 필요 이상으로 분리하지 않는다.

```csharp
public void Connect()
public void CreateRoom()
private void Initialize()
```

### 클래스 / Enum

PascalCase.

### 인터페이스

`I` 접두사. `INetwork`, `ISaveSystem`, `IDamageable`

### 이벤트

`On` + 과거완료형. `event` 키워드 필수.

```csharp
public event Action OnConnected;
public event Action<int> OnHealthChanged;
```

필요 시 `Invoke`를 감싸는 public 함수는 `Raise` + On을 뺀 형태.

```csharp
public void RaiseConnected() => OnConnected?.Invoke();
public void RaiseHealthChanged(int health) => OnHealthChanged?.Invoke(health);
```

---

## 2. 선언 순서

접근 지정자: `public` → `protected` → `private`

클래스 멤버 순서:

```
Const
Static Field
[SerializeField] private
Private Field
Property
Event

Unity LifeCycle
Public Method
Protected Method
Private Method
```

---

## 3. 주석

* **Public API**: XML 주석 작성.
* **Private 함수**: 특별한 이유가 없으면 작성하지 않는다.
* **구현 설명**: 복잡한 알고리즘이나 의도를 설명할 때만. "무엇을 하는지"보다 **"왜 이렇게 구현했는지"** 를 쓴다.

```csharp
// Photon의 콜백은 비동기로 호출되므로
// 연결 완료는 ConnectionCallback에서 처리한다.
```

---

## 4. 이 프로젝트 추가 규칙

### 4.1 인코딩

모든 `.cs` 파일은 **UTF-8 with BOM**. 한글 주석/문자열이 `��` 로 보이면 인코딩이 CP949로 저장된 것이다.

### 4.2 네임스페이스

모든 스크립트는 네임스페이스를 갖는다. 루트는 `Chapchu`.

> 2026-09-23 게임 이름(찹츄)에 맞춰 `Muffin` → `Chapchu` 로 일괄 변경했다.
> 저장소·Unity 프로젝트 폴더 이름(`Project-Muffin`, `MuffinProject`)은 그대로 둔다.

```
Chapchu.Core         공통 인프라 (싱글톤, 부트스트랩, 씬 경로, 상수)
Chapchu.Network      Photon 연결 / 룸 / 네트워크 이벤트
Chapchu.Game         인게임 규칙 (턴, 덱, 손패, 체력, 승리)
Chapchu.Game.Cards   카드 데이터 / 효과 / 조건
Chapchu.Presentation 인게임 표시 (좌석, 손패·카드 뷰, 입력)
Chapchu.UI           로비 / 방 / 팝업 UI
Chapchu.DebugTools   디버그 전용
```

네임스페이스는 `Assets/Scripts/` 아래 폴더 경로를 그대로 따른다.

`Type`, `Deck`, `Card` 처럼 일반적인 이름을 전역 네임스페이스에 두지 않는다.
(`public enum Type`은 `System.Type`을 가려서 버그를 만든다. `CardType`으로 쓴다.)

**폴더 이름에도 같은 규칙이 적용된다.** 폴더명이 곧 네임스페이스가 되므로,
자주 쓰는 타입과 이름이 같은 폴더를 만들면 그 타입이 가려진다.

| 금지 | 이유 |
| --- | --- |
| `Scripts/Debug/` | `Chapchu.Debug` 가 `UnityEngine.Debug` 를 가린다 → `DebugTools/` 를 쓴다 |
| `Scripts/UI/Room/` | `Chapchu.UI.Room` 이 `Photon.Realtime.Room` 을 가린다 (CS0118) |

### 4.3 RPC

```csharp
photonView.RPC(nameof(RPC_RequestPlayCard), RpcTarget.MasterClient, cardInstanceId);
```

* 문자열 리터럴 금지.
* RPC 메서드명은 `RPC_` 접두사.
* 마스터 전용 RPC는 **첫 줄에 반드시 가드**를 넣는다.

```csharp
[PunRPC]
private void RPC_RequestPlayCard(int cardInstanceId, PhotonMessageInfo info)
{
    if (!PhotonNetwork.IsMasterClient) return;
    ...
}
```

### 4.4 문자열 키

Photon CustomProperties 키는 리터럴로 쓰지 않고 상수 클래스에 모은다.

```csharp
public static class PlayerProps
{
    public const string Hp = "hp";
    public const string HandCount = "handCount";
}
```

### 4.5 금지

* `async void` (이벤트 핸들러 포함)
* `[SerializeField] static`
* `Invoke("MethodName", ...)` / `SendMessage`
* 클래스별 자체 싱글톤 구현 (`Core/Singleton.cs` 사용)
* `GameObject.Find` 계열의 매 프레임 호출

### 4.6 수치 타입

HP와 카드 장수 등 게임 규칙 수치는 **`int`** 로 통일한다. `float` HP 금지.
