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
        private bool _creatingSolo; // 「혼자 하기」 방을 만드는 중 — 코드가 겹쳐 다시 만들 때도 같은 종류로

        public void JoinRoom(string roomName)
        {
            if (!PhotonConnection.ValidConnect) return;
            
            if (string.IsNullOrEmpty(roomName))
            {
                RoomEvents.RaiseJoinRoomFailed(ErrorCode.InvalidOperation, "방 코드가 없습니다.");
                return;
            }
            PhotonNetwork.JoinRoom(roomName.Trim().ToUpper());
        }
    
        public void JoinRandomRoom()
        {
            if (!PhotonConnection.ValidConnect) return;
            PhotonNetwork.JoinRandomRoom();
        }
    
        public void LeaveRoom()
        {
            PhotonNetwork.LeaveRoom();
        }
    
        public void CreateRoom()
        {
            if (!PhotonConnection.ValidConnect) return;

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
            if (!PhotonConnection.ValidConnect) return;

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

            int[] numbers = BotRoster.Numbers;
            if (numbers.Length >= BotRoster.MaxBots || BotRoster.MemberCount >= NetworkManager.MaxPlayers)
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

            int[] numbers = BotRoster.Numbers;
            if (!numbers.Contains(number))
                return false;

            SetBots(numbers.Where(n => n != number).ToArray());
            return true;
        }

        // 방장만 · 게임 시작 전만 (시작하면 방을 닫는다 — 08-room.md 4절)
        private static bool CanEditBots() => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.IsOpen;

        private static void SetBots(int[] numbers)
        {
            Room room = PhotonNetwork.CurrentRoom;
            room.MaxPlayers = NetworkManager.MaxPlayers - numbers.Length;
            room.SetCustomProperties(BotRoster.BotsTable(numbers));
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
            RoomEvents.RaisePlayerEntered(player);
        }

        public void OnPlayerLeft(Player player)
        {
            RoomEvents.RaisePlayerLeft(player);
        }

        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable changed)
        {
            if (changed.ContainsKey(RoomProps.Bots))
                RoomEvents.RaiseBotsChanged(BotRoster.Numbers);
        }

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            RoomEvents.RaiseRoomListUpdate(roomList);
        }

        /// <summary>
        /// 방장 전환시 호출되는 콜백 함수
        /// 방장 이탈 시 처리(08-room.md 5절 #5)는 미정이라 통지만 한다.
        /// </summary>
        public void OnMasterClientSwitched(Player newMasterClient)
        {
            Debug.LogWarning($"[{nameof(PhotonRoom)}] 방장 교체 → {newMasterClient.NickName} (방장 이탈 처리는 미정, 09 9절)");
            RoomEvents.RaiseMasterClientSwitched(newMasterClient);
        }
    }
}
