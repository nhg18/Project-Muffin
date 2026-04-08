using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonRoom
{
    public readonly RoomService service;
    public readonly RoomCallback callback;

    public PhotonRoom()
    {
        service = new RoomService();
        callback = new RoomCallback(service);
    }
}
