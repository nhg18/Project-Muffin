using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnection
{
    public ConnectionService service { get; }
    public ConnectionCallback callback { get; }

    public PhotonConnection()
    {
        service = new ConnectionService();
        callback = new ConnectionCallback(service);
    }
}
