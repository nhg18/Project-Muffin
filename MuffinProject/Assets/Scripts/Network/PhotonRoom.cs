using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PhotonRoom
{
    public readonly PhotonRoomService service;
    public readonly PhotonRoomCallback callback;

    public PhotonRoom()
    {
        service = new PhotonRoomService();
        callback = new PhotonRoomCallback(service);
    }
}
