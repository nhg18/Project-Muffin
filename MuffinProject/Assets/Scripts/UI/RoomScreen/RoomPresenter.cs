using System;
using System.Collections.Generic;
using System.Linq;
using Chapchu.Core;
using Chapchu.Network;
using Chapchu.Network.Events;
using Chapchu.UI.Lobby;
using Chapchu.UI.Popups;
using Chapchu.UI.Title;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Chapchu.UI.RoomScreen
{
    /// <summary>
    /// 방 화면의 뷰들과 네트워크 · 씬 전환을 잇는다. Scripts/UI/RoomScreen 에서 네트워크를 아는 유일한 파일이다.
    /// 칸 · 인원 · 공개 여부 · 시작 자리는 방 상태가 바뀔 때마다 Refresh 한 곳에서 다시 그린다.
    /// 기준 문서: docs/systems/16-room-ui-full.md, docs/systems/08-room.md 4 · 5절, docs/systems/18-bot.md 5 · 8-1절
    /// </summary>
    public class RoomPresenter : MonoBehaviour
    {
        // 16-room-ui-full.md 8절 문구
        private const string CountFormat = "{0} / {1}";
        private const string EnteredLogFormat = "{0}님이 입장했습니다.";
        private const string LeftLogFormat = "{0}님이 퇴장했습니다.";
        private const string HostChangedLogFormat = "{0}님이 방장이 되었습니다.";
        private const string PublicLog = "공개로 변경";
        private const string PrivateLog = "비공개로 변경";
        private const string BotAddedLogFormat = "봇 {0} 추가";
        private const string BotRemovedLogFormat = "봇 {0} 제거";
        private const string BecameHostToast = "방장이 되었습니다. 2명 이상이면 시작할 수 있어요.";
        private const string FallbackNickname = "플레이어";

        [Header("위")]
        [SerializeField] private Button leaveButton;
        [SerializeField] private RoomCodeCardView codeCard;
        [SerializeField] private TMP_Text playerCountText;

        [Header("가운데")]
        [Tooltip("플레이어 칸 4개 — 왼쪽부터 입장 순서")]
        [SerializeField] private RoomSlotView[] slots;

        [Header("아래")]
        [SerializeField] private RoomLogView log;
        [SerializeField] private RoomVisibilityView visibility;
        [SerializeField] private RoomStartView start;

        [Header("판")]
        [SerializeField] private RoomLeaveConfirmView leaveConfirm;
        [SerializeField] private RoomPlayerInfoView playerInfo;

        [Tooltip("화면 전체를 덮어 입력을 막는 투명 판 — 시작 중에만 켠다")]
        [SerializeField] private GameObject inputLock;

        // 칸에 앉은 순서 — 사람(입장 순) 뒤에 봇(번호 순). slots 와 같은 순서다 (16-room-ui-full 3절 #6 · 18-bot 8-3)
        private readonly List<Seat> _seats = new List<Seat>();

        // 방 입장 시 1회 초기화. 씬이 먼저 뜨고 OnJoinedRoom 이 나중에 오는 경우(AutomaticallySyncScene 으로 끌려온 참가자)와
        // 이미 방에 있는 채로 씬이 뜨는 경우(방장) 둘 다 여기로 모은다.
        private bool _initialized;

        // 방이 닫혔다 = 방장이 시작을 눌렀다. 전원 화면이 「게임을 시작합니다」 가 되고 입력이 잠긴다
        private bool _isLocked;
        private bool _isLeaving;

        // 공개 여부 · 열림 · 봇 목록의 지난 값 — 바뀐 것을 찾아 로그 · 잠금에 쓴다
        private bool _wasPublic;
        private bool _wasOpen;
        private int[] _bots = Array.Empty<int>();

        private readonly struct Seat
        {
            public readonly int ActorNumber;
            public readonly string Nickname;
            public readonly bool IsBot;

            public Seat(int actorNumber, string nickname, bool isBot)
            {
                ActorNumber = actorNumber;
                Nickname = nickname;
                IsBot = isBot;
            }
        }

        private void Awake()
        {
            if (leaveButton == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(leaveButton)} 참조가 비어 있습니다.", this);

            if (codeCard == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(codeCard)} 참조가 비어 있습니다.", this);

            if (playerCountText == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(playerCountText)} 참조가 비어 있습니다.", this);

            if (slots == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(slots)} 참조가 비어 있습니다.", this);
            else
            {
                if (slots.Length != NetworkManager.MaxPlayers)
                    Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(slots)} 는 {NetworkManager.MaxPlayers}칸이어야 합니다 (지금 {slots.Length}칸).", this);

                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] == null)
                        Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(slots)}[{i}] 참조가 비어 있습니다.", this);
                }
            }

            if (log == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(log)} 참조가 비어 있습니다.", this);

            if (visibility == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(visibility)} 참조가 비어 있습니다.", this);

            if (start == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(start)} 참조가 비어 있습니다.", this);

            if (leaveConfirm == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(leaveConfirm)} 참조가 비어 있습니다.", this);

            if (playerInfo == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(playerInfo)} 참조가 비어 있습니다.", this);

            if (inputLock == null)
                Debug.LogError($"[{nameof(RoomPresenter)}] {nameof(inputLock)} 참조가 비어 있습니다.", this);
        }

        private void OnEnable()
        {
            leaveButton.onClick.AddListener(HandleLeaveClicked);
            start.OnStartRequested += HandleStartRequested;
            visibility.OnChangeRequested += HandleVisibilityChangeRequested;
            leaveConfirm.OnLeaveConfirmed += HandleLeaveConfirmed;
            foreach (RoomSlotView slot in slots)
            {
                slot.OnClicked += HandleSlotClicked;
                slot.OnAddBotRequested += HandleAddBotRequested;
                slot.OnRemoveBotRequested += HandleRemoveBotRequested;
            }

            RoomEvents.OnPlayerEntered += HandlePlayerEntered;
            RoomEvents.OnPlayerLeft += HandlePlayerLeft;
            // 방장이 나가면 PUN 이 새 방장을 정한다. OnPlayerLeft 와의 순서가 보장되지 않으므로 둘 다에서 갱신한다.
            RoomEvents.OnMasterClientSwitched += HandleMasterClientSwitched;
            RoomEvents.OnBotsChanged += HandleBotsChanged;
            RoomEvents.OnJoinedRoom += HandleJoinedRoom;
            RoomEvents.OnLeftRoom += HandleLeftRoom;
        }

        private void OnDisable()
        {
            leaveButton.onClick.RemoveListener(HandleLeaveClicked);
            start.OnStartRequested -= HandleStartRequested;
            visibility.OnChangeRequested -= HandleVisibilityChangeRequested;
            leaveConfirm.OnLeaveConfirmed -= HandleLeaveConfirmed;
            foreach (RoomSlotView slot in slots)
            {
                slot.OnClicked -= HandleSlotClicked;
                slot.OnAddBotRequested -= HandleAddBotRequested;
                slot.OnRemoveBotRequested -= HandleRemoveBotRequested;
            }

            RoomEvents.OnPlayerEntered -= HandlePlayerEntered;
            RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
            RoomEvents.OnMasterClientSwitched -= HandleMasterClientSwitched;
            RoomEvents.OnBotsChanged -= HandleBotsChanged;
            RoomEvents.OnJoinedRoom -= HandleJoinedRoom;
            RoomEvents.OnLeftRoom -= HandleLeftRoom;
        }

        private void Start()
        {
            if (PhotonNetwork.InRoom)
                Initialize();
        }

        private void Update()
        {
            if (!_initialized || _isLeaving || !PhotonNetwork.InRoom)
                return;

            SyncRoomOptions();

            // Android 뒤로가기 · PC Esc (16-room-ui-full 3절 #12)
            if (Input.GetKeyDown(KeyCode.Escape))
                HandleBackPressed();
        }

        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            // 로비가 10초 시간 초과로 포기한 요청의 늦은 입장 — 씬 동기화로 끌려왔으므로 바로 나가 로비로 돌아간다 (14-lobby-ui 6절)
            if (SceneFlow.IsRoomRequestAbandoned)
            {
                SceneFlow.IsRoomRequestAbandoned = false;
                Leave();
                return;
            }

            Room room = PhotonNetwork.CurrentRoom;
            _wasPublic = room.IsVisible;
            _wasOpen = room.IsOpen;
            _bots = BotRoster.Numbers;

            // 로그는 내가 들어온 뒤의 일만 가진다 — 먼저 와 있던 사람은 칸으로만 보인다 (7-9)
            codeCard.SetCode(room.Name);
            log.Add(string.Format(EnteredLogFormat, DisplayName(NetworkManager.Nickname)));
            SetLocked(!room.IsOpen);
        }

        #region 다시 그리기

        private void Refresh()
        {
            if (!PhotonNetwork.InRoom) return;

            Room room = PhotonNetwork.CurrentRoom;
            bool isHost = PhotonNetwork.IsMasterClient;
            bool canEditBots = isHost && !_isLocked;
            int[] bots = BotRoster.Numbers;

            // PlayerList 는 ActorNumber 순 — 들어온 순서대로 칸을 채운다. 봇은 그 뒤에 번호 순.
            _seats.Clear();
            foreach (Player player in PhotonNetwork.PlayerList)
                _seats.Add(new Seat(player.ActorNumber, DisplayName(player.NickName), false));
            foreach (int number in bots)
                _seats.Add(new Seat(BotRoster.ActorOf(number), BotRoster.NicknameOf(BotRoster.ActorOf(number)), true));

            bool canAddBot = canEditBots && bots.Length < BotRoster.MaxBots && _seats.Count < NetworkManager.MaxPlayers;
            for (int i = 0; i < slots.Length; i++)
            {
                if (i >= _seats.Count)
                    slots[i].ShowEmpty(canAddBot);
                else if (_seats[i].IsBot)
                    slots[i].ShowBot(_seats[i].Nickname, canEditBots);
                else
                    slots[i].ShowPlayer(_seats[i].Nickname, _seats[i].ActorNumber == room.MasterClientId, _seats[i].ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
            }

            // 인원은 봇을 포함해 센다 (18-bot 8-1). 분모는 방 정원이 아니라 칸 수 — 봇이 들면 방 정원은 줄어든다
            playerCountText.text = string.Format(CountFormat, _seats.Count, NetworkManager.MaxPlayers);
            codeCard.SetPublic(room.IsVisible);
            visibility.Set(room.IsVisible, isHost, _isLocked);

            if (_isLocked)
                start.ShowStarting();
            else if (isHost)
                start.ShowHost(CanStartGame());
            else
                start.ShowGuest();

            // 열린 채 방장이 바뀌면 위임 안내도 지금 방장 기준으로 맞춘다. 다른 사람(봇 제외)이 없어졌으면 판을 닫는다 (7-8)
            if (leaveConfirm.IsOpen)
            {
                if (room.PlayerCount > 1)
                    leaveConfirm.Show(isHost);
                else
                    leaveConfirm.Hide();
            }

            // 정보 판을 보던 사람이 나갔으면 판을 닫는다 (7-5). 남아 있으면 방장 여부를 다시 맞춘다 (3절 #5)
            if (playerInfo.IsOpen)
            {
                int shown = _seats.FindIndex(seat => seat.ActorNumber == playerInfo.ActorNumber);
                if (shown < 0)
                    playerInfo.Hide();
                else
                    playerInfo.Show(_seats[shown].ActorNumber, _seats[shown].Nickname, _seats[shown].ActorNumber == room.MasterClientId,
                        _seats[shown].ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
            }
        }

        // 공개 여부 · 열림은 방 속성이라 바뀐 것을 알려 주는 이벤트가 없다 — 지난 값과 견줘 찾는다
        private void SyncRoomOptions()
        {
            Room room = PhotonNetwork.CurrentRoom;
            if (room.IsVisible != _wasPublic)
            {
                _wasPublic = room.IsVisible;
                log.Add(_wasPublic ? PublicLog : PrivateLog);
                Refresh();
            }

            if (room.IsOpen != _wasOpen)
            {
                _wasOpen = room.IsOpen;
                SetLocked(!_wasOpen);
            }
        }

        // 시작 중 — 「게임을 시작합니다」 로 바꾸고 나가기 · 코드 · 칸 · 토글 입력을 잠근다. 열린 판 · 로그는 닫는다 (6절 · 7-12)
        private void SetLocked(bool locked)
        {
            _isLocked = locked;
            inputLock.SetActive(locked);
            LobbyStyle.SetEnabled(leaveButton, !locked);
            codeCard.SetLocked(locked);

            if (locked)
            {
                leaveConfirm.Hide();
                playerInfo.Hide();
                log.Collapse();
            }

            Refresh();
        }

        // 08-room.md 5절 #2: 최소 인원 2명 (준비 상태 없음) — 봇 포함 (08 2절 · 18-bot 4절)
        private static bool CanStartGame() => BotRoster.MemberCount >= NetworkManager.MinPlayers;

        // 서식 태그 · 제어 문자를 거른 닉네임(NicknameValidator.ForDisplay), 비면 「플레이어」 (16-room-ui-full 9절)
        private static string DisplayName(string nickname)
        {
            string name = NicknameValidator.ForDisplay(nickname);
            return name.Length == 0 ? FallbackNickname : name;
        }

        #endregion

        #region 방 상태 통지

        private void HandleJoinedRoom() => Initialize();

        private void HandlePlayerEntered(Player player)
        {
            log.Add(string.Format(EnteredLogFormat, DisplayName(player.NickName)));
            Refresh();
        }

        private void HandlePlayerLeft(Player player)
        {
            log.Add(string.Format(LeftLogFormat, DisplayName(player.NickName)));
            Refresh();
        }

        private void HandleMasterClientSwitched(Player newMaster)
        {
            log.Add(string.Format(HostChangedLogFormat, DisplayName(newMaster.NickName)));
            if (newMaster.IsLocal)
                PopupManager.Instance.ShowToast(BecameHostToast);

            Refresh();
        }

        private void HandleBotsChanged(int[] numbers)
        {
            foreach (int number in numbers.Except(_bots))
                log.Add(string.Format(BotAddedLogFormat, number));
            foreach (int number in _bots.Except(numbers))
                log.Add(string.Format(BotRemovedLogFormat, number));

            _bots = numbers;
            Refresh();
        }

        private void HandleLeftRoom()
        {
            // 들어온 곳으로 돌아간다. Lobby 에서 왔으면 Lobby, DebugLobby 에서 왔으면 DebugLobby.
            SceneManager.LoadScene(SceneFlow.ReturnSceneAfterRoom);
        }

        #endregion

        #region 입력

        private void HandleStartRequested()
        {
            if (_isLocked || _isLeaving || !PhotonNetwork.IsMasterClient || !CanStartGame()) return;

            // 방을 닫는다 — 공개 여부는 그대로 둔다 (08-room.md 5절 #8). 닫힌 것을 보고 전원 화면이 잠긴다
            NetworkManager.Instance.UpdateRoomOptions(PhotonNetwork.CurrentRoom.IsVisible, isOpen: false);
            SyncRoomOptions();

            // 방 전원이 함께 넘어가야 하므로 LoadScene 이 아닌 LoadLevel (ScenePaths 설명 참고).
            PhotonNetwork.LoadLevel(SceneFlow.GameSceneAfterRoom);
        }

        private void HandleVisibilityChangeRequested(bool isPublic)
        {
            if (_isLocked || !PhotonNetwork.IsMasterClient) return;

            // 로그 · 글자는 바뀐 방 속성을 보고 전원 화면에서 같이 바뀐다 (SyncRoomOptions)
            NetworkManager.Instance.UpdateRoomOptions(isPublic, PhotonNetwork.CurrentRoom.IsOpen);
        }

        private void HandleAddBotRequested()
        {
            if (_isLocked) return;

            // 방장은 서버 확인 전에도 자기가 정한 목록을 읽는다 — 칸은 바로, 로그는 OnBotsChanged 에서
            if (NetworkManager.Instance.AddBot())
                Refresh();
        }

        private void HandleRemoveBotRequested(RoomSlotView slot)
        {
            if (_isLocked) return;

            Seat seat = _seats[Array.IndexOf(slots, slot)];
            if (NetworkManager.Instance.RemoveBot(seat.ActorNumber - BotRoster.ActorBase))
                Refresh();
        }

        private void HandleSlotClicked(RoomSlotView slot)
        {
            if (_isLocked) return;

            Seat seat = _seats[Array.IndexOf(slots, slot)];
            playerInfo.Show(seat.ActorNumber, seat.Nickname, seat.ActorNumber == PhotonNetwork.CurrentRoom.MasterClientId,
                seat.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber);
        }

        private void HandleLeaveClicked()
        {
            if (_isLocked || _isLeaving) return;

            // 다른 사람(봇 제외)이 있을 때만 묻는다. 혼자면 바로 나간다 (7-8)
            if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
                leaveConfirm.Show(PhotonNetwork.IsMasterClient);
            else
                Leave();
        }

        private void HandleLeaveConfirmed() => Leave();

        // 열린 것부터 닫고, 닫을 것이 없으면 「나가기」 와 같다. 시작 중에는 무시
        private void HandleBackPressed()
        {
            if (_isLocked) return;

            if (log.IsExpanded)
                log.Collapse();
            else if (playerInfo.IsOpen)
                playerInfo.Hide();
            else if (leaveConfirm.IsOpen)
                leaveConfirm.Hide();
            else
                HandleLeaveClicked();
        }

        private void Leave()
        {
            _isLeaving = true;
            NetworkManager.Instance.LeaveRoom();
        }

        #endregion
    }
}
