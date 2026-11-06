# 코드 컨벤션

목표: 누가 이어받아도 읽고 · 값을 바꾸고 · 조합해서 새 기능을 만들 수 있는 코드.
이 문서가 기준이다. 코드가 다르면 코드를 고친다. 새 규칙은 여기에만 적는다.

## 1. 이름

| 대상 | 규칙 | 예 |
| --- | --- | --- |
| 클래스 · 메서드 · 프로퍼티 · enum · `const` · `static readonly` | PascalCase. 메서드는 동사로 시작 | `CreateRoom()` · `MaxHp` |
| `private` 필드 | `_` + camelCase | `_health` |
| `[SerializeField] private` 필드 | camelCase (Inspector 에 그대로 보인다) | `health` |
| 지역 변수 · 매개변수 | camelCase | `player` |
| 인터페이스 | `I` 접두사 | `IDamageable` |
| 이벤트 | `On` + 일어난 일(과거형) 또는 `On` + 요청 + `Requested` | `OnJoinedRoom` · `OnStartRequested` |
| 이벤트 구독 함수 | `Handle` + 이벤트 이름에서 `On` 을 뺀 것 | `HandleJoinedRoom` |
| 이벤트 발생 함수 (다른 클래스가 발생시킬 때만) | `Raise` + 이벤트 이름에서 `On` 을 뺀 것 | `RaiseJoinedRoom()` |
| RPC 메서드 | `RPC_` + 동사. 방장에게 보내는 요청은 `RPC_Request` + 동사 | `RPC_RequestPlayCard` · `RPC_ApplyDamage` |
| 코루틴 | 동사 + `Routine` | `CountdownRoutine()` |
| 카드 연출 이펙트 컴포넌트 (화면 연출) | 종류 + `Fx` — 카드 규칙 효과(`…Effect`)와 구분 | `FlashFx` · `SlashFlurryFx` |
| 파일 | 클래스 이름과 같게, 영문. 작은 enum · struct 는 쓰는 클래스 파일에 둬도 된다 | `RoomPresenter.cs` |

* `var` 는 오른쪽에서 타입이 바로 보일 때만 (`new X()` · `Load<X>()`). 메서드 반환값은 타입을 쓴다.
* 축약하지 않는다 (`mgr` ✗ → `manager`). 약어는 두 글자까지만 대문자 (`UI` · `Rpc` · `Hp`).

## 2. 파일 구조

* 네임스페이스 루트는 `Chapchu`, 하위는 `Assets/Scripts/` 아래 폴더 경로. 흔한 타입을 가리는 폴더 이름 금지 (`Debug` → `DebugTools`, `UI/Room` → `UI/RoomScreen`).
* 에디터 전용 스크립트는 `Scripts/Editor/` (Unity 특수 폴더라 이름을 바꿀 수 없다). 네임스페이스는 `Chapchu.EditorTools` — `UnityEditor.Editor` 를 가리지 않게.
* 선언 순서: `const` → `static` → `[SerializeField]` → private 필드 → 프로퍼티 → 이벤트 → Unity 생명주기(실행 순서대로) → public → protected → private 메서드.
* 중괄호는 줄을 바꿔 연다(Allman). 들여쓰기 4칸. 한 줄에 한 문장. 조건이 하나면 early return.
* `.cs` 는 UTF-8 BOM (hook 이 붙인다).

## 3. 주석

* 클래스 요약(`/// <summary>`)에 하는 일 한 줄과 기준 문서(`docs/systems/NN 절`)를 적는다.
* public 멤버는 XML 주석. 그 밖에는 "왜 이렇게 했는지"가 필요할 때만. 무엇을 하는지는 코드가 말한다.
* 주석과 로그는 한국어.

## 4. 컴포넌트

* 기능 하나 = 컴포넌트 하나. 씬이나 프리팹에 붙여서 쓸 수 있는 단위로 만든다.
* 다른 컴포넌트는 `[SerializeField]` 로 받는다. 같은 오브젝트면 `[RequireComponent]` + `GetComponent`(Awake 에서 1회). `Find` 계열은 초기화 때 1회만, 매 프레임 금지.
* 필수 참조가 비어 있으면 `Awake` 에서 `Debug.LogError` 로 어느 참조인지 알린다. 조용히 넘어가지 않는다.
* 생명주기: `Awake` 자기 초기화 · `OnEnable`/`OnDisable` 이벤트 구독/해제(`+=` 와 `-=` 는 항상 짝) · `Start` 다른 오브젝트 참조. `OnEnable` 에서 다른 `Instance` 에 접근하지 않는다.
* 필드는 private, 밖에 보일 것은 읽기 전용 프로퍼티나 메서드로 연다. 순수 데이터(ScriptableObject · struct) 는 public 필드로 둬도 된다.
* 싱글톤은 `Core/Singleton.cs` · `Core/SingletonPun.cs` 를 상속한다. 싱글톤 `Instance` 와 `Network/Events/` 의 static 이벤트 외에 `static` 변경 가능 상태는 두지 않는다.
* UI 는 화면 영역 하나에 컴포넌트 하나. 한 파일이 길어지면 화면을 그리는 코드와 네트워크 · 규칙을 아는 코드로 나눈다.

## 5. 수치와 문자열

* 규칙 수치(체력 · 장수 · 초)는 `int` 상수 또는 ScriptableObject. 상수에는 기준 문서 절을 주석으로 남긴다. 코드 가운데 숫자를 바로 쓰지 않는다.
* 연출값(시간 · 색 · 거리)과 참조는 `[SerializeField]` 로 Inspector 에서 바꾼다. 값 필드에는 `[Tooltip]` 한 줄 — 코드를 열지 않고 Inspector 에서 뜻을 알 수 있게.
* 문자열 키(씬 이름 · PlayerPrefs · CustomProperties · Resources 경로)는 상수 클래스에 모은다 (`ScenePaths` · `PlayerPrefsKeys` · `ResourcePaths`).
* 카드별 연출 프리팹은 `Resources/CardFx/{연출 컴포넌트 이름}.prefab` 에 두고 `CardFxLoader` 로 이름으로 불러온다. 씬 · 프리팹에서 직접 참조하지 않는다 — 참조하면 그 씬을 열 때 그림까지 메모리에 올라온다 (10-ui 5-1).
* 사람에게 보이는 문구는 클래스마다 한 곳(문구를 돌려주는 함수 또는 상수)에 모은다. 로직 사이에 흩어 두지 않는다.

## 6. 네트워크

* 게임 규칙 판정은 방장만. 참가자는 `RPC_Request*` 로 요청하고, 방장이 판정해 결과를 모두에게 보낸다 → skill `network-rpc`.
* RPC 이름은 `nameof(RPC_Xxx)`. 문자열로 쓰지 않는다 (hook 이 막는다).
* 네트워크 콜백은 `Network/Events/` 의 static 이벤트로 바꿔 흘린다. UI 는 Photon 을 직접 구독하지 않는다.

## 7. 시간과 로그

* 지연 · 연출은 코루틴 또는 DOTween. `async void` 금지 (hook 이 막는다). `Invoke("Method")` · `SendMessage` 금지.
* 로그는 `Debug.Log($"[{nameof(ClassName)}] ...")`. 어느 클라이언트인지 알아야 할 때는 닉네임을 넣는다. `Update` 안에서는 로그하지 않는다.
* 예외를 빈 `catch` 로 삼키지 않는다.

## 8. C# 버전

C# 9 까지. file-scoped namespace · `record struct` · `global using` 등 C# 10+ 문법은 hook 이 막는다.

## 9. 게임 규칙 계층

게임 규칙(검증 · 상태 변경)은 Unity · Photon 을 모르는 순수 C# 으로 만들고, 방장만 실행한다.

* 위치는 `Scripts/Game/`. 어셈블리를 따로 둔다 (`Chapchu.Game.asmdef`, `noEngineReferences: true`, 참조 없음). UnityEngine · Photon · UI 를 쓰면 컴파일이 안 된다.
* 게임 상태(덱 · 손패 · 버림 더미 · HP · 처리 체인 · 처리 ID)는 객체 하나에 모은다. 방장 이주(`09-network` 9절)를 나중에 붙일 수 있게 하기 위해서다.
* 규칙은 요청을 검증하고, 상태를 바꾸고, **일어난 일 목록**을 돌려준다. 누구에게 무엇을 보낼지(전체 · 소유자만)는 네트워크 계층이 정한다.
* 난수와 시간은 밖에서 받는다. 반응 · 턴 타이머는 네트워크 계층이 재고 만료를 규칙에 알린다.
* 카드 효과는 `Game` 의 `[Serializable]` 순수 C# 클래스로 만들고, 카드 정의 ScriptableObject(`Scripts/Cards/`)에 `[SerializeReference]` 목록으로 조립한다. 카드마다 클래스를 만들지 않는다. 효과 추가 메뉴는 에디터 스크립트로 만든다.
  * 효과 클래스는 `Scripts/Game/Effects/` 에 `종류 + Effect` 이름으로 (`DamageEffect`). 에셋에 클래스 이름 · 네임스페이스로 저장되므로 만든 뒤에는 바꾸지 않는다 — 바꾸면 그 효과가 에셋에서 빈다.
  * 효과는 같은 종류의 카드 여러 장이 한 인스턴스를 같이 쓴다. 실행 중에 필드를 바꾸지 않는다.
* 네트워크로는 카드 ID · 인스턴스 ID(`int`)만 보낸다. ScriptableObject 에 실행 중 상태를 저장하지 않는다.
* 클라이언트는 받은 일을 연출 큐에 넣고 하나씩 재생한다. 화면 값은 재생하면서 이벤트에 담긴 값으로 바꾼다.
  * 큐는 화면마다 하나(`GameScreenPresenter`). 큐만 `GameEvents` 를 구독하고, 영역 프레젠터는 큐가 부르는 함수(`Show*` 즉시 · `*Routine` 연출)만 둔다. 받은 순서가 곧 일어난 순서라 큐는 순서를 바꾸지 않는다.
  * 큐를 타지 않는 것: 내 요청의 거절(바로 토스트) · 매 프레임 계산하는 값(남은 초) · 늦게 열렸을 때 CustomProperties 로 복구하는 초기값(각 프레젠터 `Start`).
