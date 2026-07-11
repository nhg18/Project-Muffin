using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SeatManager : Singleton<SeatManager>
{
    [SerializeField] private PlayerSeatView playerSeatPrefab;
    [SerializeField] private RectTransform[] seatPositions;
    
    private readonly List<PlayerSeatView> _playerSeats = new();
    
    private void Start()
    {
        CreateSeats();
    }
    
    public int GetSeatIndex(int myActorNumber, int targetActorNumber, int playerCount)
    {
        return (targetActorNumber - myActorNumber + playerCount) % playerCount;
    }
    
    public Dictionary<Player, int> GetSeatAssignments(int myActorNumber)
    {
        Dictionary<Player, int> result = new();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            var seatIndex = GetSeatIndex(myActorNumber, player.ActorNumber, PhotonNetwork.CurrentRoom.PlayerCount);
            result.Add(player, seatIndex);
        }
        
        return result;
    }

    public void CreateSeats()
    {
        var myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
        var seatAssignments = GetSeatAssignments(myActorNumber);
        
        foreach (var (targetPlayer, seatIndex) in seatAssignments)
        {
            _playerSeats.Add(Instantiate(playerSeatPrefab, seatPositions[seatIndex]));
        }
    }

    public void UpdateSeatUI()
    {
        
    }
}
