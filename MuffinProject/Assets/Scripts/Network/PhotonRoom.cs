using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Chapchu.Core;
using Chapchu.Network.Events;

namespace Chapchu.Network
{
    /// <summary>방 만들기 · 입장 · 나가기 요청과 그 콜백 (08-room.md). NetworkManager 가 갖고 있고, 결과는 RoomEvents 로 알린다.</summary>
    public class PhotonRoom
    {
        // 연결이 없어 요청을 보내지 못함 — 실패로 알려 화면이 대기 표시를 닫고 「그 밖의 실패」 안내를 띄우게 (08-room.md 7절)
        private const short NotConnectedCode = ErrorCode.OperationNotAllowedInCurrentState;
        private const string NotConnectedMessage = "서버에 연결되어 있지 않습니다.";

        private bool _creatingSolo; // 「혼자 하기」 방을 만드는 중 — 코드가 겹쳐 다시 만들 때도 같은 종류로

        // 방장이 써 놓고 서버 확인을 아직 못 받은 봇 목록. PUN 은 확인이 돌아온 뒤에야 내 기기의 방 속성을 바꾼다
        private int[] _pendingBots;

        // 방장이 정한 봇 목록 — 원본 (09-network.md 3절). 방장이 아니면 null. 방 속성은 아무 클라나 쓸 수 있어 이것과 다르면 되돌린다
        private int[] _hostBots;

        /// <summary>방장이 정한 봇 목록 (서버 확인 전 포함). 방장이 아니면 null — 읽기는 BotRoster.Numbers 로.</summary>
        internal int[] HostBots => _hostBots;

        public void JoinRoom(string roomName)
        {
            if (!PhotonConnection.ValidConnect)
            {
                RoomEvents.RaiseJoinRoomFailed(NotConnectedCode, NotConnectedMessage);
                return;
            }

            if (string.IsNullOrEmpty(roomName))
            {
                RoomEvents.RaiseJoinRoomFailed(ErrorCode.InvalidOperation, "방 코드가 없습니다.");
                return;
            }
            PhotonNetwork.JoinRoom(roomName.Trim().ToUpper());
        }
    
        public void JoinRandomRoom()
        {
            if (!PhotonConnection.ValidConnect)
            {
                RoomEvents.RaiseJoinRandomFailed(NotConnectedCode, NotConnectedMessage);
                return;
            }
            PhotonNetwork.JoinRandomRoom();
        }
    
        public void LeaveRoom()
        {
            PhotonNetwork.LeaveRoom();
        }
    
        public void CreateRoom()
        {
            if (!PhotonConnection.ValidConnect)
            {
                RoomEvents.RaiseCreateRoomFailed(NotConnectedCode, NotConnectedMessage);
                return;
            }

            _creatingSolo = false;
            RoomOptions options = CreateRoomOptions(NetworkManager.MaxPlayers, true, true);
            string code = RandomCode.GenerateRandomCode();

            PhotonNetwork.CreateRoom(code.Trim().ToUpper(), options);
        }

        /// <summary>
        /// 「혼자 하기」 (18-bot.md 10절) — 봇 3 이 든 방을 만든다. 랜덤 매칭에서 빠지고(비공개) 코드 입장은 된다.
        /// 봇 3 이면 정원이 1 이라 가득 찬다. 자동 시작하지 않는다.
        /// </summary>
        public void CreateSoloRoom()
        {
            if (!PhotonConnection.ValidConnect)
            {
                RoomEvents.RaiseCreateRoomFailed(NotConnectedCode, NotConnectedMessage);
                return;
            }

            _creatingSolo = true;
            int[] bots = Enumerable.Range(1, BotRoster.MaxBots).ToArray();
            RoomOptions options = CreateRoomOptions(NetworkManager.MaxPlayers - bots.Length, false, true);
            options.CustomRoomProperties = new ExitGames.Client.Photon.Hashtable { [RoomProps.Bots] = bots, [RoomProps.Solo] = true };
            string code = RandomCode.GenerateRandomCode();

            PhotonNetwork.CreateRoom(code.Trim().ToUpper(), options);
        }

        /// <summary>
        /// 봇 추가 (18-bot.md 5절). 방장 · 게임 시작 전 · 봇 3 미만 · 빈 칸이 있을 때. 번호는 비어 있는 가장 작은 번호.
        /// 봇 1 당 사람 정원을 1 줄여 봇 자리에 사람이 들어오지 못하게 한다 (3절 #8). 결과는 RoomEvents.OnBotsChanged.
        /// </summary>
        public bool AddBot()
        {
            if (!CanEditBots())
                return false;

            int[] numbers = CurrentBots;
            if (numbers.Length >= BotRoster.MaxBots || PhotonNetwork.CurrentRoom.PlayerCount + numbers.Length >= NetworkManager.MaxPlayers)
                return false;

            int number = 1;
            while (numbers.Contains(number))
                number++;

            SetBots(numbers.Append(number).OrderBy(n => n).ToArray());
            return true;
        }

        /// <summary>봇 제거 (18-bot.md 5절). 방장 · 게임 시작 전. 자리가 다시 열린다.</summary>
        public bool RemoveBot(int number)
        {
            if (!CanEditBots())
                return false;

            int[] numbers = CurrentBots;
            if (!numbers.Contains(number))
                return false;

            SetBots(numbers.Where(n => n != number).ToArray());
            return true;
        }

        // 방장만 · 게임 시작 전만 (시작하면 방을 닫는다 — 08-room.md 4절)
        private static bool CanEditBots() => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.IsOpen;

        // 방장이 정한 원본 — 서버 확인 전이어도 방금 쓴 목록이라 「제거」 직후 「추가」 가 지운 봇을 되살리지 않는다
        private int[] CurrentBots => _hostBots ?? BotRoster.Numbers;

        private void SetBots(int[] numbers)
        {
            Room room = PhotonNetwork.CurrentRoom;
            room.MaxPlayers = NetworkManager.MaxPlayers - numbers.Length;
            if (room.SetCustomProperties(BotRoster.BotsTable(numbers)))
                _pendingBots = _hostBots = numbers;
        }

        // 방장이 되면 지금 방의 봇 목록(거른 사본)을 원본으로 이어받는다 (18-bot.md 9절 「방장 교체 (방 대기 중)」)
        private void AdoptBots()
        {
            _hostBots = null;
            if (PhotonNetwork.IsMasterClient)
                _hostBots = BotRoster.Numbers;
        }
    
        public RoomOptions CreateRoomOptions(int maxPlayers, bool isVisible, bool isOpen)
        {
            return new RoomOptions
            {
                MaxPlayers = maxPlayers,
                IsVisible = isVisible,
                IsOpen = isOpen,
            };
        }
    
        public void UpdateRoomOptions(bool isVisible, bool isOpen)
        {
            if (!PhotonNetwork.InRoom)
            {
                Debug.LogWarning($"[{nameof(PhotonRoom)}] 방 안에서만 방 설정을 바꿀 수 있다");
                return;
            }
            PhotonNetwork.CurrentRoom.IsVisible = isVisible;
            PhotonNetwork.CurrentRoom.IsOpen = isOpen;
        }
    
    
        /// <summary>
        /// 룸 생성 완료시 호출되는 콜백 함수
        /// </summary>
        public void OnCreatedRoom()
        {
            RoomEvents.RaiseCreatedRoom();
        }

        /// <summary>
        /// 룸 생성 실패시 호출되는 콜백 함수
        /// </summary>
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[{nameof(PhotonRoom)}] 방 만들기 실패 [{returnCode}]: {message}");

            switch (returnCode)
            {
                // 랜덤 코드로 방 생성시 겹치는 문제시 CreateRoom 함수 호출
                case 32766:
                    if (_creatingSolo)
                        CreateSoloRoom();
                    else
                        CreateRoom();
                    return;
            }
            RoomEvents.RaiseCreateRoomFailed(returnCode, message);
        }
    
        /// <summary>
        /// 룸 참가시 호출되는 콜백 함수
        /// 룸 씬 로드
        /// </summary>
        public void OnJoinedRoom()
        {
            AdoptBots();
            RoomEvents.RaiseJoinedRoom();
        }
    
        /// <summary>
        /// 룸 나가기시 호출되는 콜백 함수
        /// </summary>
        public void OnLeftRoom()
        {
            // 지난 판의 내 상태(체력 · 손패 장수)를 지운다. PUN 은 방에 들어갈 때 내 CustomProperties 를 같이 보내서,
            // 지우지 않으면 다음 판 시작 때 지난 판 값이 그대로 보인다.
            PhotonNetwork.LocalPlayer.CustomProperties.Clear();
            _pendingBots = null;
            _hostBots = null;

            RoomEvents.RaiseLeftRoom();
        }
    
        /// <summary>
        /// 룸 참가 실패시 호출되는 콜백 함수
        /// </summary>
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[{nameof(PhotonRoom)}] 방 입장 실패: {message}");
            RoomEvents.RaiseJoinRoomFailed(returnCode, message);
        }

        /// <summary>
        /// 랜덤 룸 참가 실패시 호출되는 콜백 함수
        /// </summary>
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.LogWarning($"[{nameof(PhotonRoom)}] 랜덤 입장 실패 — 새 방을 만든다: {message}");
            CreateRoom();
        }

        public void OnPlayerEntered(Player player)
        {
            // 봇 추가와 사람 입장이 서버에서 엇갈리면 사람 + 봇이 정원을 넘는다 — 넘친 봇을 뺀다 (08-room.md 2절 · 18-bot.md 3절 #8)
            if (CanEditBots())
            {
                int[] numbers = CurrentBots;
                int seats = NetworkManager.MaxPlayers - PhotonNetwork.CurrentRoom.PlayerCount;
                if (numbers.Length > seats)
                    SetBots(numbers.Take(seats).ToArray());
            }

            RoomEvents.RaisePlayerEntered(player);
        }

        public void OnPlayerLeft(Player player)
        {
            RoomEvents.RaisePlayerLeft(player);
        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changed)
        {
            if (!changed.ContainsKey(RoomProps.Bots))
                return;

            // 마지막으로 쓴 목록까지 서버가 확인해 주면 대기를 푼다
            int[] inRoom = BotRoster.RoomNumbers;
            if (_pendingBots != null && inRoom.SequenceEqual(_pendingBots))
                _pendingBots = null;

            // 확인 대기가 없는데 원본과 다르면 방장이 쓴 값이 아니다 — 원본과 정원으로 다시 쓴다 (09-network.md 2절 #3 · 18-bot.md 3절 #7)
            if (PhotonNetwork.IsMasterClient && _pendingBots == null && _hostBots != null && !inRoom.SequenceEqual(_hostBots))
            {
                Debug.LogWarning($"[{nameof(PhotonRoom)}] 방장이 쓰지 않은 봇 목록 [{string.Join(",", inRoom)}] — 원본 [{string.Join(",", _hostBots)}] 으로 되돌린다");
                SetBots(_hostBots);
            }
            RoomEvents.RaiseBotsChanged(BotRoster.Numbers);
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            RoomEvents.RaiseRoomListUpdate(roomList);
        }

        /// <summary>
        /// 방장 전환시 호출되는 콜백 함수
        /// 방장 이탈 시 처리(08-room.md 5절 #5)는 미정이라 통지만 한다. 봇 목록은 새 방장이 이어받는다 (18-bot.md 9절).
        /// </summary>
        public void OnMasterClientSwitched(Player newMasterClient)
        {
            Debug.LogWarning($"[{nameof(PhotonRoom)}] 방장 교체 → {newMasterClient.NickName} (방장 이탈 처리는 미정, 09 9절)");
            AdoptBots();
            RoomEvents.RaiseMasterClientSwitched(newMasterClient);
        }
    }
}
