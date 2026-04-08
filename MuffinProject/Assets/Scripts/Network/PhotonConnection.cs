using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnection
{
    public readonly PhotonConnectionService service = new PhotonConnectionService();
    public readonly PhotonConnectionCallback callback = new PhotonConnectionCallback();
}
