# UI 코드 리팩토링 플랜 (인게임 제외)

**작성일**: 2026-09-23
**범위**: `Assets/Scripts/UI/**` + UI가 직접 건드리는 `Core` / `Network` 경계
**제외**: `GameScene` 및 `Assets/Scripts/Presentation/**` (좌석·손패·카드·덱·턴·입력) — 별도 진행
**관계 문서**: [`refactoring-plan.md`](refactoring-plan.md) Phase 6(UI) 자리에 들어간다 · [`ui-plan.md`](ui-plan.md) 의 선행 작업

---

## 0. 결론 먼저

검증 결과 **UI 코드의 문제는 대부분 "없는 기능"이 아니라 "있는데 연결이 끊긴 것"** 이다.
팝업 시스템은 세 군데가 끊겨 있어 **토스트를 띄울 수 없고**, 씬을 넘어가면 **모달 껍데기가 화면에 남는다.**

| 분류 | 건수 | 대표 |
| --- | --- | --- |
| 🔴 동작 결함 (지금 깨져 있음) | 4 | 토스트 사용 불가, 모달 잔재, 실패 시 무반응 |
| 🟠 사용자가 막히는 문제 | 5 | 접속 실패 시 무한 대기, 죽은 랜덤매치 버튼 |
| 🟡 규약 위반 · 정리 | 14 | `async void`, 죽은 스크립트 5개, 네임스페이스 충돌 |

**작업 순서는 6개 PR이다. PR1(팝업 인프라)을 먼저 하지 않으면 나머지 전부가 임시 처리로 끝난다** — 실패 안내·로딩 표시·거절 사유가 전부 팝업 위에 올라가기 때문이다.

### 검증 방법

코드뿐 아니라 **씬·프리팹의 직렬화 필드와 `Resources` 프리팹 루트 컴포넌트까지 확인**했다.
"필드는 연결돼 있는데 코드가 안 쓴다"(U-5), "클래스는 있는데 프리팹에 컴포넌트가 없다"(U-3) 같은 건 코드만 봐서는 안 보인다.
죽은 스크립트 판정은 `.meta` GUID를 모든 `.unity` / `.prefab` 에서 역검색해 참조 0을 확인했다.

---

## 1. 동작 결함 (🔴 / 🟠)

### U-1 🔴 씬 전환 시 모달 팝업이 화면에 남는다

**위치**: `UI/Popup/PopupManager.cs` `CloseAllModals()`

```csharp
while (_modalStack.Count > 0)
    Destroy(_modalStack.Pop());          // ← .gameObject 누락
```

`CloseAllNonModals` / `CloseAllToasts` 는 `.gameObject` 를 넘기는데 모달만 **컴포넌트를 지운다.**
`CloseAllPopups` 는 `SceneManager.sceneLoaded` 에 물려 있으므로 **모달을 띄운 채 씬을 넘기면** 스크립트만 사라진 유령 UI가 남는다. 버튼도 안 먹고 닫을 방법도 없다.

**수정**

```csharp
while (_modalStack.Count > 0)
    Destroy(_modalStack.Pop().gameObject);
RefreshBlocker();
```

---

### U-2 🔴 `Popup.Open()` / `Close()` 를 아무도 호출하지 않는다

**위치**: `UI/Popup/PopupManager.cs` `OpenModal` / `OpenNonModal` / `OpenToast` / `Close*`

세 Open 메서드 모두 `Instantiate` 만 하고 `Open()` 을 부르지 않는다.
그래서 `Popup.OnOpen()` / `OnClose()` 훅 **전체가 죽은 설계**이고, 특히 `ToastPopup.OnOpen()` 의 자동 닫힘 코루틴이 시작되지 않아 **토스트가 영원히 안 닫힌다.**

**수정**: Open 계열 마지막에 `popup.Open();`, Close 계열에서 `Destroy` 직전에 `popup.Close();`

```csharp
public T OpenToast<T>(T prefab) where T : ToastPopup
{
    var popup = Instantiate(prefab, toastLayer);
    _toastList.Add(popup);
    popup.Open();                 // ← 추가
    return popup;
}
```

---

### U-3 🔴 토스트는 애초에 띄울 수 없다

**위치**: `UI/Popup/ToastPopup.cs`, `Resources/Popups/ToastPopup.prefab`

* `ToastPopup` 은 **abstract** 이고 이를 상속한 클래스가 프로젝트에 **없다.**
* `ToastPopup.prefab` 루트의 컴포넌트는 `RectTransform / CanvasRenderer / Image / Button / (기타)` 이고 **`ToastPopup` 계열 스크립트가 붙어 있지 않다.** (GUID `dde3dfac…` 참조처 0)
* 따라서 `PopupManager.Get<ToastPopup>()` → `Resources.Load` 가 `null` → `OpenToast(null)` 에서 예외.

`systems/10-ui.md` 8절은 이걸 **"구현됨"** 이라고 적고 있다. 문서 쪽을 정정해야 한다.

**수정**

```csharp
// UI/Popups/MessageToast.cs (신규)
namespace Chapchu.UI.Popups
{
    public class MessageToast : ToastPopup
    {
        [SerializeField] private TMP_Text messageText;

        public string Message { set => messageText.text = value; }
    }
}
```

프리팹 루트에 `MessageToast` 를 붙이고 파일명을 `Resources/Popups/MessageToast.prefab` 로 맞춘다
(`Get<T>()` 가 `Popups/{타입명}` 규약이므로 **이름이 곧 계약**이다 — U-20).

> M2의 "카드 사용 거절 사유 표시", M1의 "방 참가 실패 안내"가 전부 이 위에 올라간다. **가장 먼저 고친다.**

---

### U-4 🟠 방 생성 / 참가 실패 시 아무 일도 일어나지 않는다

**위치**: `UI/LobbyPanel.cs`

```csharp
private void OnRoomCreateFailed(short code, string message) { }   // 비어 있음
private void OnJoinRoomFailed(short code, string message)   { }   // 비어 있음
```

없는 방 코드를 입력하면 화면이 그대로 멈춘다. 입력 팝업도 닫히지 않고 안내도 없다.

**수정**: `WarningPopup`(치명) / `MessageToast`(경미) 로 사유 표시 + 입력 팝업 정리 + 버튼 복구.
사유 문자열은 Photon 원문(`message`)을 그대로 노출하지 말고 매핑한다.

| `returnCode` | 표시 |
| --- | --- |
| `GameDoesNotExist`(32758) | "방을 찾을 수 없습니다." |
| `GameFull`(32765) | "방이 가득 찼습니다." |
| `GameClosed`(32764) | "이미 시작된 방입니다." |
| 그 외 | "참가에 실패했습니다. 잠시 후 다시 시도해주세요." |

---

### U-5 🟠 랜덤 매치 버튼이 죽어 있다

**위치**: `UI/LobbyPanel.cs` / `LobbyScene`

`randomMatchButton` 은 씬에서 **할당돼 있지만**(`fileID: 1168191721`) `OnEnable` 에서 리스너를 붙이지 않는다. 눌러도 아무 반응이 없다.

**수정**: `NetworkManager.JoinRandomRoom()` 연결. `PhotonRoom.OnJoinRandomFailed` 가 이미 `CreateRoom()` 폴백을 갖고 있어 동작은 바로 된다.
**기획상 랜덤 매치를 MVP에서 뺄 거라면 버튼을 숨긴다.** 지금처럼 "보이는데 안 눌리는" 상태가 가장 나쁘다.

---

### U-6 🟠 접속 실패 시 타이틀에서 무한 대기

**위치**: `UI/NickName/NicknameInput.cs` + `Network/PhotonConnection.cs`

* `NicknameInput` 은 `ConnectionEvents.OnConnected` 만 구독한다. **`OnDisconnected` 구독이 없다.**
* 제출 버튼은 접속 완료 전까지 비활성이므로, 접속에 실패하면 **버튼이 영원히 비활성인 채 안내도 없다.**
* `PhotonConnection.Initialize()` 는 인터넷이 없으면 팝업 주석만 남기고 조기 반환하고, `OnDisconnected` 의 분기는 **전부 주석 처리**돼 있다.

**수정(UI 측)**: `ConnectionEvents.OnDisconnected` 구독 → 인라인 에러 문구 + "재시도" 버튼(→ `NetworkManager.Connect()`).
**네트워크 측**(A 담당): 조기 반환 시 `AutomaticallySyncScene` 설정 누락 문제와 함께 처리 — `development-plan.md` 6절 #1·#2 와 같은 건이다.

---

### U-7 🟠 저장된 닉네임이 복원되지 않는다

**위치**: `Network/PhotonConnection.SetupInitNickname()` — `private` 이고 **호출처 0 (죽은 코드)**

`SetNickname` 은 `PlayerPrefs` 에 저장까지 하는데, 읽어서 입력창을 채우는 코드가 어디에도 없다.

**수정**: 타이틀 프레젠터(U-22)의 초기화에서 `PlayerPrefsKeys.PlayerName` 을 읽어 입력창에 채우고 카운터를 갱신한다. `SetupInitNickname()` 은 삭제한다(A 소유 파일 → 요청).

---

### U-8 🟡 닉네임 검증이 노션 스펙과 다르다

**위치**: `UI/NickName/NicknameValidator.cs`

| 노션 8절 | 현재 코드 |
| --- | --- |
| 앞뒤 공백 Trim | **안 함** — 원문 그대로 검증 |
| 중간 연속 공백 1칸 축약 | 없음 |
| 최대 16자 | 16자 ✓ |
| — | **최소 2자** (문서에 없는 코드 전용 규칙) |

공백이 허용 문자 집합 밖이라 `" ab"` 는 "사용할 수 없는 문자가 포함되어 있습니다" 로 거절된다. 사용자 입장에서는 원인을 알 수 없다.

**수정**: `NicknameValidator.Normalize(string)` 추가(Trim + 연속 공백 축약) → `Validate` 전에 적용.
최소 길이·특수문자 차단은 **기획 확정 후 문서에 올린다**(`ui-plan.md` 3-4).

---

### U-9 🟡 방 코드 길이가 두 곳에 하드코딩

`LobbyPanel`: `joinPopup.CharacterLimit = 4` / `Core/RandomCode.GenerateRandomCode(int length = 4)`

**수정**: `RandomCode.Length` 상수 하나로 모으고 양쪽에서 참조. (`08-room.md` 5절에서 자릿수가 확정되면 여기 한 줄만 바꾸면 된다.)

---

### U-10 🟡 `InputPopup` 이 제출 후 닫히지 않는다

`LobbyPanel.OnJoinRoomClicked` 의 submit 콜백은 `JoinRoom` 만 호출한다. 참가에 성공하면 씬이 바뀌면서 U-1과 겹쳐 유령 UI가 된다.

**수정**: 제출 시 팝업을 닫고 로딩 표시로 전환. 실패하면(U-4) 에러와 함께 다시 연다.

---

### U-11 🟡 `WarningPopup` 이 스스로 닫지 않는다

```csharp
private void ClickedOkButton()
{
    // PopupManager.Instance.CloseModal(this);   ← 주석 처리
    OnClickedOkButton?.Invoke();
}
```

모든 호출처가 "닫기"를 직접 기억해야 한다. 한 곳이라도 빠지면 닫히지 않는 모달이 된다.

**수정**: 기본 동작을 **닫기 + 콜백**으로. 닫지 않아야 하는 경우만 옵션으로 둔다.

---

### U-12 🟡 방장 표시 / 시작 버튼이 한 박자 늦을 수 있다

**위치**: `UI/RoomInfoPanel.cs`, `UI/RoomPanel.cs`

둘 다 `RoomEvents.OnPlayerEntered / OnPlayerLeft` 만 구독하고 **마스터 전환 이벤트를 구독하지 않는다.**
PUN 의 `OnMasterClientSwitched` 와 `OnPlayerLeftRoom` 의 호출 순서는 보장되지 않으므로, 방장이 나간 직후 `(Host)` 표기와 시작 버튼 표시가 어긋날 수 있다.

**수정**: `Network/Events/RoomEvents` 에 `OnMasterClientSwitched` 추가(**A 소유 파일 → 요청**) 후 양쪽에서 구독.
`08-room.md` 5절 #5(방장 이탈 처리)가 확정되면 함께 반영한다.

---

### U-13 🟡 방 로그가 무한히 쌓인다

**위치**: `UI/RoomInfoPanel.AddLog`

```csharp
logText.text += "\n" + text;   // 첫 줄부터 빈 줄, 상한 없음, 스크롤 고정 없음
```

**수정**: `Queue<string>` + 최대 줄 수(제안 50) + `string.Join` 재조립 + 새 줄 추가 시 `ScrollRect.verticalNormalizedPosition = 0`.
표시할 이벤트 목록은 기획 확정 대기(`08-room.md` 5절 #4) — 지금은 현재 문구(입장/퇴장)를 유지한다.

---

## 2. 규약 위반 (`CLAUDE.md` 12·13절 / `CODE_CONVENTION.md` 4.5)

### U-14 🔴 `async void` — `NicknameInput.LoadScene()`

```csharp
private async void LoadScene()   // 금지 항목
{
    var op = SceneManager.LoadSceneAsync(...);
    op.allowSceneActivation = false;
    while (op.progress < 0.9f) await Task.Yield();
    op.allowSceneActivation = true;
}
```

예외가 삼켜지고, 취소 경로가 없으며, `allowSceneActivation = false` 상태에서 예외가 나면 **영구 정지**한다.
→ U-18 `SceneLoader` 로 대체하면서 코루틴으로 바꾼다.

### U-15 🟡 네임스페이스가 타입 이름을 가린다

`Chapchu.UI.Popup` 네임스페이스 안에 `Popup` 클래스가 있어 `class LoadingPopup : Popup.Popup` 같은 표현이 나온다.
`CODE_CONVENTION.md` 4.2 가 경고하는 패턴(`Scripts/UI/Room/` 금지와 같은 이유)이다.

| 현재 | 변경 |
| --- | --- |
| `UI/Popup/` (`Chapchu.UI.Popup`) | `UI/Popups/` (`Chapchu.UI.Popups`) |
| `UI/Popup/PopupScripts/` | `UI/Popups/` 로 평탄화 |
| `UI/Components/LoadingPopup.cs` | `UI/Popups/LoadingPopup.cs` |
| `UI/NickName/` | `UI/Title/` (타이틀 화면 전용으로 재편) |

> **`.cs` 와 `.cs.meta` 를 반드시 함께 옮긴다.** GUID 가 보존되어야 씬·프리팹 참조가 끊기지 않는다. (Phase 1에서 같은 방식으로 처리한 선례가 있다)

### U-16 🟡 필드 명명 혼재

`ProfilePresenter._view`, `ProfileView._nicknameText` 는 `[SerializeField] private` 인데 `_` 접두사를 쓴다.
컨벤션 1절 표에서 `[SerializeField] private` 는 **접두사 없는 camelCase** 다. UI 폴더 안에서 통일한다.

### U-17 🟡 사용하지 않는 `using`

거의 모든 UI 파일에 `System.Collections`, `UnityEngine.SceneManagement`, `Chapchu.UI.Components` 등 템플릿 잔재가 남아 있다. 일괄 정리하되 **저장 인코딩이 UTF-8(BOM) 에서 바뀌지 않는지 확인**한다(`CLAUDE.md` 13절).

---

## 3. 죽은 코드 / 중복

### U-18 🟠 씬 로딩이 3곳에 흩어져 있고 방식이 제각각

| 위치 | 방식 |
| --- | --- |
| `NicknameInput.LoadScene` | `LoadSceneAsync` + `allowSceneActivation` (`async void`) |
| `LobbyPanel.OnJoinedRoom` | `SceneManager.LoadScene(ScenePaths.Get(...))` |
| `RoomPanel.OnLeftRoom` | `SceneManager.LoadScene(ScenePaths.Get(DebugLobby))` |
| `RoomPanel.OnStartClicked` | `PhotonNetwork.LoadLevel(ScenePaths.GetName(Game))` |

`ScenePaths.Get()` 이 넘기는 `"Scenes/RoomScene"` 은 빌드 세팅 경로(`Assets/Scenes/RoomScene.unity`)도, 씬 이름(`RoomScene`)도 아닌 **부분 경로**다.
→ **먼저 에디터에서 타이틀→로비→방 전환을 1회 확인한다.** 동작 여부와 무관하게, 형식이 하나로 고정되지 않은 점 자체가 이미 한 번 사고를 냈다(`PhotonNetwork.LoadLevel` 무한 재로드, `bb43c4e`).

**수정**: `Core/SceneLoader.cs` 신규 — UI는 이것만 호출한다.

```csharp
public static class SceneLoader
{
    /// <summary>로컬 씬 전환. Photon 동기화가 필요한 전환은 마스터가 PhotonNetwork.LoadLevel 을 쓴다.</summary>
    public static void Load(SceneType type) => SceneManager.LoadScene(ScenePaths.GetName(type));

    /// <summary>로딩 팝업을 띄운 채 비동기 전환. 코루틴이므로 호출자가 MonoBehaviour 여야 한다.</summary>
    public static IEnumerator LoadAsync(SceneType type, Action onLoaded = null) { ... }
}
```

노션 타이틀 스펙 9절의 `SceneLoader.Load("MainMenu")` 와도 형태가 맞는다.

### U-19 🟡 죽은 스크립트 5개 삭제

GUID 역검색 결과 씬·프리팹 참조가 **전부 0** 이다.

| 파일 | 삭제 사유 |
| --- | --- |
| `UI/JoinRoomPanel.cs` | `InputPopup` 경로로 대체됨 |
| `UI/JoinRoomSubmitLogic.cs` | 위와 동일 |
| `UI/Interfaces/ISubmitLogic.cs` | 구현체가 위 둘뿐 |
| `UI/Interfaces/IButtonLogic.cs` | 구현체 0 |
| `UI/NickName/NicknameInputLogic.cs` | **`NicknameValidator` 와 규칙이 다른 두 번째 검증 구현.** 방치하면 반드시 갈라진다 |

### U-20 🟡 `PopupManager.Get<T>()` 의 암묵적 규약

`Resources/Popups/{타입명}` 규칙이 코드에만 있고, 프리팹 루트에 컴포넌트가 없으면 **조용히 `null`** 을 돌려준다. U-3 이 이 방식으로 숨어 있었다.

**수정**: `null` 검사 + 원인이 보이는 에러 로그.

```csharp
public static T Get<T>() where T : Popup
{
    var prefab = Resources.Load<T>($"Popups/{typeof(T).Name}");
    if (prefab == null)
        Debug.LogError($"[PopupManager] Resources/Popups/{typeof(T).Name} 없음 또는 루트에 {typeof(T).Name} 컴포넌트 미부착");
    return prefab;
}
```

---

## 4. 구조 정리

### U-21 🟠 타이틀 화면에 책임 분리가 없다

`NicknameInput` 하나가 **입력 검증 + Photon 닉네임 설정 + 씬 로딩 + 팝업 호출**을 전부 한다.
프로젝트는 이미 `ProfilePresenter` / `ProfileView` 로 MVP 를 쓰고 있고, 노션 타이틀 스펙도 `TitlePresenter` 를 전제로 쓰여 있다.

**수정**

| 클래스 | 책임 |
| --- | --- |
| `TitleView` | TMP·버튼 참조만 보유. `SetError` / `SetCount` / `SetSubmitInteractable` / `SetNickname` |
| `TitlePresenter` | `NicknameValidator` 호출, `NetworkManager` 요청, `ConnectionEvents` 구독, `SceneLoader` 호출 |

> **Model 은 만들지 않는다.** 화면 상태가 "입력값 + 에러 문구" 뿐이라 계층을 하나 더 두면 KISS 위반이다.
> 노션 스펙의 인라인 `ErrorText`(레이아웃 점프 방지)와 16자 도달 시 카운터 색상 전환이 `TitleView` 에 들어간다.

### U-22 🟡 `RoomPanel` 의 이벤트 핸들러가 중복이다

```csharp
RoomEvents.OnPlayerEntered += OnRoomStateChanged;      // → RefreshStartButton
RoomEvents.OnPlayerEntered += UpdateStartButtonState;  // → interactable
```

같은 이벤트에 두 핸들러를 붙여 겹치는 일을 두 번 한다. 게다가 `RefreshStartButton` 은 `onClick.RemoveAllListeners()` 로 **인스펙터에서 연결한 리스너까지 지운다.**

**수정**: 핸들러 1개로 통합. `onClick` 등록은 `Awake` 에서 **1회만**, 이후에는 `SetActive` / `interactable` 만 갱신.

### U-23 🟡 UI 가 네트워크 전역 설정을 만진다

`RoomPanel.Awake` 의 `PhotonNetwork.AutomaticallySyncScene = true` 는 `PhotonConnection.Initialize()` 와 **중복**이며, 역할 분담상 UI(B)가 건드릴 값이 아니다.

**수정**: Network 계층 한 곳으로 일원화하고 UI 에서 제거(**A 소유 파일 → 요청**).
단, `PhotonConnection.Initialize()` 의 조기 반환 버그(U-6)가 고쳐지기 전에 UI 쪽만 지우면 **인게임 진입이 깨진다.** 반드시 A 의 수정과 같은 PR 이거나 그 이후여야 한다.

### U-24 🟡 `RoomInfoPanel` 이 표시와 조립을 같이 한다

플레이어 목록을 `TMP_Text` 하나에 문자열로 누적한다. 방장 표시·좌석·준비 상태가 붙으면 바로 한계가 온다.

**수정(이번 범위)**: 목록 조립을 `RoomPlayerListView` 로 분리하고, 항목 프리팹 도입은 `ui-plan.md` R1(M1)에서.

---

## 5. 작업 순서 (PR 단위)

한 PR 이 끝날 때마다 **컴파일 에러 0 + 타이틀→로비→방 왕복 1회**가 게이트다.

| PR | 내용 | 항목 | 검증 |
| --- | --- | --- | --- |
| **PR1** | 팝업 인프라 복구 | U-1, U-2, U-3, U-11, U-20 | 토스트가 3초 후 자동으로 닫힌다 / 모달을 띄운 채 씬 전환 시 잔재가 없다 |
| **PR2** | 죽은 코드 제거 · 폴더/네임스페이스 정리 | U-15, U-16, U-17, U-19 | 컴파일 0, 씬·프리팹 참조 유지(`.meta` 동반 이동) |
| **PR3** | `SceneLoader` 도입 | U-14, U-18 | 3개 씬 전환 + 로딩 팝업 표시/해제 |
| **PR4** | 타이틀 재구성 | U-6, U-7, U-8, U-21 | 닉네임 복원 / 오류가 인라인 표시 / 접속 실패 시 재시도 가능 |
| **PR5** | 로비 | U-4, U-5, U-9, U-10 | 없는 코드 입력 시 사유 표시 + 버튼 복구 / 랜덤매치 동작(또는 숨김) |
| **PR6** | 방 | U-12, U-13, U-22, U-23, U-24 | 방장 이탈 시 표기·시작 버튼 즉시 갱신 / 로그 상한 동작 |

**A(로직) 에게 요청해야 하는 항목** — 파일 소유가 `Network/` · `Core/` 쪽이다.

1. `RoomEvents.OnMasterClientSwitched` 추가 (U-12)
2. `PhotonConnection.SetupInitNickname()` 삭제 (U-7)
3. `PhotonConnection.Initialize()` 조기 반환 + `OnDisconnected` 분기 (U-6, U-23) — **PR6 전에 끝나야 한다**

---

## 6. 이번에 하지 않는 것

| 항목 | 이유 |
| --- | --- |
| Canvas 규격 통일 (800×600 → 1920×1080) | 화면 방향 확정 대기. `ui-plan.md` 2-1 의 별도 PR (`Resources/Bootstrap/PopupManager.prefab` 의 캔버스도 같이 바꿔야 한다) |
| 씬 레이아웃 · 9-slice · 폰트 교체 | `ui-plan.md` M1. 코드 리팩토링과 섞으면 PR 리뷰가 불가능해진다 |
| `GameScene` / `Presentation/**` | 범위 제외 |
| `ProfilePresenter` 의 닉네임 실시간 갱신 | 닉네임 변경 경로 자체가 아직 없다. 생길 때 함께 (YAGNI) |
| 설정 · 사운드 화면 | 기획 미확정 (M5) |

---

## 7. 문서 반영

| 문서 | 수정 |
| --- | --- |
| `systems/10-ui.md` 8절 | `ToastPopup` "구현됨" → **"미구현"** (U-3) |
| `systems/10-ui.md` 10절 | 중복 UI 시스템(`GameUIManager`/`GamePlayerInfoUI`) 행 삭제 — 이미 제거됨 |
| `systems/08-room.md` | 방장 이탈 시 동작을 확정으로 올릴지 결정 (U-12) |
| `refactoring-plan.md` | Phase 6(UI) 항목을 이 문서로 연결 |
| 노션 「타이틀 화면 개발 문서」 9절 | 샘플 코드의 `SceneLoader.Load("MainMenu")` → 실제 도입되는 `SceneLoader.Load(SceneType.Lobby)` 로 갱신 (U-18) |
