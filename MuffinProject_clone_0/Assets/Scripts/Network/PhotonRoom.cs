using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Network
{
    public class PhotonRoom
    {
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
            
            RoomOptions options = CreateRoomOptions(NetworkManager.MaxPlayers, true, true);
            string code = RandomCode.GenerateRandomCode();
        
            PhotonNetwork.CreateRoom(code.Trim().ToUpper(), options);
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
                Debug.LogWarning("Must be in room");
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
            Debug.Log("On Created Room" + PhotonNetwork.CurrentRoom.Name);
            RoomEvents.RaiseCreatedRoom();
        }

        /// <summary>
        /// 룸 생성 실패시 호출되는 콜백 함수
        /// </summary>
        public void OnCreateRoomFailed(short returnCode, string message)
        {
            Debug.Log($"On Create Room Failed [{returnCode}]: {message}");

            switch (returnCode)
            {
                // 랜덤 코드로 방 생성시 겹치는 문제시 CreateRoom 함수 호출
                case 32766:
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
            Debug.Log("On Joined Room");
            RoomEvents.RaiseJoinedRoom();
        }
    
        /// <summary>
        /// 룸 나가기시 호출되는 콜백 함수
        /// 타이틀 씬 로드
        /// </summary>
        public void OnLeftRoom()
        {
            Debug.Log("On Left Room");
            RoomEvents.RaiseLeftRoom();
        }
    
        /// <summary>
        /// 룸 참가 실패시 호출되는 콜백 함수
        /// </summary>
        public void OnJoinRoomFailed(short returnCode, string message)
        {
            Debug.Log($"On Join Room Failed: {message}");
            RoomEvents.RaiseJoinRoomFailed(returnCode, message);
        }

        /// <summary>
        /// 랜덤 룸 참가 실패시 호출되는 콜백 함수
        /// </summary>
        public void OnJoinRandomFailed(short returnCode, string message)
        {
            Debug.Log($"On Join Random Room Failed: {message}");
            Debug.Log("No Empty Room -> Create New Room");
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

        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            RoomEvents.RaiseRoomListUpdate(roomList);
        }
    }
}
