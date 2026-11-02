using Photon.Pun;
using UnityEngine;

namespace Chapchu.Core
{
    /// <summary>씬에 하나 있는 컴포넌트를 Instance 로 연다. 중복은 막지 않는다 — 막으려면 Singleton.</summary>
    public abstract class StaticInstance<T> : MonoBehaviour where T : MonoBehaviour
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

    /// <summary>이미 Instance 가 있으면 새로 생긴 쪽을 지운다.</summary>
    public abstract class Singleton<T> : StaticInstance<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            base.Awake();
        }
    }

    /// <summary>Singleton + 씬이 바뀌어도 남는다. 전역 매니저용 — GameBootstrap 이 만든다.</summary>
    public abstract class SingletonPersistent<T> : Singleton<T> where T : MonoBehaviour
    {
        protected override void Awake()
        {
            DontDestroyOnLoad(gameObject);
            base.Awake();
        }
    }
}
