using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SeatManager : Singleton<SeatManager>
{
    [SerializeField] private PlayerSeat[] playerSeats = new PlayerSeat[4];
    
    private readonly Dictionary<int, PlayerSeat> _playerSeats = new(); // ActorNumber, PlayerSeat
    
    private void OnEnable()
    {
        RoomEvents.OnPlayerLeft += HandlePlayerLeft;
    }

    private void Start()
    {
        ApplyPlayerSeats();
        SetupSeatUI();
    }

    private void OnDisable()
    {
        RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
    }

    public void UpdateSeatUI()
    {
        foreach (var (actorNumber, seatView) in _playerSeats)
        {
            var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);

            if (player == null)
            {
                Debug.Log($"Player {actorNumber} has no player in CurrentRoom");
                continue;
            }
            
            bool isMyTurn = actorNumber == TurnManager.Instance.CurrentTurnActor;
            seatView.SetTurnUI(isMyTurn);
        }
    }
    
    /// <summary>
    /// 자신의 Actor 번호 기준으로 좌석배치하는 알고리즘
    /// </summary>
    /// <param name="myActorNumber"> 자신의 Actor번호, PhotonNetwork.LocalPlayer.ActorNumber </param>
    /// <returns> int, int : ActorNumber, SeatIndex </returns>
    public Dictionary<int, int> GetSeatAssignments(int myActorNumber)
    {
        Dictionary<int, int> result = new();
        List<int> others = new();
        
        // CurrentRoom.PlayerCount 말고 게임 클래스 만들어서 PlayerCount 만들어야 함. 현재는 중도 난입이 없다는 가정하에 안전
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        foreach (var kvp in PhotonNetwork.CurrentRoom.Players)
        {
            if (kvp.Key == myActorNumber)
            {
                result.Add(kvp.Key, 0); // 본인은 항상 1번 자리(인덱스 0)
            }
            else
            {
                others.Add(kvp.Key);
            }
        }

        // 시간복잡도 최악 O(NlogN) = O(4.6..) = O(1)
        others.Sort();
        
        for (var i = 0; i < others.Count; i++)
        {
            // 2명일 때는 상대방을 3번 자리(인덱스 2)에 고정
            // 3~4명일 때는 2번 자리(인덱스 1)부터 순차적으로 배치
            int seatIndex = (playerCount == 2) ? 2 : (i + 1);
            result.Add(others[i], seatIndex);
        }
        
        return result;
    }

    private void ApplyPlayerSeats()
    {
        var myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        var seatAssignments = GetSeatAssignments(myActorNumber);
        
        foreach (var (targetPlayer, seatIndex) in seatAssignments)
        {
            _playerSeats.Add(targetPlayer, playerSeats[seatIndex]);
            playerSeats[seatIndex].PlayerActorNumber = targetPlayer;//수정부분!!
        }
    }

    private void HandlePlayerLeft(Player player)
    {
        if (!_playerSeats.TryGetValue(player.ActorNumber, out PlayerSeat seatView)) return;
        
        if (seatView)
        {
            Destroy(seatView.gameObject);
        }
        
        _playerSeats.Remove(player.ActorNumber);
    }
    
    private void SetupSeatUI()
    {
        foreach (var (actorNumber, seatView) in _playerSeats)
        {
            seatView.gameObject.SetActive(true);
            var player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);

            if (player == null)
            {
                Debug.Log($"Player {actorNumber} has no player in CurrentRoom");
                continue;
            }
            
            seatView.SetNicknameUI(player.NickName);
            bool isMyTurn = actorNumber == TurnManager.Instance.CurrentTurnActor;
            seatView.SetTurnUI(isMyTurn);
        }
    }

}
