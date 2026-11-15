using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>Photon 콜백을 받는 StaticInstance (MonoBehaviourPunCallbacks).</summary>
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

    /// <summary>Photon 콜백을 받는 Singleton.</summary>
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

    /// <summary>Photon 콜백을 받는 SingletonPersistent.</summary>
    public abstract class SingletonPersistentPun<T> : SingletonPun<T> where T : MonoBehaviourPunCallbacks
    {
        protected override void Awake()
        {   
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }
    }
}
