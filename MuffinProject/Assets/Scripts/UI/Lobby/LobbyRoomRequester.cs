using System.Collections;
using System.Threading.Tasks;
using Chapchu.Account;
using Chapchu.Core;
using Chapchu.Network;
using Chapchu.Network.Events;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using ErrorCode = Photon.Realtime.ErrorCode; // Photon.Realtime 에도 AccountService 가 있어 통째로 가져오지 않는다

namespace Chapchu.UI.Lobby
{
    /// <summary>
    /// 로비의 방 요청 — 혼자 하기 · 방 만들기 · 코드로 참가 · 랜덤 매칭 · 초대 수락. 결과는 RoomEvents 로 오고, 10초 안에 응답이 없으면 실패로 알린다.
    /// 요청을 보내기 전에 설정 값을 다시 읽는다 — 앱을 켜 둔 사이 최소 버전이 올랐거나 점검이 시작됐으면 타이틀로 돌려보낸다 (23-login 7-6).
    /// 기준: docs/systems/08-room.md 7절 · 14-lobby-ui.md 6절 · 17-lobby-ui-full.md 5-1 · 22-popup-ui 3절 · 23-login.md 7-6
    /// </summary>
    public class LobbyRoomRequester : MonoBehaviour
    {
        private enum RoomRequest { Random, Create, Solo, Join }

        // 08-room 7절 · 22-popup-ui 3절 — 확정 문구
        private const string SearchingText = "방을 찾는 중…";
        private const string CreatingText = "방을 만드는 중…";
        private const string JoiningText = "방에 들어가는 중…";
        private const string CreateFailedTitle = "방 생성 실패";
        private const string JoinFailedTitle = "방 참가 실패";
        private const string RandomMatchFailedTitle = "랜덤 매칭 실패";
        private const string RoomNotFoundText = "방을 찾을 수 없어요.";
        private const string RoomFullText = "방이 가득 찼어요.";
        private const string RoomClosedText = "이미 시작된 방이에요.";
        private const string NoRandomRoomText = "참가할 수 있는 방이 없어요.";
        private const string NoRoomCodeText = "방 코드를 입력해 주세요.";
        private const string RetryLaterText = "잠시 후 다시 시도해 주세요.";

        // 방에 가기 전 확인 (23-login 7-6) — 타이틀 팝업과 같은 글
        private const string UpdateTitle = "업데이트 필요";
        private const string UpdateText = "새 버전으로 업데이트해 주세요.";
        private const string MaintenanceTitle = "점검 중";

        private const float RoomRequestTimeoutSeconds = 10f; // 14-lobby-ui 6절
        private const float ConfigRefreshSeconds = 30f;      // 23-login 4절 — 이 로비에서 마지막으로 읽은 지 이 안이면 다시 읽지 않는다

        [SerializeField] private LobbyHomeView home;
        [SerializeField] private LobbyModalView modal;
        [SerializeField] private LobbyInviteBannerView invite;
        [SerializeField] private LobbySectionPresenter sections;

        private RoomRequest _request;
        private Coroutine _roomTimeout; // 방 요청의 응답을 기다리는 동안만 있다
        private float _configReadAt = float.NegativeInfinity; // 이 로비에서 설정 값을 마지막으로 다시 읽기 시작한 때 (실패한 읽기도 센다)

        // 초대 배너가 떠 있는 동안 플레이 버튼은 눌리지 않는다 (17 5-1)
        private bool CanPlay => !invite.IsShown && !sections.IsMoving && !modal.IsLoading;

        private void Awake()
        {
            if (home == null || modal == null || invite == null || sections == null)
                Debug.LogError($"[{nameof(LobbyRoomRequester)}] 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            home.OnSoloRequested += HandleSoloRequested;
            home.OnCreateRequested += HandleCreateRoomRequested;
            home.OnCodeRequested += HandleJoinRoomRequested;
            home.OnRandomRequested += HandleRandomMatchRequested;
            modal.OnInputSubmitted += HandleInputSubmitted;
            invite.OnAccepted += HandleInviteAccepted;

            RoomEvents.OnJoinedRoom += HandleJoinedRoom;
            RoomEvents.OnCreateRoomFailed += HandleCreateRoomFailed;
            RoomEvents.OnJoinRoomFailed += HandleJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed += HandleJoinRandomFailed;
        }

        private void OnDisable()
        {
            home.OnSoloRequested -= HandleSoloRequested;
            home.OnCreateRequested -= HandleCreateRoomRequested;
            home.OnCodeRequested -= HandleJoinRoomRequested;
            home.OnRandomRequested -= HandleRandomMatchRequested;
            modal.OnInputSubmitted -= HandleInputSubmitted;
            invite.OnAccepted -= HandleInviteAccepted;

            RoomEvents.OnJoinedRoom -= HandleJoinedRoom;
            RoomEvents.OnCreateRoomFailed -= HandleCreateRoomFailed;
            RoomEvents.OnJoinRoomFailed -= HandleJoinRoomFailed;
            RoomEvents.OnJoinRandomFailed -= HandleJoinRandomFailed;
        }

        private void Start()
        {
            // 돌아갈 씬 · 시작할 게임 씬은 로비에 들어온 순간 정한다.
            SceneFlow.ReturnSceneAfterRoom = ScenePaths.Lobby;
            SceneFlow.GameSceneAfterRoom = ScenePaths.Game;
        }

        #region 요청

        private void HandleRandomMatchRequested()
        {
            if (CanPlay)
                RequestRoom(RoomRequest.Random);
        }

        private void HandleCreateRoomRequested()
        {
            if (CanPlay)
                RequestRoom(RoomRequest.Create);
        }

        private void HandleSoloRequested()
        {
            if (CanPlay)
                RequestRoom(RoomRequest.Solo);
        }

        private void HandleJoinRoomRequested()
        {
            if (CanPlay)
                modal.ShowInput(LobbyInputKind.RoomCode);
        }

        // 입력 창은 닉네임 · 계정 삭제에도 쓰인다 — 방 코드일 때만 받는다
        private void HandleInputSubmitted(LobbyInputKind kind, string roomCode)
        {
            if (kind != LobbyInputKind.RoomCode)
                return;

            modal.HideInput();
            if (string.IsNullOrEmpty(roomCode))
                modal.ShowWarn(JoinFailedTitle, NoRoomCodeText);
            else
                RequestRoom(RoomRequest.Join, roomCode);
        }

        // 친구 서버가 없어 초대한 친구의 방이 실제로는 없다 — 방이 사라졌을 때의 문구로 끝난다 (17 8절)
        private void HandleInviteAccepted() => modal.ShowWarn(JoinFailedTitle, RoomNotFoundText);

        private void RequestRoom(RoomRequest request, string roomCode = null)
        {
            if (modal.IsLoading)
                return;

            _request = request;
            StartCoroutine(RequestRoutine(request, roomCode));
        }

        // 설정 값 다시 읽기 → (업데이트 · 점검이면 타이틀로) → 요청 보내기. 로딩 창은 처음부터 그 요청의 글로 뜬다
        private IEnumerator RequestRoutine(RoomRequest request, string roomCode)
        {
            modal.ShowLoading(request switch
            {
                RoomRequest.Random => SearchingText,
                RoomRequest.Join   => JoiningText,
                _                  => CreatingText,
            });

            yield return RefreshConfigRoutine();

            // 앱을 켜 둔 사이 최소 버전이 올랐거나 점검이 시작됐다 — 요청을 보내지 않고 타이틀로 돌려보낸다. 닫을 수 없는 팝업은 타이틀이 띄운다 (23-login 7-6)
            if (AccountService.IsSignedIn && ServerConfig.HasValues && (ServerConfig.NeedsUpdate || ServerConfig.IsMaintenance))
            {
                modal.HideLoading();
                bool update = ServerConfig.NeedsUpdate;
                modal.ShowWarn(update ? UpdateTitle : MaintenanceTitle, update ? UpdateText : ServerConfig.MaintenanceMessage, onOk: ReturnToTitle);
                yield break;
            }

            // 서버가 새 요청을 받을 수 있다 = 포기한 옛 요청은 끝났다. 받을 수 없으면(옛 요청이 아직 처리 중) 표시를 그대로 둔다 —
            // 새 요청은 그 자리에서 실패하고, 옛 요청의 늦은 입장은 계속 막는다
            if (NetworkManager.IsReady)
                SceneFlow.IsRoomRequestAbandoned = false;
            // 요청이 부르는 자리에서 바로 실패할 수 있어, 요청보다 먼저 센다
            _roomTimeout = StartCoroutine(RoomTimeoutRoutine());
            switch (request)
            {
                case RoomRequest.Random:
                    NetworkManager.Instance.JoinRandomRoom();
                    break;
                case RoomRequest.Create:
                    NetworkManager.Instance.CreateRoom();
                    break;
                case RoomRequest.Solo:
                    NetworkManager.Instance.CreateSoloRoom();
                    break;
                default:
                    NetworkManager.Instance.JoinRoom(roomCode);
                    break;
            }
        }

        // 이 로비에서 마지막으로 읽은 지 30초가 지났으면 설정 값을 다시 읽는다. 읽지 못하면 지난 값으로 간다 — 읽기 실패가 방에 가는 것을 막지 않는다.
        // 로그인하지 않은 실행(디버그 로비)은 읽지 않는다
        private IEnumerator RefreshConfigRoutine()
        {
            if (!AccountService.IsSignedIn || Time.realtimeSinceStartup - _configReadAt < ConfigRefreshSeconds)
                yield break;

            _configReadAt = Time.realtimeSinceStartup;
            Task fetch = ServerConfig.FetchAsync();
            float waited = 0f;
            while (!fetch.IsCompleted && waited < ServerConfig.FetchTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, AccountService.MaxCountedFrameSeconds);
                yield return null;
            }

            if (!fetch.IsCompleted || fetch.IsFaulted)
                Debug.LogWarning($"[{nameof(LobbyRoomRequester)}] 설정 값 읽기 실패 — 지난 값으로 간다: {fetch.Exception?.GetBaseException().Message ?? "시간 초과"}");
        }

        private static void ReturnToTitle() => SceneManager.LoadScene(ScenePaths.Title);

        #endregion

        #region 결과

        private void HandleJoinedRoom()
        {
            if (_roomTimeout == null) // 10초가 지나 이미 실패로 알린 요청의 늦은 입장 — 방으로 가지 않고 나간다
            {
                SceneFlow.IsRoomRequestAbandoned = false; // 포기한 요청이 끝났다
                NetworkManager.Instance.LeaveRoom();
                return;
            }

            StopCoroutine(_roomTimeout); // 입장했다 — 방 씬이 열리기 전에 시간 초과가 끼어들지 않게 멈춘다
            // 봇은 Photon 플레이어가 아니다 — PlayerCount 가 곧 사람 수 (18-bot)
            AnalyticsRecorder.RecordRoomEntered(ToVia(_request), PhotonNetwork.CurrentRoom.IsVisible, PhotonNetwork.CurrentRoom.PlayerCount);
            SceneManager.LoadScene(ScenePaths.Room);
        }

        // 혼자 하기(봇 방)도 방 만들기다. 초대 수락은 친구 서버가 붙으면 Invite 로 (27 5-2)
        private static AnalyticsRecorder.RoomVia ToVia(RoomRequest request)
        {
            switch (request)
            {
                case RoomRequest.Random:
                    return AnalyticsRecorder.RoomVia.QuickStart;
                case RoomRequest.Join:
                    return AnalyticsRecorder.RoomVia.Code;
                default:
                    return AnalyticsRecorder.RoomVia.Create;
            }
        }

        // 랜덤 매칭이 방을 못 찾으면 바로 새 방을 만든다 — 그 생성이 실패한 경우도 여기로 온다
        private void HandleCreateRoomFailed(short code, string message)
            => ShowRoomError(_request == RoomRequest.Random ? RandomMatchFailedTitle : CreateFailedTitle, code, message);

        private void HandleJoinRoomFailed(short code, string message) => ShowRoomError(JoinFailedTitle, code, message);

        // 랜덤 매칭을 보내지도 못했을 때(미연결)만 온다
        private void HandleJoinRandomFailed(short code, string message) => ShowRoomError(RandomMatchFailedTitle, code, message);

        private void ShowRoomError(string title, short code, string message)
        {
            if (_roomTimeout == null) // 10초가 지나 이미 실패로 알린 요청의 늦은 응답 — 포기한 요청이 끝났다
            {
                SceneFlow.IsRoomRequestAbandoned = false;
                return;
            }

            StopCoroutine(_roomTimeout);
            _roomTimeout = null;
            modal.HideLoading();
            modal.ShowWarn(title, GetFailureMessage(code, message));
        }

        // 10초 안에 응답이 없으면 실패로 알린다 (14-lobby-ui 6절)
        private IEnumerator RoomTimeoutRoutine()
        {
            float waited = 0f;
            while (waited < RoomRequestTimeoutSeconds)
            {
                waited += Mathf.Min(Time.unscaledDeltaTime, AccountService.MaxCountedFrameSeconds);
                yield return null;
            }

            // 입장 응답은 이미 왔고 방 씬이 열리는 중이다 — 포기로 찍지 않고 기다린다
            while (NetworkManager.HasEnteredRoom)
                yield return null;

            _roomTimeout = null;
            // 늦은 입장 응답은 HandleJoinedRoom 이 받아 나간다. 로비가 그 전에 사라졌으면 방 화면이 보고 나간다
            SceneFlow.IsRoomRequestAbandoned = true;
            modal.HideLoading();
            modal.ShowWarn(_request switch
            {
                RoomRequest.Random => RandomMatchFailedTitle,
                RoomRequest.Join   => JoinFailedTitle,
                _                  => CreateFailedTitle,
            }, RetryLaterText);
        }

        // 08-room.md 7절. Photon 원문(message)은 우리 코드가 직접 만든 사유(InvalidOperation)일 때만 그대로 쓴다.
        private static string GetFailureMessage(short code, string message) => code switch
        {
            ErrorCode.GameDoesNotExist   => RoomNotFoundText,
            ErrorCode.GameFull           => RoomFullText,
            ErrorCode.GameClosed         => RoomClosedText,
            ErrorCode.NoRandomMatchFound => NoRandomRoomText,
            ErrorCode.InvalidOperation   => message,
            _                            => RetryLaterText,
        };

        #endregion
    }
}
