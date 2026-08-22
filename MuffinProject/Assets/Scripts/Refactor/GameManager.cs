using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Refactor;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    private readonly Dictionary<int, Player> _players = new ();

    protected override void Awake()
    {
        base.Awake();
        
        var photonPlayers = PhotonNetwork.CurrentRoom.Players;
        foreach (var kvp in photonPlayers)
        {
            var actorNumber = kvp.Key;
            var nickname = kvp.Value.NickName;
            var player = new Player(actorNumber, nickname, 100);
            _players.Add(kvp.Key, player);
        }
    }

    private void OnEnable()
    {
        RoomEvents.OnPlayerLeft += HandlePlayerLeft;
    }

    private void OnDisable()
    {
        RoomEvents.OnPlayerLeft -= HandlePlayerLeft;
    }

    private void HandlePlayerLeft(Photon.Realtime.Player player)
    {
        if (_players.Remove(player.ActorNumber))
        {
            Debug.Log($"{player.NickName} ${player.ActorNumber} be removed successfully");
        }
        else
        {
            Debug.Log($"{player.NickName} ${player.ActorNumber} not removed");
        }
    }
    
}
