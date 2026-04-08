using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnection
{
    public PhotonConnectionService service { get; }
    public PhotonConnectionCallback callback { get; }

    public PhotonConnection()
    {
        service = new PhotonConnectionService();
        callback = new PhotonConnectionCallback(service);
    }
}
