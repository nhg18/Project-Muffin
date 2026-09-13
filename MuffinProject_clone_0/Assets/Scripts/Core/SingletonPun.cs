using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;


public abstract class StaticInstancePun<T> : MonoBehaviourPunCallbacks where T : MonoBehaviourPunCallbacks
{
    public static T Instance { get; private set; }
    protected virtual void Awake() => Instance = this as T;

    protected virtual void OnApplicationQuit()
    {
        Instance = null;
        Destroy(gameObject);
    }

    protected virtual void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}

public abstract class SingletonPun<T> : StaticInstancePun<T> where T : MonoBehaviourPunCallbacks
{
    protected override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        base.Awake();
    }
}

public abstract class SingletonPersistentPun<T> : SingletonPun<T> where T : MonoBehaviourPunCallbacks
{
    protected override void Awake()
    {   
        DontDestroyOnLoad(gameObject);
        base.Awake();
    }
}