using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SeatManager : Singleton<SeatManager>
{
    [SerializeField] private PlayerSeat playerSeatPrefab;
    [SerializeField] private RectTransform[] seatPositions;
    
    private readonly Dictionary<Player, PlayerSeat> _playerSeats = new();
    
    private void OnEnable()
    {
        RoomEvents.OnPlayerLeft += HandlePlayerLeft;
        GameEvents.OnTurnChanged += UpdateSeatUI;
    }

    private void Start()
    {
        CreateSeats();
        UpdateSeatUI();
    }

    private void OnDisable()
    {
        RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
        GameEvents.OnTurnChanged -= UpdateSeatUI;
    }

    public void UpdateSeatUI()
    {
        foreach (var (player, seatView) in _playerSeats)
        {
            seatView.SetNicknameUI(player.NickName);
            
            bool isMyTurn = player.ActorNumber == TurnManager.Instance.CurrentTurnActor;
            seatView.SetTurnUI(isMyTurn);
        }
    }
    
    private Dictionary<Player, int> GetSeatAssignments(int myActorNumber)
    {
        Dictionary<Player, int> result = new();
        List<Player> others = new();
        
        // CurrentRoom.PlayerCount 말고 게임 클래스 만들어서 PlayerCount 만들어야 함. 현재는 중도 난입이 없다는 가정하에 안전
        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == myActorNumber)
            {
                result.Add(player, 0); // 본인은 항상 1번 자리(인덱스 0)
            }
            else
            {
                others.Add(player);
            }
        }

        // 시간복잡도 최악 O(NlogN) = O(4.6..) = O(1)
        others.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
        
        for (var i = 0; i < others.Count; i++)
        {
            // 2명일 때는 상대방을 3번 자리(인덱스 2)에 고정
            // 3~4명일 때는 2번 자리(인덱스 1)부터 순차적으로 배치
            int seatIndex = (playerCount == 2) ? 2 : (i + 1);
            result.Add(others[i], seatIndex);
        }
        
        return result;
    }

    private void CreateSeats()
    {
        var myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        var seatAssignments = GetSeatAssignments(myActorNumber);
        
        foreach (var (targetPlayer, seatIndex) in seatAssignments)
        {
            var seatView = Instantiate(playerSeatPrefab, seatPositions[seatIndex]);
            _playerSeats.Add(targetPlayer, seatView);
        }
    }

    private void HandlePlayerLeft(Player player)
    {
        if (!_playerSeats.TryGetValue(player, out PlayerSeat seatView)) return;
        
        if (seatView)
        {
            Destroy(seatView.gameObject);
        }
        
        _playerSeats.Remove(player);
    }
}
